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
using System;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
    /// </summary>
    public partial class EmptyProteinsDlg : FormEx
    {
        public int EmptyProteins { get; private set; }

        public EmptyProteinsDlg(int countEmpty)
        {
            InitializeComponent();

            EmptyProteins = countEmpty;

            string message = string.Format(labelMessage.Text, countEmpty == 1
                ? Resources.EmptyProteinsDlg_EmptyProteinsDlg_1_new_protein 
                : string.Format(Resources.EmptyProteinsDlg_EmptyProteinsDlg__0__new_proteins, countEmpty));
            labelMessage.Text = message;
            if (countEmpty < 500)
                labelPerf.Visible = false;
        }

        public bool IsKeepEmptyProteins { get; set; }

        protected override void CreateHandle()
        {
            base.CreateHandle();

            Text = Program.Name;
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private void btnKeep_Click(object sender, EventArgs e)
        {
            KeepEmptyProteins();
        }

        public void KeepEmptyProteins()
        {
            IsKeepEmptyProteins = true;
            OkDialog();
        }
    }
}
