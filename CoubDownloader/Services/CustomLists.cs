using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CoubDownloader.Extensions;
using CoubDownloader.Models;
using Newtonsoft.Json;

namespace CoubDownloader.Services;

/// <summary>
///     Lists of coubs entered by user (text file or URLs), as opposed to coubs gathered from Coub's timelines
/// </summary>
public class CustomLists
{
    private static readonly Regex ViewUrlRegex = new(@"^(?:https?://)?(?:www\.)?coub\.com/view/([^/?#\s]+)", RegexOptions.IgnoreCase);
    private static readonly Regex PermalinkRegex = new("^[A-Za-z0-9._-]+$");

    private readonly CoubPageReader _pageReader;

    public CustomLists(CoubPageReader pageReader)
    {
        _pageReader = pageReader;
    }

    public static bool IsCoubUrl(string entry)
    {
        return ViewUrlRegex.IsMatch(entry);
    }

    public static string GetDirectory(string name)
    {
        // Do not mix the list with gathered channels or liked/bookmarked coubs
        var isTaken = name.Equals(Constants.LikedCategory, StringComparison.OrdinalIgnoreCase) ||
                      name.Equals(Constants.BookmarksCategory, StringComparison.OrdinalIgnoreCase) ||
                      File.Exists(CoubPaths.GetDataPath(name, Constants.RawMetaDataFileName));

        return isTaken ? name + "_list" : name;
    }

    public static bool IsOwnList(string dir)
    {
        // Gathered categories always have metadata, folders with only URLs are lists
        return File.Exists(CoubPaths.GetDataPath(dir, Constants.UrlListFileName)) &&
               !File.Exists(CoubPaths.GetDataPath(dir, Constants.RawMetaDataFileName));
    }

    // Gathers media info (media.json) for URLs in already existing lists that don't have it yet,
    // this covers lists created by older versions
    public async Task ResolveAllAsync()
    {
        if (!Directory.Exists(CoubPaths.InfoPath))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(CoubPaths.InfoPath))
        {
            await AppendMissingMediaAsync(new DirectoryInfo(directory).Name);
        }
    }

    // Prepares list of coubs entered by user, the list is always the source of truth
    // (so removed lines are not downloaded) and media.json only remembers already gathered coubs
    public async Task GatherAsync(string dir, IEnumerable<string> lines, bool saveUrlList)
    {
        Directory.CreateDirectory(Path.Combine(CoubPaths.InfoPath, dir));

        var permalinks = ReadPermalinks(dir, lines);
        if (permalinks.Count == 0)
        {
            ConsoleEx.WriteLineColor($"[No coub URLs found for '{dir}'.]", ConsoleColor.Yellow);
        }

        var known = (await LoadMediaAsync(dir))
            .GroupBy(x => x.Permalink)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (var coub in await GetMediaAsync(dir, permalinks.Where(x => !known.ContainsKey(x)).ToList()))
        {
            known[coub.Permalink] = coub;
        }

        if (saveUrlList)
        {
            var urls = string.Join("\n", permalinks.Select(x => Constants.CoubViewUrl + x));
            await File.WriteAllTextAsync(CoubPaths.GetDataPath(dir, Constants.UrlListFileName), urls, Encoding.UTF8);
        }

        await SaveMediaAsync(dir, permalinks.Where(known.ContainsKey).Select(x => known[x]).ToList());
    }

    private async Task AppendMissingMediaAsync(string dir)
    {
        var urlsPath = CoubPaths.GetDataPath(dir, Constants.UrlListFileName);
        if (!File.Exists(urlsPath))
        {
            return;
        }

        var media = await LoadMediaAsync(dir);
        var known = media.Select(x => x.Permalink).ToHashSet();
        var missing = ReadPermalinks(dir, await File.ReadAllLinesAsync(urlsPath))
            .Where(x => !known.Contains(x))
            .ToList();

        var resolved = await GetMediaAsync(dir, missing);
        if (resolved.Count > 0)
        {
            media.AddRange(resolved);
            await SaveMediaAsync(dir, media);
        }
    }

    private static List<string> ReadPermalinks(string dir, IEnumerable<string> lines)
    {
        var permalinks = new List<string>();
        var seen = new HashSet<string>();

        foreach (var line in lines.Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            var permalink = GetPermalink(line);
            if (permalink == null)
            {
                ConsoleEx.WriteLineColor($"[Skipping invalid line in '{dir}' URL list: {line}]", ConsoleColor.Yellow);
            }
            else if (seen.Add(permalink))
            {
                permalinks.Add(permalink);
            }
        }

        return permalinks;
    }

    private static string GetPermalink(string line)
    {
        var match = ViewUrlRegex.Match(line);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // Plain permalink without the URL
        return PermalinkRegex.IsMatch(line) ? line : null;
    }

    private async Task<List<CoubMedia>> GetMediaAsync(string dir, List<string> permalinks)
    {
        var media = new List<CoubMedia>();
        if (permalinks.Count == 0)
        {
            return media;
        }

        ConsoleEx.WriteLineColor($"[Gathering info about {permalinks.Count} coubs for '{dir}'...]", ConsoleColor.Yellow);

        for (var i = 0; i < permalinks.Count; i++)
        {
            Console.WriteLine($"  {i + 1} out of {permalinks.Count} ({permalinks[i]})");

            try
            {
                var coub = await _pageReader.GetCoubAsync(permalinks[i]);
                media.Add(coub.Media);
            }
            catch (Exception ex)
            {
                ConsoleEx.WriteLineColor($"    [Failed to get coub '{permalinks[i]}']: {ex.Message}", ConsoleColor.Red);
            }
        }

        return media;
    }

    private static async Task<List<CoubMedia>> LoadMediaAsync(string dir)
    {
        var mediaPath = CoubPaths.GetDataPath(dir, Constants.MediaListFileName);

        return File.Exists(mediaPath)
            ? JsonConvert.DeserializeObject<List<CoubMedia>>(await File.ReadAllTextAsync(mediaPath))
            : new List<CoubMedia>();
    }

    private static async Task SaveMediaAsync(string dir, List<CoubMedia> media)
    {
        var mediaPath = CoubPaths.GetDataPath(dir, Constants.MediaListFileName);
        await File.WriteAllTextAsync(mediaPath, JsonConvert.SerializeObject(media, Formatting.Indented), Encoding.UTF8);
    }
}
