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
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += Application_ThreadExceptionEventHandler;

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

        public static void Application_ThreadExceptionEventHandler(Object sender,ThreadExceptionEventArgs e )
        {
            MessageBox.Show(MainWindow, "Unhandled exception: " + e.Exception, Name);
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
