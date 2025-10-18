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
using System.Diagnostics;
using System.IO;
using System.Linq;
using Ionic.Zip;
using pwiz.Common.SystemUtil;
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
            PdfFileLocation = pdfFileLocation + @"&show=html&ver=" + Install.TutorialVersionFolder;
            SkyFileLocationInZip = skyFileLocationInZip;
        }

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

        private string GetTempPath()
        {
            return Path.Combine(TempPath, (Path.GetFileName(ExtractPath) ?? string.Empty) + StartPage.EXT_TUTORIAL_FILES);
        }

        public void LongWaitDlgAction(SkylineWindow skylineWindow)
        {
            skylineWindow.ResetDefaultSettings();
            try
            {
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.Text = StartupResources.ActionTutorial_LongWaitDlgAction_Downloading_Tutorial_Zip_File;
                    var status = longWaitDlg.PerformWork(skylineWindow, 1000, DownloadTutorials);
                    if (status.IsCanceled)
                    {
                        return;
                    }
                }

                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.Text = StartupResources.ActionTutorial_LongWaitDlgAction_Extracting_Tutorial_Zip_File_in_the_same_directory_;
                    longWaitDlg.PerformWork(skylineWindow, 1000, ExtractTutorial);
                }
            }
            catch (Exception exception)
            {
                ExceptionUtil.DisplayOrReportException(Program.MainWindow, exception);
            }
            finally
            {
                // Attempt to get rid of the temp ZIP file
                FileEx.SafeDelete(GetTempPath(), true);
            }
        }

        // Download
        public void DownloadTutorials(IProgressMonitor waitBroker)
        {
            var status = new ProgressStatus(string.Format(
                StartupResources.ActionTutorial_LongWaitDlgAction_Downloading_to___0__1_Tutorial_will_open_in_browser_when_download_is_complete_,
                GetTempPath(), Environment.NewLine));

            using var httpClient = new HttpClientWithProgress(waitBroker, status);
            httpClient.DownloadFile(TutorialZipFileLocation, GetTempPath());
        }

        // Extract
        public void ExtractTutorial(IProgressMonitor waitBroker)
        {
            IProgressStatus status = new ProgressStatus(StartupResources.ActionTutorial_LongWaitDlgAction_Extracting_Tutorial_Zip_File_in_the_same_directory_);

            using (ZipFile zip = ZipFile.Read(GetTempPath()))
            {
                ExpectedSize = zip.Entries.Sum(entry => entry.UncompressedSize);

                zip.ExtractProgress += (s,e) => TutorialFile_ExtractProgress(s,e, waitBroker, ref status);
                var extractDir = ExtractPath;
                var skyFileToOpen = Path.Combine(extractDir ?? string.Empty, SkyFileLocationInZip);
                foreach (var entry in zip.Entries.ToList())
                {
                    if (entry.FileName.IndexOf('/') >= 0)
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
                        if (waitBroker.IsCanceled)
                            break;
                        
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
                        if (!string.IsNullOrEmpty(ExtractPath))
                        {
                            // Make it convenient for user to locate tutorial files if we haven't already opened anything
                            Directory.SetCurrentDirectory(ExtractPath);
                            Settings.Default.LibraryDirectory =
                                Settings.Default.ActiveDirectory =
                                    Settings.Default.ExportDirectory =
                                        Settings.Default.FastaDirectory =
                                            Settings.Default.LibraryResultsDirectory =
                                                Settings.Default.ProteomeDbDirectory =
                                                    ExtractPath;
                        }

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
            }
        }

        private void TutorialFile_ExtractProgress(object sender, ExtractProgressEventArgs e, IProgressMonitor waitBroker, ref IProgressStatus status)
        {
            if (waitBroker != null)
            {
                if (waitBroker.IsCanceled)
                {
                    e.Cancel = true;
                    return;
                }

                int progressValue = (int)Math.Round((ExtractedSize + e.BytesTransferred) * 100.0 / ExpectedSize);

                if (progressValue != status.PercentComplete)
                {
                    waitBroker.UpdateProgress(status = status.ChangePercentComplete(progressValue).ChangeMessage(
                        string.Format(Resources.SrmDocumentSharing_SrmDocumentSharing_ExtractProgress_Extracting__0__,
                            e.CurrentEntry.FileName)));
                }
            }
        }
    }
}