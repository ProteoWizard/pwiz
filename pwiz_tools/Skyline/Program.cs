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
using System.Collections.Specialized;
using System.IO;
using System.IO.Pipes;
using System.Linq;
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
        private const int LICENSE_VERSION_CURRENT = 3;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main()
        {

            // First, check for command-line parameters. If there are any, let
            // CommandLine deal with them and write output over a named pipe
            // then exit the program.
            if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments != null &&
                AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData != null &&
                AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData.Length > 0)
            {
                String[] inputArgs = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData;
                inputArgs = inputArgs[0].Split(new[] {','});

                if (inputArgs.Any(arg => arg.StartsWith("--")))
                {
                    NamedPipeServerStream pipeStream = new NamedPipeServerStream("SkylinePipe");
                    pipeStream.WaitForConnection();

                    RunCommand(inputArgs, new StreamWriter(pipeStream));

                    return;
                }
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
// Update Skyline-daily users to the new license automatically.
//                    if (licenseVersion != 0 || !Settings.Default.MainWindowSize.IsEmpty)
//                    {
//                        var dlg = new UpgradeDlg(licenseVersion);
//                        if (dlg.ShowDialog() == DialogResult.Cancel)
//                            return;
//                    }
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

        public static void RunCommand(string[] inputArgs, TextWriter consoleOut)
        {
            using (CommandLine cmd = new CommandLine(consoleOut))
            {
                cmd.Run(inputArgs);
            }
        }

        public static void ThreadExceptionEventHandler(Object sender, ThreadExceptionEventArgs e)
        {
            var reportForm = new ReportErrorDlg(e.Exception);
            if (reportForm.ShowDialog(MainWindow) == DialogResult.OK)
                SendErrorReport(reportForm.MessageBody);
        }

        private static void SendErrorReport(string messageBody)
        {
            WebClient webClient = new WebClient();

            const string address = "https://brendanx-uw1.gs.washington.edu/labkey/announcements/home/issues/exceptions/insert.view";

            NameValueCollection form = new NameValueCollection
                                           {
                                               { "title", "Unhandled Exception" },
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
}
