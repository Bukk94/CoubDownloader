using System;
using System.Globalization;
using System.IO;
using System.Linq;
using CoubDownloader.Configurations;
using CoubDownloader.Extensions;

namespace CoubDownloader
{
    public class Downloader
    {
        private const string DownloadedArchive = "downloaded.txt";
        private readonly Configuration _configuration;
        private string coubsDir = Path.Combine(Environment.CurrentDirectory, Constants.CoubDataDir);

        public Downloader(Configuration configuration)
        {
            _configuration = configuration;

            if (!string.IsNullOrWhiteSpace(_configuration.OutputFolderPath))
            {
                // Overwrite default path with user's one
                coubsDir = Path.Combine(_configuration.OutputFolderPath, Constants.CoubDataDir);
            }
        }
        
        public void DownloadCoubs(string infoPath, string[] dirs)
        {
            var directoriesToDownload = dirs.Any() ? dirs : Directory.GetDirectories(infoPath);
            
            foreach (var directory in directoriesToDownload)
            {
                var dir = new DirectoryInfo(directory).Name;
                if (!HasUrlList(infoPath, dir))
                {
                    Console.WriteLine($"No URL list for category '{dir}' found, skipping download...");
                    continue;
                }
                
                DownloadCoubsCategory(dir);
            }
        }

        private static bool HasUrlList(string path, string dir)
        {
            var filename = Path.Combine(Path.Combine(path, dir), Constants.UrlListFileName);
            return File.Exists(filename);
        }
        
        private void DownloadCoubsCategory(string dir)
        {
            ConsoleEx.WriteLineColor($"[Starting download of '{dir}'...]", ConsoleColor.Yellow);

            EnsureInput(dir);

            try
            {
                ExecuteCommand(
                    $"{Constants.CoubInfoDir}\\{dir}\\{Constants.UrlListFileName}",
                    $"{Constants.CoubDataDir}\\{dir}\\%id%_%title%");

                var repostsList = $"{Constants.CoubInfoDir}\\{dir}\\{Constants.RepostUrlListFileName}";
                if (File.Exists(repostsList))
                {
                    ConsoleEx.WriteLineColor($"[Starting download of '{dir}' reposts...]", ConsoleColor.Yellow);
                    
                    var repostsPath = $"{Constants.CoubDataDir}\\{dir}\\{Constants.RepostsDir}";
                    if (!Directory.Exists(repostsPath))
                    {
                        Directory.CreateDirectory(repostsPath);
                    }
                    
                    ExecuteCommand(
                        repostsList,
                        $"{repostsPath}\\%id%_%title%");
                }
                
                ConsoleEx.WriteLineColor("[DONE]", ConsoleColor.Blue);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex}");
            }
        }
        
        private void ExecuteCommand(string inputList, string target)
        {
            Run.RunCommand(
                $"python.exe -X utf8 coub_v2.py " +
                    $"-l {inputList} " +
                    $"-o \"{GetOutputDirectory(target)}\" " +
                    $"--use-archive {Constants.CoubInfoDir}\\{DownloadedArchive} " +
                    $"--sleep {_configuration.WaitTime.ToString(CultureInfo.InvariantCulture)} " +
                    $"--repeat {(_configuration.Loops <= 0 ? 1000 : _configuration.Loops)} " +
                    $"{GetVideoSettings()} " +
                    $"{ShouldKeepIndividualStreams()}",
                Environment.CurrentDirectory);
        }

        private string GetVideoSettings()
        {
            return _configuration.VideoQuality switch
            {
                VideoQuality.Highest => "--bestvideo",
                VideoQuality.Medium => "--mediumvideo",
                VideoQuality.Low => "--worstvideo",
                _ => ""
            };
        }

        private string GetOutputDirectory(string target)
        {
            if (string.IsNullOrWhiteSpace(_configuration.OutputFolderPath))
            {
                return target;
            }

            // Ensure path exists
            Directory.CreateDirectory(_configuration.OutputFolderPath);
            return Path.Combine(_configuration.OutputFolderPath, target);
        }

        private string ShouldKeepIndividualStreams()
        {
            return _configuration.KeepAudioVideo ? "--keep" : "";
        }
        
        private string GetDownloadLocation(string dir)
        {
            return Path.Combine(coubsDir, dir);
        }

        private void EnsureInput(string dir)
        {
            // coub_v2.py is creating temp list for data
            // It might be left behind if something happens, clean that up before starting
            var tmpList = Path.Combine(Environment.CurrentDirectory, "list.txt");
            if (File.Exists(tmpList))
            {
                File.Delete(tmpList);
            }
            
            // Main coub directory
            if (!Directory.Exists(coubsDir))
            {
                Directory.CreateDirectory(coubsDir);
            }

            var targetDirectory = GetDownloadLocation(dir);
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }
        }
    }
}