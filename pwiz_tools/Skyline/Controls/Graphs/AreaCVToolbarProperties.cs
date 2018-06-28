/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class AreaCVToolbarProperties : FormEx
    {
        private bool _showDecimals;
        private readonly GraphSummary _graphSummary;

        public AreaCVToolbarProperties(GraphSummary graphSummary)
        {
            InitializeComponent();

            _graphSummary = graphSummary;
        }

        private void AreaCvToolbarSettings_Load(object sender, EventArgs e)
        {
            var enabled = _graphSummary.DocumentUIContainer.DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained &&
                AreaGraphController.PointsType == PointsTypePeakArea.targets;
            if (enabled)
                textQValueCutoff.Text = ValueOrEmpty(Settings.Default.AreaCVQValueCutoff);

            if(!double.IsNaN(Settings.Default.AreaCVQValueCutoff))
                textQValueCutoff.Text = Settings.Default.AreaCVQValueCutoff.ToString(LocalizationHelper.CurrentCulture);

            textCvCutoff.Text = Settings.Default.AreaCVCVCutoff.ToString(LocalizationHelper.CurrentCulture);
            checkShowCvCutoff.Checked = Settings.Default.AreaCVShowCVCutoff;
            checkShowMedian.Checked = Settings.Default.AreaCVShowMedianCV;
            checkBoxDecimalCvs.Checked = _showDecimals = Settings.Default.AreaCVShowDecimals;
            labelMaxCvPercent.Visible = labelCvCutoffPercent.Visible = !Settings.Default.AreaCVShowDecimals;
            textMaxFrequency.Text = ValueOrEmpty(Settings.Default.AreaCVMaxFrequency);
            textMaximumCv.Text = ValueOrEmpty(Settings.Default.AreaCVMaxCV);
            textMinLog10.Text = ValueOrEmpty(Settings.Default.AreaCVMinLog10Area);
            textMaxLog10.Text = ValueOrEmpty(Settings.Default.AreaCVMaxLog10Area);

            GraphFontSize.PopulateCombo(comboFontSize, Settings.Default.AreaFontSize);

            var is2DHistogram = _graphSummary.Type == GraphTypeSummary.histogram2d;
            textMinLog10.Visible = textMaxLog10.Visible = labelMinLog10.Visible = labelMaxLog10.Visible = is2DHistogram;
            textMaxFrequency.Visible = labelMaxFrequency.Visible = !is2DHistogram;

            if (is2DHistogram)
            {
                textMinLog10.Location = new Point(textMaxFrequency.Location.X, textMinLog10.Location.Y);
                textMaxLog10.Location = new Point(textMaxFrequency.Location.X, textMaxLog10.Location.Y);
                labelMinLog10.Location = new Point(labelMaxFrequency.Location.X, labelMinLog10.Location.Y);
                labelMaxLog10.Location = new Point(labelMaxFrequency.Location.X, labelMaxLog10.Location.Y);
            }
        }

        private string ValueOrEmpty(double value)
        {
            return double.IsNaN(value) ? string.Empty : value.ToString(CultureInfo.CurrentUICulture);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            var helper = new MessageBoxHelper(this);
            var is2DHistogram = _graphSummary.Type == GraphTypeSummary.histogram2d;

            double qvalue = double.NaN;
            if (!string.IsNullOrWhiteSpace(textQValueCutoff.Text) &&
                !helper.ValidateDecimalTextBox(textQValueCutoff, 0.0, 1.0, out qvalue))
                return;

            double cvCutoff;
            if (!helper.ValidateDecimalTextBox(textCvCutoff, 0, null, out cvCutoff))
                return;

            var maxFrequency = double.NaN;
            if (!is2DHistogram)
            {
                if (!string.IsNullOrEmpty(textMaxFrequency.Text) && !helper.ValidateDecimalTextBox(textMaxFrequency, 0, null, out maxFrequency))
                    return;
            }

            var maxCV = double.NaN;
            if (!string.IsNullOrEmpty(textMaximumCv.Text) && !helper.ValidateDecimalTextBox(textMaximumCv, 0, null, out maxCV))
                return;

            var minLog10Area = double.NaN;
            var maxLog10Area = double.NaN;

            if (is2DHistogram)
            {
                if (!string.IsNullOrEmpty(textMinLog10.Text) && !helper.ValidateDecimalTextBox(textMinLog10, 0, null, out minLog10Area))
                    return;

                if (!string.IsNullOrEmpty(textMaxLog10.Text) && !helper.ValidateDecimalTextBox(textMaxLog10, 0, null, out maxLog10Area))
                    return;

                if (!double.IsNaN(minLog10Area) && !double.IsNaN(maxLog10Area) && minLog10Area >= maxLog10Area)
                {
                    MessageDlg.Show(this, Resources.AreaCVToolbarProperties_btnOk_Click_The_maximum_log10_area_has_to_be_greater_than_the_minimum_log10_area);
                    return;
                }
            }

            Settings.Default.AreaCVQValueCutoff = qvalue;
            Settings.Default.AreaCVCVCutoff = cvCutoff;
            Settings.Default.AreaCVShowCVCutoff = checkShowCvCutoff.Checked;
            Settings.Default.AreaCVShowMedianCV = checkShowMedian.Checked;

            var previous = Settings.Default.AreaCVShowDecimals;
            Settings.Default.AreaCVShowDecimals = checkBoxDecimalCvs.Checked;

            if (!previous && Settings.Default.AreaCVShowDecimals)
                Settings.Default.AreaCVHistogramBinWidth *= 0.01;
            else if (previous && !Settings.Default.AreaCVShowDecimals)
                Settings.Default.AreaCVHistogramBinWidth *= 100.0;

            if (is2DHistogram)
            {
                Settings.Default.AreaCVMinLog10Area = minLog10Area;
                Settings.Default.AreaCVMaxLog10Area = maxLog10Area;
            }
            else
                Settings.Default.AreaCVMaxFrequency = maxFrequency;
            
            Settings.Default.AreaCVMaxCV = maxCV;
            Settings.Default.AreaFontSize = GraphFontSize.GetFontSize(comboFontSize).PointSize;

            DialogResult = DialogResult.OK;
        }

        private void checkDecimalCvs_CheckedChanged(object sender, EventArgs e)
        {
            labelMaxCvPercent.Visible = labelCvCutoffPercent.Visible = checkBoxDecimalCvs.Checked;

            if (checkBoxDecimalCvs.Checked != _showDecimals)
            {
                double value;
                if (double.TryParse(textCvCutoff.Text, out value))
                    textCvCutoff.Text = (value * (checkBoxDecimalCvs.Checked ? 0.01 : 100.0)).ToString(LocalizationHelper.CurrentCulture);

                if (double.TryParse(textMaximumCv.Text, out value))
                    textMaximumCv.Text = (value * (checkBoxDecimalCvs.Checked ? 0.01 : 100.0)).ToString(LocalizationHelper.CurrentCulture);

                _showDecimals = checkBoxDecimalCvs.Checked;
            }
        }

        #region Functional test support

        public void OK()
        {
            btnOk.PerformClick();
        }

        public bool ShowCVCutoff
        {
            get { return checkShowCvCutoff.Checked; }
            set { checkShowCvCutoff.Checked = value; }
        }

        public bool ShowMedianCV
        {
            get { return checkShowMedian.Checked; }
            set { checkShowMedian.Checked = value; }
        }

        public double QValueCutoff
        {
            get
            {
                double result;
                return double.TryParse(textQValueCutoff.Text, out result) ? result : double.NaN;
            }
            set { textQValueCutoff.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        #endregion
    }
}

