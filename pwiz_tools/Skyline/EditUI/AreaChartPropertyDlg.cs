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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class AreaChartPropertyDlg : FormEx
    {
        public AreaChartPropertyDlg()
        {
            InitializeComponent();

            cbDecimalCvs.Checked = Settings.Default.PeakDecimalCv;
            if (Settings.Default.PeakAreaMaxArea != 0)
                textMaxArea.Text = Settings.Default.PeakAreaMaxArea.ToString(LocalizationHelper.CurrentCulture);
            if (Settings.Default.PeakAreaMaxCv != 0)
                textMaxCv.Text = Settings.Default.PeakAreaMaxCv.ToString(LocalizationHelper.CurrentCulture);
            GraphFontSize.PopulateCombo(textSizeComboBox, Settings.Default.AreaFontSize);
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            double maxArea = 0;
            if (!string.IsNullOrEmpty(textMaxArea.Text))
            {
                if (!helper.ValidateDecimalTextBox(textMaxArea, 5, double.MaxValue, out maxArea))
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

            Settings.Default.PeakAreaMaxArea = maxArea;
            Settings.Default.PeakAreaMaxCv = maxCv;
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
                textMaxCv.Text = (maxCv*factor).ToString(LocalizationHelper.CurrentCulture);
        }
    }
}
