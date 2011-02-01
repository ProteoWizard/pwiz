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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.FileUI
{

    /// <summary>
    /// Allows users the Scheduling Options of either using average retention times 
    /// or using retention values from a single data set.
    /// </summary>

    public partial class SchedulingOptionsDlg : Form
    {
        public SchedulingOptionsDlg(SrmDocument document)
        {
            InitializeComponent();

            foreach (var chromatogramSet in document.Settings.MeasuredResults.Chromatograms)
            {
                comboReplicateNames.Items.Add(chromatogramSet);
            }
            comboReplicateNames.SelectedIndex = comboReplicateNames.Items.Count - 1;

            radioSingleDataSet.Checked = !Settings.Default.ScheduleAvergeRT;
        }

        public int? ReplicateIndex
        {
            get
            {
                if (radioSingleDataSet.Checked)
                    return comboReplicateNames.SelectedIndex;
                return null;
            }
            set { comboReplicateNames.SelectedIndex = value ?? 0; }
        }

        public ExportSchedulingAlgorithm Algorithm
        {
            get
            {
                return (radioSingleDataSet.Checked ?
                    ExportSchedulingAlgorithm.Single :
                    ExportSchedulingAlgorithm.Average);
            }
            set
            {
                if (value == ExportSchedulingAlgorithm.Single)
                    radioSingleDataSet.Checked = true;
                else
                    radioRTavg.Checked = true;
            }
        }

        public void OkDialog()
        {
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
            if (radioRTavg.Checked)
            {
                comboReplicateNames.Enabled = false;
            }
            else
            {
                comboReplicateNames.Enabled = true;
            }
        }
    }
}
