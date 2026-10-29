using System;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CoubDownloader.Configurations;
using CoubDownloader.Extensions;
using CoubDownloader.Models;
using CoubDownloader.Services;
using Newtonsoft.Json;

namespace CoubDownloader;

public class Crawler
{
    private readonly Configuration _configuration;
    private readonly HttpClient _http;
    private readonly AuthToken _authToken;
    private readonly CustomLists _customLists;
    private Pager _pager = new();
    private const string CustomList = "custom";
    private const string TimelineUrl = "https://coub.com/api/coub-site/timeline";

    private static readonly Regex ChannelUrlRegex = new(@"^(?:https?://)?(?:www\.)?coub\.com/([^/?#\s]+)/?$", RegexOptions.IgnoreCase);

    public Crawler(Configuration configuration)
    {
        _configuration = configuration;
        _http = CoubHttp.Create(configuration);
        _authToken = new AuthToken(_http);
        _customLists = new CustomLists(new CoubPageReader(configuration, _http));
    }

    public string InfoPath => CoubPaths.InfoPath;

    /// <summary>
    ///     Gathers coubs for all entered entries and returns names of directories to download
    /// </summary>
    public async Task<string[]> CrawlUrlsAsync(string[] entries)
    {
        var directories = new List<string>();
        var pastedUrls = new List<string>();

        foreach (var entry in entries)
        {
            var category = entry.ToLower();

            if (category == Constants.LikedCategory)
            {
                await DownloadScopedCoubsAsync(Constants.LikedCategory, "likes");
                directories.Add(Constants.LikedCategory);
            }
            else if (category == Constants.BookmarksCategory)
            {
                await DownloadScopedCoubsAsync(Constants.BookmarksCategory, "bookmarks");
                directories.Add(Constants.BookmarksCategory);
            }
            else if (File.Exists(entry))
            {
                var name = CustomLists.GetDirectory(Path.GetFileNameWithoutExtension(entry));
                await _customLists.GatherAsync(name, await File.ReadAllLinesAsync(entry), true);
                directories.Add(name);
            }
            else if (entry.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleEx.WriteLineColor($"[File '{entry}' was not found.]", ConsoleColor.Yellow);
            }
            else if (CustomLists.IsCoubUrl(entry))
            {
                pastedUrls.Add(entry);
            }
            else if (CustomLists.IsOwnList(category))
            {
                // Folder with URL list created by hand in Coubs-info, which was the way to do it in older versions
                var urls = await File.ReadAllLinesAsync(CoubPaths.GetDataPath(category, Constants.UrlListFileName));
                await _customLists.GatherAsync(category, urls, false);
                directories.Add(category);
            }
            else
            {
                // Channel can be entered as its username or as its URL
                var channelUrl = ChannelUrlRegex.Match(entry);
                var channel = channelUrl.Success ? channelUrl.Groups[1].Value.ToLower() : category;

                await DownloadChannelCoubsAsync(channel);
                directories.Add(channel);
            }
        }

        if (pastedUrls.Any())
        {
            await _customLists.GatherAsync(CustomList, pastedUrls, true);
            directories.Add(CustomList);
        }

        return directories.Distinct().ToArray();
    }

    public Task ResolveUrlListsAsync()
    {
        return _customLists.ResolveAllAsync();
    }

    private async Task DownloadChannelCoubsAsync(string channel)
    {
        if (ShouldSkipDownloaded(channel))
        {
            return;
        }

        var channelId = await GetChannelIdAsync(channel);
        if (channelId == null)
        {
            ConsoleEx.WriteLineColor($"[Channel with name '{channel}' does not exist.]", ConsoleColor.Yellow);
            return;
        }

        var query = GetTimelineQuery("channel", $"&channel_id={channelId}");
        await DownloadLinksAsync(query, channel, null, channel);
    }

    private async Task<string> GetChannelIdAsync(string channel)
    {
        using var response = await _http.GetAsync($"https://coub.com/{channel}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        // If there is some other error, let it go up and stop crawling
        response.EnsureSuccessStatusCode();

        // Channel data is embedded in the page as escaped JSON
        var html = (await response.Content.ReadAsStringAsync()).Replace("\\\"", "\"");
        var match = Regex.Match(
            html,
            $"\"channelId\":\"([0-9a-f-]{{36}})\",\"permalink\":\"{Regex.Escape(channel)}\"",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value : null;
    }

    private bool ShouldSkipDownloaded(string dir)
    {
        var urlsPath = CoubPaths.GetDataPath(dir, Constants.UrlListFileName);

        if (!File.Exists(urlsPath))
        {
            // No file exists for this directory so no skip
            return false;
        }

        var urlsCount = File.ReadLines(urlsPath).Count();
        ConsoleEx.WriteLineColor($"[Found previously gathered list for '{dir}' with {urlsCount} links!]", ConsoleColor.Yellow);
        ConsoleEx.WriteLineColor("[Gather it again to get the newest coubs?] Already downloaded coubs will remain unchanged.", ConsoleColor.Green);
        ConsoleEx.WriteColor("Type [Y] for yes or [N] for no (then press enter): ", ConsoleColor.Red);
        var answer = Console.ReadLine()?.ToLower();

        // Original list is replaced only after the new one is successfully gathered
        return answer != "y" && answer != "yes";
    }

    private string GetTimelineQuery(string defaultKind, string extraParameters)
    {
        var (kind, order) = _configuration.Ordering switch
        {
            CoubOrder.Oldest => ("hot", "oldest"),
            CoubOrder.LikesCount => ("hot", "likes_count"),
            CoubOrder.ViewsCount => ("hot", "views_count"),
            _ => (defaultKind, null)
        };

        var query = $"kind={kind}{extraParameters}";
        return order == null ? query : $"{query}&order={order}";
    }

    private async Task DownloadScopedCoubsAsync(string dir, string scope)
    {
        if (ShouldSkipDownloaded(dir))
        {
            return;
        }

        var token = _authToken.Get();

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("Invalid token! Valid token is necessary to download user-specific categories "
                                    + "like 'liked' coubs. Skipping download of " + dir);
            return;
        }

        try
        {
            var query = GetTimelineQuery("hot", $"&scope={scope}");
            await DownloadLinksAsync(query, dir, token, null);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Unexpected error: " + ex);
        }
    }

    private static Pager GetPager()
    {
        ConsoleEx.WriteColor("[Number of coubs to download]. Type -1 for all, or number of coubs to download: ", ConsoleColor.Green);

        var answer = Console.ReadLine();
        if (int.TryParse(answer, out var result) && result > 0)
        {
            return new Pager
            {
                Take = result
            };
        }

        return new Pager();
    }

    private async Task DownloadLinksAsync(string query, string dir, string token, string channel)
    {
        ConsoleEx.WriteLineColor($"[Starting gathering links for '{dir}'...]", ConsoleColor.Yellow);
        if (_configuration.NsfwOnly)
        {
            ConsoleEx.WriteLineColor("[Downloading NSFW coubs ONLY!]", ConsoleColor.Magenta);
        }

        _pager = GetPager();

        var links = await GetLinksAsync(query, token, channel);
        if (links.Count == 0)
        {
            ConsoleEx.WriteLineColor($"[No coubs were gathered for '{dir}', existing list (if there is any) is kept.]", ConsoleColor.Yellow);
            return;
        }

        var rawMetaDataPath = CoubPaths.GetDataPath(dir, Constants.RawMetaDataFileName);
        var formattedMetaDataPath = CoubPaths.GetDataPath(dir, Constants.MetaDataFileName);
        var urlsPath = CoubPaths.GetDataPath(dir, Constants.UrlListFileName);
        var mediaPath = CoubPaths.GetDataPath(dir, Constants.MediaListFileName);

        if (!Directory.Exists(CoubPaths.InfoPath))
        {
            Directory.CreateDirectory(CoubPaths.InfoPath);
        }

        var subDirectory = Path.Combine(CoubPaths.InfoPath, dir);
        if (!Directory.Exists(subDirectory))
        {
            Directory.CreateDirectory(subDirectory);
        }

        // Save URLs
        var linksList = links.Where(x => !x.IsRepost).Select(x => x.Link).ToList();
        Console.WriteLine($"Saving {linksList.Count} crawled URLs...");
        var urlLinks = string.Join("\n", linksList);
        await File.WriteAllTextAsync(urlsPath, urlLinks, Encoding.UTF8);

        // Save reposts
        if (links.Any(x => x.IsRepost))
        {
            Console.WriteLine($"Saving {links.Count(x => x.IsRepost)} repost URLs...");
            var repostsPath = CoubPaths.GetDataPath(dir, Constants.RepostUrlListFileName);
            var repostLinks = string.Join("\n", links.Where(x => x.IsRepost).Select(x => x.RepostUrl));
            await File.WriteAllTextAsync(repostsPath, repostLinks, Encoding.UTF8);
        }

        // Save media links used by downloader
        Console.WriteLine("Saving media links...");
        await File.WriteAllTextAsync(mediaPath, JsonConvert.SerializeObject(links.Select(x => x.Media), Formatting.Indented), Encoding.UTF8);

        // Save raw metadata
        Console.WriteLine("Saving metadata...");
        var metaData = string.Join(",\n", links.Select(x => x.RawData));
        await File.WriteAllTextAsync(rawMetaDataPath, metaData, Encoding.UTF8);

        // Save formatted metadata
        Console.WriteLine("Saving metadata details...");
        var formattedLinks = string.Join("\n", links.Select(x => x.FormattedData));
        await File.WriteAllTextAsync(formattedMetaDataPath, formattedLinks, Encoding.UTF8);
    }

    private async Task<string> DownloadJsonAsync(string url, string token = null)
    {
        await Task.Delay(_configuration.WaitDelay);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (!string.IsNullOrWhiteSpace(token))
        {
            // Add cookies header with auth token to retrieve user-specific data
            request.Headers.Add("Cookie", AuthToken.GetCookie(token));
        }

        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private async Task<List<CoubDownloadResult>> GetLinksAsync(string query, string token, string channel)
    {
        var downloadedData = new List<CoubDownloadResult>();
        var cursor = string.Empty;
        var page = 0;

        while (true)
        {
            page++;
            Console.WriteLine($"Crawling page {page}");

            var url = $"{TimelineUrl}?{query}&cursor={Uri.EscapeDataString(cursor)}";

            JObject data;
            try
            {
                token = await _authToken.RefreshIfExpiringAsync(token);
                var json = await DownloadJsonAsync(url, token);
                data = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                ConsoleEx.WriteLineColor($"[Failed to download page {page} with URL: {url}] {ex.Message}", ConsoleColor.Red);
                return downloadedData;
            }

            var coubs = (JArray)data["items"] ?? [];
            if (page == 1 && coubs.Count == 0 && token != null)
            {
                // Invalid or expired token is not an error, API just returns empty list
                ConsoleEx.WriteLineColor("[No coubs returned. Your auth token might be invalid or expired.]", ConsoleColor.Yellow);
            }

            foreach (var coub in coubs)
            {
                var isNsfw = (bool?)coub["ageRestricted"] ?? false;
                var result = CoubDownloadResult.From(coub, channel);

                if (!_configuration.NsfwOnly ||
                    (_configuration.NsfwOnly && isNsfw))
                {
                    downloadedData.Add(result);
                    _pager.Use();
                }

                if (!_pager.HasNext)
                {
                    ConsoleEx.WriteLineColor($"[Reached pager target ({_pager.Take})... gathering results...]", ConsoleColor.Yellow);
                    return downloadedData;
                }
            }

            var next = data["next"]?.ToString();
            if (string.IsNullOrEmpty(next) || next == cursor)
            {
                ConsoleEx.WriteLineColor("[Reached last page... gathering results...]", ConsoleColor.Yellow);
                return downloadedData;
            }

            cursor = next;
        }
    }
}
