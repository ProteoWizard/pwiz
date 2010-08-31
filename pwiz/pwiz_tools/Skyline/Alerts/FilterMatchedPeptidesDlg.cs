/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.SettingsUI;

namespace pwiz.Skyline.Alerts
{
    public sealed partial class FilterMatchedPeptidesDlg : Form
    {
        public FilterMatchedPeptidesDlg(int numWithDuplicates, bool single)
        {
            InitializeComponent();

            Text = Program.Name;
            msg.Text = string.Format("{0} peptide{1} found in multiple protein sequences.\n"
                + "How would you like to handle {2}?", numWithDuplicates > 1 ? numWithDuplicates.ToString() : (single ? "This" : "A"),
                                numWithDuplicates > 1 ? "s were" : " was", 
                                numWithDuplicates > 1 ? "these peptides" : "the peptide");
        }

        public ViewLibraryPepMatching.DuplicateProteinsFilter PeptidesDuplicateProteins
        {
            get; private set;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (btnNoDuplicates.Checked)
                PeptidesDuplicateProteins = ViewLibraryPepMatching.DuplicateProteinsFilter.NoDuplicates;
            else if (btnFirstOccurence.Checked)
                PeptidesDuplicateProteins = ViewLibraryPepMatching.DuplicateProteinsFilter.FirstOccurence;
            else
                PeptidesDuplicateProteins = ViewLibraryPepMatching.DuplicateProteinsFilter.AddToAll;

            DialogResult = DialogResult.OK;
        }
    }
}
