using System;
using System.IO;

namespace CoubDownloader.Services;

public static class CoubPaths
{
    public static string InfoPath => Path.Combine(Environment.CurrentDirectory, Constants.CoubInfoDir);

    public static string GetDataPath(string dir, string filename)
    {
        return Path.Combine(Path.Combine(InfoPath, dir), filename);
    }
}
