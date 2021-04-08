/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Configuration;
using System.Deployment.Application;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using log4net.Config;
using SkylineBatch.Properties;
using SharedBatch;

namespace SkylineBatch
{
    public class Program
    {
        private static string _version;

        #region For tests
        public static MainForm MainWindow { get; private set; }     // Accessed by functional tests
        // Parameters for running tests
        public static bool FunctionalTest { get; set; }             // Set to true by AbstractFunctionalTest
        public static string TestDirectory { get; set; }       

        public static List<Exception> TestExceptions { get; set; }  // To avoid showing unexpected exception UI during tests and instead log them as failures
        // public static IList<string> PauseForms { get; set; }        // List of forms to pause after displaying.
        #endregion

        [STAThread]
        public static void Main(string[] args)
        {
            ProgramLog.Init("SkylineBatch");
            Application.EnableVisualStyles();

            AddFileTypesToRegistry();

            if (!FunctionalTest)
            {
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                // Handle exceptions on the UI thread.
                Application.ThreadException += ((sender, e) => ProgramLog.Error(e.Exception.Message, e.Exception));
                // Handle exceptions on the non-UI thread.
                AppDomain.CurrentDomain.UnhandledException += ((sender, e) =>
                {
                    try
                    {
                        ProgramLog.Error(Resources.Program_Main_An_unexpected_error_occured_during_initialization_,
                            (Exception) e.ExceptionObject);
                        MessageBox.Show(Resources.Program_Main_An_unexpected_error_occured_during_initialization_ +
                                        Environment.NewLine +
                                        string.Format(Resources.Program_Main_Error_details_may_be_found_in_the_file__0_,
                                            Path.Combine(
                                                Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty,
                                                "SkylineBatchProgram.log")) + Environment.NewLine +
                                        Environment.NewLine +
                                        ((Exception) e.ExceptionObject).Message
                        );
                    }
                    finally
                    {
                        Application.Exit();
                    }
                });
            }

            using (var mutex = new Mutex(false, $"University of Washington {AppName()}"))
            {
                if (!mutex.WaitOne(TimeSpan.Zero))
                {
                    MessageBox.Show(string.Format(Resources.Program_Main_Another_instance_of__0__is_already_running_, AppName()), AppName(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                InitializeSecurityProtocol();
                
                // Initialize log4net -- global application logging
                XmlConfigurator.Configure();

                try
                {
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    ProgramLog.Info(string.Format(Resources.Program_Main_Saved_configurations_were_found_in___0_, config.FilePath));
                }
                catch (Exception)
                {
                    // ignored
                }
                
                if (!InitSkylineSettings()) return;
                RInstallations.FindRDirectory();

                var openFile = args.Length > 0 ? args[0] : null;
                MainWindow = new MainForm(openFile);
                // CurrentDeployment is null if it isn't network deployed.
                _version = ApplicationDeployment.IsNetworkDeployed
                    ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString()
                    : string.Empty;
                MainWindow.Text = Version();
                Application.Run(MainWindow);

                mutex.ReleaseMutex();
            }
        }
        
        private static bool InitSkylineSettings()
        {
            if (SkylineInstallations.FindSkyline())
                return true;
            
            var form = new FindSkylineForm(AppName(), Icon());
            Application.Run(form);
            if (form.DialogResult == DialogResult.OK)
                return true;

            MessageBox.Show(string.Format(Resources.Program_InitSkylineSettings__0__requires_Skyline_to_run_, AppName()) + Environment.NewLine +
                string.Format(Resources.Program_InitSkylineSettings_Please_install_Skyline_to_start__0__, AppName()), AppName(), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        private static void AddFileTypesToRegistry()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var configFileIconPath = Path.Combine(baseDirectory, "SkylineBatch_configs.ico");
            FileUtil.AddFileType(".bcfg", "SkylineBatch.Configuration.0", @"Skyline Batch Configuration File", 
                Assembly.GetExecutingAssembly().Location, configFileIconPath);
        }
        
        public static string Version()
        {
            return $"{AppName()} {_version}";
        }

        public static string AppName()
        {
            return "Skyline Batch";
        }

        public static Icon Icon()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var iconPath = Path.Combine(baseDirectory, "SkylineBatch_release.ico");
            return System.Drawing.Icon.ExtractAssociatedIcon(iconPath);
        }

        private static void InitializeSecurityProtocol()
        {
            // Make sure we can negotiate with HTTPS servers that demand TLS 1.2 (default in dotNet 4.6, but has to be turned on in 4.5)
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);  
        }

        public static void AddTestException(Exception exception)
        {
            lock (TestExceptions)
            {
                TestExceptions.Add(exception);
            }
        }
    }
}
