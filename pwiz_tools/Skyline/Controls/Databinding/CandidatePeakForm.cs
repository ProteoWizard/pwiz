using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class CandidatePeakForm : DataboundGridForm
    {
        private readonly SkylineDataSchema _dataSchema;
        private SequenceTree _sequenceTree;
        private IList<IdentityPath> _selectedIdentityPaths = ImmutableList.Empty<IdentityPath>();
        private CandidatePeakGroups _peakGroups;

        public CandidatePeakForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _dataSchema = new SkylineDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            BindingListSource.QueryLock = _dataSchema.QueryLock;
            _peakGroups = new CandidatePeakGroups();
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(CandidatePeakGroup));
            var rowSourceInfo = new RowSourceInfo(_peakGroups,
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
            UpdateRowSource();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            SkylineWindow.ComboResults.SelectedIndexChanged -= ComboResults_OnSelectedIndexChanged;
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
            UpdateRowSource();
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
                SelectedIdentityPaths = _sequenceTree.SelectedPaths;
            }
        }

        private void SequenceTreeOnAfterSelect(object sender, TreeViewEventArgs args)
        {
            SelectedIdentityPaths = _sequenceTree.SelectedPaths;
        }

        public IList<IdentityPath> SelectedIdentityPaths
        {
            get { return _selectedIdentityPaths; }
            set
            {
                if (_selectedIdentityPaths.SequenceEqual(value))
                {
                    return;
                }
                _selectedIdentityPaths = ImmutableList.ValueOf(value);
                UpdateRowSource();
            }
        }

        private void UpdateRowSource()
        {
            _peakGroups.List = ImmutableList.ValueOf(GetCandidatePeakGroups(SkylineWindow.DocumentUI, GetPrecursorIdentityPath(), SkylineWindow.SelectedResultsIndex, null));
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
                    .Property(nameof(PeakGroupScore.WeightedFeatures)).DictionaryValues())
            });
            return viewSpec;
        }

        public IList<CandidatePeakGroup> GetCandidatePeakGroups(SrmDocument document, IdentityPath precursorIdentityPath, int replicateIndex, ChromFileInfoId chromFileInfoId)
        {
            var candidatePeakGroups = new List<CandidatePeakGroup>();
            if (precursorIdentityPath == null)
            {
                return null;
            }
            var featureCalculator = OnDemandFeatureCalculator.GetFeatureCalculator(document,
                precursorIdentityPath.GetPathTo(1), replicateIndex, chromFileInfoId);
            if (featureCalculator == null)
            {
                return candidatePeakGroups;
            }
            var precursor = new Precursor(_dataSchema, precursorIdentityPath);
            var precursorResult = new PrecursorResult(precursor,
                new ResultFile(new Replicate(_dataSchema, replicateIndex), chromFileInfoId, 0));

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
    }
}
