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
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    public interface IAsynchronousDownloadClient : IDisposable
    {
        /// <summary>
        /// Attempts to download the file located at the specified address to the specified
        /// file path. This function returns true if the download succeeded.
        /// </summary>
        bool DownloadFileAsync(Uri address, string path);
    }
    
    public class MultiFileAsynchronousDownloadClient : IAsynchronousDownloadClient
    {
        private readonly ILongWaitBroker _longWaitBroker;
        private readonly WebClient _webClient;
        private int FilesDownloaded { get; set; }

        // indicate aspects of the most recent download
        private bool DownloadComplete { get; set; }
        private bool DownloadSucceeded { get; set; }

        /// <summary>
        /// The asynchronous download client links a webClient to a LongWaitBroker. It supports
        /// multiple asynchronous downloads, and updates the associated broker's progress value.
        /// </summary>
        /// <param name="waitBroker">The associated LongWaitBroker</param>
        /// <param name="files">The numbers of file this instance is expected to download. This
        /// is used to accurately update the broker's progress value</param>
        public MultiFileAsynchronousDownloadClient(ILongWaitBroker waitBroker, int files)
        {
            _longWaitBroker = waitBroker;
            _webClient = new WebClient();
            FilesDownloaded = 0;

            _webClient.DownloadProgressChanged += (sender, args) =>
                {
                    _longWaitBroker.ProgressValue = (int) Math.Min(100, ((((double) FilesDownloaded)/files)*100)
                                                                        + ((1.0/files)*args.ProgressPercentage));
                };

            _webClient.DownloadFileCompleted += (sender, args) =>
                {
                    FilesDownloaded++;
                    DownloadSucceeded = (args.Error == null);
                    DownloadComplete = true;
                };
        }

        /// <summary>
        /// Downloads a file asynchronously
        /// </summary>
        /// <param name="address">The Uri of the file to download</param>
        /// <param name="path">The path to download the file to, including the name of 
        /// the file, e.g "C:\Users\Trevor\Downloads\example.txt"</param>
        /// <exception cref="ToolExecutionException">Thrown if the user cancels the download 
        /// using the instances' LongWaitBroker</exception>
        /// <returns>True if the downlaod was successful, otherwise false</returns>
        public bool DownloadFileAsync(Uri address, string path)
        {
            // reset download status
            DownloadComplete = false;
            DownloadSucceeded = false;

            Match file = Regex.Match(address.AbsolutePath, @"[^/]*$"); // Not L10N
            _longWaitBroker.Message = string.Format(Resources.MultiFileAsynchronousDownloadClient_DownloadFileAsync_Downloading__0_, file);
            _webClient.DownloadFileAsync(address, path);

            // while downloading, check to see if the user has canceled the operation
            while (!DownloadComplete)
            {
                if (_longWaitBroker.IsCanceled)
                {
                    _webClient.CancelAsync();
                    throw new ToolExecutionException(
                        Resources.MultiFileAsynchronousDownloadClient_DownloadFileAsyncWithBroker_Download_canceled_);
                }
            }
            return DownloadSucceeded;
        }
 
        #region IDisposable Members

        public void Dispose()
        {
            _webClient.Dispose();
        }

        #endregion
    }

    // The test Asynchronous Download Client allows us to simulate downloading files from the internet
    public class TestAsynchronousDownloadClient : IAsynchronousDownloadClient
    {
        public bool DownloadSuccess { get; set; }
        public bool CancelDownload { get; set; }

        public bool DownloadFileAsync(Uri address, string fileName)
        {
            if (CancelDownload)
                throw new ToolExecutionException(Resources.MultiFileAsynchronousDownloadClient_DownloadFileAsyncWithBroker_Download_canceled_);
            return DownloadSuccess;
        }

        public void Dispose()
        {
        }

    }

    public interface ISkylineProcessRunnerWrapper
    {
        /// <summary>
        /// Wrapper interface for the NamedPipeProcessRunner class
        /// </summary>
        int RunProcess(string arguments, bool runAsAdministrator, TextWriter writer);
    }

    public class SkylineProcessRunnerWrapper : ISkylineProcessRunnerWrapper
    {
        public int RunProcess(string arguments, bool runAsAdministrator, TextWriter writer)
        {
            return SkylineProcessRunner.RunProcess(arguments, runAsAdministrator, writer);
        }
    }

    // The test Named PipeProcess Runner allows us to simulate running NamedPipeProcessRunner.exe
    // by specifying its return code and whether the pipe connected or not
    public class TestSkylineProcessRunner : ISkylineProcessRunnerWrapper
    {
        public bool ConnectSuccess { get; set; }
        public bool UserOkRunAsAdministrator { get { return _userOkRunAsAdministrator; } set { _userOkRunAsAdministrator = value; } }
        private bool _userOkRunAsAdministrator = true; 
        public int ExitCode { get; set; }
        public string stringToWriteToWriter { get; set; }
        
        public int RunProcess(string arguments, bool runAsAdministrator, TextWriter writer)
        {
            if (!UserOkRunAsAdministrator)
            {
                throw new System.ComponentModel.Win32Exception(Resources.TestSkylineProcessRunner_RunProcess_The_operation_was_canceled_by_the_user_);
            }
            if (!ConnectSuccess)
                throw new IOException(Resources.TestNamedPipeProcessRunner_RunProcess_Error_running_process);
            if (!string.IsNullOrEmpty(stringToWriteToWriter))
                writer.WriteLine(stringToWriteToWriter);
            return ExitCode;
        }
    }

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

    // The test Process Runner allows us to simulate the (a)synchronous execution of a Process by specifying
    // its return code
    public class TestRunProcess : IRunProcess
    {
        public int ExitCode { get; set; }

        public int RunProcess(Process process)
        {
            return ExitCode;
        }
    }

    public class AsynchronousRunProcess : IRunProcess
    {   
        public int RunProcess(Process process)
        {
            bool finished = false;
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => finished = true;
            process.Start();
            while (!finished)
            {
                // continue
            }
            return process.ExitCode;
        }
    }
}
