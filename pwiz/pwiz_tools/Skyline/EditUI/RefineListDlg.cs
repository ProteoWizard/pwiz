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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

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

        public RefinementSettings.PeptideCharge[] AcceptedPeptides { get; private set; }

        public bool RemoveEmptyProteins
        {
            get { return cbRemoveProteins.Checked; }
            set { cbRemoveProteins.Checked = value; }            
        }

        public bool MatchModified
        {
            get { return cbMatchModified.Checked; }
            set { cbMatchModified.Checked = value; }
        }

        public string PeptidesText
        {
            get { return textPeptides.Text; }
            set { textPeptides.Text = value; }
        }

        public void OkDialog()
        {
            var reader = new StringReader(PeptidesText);
            var invalidLines = new List<string>();
            var notFoundLines = new List<string>();
            var acceptedPeptides = new List<RefinementSettings.PeptideCharge>();
            var peptideSequences = GetPeptideSequences();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;
                int? charge = null;
                int chargeIndex = line.IndexOf('+');
                if (chargeIndex != -1)
                {
                    charge = Transition.GetChargeFromIndicator(line,
                        TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
                    if (charge.HasValue)
                        line = line.Substring(0, line.Length - Transition.GetChargeIndicator(charge.Value).Length);
                }
                if (!FastaSequence.IsExSequence(line))
                {
                    invalidLines.Add(line);
                    continue;
                }
                try
                {
                    line = SequenceMassCalc.NormalizeModifiedSequence(line);
                }
                catch (Exception)
                {
                    invalidLines.Add(line);
                    continue;
                }

                if (!peptideSequences.Contains(line))
                    notFoundLines.Add(line);
                else
                    acceptedPeptides.Add(new RefinementSettings.PeptideCharge(line, charge));
            }

            if (invalidLines.Count > 0)
            {
                if (invalidLines.Count == 1)
                    MessageDlg.Show(this, string.Format(Resources.RefineListDlg_OkDialog_The_sequence__0__is_not_a_valid_peptide, invalidLines[0]));
                else
                    MessageDlg.Show(this, TextUtil.LineSeparate(Resources.RefineListDlg_OkDialog_The_following_sequences_are_not_valid_peptides,string.Empty, TextUtil.LineSeparate(invalidLines)));
                return;
            }
            if (acceptedPeptides.Count == 0)
            {
                MessageDlg.Show(this, Resources.RefineListDlg_OkDialog_None_of_the_specified_peptides_are_in_the_document);
                return;
            }
            if (notFoundLines.Count > 0)
            {
                string message;
                if (notFoundLines.Count == 1)
                {
                    message = string.Format(Resources.RefineListDlg_OkDialog_The_peptide__0__is_not_in_the_document_Do_you_want_to_continue, notFoundLines[0]);
                }
                else if (notFoundLines.Count < 15)
                {
                    message = TextUtil.LineSeparate(Resources.RefineListDlg_OkDialog_The_following_peptides_are_not_in_the_document, string.Empty,
                                                    TextUtil.LineSeparate(notFoundLines),string.Empty,
                                                    Resources.RefineListDlg_OkDialog_Do_you_want_to_continue);
                }
                else
                {
                    message = string.Format(Resources.RefineListDlg_OkDialog_Of_the_specified__0__peptides__1__are_not_in_the_document_Do_you_want_to_continue,
                                            notFoundLines.Count + acceptedPeptides.Count, notFoundLines.Count);
                }
                if (MultiButtonMsgDlg.Show(this, message, MultiButtonMsgDlg.BUTTON_OK) != DialogResult.OK)
                    return;
            }

            AcceptedPeptides = acceptedPeptides.ToArray();
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
            {
                peptideSequences.Add(MatchModified ? nodePep.ModifiedSequence : nodePep.Peptide.Sequence);
            }
            return peptideSequences;
        }

        private void textPeptides_Enter(object sender, EventArgs e)
        {
            AcceptButton = null;
        }

        private void textPeptides_Leave(object sender, EventArgs e)
        {
            AcceptButton = btnOk;
        }
    }
}
