//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Text.RegularExpressions;
using IDPicker.Forms;

namespace IDPicker
{
    static class Program
    {
        public static bool IsHeadless { get; private set; }
        public static IDPickerForm MainWindow { get; private set; }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main (string[] args)
        {
            // Add the event handler for handling UI thread exceptions to the event.
            Application.ThreadException += new ThreadExceptionEventHandler(UIThread_UnhandledException);

            // Set the unhandled exception mode to force all Windows Forms errors to go through
            // our handler.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // Add the event handler for handling non-UI thread exceptions to the event. 
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            var singleInstanceHandler = new SingleInstanceHandler(Application.ExecutablePath) { Timeout = 200 };
            singleInstanceHandler.Launching += (sender, e) =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var singleInstanceArgs = e.Args.ToList();
                IsHeadless = singleInstanceArgs.Contains("--headless");
                if (IsHeadless)
                    singleInstanceArgs.Remove("--headless");

                // initialize webClient asynchronously
                initializeWebClient();

                automaticCheckForUpdates();

                //HibernatingRhinos.Profiler.Appender.NHibernate.NHibernateProfiler.Initialize();

                MainWindow = new IDPickerForm(singleInstanceArgs);
                Application.Run(MainWindow);
            };

            try
            {
                singleInstanceHandler.Connect(args);
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }

        #region Exception handling
        public static void HandleException (Exception e)
        {
            if (MainWindow == null)
            {
                MessageBox.Show(e.ToString(), "Error");
                return;
            }

            if (MainWindow.InvokeRequired)
            {
                MainWindow.Invoke(new MethodInvoker(() => HandleException(e)));
                return;
            }

            using (var reportForm = new ReportErrorDlg(e, ReportErrorDlg.ReportChoice.choice))
            {
                if (IsHeadless)
                {
                    Console.Error.WriteLine("Error: {0}\r\n\r\nDetails:\r\n{1}", reportForm.ExceptionType, reportForm.MessageBody);
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }

                if (MainWindow.IsDisposed)
                {
                    if (reportForm.ShowDialog() == DialogResult.OK)
                        SendErrorReport(reportForm.MessageBody, reportForm.ExceptionType, reportForm.Email);
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }

                if (reportForm.ShowDialog(MainWindow) == DialogResult.OK)
                    SendErrorReport(reportForm.MessageBody, reportForm.ExceptionType, reportForm.Email);
                if (reportForm.ForceClose)
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
        }

        private static void UIThread_UnhandledException (object sender, ThreadExceptionEventArgs e)
        {
            HandleException(e.Exception);
        }

        private static void CurrentDomain_UnhandledException (object sender, UnhandledExceptionEventArgs e)
        {
            HandleException(e.ExceptionObject as Exception);
        }
        #endregion

        #region Update checking and error reporting
        private static WebClient webClient = new WebClient();
        private static void initializeWebClient ()
        {
            new Thread(() => { try { lock (webClient) webClient.DownloadString("http://www.google.com"); } catch {/* TODO: log warning */} }).Start();
        }

        private static void automaticCheckForUpdates ()
        {
            if (!Properties.GUI.Settings.Default.AutomaticCheckForUpdates)
                return;

            var timeSinceLastCheckForUpdates = DateTime.UtcNow - Properties.GUI.Settings.Default.LastCheckForUpdates;
            if (timeSinceLastCheckForUpdates.TotalDays < 1)
                return;

            // ignore development builds
            if (Application.ExecutablePath.Contains("build-nt-x86"))
                return;

            new Thread(() => { try { CheckForUpdates(); } catch {/* TODO: log warning */} }).Start();
        }

        public static bool CheckForUpdates ()
        {
            Properties.GUI.Settings.Default.LastCheckForUpdates = DateTime.UtcNow;
            Properties.GUI.Settings.Default.Save();

            string teamcityURL = "http://teamcity.fenchurch.mc.vanderbilt.edu";
            string buildsURL = teamcityURL + "/httpAuth/app/rest/buildTypes/id:bt31/builds?status=SUCCESS&count=1&guest=1";
            string latestArtifactURL;
            string versionArtifactFormatURL = teamcityURL + "/repository/download/bt31/{0}:id/VERSION?guest=1";

            Version latestVersion;

            lock (webClient)
            {
                string xml = webClient.DownloadString(buildsURL);
                int startIndex = xml.IndexOf("id=");
                if (startIndex < 0) throw new InvalidDataException("build id not found in:\r\n" + xml);
                int endIndex = xml.IndexOfAny("\"'".ToCharArray(), startIndex + 4);
                if (endIndex < 0) throw new InvalidDataException("not well formed xml:\r\n" + xml);
                startIndex += 4; // skip the attribute name, equals, and opening quote
                string buildId = xml.Substring(startIndex, endIndex - startIndex);

                latestArtifactURL = String.Format("{0}/repository/download/bt31/{1}:id", teamcityURL, buildId);
                latestVersion = new Version(webClient.DownloadString(latestArtifactURL + "/VERSION?guest=1"));
            }

            Version currentVersion = new Version(Util.Version);

            if (currentVersion < latestVersion)
            {
                string updateMessage = String.Format("There is a newer version of {0} available.\r\n" +
                                                     "You are using version {1}.\r\n" +
                                                     "The latest version is {2}.\r\n" +
                                                     "\r\n" +
                                                     "Download it now?",
                                                     Application.ProductName,
                                                     currentVersion,
                                                     latestVersion);

                var result = MessageBox.Show(updateMessage, "Newer Version Available",
                                             MessageBoxButtons.YesNo,
                                             MessageBoxIcon.Information,
                                             MessageBoxDefaultButton.Button2);

                if (result == DialogResult.Yes)
                {
                    string installerURL = String.Format("{0}/IDPicker-{1}.msi?guest=1", latestArtifactURL, latestVersion);
                    System.Diagnostics.Process.Start(installerURL);
                }
                return true;
            }
            return false;
        }

        private static void SendErrorReport (string messageBody, string exceptionType, string email)
        {
            const string address = "http://forge.fenchurch.mc.vanderbilt.edu/tracker/index.php?func=add&group_id=10&atid=149";

            lock (webClient)
            {
                string html = webClient.DownloadString(address);
                Match m = Regex.Match(html, "name=\"form_key\" value=\"(?<key>\\S+)\"");
                if (!m.Groups["key"].Success)
                {
                    MessageBox.Show("Unable to find form_key for exception tracker.", "Error submitting report",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                NameValueCollection form = new NameValueCollection
                                               {
                                                   {"form_key", m.Groups["key"].Value},
                                                   {"func", "postadd"},
                                                   {"summary", "Unhandled " + exceptionType},
                                                   {"details", messageBody},
                                                   {"user_email", email},
                                               };

                webClient.UploadValues(address, form);
            }
        }
        #endregion
    }
}
