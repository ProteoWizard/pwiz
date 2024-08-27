using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs.Calibration
{
    public partial class CalibrationCurveOptionsDlg : FormEx
    {
        public CalibrationCurveOptionsDlg()
        {
            InitializeComponent();
            checkedListBoxSampleTypes.Items.AddRange(SampleType.ALL.ToArray());
            var options = Properties.Settings.Default.CalibrationCurveOptions;
            textLineWidth.Text = options.LineWidth.ToString(CultureInfo.CurrentCulture);
            GraphFontSize.PopulateCombo(textSizeComboBox, options.FontSize);
            cbxLogXAxis.Checked = options.LogXAxis;
            cbxLogYAxis.Checked = options.LogYAxis;
            DisplaySampleTypes = options.DisplaySampleTypes;

            cbxSingleBatch.Checked = options.SingleBatch;
            cbxShowLegend.Checked = options.ShowLegend;
            cbxShowFiguresOfMerit.Checked = options.ShowFiguresOfMerit;
            cbxShowBootstrapCurves.Checked = options.ShowBootstrapCurves;
        }

        public void OkDialog()
        {
            var options = Properties.Settings.Default.CalibrationCurveOptions;
            options = options.ChangeLineWidth((float)textLineWidth.Value)
                .ChangeFontSize(GraphFontSize.GetFontSize(textSizeComboBox).PointSize)
                .ChangeLogXAxis(cbxLogXAxis.Checked)
                .ChangeLogYAxis(cbxLogYAxis.Checked)
                .ChangeDisplaySampleTypes(checkedListBoxSampleTypes.CheckedItems.OfType<SampleType>())
                .ChangeSingleBatch(cbxSingleBatch.Checked)
                .ChangeShowLegend(cbxShowLegend.Checked)
                .ChangeShowFiguresOfMerit(cbxShowFiguresOfMerit.Checked)
                .ChangeShowBootstrapCurves(cbxShowBootstrapCurves.Checked);
            Properties.Settings.Default.CalibrationCurveOptions = options;
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public IEnumerable<SampleType> DisplaySampleTypes
        {
            get
            {
                return checkedListBoxSampleTypes.CheckedItems.OfType<SampleType>();
            }
            set
            {
                var sampleTypeSet = value.ToHashSet();
                for (int i = 0; i < checkedListBoxSampleTypes.Items.Count; i++)
                {
                    checkedListBoxSampleTypes.SetItemChecked(i, sampleTypeSet.Contains(checkedListBoxSampleTypes.Items[i]));
                }
            }
        }
    }
}
