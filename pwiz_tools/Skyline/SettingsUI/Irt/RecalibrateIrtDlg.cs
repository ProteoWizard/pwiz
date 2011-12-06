/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class RecalibrateIrtDlg : Form
    {
        private readonly IEnumerable<DbIrtPeptide> _irtPeptides;
        private readonly IDictionary<string, DbIrtPeptide> _dictSequenceToPeptide;

        public RecalibrateIrtDlg(IEnumerable<DbIrtPeptide> irtPeptides)
        {
            _irtPeptides = irtPeptides;
            _dictSequenceToPeptide = new Dictionary<string, DbIrtPeptide>();
            foreach (var peptide in irtPeptides)
            {
                if (!_dictSequenceToPeptide.ContainsKey(peptide.PeptideModSeq))
                    _dictSequenceToPeptide.Add(peptide.PeptideModSeq, peptide);
            }

            InitializeComponent();
        }

        public RegressionLine LinearEquation { get; private set; }

        public void OkDialog()
        {
            double minIrt;
            double maxIrt;

            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);
            if (!helper.ValidateDecimalTextBox(e, textMinIrt, null, null, out minIrt))
                return;
            if (!helper.ValidateDecimalTextBox(e, textMaxIrt, minIrt, null, out maxIrt))
                return;

            string pepSeq1 = textFixedPep1.Text;
            DbIrtPeptide peptide1;
            if (!ValidatePeptideSequence(pepSeq1, out peptide1))
            {
                textFixedPep1.Focus();
                return;
            }

            string pepSeq2 = textFixedPep2.Text;
            DbIrtPeptide peptide2;
            if (!ValidatePeptideSequence(pepSeq2, out peptide2))
            {
                textFixedPep2.Focus();
                return;
            }

            double minCurrent = Math.Min(peptide1.Irt, peptide2.Irt);
            double maxCurrent = Math.Max(peptide1.Irt, peptide2.Irt);

            var statX = new Statistics(minCurrent, maxCurrent);
            var statY = new Statistics(minIrt, maxIrt);

            LinearEquation = new RegressionLine(statY.Slope(statX), statY.Intercept(statX));

            // Convert all of the peptides to the new scale.
            foreach (var peptide in _irtPeptides)
            {
                peptide.Irt = LinearEquation.GetY(peptide.Irt);
            }

            DialogResult = DialogResult.OK;
        }

        private bool ValidatePeptideSequence(string sequence, out DbIrtPeptide peptide)
        {
            peptide = null;
            if (!FastaSequence.IsExSequence(sequence))
            {
                MessageDlg.Show(this, string.Format("The text '{0}' is not a valid modified peptide sequence.", sequence));
                return false;
            }

            if (!_dictSequenceToPeptide.TryGetValue(sequence, out peptide))
            {
                MessageDlg.Show(this, string.Format("The sequence '{0}' is not in the iRT database.  Enter an existing sequence for recalibration.", sequence));
                return false;
            }
            return true;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}