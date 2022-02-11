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
using System.Globalization;
using System.Linq;
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

            comboDotpDisplayType.Items.AddRange( DotProductDisplayOptionExtension.ListAll().Select(op => op.GetLocalizedString()).ToArray());
            comboDotpDisplayType.SelectedItem = DotProductDisplayOptionExtension.GetCurrent(Settings.Default).GetLocalizedString();

            cbDecimalCvs.Checked = Settings.Default.PeakDecimalCv;
            if (Settings.Default.PeakAreaMaxArea != 0)
                textMaxArea.Text = Settings.Default.PeakAreaMaxArea.ToString(LocalizationHelper.CurrentCulture);
            if (Settings.Default.PeakAreaMaxCv != 0)
                textMaxCv.Text = Settings.Default.PeakAreaMaxCv.ToString(LocalizationHelper.CurrentCulture);
            GraphFontSize.PopulateCombo(textSizeComboBox, Settings.Default.AreaFontSize);
            cbShowDotpCutoff.Checked = Settings.Default.PeakAreaDotpCutoffShow;
            SetDisplayType();

            foreach (var expectedValue in new[]
                {AreaExpectedValue.library, AreaExpectedValue.isotope_dist, AreaExpectedValue.ratio_to_label})
            {
                var rowIndex = dataGridDotpCutoffValues.Rows.Add(
                    new[] { expectedValue.GetDotpLabel(), expectedValue.GetDotpValueCutoff(Settings.Default).ToString(LocalizationHelper.CurrentCulture)});
                dataGridDotpCutoffValues.Rows[rowIndex].Tag = expectedValue;
            }

            dataGridDotpCutoffValues.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridDotpCutoffValues.AutoResizeColumns();
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

            Settings.Default.PeakAreaDotpDisplay =
                DotProductDisplayOptionExtension.ParseLocalizedString(comboDotpDisplayType.SelectedItem as string).ToString();

            Settings.Default.PeakAreaDotpCutoffShow = cbShowDotpCutoff.Checked;
            if (cbShowDotpCutoff.Checked)
            {
                var isError = false;
                foreach (DataGridViewRow row in dataGridDotpCutoffValues.Rows)
                {
                    try
                    {
                        var val = float.Parse(row.Cells[1].Value.ToString(), LocalizationHelper.CurrentCulture);
                        ((AreaExpectedValue)(row.Tag??AreaExpectedValue.none)).SetDotpValueCutoff(Settings.Default, val);
                        row.ErrorText = null;
                    }
                    catch (FormatException)
                    {
                        row.ErrorText =
                            string.Format(CultureInfo.CurrentCulture, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, row.Cells[0].Value);
                        isError = true;
                    }
                    catch (AssumptionException ex)
                    {
                        row.ErrorText = ex.Message;
                        isError = true;
                    }
                }
                if (isError)
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
        private void cbShowDotpCutoff_CheckedChanged(object sender, EventArgs e)
        {
            
            dataGridDotpCutoffValues.Enabled = label4.Enabled = cbShowDotpCutoff.Checked;
        }

        private void comboDotpDisplayType_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetDisplayType();
        }

        private void SetDisplayType()
        {
            var enableCutoff =
                DotProductDisplayOption.line.GetLocalizedString().Equals(comboDotpDisplayType.SelectedItem);
            cbShowDotpCutoff.Enabled = dataGridDotpCutoffValues.Enabled = label4.Enabled = enableCutoff;
        }

        #region Test Support

        public void SetDotpDisplayProperty(DotProductDisplayOption displayType)
        {
            comboDotpDisplayType.SelectedItem = displayType.GetLocalizedString();
        }

        public void SetShowCutoffProperty(bool showCutoff)
        {
            cbShowDotpCutoff.Checked = showCutoff;
        }

        public void SetDotpCutoffValue(AreaExpectedValue dotpType, string stringValue)
        {
            var dotpRow = dataGridDotpCutoffValues.Rows.OfType<DataGridViewRow>()
                .First(row => dotpType.GetDotpLabel().Equals(row.Cells[0].Value));
            dotpRow.Cells[1].Value = stringValue;
        }

        public string GetRdotpErrorText()
        {
            var dotpRow = dataGridDotpCutoffValues.Rows.OfType<DataGridViewRow>()
                .First(row => @"rdotp".Equals(row.Cells[0].Value));
            return dotpRow.ErrorText;
        }
        #endregion

    }
}
