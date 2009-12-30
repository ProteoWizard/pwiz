using System.ComponentModel;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.EditUI
{
    public partial class RefineDlg : Form
    {
        public RefineDlg(SrmDocument document)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            var settings = document.Settings;
            tabResults.Visible = settings.HasResults;
            if (settings.PeptideSettings.Libraries.HasLibraries)
            {
                groupLibCorr.Enabled = true;
                labelMinDotProduct.Enabled = true;
                textMinDotProduct.Enabled = true;
            }
        }

        public RefinementSettings RefinementSettings { get; private set; }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            int? minPeptidesPerProtein = null;
            if (!string.IsNullOrEmpty(textMinPeptides.Text))
            {
                int minVal;
                if (!helper.ValidateNumberTextBox(e, tabControl1, 0, textMinPeptides, 0, 10, out minVal))
                    return;
                minPeptidesPerProtein = minVal;
            }
            int? minTransitionsPerPrecursor = null;
            if (!string.IsNullOrEmpty(textMinTransitions.Text))
            {
                int minVal;
                if (!helper.ValidateNumberTextBox(e, tabControl1, 0, textMinTransitions, 0, 100, out minVal))
                    return;
                minTransitionsPerPrecursor = minVal;
            }
            bool removeDuplicatePeptides = cbRemoveDuplicatePeptides.Checked;
            bool removeRepeatedPeptides = cbRemoveRepeatedPeptides.Checked;
            bool removeHeavyWithLight = cbRemoveHeavy.Checked;

            double? minPeakFoundRatio = null, maxPeakFoundRatio = null;
            if (!string.IsNullOrEmpty(textMinPeakFoundRatio.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, 1, textMinPeakFoundRatio, 0, 1, out minVal))
                    return;
                minPeakFoundRatio = minVal;
            }
            if (!string.IsNullOrEmpty(textMaxPeakFoundRatio.Text))
            {
                double maxVal;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, 1, textMaxPeakFoundRatio, 0, 1, out maxVal))
                    return;
                maxPeakFoundRatio = maxVal;
            }
            if (minPeakFoundRatio.HasValue && maxPeakFoundRatio.HasValue &&
                    minPeakFoundRatio.Value > maxPeakFoundRatio.Value)
            {
                helper.ShowTextBoxError(textMaxPeakFoundRatio, "{0} must be less than min peak found ratio.");
                return;
            }

            bool removeMissingResults = radioRemoveMissing.Checked;

            double? rtRegressionThreshold = null;
            if (!string.IsNullOrEmpty(textRTRegressionThreshold.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, 1, textRTRegressionThreshold, 0, 1, out minVal))
                    return;
                rtRegressionThreshold = minVal;
            }

            double? dotProductThreshold = null;
            if (!string.IsNullOrEmpty(textMinDotProduct.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, 1, textMinDotProduct, 0, 1, out minVal))
                    return;
                dotProductThreshold = minVal;
            }

            RefinementSettings = new RefinementSettings(minPeptidesPerProtein,
                                                        removeDuplicatePeptides,
                                                        removeRepeatedPeptides,
                                                        minTransitionsPerPrecursor,
                                                        removeHeavyWithLight,
                                                        minPeakFoundRatio,
                                                        maxPeakFoundRatio,
                                                        removeMissingResults,
                                                        rtRegressionThreshold,
                                                        dotProductThreshold);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnOK_Click(object sender, System.EventArgs e)
        {
            OkDialog();
        }
    }
}
