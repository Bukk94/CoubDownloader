using System;
using System.IO;
using System.Linq;
using CoubDownloader.Configurations;
using CoubDownloader.Extensions;
using Newtonsoft.Json;

namespace CoubDownloader
{
    public class Program
    {
        private const string Version = "0.12";
        
        public static void Main()
        {
            try
            {
                // This call is Windows specific. Try to set width to nicely match text, but if that fails it's no big deal.
                Console.SetWindowSize(125, Console.WindowHeight);
            } catch {}
            ConsoleEx.WriteLineColor(@"[
 ██████  ██████  ██    ██ ██████      ██████   ██████  ██     ██ ███    ██ ██       ██████   █████  ██████  ███████ ██████  
██      ██    ██ ██    ██ ██   ██     ██   ██ ██    ██ ██     ██ ████   ██ ██      ██    ██ ██   ██ ██   ██ ██      ██   ██ 
██      ██    ██ ██    ██ ██████      ██   ██ ██    ██ ██  █  ██ ██ ██  ██ ██      ██    ██ ███████ ██   ██ █████   ██████  
██      ██    ██ ██    ██ ██   ██     ██   ██ ██    ██ ██ ███ ██ ██  ██ ██ ██      ██    ██ ██   ██ ██   ██ ██      ██   ██ 
 ██████  ██████   ██████  ██████      ██████   ██████   ███ ███  ██   ████ ███████  ██████  ██   ██ ██████  ███████ ██   ██ 
]", ConsoleColor.Blue);
            Console.WriteLine($"[Version: {Version}]\n");
            
            try
            {
                var configuration = LoadConfiguration();

                var input = GetDownloadInput();
                GetCoubs(input, configuration);
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
            ConsoleEx.WriteLineColor("[What do you want to download?]", ConsoleColor.Green);
            Console.WriteLine("You can download your liked coubs by typing liked, bookmarks or any channel by entering its username (not displayname!)");
            Console.WriteLine("You can download multiple channels, separated by comma.");
            Console.WriteLine("If you already have a list of URLs in correct format, leave input empty and just press enter.\n");
            Console.WriteLine("Input example: liked,bookmarks,channelone,redcoubhead,just.for.kicks");

            ConsoleEx.WriteColor("[Input]: ", ConsoleColor.Green);
            var input = Console.ReadLine();
            
            return input?.ToLower().Trim();
        }

        private static Configuration LoadConfiguration()
        {
            var path = Path.Combine(Environment.CurrentDirectory, "Configuration.json");
            if (!File.Exists(path))
            {
                // No configuration found, load default
                Console.WriteLine("No configuration found, using default");
                return Configuration.Default;
            }

            try
            {
                var hasErrorOrWarning = false; 
                var defaultConfiguration = Configuration.Default;
                
                var configuration = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(path));
                if (configuration == null)
                {
                    return defaultConfiguration;
                }
                
                if (!string.IsNullOrWhiteSpace(configuration.OutputFolderPath))
                {
                    // If folder path is set, validate it
                    if (!configuration.OutputFolderPath.EndsWith("\\") || // Path must end with slash
                        configuration.OutputFolderPath.Contains("/")) // Path cannot contain forward slashes
                    {
                        ConsoleEx.WriteLineColor($"[Invalid {nameof(Configuration.OutputFolderPath)}, using default (same directory).]", ConsoleColor.Red);
                        configuration.OutputFolderPath = defaultConfiguration.OutputFolderPath;
                        hasErrorOrWarning = true;
                    }
                }

                if (configuration.WaitTime < 0)
                {
                    ConsoleEx.WriteLineColor($"[Invalid {nameof(Configuration.WaitTime)} value! Cannot have negative time. Using default.]", ConsoleColor.Red);
                    configuration.WaitTime = defaultConfiguration.WaitTime;
                    hasErrorOrWarning = true;
                }
                else if (configuration.WaitTime == 0)
                {
                    ConsoleEx.WriteLineColor("[Warning] Wait time is set to 0 (no wait). This can result into temporal IP ban to Coub's website.", ConsoleColor.Yellow);
                    hasErrorOrWarning = true;
                }
                else if (configuration.WaitTime > 120)
                {
                    ConsoleEx.WriteLineColor($"[Warning] Wait time is set to {configuration.WaitTime} (seconds). This will result into really slow downloading. Was this intentional?", ConsoleColor.Yellow);
                    hasErrorOrWarning = true;
                }

                if (hasErrorOrWarning)
                {
                    // Add extra formatting line
                    Console.WriteLine();
                }
                
                return configuration;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading configuration!\n" + ex.Message);
                Console.WriteLine("Loading default configuration...");
                // Something went wrong, use defaults
                return Configuration.Default;
            }
        }
        
        private static void GetCoubs(string input, Configuration configuration)
        {
            var crawler = new Crawler(configuration);
            var downloader = new Downloader(configuration);

            var toDownload = input?.Split(",", StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray() ?? Array.Empty<string>();
            
            // NSFW coubs should be gone after June 27th, so check if user is trying to download them after this date
            if (configuration.NsfwOnly &&
               DateTime.Now > new DateTime(2022, 6, 28))
            {
                // Configuration contains NSFW only option but 
                ConsoleEx.WriteLineColor("[NSFW ONLY option is enabled, but today is past June 27th 2022. All NSFW coubs are probably gone. Do you want to still proceed?]", ConsoleColor.Red);
                ConsoleEx.WriteColor("Type [Y] for yes or [N] for no (then press enter): ", ConsoleColor.Red);
                var answer = Console.ReadLine()?.ToLower();
                if (answer != "y" && 
                    answer != "yes")
                {
                    // Used decided not to proceed, exit the program
                    ConsoleEx.WriteLineColor("Open [Configuration.json] file and set [\"NsfwOnly\": false]", ConsoleColor.Yellow);
                    return;
                }
            }
            
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