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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class PeptidesPerProteinDlg : FormEx
    {
        public int MinPeptides
        {
            get { return radioKeepMinPeptides.Checked ? Convert.ToInt32(numMinPeptides.Value) : 0; }
            set { numMinPeptides.Value = value; }
        }

        private SrmDocument DocumentFiltered { get { return ImportPeptideSearch.RemoveProteinsByPeptideCount(_document, MinPeptides); } }

        public SrmDocument DocumentFinal
        {
            get
            {
                var refinement = Refinement;
                return refinement != null ? refinement.GenerateDecoys(DocumentFiltered) : DocumentFiltered;
            }
        }

        private RefinementSettings Refinement
        {
            get
            {
                var numDecoys = NumDecoys(FilteredPeptideGroups);
                return numDecoys > 0
                    ? new RefinementSettings {DecoysMethod = _decoyGenerationMethod, NumberOfDecoys = numDecoys}
                    : null;
            }
        }

        public bool KeepAll
        {
            get
            {
                return radioKeepAll.Checked;
            }
            set
            {
                if (value)
                    radioKeepAll.Checked = true;
                else
                    radioKeepMinPeptides.Checked = true;
                UpdateRemaining(null, null);
            }
        }

        private readonly SrmDocument _document;
        private readonly List<PeptideGroupDocNode> _addedPeptideGroups;

        private IEnumerable<PeptideGroupDocNode> FilteredPeptideGroups { get { return DocumentFiltered.PeptideGroups.Intersect(_addedPeptideGroups); } }
        public IEnumerable<PeptideGroupDocNode> FilteredPeptideGroupsWithDecoys { get { return FilteredPeptideGroups.Concat(DocumentFinal.PeptideGroups.Where(pepGroup => Equals(pepGroup.Name, PeptideGroup.DECOYS))); } }

        public int EmptyProteins { get { return _addedPeptideGroups.Count(pepGroup => pepGroup.PeptideCount == 0); } }

        private readonly string _decoyGenerationMethod;
        private readonly double _decoysPerTarget;

        private readonly string _remainingHeader;
        private readonly string _remaniningText;
        private readonly Color _remainingColor;

        public PeptidesPerProteinDlg(SrmDocument doc, List<PeptideGroupDocNode> addedPeptideGroups, string decoyGenerationMethod, double decoysPerTarget)
        {
            InitializeComponent();
            _document = doc;
            _addedPeptideGroups = addedPeptideGroups;
            _decoyGenerationMethod = decoyGenerationMethod;
            _decoysPerTarget = decoysPerTarget;
            _remainingHeader = lblRemainingHeader.Text;
            _remaniningText = lblRemaining.Text;
            _remainingColor = lblRemaining.ForeColor;
            int proteinCount, peptideCount, precursorCount, transitionCount;
            NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
            lblNew.Text = string.Format(lblNew.Text, proteinCount, peptideCount, precursorCount, transitionCount);
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
                var docWithDecoys = new RefinementSettings { DecoysMethod = _decoyGenerationMethod, NumberOfDecoys = numDecoys }.GenerateDecoys(_document);
                var decoyGroups = docWithDecoys.PeptideGroups.Where(pepGroup => Equals(pepGroup.Name, PeptideGroup.DECOYS));
                pepGroups = pepGroups.Concat(decoyGroups).ToArray();
            }
            proteins = pepGroups.Length;
            peptides = pepGroups.Sum(pepGroup => pepGroup.PeptideCount);
            precursors = pepGroups.Sum(pepGroup => pepGroup.TransitionGroupCount);
            transitions = pepGroups.Sum(pepGroup => pepGroup.TransitionCount);
        }

        public void NewTargetsFiltered(out int proteins, out int peptides, out int precursors, out int transitions)
        {
            var pepGroups = FilteredPeptideGroupsWithDecoys.ToArray();
            proteins = pepGroups.Length;
            peptides = pepGroups.Sum(pepGroup => pepGroup.PeptideCount);
            precursors = pepGroups.Sum(pepGroup => pepGroup.TransitionGroupCount);
            transitions = pepGroups.Sum(pepGroup => pepGroup.TransitionCount);
        }

        private int NumDecoys(IEnumerable<PeptideGroupDocNode> pepGroups)
        {
            return !string.IsNullOrEmpty(_decoyGenerationMethod) && _decoysPerTarget > 0 ? (int) Math.Round(pepGroups.Sum(pepGroup => pepGroup.PeptideCount) * _decoysPerTarget) : 0;
        }

        private void UpdateRemaining(object sender, EventArgs e)
        {
            if (KeepAll)
            {
                numMinPeptides.Enabled = false;

                lblRemainingHeader.Text = string.Format(Resources.PeptidesPerProteinDlg_UpdateRemaining__0__empty_proteins_will_be_added_, EmptyProteins);
                lblRemainingHeader.ForeColor = Color.Red;

                lblRemaining.Hide();
            }
            else
            {
                numMinPeptides.Enabled = true;

                lblRemainingHeader.Text = _remainingHeader;
                lblRemainingHeader.ForeColor = _remainingColor;

                lblRemaining.Show();

                int proteinCount, peptideCount, precursorCount, transitionCount;
                NewTargetsFiltered(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                lblRemaining.Text = string.Format(_remaniningText, proteinCount, peptideCount, precursorCount, transitionCount);
            }
        }
    }
}
