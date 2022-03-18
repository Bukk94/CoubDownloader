using System;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace CoubDownloader
{
    public class Crawler
    {
        private string _usersAccessToken;
        private const string LikedCategory = "liked";
        private const string BookmarksCategory = "bookmarks";

        private const string DataFormat =
            "{0}\n" +
            "\tTitle: {1}\n" +
            "\tTags (encoded): {2}\n" +
            "\tTags: {3}\n" +
            "\tType: {4}\n" +
            "\tCreated At: {5}\n" +
            "\tDuration: {6}\n" +
            "\tNSFW: {7}\n" +
            "\tMade by: {8}\n";
        
        public string InfoPath => Path.Combine(Environment.CurrentDirectory, Constants.CoubInfoDir);

        public void CrawlUrls(string[] categories)
        {
            foreach (var category in categories)
            {
                if (category == LikedCategory)
                {
                    DownloadLikedCoubs();
                }
                else if (category == BookmarksCategory)
                {
                    DownloadBookmarkedCoubs();
                }
                else
                {
                    DownloadChannelCoubs(category);
                }
            }
        }
        
        public void DownloadChannelCoubs(string channel)
        {
            if (AlreadyDownloaded(channel))
            {
                return;
            }
            
            var url = "https://coub.com/api/v2/timeline/channel/"+ channel + "?page={0}&per_page=25";
            DownloadLinks(url, channel);
        }

        private string GetUrlListLocation(string dir)
        {
            return Path.Combine(Path.Combine(InfoPath, dir), Constants.UrlListFileName);
        }
        
        private string GetMetaDataLocation(string dir)
        {
            return Path.Combine(Path.Combine(InfoPath, dir), Constants.MetaDataFileName);
        }
        
        private string GetRawMetaDataLocation(string dir)
        {
            return Path.Combine(Path.Combine(InfoPath, dir), Constants.RawMetaDataFileName);
        }

        private bool AlreadyDownloaded(string dir)
        {
            var urlsPath = GetUrlListLocation(dir);

            if (File.Exists(urlsPath))
            {
                Console.WriteLine($"URL list for '{dir}' found! Skipping crawling.");
                return true;
            }

            return false;
        }

        public void DownloadLikedCoubs()
        {
            var dir = LikedCategory;
            if (AlreadyDownloaded(dir))
            {
                return;
            }

            var token = GetAccessToken();

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.Error.WriteLine("Invalid token! Valid token is necessary to download user-specific categories "
                                        + "like 'liked' coubs. Skipping download of " + dir);
                return;
            }
            
            try
            {
                var url = "https://coub.com/api/v2/timeline/likes?all=true&order_by=date&page={0}&per_page=25";
                DownloadLinks(url, dir, token);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Forbidden"))
                {
                    Console.Error.WriteLine("Invalid access token!");
                }
                else
                {
                    Console.Error.WriteLine("Unexpected error: " + ex);
                }
            }
        }
        
        public void DownloadBookmarkedCoubs()
        {
            var dir = BookmarksCategory;
            if (AlreadyDownloaded(dir))
            {
                return;
            }

            var token = GetAccessToken();

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.Error.WriteLine("Invalid token! Valid token is necessary to download user-specific categories "
                                        + "like 'liked' coubs. Skipping download of " + dir);
                return;
            }
            
            try
            {
                var url = "https://coub.com/api/v2/timeline/favourites?all=true&order_by=date&page={0}&per_page=25";
                DownloadLinks(url, dir, token);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Forbidden"))
                {
                    Console.Error.WriteLine("Invalid access token!");
                }
                else
                {
                    Console.Error.WriteLine("Unexpected error: " + ex);
                }
            }
        }
        
        private string GetAccessToken()
        {
            if (!string.IsNullOrWhiteSpace(_usersAccessToken))
            {
                // If user already passed token, return it
                return _usersAccessToken;
            }
            
            Console.WriteLine("Write/paste your access token. Read README if you don't know how to get it");
            Console.Write("Access Token: ");
            var token = Console.ReadLine();
            token = token?.Replace("remember_token=", "").Trim();

            _usersAccessToken = token;
            
            return token;
        }
        
        private void DownloadLinks(string baseUrl, string dir, string token = null)
        {
            Console.WriteLine($"Starting gathering links for '{dir}'...");
            var links = GetLinks(baseUrl, 1, token);
    
            var rawMetaDataPath = GetRawMetaDataLocation(dir);
            var formattedMetaDataPath = GetMetaDataLocation(dir);
            var urlsPath = GetUrlListLocation(dir);

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
            Console.WriteLine("Saving crawled URLs...");
            var urlLinks = string.Join("\n", links.Select(x => x.Link));
            File.WriteAllText(urlsPath, urlLinks, ASCIIEncoding.UTF8);

            // Save raw metadata
            Console.WriteLine("Saving metadata...");
            var metaData = string.Join(",\n", links.Select(x => x.RawData));
            File.WriteAllText(rawMetaDataPath, metaData, ASCIIEncoding.UTF8);
            
            // Save formatted metadata
            Console.WriteLine("Saving metadata details...");
            var formattedLinks = string.Join("\n", links.Select(x => x.FormattedData));
            File.WriteAllText(formattedMetaDataPath, formattedLinks, ASCIIEncoding.UTF8);
        }
        
        private static List<CoubDownloadResult> GetLinks(string baseUrl, int page, string token)
        {
            var request = (HttpWebRequest)WebRequest.Create(
                    string.Format(baseUrl, page));
            
            if (!string.IsNullOrWhiteSpace(token))
            {
                // Add cookies header with access token to retrieve user-specific data
                request.Headers["Cookie"] = $"remember_token={token}";
            }

            var response = (HttpWebResponse)request.GetResponse();

            var responseText = "";
            using (var reader = new System.IO.StreamReader(response.GetResponseStream(), ASCIIEncoding.UTF8))
            {
                responseText = reader.ReadToEnd();
            }

            dynamic data = JObject.Parse(responseText);
            var totalPages = data.total_pages;

            var currentPage = data.page;
            var coubs = data.coubs;
            
            var downloadedData = new List<CoubDownloadResult>();

            foreach (var coub in coubs)
            {
                var tags = new List<string>();
                var tagsEncoded = new List<string>();
                foreach (var tag in coub.tags)
                {
                    tagsEncoded.Add(tag.value.ToString());
                    tags.Add(tag.title.ToString());
                }

                var isNsfw = (bool?)coub.not_safe_for_work ?? false;
                string title = coub.title.ToString();
                    
                var formatted = string.Format(DataFormat,
                    "https://coub.com/view/" + coub.permalink,
                    title.RemoveLinebreaks(),
                    string.Join(",", tagsEncoded).RemoveLinebreaks(),
                    string.Join(",", tags),
                    coub.type,
                    coub.created_at,
                    coub.duration,
                    isNsfw ? "Yes" : "No",
                    coub.channel.permalink);
                
                var result = new CoubDownloadResult
                {
                    Link = "https://coub.com/view/" + coub.permalink,
                    FormattedData = formatted.ToString(),
                    RawData = coub.ToString()
                };

                downloadedData.Add(result);
            }

            if (currentPage != totalPages)
            {
                Console.WriteLine($"Crawling page {currentPage} out of {totalPages}");
                downloadedData.AddRange(GetLinks(baseUrl, ++page, token));
            }
            else
            {
                Console.WriteLine("Reached last page... gathering results...");
            }

            return downloadedData;
        }
    }
}