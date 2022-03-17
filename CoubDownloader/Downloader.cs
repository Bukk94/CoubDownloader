﻿using System;
using System.IO;

namespace CoubDownloader
{
    public class Downloader
    {
        private const string DownloadedArchive = "downloaded.txt";
        private string coubsDir = Path.Combine(Environment.CurrentDirectory, Constants.CoubDataDir);

        public void DownloadCoubs(string path)
        {
            var directoriesToDownload = Directory.GetDirectories(path);
            foreach (var directory in directoriesToDownload)
            {
                var dir = new DirectoryInfo(directory).Name;
                if (!HasUrlList(path, dir))
                {
                    Console.WriteLine($"No URL list for category '{dir}' found, skipping download...");
                    continue;
                }
                
                DownloadCoubsCategory(dir);
            }
        }

        private bool HasUrlList(string path, string dir)
        {
            var filename = Path.Combine(Path.Combine(path, dir), Constants.UrlListFileName);
            return File.Exists(filename);
        }
        
        private void DownloadCoubsCategory(string dir)
        {
            Console.WriteLine($"Starting download of '{dir}'...");

            EnsureInput(dir);

            try
            {
                Run.RunCommand(
                    $"python.exe -X utf8 coub_v2.py -l {Constants.CoubInfoDir}\\{dir}\\{Constants.UrlListFileName} -o \"{Constants.CoubDataDir}\\{dir}\\%id%_%title%\" --use-archive {Constants.CoubInfoDir}\\{DownloadedArchive}",
                    Environment.CurrentDirectory);
                
                Console.WriteLine("DONE");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Fatal error: " + ex);
            }
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