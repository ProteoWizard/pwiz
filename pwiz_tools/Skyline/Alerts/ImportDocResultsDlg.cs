/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class ImportDocResultsDlg : FormEx
    {
        public ImportDocResultsDlg(bool canImportResults)
        {
            InitializeComponent();

            Text = Program.Name;

            CanImportResults = canImportResults;
        }

        public bool CanImportResults { get; private set; }

        public MeasuredResults.MergeAction Action
        {
            get
            {
                if (radioMergeByName.Checked)
                    return MeasuredResults.MergeAction.merge_names;
                if (radioMergeByIndex.Checked)
                    return MeasuredResults.MergeAction.merge_indices;
                if (radioAdd.Checked)
                    return MeasuredResults.MergeAction.add;

                return MeasuredResults.MergeAction.remove;
            }

            set
            {
                switch (value)
                {
                    case MeasuredResults.MergeAction.remove:
                        radioRemove.Checked = true;
                        break;
                    case MeasuredResults.MergeAction.merge_names:
                        radioMergeByName.Checked = true;
                        break;
                    case MeasuredResults.MergeAction.merge_indices:
                        radioMergeByIndex.Checked = true;
                        break;
                    case MeasuredResults.MergeAction.add:
                        radioAdd.Checked = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("value"); // Not L10N
                }
            }
        }

        public bool IsMergePeptides
        {
            get { return cbMergePeptides.Checked; }
            set { cbMergePeptides.Checked = value; }
        }

        public void OkDialog()
        {
            if (!CanImportResults && Action != MeasuredResults.MergeAction.remove)
            {
                MessageDlg.Show(this, Resources.ImportDocResultsDlg_OkDialog_The_document_must_be_saved_before_results_may_be_imported);
                Action = MeasuredResults.MergeAction.remove;
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}
