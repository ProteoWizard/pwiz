/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Colors;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class CandidatePeakForm : DataboundGridForm
    {
        private readonly SkylineDataSchema _dataSchema;
        private SequenceTree _sequenceTree;
        private Selector _selector;
        private BindingList<CandidatePeakGroup> _bindingList;
        private List<CandidatePeakGroup> _candidatePeakGroups;
        private Color _originalPeakColor;
        private readonly DocumentChangeListener _documentChangeListener;
        private bool _updatePending;

        public CandidatePeakForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            _documentChangeListener = new DocumentChangeListener(this);
            SkylineWindow = skylineWindow;
            _dataSchema = new SkylineDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            BindingListSource.QueryLock = _dataSchema.QueryLock;
            _candidatePeakGroups = new List<CandidatePeakGroup>();
            _bindingList = new BindingList<CandidatePeakGroup>(_candidatePeakGroups);
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(CandidatePeakGroup));
            var rowSourceInfo = new RowSourceInfo(BindingListRowSource.Create(_bindingList),
                new ViewInfo(rootColumn, GetDefaultViewSpec()));
            var viewContext = new SkylineViewContext(_dataSchema, new []{rowSourceInfo});
            BindingListSource.SetViewContext(viewContext);
            Text = TabText = Resources.CandidatePeakForm_CandidatePeakForm_Candidate_Peaks;
            DataboundGridControl.DataGridView.CellFormatting += DataGridView_OnCellFormatting;
            DataboundGridControl.DataGridView.CurrentCellDirtyStateChanged += DataGridView_OnCurrentCellDirtyStateChanged;
            _originalPeakColor = GetOriginalPeakColor();
        }

        private void DataGridView_OnCurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            var dataGridView = DataboundGridControl.DataGridView;
            var currentColumn = dataGridView.CurrentCell.OwningColumn;
            if (currentColumn is DataGridViewCheckBoxColumn)
            {
                // If the user has clicked on the "Chosen" column, commit that change immediately since
                // allowing them to move off the row at the same time as the value is being committed
                // can lead to painting weirdness.
                var currentPropertyPath = (BindingListSource.ItemProperties.FindByName(currentColumn.DataPropertyName)
                    as ColumnPropertyDescriptor)?.PropertyPath;
                if (PropertyPath.Root.Property(nameof(CandidatePeakGroup.Chosen)).Equals(currentPropertyPath))
                {
                    dataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            }
        }

        private void DataGridView_OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (!Settings.Default.ShowOriginalPeak)
            {
                return;
            }

            if (e.RowIndex < 0 || e.RowIndex >= BindingListSource.Count)
                return;
            var candidatePeak = (BindingListSource[e.RowIndex] as RowItem)?.Value as CandidatePeakGroup;
            if (candidatePeak == null)
            {
                return;
            }

            if (!candidatePeak.Chosen && candidatePeak.GetCandidatePeakGroupData().OriginallyBestPeak)
            {
                e.CellStyle.BackColor = _originalPeakColor;
            }
        }

        public SkylineWindow SkylineWindow { get; }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _dataSchema.Listen(_documentChangeListener);
            SkylineWindow.ComboResults.SelectedIndexChanged += ComboResults_OnSelectedIndexChanged;
            OnDocumentChanged();
        }

        private void ComboResults_OnSelectedIndexChanged(object sender, EventArgs e)
        {
            QueueUpdateRowSource();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (SkylineWindow.ComboResults != null)
            {
                SkylineWindow.ComboResults.SelectedIndexChanged -= ComboResults_OnSelectedIndexChanged;
            }
            _dataSchema.Unlisten(_documentChangeListener);
            SetSequenceTree(null);
            base.OnHandleDestroyed(e);
        }

        private void OnDocumentChanged()
        {
            SetSequenceTree(SkylineWindow.SequenceTree);
            QueueUpdateRowSource();
        }

        private void SetSequenceTree(SequenceTree sequenceTree)
        {
            if (ReferenceEquals(_sequenceTree, sequenceTree))
            {
                return;
            }
            if (null != _sequenceTree)
            {
                _sequenceTree.AfterSelect -= SequenceTreeOnAfterSelect;
            }
            _sequenceTree = sequenceTree;
            if (null != _sequenceTree)
            {
                _sequenceTree.AfterSelect += SequenceTreeOnAfterSelect;
            }
        }

        private void SequenceTreeOnAfterSelect(object sender, TreeViewEventArgs args)
        {
            QueueUpdateRowSource();
        }

        private void QueueUpdateRowSource()
        {
            _updatePending = true;
            BeginInvoke(new Action(UpdateRowSource));
        }

        private void UpdateRowSource()
        {
            _updatePending = false;
            var newSelector = GetSelector();
            if (Equals(newSelector, _selector))
            {
                return;
            }

            _selector = newSelector;
            _candidatePeakGroups.Clear();
            _candidatePeakGroups.AddRange(GetCandidatePeakGroups(newSelector));
            _bindingList.ResetBindings();
        }

        private IdentityPath GetPrecursorIdentityPath()
        {
            var document = SkylineWindow.DocumentUI;
            foreach (var identityPath in _sequenceTree.SelectedPaths.OrderByDescending(path=>path.Length))
            {
                if (identityPath.Length >= 3)
                {
                    return identityPath.GetPathTo(2);
                }

                PeptideGroupDocNode peptideGroupDocNode;
                if (identityPath.Length >= 1)
                {
                    peptideGroupDocNode = (PeptideGroupDocNode) document.FindNode(identityPath.GetIdentity(0));
                }
                else
                {
                    continue;
                }

                if (peptideGroupDocNode == null)
                {
                    continue;
                }

                PeptideDocNode peptideDocNode;
                if (identityPath.Length >= 2)
                {
                    peptideDocNode = (PeptideDocNode) peptideGroupDocNode.FindNode(identityPath.GetIdentity(1));
                }
                else
                {
                    if (peptideGroupDocNode.Children.Count > 1)
                    {
                        continue;
                    }
                    peptideDocNode = peptideGroupDocNode.Molecules.FirstOrDefault();
                }

                if (peptideDocNode == null)
                {
                    continue;
                }

                var transitionGroupDocNode = peptideDocNode.TransitionGroups.FirstOrDefault();
                if (transitionGroupDocNode == null)
                {
                    continue;
                }

                return new IdentityPath(peptideGroupDocNode.PeptideGroup, peptideDocNode.Peptide,
                    transitionGroupDocNode.TransitionGroup);
            }

            return null;
        }

        private ViewSpec GetDefaultViewSpec()
        {
            var viewSpec = new ViewSpec().SetName(Resources.CandidatePeakForm_CandidatePeakForm_Candidate_Peaks).SetColumns(new[]
            {
                new ColumnSpec(PropertyPath.Root),
                new ColumnSpec(PropertyPath.Root.Property(nameof(CandidatePeakGroup.PeakGroupRetentionTime))),
                new ColumnSpec(PropertyPath.Root.Property(nameof(CandidatePeakGroup.Chosen))),
                new ColumnSpec(PropertyPath.Root.Property(nameof(CandidatePeakGroup.PeakScores))
                    .Property(nameof(PeakGroupScore.ModelScore))),
                new ColumnSpec(PropertyPath.Root.Property(nameof(CandidatePeakGroup.PeakScores))
                    .Property(nameof(PeakGroupScore.PeakQValue))),
                new ColumnSpec(PropertyPath.Root.Property(nameof(CandidatePeakGroup.PeakScores))
                    .Property(nameof(PeakGroupScore.WeightedFeatures)).DictionaryValues())
            });
            return viewSpec;
        }

        private IList<CandidatePeakGroup> GetCandidatePeakGroups(Selector selector)
        {
            var candidatePeakGroups = new List<CandidatePeakGroup>();
            if (selector == null)
            {
                return candidatePeakGroups;
            }
            var featureCalculator = OnDemandFeatureCalculator.GetFeatureCalculator(selector.Document,
                selector.PeptideIdentityPath, selector.ReplicateIndex, selector.ChromFileInfoId);
            if (featureCalculator == null)
            {
                return candidatePeakGroups;
            }

            var precursorIdentityPath =
                new IdentityPath(selector.PeptideIdentityPath, selector.TransitionGroups.First());
            var precursor = new Precursor(_dataSchema, precursorIdentityPath);
            var precursorResult = new PrecursorResult(precursor,
                new ResultFile(new Replicate(_dataSchema, selector.ReplicateIndex), selector.ChromFileInfoId, 0));

            var transitionGroup = (TransitionGroup)precursorIdentityPath.Child;
            foreach (var peakGroupData in featureCalculator.GetCandidatePeakGroups(transitionGroup))
            {
                candidatePeakGroups.Add(new CandidatePeakGroup(precursorResult, peakGroupData));
            }

            if (!candidatePeakGroups.Any(peak => peak.Chosen))
            {
                var chosenPeak = featureCalculator.GetChosenPeakGroupData(transitionGroup);
                if (chosenPeak != null)
                {
                    candidatePeakGroups.Add(new CandidatePeakGroup(precursorResult, chosenPeak));
                }
            }

            return candidatePeakGroups.OrderBy(peak => Tuple.Create(peak.PeakGroupStartTime, peak.PeakGroupEndTime)).ToList();
        }

        private Selector GetSelector()
        {
            var precursorIdentityPath = GetPrecursorIdentityPath();
            if (precursorIdentityPath == null)
            {
                return null;
            }
            var peptideIdentityPath = precursorIdentityPath.Parent;
            var transitionGroup = (TransitionGroup) precursorIdentityPath.Child;
            var document = SkylineWindow.DocumentUI;
            if (!document.Settings.HasResults)
            {
                return null;
            }
            var peptideDocNode = (PeptideDocNode)document.FindNode(peptideIdentityPath);
            if (peptideDocNode == null)
            {
                return null;
            }
            int replicateIndex = SkylineWindow.SelectedResultsIndex;
            if (replicateIndex < 0 || replicateIndex >= document.Settings.MeasuredResults.Chromatograms.Count)
            {
                return null;
            }

            var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[replicateIndex];
            var chromFileInfoId = chromatogramSet.MSDataFileInfos.First().FileId;
            foreach (var comparableGroup in peptideDocNode.GetComparableGroups())
            {
                var transitionGroups = ImmutableList.ValueOf(comparableGroup.Select(tg => tg.TransitionGroup));
                if (transitionGroups.Any(precursor => ReferenceEquals(transitionGroup, precursor)))
                {
                    return new Selector(document, peptideIdentityPath, transitionGroups,
                        replicateIndex, chromFileInfoId);
                }
            }

            return null;
        }

        private class Selector
        {
            public Selector(SrmDocument document, IdentityPath peptideIdentityPath,
                IEnumerable<TransitionGroup> transitionGroups, int replicateIndex, ChromFileInfoId chromFileInfoId)
            {
                Document = document;
                PeptideIdentityPath = peptideIdentityPath;
                TransitionGroups = ImmutableList.ValueOf(transitionGroups);
                ReplicateIndex = replicateIndex;
                ChromFileInfoId = chromFileInfoId;
            }

            public SrmDocument Document { get; }
            public IdentityPath PeptideIdentityPath { get; }
            public ImmutableList<TransitionGroup> TransitionGroups { get; }
            public int ReplicateIndex { get; }
            public ChromFileInfoId ChromFileInfoId { get; }

            protected bool Equals(Selector other)
            {
                return ReferenceEquals(Document, other.Document)
                       && Equals(PeptideIdentityPath, other.PeptideIdentityPath)
                       && ArrayUtil.ReferencesEqual(TransitionGroups, other.TransitionGroups)
                       && ReplicateIndex == other.ReplicateIndex
                       && ReferenceEquals(ChromFileInfoId, other.ChromFileInfoId);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Selector) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = RuntimeHelpers.GetHashCode(Document);
                    hashCode = (hashCode * 397) ^ PeptideIdentityPath.GetHashCode();
                    hashCode = (hashCode * 397) ^ ReplicateIndex;
                    hashCode = (hashCode * 397) ^ RuntimeHelpers.GetHashCode(ChromFileInfoId);
                    return hashCode;
                }
            }
        }

        /// <summary>
        /// Returns the color to be used as the background to indicate the originally chosen peak.
        /// </summary>
        private Color GetOriginalPeakColor()
        {
            return ColorPalettes.MergeWithBackground(ChromGraphItem.COLOR_ORIGINAL_PEAK_SHADE,
                DataboundGridControl.DataGridView.BackColor);
        }

        private class DocumentChangeListener : IDocumentChangeListener
        {
            private readonly CandidatePeakForm _form;
            public DocumentChangeListener(CandidatePeakForm form)
            {
                _form = form;
            }

            public void DocumentOnChanged(object sender, DocumentChangedEventArgs args)
            {
                _form.OnDocumentChanged();
            }
        }

        public new bool IsComplete
        {
            get
            {
                return base.IsComplete && !_updatePending;
            }
        }
    }
}
