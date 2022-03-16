using System;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace CoubDownloader
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("Write/paste your access token. Read README if you don't know how to get it");
            Console.Write("Access Token: ");
            var token = Console.ReadLine();
            token = token?.Replace("remember_token=", "")?.Trim();

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.Error.WriteLine("Invalid token!");
                Console.ReadKey();
                return;
            }

            var urlsPath = Path.Combine(Path.Combine(Environment.CurrentDirectory, "Coubs-info"), "url_list.txt");
            if (!File.Exists(urlsPath))
            {
                try
                {
                    DownloadLinks(token);
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
    
                    Console.ReadKey();
                    return;
                }
            }
            else
            {
                Console.WriteLine("URL list found! Skipping crawling.");
            }

            DownloadCoubs();

            Console.ReadKey();
        }

        private static void DownloadCoubs()
        {
            Console.WriteLine("Starting download...");

            // coub_v2.py is creating temp list for data
            // It might be left behind if something happens, clean that up before starting
            var tmpList = Path.Combine(Environment.CurrentDirectory, "list.txt");
            if (File.Exists(tmpList))
            {
                File.Delete(tmpList);
            }
            
            var coubsDir = Path.Combine(Environment.CurrentDirectory, "Coubs");
            if (!Directory.Exists(coubsDir))
            {
                Directory.CreateDirectory(coubsDir);
            }

            try
            {
                Run.RunCommand("python.exe -X utf8 coub_v2.py -l Coubs-info\\url_list.txt -o \"Coubs\\%id%_%title%\"",
                    Environment.CurrentDirectory);
                Console.WriteLine("DONE");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Fatal error: " + ex);
            }
        }

        private static void DownloadLinks(string token)
        {
            Console.WriteLine("Starting gathering links...");
            var links = GetLinks(1, token);

            var infoDir = Path.Combine(Environment.CurrentDirectory, "Coubs-info");
            
            var infoPath = Path.Combine(infoDir, "data.txt");
            var urlsPath = Path.Combine(infoDir, "url_list.txt");

            if (!Directory.Exists(infoDir))
            {
                Directory.CreateDirectory(infoDir);
            }
            
            Console.WriteLine("Writing crawled URLs...");
            var urlLinks = string.Join("\n", links.Select(x => x.Link));
            File.WriteAllText(urlsPath, urlLinks, ASCIIEncoding.UTF8);

            Console.WriteLine("Saving metadata details...");
            var formattedLinks = string.Join("\n", links.Select(x => x.FormattedData));
            File.WriteAllText(infoPath, formattedLinks, ASCIIEncoding.UTF8);
        }

        private static List<CoubDownloadResult> GetLinks(int page, string token)
        {
            var request = (HttpWebRequest)WebRequest.Create(
                    $"https://coub.com/api/v2/timeline/likes?all=true&order_by=date&page={page}&per_page=25");
            request.Headers["Cookie"] = $"remember_token={token}";

            var response = (HttpWebResponse)request.GetResponse();

            var responseText = "";
            using (var reader = new System.IO.StreamReader(response.GetResponseStream(), ASCIIEncoding.UTF8))
            {
                responseText = reader.ReadToEnd();
            }

            //Console.WriteLine(responseText);

            dynamic data = JObject.Parse(responseText);
            var totalPages = data.total_pages;
            //Console.WriteLine(totalPages);

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
                downloadedData.AddRange(GetLinks(++page, token));
            }
            else
            {
                Console.WriteLine("Reached last page... gathering results...");
            }

            return downloadedData;
        }
    }
}