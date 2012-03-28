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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class ChangeIrtPeptidesDlg : FormEx
    {
        private readonly IDictionary<string, DbIrtPeptide> _dictSequenceToPeptide;
        private IList<DbIrtPeptide> _standardPeptides;

        public ChangeIrtPeptidesDlg(IList<DbIrtPeptide> irtPeptides)
        {
            _dictSequenceToPeptide = new Dictionary<string, DbIrtPeptide>();
            foreach (var peptide in irtPeptides)
            {
                if (!_dictSequenceToPeptide.ContainsKey(peptide.PeptideModSeq))
                    _dictSequenceToPeptide.Add(peptide.PeptideModSeq, peptide);
            }

            InitializeComponent();

            Peptides = irtPeptides.Where(peptide => peptide.Standard).ToArray();
        }

        public IList<DbIrtPeptide> Peptides
        {
            get { return _standardPeptides; }
            set
            {
                _standardPeptides = value;
                textPeptides.Text = string.Join(Environment.NewLine,
                    _standardPeptides.Select(peptide => peptide.PeptideModSeq).ToArray());
            }
        }

        public void OkDialog()
        {
            var reader = new StringReader(textPeptides.Text);
            var standardPeptides = new List<DbIrtPeptide>();
            var invalidLines = new List<string>();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                // Skip blank lines
                if (string.IsNullOrEmpty(line))
                    continue;
                DbIrtPeptide peptide;
                if (!_dictSequenceToPeptide.TryGetValue(line, out peptide))
                    invalidLines.Add(line);
                standardPeptides.Add(peptide);
            }

            if (invalidLines.Count > 0)
            {
                if (invalidLines.Count == 1)
                    MessageBox.Show(this, string.Format("The sequence '{0}' is not currently in the database.\nStandard peptides must exist in the database.", invalidLines[0]), Program.Name);
                else
                    MessageBox.Show(this, string.Format("The following sequences are not currently in the database:\n\n{0}\n\nStandard peptides must exist in the database.",
                                                  string.Join("\n", invalidLines.ToArray())), Program.Name);
                return;
            }

            _standardPeptides = standardPeptides.ToArray();

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}