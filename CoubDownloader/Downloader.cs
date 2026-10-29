using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CoubDownloader.Configurations;
using CoubDownloader.Extensions;
using CoubDownloader.Models;
using Newtonsoft.Json;

namespace CoubDownloader;

public class Downloader
{
    private const string DownloadedArchive = "downloaded.txt";
    private readonly Configuration _configuration;
    private readonly HttpClient _http;
    private readonly string _coubsDir = Path.Combine(Environment.CurrentDirectory, Constants.CoubDataDir);

    public Downloader(Configuration configuration)
    {
        _configuration = configuration;
        _http = CoubHttp.Create(configuration);

        if (!string.IsNullOrWhiteSpace(_configuration.OutputFolderPath))
        {
            // Overwrite default path with user's one
            _coubsDir = Path.Combine(_configuration.OutputFolderPath, Constants.CoubDataDir);
        }
    }

    public async Task DownloadCoubsAsync(string infoPath, string[] dirs)
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

            await DownloadCoubsCategoryAsync(infoPath, dir);
        }
    }

    private static bool HasUrlList(string path, string dir)
    {
        var filename = Path.Combine(Path.Combine(path, dir), Constants.UrlListFileName);
        return File.Exists(filename);
    }

    private async Task DownloadCoubsCategoryAsync(string infoPath, string dir)
    {
        ConsoleEx.WriteLineColor($"[Starting download of '{dir}'...]", ConsoleColor.Yellow);

        var mediaPath = Path.Combine(infoPath, dir, Constants.MediaListFileName);
        if (!File.Exists(mediaPath))
        {
            ConsoleEx.WriteLineColor($"[No media info found for '{dir}', nothing to download.]", ConsoleColor.Red);
            return;
        }

        var hasFfmpeg = await HasFfmpegAsync();
        if (!hasFfmpeg)
        {
            ConsoleEx.WriteLineColor("[ffmpeg was not found. Install it and make sure it is available in PATH.]", ConsoleColor.Red);
            return;
        }

        try
        {
            var coubs = JsonConvert.DeserializeObject<List<CoubMedia>>(await File.ReadAllTextAsync(mediaPath));
            var archivePath = Path.Combine(infoPath, DownloadedArchive);
            var archive = File.Exists(archivePath)
                ? new HashSet<string>(await File.ReadAllLinesAsync(archivePath))
                : new HashSet<string>();

            var targetDir = GetDownloadLocation(dir);
            var repostsDir = Path.Combine(targetDir, Constants.RepostsDir);
            Directory.CreateDirectory(targetDir);
            if (coubs.Any(x => x.IsRepost))
            {
                Directory.CreateDirectory(repostsDir);
            }

            var count = 0;
            var downloaded = 0;
            var skipped = 0;
            var failed = 0;
            foreach (var coub in coubs)
            {
                count++;
                Console.WriteLine($"  {count} out of {coubs.Count} ({coub.Permalink})");

                var coubDir = coub.IsRepost ? repostsDir : targetDir;

                if (archive.Contains(coub.Permalink))
                {
                    Console.WriteLine("    Already downloaded, skipping...");
                    skipped++;
                    continue;
                }

                if (File.Exists(GetPath(coub, coubDir) + ".mp4"))
                {
                    Console.WriteLine("    File already exists, skipping...");
                    await AddToArchiveAsync(archive, archivePath, coub.Permalink);
                    skipped++;
                    continue;
                }

                try
                {
                    var isDownloaded = await DownloadCoubAsync(coub, coubDir);
                    if (!isDownloaded)
                    {
                        failed++;
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    ConsoleEx.WriteLineColor($"    [Failed to download coub '{coub.Permalink}']: {ex.Message}", ConsoleColor.Red);
                    failed++;
                    continue;
                }

                downloaded++;
                await AddToArchiveAsync(archive, archivePath, coub.Permalink);

                await Task.Delay(_configuration.WaitDelay);
            }

            ConsoleEx.WriteLineColor($"[DONE:] Downloaded: {downloaded}, skipped: {skipped}, failed: {failed}", ConsoleColor.Blue);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex}");
        }
    }

    private static async Task AddToArchiveAsync(HashSet<string> archive, string archivePath, string permalink)
    {
        archive.Add(permalink);
        await File.AppendAllTextAsync(archivePath, permalink + "\n");
    }

    private static string GetPath(CoubMedia coub, string targetDir)
    {
        return Path.Combine(targetDir, $"{coub.Permalink}_{GetSafeName(coub.Title)}");
    }

    private async Task<bool> DownloadCoubAsync(CoubMedia coub, string targetDir)
    {
        var videoUrl = GetVideoUrl(coub);
        if (videoUrl == null)
        {
            ConsoleEx.WriteLineColor("    [Coub has no video available]", ConsoleColor.Red);
            return false;
        }

        var name = GetPath(coub, targetDir);
        var videoPath = name + "_raw.mp4";
        var audioPath = name + ".mp3";
        var outputPath = name + ".mp4";

        await DownloadFileAsync(videoUrl, videoPath);

        var audioUrl = coub.AudioUrl ?? coub.AudioMedUrl;
        var hasAudio = false;
        if (audioUrl != null)
        {
            try
            {
                await DownloadFileAsync(audioUrl, audioPath);
                hasAudio = true;
            }
            catch (HttpRequestException)
            {
                ConsoleEx.WriteLineColor("    [Audio is unavailable, saving video only]", ConsoleColor.Yellow);
            }
        }

        if (!hasAudio)
        {
            File.Move(videoPath, outputPath, true);
            return true;
        }

        await MergeAsync(videoPath, audioPath, outputPath);

        if (!_configuration.KeepAudioVideo)
        {
            File.Delete(videoPath);
            File.Delete(audioPath);
        }

        return true;
    }

    private string GetVideoUrl(CoubMedia coub)
    {
        return _configuration.VideoQuality switch
        {
            VideoQuality.Medium => coub.VideoSdUrl ?? coub.VideoLowUrl ?? coub.VideoHdUrl,
            VideoQuality.Low => coub.VideoLowUrl ?? coub.VideoSdUrl ?? coub.VideoHdUrl,
            _ => coub.VideoHdUrl ?? coub.VideoSdUrl ?? coub.VideoLowUrl
        };
    }

    private static string GetSafeName(string title)
    {
        var name = string.Concat(title.RemoveLinebreaks().Select(x => Path.GetInvalidFileNameChars().Contains(x) ? '_' : x));
        name = name.Length > 100 ? name.Substring(0, 100) : name;

        return name.TrimEnd('.', ' ');
    }

    private async Task DownloadFileAsync(string url, string path)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var target = File.Create(path);
        await source.CopyToAsync(target);
    }

    private async Task MergeAsync(string videoPath, string audioPath, string outputPath)
    {
        // Loop video until audio ends (or for configured number of loops)
        var loops = _configuration.Loops > 0 ? _configuration.Loops - 1 : -1;

        // Merge into a temporary file, so an interrupted merge never leaves a broken coub behind
        var tempPath = Path.ChangeExtension(outputPath, ".tmp.mp4");

        try
        {
            await RunFfmpegAsync("-y", "-v", "error",
                "-stream_loop", loops.ToString(), "-i", videoPath,
                "-i", audioPath,
                "-c", "copy", "-shortest", tempPath);

            File.Move(tempPath, outputPath, true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static async Task<bool> HasFfmpegAsync()
    {
        try
        {
            await RunFfmpegAsync("-version");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunFfmpegAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("ffmpeg")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        // Both streams must be drained at the same time, otherwise ffmpeg can block on a full pipe
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new Exception($"ffmpeg failed: {error}");
        }
    }

    private string GetDownloadLocation(string dir)
    {
        return Path.Combine(_coubsDir, dir);
    }
}