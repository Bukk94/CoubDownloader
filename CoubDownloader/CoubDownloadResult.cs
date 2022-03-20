namespace CoubDownloader
{
    public class CoubDownloadResult
    {
        public string Link { get; set; }
        public bool IsRepost { get; set; }
        public string RepostUrl { get; set; }
        public string FormattedData { get; set; }
        public string RawData { get; set; }
        public (string id, string segments) Segments { get; set; }
    }
}