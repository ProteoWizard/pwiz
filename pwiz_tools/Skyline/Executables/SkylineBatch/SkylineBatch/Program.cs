/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Text;
using System.Threading;
using System.Windows.Forms;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Repository.Hierarchy;

namespace SkylineBatch
{
    class Program
    {
        private static readonly ILog LOG = LogManager.GetLogger("SkylineBatch");
        private static string _version;
        

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            // Handle exceptions on the UI thread.
            Application.ThreadException += ((sender, e) => LOG.Error(e.Exception));
            // Handle exceptions on the non-UI thread.
            AppDomain.CurrentDomain.UnhandledException += ((sender, e) =>
            {
                try
                {
                    LOG.Error("SkylineBatch encountered an unexpected error. ", (Exception)e.ExceptionObject);
                    MessageBox.Show("SkylineBatch encountered an unexpected error. " +
                                    "Error details may be found in the SkylineBatchProgram.log file in this directory : "
                                    + Path.GetDirectoryName(Application.ExecutablePath)
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
                    MessageBox.Show($"Another instance of {AppName()} is already running.", $"{AppName()} Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                InitSkylineSettings();
                InitRSettings();


                var form = new MainForm();

                // CurrentDeployment is null if it isn't network deployed.
                _version = ApplicationDeployment.IsNetworkDeployed
                    ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString()
                    : "";
                form.Text = Version();

                Application.Run(form);

                mutex.ReleaseMutex();
            }
        }


        private static bool InitSkylineSettings()
        {
            if (SkylineSettings.IsInitialized() || SkylineSettings.FindSkyline(out var pathsChecked))
            {
                return true;
            }

            var message = new StringBuilder();
            message.AppendLine(
                    $"SkylineBatch requires {SkylineSettings.Skyline} or {SkylineSettings.SkylineDaily} to be installed on the computer.")
                .AppendLine($"Unable to find {SkylineSettings.Skyline} at any of the following locations: ")
                .AppendLine(string.Join(Environment.NewLine, pathsChecked)).AppendLine()
                .AppendLine(
                    $"Please install {SkylineSettings.Skyline} or {SkylineSettings.SkylineDaily} to use SkylineBatch");
            MessageBox.Show(message.ToString(), "Unable To Find Skyline",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        private static bool InitRSettings()
        {
            if (SkylineSettings.HasRPath() || SkylineSettings.FindR(out var pathsChecked))
            {
                return true;
            }

            var message = new StringBuilder();
            message.AppendLine(
                    $"SkylineBatch requires rScript.exe to be installed on the computer.")
                .AppendLine($"Unable to find rScript.exe at any of the following locations: ")
                .AppendLine(string.Join(Environment.NewLine, pathsChecked)).AppendLine()
                .AppendLine(
                    $"Please install rScript.exe to use SkylineBatch");
            MessageBox.Show(message.ToString(), "Unable To Find R",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        private static bool IsFirstRun()
        {
            if (!ApplicationDeployment.IsNetworkDeployed)
                return false;

            var currentVersion = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            var installedVersion = Properties.Settings.Default.InstalledVersion ?? string.Empty;
            if (!currentVersion.Equals(installedVersion))
            {
                LogInfo(string.Empty.Equals(installedVersion)
                    ? $"This is a first install and run of version: {currentVersion}."
                    : $"Current version: {currentVersion} is newer than the last installed version: {installedVersion}.");

                Properties.Settings.Default.InstalledVersion = currentVersion;
                Properties.Settings.Default.Save();
                return true;
            }
            LogInfo($"Current version: {currentVersion} same as last installed version: {installedVersion}.");
            return false;
        }

        public static void LogError(string message)
        {
            LOG.Error(message);
        }

        public static void LogError(string configName, string message)
        {
            LOG.Error(string.Format("{0}: {1}", configName, message));
        }

        public static void LogError(string message, Exception e)
        {
            LOG.Error(message, e);
        }

        public static void LogError(string configName, string message, Exception e)
        {
            LogError(string.Format("{0}: {1}", configName, message), e);
        }

        public static void LogInfo(string message)
        {
            LOG.Info(message);
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

        private static string AppName()
        {
            return "SkylineBatch";
        }

        private static void InitializeSecurityProtocol()
        {
            // Make sure we can negotiate with HTTPS servers that demand TLS 1.2 (default in dotNet 4.6, but has to be turned on in 4.5)
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);  
        }
    }
}
