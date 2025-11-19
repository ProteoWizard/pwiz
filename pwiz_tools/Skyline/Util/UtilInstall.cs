/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using Ionic.Zip;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Util
{
    public interface IAsynchronousDownloadClient : IDisposable
    {
        /// <summary>
        /// Downloads the file and throws on error or cancellation.
        /// </summary>
        void DownloadFileAsyncOrThrow(Uri address, string path);
    }
    
    public class MultiFileAsynchronousDownloadClient : IAsynchronousDownloadClient
    {
        private readonly IProgressMonitor _progressMonitor;
        private IProgressStatus _progressStatus;


        /// <summary>
        /// The asynchronous download client links a webClient to a LongWaitBroker. It supports
        /// multiple asynchronous downloads, and updates the associated broker's progress value.
        /// </summary>
        /// <param name="waitBroker">The associated LongWaitBroker</param>
        /// <param name="files">The numbers of file this instance is expected to download. This
        /// is used to accurately update the broker's progress value</param>
        public MultiFileAsynchronousDownloadClient(IProgressMonitor waitBroker, int files)
        {
            _progressMonitor = waitBroker;
            _progressStatus = new ProgressStatus().ChangeSegments(0, files);
        }


        public void DownloadFileAsyncOrThrow(Uri address, string path)
        {
            var file = Regex.Match(address.AbsolutePath, @"[^/]*$");
            _progressStatus = _progressStatus.ChangeMessage(string.Format(UtilResources.MultiFileAsynchronousDownloadClient_DownloadFileAsync_Downloading__0_, file));
            _progressMonitor.UpdateProgress(_progressStatus);

            using var httpClient = new HttpClientWithProgress(_progressMonitor, _progressStatus);
            httpClient.DownloadFile(address, path);
        }
 
        #region IDisposable Members

        public void Dispose()
        {
            // nothing to dispose currently
        }

        #endregion
    }

    public struct FileDownloadInfo
    {
        /// <summary>
        /// The name of the file after downloading. For ZIP files (Unzip is true), the file will be deleted after unzipping.
        /// </summary>
        public string Filename;

        /// <summary>
        /// The path to download the file to, and to unzip if Unzip is true.
        /// </summary>
        public string InstallPath;

        /// <summary>
        /// If not null, the path to a file or directory which if present indicates the file has been installed.
        /// </summary>
        public string CheckInstalledPath;

        /// <summary>
        /// The online location of the file to download.
        /// </summary>
        public Uri DownloadUrl;

        /// <summary>
        /// If true, existing files will be overwritten (including during the unzip step).
        /// </summary>
        public bool OverwriteExisting;

        /// <summary>
        /// If true, the file must be a ZIP file and it will unzipped into InstallPath after downloading.
        /// </summary>
        public bool Unzip;
        
        /// <summary>
        /// The enum-based value for this kind of tool in the SearchTool system (that allows users to provide their own local versions of tools).
        /// </summary>
        public SearchToolType ToolType;

        /// <summary>
        /// The final path to the executable tool.
        /// </summary>
        public string ToolPath;

        /// <summary>
        /// Extra argument to pass to the tool's command-line.
        /// </summary>
        public string ToolExtraArgs;
    }

    public static class JavaDownloadInfo
    {
        static string JRE_FILENAME = @"jre-17.0.1";

        /// <summary>
        /// This custom OpenJDK JRE was created with https://justinmahar.github.io/easyjre/
        /// </summary>
        static Uri JRE_URL = new Uri($@"https://ci.skyline.ms/skyline_tool_testing_mirror/{JRE_FILENAME}.zip");
        public static string JavaDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), JRE_FILENAME);
        public static string JavaBinary => Settings.Default.SearchToolList.GetToolPathOrDefault(SearchToolType.Java, Path.Combine(JavaDirectory, JRE_FILENAME, @"bin", @"java.exe"));
        public static string JavaExtraArgs => Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.Java, JavaMaxHeapArg(2 * MemoryInfo.TotalBytes / 3));

        // TODO: Try to find a pre-existing installation of Java instead of downloading: https://stackoverflow.com/questions/3038140/how-to-determine-windows-java-installation-location
        public static FileDownloadInfo[] FilesToDownload => new[]
        {
            new FileDownloadInfo
            {
                Filename = JRE_FILENAME, InstallPath = JavaDirectory, DownloadUrl = JRE_URL, OverwriteExisting = true, Unzip = true,
                ToolType = SearchToolType.Java, ToolPath = JavaBinary, ToolExtraArgs = JavaExtraArgs
            }
        }; // N.B. lazy evaluation so that JavaDirectory reflects current Tools directory, which may change from test to test

        public static string JavaMaxHeapArg(long maxHeapBytes) => $@"-Xmx{maxHeapBytes / 1024 / 1024}M";
    }

    public static class Java8DownloadInfo
    {
        static string JRE_FILENAME = @"openlogic-openjdk-jre-8u342-b07-windows-x64";
        private static string JRE_SUBDIRECTORY = @"openlogic-openjdk-jre-8u342-b07-windows-64";

        /// <summary>
        /// This custom OpenJDK JRE was created with https://justinmahar.github.io/easyjre/
        /// </summary>
        static Uri JRE_URL = new Uri($@"https://ci.skyline.ms/skyline_tool_testing_mirror/{JRE_FILENAME}.zip");
        public static string JavaDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory());
        public static string JavaBinary => Settings.Default.SearchToolList.GetToolPathOrDefault(SearchToolType.Java8, Path.Combine(JavaDirectory, JRE_SUBDIRECTORY, @"bin", @"java.exe"));
        public static string JavaExtraArgs => Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.Java8, JavaDownloadInfo.JavaMaxHeapArg(2 * MemoryInfo.TotalBytes / 3));

        public static FileDownloadInfo[] FilesToDownload => new[]
        {
            new FileDownloadInfo
            {
                Filename = JRE_FILENAME, InstallPath = JavaDirectory, CheckInstalledPath = JavaBinary, DownloadUrl = JRE_URL, OverwriteExisting = true, Unzip = true,
                ToolType = SearchToolType.Java8, ToolPath = JavaBinary, ToolExtraArgs = JavaExtraArgs
            }
        }; // N.B. lazy evaluation so that JavaDirectory reflects current Tools directory, which may change from test to test
    }

    public static class SimpleFileDownloader
    {
        private static readonly string SKYLINE_TOOL_TESTING_MIRROR_URL = @"https://ci.skyline.ms/skyline_tool_testing_mirror";

        public static bool FileAlreadyDownloaded(FileDownloadInfo requiredFile)
        {
            string requiredFilePath = requiredFile.Unzip
                ? requiredFile.CheckInstalledPath ?? requiredFile.InstallPath
                : Path.Combine(requiredFile.InstallPath, requiredFile.Filename);
            requiredFilePath = Settings.Default.SearchToolList.GetToolPathOrDefault(requiredFile.ToolType, requiredFilePath);

            bool alreadyDownloaded = File.Exists(requiredFilePath) || Directory.Exists(requiredFilePath);

            if (!alreadyDownloaded)
                return false;
            
            if (!Settings.Default.SearchToolList.ContainsKey(requiredFile.ToolType))
                Settings.Default.SearchToolList.Add(new SearchTool(requiredFile.ToolType, requiredFile.ToolPath, requiredFile.ToolExtraArgs, requiredFile.InstallPath, true));
            return true;
        }

        public static IEnumerable<FileDownloadInfo> FilesNotAlreadyDownloaded(IEnumerable<FileDownloadInfo> requiredFiles)
        {
            return requiredFiles.Where(f => !FileAlreadyDownloaded(f));
        }

        public static IEnumerable<SearchTool> SearchToolsConfiguredButMissing(IEnumerable<FileDownloadInfo> requiredFiles)
        {
            foreach(var requiredFile in requiredFiles)
            {
                if (Settings.Default.SearchToolList.ContainsKey(requiredFile.ToolType))
                {
                    var searchTool = Settings.Default.SearchToolList[requiredFile.ToolType];
                    if (!searchTool.AutoInstalled && !File.Exists(searchTool.Path))
                        yield return searchTool;
                }
            }
        }

        private static void LogToConsole(string message)
        {
            if (TryHelper.RunningResharperAnalysis)
            {
                Trace.WriteLine(@"# (ReSharper Analysis Trace) " + message);
            }
            Console.WriteLine(@"# " + message);
        }

        public static bool DownloadRequiredFiles(IEnumerable<FileDownloadInfo> filesToDownload, IProgressMonitor waitBroker)
        {
            var filesNotAlreadyDownloaded = FilesNotAlreadyDownloaded(filesToDownload).ToList();
            using (var client = new MultiFileAsynchronousDownloadClient(waitBroker, filesNotAlreadyDownloaded.Count))
            {
                Stopwatch downloadTimer = null;
                Stopwatch unzipTimer = null;
                if (Program.UnitTest)
                {
                    downloadTimer = new Stopwatch();
                    unzipTimer = new Stopwatch();
                }

                void addSearchToolsForRequiredFilesGroup(IEnumerable<FileDownloadInfo> requiredFiles)
                {
                    foreach(var requiredFile in requiredFiles)
                        Settings.Default.SearchToolList.Add(new SearchTool(requiredFile.ToolType, requiredFile.ToolPath, requiredFile.ToolExtraArgs, requiredFile.InstallPath, true));
                }

                foreach (var requiredFileGroup in filesNotAlreadyDownloaded.GroupBy(f => f.Filename))
                {
                    var requiredFile = requiredFileGroup.First();
                    
                    // For testing, replace the hostname with the Skyline tool testing mirror path on AWS
                    var downloadUrl = requiredFile.DownloadUrl;
                    if (Program.UnitTest && !Program.UseOriginalURLs)
                        downloadUrl = new Uri(Regex.Replace(downloadUrl.OriginalString, ".*/(.*)", $"{SKYLINE_TOOL_TESTING_MIRROR_URL}/$1"));

                    var useCachedDownloads = Program.UnitTest; // Cache downloads in case of tests running in parallel
                    var destinationFilename = Path.Combine(requiredFile.InstallPath, requiredFile.Filename);
                    string downloadFilename = useCachedDownloads
                        ? Path.Combine(GetCachedDownloadsDirectory(), requiredFile.DownloadUrl.Segments.Last()) :
                        requiredFile.Unzip ? Path.GetTempFileName() : destinationFilename;

                    if (useCachedDownloads && File.Exists(downloadFilename))
                    {
                        LogToConsole($@"Using cached test data from {downloadUrl} in {downloadFilename} as {destinationFilename}...");
                        if (!requiredFile.Unzip)
                        {
                            TryHelper.TryTwice(() => File.Copy(downloadFilename, destinationFilename));
                        }
                        downloadTimer = null;
                    }
                    else
                    {
                        if (downloadTimer != null)
                        {
                            LogToConsole($@"Downloading test data file {downloadUrl} to {downloadFilename}...");
                            downloadTimer.Start();
                        }

                        using (var fileSaver = new FileSaver(downloadFilename))
                        {
                            client.DownloadFileAsyncOrThrow(downloadUrl, fileSaver.SafeName);
                            fileSaver.Commit();
                        }

                        if (downloadTimer != null)
                        {
                            downloadTimer.Stop();
                            LogToConsole($@"Done downloading test data file {downloadUrl} to {downloadFilename}");
                        }
                    }

                    if (!requiredFile.Unzip)
                    {
                        if (useCachedDownloads && !File.Exists(destinationFilename))
                            File.Copy(downloadFilename, destinationFilename);
                        addSearchToolsForRequiredFilesGroup(requiredFileGroup);
                        continue;
                    }

                    if (unzipTimer != null)
                    {
                        LogToConsole($@"Unzipping test data file {Path.GetFileName(downloadFilename)} to {requiredFile.InstallPath}...");
                        unzipTimer.Start();
                    }

                    Directory.CreateDirectory(requiredFile.InstallPath);
                    using (var zipFile = new ZipFile(downloadFilename))
                    {
                        zipFile.ExtractAll(requiredFile.InstallPath, requiredFile.OverwriteExisting ? ExtractExistingFileAction.OverwriteSilently : ExtractExistingFileAction.DoNotOverwrite);
                    }

                    if (unzipTimer != null)
                    {
                        unzipTimer.Stop();

                        LogToConsole($@"Done unzipping test data file {Path.GetFileName(downloadFilename)} to {requiredFile.InstallPath}...");
                    }

                    if (!useCachedDownloads)
                    {
                        FileEx.SafeDelete(downloadFilename);
                    }
                    addSearchToolsForRequiredFilesGroup(requiredFileGroup);
                }

                if (downloadTimer != null)
                {
                    LogToConsole($@"Total download time {downloadTimer.ElapsedMilliseconds / 1000.0:F2} sec ");
                }

                if (unzipTimer != null)
                {
                    LogToConsole($@"Total unzip time {unzipTimer.ElapsedMilliseconds / 1000.0:F2} sec ");
                }
            }
            return true;
        }

        public static string GetCachedDownloadsDirectory()
        {
            return Path.Combine(ToolDescriptionHelpers.GetSkylineInstallationPath(), @"CachedDownloadsForTests");
        }
    }

    public interface ISkylineProcessRunnerWrapper
    {
        /// <summary>
        /// Wrapper interface for the NamedPipeProcessRunner class
        /// </summary>
        int RunProcess(string arguments, bool runAsAdministrator, TextWriter writer, bool createNoWindow = false, CancellationToken cancellationToken = default);
    }

    public class SkylineProcessRunnerWrapper : ISkylineProcessRunnerWrapper
    {
        public int RunProcess(string arguments, bool runAsAdministrator, TextWriter writer, bool createNoWindow = false, CancellationToken cancellationToken = default)
        {
            return SkylineProcessRunner.RunProcess(arguments, runAsAdministrator, writer, createNoWindow, cancellationToken);
        }
    }

    // The test Named PipeProcess Runner allows us to simulate running NamedPipeProcessRunner.exe
    // by specifying its return code and whether the pipe connected or not

    /// <summary>
    /// The IProcessRunner serves as a wrapper class for running a process synchronously
    /// </summary>
    public interface IRunProcess
    {
        /// <summary>
        /// Returns the exit code that results from running a process
        /// </summary>
        int RunProcess(Process process);
    }

    public class SynchronousRunProcess : IRunProcess
    {
        public int RunProcess(Process process)
        {
            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
    }
}
