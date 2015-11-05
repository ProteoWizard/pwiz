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
using System.Windows.Forms;

namespace AutoQC
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            var form = new AutoQCForm();
            var version = ApplicationDeployment.IsNetworkDeployed
                ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString()
                : "";
            form.Text = string.Format("Panorama AutoQC-daily {0}", version);

            // Handle exceptions on the UI thread.
            Application.ThreadException += ((sender, e) => form.LogException(e.Exception));

            // Handle exceptions on the non-UI thread.
            AppDomain.CurrentDomain.UnhandledException += ((sender, e) =>
            {
                try
                {
                    form.LogError("AutoQC encountered an unexpected error. ");
                    form.LogException((Exception) e.ExceptionObject);
                    MessageBox.Show("AutoQC encountered an unexpected error. " +
                                    "Please send the latest AutoQC log file in this directory to the developers : " +
                                    form.GetLogDirectory());
                }
                finally
                {
                    Application.Exit();
                }
            }
                );

            Application.Run(form);       
        }
    }
}
