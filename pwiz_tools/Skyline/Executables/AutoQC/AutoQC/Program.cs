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
using System.Deployment.Application;
using System.IO;
using System.Windows.Forms;
using log4net;
using log4net.Config;

namespace AutoQC
{
    class Program
    {
        private static readonly ILog LOG = LogManager.GetLogger("AutoQC");
        private static string VERSION;

        [STAThread]
        public static void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // Initialize log4net -- global application logging
            XmlConfigurator.Configure();

            var form = new MainForm();
            VERSION = ApplicationDeployment.IsNetworkDeployed
                ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString()
                : "";
            form.Text = string.Format("AutoQC Loader {0}", VERSION);
            // form.Text = string.Format("AutoQC Loader-daily {0}", VERSION);

            //Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            //Console.WriteLine("Local user config path: {0}", config.FilePath);

            // Handle exceptions on the UI thread.
            Application.ThreadException += ((sender, e) => LOG.Error(e.Exception));

            // Handle exceptions on the non-UI thread.
            AppDomain.CurrentDomain.UnhandledException += ((sender, e) =>
            {
                try
                {
                    LOG.Error("AutoQC Loader encountered an unexpected error. ", (Exception)e.ExceptionObject);
                    MessageBox.Show("AutoQC Loader encountered an unexpected error. " +
                                    "Error details may be found in the AutoQCProgram.log file in this directory : "
                                     + Path.GetDirectoryName(Application.ExecutablePath)
                                    );
                }
                finally
                {
                    Application.Exit();
                }
            }
                );

            Application.Run(form);       
        }

        public static void LogError(string message)
        {
            LOG.Error(message);
        }

        public static void LogError(string message, Exception e)
        {
            LOG.Error(message, e);
        }

        public static void LogInfo(string message)
        {
            LOG.Info(message);
        }

        public static string version()
        {
            return VERSION;
        }

    }
}
