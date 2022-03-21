using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CoubDownloaderNoWatermark
{
    public class Program
    {
        // Work - in - progress
        static void Main(string[] args)
        {
            Console.Write("Coub ID: ");
            var coubId = Console.ReadLine(); // "30jb4y"
            
            var coubJson = DownloadJson($"https://coub.com/api/v2/coubs/{coubId}");
            dynamic coubData = JObject.Parse(coubJson);
            
            var segmentsJson = DownloadJson($"https://coub.com/api/v2/coubs/{coubId}/segments");
            dynamic segmentsData = JObject.Parse(segmentsJson);

            string coubPermalink = segmentsData.coub.permalink.ToString();
            var targetFolder = Path.Combine(Environment.CurrentDirectory, coubPermalink);
            Directory.CreateDirectory(targetFolder);
            
            string contents = "";
            int numberOfSegments = 0;
            foreach (var segment in segmentsData.segments)
            {
                ++numberOfSegments;
                var path = targetFolder + string.Format("\\{0}.mp4", numberOfSegments);
                bool downloadSuccessful = DownloadFile(segment.cutter_mp4_dashed.file.ToString(), path);
                contents += downloadSuccessful ? 
                    string.Format("file '{0}\\{1}.mp4'", targetFolder, numberOfSegments) + "\r\n" 
                    : "";
            }
            
            var audioPath = targetFolder + "\\high.mp3";
            if (!DownloadFile(coubData.file_versions.html5.audio.high.url.ToString(), audioPath))
            {
                // If high audio is missing, fallback to medium
                audioPath = targetFolder + "\\med.mp3";
                DownloadFile(coubData.file_versions.html5.audio.med.url.ToString(), audioPath);
            }
            
            // Save data for ffmpeg export
            string filesList = targetFolder + "\\files.txt";
            System.IO.File.AppendAllText(filesList, contents);
                  
            ffmpeg(true, 
                filesList, 
                audioPath, 
                targetFolder + "\\" + coubPermalink + ".mp4");
        }
        
        // FFMPEG generation logic
        private static void ffmpeg(bool segment, string video, string audio, string save)
        {
            try
            {
                // Check if ffmpeg exists, otherwise skip
                if (System.IO.File.Exists(Environment.CurrentDirectory + "\\ffmpeg\\x64.exe"))
                {
                    using (var process = Process.Start(new ProcessStartInfo
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = Environment.CurrentDirectory + "\\ffmpeg\\x64.exe",
                        Arguments = (segment
                                        ? "-hwaccel_device 1 -threads 2 -f concat -safe 0 "
                                        : "-hwaccel_device 1 -threads 2 ")
                                    + "-stream_loop -1 -i \"" + video + "\" -i \"" + audio + "\"" +
                                    //string.Format(" -t \"{0}\"", 30) + // max 30 sec 
                                    " -movflags +faststart -c copy -shortest -map 0:v:0 -map 1:a:0 -y \"" + save +
                                    "\""
                    }))
                    {
                        process.WaitForExit();
                    }
                }
                
                Console.WriteLine(
                    "The executable file ffmpeg was not found in the root folder of the program.\r\nFinal video was not combined.");
            }
            catch (Exception ex)
            {
                // TODO: log
            }
        }
        
        private static string DownloadJson(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);

            var response = (HttpWebResponse)request.GetResponse();

            using var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
            
            return reader.ReadToEnd();
        }
        
        private static bool DownloadFile(string url, string path)
        {
            if (url == null || path == null)
                return false;
            
            try
            {
                using (var webClient = new WebClient())
                {
                    webClient.DownloadFile(url, path);
                    
                }
                
                return File.Exists(path);
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}