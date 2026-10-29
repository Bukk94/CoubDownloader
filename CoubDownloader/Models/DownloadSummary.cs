namespace CoubDownloader.Models;

public class DownloadSummary
{
    public int Downloaded { get; set; }

    public int Skipped { get; set; }

    public int Failed { get; set; }

    // Categories that could not be processed at all (missing list, missing ffmpeg...)
    public int Problems { get; set; }

    public bool HasProblems => Failed > 0 || Problems > 0;

    public void Add(DownloadSummary other)
    {
        Downloaded += other.Downloaded;
        Skipped += other.Skipped;
        Failed += other.Failed;
        Problems += other.Problems;
    }
}
