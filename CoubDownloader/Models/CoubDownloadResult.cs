using System;
using System.Linq;
using CoubDownloader.Extensions;
using Newtonsoft.Json.Linq;

namespace CoubDownloader.Models;

public class CoubDownloadResult
{
    private const string DataFormat =
        "{0}\n" +
        "\tTitle: {1}\n" +
        "\tTags: {2}\n" +
        "\tCreated At: {3}\n" +
        "\tDuration: {4}\n" +
        "\tNSFW: {5}\n" +
        "\tMade by: {6}\n";

    public string Link { get; set; }
    public bool IsRepost { get; set; }
    public string RepostUrl { get; set; }
    public string FormattedData { get; set; }
    public string RawData { get; set; }
    public CoubMedia Media { get; set; }

    public static CoubDownloadResult From(JToken coub, string channel)
    {
        var tags = coub["tags"].Select(x => x.ToString()).ToList();
        var isNsfw = (bool?)coub["ageRestricted"] ?? false;
        var permalink = coub["permalink"].ToString();
        var title = coub["title"].ToString();

        // Channel timeline contains coubs from other channels only when they were reposted
        var madeBy = coub["channel"]["permalink"].ToString();
        var isRepost = channel != null && !string.Equals(madeBy, channel, StringComparison.OrdinalIgnoreCase);

        var formatted = string.Format(DataFormat,
            Constants.CoubViewUrl + permalink,
            title.RemoveLinebreaks(),
            string.Join(",", tags),
            coub["publishedAt"],
            coub["duration"],
            isNsfw ? "Yes" : "No",
            madeBy);

        return new CoubDownloadResult
        {
            Link = Constants.CoubViewUrl + permalink,
            IsRepost = isRepost,
            RepostUrl = isRepost ? Constants.CoubViewUrl + permalink : string.Empty,
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
    }
}
