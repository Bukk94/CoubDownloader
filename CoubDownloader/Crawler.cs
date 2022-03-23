using System;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CoubDownloader
{
    public class Crawler
    {
        private string _usersAccessToken;
        private bool _crawlSegments;
        private const int PageLimit = 25;
        private const string LikedCategory = "liked";
        private const string BookmarksCategory = "bookmarks";
        private const string BaseUrl = "https://coub.com/view/";

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
            ShouldCrawlSegments();
            
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
        
        private void DownloadChannelCoubs(string channel)
        {
            if (ShouldSkipDownloaded(channel))
            {
                return;
            }
            
            var url = "https://coub.com/api/v2/timeline/channel/"+ channel + $"?page={{0}}&per_page={PageLimit}";
            DownloadLinks(url, channel);
        }

        private string GetDataPath(string dir, string filename)
        {
            return Path.Combine(Path.Combine(InfoPath, dir), filename);
        }
        
        private void ShouldCrawlSegments()
        {
            Console.WriteLine("Most of the coubs has additional segment metadata. Should those be downloaded too? " +
                              "(it will slow down initial URL crawling).");
            Console.Write("Type Y for yes or N for no (then press enter): ");
            var answer = Console.ReadLine()?.ToLower();
            if (answer == "y" || answer == "yes")
            {
                _crawlSegments = true;
                Console.WriteLine($"Segments for crawled coubs will be stored in the file called {Constants.SegmentsFileName}\n");
            }
            else
            {
                Console.WriteLine("Segments won't be downloaded.\n");
            }
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
            Console.WriteLine($"Found URL list for '{dir}' with {urlsCount} links!");
            Console.WriteLine("Download the list again to get newest changes? Original list will be deleted, but already downloaded coubs will remain unchanged.");
            Console.Write("Type Y for yes or N for no (then press enter): ");
            var answer = Console.ReadLine()?.ToLower();
            if (answer == "y" || answer == "yes")
            {
                // User wants new crawl, remove original file
                File.Delete(urlsPath);
                return false;
            }

            return true;
        }

        private void DownloadLikedCoubs()
        {
            var dir = LikedCategory;
            if (ShouldSkipDownloaded(dir))
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
                var url = $"https://coub.com/api/v2/timeline/likes?order_by=date&page={{0}}&per_page={PageLimit}";
                var totalLikes = GetTotalLikes(token);
                Console.WriteLine($"Estimated {totalLikes} liked coubs to download.");
                var totalPages = CalculateNumberOfPages(totalLikes);
                DownloadLinks(url, dir, token, totalPages);
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
        
        private void DownloadBookmarkedCoubs()
        {
            var dir = BookmarksCategory;
            if (ShouldSkipDownloaded(dir))
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
                var url = $"https://coub.com/api/v2/timeline/favourites?order_by=date&page={{0}}&per_page={PageLimit}";
                var totalBookmarks = GetTotalBookmarks(token);
                Console.WriteLine($"Estimated {totalBookmarks} bookmarked coubs to download.");
                var totalPages = CalculateNumberOfPages(totalBookmarks);
                DownloadLinks(url, dir, token, totalPages);
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

        private static int GetTotalLikes(string token)
        {
            var json = DownloadJson("https://coub.com/api/v2/users/me", token);
            dynamic data = JObject.Parse(json);
            
            return int.Parse(data.likes_count.ToString());
        }
        
        private static int GetTotalBookmarks(string token)
        {
            var json = DownloadJson("https://coub.com/api/v2/users/me", token);
            dynamic data = JObject.Parse(json);
            
            return int.Parse(data.favourites_count.ToString());
        }
        
        private string GetAccessToken()
        {
            if (!string.IsNullOrWhiteSpace(_usersAccessToken))
            {
                // If user already passed token, return it
                return _usersAccessToken;
            }
            
            Console.WriteLine("Write/paste your access token. Read README if you don't know how to get it.");
            Console.Write("Access Token: ");
            var token = Console.ReadLine();
            token = token?.Replace("remember_token=", "").Trim();

            _usersAccessToken = token;
            
            return token;
        }

        private static int CalculateNumberOfPages(int total)
        {
            return total > PageLimit ? 
                (int)Math.Ceiling((double)total / PageLimit) // If number of items is over the limit, batch it by PageLimit 
                : 1; // Otherwise go one by one
        }
        
        private void DownloadLinks(string baseUrl, string dir, string token = null, int? totalPages = null)
        {
            Console.WriteLine($"Starting gathering links for '{dir}'...");
            var links = GetLinks(baseUrl, 1, token, totalPages);
    
            var rawMetaDataPath = GetDataPath(dir, Constants.RawMetaDataFileName);
            var formattedMetaDataPath = GetDataPath(dir, Constants.MetaDataFileName);
            var urlsPath = GetDataPath(dir, Constants.UrlListFileName);
            var segmentsPath = GetDataPath(dir, Constants.SegmentsFileName);

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
            File.WriteAllText(urlsPath, urlLinks, Encoding.UTF8);

            // Save reposts
            if (links.Any(x => x.IsRepost))
            {
                Console.WriteLine($"Saving {links.Count(x => x.IsRepost)} repost URLs...");
                var repostsPath = GetDataPath(dir, Constants.RepostUrlListFileName);
                var repostLinks = string.Join("\n", links.Where(x => x.IsRepost).Select(x => x.RepostUrl));
                File.WriteAllText(repostsPath, repostLinks, Encoding.UTF8);
            }
            
            // Save raw metadata
            Console.WriteLine("Saving metadata...");
            var metaData = string.Join(",\n", links.Select(x => x.RawData));
            File.WriteAllText(rawMetaDataPath, metaData, Encoding.UTF8);
            
            // Save formatted metadata
            Console.WriteLine("Saving metadata details...");
            var formattedLinks = string.Join("\n", links.Select(x => x.FormattedData));
            File.WriteAllText(formattedMetaDataPath, formattedLinks, Encoding.UTF8);
            
            if (_crawlSegments)
            {
                // Save segments
                Console.WriteLine("Saving coub segments...");

                var data = links
                    .Where(x => !string.IsNullOrWhiteSpace(x.Segments.segments))
                    .Select(x => new
                {
                    permalink = x.Segments.id,
                    data = JToken.Parse(x.Segments.segments)
                }).ToArray();

                var serializedSegments = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(segmentsPath, serializedSegments, Encoding.UTF8);
            }
        }

        private static string DownloadJson(string url, string token = null)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            
            if (!string.IsNullOrWhiteSpace(token))
            {
                // Add cookies header with access token to retrieve user-specific data
                request.Headers["Cookie"] = $"remember_token={token}";
            }

            var response = (HttpWebResponse)request.GetResponse();

            using var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
            
            return reader.ReadToEnd();
        }
        
        private List<CoubDownloadResult> GetLinks(string baseUrl, int page, string token, int? totalPagesToDownload)
        {
            if (totalPagesToDownload.HasValue)
            {
                Console.WriteLine($"Crawling page {page} out of {totalPagesToDownload}");
            }
            else
            {
                Console.WriteLine($"Crawling page {page}");
            }

            var url = string.Format(baseUrl, page);
            var json = DownloadJson(url, token);

            dynamic data = JObject.Parse(json);
            var totalPages = totalPagesToDownload ?? data.total_pages; // Sometimes paging info is not always right

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
                string permalink = coub.permalink.ToString();
                string title = coub.title.ToString();
                var segments = string.Empty;

                var repostUrl = string.Empty;
                var isRepost = (bool)(coub.recoub_to.GetHashCode() != 0); // Working with JValue type, if hash is 0, it has no value
                if (isRepost)
                {
                    repostUrl = BaseUrl + coub.recoub_to.permalink.ToString();
                }
                
                if (_crawlSegments)
                {
                    // For each coub download segments data
                    try
                    {
                        segments = DownloadJson($"https://coub.com/api/v2/coubs/{permalink}/segments");
                    }
                    catch (Exception ex)
                    {
                        // A lot of recoubed videos don't have segments for some reason
                        if (ex.Message.Contains("The remote server returned an error: (404) Not Found.") &&
                            coub.type.ToString() != "Coub::Recoub")
                        {
                            Console.WriteLine($"Segments for coub '{permalink}' was not found.");
                        }
                    }
                }
                
                var formatted = string.Format(DataFormat,
                    BaseUrl + permalink,
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
                    Link = BaseUrl + permalink,
                    IsRepost = isRepost,
                    RepostUrl = repostUrl,
                    FormattedData = formatted.ToString(),
                    RawData = coub.ToString(),
                    Segments = (permalink, segments)
                };

                downloadedData.Add(result);
            }

            if (downloadedData.Count == 0)
            {
                // Nothing was downloaded, probably reached end of pages
                Console.WriteLine("Reached last item, skipping the rest of the pages... gathering results...");
                return downloadedData;
            }
            
            if (currentPage != totalPages)
            {
                downloadedData.AddRange(GetLinks(baseUrl, ++page, token, (int?)totalPages));
            }
            else
            {
                Console.WriteLine("Reached last page... gathering results...");
            }

            return downloadedData;
        }
    }
}