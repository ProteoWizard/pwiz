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
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Text.RegularExpressions;
using IDPicker.Forms;
using pwiz.Common.Collections;
using System.Security.Policy;


namespace IDPicker
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        public static bool IsHeadless { get; private set; }
        public static IDPickerForm MainWindow { get; private set; }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main (string[] args)
        {
            // redirect console output to parent process;
            // must be before any calls to Console.WriteLine()
            AttachConsole(ATTACH_PARENT_PROCESS);

            if (!args.Contains("--test-ui-layout"))
            {
                // Add the event handler for handling UI thread exceptions to the event.
                Application.ThreadException += UIThread_UnhandledException;

                // Set the unhandled exception mode to force all Windows Forms errors to go through our handler.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

                // Add the event handler for handling non-UI thread exceptions to the event. 
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            }

            var singleInstanceHandler = new SingleInstanceHandler(Application.ExecutablePath) { Timeout = 200 };
            singleInstanceHandler.Launching += (sender, e) =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var singleInstanceArgs = e.Args.ToList();
                IsHeadless = singleInstanceArgs.Contains("--headless");
                if (IsHeadless)
                    singleInstanceArgs.RemoveAll(o => o == "--headless");

                // initialize webClient asynchronously
                initializeWebClient();

                checkin();

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
        public static void HandleUserError (Exception e)
        {
            if (IsHeadless)
            {
                Console.Error.WriteLine("\r\nProbable user error: {0}\r\n\r\nIf you suspect it's not a user error, please send the error to:\r\nbumbershoot-support@googlegroups.com", e.Message);
                //System.Diagnostics.Process.GetCurrentProcess().Kill();
                Environment.Exit(0);
            }

            if (MainWindow == null)
            {
                MessageBox.Show(e.ToString(), "Error");
                return;
            }

            if (MainWindow.InvokeRequired)
            {
                MainWindow.Invoke(new MethodInvoker(() => HandleUserError(e)));
                return;
            }

            using (var userErrorForm = new UserErrorForm(e.Message))
            {
                userErrorForm.StartPosition = FormStartPosition.CenterParent;
                userErrorForm.ShowDialog(MainWindow);
            }
        }

        public static void HandleException (Exception e)
        {
            if (MainWindow == null)
            {
                if (IsHeadless && !System.Diagnostics.Debugger.IsAttached)
                {
                    Console.Error.WriteLine("\r\nError: {0}\r\n\r\nDetails:\r\n{1}", e.Message, e.ToString());
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }
                else
                    MessageBox.Show(e.ToString(), "Error");
                return;
            }

            if (MainWindow.InvokeRequired)
            {
                MainWindow.Invoke(new MethodInvoker(() => HandleException(e)));
                return;
            }

            // for certain exception types, the InnerException is a better representative of the real error
            if ((e is NHibernate.ADOException || e.GetType() == typeof(Exception)) &&
                e.InnerException != null &&
                e.InnerException.StackTrace.Contains("IDPicker"))
                e = e.InnerException;

            using (var reportForm = new ReportErrorDlg(e, ReportErrorDlg.ReportChoice.choice))
            {
                reportForm.StartPosition = FormStartPosition.CenterParent;

                if (IsHeadless && !System.Diagnostics.Debugger.IsAttached)
                {
                    Console.Error.WriteLine("\r\nError: {0}\r\n\r\nDetails:\r\n{1}", reportForm.ExceptionType, reportForm.MessageBody);
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }

                if (MainWindow.IsDisposed)
                {
                    reportForm.ShowDialog();
                    //if (reportForm.ShowDialog() == DialogResult.OK)
                        //SendErrorReport(reportForm.MessageBody, reportForm.ExceptionType, reportForm.Email, reportForm.Username);
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }

                reportForm.ShowDialog(MainWindow);
                //if (reportForm.ShowDialog(MainWindow) == DialogResult.OK)
                    //SendErrorReport(reportForm.MessageBody, reportForm.ExceptionType, reportForm.Email, reportForm.Username);
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
        private static CookieAwareWebClient webClient = new CookieAwareWebClient();
        public static CookieAwareWebClient WebClient { get { return webClient; } }

        //private const string errorReportAddress = "http://forge.fenchurch.mc.vanderbilt.edu/tracker/index.php?func=add&group_id=10&atid=149";
        
        private static void initializeWebClient ()
        {
            /*new Thread(() =>
            {
                try
                {
                    lock (webClient)
                    {
                        var tpw = new char[] { 'T', 'r', '4', '<', 'k', '3', 'r' };

                        string html = webClient.DownloadString(loginAddress);
                        Match m = Regex.Match(html, "name=\"form_key\" value=\"(?<key>\\S+)\"");
                        if (!m.Groups["key"].Success)
                        {
                            MessageBox.Show("Unable to find form_key for login page.", "Error logging in to IDPicker exception tracker",
                                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        NameValueCollection form = new NameValueCollection
                                               {
                                                   {"form_key", m.Groups["key"].Value},
                                                   {"form_loginname", "idpicker"},
                                                   {"form_pw", String.Join("", tpw)},
                                                   {"login", "1"},
                                                   {"return_to", "1"},
                                               };

                        webClient.UploadValues(loginAddress, form);
                    }
                }
                catch {}
            }).Start();*/
        }

        private static void checkin ()
        {
            if (!Properties.GUI.Settings.Default.AutomaticCheckForUpdates)
                return;

            // create a unique user id if we haven't already done so
            if (Properties.Settings.Default.UUID == Guid.Empty)
            {
                Properties.Settings.Default.UUID = Guid.NewGuid();
                Properties.Settings.Default.Save();
            }

            string analyticsToken = "UA-30609227-2";
            string clientId = Properties.Settings.Default.UUID.ToString();
            string checkinAddressFormat = "http://www.google-analytics.com/collect?v=1&tid={0}&cid={1}&t=pageview&dh=idpicker.org&dp=/checkin/IDPicker%20{2}";
            webClient.DownloadStringAsync(new Uri(String.Format(checkinAddressFormat, analyticsToken, clientId, Util.Version)));
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

            new Thread(() => { try { CheckForUpdates(); } catch {} }).Start();
        }

        /// <summary>
        /// Filters out log lines that do not start with '-'
        /// </summary>
        private static string filterRevisionLog (string log)
        {
            var lines = log.Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            return String.Join("\r\n", lines.Where(o=> o.TrimStart().StartsWith("-")).Select(o=> o.TrimEnd('\r')).ToArray());
        }

        public static bool CheckForUpdates ()
        {
            Properties.GUI.Settings.Default.LastCheckForUpdates = DateTime.UtcNow;
            Properties.GUI.Settings.Default.Save();

            string teamcityURL = "http://teamcity.labkey.org";
            string buildType = Environment.Is64BitProcess ? "Bumbershoot_Windows_X86_64" : "ProteoWizard_Bumbershoot_Windows_X86";
            string buildsURL = String.Format("{0}/httpAuth/app/rest/buildTypes/id:{1}/builds?status=SUCCESS&count=1&guest=1", teamcityURL, buildType);
            string latestArtifactURL;
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

                latestArtifactURL = String.Format("{0}/repository/download/{1}/{2}:id", teamcityURL, buildType, buildId);
                latestVersion = new Version(webClient.DownloadString(latestArtifactURL + "/IDPICKER_VERSION?guest=1"));
            }

            Version currentVersion = new Version(Util.Version);

            if (currentVersion < latestVersion)
            {
                /*System.Collections.ObjectModel.Collection<SvnLogEventArgs> logItems = null;

                using (var client = new SvnClient())
                {
                    client.Authentication.Clear();
                    client.Authentication.DefaultCredentials = new NetworkCredential("anonsvn", "anonsvn");

                    try
                    {
                        client.GetLog(new Uri("svn://svn.code.sf.net/p/proteowizard/code/trunk/pwiz/pwiz_tools/Bumbershoot/idpicker/"),
                                      new SvnLogArgs(new SvnRevisionRange(currentVersion.Build, latestVersion.Build)),
                                      out logItems);
                    }
                    catch(Exception e)
                    {
                        HandleException(e);
                    }
                }

                IEnumerable<SvnLogEventArgs> filteredLogItems = logItems;

                string changeLog;
                if (logItems.IsNullOrEmpty())
                    changeLog = "<unable to get change log>";
                else
                {
                    // return if no important revisions have happened
                    filteredLogItems = logItems.Where(o => !filterRevisionLog(o.LogMessage).Trim().IsNullOrEmpty());
                    if (!filteredLogItems.IsNullOrEmpty())
                    {
                        var logEntries = filteredLogItems.Select(o => String.Format("Revision {0}:\r\n{1}",
                                                                                    o.Revision,
                                                                                    filterRevisionLog(o.LogMessage)));
                        changeLog = String.Join("\r\n\r\n", logEntries.ToArray());
                    }
                    else
                        changeLog = "Technical changes and bug fixes.";
                }*/
                string changeLog = "<unable to get change log>";

                MainWindow.Invoke(new MethodInvoker(() =>
                {
                    var form = new NewVersionForm(Application.ProductName,
                                                  currentVersion.ToString(),
                                                  latestVersion.ToString(),
                                                  changeLog)
                                                  {
                                                      Owner = MainWindow,
                                                      StartPosition = FormStartPosition.CenterParent
                                                  };

                    if (form.ShowDialog() == DialogResult.Yes)
                    {
                        string archSuffix = Environment.Is64BitProcess ? "x86_64" : "x86";
                        string guestAccess = Application.ExecutablePath.Contains("build-nt-x86") ? "" : "?guest=1"; // don't log me out of TC session
                        string installerURL = String.Format("{0}/IDPicker-{1}-{2}.msi{3}", latestArtifactURL, latestVersion, archSuffix, guestAccess);
                        System.Diagnostics.Process.Start(installerURL);
                    }
                }));
                return true;
            }
            return false;
        }

        private static void SendErrorReport (string messageBody, string exceptionType, string email, string username)
        {
            /*lock (webClient)
            {
                string html = webClient.DownloadString(errorReportAddress);
                Match m = Regex.Match(html, "name=\"form_key\" value=\"(?<key>\\S+)\"");
                if (!m.Groups["key"].Success)
                {
                    MessageBox.Show("Unable to find form_key for exception tracker.", "Error submitting report",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                exceptionType = exceptionType.Replace("System.", "");
                string errorMessage = Regex.Match(messageBody, "Error message: (.+?)\\r").Groups[1].Value;
                errorMessage = errorMessage.Length > 60 ? errorMessage.Substring(0, 60) + "..." : errorMessage;
                username = String.IsNullOrEmpty(username) ? "unknown" : username;

                NameValueCollection form = new NameValueCollection
                                               {
                                                   {"form_key", m.Groups["key"].Value},
                                                   {"func", "postadd"},
                                                   {"summary", exceptionType + " (User: " + username + "; Message: " + errorMessage + ")"},
                                                   {"details", messageBody},
                                                   {"user_email", email},
                                               };

                webClient.UploadValues(errorReportAddress, form);
            }*/
        }
        #endregion
    }
}
