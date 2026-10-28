namespace CoubDownloader
{
    public class CoubMedia
    {
        public string Permalink { get; set; }
        public string Title { get; set; }
        public bool IsRepost { get; set; }
        public string VideoHdUrl { get; set; }
        public string VideoSdUrl { get; set; }
        public string VideoLowUrl { get; set; }
        public string AudioUrl { get; set; }
        public string AudioMedUrl { get; set; }
    }
}