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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class ImportResultsNameDlg : FormEx
    {
        private readonly string _prefixInitial;
        private readonly string _suffixInitial;
        private readonly string[] _resultNames;

        public ImportResultsNameDlg(string prefix, string suffix, string[] resultNames)
        {
            InitializeComponent();

            _resultNames = resultNames;
            textPrefix.Text = _prefixInitial = prefix;
            textPrefix.SelectionStart = textPrefix.Text.Length;
            textPrefix.SelectionLength = 0;
            textSuffix.Text = _suffixInitial = suffix;
            textSuffix.SelectionStart = textSuffix.SelectionLength = 0;
            int heightDistance = textSuffix.Location.Y - textPrefix.Location.Y;
            if (string.IsNullOrEmpty(suffix))
            {
                // Use default text in form
                ShrinkHeight(heightDistance);
                textSuffix.Visible = false;
                labelSuffix.Visible = false;
            }
            else if (string.IsNullOrEmpty(prefix))
            {
                labelExplanation.Text = FileUIResources.ImportResultsNameDlg_CommonSuffix;
                ShrinkHeight(heightDistance);
                textPrefix.Visible = false;
                labelPrefix.Visible = false;
                textSuffix.Location -= new Size(0, heightDistance);
                labelSuffix.Location -= new Size(0, heightDistance);
            }
            else
            {
                labelExplanation.Text = FileUIResources.ImportResultsNameDlg_CommonPrefix_and_Suffix;
            }
        }

        private void ShrinkHeight(int heightDistance)
        {
            listReplicateNames.Anchor = listReplicateNames.Anchor & ~AnchorStyles.Bottom;
            Height -= heightDistance;
            labelReplicateNames.Top -= heightDistance;
            listReplicateNames.Top -= heightDistance;
            listReplicateNames.Anchor = listReplicateNames.Anchor | AnchorStyles.Bottom;
        }

        public bool IsRemove
        {
            get { return radioRemove.Checked; }
            set
            {
                if (value)
                    radioRemove.Checked = true;
                else
                    radioDontRemove.Checked = true;
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
            if (name.StartsWith(Prefix))
                name = name.Substring(Prefix.Length);
            if (name.EndsWith(Suffix))
                name = name.Substring(0, name.Length - Suffix.Length);
            return name;
        }

        public void OkDialog()
        {
            if (!string.IsNullOrEmpty(Prefix) && !string.IsNullOrEmpty(_prefixInitial) && !_prefixInitial.StartsWith(Prefix))
            {
                MessageDlg.Show(this, string.Format(FileUIResources.ImportResultsNameDlg_OkDialog_The_text__0__is_not_a_prefix_of_the_files_chosen, Prefix));
                return;
            }
            if (!string.IsNullOrEmpty(Suffix) && !string.IsNullOrEmpty(_suffixInitial) && !_suffixInitial.EndsWith(Suffix))
            {
                MessageDlg.Show(this, string.Format(FileUIResources.ImportResultsNameDlg_OkDialog_The_text__0__is_not_a_suffix_of_the_files_chosen, Suffix));
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void textPrefix_TextChanged(object sender, EventArgs e)
        {
            textPrefix.ForeColor = (_prefixInitial.StartsWith(Prefix) ? Color.Black : Color.Red);
            UpdateReplicateNames();
        }

        private void textSuffix_TextChanged(object sender, EventArgs e)
        {
            textSuffix.ForeColor = (_suffixInitial.EndsWith(Suffix) ? Color.Black : Color.Red);
            UpdateReplicateNames();
        }

        private void UpdateReplicateNames()
        {
            string prefix = Prefix, suffix = Suffix;
            listReplicateNames.BeginUpdate();
            listReplicateNames.Items.Clear();
            listReplicateNames.Items.AddRange(_resultNames.Select(name =>
            {
                if (!string.IsNullOrEmpty(prefix) && name.StartsWith(prefix))
                    name = name.Substring(prefix.Length, name.Length - prefix.Length);
                if (!string.IsNullOrEmpty(suffix) && name.EndsWith(suffix))
                    name = name.Substring(0, name.Length - suffix.Length);
                return name;
            }).ToArray());
            listReplicateNames.EndUpdate();
        }

        #region Functional test support

        public void NoDialog()
        {
            IsRemove = false;
            OkDialog();
        }

        public void YesDialog()
        {
            IsRemove = true;
            OkDialog();
        }

        #endregion

        private void radioDontRemove_CheckedChanged(object sender, EventArgs e)
        {
            UpdateIsRemove();
        }

        private void radioRemove_CheckedChanged(object sender, EventArgs e)
        {
            UpdateIsRemove();
        }

        private void UpdateIsRemove()
        {
            labelPrefix.Enabled = labelSuffix.Enabled =
                textPrefix.Enabled = textSuffix.Enabled = IsRemove;
            if (IsRemove)
            {
                textPrefix.Text = _prefixInitial;
                textSuffix.Text = _suffixInitial;
            }
            else
            {
                textPrefix.Text = textSuffix.Text = string.Empty;
            }
        }
    }
}
