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
    
            var metaDataPath = GetMetaDataLocation(dir);
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
            
            Console.WriteLine("Writing crawled URLs...");
            var urlLinks = string.Join("\n", links.Select(x => x.Link));
            File.WriteAllText(urlsPath, urlLinks, ASCIIEncoding.UTF8);

            Console.WriteLine("Saving metadata details...");
            var formattedLinks = string.Join("\n", links.Select(x => x.FormattedData));
            File.WriteAllText(metaDataPath, formattedLinks, ASCIIEncoding.UTF8);
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

                var formatted = "https://coub.com/view/" + coub.permalink
                                     + "         | " + coub.title
                                     + " | " + string.Join(",", tags)
                                     + " | " + string.Join(",", tagsEncoded)
                    .Replace("\r\n", "") // Remove any line-breaks that is often in the tags
                    .Replace("\n", "")
                    .Replace("\r", "");
                
                var result = new CoubDownloadResult
                {
                    Link = "https://coub.com/view/" + coub.permalink,
                    FormattedData = formatted.ToString()
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