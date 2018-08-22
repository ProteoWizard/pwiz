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
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

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

        public bool DocumentFinalCalculated { get; private set; }
        public SrmDocument DocumentFinal { get; private set; }
        private int? _documentFinalEmptyProteins;
        public int? DocumentFinalEmptyProteins { get { return DocumentFinalCalculated ? _documentFinalEmptyProteins : null; } }

        private readonly string _decoyGenerationMethod;
        private readonly double _decoysPerTarget;

        private CancellationTokenSource _cancellationTokenSource;

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
            lblNew.Text = FormatCounts(lblNew.Text, proteinCount, peptideCount, precursorCount, transitionCount);

            var docRefined = new RefinementSettings {RemoveDuplicatePeptides = true}.Refine(_document);
            if (_document.PeptideCount == docRefined.PeptideCount)
            {
                Height -= panelRemaining.Top - panelDuplicates.Top;
                panelDuplicates.Hide();
                panelRemaining.Top = panelDuplicates.Top;
            }

            numMinPeptides.TextChanged += UpdateRemaining;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            // Start background update after the handle is created, because it relies
            // on BeginInvoke on the handle to complete
            UpdateRemaining(null, null);

            base.OnHandleCreated(e);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (!DocumentFinalCalculated)
                return;
            DialogResult = DialogResult.OK;
        }

        private static string FormatCounts(string text, int proteins, int peptides, int precursors, int transitions)
        {
            const int separatorThreshold = 10000;
            var culture = LocalizationHelper.CurrentCulture;
            var proteinString = proteins < separatorThreshold ? proteins.ToString(culture) : proteins.ToString("N0", culture); // Not L10N
            var peptideString = peptides < separatorThreshold ? peptides.ToString(culture) : peptides.ToString("N0", culture); // Not L10N
            var precursorString = precursors < separatorThreshold ? precursors.ToString(culture) : precursors.ToString("N0", culture); // Not L10N
            var transitionString = transitions < separatorThreshold ? transitions.ToString(culture) : transitions.ToString("N0", culture); // Not L10N
            return string.Format(text, proteinString, peptideString, precursorString, transitionString);
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
            if (!DocumentFinalCalculated)
                throw new Exception();
            proteins = DocumentFinal.PeptideGroupCount;
            peptides = DocumentFinal.PeptideCount;
            precursors = DocumentFinal.PeptideTransitionGroupCount;
            transitions = DocumentFinal.PeptideTransitionCount;
        }

        public void NewTargetsFinalSync(out int proteins, out int peptides, out int precursors, out int transitions)
        {
            int? emptyProteins;
            NewTargetsFinalSync(out proteins, out peptides, out precursors, out transitions, out emptyProteins);
        }

        public void NewTargetsFinalSync(out int proteins, out int peptides, out int precursors, out int transitions, out int? emptyProteins)
        {
            var doc = GetDocumentFinal(CancellationToken.None, _document, MinPeptides, RemoveRepeatedPeptides, RemoveDuplicatePeptides, out emptyProteins);
            proteins = doc.PeptideGroupCount;
            peptides = doc.PeptideCount;
            precursors = doc.PeptideTransitionGroupCount;
            transitions = doc.PeptideTransitionCount;
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
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }
            _cancellationTokenSource = new CancellationTokenSource();
            DoUpdateRemaining(_cancellationTokenSource.Token);
        }

        private void DoUpdateRemaining(CancellationToken cancellationToken)
        {
            btnOK.Enabled = false;
            DocumentFinalCalculated = false;

            lblRemaining.Text = Resources.PeptidesPerProteinDlg_UpdateRemaining_Calculating___;
            lblEmptyProteins.Text = string.Empty;

            var keepAll = KeepAll;
            numMinPeptides.Enabled = !keepAll;

            ActionUtil.RunAsync(() =>
            {
                var doc = _document;
                var minPeptides = MinPeptides;
                var removeRepeated = RemoveRepeatedPeptides;
                var removeDuplicate = RemoveDuplicatePeptides;
                SrmDocument docFinal;
                try
                {
                    docFinal = GetDocumentFinal(cancellationToken, doc, minPeptides, removeRepeated, removeDuplicate,
                        out _documentFinalEmptyProteins);
                }
                catch (OperationCanceledException)
                {
                    docFinal = null;
                }
                if (cancellationToken.IsCancellationRequested || docFinal == null)
                    return;
                CommonActionUtil.SafeBeginInvoke(this, () =>
                {
                    DocumentFinal = docFinal;
                    DocumentFinalCalculated = true;
                    btnOK.Enabled = true;

                    if (keepAll)
                        lblEmptyProteins.Text = string.Format(_emptyProteinsText,
                            DocumentFinalEmptyProteins.GetValueOrDefault());

                    int proteinCount, peptideCount, precursorCount, transitionCount;
                    NewTargetsFinal(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                    lblRemaining.Text = FormatCounts(_remaniningText, proteinCount, peptideCount, precursorCount,
                        transitionCount);
                });
            });
        }

        private SrmDocument GetDocumentFinal(CancellationToken cancellationToken, SrmDocument doc, int minPeptides, bool removeRepeated, bool removeDuplicate, out int? emptyProteins)
        {
            emptyProteins = null;

            // Remove repeated/duplicate peptides
            var newDoc = removeRepeated || removeDuplicate
                ? new RefinementSettings {RemoveRepeatedPeptides = removeRepeated, RemoveDuplicatePeptides = removeDuplicate}.Refine(doc)
                : doc;

            if (cancellationToken.IsCancellationRequested)
                return null;

            // Remove proteins without enough peptides
            newDoc = ImportPeptideSearch.RemoveProteinsByPeptideCount(newDoc, minPeptides);

            if (cancellationToken.IsCancellationRequested)
                return null;

            // Move iRT proteins to top
            var irtPeptides = new HashSet<Target>(RCalcIrt.IrtPeptides(newDoc));
            var proteins = new List<PeptideGroupDocNode>(newDoc.PeptideGroups);
            var proteinsIrt = new List<PeptideGroupDocNode>();
            for (var i = 0; i < proteins.Count; i++)
            {
                var nodePepGroup = proteins[i];
                if (nodePepGroup.Peptides.All(nodePep => irtPeptides.Contains(new Target(nodePep.ModifiedSequence))))
                {
                    proteinsIrt.Add(nodePepGroup);
                    proteins.RemoveAt(i--);
                }
            }
            if (proteinsIrt.Any())
                newDoc = (SrmDocument) newDoc.ChangeChildrenChecked(proteinsIrt.Concat(proteins).Cast<DocNode>().ToArray());

            if (cancellationToken.IsCancellationRequested)
                return null;

            // Add decoys
            newDoc = AddDecoys(newDoc);

            if (cancellationToken.IsCancellationRequested)
                return null;

            // Count empty proteins
            emptyProteins = newDoc.PeptideGroups.Count(pepGroup => pepGroup.PeptideCount == 0);

            if (cancellationToken.IsCancellationRequested)
                return null;

            return newDoc;
        }
    }
}
