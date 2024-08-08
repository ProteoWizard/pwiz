/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using Settings = pwiz.Skyline.Controls.Graphs.DetectionsGraphController.Settings;
using IntLabeledValue = pwiz.Skyline.Controls.Graphs.DetectionsGraphController.IntLabeledValue;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class DetectionToolbarProperties : FormEx
    {
        private readonly GraphSummary _graphSummary;

        public DetectionToolbarProperties(GraphSummary graphSummary)
        {
            InitializeComponent();
            _graphSummary = graphSummary;
        }

        private void DetectionToolbarProperties_Load(object sender, EventArgs e)
        {
            IntLabeledValue.PopulateCombo(cmbTargetType, Settings.TargetType);
            IntLabeledValue.PopulateCombo(cmbCountMultiple, Settings.YScaleFactor);

            txtQValueCustom.Text = Settings.QValueCutoff.ToString(LocalizationHelper.CurrentCulture);
            switch (Settings.QValueCutoff)
            {
                case 0.01f:
                    rbQValue01.Select();
                    break;
                default:
                    rbQValueCustom.Select();
                    break;
            }

            cbShowAtLeastN.Checked = Settings.ShowAtLeastN;
            cbShowSelection.Checked = Settings.ShowSelection;
            cbShowMeanStd.Checked = Settings.ShowMean;
            cbShowLegend.Checked = Settings.ShowLegend;
            GraphFontSize.PopulateCombo(cmbFontSize, Settings.FontSize);

            if (_graphSummary.DocumentUIContainer.DocumentUI.IsLoaded &&
                _graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults.Chromatograms.Count > 0)
            {
                tbAtLeastN.Maximum = _graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults.Chromatograms.Count;
                if(Settings.RepCount < tbAtLeastN.Maximum && Settings.RepCount > tbAtLeastN.Minimum)
                    tbAtLeastN.Value = Settings.RepCount;
                else
                    tbAtLeastN.Value = tbAtLeastN.Maximum / 2;
            }
            cmbTargetType.Focus();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            if (rbQValue01.Checked)
                Settings.QValueCutoff = 0.01f;
            if (rbQValueCustom.Checked)
            {
                var qValueCutoff = double.NaN;
                if (!string.IsNullOrEmpty(txtQValueCustom.Text)
                    && !helper.ValidateDecimalTextBox(txtQValueCustom, 0, 1, out qValueCutoff))
                    return;
                else
                    Settings.QValueCutoff = (float)qValueCutoff;
            }

            Settings.YScaleFactor = IntLabeledValue.GetValue(cmbCountMultiple, Settings.YScaleFactor);
            Settings.TargetType = IntLabeledValue.GetValue(cmbTargetType, Settings.TargetType);

            Settings.ShowAtLeastN = cbShowAtLeastN.Checked;
            Settings.ShowSelection = cbShowSelection.Checked;
            Settings.ShowMean = cbShowMeanStd.Checked;
            Settings.ShowLegend = cbShowLegend.Checked;

            Settings.RepCount = tbAtLeastN.Value;
            Settings.FontSize = GraphFontSize.GetFontSize(cmbFontSize).PointSize;

            DialogResult = DialogResult.OK;
        }

        private void txtQValueCustom_Enter(object sender, EventArgs e)
        {
            rbQValueCustom.Checked = true;
        }

        private void tbAtLeastN_ValueChanged(object sender, EventArgs e)
        {
            gbAtLeastN.Text = String.Format(CultureInfo.CurrentCulture,
                GraphsResources.DetectionToolbarProperties_AtLeastNReplicates, tbAtLeastN.Value);
        }

        private void cmbTargetType_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.TargetType = IntLabeledValue.GetValue(cmbTargetType, Settings.TargetType);
            IntLabeledValue.PopulateCombo(cmbCountMultiple, Settings.YScaleFactor);
        }

        #region Functional test support

        public void SetQValueTo(float qValue)
        {
            if (qValue == .01f)
                rbQValue01.Checked = true;
            else
            {
                rbQValueCustom.Checked = true;
                txtQValueCustom.Text = qValue.ToString(CultureInfo.CurrentCulture);
            }
        }
        #endregion
    }
}
