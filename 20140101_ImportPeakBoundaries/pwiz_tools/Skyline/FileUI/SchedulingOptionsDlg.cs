/*
 * Original author: Mimi Fung <mfung03 .at. u.washington.edu>,
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
using System.IO;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{

    /// <summary>
    /// Allows users the Scheduling Options of either using average retention times 
    /// or using retention values from a single data set.
    /// </summary>
    public partial class SchedulingOptionsDlg : FormEx
    {
        private readonly SrmDocument _document;
        private readonly Func<int, bool> _canTriggerReplicate;

        public SchedulingOptionsDlg(SrmDocument document, Func<int, bool> canTriggerReplicate)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _document = document;
            _canTriggerReplicate = canTriggerReplicate;

            foreach (var chromatogramSet in document.Settings.MeasuredResults.Chromatograms)
            {
                comboReplicateNames.Items.Add(chromatogramSet);
            }
            ComboHelper.AutoSizeDropDown(comboReplicateNames);

            radioSingleDataSet.Checked = !Settings.Default.ScheduleAvergeRT;
        }

        private int? _replicateNum;
        public int? ReplicateNum
        {
            get
            {
                if (Algorithm == ExportSchedulingAlgorithm.Single)
                    _replicateNum = comboReplicateNames.SelectedIndex;
                return _replicateNum;
            }
            set
            {
                _replicateNum = value;
                if (Algorithm == ExportSchedulingAlgorithm.Single)
                    comboReplicateNames.SelectedIndex = _replicateNum ?? 0;
            }
        }

        public ExportSchedulingAlgorithm Algorithm
        {
            get
            {
                if (radioSingleDataSet.Checked)
                    return ExportSchedulingAlgorithm.Single;

                return radioTrends.Checked
                           ? ExportSchedulingAlgorithm.Trends
                           : ExportSchedulingAlgorithm.Average;
            }
            set
            {
                if (value == ExportSchedulingAlgorithm.Single)
                {
                    radioSingleDataSet.Checked = true;
                }
                else if (value == ExportSchedulingAlgorithm.Trends)
                {
                    if (!HasMinTrendReplicates())
                        throw new InvalidDataException(TrendsError);

                    radioTrends.Checked = true;
                }
                else
                {
                    radioRTavg.Checked = true;
                }
            }
        }

        // TODO: Set properties here
        // Save document as property
        public void OkDialog()
        {
            if (Algorithm == ExportSchedulingAlgorithm.Single)
            {
                ReplicateNum = comboReplicateNames.SelectedIndex;
                if (!_canTriggerReplicate(ReplicateNum.Value))
                {
                    MessageDlg.Show(this, string.Format(Resources.SchedulingOptionsDlg_OkDialog_The_replicate__0__contains_peptides_without_enough_information_to_rank_transitions_for_triggered_acquisition_,
                                                        comboReplicateNames.SelectedItem));
                    return;
                }
            }
            // TODO: Show radio botton and complete this code.
            else if (Algorithm == ExportSchedulingAlgorithm.Trends)
            {
                ReplicateNum = _document.Settings.PeptideSettings.Prediction.CalcMaxTrendReplicates(_document);
                // TODO: check if calc max trend = 0, if so message box, saying can't do it
                // return
            }

            Settings.Default.ScheduleAvergeRT = radioRTavg.Checked;
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        /// <summary>
        /// Updates SchedulingOptions form should radioSingleDataSet be checked
        /// </summary>
        private void radioSingleDataSet_CheckedChanged(object sender, EventArgs e)
        {
            comboReplicateNames.Enabled = radioSingleDataSet.Checked;
            comboReplicateNames.SelectedIndex = comboReplicateNames.Enabled
                ? comboReplicateNames.Items.Count - 1
                : -1;

            if (radioTrends.Checked && !HasMinTrendReplicates())
            {
                MessageDlg.Show(this, TrendsError);
            }
        }

        private static string TrendsError
        {
            get
            {
                return string.Format(Resources.SchedulingOptionsDlg_TrendsError_Using_trends_in_scheduling_requires_at_least__0__replicates,
                                     TransitionGroupDocNode.MIN_TREND_REPLICATES);
            }
        }

        private bool HasMinTrendReplicates()
        {
            return comboReplicateNames.Items.Count >= TransitionGroupDocNode.MIN_TREND_REPLICATES;
        }
    }
}
