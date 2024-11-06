/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using Ionic.Zip;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Startup
{
    public class ActionTutorial
    {
        public string TutorialZipFileLocation { get; set; }
        public string PdfFileLocation { get; set; }
        public string SkyFileLocationInZip { get; set; }
        public string ExtractPath { get; set; }
        private long ExpectedSize { get; set; }
        private long ExtractedSize { get; set; }
        private readonly string TempPath = Path.GetTempPath();

        public ActionTutorial(string extractPath, string skyFileLocation, string pdfFileLocation, string skyFileLocationInZip)
        {
            ExtractPath = extractPath;
            TutorialZipFileLocation = skyFileLocation;
            PdfFileLocation = pdfFileLocation + @"&show=html&ver=24-1";
            SkyFileLocationInZip = skyFileLocationInZip;
        }

        private ILongWaitBroker WaitBroker { get; set; }
        private double Progress { get; set; }
        public bool DoStartupAction(SkylineWindow skylineWindow)
        {
            if (skylineWindow.Visible)
            {
                LongWaitDlgAction(skylineWindow);
            }
            else
            {
                skylineWindow.Shown += (sender, eventArgs) => LongWaitDlgAction(skylineWindow);
            }
            return true;
        }

        private string getTempPath()
        {
            return Path.Combine(TempPath, Path.GetFileName(ExtractPath) ?? string.Empty);
        }

        private string getExtractPath()
        {
            return ExtractPath;
        }

        public void LongWaitDlgAction(SkylineWindow skylineWindow)
        {
            skylineWindow.ResetDefaultSettings();
            try
            {
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.Text = StartupResources.ActionTutorial_LongWaitDlgAction_Downloading_Tutorial_Zip_File;
                    longWaitDlg.Message = String.Format(
                        StartupResources
                            .ActionTutorial_LongWaitDlgAction_Downloading_to___0__1_Tutorial_will_open_in_browser_when_download_is_complete_,
                        getTempPath(), Environment.NewLine);
                    longWaitDlg.ProgressValue = 0;
                    longWaitDlg.PerformWork(skylineWindow, 1000, DownloadTutorials);
                    if (longWaitDlg.IsCanceled)
                    {
                        return;
                    }
                }
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.Text = StartupResources.ActionTutorial_LongWaitDlgAction_Extracting_Tutorial_Zip_File_in_the_same_directory_;
                    longWaitDlg.ProgressValue = 0;
                    longWaitDlg.PerformWork(skylineWindow, 1000, ExtractTutorial);
                }
            }
            catch (Exception exception)
            {
                MessageDlg.ShowWithException(Program.MainWindow, string.Format(StartupResources.ActionTutorial_DownloadTutorials_Error__0_, exception.Message), exception);
            }
        }

        // Download
        public void DownloadTutorials(ILongWaitBroker waitBroker)
        {
            WaitBroker = waitBroker;
            WaitBroker.ProgressValue = Convert.ToInt32(Progress * 100);
            WebClient client = new WebClient
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore),
            };
            client.DownloadProgressChanged += client_DownloadProgressChanged;
            client.DownloadFile(new Uri(TutorialZipFileLocation), getTempPath());
        }

        public void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            int percentage = (int) (e.BytesReceived*100/e.TotalBytesToReceive);
            Progress = percentage;
            WaitBroker.ProgressValue = percentage;
        }

        public void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Program.MainWindow.BeginInvoke(new Action(() =>
            {
                if (e.Error != null)
                {
                    MessageDlg.ShowWithException(Program.MainWindow,string.Format(StartupResources.ActionTutorial_DownloadTutorials_Error__0_,e.Error), e.Error);
                }
                else if(string.IsNullOrEmpty(SkyFileLocationInZip))
                {
                    MessageDlg.Show(Program.MainWindow,
                    string.Format(StartupResources.ActionTutorial_client_DownloadFileCompleted_File_saved_at___0_,getTempPath()));
                    Process.Start(PdfFileLocation); // Opens Tutorial PDF in users default browser.
                }
            }));
        }

        // Extract
        public void ExtractTutorial(ILongWaitBroker waitBroker)
        {
            using (ZipFile zip = ZipFile.Read(getTempPath()))
            {
                ExpectedSize = zip.Entries.Sum(entry => entry.UncompressedSize);

                zip.ExtractProgress += (s,e) => TutorialFile_ExtractProgress(s,e, waitBroker);
                var extractDir = getExtractPath();
                var skyFileToOpen = Path.Combine(extractDir ?? string.Empty, SkyFileLocationInZip);
                foreach (var entry in zip.Entries.ToList())
                {
                    entry.FileName = entry.FileName.Substring(entry.FileName.IndexOf('/')); // Gets rid of everything up to and including first '/'.
                    if (string.IsNullOrEmpty(entry.FileName))
                    {
                        continue;
                    }
                    if (waitBroker.IsCanceled)
                        break;
                    try
                    {
                        entry.Extract(extractDir);

                        ExtractedSize += entry.UncompressedSize;
                    }
                    catch (Exception)
                    {
                        if (!waitBroker.IsCanceled)
                            throw;
                    }
                }
                var hasSkylineFile = !string.IsNullOrEmpty(SkyFileLocationInZip) && !string.IsNullOrEmpty(ExtractPath);
                Program.MainWindow.BeginInvoke(new Action(() =>
                {
                    if (hasSkylineFile)
                    {
                        Program.MainWindow.OpenFile(skyFileToOpen);
                    }
                    else
                    {
                        Program.MainWindow.NewDocument(true);
                    }
                    if (string.IsNullOrEmpty(SkyFileLocationInZip))
                    {
                        MessageDlg.Show(Program.MainWindow,
                            string.Format(StartupResources.ActionTutorial_client_DownloadFileCompleted_File_saved_at___0_, extractDir));
                    }

                    try
                    {
                        Process.Start(PdfFileLocation); // Opens Tutorial PDF in users default browser.
                    }
                    catch (Exception e)
                    {
                        string message = string.Format(StartupResources
                                .ActionTutorial_ExtractTutorial_An_error_occurred_while_trying_to_display_the_document___0____,
                            PdfFileLocation);
                        MessageDlg.ShowWithException(Program.MainWindow, message, e);
                    }
                }));

                // Make it convenient for user to locate tutorial files if we haven't already opened anything
                if (!hasSkylineFile && !string.IsNullOrEmpty(ExtractPath))
                {
                    Directory.SetCurrentDirectory(ExtractPath);
                    Settings.Default.LibraryDirectory =
                        Settings.Default.ActiveDirectory =
                            Settings.Default.ExportDirectory =
                                Settings.Default.FastaDirectory =
                                    Settings.Default.LibraryResultsDirectory =
                                            Settings.Default.ProteomeDbDirectory =
                                                ExtractPath;
                }
            }
        }

        private void TutorialFile_ExtractProgress(object sender, ExtractProgressEventArgs e, ILongWaitBroker waitBroker)
        {
            if (waitBroker != null)
            {
                if (waitBroker.IsCanceled)
                {
                    e.Cancel = true;
                    return;
                }

                int progressValue = (int)Math.Round((ExtractedSize + e.BytesTransferred) * 100.0 / ExpectedSize);

                if (progressValue != WaitBroker.ProgressValue)
                {
                    waitBroker.ProgressValue = progressValue;
                    waitBroker.Message = (string.Format(Resources.SrmDocumentSharing_SrmDocumentSharing_ExtractProgress_Extracting__0__,
                                                              e.CurrentEntry.FileName));
                }
            }
        }
    }
}