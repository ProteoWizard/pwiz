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

namespace pwiz.Skyline.EditUI
{
    public partial class FindNodeDlg : Form
    {
        public FindNodeDlg()
        {
            InitializeComponent();
        }

        public string SearchString
        {
            get { return textSequence.Text; }
            set { textSequence.Text = value; }
        }

        public bool SearchUp
        {
            get { return radioUp.Checked; }
            set { radioUp.Checked = value; }
        }

        public bool CaseSensitive
        {
            get { return cbCaseSensitive.Checked; }
            set { cbCaseSensitive.Checked = value; }
        }

        private void textSequence_TextChanged(object sender, EventArgs e)
        {
            btnFindNext.Enabled = !string.IsNullOrEmpty(SearchString);
        }

        private void btnFindNext_Click(object sender, EventArgs e)
        {
            FindNext();
        }

        public void FindNext()
        {
            Settings.Default.EditFindText = SearchString;
            Settings.Default.EditFindCase = CaseSensitive;
            ((SkylineWindow)Owner).FindNext(SearchUp);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
