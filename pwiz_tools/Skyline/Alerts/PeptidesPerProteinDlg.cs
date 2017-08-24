/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class PeptidesPerProteinDlg : FormEx
    {
        public bool KeepAll
        {
            get { return radioKeepAll.Checked; }
            set
            {
                if (value)
                    radioKeepAll.Checked = true;
                else
                    radioKeepMinPeptides.Checked = true;
                UpdateRemaining(null, null);
            }
        }

        public int MinPeptides
        {
            get { return KeepAll ? 0 : Convert.ToInt32(numMinPeptides.Value); }
            set
            {
                if (value == 0)
                {
                    KeepAll = true;
                }
                else
                {
                    KeepAll = false;
                    numMinPeptides.Value = value;
                }
                UpdateRemaining(null, null);
            }
        }

        public bool DuplicateControlsVisible { get { return panelDuplicates.Visible; } }

        public bool RemoveRepeatedPeptides
        {
            get { return DuplicateControlsVisible && cbRemoveRepeated.Checked; }
            set
            {
                cbRemoveRepeated.Checked = value;
                UpdateRemaining(null, null);
            }
        }

        public bool RemoveDuplicatePeptides
        {
            get { return DuplicateControlsVisible && cbRemoveDuplicate.Checked; }
            set
            {
                cbRemoveDuplicate.Checked = value; 
                UpdateRemaining(null, null);
            }
        }

        private readonly SrmDocument _document;
        private readonly List<PeptideGroupDocNode> _addedPeptideGroups;

        public SrmDocument DocumentFinal { get; private set; }

        public int EmptyProteins { get { return DocumentFinal.PeptideGroups.Count(pepGroup => pepGroup.PeptideCount == 0); } }

        private readonly string _decoyGenerationMethod;
        private readonly double _decoysPerTarget;

        private readonly string _remaniningText;
        private readonly string _emptyProteinsText;

        public PeptidesPerProteinDlg(SrmDocument doc, List<PeptideGroupDocNode> addedPeptideGroups, string decoyGenerationMethod, double decoysPerTarget)
        {
            InitializeComponent();
            _document = doc;
            _addedPeptideGroups = addedPeptideGroups;
            _decoyGenerationMethod = decoyGenerationMethod;
            _decoysPerTarget = decoysPerTarget;
            _remaniningText = lblRemaining.Text;
            _emptyProteinsText = lblEmptyProteins.Text;
            int proteinCount, peptideCount, precursorCount, transitionCount;
            NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
            lblNew.Text = string.Format(lblNew.Text, proteinCount, peptideCount, precursorCount, transitionCount);

            var docRefined = new RefinementSettings {RemoveDuplicatePeptides = true}.Refine(_document);
            if (_document.PeptideCount == docRefined.PeptideCount)
            {
                Height -= panelRemaining.Top - panelDuplicates.Top;
                panelDuplicates.Hide();
                panelRemaining.Top = panelDuplicates.Top;
            }

            UpdateRemaining(null, null);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        public void NewTargetsAll(out int proteins, out int peptides, out int precursors, out int transitions)
        {
            var numDecoys = NumDecoys(_addedPeptideGroups);
            var pepGroups = _addedPeptideGroups.ToArray();
            if (numDecoys > 0)
            {
                var decoyGroups = AddDecoys(_document).PeptideGroups.Where(pepGroup => Equals(pepGroup.Name, PeptideGroup.DECOYS));
                pepGroups = pepGroups.Concat(decoyGroups).ToArray();
            }
            proteins = pepGroups.Length;
            peptides = pepGroups.Sum(pepGroup => pepGroup.PeptideCount);
            precursors = pepGroups.Sum(pepGroup => pepGroup.TransitionGroupCount);
            transitions = pepGroups.Sum(pepGroup => pepGroup.TransitionCount);
        }

        public void NewTargetsFinal(out int proteins, out int peptides, out int precursors, out int transitions)
        {
            var pepGroups = DocumentFinal.PeptideGroups.ToArray();
            proteins = pepGroups.Length;
            peptides = pepGroups.Sum(pepGroup => pepGroup.PeptideCount);
            precursors = pepGroups.Sum(pepGroup => pepGroup.TransitionGroupCount);
            transitions = pepGroups.Sum(pepGroup => pepGroup.TransitionCount);
        }

        private int NumDecoys(IEnumerable<PeptideGroupDocNode> pepGroups)
        {
            return !string.IsNullOrEmpty(_decoyGenerationMethod) && _decoysPerTarget > 0
                ? (int) Math.Round(pepGroups.Sum(pepGroup => pepGroup.PeptideCount) * _decoysPerTarget)
                : 0;
        }

        private SrmDocument AddDecoys(SrmDocument document)
        {
            var numDecoys = NumDecoys(document.PeptideGroups);
            return numDecoys > 0
                ? new RefinementSettings { DecoysMethod = _decoyGenerationMethod, NumberOfDecoys = numDecoys }.GenerateDecoys(document)
                : document;
        }

        private void UpdateRemaining(object sender, EventArgs e)
        {
            DocumentFinal = AddDecoys(ImportPeptideSearch.RemoveProteinsByPeptideCount(
                RemoveRepeatedPeptides || RemoveDuplicatePeptides
                    ? new RefinementSettings {RemoveRepeatedPeptides = RemoveRepeatedPeptides, RemoveDuplicatePeptides = RemoveDuplicatePeptides}.Refine(_document)
                    : _document
                , MinPeptides));

            if (KeepAll)
            {
                numMinPeptides.Enabled = false;
                lblEmptyProteins.Text = string.Format(_emptyProteinsText, EmptyProteins);
                lblEmptyProteins.Show();
            }
            else
            {
                numMinPeptides.Enabled = true;
                lblEmptyProteins.Hide();
            }

            int proteinCount, peptideCount, precursorCount, transitionCount;
            NewTargetsFinal(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
            lblRemaining.Text = string.Format(_remaniningText, proteinCount, peptideCount, precursorCount, transitionCount);
        }
    }
}
