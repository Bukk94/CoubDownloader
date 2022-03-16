using System;
using System.Diagnostics;
using System.IO;

namespace CoubDownloader
{
    public static class Run
    {
        public static void RunCommand(string commandToRun, string workingDirectory = null)
        {
            if (string.IsNullOrEmpty(workingDirectory))
            {
                workingDirectory = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());
            }

            var processStartInfo = new ProcessStartInfo()
            {
                FileName = "cmd",
                RedirectStandardOutput = false,
                RedirectStandardInput = true,
                WorkingDirectory = workingDirectory
            };

            var process = Process.Start(processStartInfo);

            if (process == null)
            {
                throw new Exception("Process should not be null.");
            }

            process.StandardInput.WriteLine($"{commandToRun} & exit");
            process.WaitForExit();
        }
    }
}