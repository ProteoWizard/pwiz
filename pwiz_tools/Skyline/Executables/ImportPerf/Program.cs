using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ImportPerf
{
    class Program
    {
        private static CommandArgs _cmdArgs;
        private static QueueWorker<List<string>> _queue;
        private static Stopwatch _stopwatch;
        
        static void Main(string[] args)
        {
            _cmdArgs = new CommandArgs();
            if (!_cmdArgs.ParseArgs(args))
                return;

            // Remove all SKYD files
            foreach (var skydFile in Directory.EnumerateFiles(Path.GetDirectoryName(_cmdArgs.FilePath) ?? string.Empty, "*.skyd"))
            {
                try
                {
                    File.Delete(skydFile);
                }
                catch (Exception)
                {
                    Console.Error.WriteLine("Error: Failed to remove existing file {0}", skydFile);
                    return;
                }
            }

            _stopwatch = new Stopwatch();
            _stopwatch.Start();
            _queue = new QueueWorker<List<string>>(Run);
            _queue.RunAsync(_cmdArgs.Processes, "Start Skyline");

            string dataFilter = "*.*";
            if (!string.IsNullOrEmpty(_cmdArgs.DataFilter))
                dataFilter = _cmdArgs.DataFilter;
            string dataDir = ".";
            if (!string.IsNullOrEmpty(_cmdArgs.DataDir))
                dataDir = _cmdArgs.DataDir;
            var fileGroupCurrent = new List<string>();
            var fileGroups = new List<List<string>> {fileGroupCurrent};
            var dataFiles = Directory.EnumerateFiles(dataDir, dataFilter).ToArray();
            int filesPerProcess = (int) Math.Ceiling(((double) dataFiles.Length)/_cmdArgs.Processes);
            foreach (var dataFile in dataFiles)
            {
                if (fileGroupCurrent.Count >= filesPerProcess)
                {
                    fileGroupCurrent = new List<string>();
                    fileGroups.Add(fileGroupCurrent);
                }
                fileGroupCurrent.Add(dataFile);
            }
            _queue.Add(fileGroups, true);

            Console.WriteLine("Elapsed time: " + _stopwatch.Elapsed.ToString(@"mm\:ss"));        
        }

        private static void Run(List<string> dataFiles, int threadIndex)
        {
            AddToLog(threadIndex, "Start " + GetFileNames(dataFiles));
            var args = new List<string>(_cmdArgs.UiArgs)
            {
                "--timestamp",
                "--in=\"" + _cmdArgs.FilePath + "\"",
                "--import-no-join",
                "--import-threads=" + _cmdArgs.Threads
            };
            args.AddRange(dataFiles.Select(f => "--import-file=\"" + f + "\""));
            string argsText = string.Join(" ", args);
            AddToLog(threadIndex, argsText);
            var psi = new ProcessStartInfo
            {
                FileName = _cmdArgs.SkylinePath,
                Arguments = argsText,
                UseShellExecute = false,
            };
            RunProcess(psi, threadIndex);
            AddToLog(threadIndex, "End   " + GetFileNames(dataFiles));
        }

        private static string GetFileNames(List<string> dataFiles)
        {
            return string.Join(", ", dataFiles.Select(Path.GetFileName).ToArray());
        }

        private static void RunProcess(ProcessStartInfo psi, int threadIndex)
        {
            // Make sure required streams are redirected.
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            var proc = Process.Start(psi);
            if (proc == null)
                throw new IOException(string.Format("Failure starting {0} command.", psi.FileName)); // Not L10N

            var reader = new ProcessStreamReader(proc);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                AddToLog(threadIndex, line);
            }
            proc.WaitForExit();
        }

        private static readonly object LogLock = new object();

        private static void AddToLog(int threadIndex, string line)
        {
            // If multiple process, prefix with a 1-based thread/process index
            if (_cmdArgs.Processes > 1)
                line = (threadIndex + 1) + "> " + line;

            lock (LogLock)
            {
                Console.WriteLine(line);
            }
        }
    }
}
