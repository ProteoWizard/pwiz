/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class RefineListDlg : FormEx
    {
        private readonly SrmDocument _document;

        public RefineListDlg(SrmDocument document)
        {
            InitializeComponent();

            _document = document;
        }

        public string[] AcceptedPeptides { get; private set; }
        public bool RemoveEmptyProteins { get; private set; }

        public void OkDialog()
        {
            var reader = new StringReader(textPeptides.Text);
            var invalidLines = new List<string>();
            var notFoundLines = new List<string>();
            var acceptedPeptides = new List<string>();
            var peptideSequences = GetPeptideSequences();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;
                if (!FastaSequence.IsExSequence(line))
                    invalidLines.Add(line);
                else if (!peptideSequences.Contains(line))
                    notFoundLines.Add(line);
                else
                    acceptedPeptides.Add(line);
            }

            if (invalidLines.Count > 0)
            {
                if (invalidLines.Count == 1)
                    MessageBox.Show(this, string.Format("The sequence '{0}' is not a valid peptide.", invalidLines[0]), Program.Name);
                else
                    MessageBox.Show(this, string.Format("The following sequences are not valid peptides:\n\n{0}",
                                                  string.Join("\n", invalidLines.ToArray())), Program.Name);
                return;
            }
            if (acceptedPeptides.Count == 0)
            {
                MessageBox.Show(this, "None of the specified peptides are in the document.");
                return;
            }
            if (notFoundLines.Count > 0)
            {
                string message;
                if (notFoundLines.Count == 1)
                {
                    message = string.Format("The peptide '{0}' is not in the document. Do you want to continue?", notFoundLines[0]);
                }
                else if (notFoundLines.Count < 15)
                {
                    message = string.Format("The following peptides are not in the document:\n\n{0}\n\nDo you want to continue?",
                                            string.Join("\n", notFoundLines.ToArray()));
                }
                else
                {
                    message = string.Format("Of the specified {0} peptides {1} are not in the document.  Do you want to continue?",
                                            notFoundLines.Count + acceptedPeptides.Count, notFoundLines.Count);
                }
                if (MessageBox.Show(this, message, Program.Name, MessageBoxButtons.OKCancel) != DialogResult.OK)
                    return;
            }

            AcceptedPeptides = acceptedPeptides.ToArray();
            RemoveEmptyProteins = cbRemoveProteins.Checked;
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private HashSet<string> GetPeptideSequences()
        {
            var peptideSequences = new HashSet<string>();
            foreach (var nodePep in _document.Peptides)
                peptideSequences.Add(nodePep.Peptide.Sequence);
            return peptideSequences;
        }
    }
}
