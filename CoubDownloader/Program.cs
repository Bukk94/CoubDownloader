using System;
using System.IO;
using System.Linq;

namespace CoubDownloader
{
    public class Program
    {
        private const string Version = "0.7";
        
        public static void Main()
        {
            Console.WriteLine($"[Version: {Version}]");
            
            try
            {
                var input = GetDownloadInput();
                GetCoubs(input);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);

                var logFile = Path.Combine(Environment.CurrentDirectory, "err.log");
                File.WriteAllText(logFile, ex.ToString());
            }
            
            Console.ReadKey(true);
        }

        private static string GetDownloadInput()
        {
            Console.WriteLine("What do you want to download?");
            Console.WriteLine("You can download your liked coubs by typing liked, bookmarks or any channel by entering its username (not displayname!)");
            Console.WriteLine("You can download multiple channels, separated by comma.");
            Console.WriteLine("If you already have a list of URLs in correct format, leave input empty and just press enter.\n");
            Console.WriteLine("Input example: liked,bookmarks,channelone,redcoubhead,just.for.kicks");
            
            Console.Write("Input: ");
            var input = Console.ReadLine();
            
            return input?.ToLower().Trim();
        }

        private static void GetCoubs(string input)
        {
            var crawler = new Crawler();
            var downloader = new Downloader();

            var toDownload = input?.Split(",", StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray() ?? Array.Empty<string>();
            
            if (!toDownload.Any())
            {
                Console.Error.WriteLine("No user input, looking for already downloaded links...");
            }
            else
            {
                crawler.CrawlUrls(toDownload);
            }
            
            downloader.DownloadCoubs(crawler.InfoPath, toDownload);
        }
    }
}