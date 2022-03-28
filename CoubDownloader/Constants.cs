namespace CoubDownloader
{
    public static class Constants
    {
        /// <summary>
        ///     Wait between each download, in seconds.
        /// </summary>
        public const int WaitBetweenDownloads = 1;
        
        public const string CoubInfoDir = "Coubs-info";
        public const string CoubDataDir = "Coubs";
        public const string RepostsDir = "Reposts";
        
        public const string UrlListFileName = "url_list.txt";
        public const string RepostUrlListFileName = "url_list_reposts.txt";
        public const string MetaDataFileName = "metadata.txt";
        public const string RawMetaDataFileName = "raw_metadata.json";
        public const string SegmentsFileName = "segments.json";
    }
}