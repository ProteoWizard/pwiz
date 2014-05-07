using System;
using System.Globalization;
using System.Windows.Forms;
using SProCoP.Properties;

namespace SProCoP
{
    // ReSharper disable InconsistentNaming
    public partial class SProCoPUI : Form
    // ReSharper restore InconsistentNaming
    {
        

        public string[] Arguments { get; private set; }

        public SProCoPUI(string[] oldArguments)
        {
            Arguments = oldArguments;
            InitializeComponent();
        }

       

        private void submitButton_Click(object sender, System.EventArgs e)
        {
            OKDialog();
        }

        private void OKDialog()
        {
            if (VerifyArguments())
            {
                GenerateArguments();
                DialogResult = DialogResult.OK;
            }
        }

        private bool VerifyArguments()
        {
            decimal mma;
            if (!Decimal.TryParse(textBoxMMAValue.Text, NumberStyles.Number, CultureInfo.InvariantCulture.NumberFormat, out mma))
            {
                MessageBox.Show(Resources.SProCoPUI_VerifyArguments_MMA_Value_must_be_a_numeric_value_);
                textBoxMMAValue.Focus();
                return false;
            }
            return true;
        }

        public void GenerateArguments()
        {
            Arguments = new string[Constants.ARGUMENT_COUNT];
            Arguments[(int)ArgumentIndices.qc_runs] = qcRunsNumUpDown.Value.ToString(CultureInfo.InvariantCulture);
            Arguments[(int)ArgumentIndices.hq_ms] = cBoxHighResolution.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int)ArgumentIndices.create_meta] = cboxMetaFile.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int)ArgumentIndices.mma_value] = textBoxMMAValue.Text.ToString(CultureInfo.InvariantCulture);
        }

        private void cBoxHighResolution_CheckedChanged(object sender, System.EventArgs e)
        {
            textBoxMMAValue.Enabled = cBoxHighResolution.Checked;
            if (labelMMAValue.ForeColor == System.Drawing.Color.DimGray)
            {
                labelMMAValue.ForeColor = System.Drawing.Color.Black;
                labelUnitPPM.ForeColor = System.Drawing.Color.Black;
            }
            else
            {
                labelMMAValue.ForeColor = System.Drawing.Color.DimGray;
                labelUnitPPM.ForeColor = System.Drawing.Color.DimGray; 
            }
            
        }

    }

    public class ArgCollector
    {
        public static string[] CollectArgs(IWin32Window parent, string report, string[] oldArgs)
        {
              
            using (var dlg = new SProCoPUI(oldArgs))
            {
                if (parent != null)
                {
                    return (dlg.ShowDialog(parent) == DialogResult.OK) ? dlg.Arguments : null;
                }
                else
                {
                    dlg.StartPosition = FormStartPosition.WindowsDefaultLocation;
                    return (dlg.ShowDialog() == DialogResult.OK) ? dlg.Arguments : null;
                }
            }
        }
    }
  
}
