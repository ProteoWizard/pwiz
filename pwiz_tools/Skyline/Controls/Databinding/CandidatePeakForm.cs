using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
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


        public CandidatePeakForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
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
        }

        public SkylineWindow SkylineWindow { get; }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SkylineWindow.DocumentUIChangedEvent += SkylineWindow_DocumentUIChangedEvent;
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
            SkylineWindow.DocumentUIChangedEvent -= SkylineWindow_DocumentUIChangedEvent;
            SetSequenceTree(null);
            base.OnHandleDestroyed(e);
        }

        private void SkylineWindow_DocumentUIChangedEvent(object sender, DocumentChangedEventArgs e)
        {
            OnDocumentChanged();
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
            BeginInvoke(new Action(UpdateRowSource));
        }

        private void UpdateRowSource()
        {
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
                new ColumnSpec(PropertyPath.Root.Property(nameof(CandidatePeakGroup.PeakGroupStartTime))),
                new ColumnSpec(PropertyPath.Root.Property(nameof(CandidatePeakGroup.PeakGroupEndTime))),
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
    }
}
