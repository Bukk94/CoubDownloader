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
using Newtonsoft.Json;

namespace CoubDownloader;

public class Crawler
{
    private readonly Configuration _configuration;
    private readonly HttpClient _http;
    private Pager _pager = new();
    private string _usersAuthToken;
    private const string LikedCategory = "liked";
    private const string BookmarksCategory = "bookmarks";
    private const string BaseUrl = "https://coub.com/view/";
    private const string TimelineUrl = "https://coub.com/api/coub-site/timeline";
    private const string TokenCookieName = "sb-gotrue-auth-token";
    private const string TokenPrefix = "base64-";

    private const string DataFormat =
        "{0}\n" +
        "\tTitle: {1}\n" +
        "\tTags: {2}\n" +
        "\tCreated At: {3}\n" +
        "\tDuration: {4}\n" +
        "\tNSFW: {5}\n" +
        "\tMade by: {6}\n";

    public Crawler(Configuration configuration)
    {
        _configuration = configuration;
        _http = CoubHttp.Create(configuration);
    }

    public string InfoPath => Path.Combine(Environment.CurrentDirectory, Constants.CoubInfoDir);

    public async Task CrawlUrlsAsync(string[] categories)
    {
        foreach (var category in categories)
        {
            if (category == LikedCategory)
            {
                await DownloadScopedCoubsAsync(LikedCategory, "likes");
            }
            else if (category == BookmarksCategory)
            {
                await DownloadScopedCoubsAsync(BookmarksCategory, "bookmarks");
            }
            else
            {
                await DownloadChannelCoubsAsync(category);
            }
        }
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

    private string GetDataPath(string dir, string filename)
    {
        return Path.Combine(Path.Combine(InfoPath, dir), filename);
    }

    private bool ShouldSkipDownloaded(string dir)
    {
        var urlsPath = GetDataPath(dir, Constants.UrlListFileName);

        if (!File.Exists(urlsPath))
        {
            // No file exists for this directory so no skip
            return false;
        }

        var urlsCount = File.ReadLines(urlsPath).Count();
        ConsoleEx.WriteLineColor($"[Found URL list for '{dir}' with {urlsCount} links!]", ConsoleColor.Yellow);
        ConsoleEx.WriteLineColor("[Download the list again to get newest changes]? Original list will be deleted, but already downloaded coubs will remain unchanged.", ConsoleColor.Green);
        ConsoleEx.WriteColor("Type [Y] for yes or [N] for no (then press enter): ", ConsoleColor.Red);
        var answer = Console.ReadLine()?.ToLower();
        if (answer == "y" || answer == "yes")
        {
            // User wants new crawl, remove original file
            File.Delete(urlsPath);
            return false;
        }

        return true;
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

        var token = GetAuthToken();

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

    private string GetAuthToken()
    {
        if (!string.IsNullOrWhiteSpace(_usersAuthToken))
        {
            // If user already passed token, return it
            return _usersAuthToken;
        }

        ConsoleEx.WriteLineColor($"[Enter/paste your auth token (value of '{TokenCookieName}' cookie, starts with base64-)]. Read README if you don't know how to get it.", ConsoleColor.Green);
        ConsoleEx.WriteColor("[Auth Token]: ", ConsoleColor.Green);
        var token = Console.ReadLine()?.Trim();

        // Allow pasting whole cookie string or 'name=value'
        var cookiePrefix = TokenCookieName + "=";
        var start = token?.IndexOf(cookiePrefix, StringComparison.Ordinal) ?? -1;
        if (start >= 0)
        {
            token = token.Substring(start + cookiePrefix.Length).Split(';')[0].Trim();
        }

        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            ConsoleEx.WriteLineColor($"[Token must start with '{TokenPrefix}'. Make sure you copied the value of '{TokenCookieName}' cookie.]", ConsoleColor.Red);
            return null;
        }

        _usersAuthToken = token;

        return token;
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

        var rawMetaDataPath = GetDataPath(dir, Constants.RawMetaDataFileName);
        var formattedMetaDataPath = GetDataPath(dir, Constants.MetaDataFileName);
        var urlsPath = GetDataPath(dir, Constants.UrlListFileName);
        var mediaPath = GetDataPath(dir, Constants.MediaListFileName);

        if (!Directory.Exists(InfoPath))
        {
            Directory.CreateDirectory(InfoPath);
        }

        var subDirectory = Path.Combine(InfoPath, dir);
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
            var repostsPath = GetDataPath(dir, Constants.RepostUrlListFileName);
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
        await Task.Delay((int)_configuration.WaitTime * 1000);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (!string.IsNullOrWhiteSpace(token))
        {
            // Add cookies header with auth token to retrieve user-specific data
            request.Headers.Add("Cookie", $"{TokenCookieName}={token}");
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
                var json = await DownloadJsonAsync(url, token);
                data = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                ConsoleEx.WriteLineColor($"[Failed to download page {page} with URL: {url}] {ex.Message}", ConsoleColor.Red);
                return downloadedData;
            }

            var coubs = (JArray)data["items"];
            if (page == 1 && coubs.Count == 0 && token != null)
            {
                // Invalid or expired token is not an error, API just returns empty list
                ConsoleEx.WriteLineColor("[No coubs returned. Your auth token might be invalid or expired.]", ConsoleColor.Yellow);
            }

            foreach (var coub in coubs)
            {
                var tags = coub["tags"].Select(x => x.ToString()).ToList();
                var isNsfw = (bool?)coub["ageRestricted"] ?? false;
                var permalink = coub["permalink"].ToString();
                var title = coub["title"].ToString();

                // Channel timeline contains coubs from other channels only when they were reposted
                var madeBy = coub["channel"]["permalink"].ToString();
                var isRepost = channel != null && !string.Equals(madeBy, channel, StringComparison.OrdinalIgnoreCase);

                var formatted = string.Format(DataFormat,
                    BaseUrl + permalink,
                    title.RemoveLinebreaks(),
                    string.Join(",", tags),
                    coub["publishedAt"],
                    coub["duration"],
                    isNsfw ? "Yes" : "No",
                    madeBy);

                var result = new CoubDownloadResult
                {
                    Link = BaseUrl + permalink,
                    IsRepost = isRepost,
                    RepostUrl = isRepost ? BaseUrl + permalink : string.Empty,
                    FormattedData = formatted,
                    RawData = coub.ToString(),
                    Media = new CoubMedia
                    {
                        Permalink = permalink,
                        Title = title,
                        IsRepost = isRepost,
                        VideoHdUrl = (string)coub["videoHdUrl"],
                        VideoSdUrl = (string)coub["videoSdUrl"],
                        VideoLowUrl = (string)coub["videoLowUrl"],
                        AudioUrl = (string)coub["audioUrl"],
                        AudioMedUrl = (string)coub["audioMedUrl"]
                    }
                };

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