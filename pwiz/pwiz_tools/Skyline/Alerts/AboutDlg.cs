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
using System.Deployment.Application;
using System.Diagnostics;
using System.Windows.Forms;

namespace pwiz.Skyline.Alerts
{
    public partial class AboutDlg : Form
    {
        public AboutDlg()
        {
            InitializeComponent();

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                labelSoftwareVersion.Text = Program.Name + " " +
                    ApplicationDeployment.CurrentDeployment.CurrentVersion;
            }
        }

// ReSharper disable MemberCanBeMadeStatic.Local
        private void linkProteome_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://proteome.gs.washington.edu");
        }

        private void linkProteoWizard_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://proteowizard.sourceforge.net/");
        }
// ReSharper restore MemberCanBeMadeStatic.Local
    }
}
