using System.Globalization;
using System.Windows.Forms;

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
            return true;
        }

        public void GenerateArguments()
        {
            Arguments = new string[Constants.ARGUMENT_COUNT];
            Arguments[(int)ArgumentIndices.qc_runs] = qcRunsNumUpDown.Value.ToString(CultureInfo.InvariantCulture);
            Arguments[(int)ArgumentIndices.hq_ms] = cBoxHighResolution.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int)ArgumentIndices.create_meta] = cboxMetaFile.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
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
