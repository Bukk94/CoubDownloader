using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CoubDownloader.Configurations;
using CoubDownloader.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CoubDownloader.Services;

/// <summary>
///     There is no API for a single coub, so its data are read from the coub's page
/// </summary>
public class CoubPageReader
{
    private static readonly Regex PageChunkRegex = new(@"self\.__next_f\.push\(\[1,(""[^""\\]*(?:\\.[^""\\]*)*"")\]\)");

    private readonly Configuration _configuration;
    private readonly HttpClient _http;

    public CoubPageReader(Configuration configuration, HttpClient http)
    {
        _configuration = configuration;
        _http = http;
    }

    public async Task<CoubDownloadResult> GetCoubAsync(string permalink)
    {
        await Task.Delay(_configuration.WaitDelay);

        using var response = await _http.GetAsync(Constants.CoubViewUrl + Uri.EscapeDataString(permalink));
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Coub does not exist.");
        }

        response.EnsureSuccessStatusCode();

        var coub = ExtractCoub(await response.Content.ReadAsStringAsync(), permalink);
        if (coub == null)
        {
            throw new InvalidOperationException("Coub data were not found on the page (coub is private/removed, or Coub changed its page).");
        }

        return CoubDownloadResult.From(coub, null);
    }

    private static JObject ExtractCoub(string html, string permalink)
    {
        // The page has all coub data embedded in script chunks as JSON encoded strings.
        // Joined together they form a stream containing the coub object.
        var stream = new StringBuilder();
        foreach (Match chunk in PageChunkRegex.Matches(html))
        {
            stream.Append(JsonConvert.DeserializeObject<string>(chunk.Groups[1].Value));
        }

        var data = stream.ToString();
        var marker = $"\"permalink\":{JsonConvert.ToString(permalink)}";
        var index = data.IndexOf(marker, StringComparison.Ordinal);

        while (index >= 0)
        {
            var start = data.LastIndexOf("{\"id\":\"", index, StringComparison.Ordinal);
            if (start >= 0)
            {
                try
                {
                    using var reader = new JsonTextReader(new StringReader(data.Substring(start)));
                    var coub = JObject.Load(reader);

                    // The permalink can also be used by other objects (e.g. a channel), the coub has media
                    if (coub["videoHdUrl"]?.Type == JTokenType.String || coub["videoSdUrl"]?.Type == JTokenType.String)
                    {
                        return coub;
                    }
                }
                catch (JsonReaderException)
                {
                    // Not a coub object, try next occurrence
                }
            }

            index = data.IndexOf(marker, index + 1, StringComparison.Ordinal);
        }

        return null;
    }
}
