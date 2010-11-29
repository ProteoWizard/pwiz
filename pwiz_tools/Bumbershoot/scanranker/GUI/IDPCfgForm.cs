//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Initial Developer of the DirecTag peptide sequence tagger is Matt Chambers.
// Contributor(s): Surendra Dasaris
//
// The Initial Developer of the ScanRanker GUI is Zeqiang Ma.
// Contributor(s): 
//
// Copyright 2009 Vanderbilt University
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ScanRanker
{
    public partial class IDPickerConfigForm : Form
    {
        private IDPickerInfo idpCfg;
        public IDPickerInfo IdpCfg
        {
            get { return idpCfg; }
            set { idpCfg = value; }
        }

        public IDPickerConfigForm()
        {
            InitializeComponent();
        }

        private void IDPickerConfigForm_Load(object sender, EventArgs e)
        {
            tbPepXMLFile.Text = string.Empty;
            tbDBfile.Text = string.Empty;
            tbDecoyPrefix.Text = "rev_";
            tbMaxFDR.Text = "0.02";
            cmbScoreWeights.SelectedIndex = 0;
            cbNormSearchScores.Checked = true;
            cbOptimizeScoreWeights.Checked = true;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                string selFile = Workspace.OpenFileBrowseDialog(tbPepXMLFile.Text);
                if (!selFile.Equals(string.Empty))
                {
                    tbPepXMLFile.Text = selFile;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
            }
            if( tbPepXMLFile.Text.Equals(string.Empty))
            {
                MessageBox.Show("Please select a pepXML file!");
                return;
            }
        }

        private void btnBrowseDB_Click(object sender, EventArgs e)
        {
            try
            {
                string selFile = Workspace.OpenFileBrowseDialog(tbDBfile.Text);
                if (!selFile.Equals(string.Empty))
                {
                    tbDBfile.Text = selFile;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
            }
            if ( tbDBfile.Text.Equals(string.Empty) )
            {
                MessageBox.Show("Please select a database file!");
                return;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }


        private void btnOK_Click(object sender, EventArgs e)
        {
            //Error check
            if (tbPepXMLFile.Text.Equals(string.Empty) || tbDBfile.Text.Equals(string.Empty)
                || tbDecoyPrefix.Text.Equals(string.Empty) || tbMaxFDR.Text.Equals(string.Empty)
                || cmbScoreWeights.Text.Equals(string.Empty))
            {
                MessageBox.Show("Please specify all required parameters");
                return;
            }
            double fdr = Convert.ToDouble(tbMaxFDR.Text);
            if (fdr <= 0 || fdr >= 1)
            {
                MessageBox.Show("Please input proper FDR between 0 and 1");
                return;
            }

            idpCfg = new IDPickerInfo();
            idpCfg.PepXMLFile = tbPepXMLFile.Text;
            idpCfg.DBFile = tbDBfile.Text;
            idpCfg.DecoyPrefix = tbDecoyPrefix.Text;
            idpCfg.MaxFDR = fdr;
            idpCfg.ScoreWeights = cmbScoreWeights.Text;
            idpCfg.NormalizeSearchScores = (cbNormSearchScores.Checked) ? 1 : 0;
            idpCfg.OptimizeScoreWeights = (cbOptimizeScoreWeights.Checked) ? 1 : 0;

            DialogResult = DialogResult.OK;
            Close();
        }


    }
}
