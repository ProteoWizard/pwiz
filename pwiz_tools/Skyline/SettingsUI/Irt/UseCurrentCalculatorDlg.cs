/*
 * Original author: Kaipo Tamura <kaipot .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class UseCurrentCalculatorDlg : FormEx
    {
        public bool UseCurrent => radioUseCurrent.Checked;
        public string StandardName { get; private set; }

        private readonly HashSet<string> _existing;

        public UseCurrentCalculatorDlg(IEnumerable<IrtStandard> existing)
        {
            InitializeComponent();
            _existing = existing.Select(standard => standard.Name).ToHashSet();
        }

        private void btnOk_Click(object sender, System.EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            if (radioUseCurrent.Checked)
            {
                if (!helper.ValidateNameTextBox(textName, out var name))
                {
                    return;
                }
                else if (_existing.Contains(name))
                {
                    helper.ShowTextBoxError(textName, Resources.CalibrateIrtDlg_OkDialog_The_iRT_standard__0__already_exists_, name);
                    return;
                }
                StandardName = name;
            }

            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, System.EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void RadioCheckedChanged(object sender, System.EventArgs e)
        {
            textName.Enabled = radioUseCurrent.Checked;
        }
    }
}
