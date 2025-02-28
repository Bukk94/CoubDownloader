namespace CoubDownloader.Configurations
{
    public class Configuration
    {
        public bool DownloadSegments { get; set; } = false;

        public int Loops { get; set; }

        public double WaitTime { get; set; }

        public VideoQuality VideoQuality { get; set; }
        
        public CoubOrder Ordering { get; set; }

        public bool KeepAudioVideo { get; set; }

        public string OutputFolderPath { get; set; }

        public bool NsfwOnly { get; set; }
        
        public static Configuration Default => new()
        {
            DownloadSegments = false,
            Loops = -1, // No limit
            WaitTime = 2.5f, // By default 2.5 seconds,
            VideoQuality = VideoQuality.Highest,
            Ordering = CoubOrder.Newest,
            KeepAudioVideo = false,
            OutputFolderPath = null,
            NsfwOnly = false
        };
    }
}