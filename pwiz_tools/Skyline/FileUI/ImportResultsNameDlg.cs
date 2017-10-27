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
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class ImportResultsNameDlg : FormEx
    {
        private readonly string _prefixInitial;
        private readonly string _suffixInitial;

        public ImportResultsNameDlg(string prefix, string suffix)
        {
            InitializeComponent();

            textPrefix.Text = _prefixInitial = prefix;
            textSuffix.Text = _suffixInitial = suffix;
            int heightDistance = textSuffix.Location.Y - textPrefix.Location.Y;
            var defaultHeight = Height;
            if (string.IsNullOrEmpty(suffix))
            {
                // Use default text in form
                Height = defaultHeight - heightDistance;
                textSuffix.Visible = false;
                label5.Visible = false;
            }
            else if (string.IsNullOrEmpty(prefix))
            {
                label2.Text = Resources.ImportResultsNameDlg_CommonSuffix;
                Height = defaultHeight - heightDistance;
                textPrefix.Visible = false;
                label1.Visible = false;
                textSuffix.Location -= new Size(0, heightDistance);
                label5.Location -= new Size(0, heightDistance);
            }
            else
            {
                label2.Text = Resources.ImportResultsNameDlg_CommonPrefix_and_Suffix;
            }
        }

        public string Prefix
        {
            get { return textPrefix.Text; }
            set { textPrefix.Text = value; }
        }

        public string Suffix
        {
            get { return textSuffix.Text; }
            set { textSuffix.Text = value; }
        }

        public void ApplyNameChange(IList<KeyValuePair<string, MsDataFileUri[]>> namedFiles)
        {
            // Rename all the replicates to remove the specified prefix or suffix.
            for (int i = 0; i < namedFiles.Count; i++)
            {
                var namedSet = namedFiles[i];
                namedFiles[i] = new KeyValuePair<string, MsDataFileUri[]>(
                    ApplyNameChange(namedSet.Key), namedSet.Value);
            }            
        }

        public string ApplyNameChange(string name)
        {
            int lenRemaining = name.Length - Prefix.Length - Suffix.Length;
            return name.Substring(Prefix.Length, lenRemaining);
        }

        public void OkDialog(DialogResult result)
        {
            if (DialogResult != DialogResult.Cancel)
            {
                if (!string.IsNullOrEmpty(Prefix) && !string.IsNullOrEmpty(_prefixInitial) && !_prefixInitial.StartsWith(Prefix))
                {
                    MessageDlg.Show(this, string.Format(Resources.ImportResultsNameDlg_OkDialog_The_text__0__is_not_a_prefix_of_the_files_chosen, Prefix));
                    return;
                }
                if (!string.IsNullOrEmpty(Suffix) && !string.IsNullOrEmpty(_suffixInitial) && !_suffixInitial.EndsWith(Suffix))
                {
                    MessageDlg.Show(this, string.Format(Resources.ImportResultsNameDlg_OkDialog_The_text__0__is_not_a_suffix_of_the_files_chosen, Suffix));
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

        private void textSuffix_TextChanged(object sender, EventArgs e)
        {
            textSuffix.ForeColor = (_suffixInitial.EndsWith(Suffix) ? Color.Black : Color.Red);
        }
    }
}
