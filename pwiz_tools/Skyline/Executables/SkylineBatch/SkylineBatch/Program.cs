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
using System.Configuration;
using System.Deployment.Application;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Repository.Hierarchy;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger("SkylineBatch");
        private static string _version;
        

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            // Handle exceptions on the UI thread.
            Application.ThreadException += ((sender, e) => Log.Error(e.Exception));
            // Handle exceptions on the non-UI thread.
            AppDomain.CurrentDomain.UnhandledException += ((sender, e) =>
            {
                try
                {
                    Log.Error(Resources.Program_Main_An_unexpected_error_occured_during_initialization_, (Exception)e.ExceptionObject);
                    MessageBox.Show(Resources.Program_Main_An_unexpected_error_occured_during_initialization_ + Environment.NewLine +
                                    string.Format(Resources.Program_Main_Error_details_may_be_found_in_the_file__0_,
                                        Path.Combine(Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty, "SkylineBatchProgram.log")) + Environment.NewLine +
                                    Environment.NewLine +
                                    ((Exception)e.ExceptionObject).Message
                    );
                }
                finally
                {
                    Application.Exit();
                }
            });

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
                    LogInfo(string.Format("user.config path: {0}", config.FilePath));
                }
                catch (Exception)
                {
                    // ignored
                }
                
                if (!InitSkylineSettings()) return;
                Installations.FindRDirectory();


                var form = new MainForm();

                // CurrentDeployment is null if it isn't network deployed.
                _version = ApplicationDeployment.IsNetworkDeployed
                    ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString()
                    : string.Empty;
                form.Text = Version();

                Application.Run(form);

                mutex.ReleaseMutex();
            }
        }


        private static bool InitSkylineSettings()
        {
            if (Installations.FindSkyline())
                return true;

            var form = new FindSkylineForm();
            Application.Run(form);
            if (form.DialogResult == DialogResult.OK)
                return true;

            MessageBox.Show(string.Format(Resources.Program_InitSkylineSettings__0__requires_Skyline_to_run_, AppName()) + Environment.NewLine +
                string.Format("Please install Skyline to start {0}.", AppName()), AppName(), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;

        }

        public static void LogError(string message)
        {
            Log.Error(message);
        }

        public static void LogError(string configName, string message)
        {
            Log.Error(string.Format("{0}: {1}", configName, message));
        }

        public static void LogError(string message, Exception e)
        {
            Log.Error(message, e);
        }

        public static void LogError(string configName, string message, Exception e)
        {
            LogError(string.Format("{0}: {1}", configName, message), e);
        }

        public static void LogInfo(string message)
        {
            Log.Info(message);
        }

        public static string GetProgramLogFilePath()
        {
            var repository = ((Hierarchy) LogManager.GetRepository());
            FileAppender rootAppender = null;
            if (repository != null)
            {
                rootAppender = repository.Root.Appenders.OfType<FileAppender>()
                    .FirstOrDefault();
            }

            return rootAppender != null ? rootAppender.File : string.Empty;
        }

        public static string Version()
        {
            return $"{AppName()} {_version}";
        }

        public static string AppName()
        {
            return "Skyline Batch";
        }

        private static void InitializeSecurityProtocol()
        {
            // Make sure we can negotiate with HTTPS servers that demand TLS 1.2 (default in dotNet 4.6, but has to be turned on in 4.5)
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);  
        }
    }
}
