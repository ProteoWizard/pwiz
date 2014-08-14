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
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class ImportResultsNameDlg : FormEx
    {
        private readonly string _prefixInitial;

        public ImportResultsNameDlg(string prefix)
        {
            InitializeComponent();

            textPrefix.Text = _prefixInitial = prefix;
        }

        public string Prefix 
        {
            get { return textPrefix.Text; }
            set { textPrefix.Text = value; } 
        }

        public void OkDialog(DialogResult result)
        {
            if (DialogResult != DialogResult.Cancel)
            {
                if (!_prefixInitial.StartsWith(Prefix))
                {
                    MessageBox.Show(this, string.Format(Resources.ImportResultsNameDlg_OkDialog_The_text__0__is_not_a_prefix_of_the_files_chosen, Prefix), Program.Name);
                    return;
                }
            }

            DialogResult = result;
            Close();
        }

        public void YesDialog()
        {
            OkDialog(DialogResult.Yes);
        }

        public void NoDialog()
        {
            OkDialog(DialogResult.No);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            YesDialog();
        }

        private void btnDoNotRemove_Click(object sender, EventArgs e)
        {
            NoDialog();
        }

        private void textPrefix_TextChanged(object sender, EventArgs e)
        {
            textPrefix.ForeColor = (_prefixInitial.StartsWith(Prefix) ? Color.Black : Color.Red);
        }
    }
}
