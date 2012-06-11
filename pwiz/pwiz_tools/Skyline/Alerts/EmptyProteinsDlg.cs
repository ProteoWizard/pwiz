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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
    /// </summary>
    public partial class EmptyProteinsDlg : FormEx
    {
        public EmptyProteinsDlg(int countEmpty)
        {
            InitializeComponent();

            string message = string.Format(labelMessage.Text, countEmpty == 1 ?
                "1 new protein" : string.Format("{0} new proteins", countEmpty));
            if (countEmpty < 500)
                message = message.Substring(0, message.LastIndexOf('\r'));
            labelMessage.Text = message;
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
            IsKeepEmptyProteins = true;
            OkDialog();
        }
    }
}
