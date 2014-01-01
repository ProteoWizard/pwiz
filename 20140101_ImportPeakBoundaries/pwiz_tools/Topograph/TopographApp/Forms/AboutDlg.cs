/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

namespace pwiz.Topograph.ui.Forms
{
    partial class AboutDlg : Form
    {
        public AboutDlg()
        {
            InitializeComponent();
            Text = String.Format("About {0}", Program.AppName);
            labelVersion.Text = String.Format("Version {0}", ApplicationDeployment.IsNetworkDeployed 
                ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString() : "");
            if (Environment.Is64BitProcess)
            {
                labelVersion.Text += " (64-bit)";
            }
        }
    }
}
