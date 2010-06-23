using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Forms.Controls
{
    public partial class AttestationDialog : Form
    {
        public AttestationDialog()
        {
            InitializeComponent();
        }

        private void bigBDResultsLoad_Click(object sender, EventArgs e)
        {
            // Browse to the final assembly file
            OpenFileDialog fdlg = new OpenFileDialog();
            fdlg.Title = "Load Assembly file of secondary results...";
            fdlg.InitialDirectory = @"c:\";
            fdlg.Filter = "Assemble XML (*.idpXML)|*.idpXML|All files (*.*)|*.*";
            fdlg.FilterIndex = 1;
            fdlg.RestoreDirectory = true;
            if (fdlg.ShowDialog() == DialogResult.OK)
            {
                bigDBResultsFile.Text = fdlg.FileName;
                autoAttestResultsCheckBox.Enabled = true;
                deltaScoreThresholdsGroupBox.Enabled = true;
                deltaTICTextBox.Enabled = true;
                deltaXCorrThresholdTextBox.Enabled = true;
                filterByTICRabioButton.Enabled = true;
                filterByXCorrRadioButton.Enabled = true;
                generateNewDeltaMassTableCheckBox.Enabled = true;
                deltaTICTextBox.Enabled = true;
                
            }
            
        }

    }
}
