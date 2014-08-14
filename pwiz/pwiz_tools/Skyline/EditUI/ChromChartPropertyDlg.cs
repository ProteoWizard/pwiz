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
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class ChromChartPropertyDlg : FormEx
    {
        public ChromChartPropertyDlg()
        {
            InitializeComponent();

            textLineWidth.Text = Settings.Default.ChromatogramLineWidth.ToString(LocalizationHelper.CurrentCulture);
            textFontSize.Text = Settings.Default.ChromatogramFontSize.ToString(LocalizationHelper.CurrentCulture);

            if (Settings.Default.ChromatogramTimeRange == 0)
            {
                Settings.Default.ChromatogramTimeRange = GraphChromatogram.DEFAULT_PEAK_RELATIVE_WINDOW;
                Settings.Default.ChromatogramTimeRangeRelative = true;
            }

            cbRelative.Checked = Settings.Default.ChromatogramTimeRangeRelative;
            textTimeRange.Text = Settings.Default.ChromatogramTimeRange.ToString(LocalizationHelper.CurrentCulture);

            if (Settings.Default.ChromatogramMaxIntensity != 0)
                textMaxIntensity.Text = Settings.Default.ChromatogramMaxIntensity.ToString(LocalizationHelper.CurrentCulture);
            cbShowOverlappingLabels.Checked = Settings.Default.AllowLabelOverlap;
            cbShowMultiplePeptides.Checked = Settings.Default.AllowMultiplePeptideSelection;
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            int lineWidth;
            if (!helper.ValidateNumberTextBox(e, textLineWidth, 1, 5, out lineWidth))
                return;

            int fontSize;
            if (!helper.ValidateNumberTextBox(e, textFontSize, 6, 128, out fontSize))
                return;

            double timeRange = 0;
            if (!string.IsNullOrEmpty(textTimeRange.Text))
            {
                if (!helper.ValidateDecimalTextBox(e, textTimeRange, 0.05, 15.0, out timeRange))
                    return;
            }
            bool relative = cbRelative.Checked;

            double maxIntensity = 0;
            if (!string.IsNullOrEmpty(textMaxIntensity.Text))
            {
                if (!helper.ValidateDecimalTextBox(e, textMaxIntensity, 5, double.MaxValue, out maxIntensity))
                    return;
            }

            Settings.Default.ChromatogramLineWidth = lineWidth;
            Settings.Default.ChromatogramFontSize = fontSize;
            Settings.Default.ChromatogramTimeRange = timeRange;
            Settings.Default.ChromatogramTimeRangeRelative = relative;
            Settings.Default.ChromatogramMaxIntensity = maxIntensity;
            if (maxIntensity != 0)
                Settings.Default.LockYChrom = true;
            Settings.Default.AllowLabelOverlap = cbShowOverlappingLabels.Checked;
            Settings.Default.AllowMultiplePeptideSelection = cbShowMultiplePeptides.Checked;
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void cbRelative_CheckedChanged(object sender, EventArgs e)
        {
            labelTimeUnits.Text = (cbRelative.Checked ? 
                Resources.ChromChartPropertyDlg_cbRelative_CheckedChanged_widths : 
                Resources.ChromChartPropertyDlg_cbRelative_CheckedChanged_minutes);
        }

        #region Functional test support

        public int LineWidth
        {
            get { return int.Parse(textLineWidth.Text); }
            set { textLineWidth.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public int FontSize
        {
            get { return int.Parse(textFontSize.Text); }
            set { textFontSize.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        #endregion
    }
}
