using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
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
            tbxFileMruLength.Text = Settings.Default.MruLength.ToString();
            tbxChromatogramLineWidth.Text = Settings.Default.ChromatogramLineWidth.ToString();
        }

        private void SaveSettings()
        {
            Settings.Default.Reload();
            Settings.Default.PeaksAsHorizontalLines = cbxPeaksHorizontalLines.Checked;
            Settings.Default.PeaksAsVerticalLines = cbxPeaksVerticalLines.Checked;
            Settings.Default.ShowChromatogramScore = cbxShowDeconvolutionScore.Checked;
            Settings.Default.SmoothChromatograms = cbxSmoothChromatograms.Checked;
            Settings.Default.MruLength = int.Parse(tbxFileMruLength.Text);
            Settings.Default.ChromatogramLineWidth = float.Parse(tbxChromatogramLineWidth.Text);
            Settings.Default.Save();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            SaveSettings();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
