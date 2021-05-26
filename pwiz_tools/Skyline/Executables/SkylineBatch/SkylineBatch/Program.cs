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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows.Forms;
using log4net.Config;
using SkylineBatch.Properties;
using SharedBatch;

namespace SkylineBatch
{
    public class Program
    {
        public const string ADMIN_VERSION = "21.1.0.146";

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
            InitializeVersion();

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
                SendAnalyticsHit();
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

                string configFile = null;
                try
                {
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    configFile = config.FilePath;
                    ProgramLog.Info(string.Format(Resources.Program_Main_Saved_configurations_were_found_in___0_, config.FilePath));
                    if (!InitSkylineSettings()) return;
                    RInstallations.FindRDirectory();
                }
                catch (ConfigurationException e)
                {
                    ProgramLog.Error(e.Message, e);
                    var folderToCopy = Path.GetDirectoryName(ProgramLog.GetProgramLogFilePath()) ?? string.Empty;
                    var newFileName = Path.Combine(folderToCopy, "error-user.config");
                    var message = string.Format(
                        Resources.Program_Main_There_was_an_error_reading_the_saved_configurations_from_an_earlier_version_of__0___,
                        AppName());
                    if (configFile != null)
                    {
                        File.Copy(configFile, newFileName, true);
                        File.Delete(configFile);
                        message += Environment.NewLine + Environment.NewLine +
                                   string.Format(
                                       Resources.Program_Main_To_help_improve__0__in_future_versions__please_post_the_configuration_file_to_the_Skyline_Support_board_,
                                       AppName()) +
                                   Environment.NewLine +
                                   newFileName;
                    }
                    
                    MessageBox.Show(message);
                    Application.Restart();
                    return;
                }
                


                AddFileTypesToRegistry();
                var openFile = GetFirstArg(args);

                MainWindow = new MainForm(openFile);
                MainWindow.Text = Version();
                Application.Run(MainWindow);

                mutex.ReleaseMutex();
            }
        }

        private static void InitializeVersion()
        {
            _version = ApplicationDeployment.IsNetworkDeployed
                ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString()
                : string.Empty;
        }

        private static string GetFirstArg(string[] args)
        {
            string arg;
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                _version = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
                var activationData = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData;
                arg = activationData != null && activationData.Length > 0
                    ? activationData[0]
                    : string.Empty;
            }
            else
            {
                _version = string.Empty;
                arg = args.Length > 0 ? args[0] : string.Empty;
            }

            return arg;
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
            if (FunctionalTest) return;
            var appReference = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Microsoft\\Windows\\Start Menu\\Programs\\MacCoss Lab, UW\\" + AppName() + TextUtil.EXT_APPREF;
            var appExe = Application.ExecutablePath;

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var configFileIconPath = Path.Combine(baseDirectory, "SkylineBatch_configs.ico");

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                FileUtil.AddFileTypeClickOnce(TextUtil.EXT_BCFG, "SkylineBatch.Configuration.0",
                    Resources.Program_AddFileTypesToRegistry_Skyline_Batch_Configuration_File,
                    appReference, configFileIconPath);
            }
            else
            {
                FileUtil.AddFileTypeAdminInstall(TextUtil.EXT_BCFG, "SkylineBatch.Configuration.0",
                    Resources.Program_AddFileTypesToRegistry_Skyline_Batch_Configuration_File,
                    appExe, configFileIconPath);
            }
        }

        private static void SendAnalyticsHit()
        {
            // ReSharper disable LocalizableElement
            var postData = "v=1"; // Version 
            postData += "&t=event"; // Event hit type
            postData += "&tid=UA-9194399-1"; // Tracking Id 
            postData += "&cid=" + SharedBatch.Properties.Settings.Default.InstallationId; // Anonymous Client Id
            postData += "&ec=InstanceBatch"; // Event Category
            postData += "&ea=" + Uri.EscapeDataString((_version.Length > 0 ? _version : ADMIN_VERSION) + "batch");
            var dailyRegex = new Regex(@"[0-9]+\.[0-9]+\.[19]\.[0-9]+");
            postData += "&el=" + (dailyRegex.IsMatch(_version) ? "batch-daily" : "batch-release");
            postData += "&p=" + "Instance"; // Page

            var data = Encoding.UTF8.GetBytes(postData);
            var analyticsUrl = "http://www.google-analytics.com/collect";
            var request = (HttpWebRequest)WebRequest.Create(analyticsUrl);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;
            try
            {
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            } catch (Exception e)
            {
                ProgramLog.Error(string.Format(Resources.Program_SendAnalyticsHit_There_was_an_error_connecting_to__0___Skipping_sending_analytics_, analyticsUrl), e);
                return;
            }

            var response = (HttpWebResponse)request.GetResponse();
            var responseStream = response.GetResponseStream();
            if (null != responseStream)
            {
                new StreamReader(responseStream).ReadToEnd();
            }
            // ReSharper restore LocalizableElement
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
            return System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
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
