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
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

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
        
        // Parameters for stress testing.
        public static bool StressTest { get; set; }                 // Set true when doing stress testing.
        public static bool SkylineOffscreen { get; set; }           // Set true to move Skyline windows offscreen.
        public static bool NoVendorReaders { get; set; }            // Set true to avoid calling vendor readers.
        public static bool NoSaveSettings { get; set; }             // Set true to use separate settings file.
        public static int UnitTestTimeoutMultiplier { get; set; }   // Set to positive multiplier for multi-process stress runs.
        private static bool _initialized;                           // Flag to do some initialization just once per process.
        private static string _name;                                // Program name.

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args = null)
        {
            // don't allow 64-bit Skyline to run in a 32-bit process
            if (Install.Is64Bit && !Environment.Is64BitProcess)
            {
                string installUrl = Install.Url;
                string installLabel = (installUrl == "") ? "" : string.Format("Install 32-bit {0}", Name);
                AlertLinkDlg.Show(null,
                    string.Format("You are attempting to run a 64-bit version of {0} on a 32-bit OS.  Please install the 32-bit version.", Name),
                    installLabel,
                    installUrl);
                return;
            }

            // For testing and debugging Skyline command-line interface
            if (args != null && args.Length > 0)
            {
                CommandLineRunner.RunCommand(args, Console.Out);

                return;
            }
            // The way Skyline command-line interface is run for an installation
            else if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments != null &&
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
                Init();

                // Make sure the user has agreed to the current license version
                // or one more recent.
                int licenseVersion = Settings.Default.LicenseVersionAccepted;
                if (licenseVersion < LICENSE_VERSION_CURRENT && !NoSaveSettings)
                {
                    // If the user has never used the application before, then
                    // they must have agreed to the current license agreement during
                    // installation.  Otherwise, make sure they agree to the new
                    // license agreement.
                    if (Install.Type == Install.InstallType.release &&
                            (licenseVersion != 0 || !Settings.Default.MainWindowSize.IsEmpty))
                    {
                        var dlg = new UpgradeDlg(licenseVersion);
                        if (dlg.ShowDialog() == DialogResult.Cancel)
                            return;
                    }

                    try
                    {
                        // Make sure the user never sees this again for this license version
                        Settings.Default.LicenseVersionAccepted = LICENSE_VERSION_CURRENT;
                        Settings.Default.Save();
                    }
// ReSharper disable EmptyGeneralCatchClause
                    catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
                    {
                        // Just try to update the license version next time.
                    }
                }

                MainWindow = new SkylineWindow();

                // Position window offscreen for stress testing.
                if (SkylineOffscreen)
                {
                    var offscreenPoint = new Point(0, 0);
                    foreach (var screen in Screen.AllScreens)
                    {
                        offscreenPoint.X = Math.Min(offscreenPoint.X, screen.Bounds.Right);
                        offscreenPoint.Y = Math.Min(offscreenPoint.Y, screen.Bounds.Bottom);
                    }
                    MainWindow.StartPosition = FormStartPosition.Manual;
                    MainWindow.Location = offscreenPoint - Screen.PrimaryScreen.Bounds.Size;    // position one screen away to top left
                }

                Application.Run(MainWindow);
            }
            catch (Exception x)
            {
                // Send unhandled exceptions to the console.
                Console.WriteLine(x.Message);
                Console.Write(x.StackTrace);
            }

            // Release main window memory during tests
            MainWindow = null;
        }

        public static void Init()
        {
            if (!_initialized)
            {
                _initialized = true;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (!StressTest)
                {
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.ThreadException += ThreadExceptionEventHandler;
                }
            }
        }

        public static void CloseSkyline()
        {
            if (MainWindow != null && !MainWindow.IsDisposed)
            {
                MainWindow.Invoke(new Action(MainWindow.Close));
            }
        }

        public static void ThreadExceptionEventHandler(Object sender, ThreadExceptionEventArgs e)
        {
            List<string> stackTraceList = Settings.Default.StackTraceList;

            using (var reportForm = new ReportErrorDlg(e.Exception, stackTraceList))
            {
                reportForm.ShowDialog(MainWindow);
            }         
        }

        public static SkylineWindow MainWindow { get; private set; }
        public static SrmDocument ActiveDocument { get { return MainWindow.Document; } }
        public static SrmDocument ActiveDocumentUI { get { return MainWindow.DocumentUI; } }

        /// <summary>
        /// Shortcut to the application name stored in <see cref="Settings"/>
        /// </summary>
        public static string Name
        {
            get
            {
                return _name ??
                       (_name =
                        Settings.Default.ProgramName + (Install.Type == Install.InstallType.daily ? "-daily" : ""));
            }
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
