using System;
using System.Globalization;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using pwiz.Skyline.Properties;
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
                case 0.05f:
                    rbQValue05.Select();
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

            tbAtLeastN.Maximum = _graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults.Chromatograms.Count;
            tbAtLeastN.Value = Settings.RepCount;
            cmbTargetType.Focus();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            var helper = new MessageBoxHelper(this);

            if (rbQValue01.Checked)
                Settings.QValueCutoff = 0.01f;
            if (rbQValue05.Checked)
                Settings.QValueCutoff = 0.05f;
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
                Resources.DetectionToolbarProperties_AtLeastNReplicates, tbAtLeastN.Value);
        }

        private void cmbTargetType_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.TargetType = IntLabeledValue.GetValue(cmbTargetType, Settings.TargetType);
            IntLabeledValue.PopulateCombo(cmbCountMultiple, Settings.YScaleFactor);
        }
    }
}
