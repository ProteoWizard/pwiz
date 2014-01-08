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

using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class AboutDlg : FormEx
    {
        public AboutDlg()
        {
            InitializeComponent();

            labelSoftwareVersion.Text = Install.ProgramNameAndVersion;

            // Designer has problems getting images from resources
            pictureSkylineIcon.Image = Resources.SkylineImg;
            pictureProteoWizardIcon.Image = Resources.ProteoWizard;
        }

        private void linkProteome_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(this, "http://skyline.gs.washington.edu"); // Not L10N
        }

        private void linkProteoWizard_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(this, "http://proteowizard.sourceforge.net/"); // Not L10N
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(this, "http://proteome.gs.washington.edu/software/Skyline/funding.html"); // Not L10N
        }
    }
}
