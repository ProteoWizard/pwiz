using System;
using System.Globalization;
using System.Windows.Forms;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DisplaySettingsForm : Form
    {
        public DisplaySettingsForm()
        {
            InitializeComponent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Repopulate();
        }

        private void Repopulate()
        {
            Settings.Default.Reload();
            cbxPeaksHorizontalLines.Checked = Settings.Default.PeaksAsHorizontalLines;
            cbxPeaksVerticalLines.Checked = Settings.Default.PeaksAsVerticalLines;
            cbxShowDeconvolutionScore.Checked = Settings.Default.ShowChromatogramScore;
            cbxSmoothChromatograms.Checked = Settings.Default.SmoothChromatograms;
            tbxFileMruLength.Text = Settings.Default.MruLength.ToString(CultureInfo.CurrentCulture);
            tbxChromatogramLineWidth.Text = Settings.Default.ChromatogramLineWidth.ToString(CultureInfo.CurrentCulture);
        }

        private void SaveSettings()
        {
            Settings.Default.Reload();
            Settings.Default.PeaksAsHorizontalLines = cbxPeaksHorizontalLines.Checked;
            Settings.Default.PeaksAsVerticalLines = cbxPeaksVerticalLines.Checked;
            Settings.Default.ShowChromatogramScore = cbxShowDeconvolutionScore.Checked;
            Settings.Default.SmoothChromatograms = cbxSmoothChromatograms.Checked;
            Settings.Default.MruLength = int.Parse(tbxFileMruLength.Text, CultureInfo.InvariantCulture);
            Settings.Default.ChromatogramLineWidth = float.Parse(tbxChromatogramLineWidth.Text, CultureInfo.InvariantCulture);
            Settings.Default.Save();
        }

        private void BtnOkOnClick(object sender, EventArgs e)
        {
            SaveSettings();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
