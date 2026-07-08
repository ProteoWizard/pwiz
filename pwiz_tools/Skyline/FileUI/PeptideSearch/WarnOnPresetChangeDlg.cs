/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Common.GUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public class WarnOnPresetChangeDlg : FormEx
    {
        private readonly CheckBox _cbDontShowAgain;

        public WarnOnPresetChangeDlg()
        {
            Text = Program.Name;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 420;
            Height = 160;

            var label = new Label
            {
                Left = 15, Top = 15, Width = 380, Height = 40,
                Text = PeptideSearchResources.ImportPeptideSearchDlg_WarnAndConfirmPresetChange_Note_that_changing_preset_may_affect_settings_on_previous_pages
            };
            _cbDontShowAgain = new CheckBox
            {
                Left = 15, Top = 60, AutoSize = true,
                Text = PeptideSearchResources.ImportPeptideSearchDlg_WarnAndConfirmPresetChange_Dont_show_this_warning_again
            };
            var btnOk = new Button { Text = CommonAlertDlg.GetDefaultButtonText(DialogResult.OK), Left = 230, Width = 80, Top = 90, DialogResult = DialogResult.OK };
            var btnCancelDlg = new Button { Text = CommonAlertDlg.GetDefaultButtonText(DialogResult.Cancel), Left = 320, Width = 80, Top = 90, DialogResult = DialogResult.Cancel };

            Controls.AddRange(new Control[] { label, _cbDontShowAgain, btnOk, btnCancelDlg });
            AcceptButton = btnOk;
            CancelButton = btnCancelDlg;
        }

        public bool DontShowAgain
        {
            get => _cbDontShowAgain.Checked;
            set => _cbDontShowAgain.Checked = value;
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        public void ClickCancel()
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
