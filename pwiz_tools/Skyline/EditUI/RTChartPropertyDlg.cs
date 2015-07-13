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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class RTChartPropertyDlg : FormEx
    {
        public RTChartPropertyDlg()
        {
            InitializeComponent();

            cbDecimalCvs.Checked = Settings.Default.PeakDecimalCv;
            if (Settings.Default.PeakTimeMax != 0)
                textMaxTime.Text = Settings.Default.PeakTimeMax.ToString(LocalizationHelper.CurrentCulture);
            if (Settings.Default.PeakTimeMin != 0)
                textMinTime.Text = Settings.Default.PeakTimeMin.ToString(LocalizationHelper.CurrentCulture);
            if (Settings.Default.PeakTimeMaxCv != 0)
                textMaxCv.Text = Settings.Default.PeakTimeMaxCv.ToString(LocalizationHelper.CurrentCulture);
            GraphFontSize.PopulateCombo(textSizeComboBox, Settings.Default.AreaFontSize);
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            double maxTime = 0;
            if (!string.IsNullOrEmpty(textMaxTime.Text))
            {
                if (!helper.ValidateDecimalTextBox(textMaxTime, 5, 1000, out maxTime))
                    return;
            }
            double minTime = 0;
            if (!string.IsNullOrEmpty(textMinTime.Text))
            {
                if (!helper.ValidateDecimalTextBox(textMinTime, 0, 1000, out minTime))
                    return;
            }

            bool decimalCv = cbDecimalCvs.Checked;

            double maxCv = 0;
            if (!string.IsNullOrEmpty(textMaxCv.Text))
            {
                double maxAllowed = 500;
                if (decimalCv)
                    maxAllowed /= 100;
                if (!helper.ValidateDecimalTextBox(textMaxCv, 0, maxAllowed, out maxCv))
                    return;
            }

            Settings.Default.PeakTimeMax = maxTime;
            Settings.Default.PeakTimeMin = minTime;
            Settings.Default.PeakTimeMaxCv = maxCv;
            Settings.Default.PeakDecimalCv = decimalCv;
            Settings.Default.AreaFontSize = GraphFontSize.GetFontSize(textSizeComboBox).PointSize;

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void cbDecimalCvs_CheckedChanged(object sender, EventArgs e)
        {
            labelCvPercent.Visible = !cbDecimalCvs.Checked;
            double factor = (cbDecimalCvs.Checked ? 0.01 : 100);
            double maxCv;
            if (double.TryParse(textMaxCv.Text, out maxCv))
                textMaxCv.Text = (maxCv * factor).ToString(LocalizationHelper.CurrentCulture);
        }
    }
}