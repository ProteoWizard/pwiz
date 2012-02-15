/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Deployment.Application;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline
{
    /// <summary>
    /// Anything in this class is really application global, and is better stored
    /// on some more local object like the document settings, or the
    /// <see cref="Settings"/> class.  Even the only existing <see cref="Name"/>
    /// property is just a shortcut to the <see cref="Settings"/> application
    /// scope property for easier use in <see cref="MessageBox"/>.
    /// 
    /// Should anything else need to be added, it should be clearly described
    /// why it is necessary.
    /// </summary>
    public static class Program
    {
        private const int LICENSE_VERSION_CURRENT = 4;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments != null &&
                AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData != null &&
                AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData.Length > 0 &&
                AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData[0] == "CMD")
            {
                CommandLineRunner clr = new CommandLineRunner();
                clr.Start();

                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += ThreadExceptionEventHandler;

                // Make sure the user has agreed to the current license version
                // or one more recent.
                int licenseVersion = Settings.Default.LicenseVersionAccepted;
                if (licenseVersion < LICENSE_VERSION_CURRENT)
                {
                    // If the user has never used the application before, then
                    // they must have agreed to the current license agreement during
                    // installation.  Otherwise, make sure they agree to the new
                    // license agreement.
                    if (licenseVersion != 0 || !Settings.Default.MainWindowSize.IsEmpty)
                    {
                        var dlg = new UpgradeDlg(licenseVersion);
                        if (dlg.ShowDialog() == DialogResult.Cancel)
                            return;
                    }
                }
                // Make sure the user never sees this again for this license version
                Settings.Default.LicenseVersionAccepted = LICENSE_VERSION_CURRENT;
                Settings.Default.Save();

                MainWindow = new SkylineWindow();
                Application.Run(MainWindow);
            }
            catch (Exception x)
            {
                // Send unhandled exceptions to the console.
                Console.WriteLine(x.Message);
                Console.Write(x.StackTrace);
            }
        }

        public static void ThreadExceptionEventHandler(Object sender, ThreadExceptionEventArgs e)
        {
            List<string> stackTraceList = Settings.Default.StackTraceList;
            var reportChoice = ReportErrorDlg.ReportChoice.choice;
            // If it was not network deployed, then either it is just a developer build,
            // or it was deployed in an environment where posting back to the web may not
            // be allowed.  In either case, never post directly to the web site.
            if (!ApplicationDeployment.IsNetworkDeployed)
                reportChoice = ReportErrorDlg.ReportChoice.never;
            else
            {
                string version = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
                string[] versionParts = version.Split('.');
                // Version #.#.0.# is used for release builds
                // If it is not a release build, then always post error reports
                if (versionParts.Length > 2 && !Equals(versionParts[2], "0"))
                    reportChoice = ReportErrorDlg.ReportChoice.always;

                if (!Equals(Settings.Default.StackTraceListVersion, version))
                {
                    Settings.Default.StackTraceListVersion = version;
                    stackTraceList.Clear();
                }
            }

            using (var reportForm = new ReportErrorDlg(e.Exception, reportChoice))
            {
                if (reportForm.ShowDialog(MainWindow) == DialogResult.OK)
                {
                    string stackText = reportForm.StackTraceText;
                    if (!stackTraceList.Contains(stackText))
                    {
                        stackTraceList.Add(stackText);
                        SendErrorReport(reportForm.MessageBody, reportForm.ExceptionType);
                    }
                }
            }         
        }

        private static void SendErrorReport(string messageBody, string exceptionType)
        {
            WebClient webClient = new WebClient();

            const string address = "https://brendanx-uw1.gs.washington.edu/labkey/announcements/home/issues/exceptions/insert.view";

            NameValueCollection form = new NameValueCollection
                                           {
                                               { "title", "Unhandled " + exceptionType},
                                               { "body", messageBody },
                                               { "fromDiscussion", "false"},
                                               { "allowMultipleDiscussions", "false"},
                                               { "rendererType", "TEXT_WITH_LINKS"}
                                           };

            webClient.UploadValues(address, form);
        }


        public static SkylineWindow MainWindow { get; private set; }
        public static SrmDocument ActiveDocument { get { return MainWindow.Document; } }
        public static SrmDocument ActiveDocumentUI { get { return MainWindow.DocumentUI; } }

        /// <summary>
        /// Shortcut to the application name stored in <see cref="Settings"/>
        /// </summary>
        public static string Name
        {
            get { return Settings.Default.ProgramName; }
        }
    }

    public class CommandLineRunner
    {
        private static readonly object SERVER_CONNECTION_LOCK = new object();
        private bool _connected;

        public static void RunCommand(string[] inputArgs, TextWriter consoleOut)
        {
            using (CommandLine cmd = new CommandLine(consoleOut))
            {
                cmd.Run(inputArgs);
            }
        }


        /// <summary>
        /// This function will try for 5 seconds to open a named pipe ("SkylineInputPipe").
        /// If this operation is not successful, the function will exit. Otherwise,
        /// the function will print each line received from the pipe
        /// out to the console and then wait for a newline from the user.
        /// </summary>
        public void Start()
        {
            List<string> args = new List<string>();
            using (NamedPipeClientStream pipeStream = new NamedPipeClientStream("SkylineInputPipe"))
            {
                // The connect function will wait 5s for the pipe to become available
                try
                {
                    pipeStream.Connect(5 * 1000);
                }
                catch (Exception)
                {
                    // Nothing to output, because no connection to command-line process.
                    return;
                }

                using (StreamReader sr = new StreamReader(pipeStream))
                {
                    string line;
                    //While (!done reading)
                    while ((line = sr.ReadLine()) != null)
                    {
                        args.Add(line);
                    }
                }
            }

            using (var serverStream = new NamedPipeServerStream("SkylineOutputPipe"))
            {
                if (!WaitForConnection(serverStream))
                {
                    return;
                }
                using (StreamWriter sw = new StreamWriter(serverStream))
                {
                    RunCommand(args.ToArray(), sw);
                }
            }
        }

        private bool WaitForConnection(NamedPipeServerStream serverStream)
        {
            Thread connector = new Thread(() =>
            {
                serverStream.WaitForConnection();

                lock (SERVER_CONNECTION_LOCK)
                {
                    _connected = true;
                    Monitor.Pulse(SERVER_CONNECTION_LOCK);
                }
            });

            connector.Start();

            bool connected;
            lock (SERVER_CONNECTION_LOCK)
            {
                Monitor.Wait(SERVER_CONNECTION_LOCK, 5 * 1000);
                connected = _connected;
            }

            if (!connected)
            {
                // Clear the waiting thread.
                try
                {
                    using (var pipeFake = new NamedPipeClientStream("SkylineOutputPipe"))
                    {
                        pipeFake.Connect(10);
                    }
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
