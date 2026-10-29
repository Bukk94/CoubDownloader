using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CoubDownloader.Configurations;
using CoubDownloader.Extensions;
using CoubDownloader.Models;
using Newtonsoft.Json;

namespace CoubDownloader;

public class Program
{
    private static readonly string Version =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

    private const string SupportUrl = "https://ko-fi.com/bukk94";

    public static async Task Main()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                // Try to set width to nicely match text, but if that fails it's no big deal.
                Console.SetWindowSize(130, Console.WindowHeight);
            }
            catch
            {
                // Window is bigger than the screen allows or the console does not support resizing
            }
        }
        
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
            await GetCoubsAsync(input, configuration);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);

            var logFile = Path.Combine(Environment.CurrentDirectory, "err.log");
            File.WriteAllText(logFile, ex.ToString());
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey(true);
    }

    private static string GetDownloadInput()
    {
        ConsoleEx.WriteLineColor("[What do you want to download?]", ConsoleColor.Green);
        Console.WriteLine("You can download your liked coubs or bookmarks by typing liked or bookmarks,");
        Console.WriteLine("and any channel by entering its username (not displayname!) or its URL.");
        Console.WriteLine("You can also enter coub URLs, or a path to a text file with coub URLs (one per line).");
        Console.WriteLine("You can combine multiple entries, separated by comma.");
        Console.WriteLine("Leave input empty and just press enter to download everything that was already gathered.\n");
        Console.WriteLine("Input example: liked,bookmarks,channelone,C:\\coubs\\list.txt,https://coub.com/view/19wef6");

        ConsoleEx.WriteColor("[Input]: ", ConsoleColor.Green);
        var input = Console.ReadLine();

        return input?.Trim();
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

            if (!string.IsNullOrWhiteSpace(configuration.ProxyUrl))
            {
                if (!Uri.TryCreate(configuration.ProxyUrl, UriKind.Absolute, out var proxyUri) ||
                    (proxyUri.Scheme != Uri.UriSchemeHttp && proxyUri.Scheme != Uri.UriSchemeHttps))
                {
                    ConsoleEx.WriteLineColor($"[Invalid {nameof(Configuration.ProxyUrl)} '{configuration.ProxyUrl}', only http:// or https:// proxies are supported. Proxy will not be used.]", ConsoleColor.Red);
                    configuration.ProxyUrl = null;
                    hasErrorOrWarning = true;
                }
                else
                {
                    ConsoleEx.WriteLineColor($"[Proxy enabled: {configuration.ProxyUrl}]", ConsoleColor.Yellow);
                    hasErrorOrWarning = true;
                }
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
        
    private static async Task GetCoubsAsync(string input, Configuration configuration)
    {
        var crawler = new Crawler(configuration);
        var downloader = new Downloader(configuration);

        var toDownload = input?.Split(",", StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().Trim('"'))
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
            
        var directories = Array.Empty<string>();
        if (!toDownload.Any())
        {
            Console.Error.WriteLine("No user input, looking for already downloaded links...");
            await crawler.ResolveUrlListsAsync();
        }
        else
        {
            directories = await crawler.CrawlUrlsAsync(toDownload);
        }

        var summary = await downloader.DownloadCoubsAsync(crawler.InfoPath, directories);

        WriteFinished(summary);
    }

    private static void WriteFinished(DownloadSummary summary)
    {
        const string line = "[============================================================]";

        Console.WriteLine();

        if (summary.HasProblems)
        {
            ConsoleEx.WriteLineColor(line, ConsoleColor.Yellow);
            ConsoleEx.WriteLineColor("[FINISHED WITH PROBLEMS!] Some coubs or lists could not be downloaded.", ConsoleColor.Yellow);
            Console.WriteLine($"Downloaded: {summary.Downloaded}, skipped: {summary.Skipped}, failed: {summary.Failed}");
            Console.WriteLine("Check the messages above to see what went wrong.");
            ConsoleEx.WriteLineColor(line, ConsoleColor.Yellow);
            return;
        }

        ConsoleEx.WriteLineColor(line, ConsoleColor.Green);
        ConsoleEx.WriteLineColor("[ALL DONE!] Everything has been processed.", ConsoleColor.Green);
        Console.WriteLine($"Downloaded: {summary.Downloaded}, skipped: {summary.Skipped}");
        Console.WriteLine("Thank you for using Coub Downloader.");
        ConsoleEx.WriteLineColor($"If you like it, you can support its development on Ko-fi: [{SupportUrl}]", ConsoleColor.Cyan);
        ConsoleEx.WriteLineColor(line, ConsoleColor.Green);
    }
}