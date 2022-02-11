/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class StartPageSettingsUI : FormEx
    {
        private readonly SkylineWindow _skylineWindow;
        private readonly string integrateAllOffText;
        private readonly string integrateAllOnText;

        public StartPageSettingsUI(SkylineWindow skylineWindow)
        {
            _skylineWindow = skylineWindow;

            InitializeComponent();
            if (ModeUI != SrmDocument.DOCUMENT_TYPE.proteomic)
            {
                pictureBox2.Image = Resources.WizardMoleculeIcon;
            }

            AcceptButton = nextBtn;
            CenterToParent();
            if (_skylineWindow.DocumentUI.Settings.TransitionSettings.Integration.IsIntegrateAll)
                radioBtnQuant.Checked = true;

            integrateAllOffText = labelIntegrateAll.Text;
            integrateAllOnText = Resources.StartPageSettingsUI_StartPageSettingsUI_Integrate_all__on;
        }

        public bool IsIntegrateAll 
        {
            get { return radioBtnQuant.Checked; }
            set { radioBtnQuant.Checked = value; }
        }

        private void peptideSettingsBtn_Click(object sender, EventArgs e)
        {
            ShowPeptideSettingsUI();
        }

        public void ShowPeptideSettingsUI()
        {
            _skylineWindow.ShowPeptideSettingsUI(this);
        }

        private void transitionSettingsBtn_Click(object sender, EventArgs e)
        {
            ShowTransitionSettingsUI();
        }

        public void ShowTransitionSettingsUI()
        {
            _skylineWindow.ShowTransitionSettingsUI(this);
        }

        private void btnResetDefaults_Click(object sender, EventArgs e)
        {
            ResetDefaults();
        }

        public void ResetDefaults()
        {
            _skylineWindow.ResetDefaultSettings();
            MessageDlg.Show(this,
                Resources.StartPageSettingsUI_btnResetDefaults_Click_The_settings_have_been_reset_to_the_default_values_);
        }

        private void radioBtnQuant_CheckedChanged(object sender, EventArgs e)
        {
            labelIntegrateAll.Text = radioBtnQuant.Checked
                ? integrateAllOnText
                : integrateAllOffText;
        }

        public void OkDialog()
        {
            AcceptButton.PerformClick();    // CONSIDER: Not the way we normally do things
        }
    }
}
