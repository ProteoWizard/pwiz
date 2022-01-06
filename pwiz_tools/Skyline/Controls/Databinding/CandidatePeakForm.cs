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
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class CandidatePeakForm : DataboundGridForm
    {
        private readonly SkylineDataSchema _dataSchema;
        private SequenceTree _sequenceTree;
        private CandidatePeakGroups _peakGroups;
        private IList<IdentityPath> _selectedIdentityPaths = ImmutableList.Empty<IdentityPath>();

        public CandidatePeakForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _dataSchema = new SkylineDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            BindingListSource.QueryLock = _dataSchema.QueryLock;
            _peakGroups = new CandidatePeakGroups(_dataSchema);
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
            OnDocumentChanged();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
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

        public void SetReplicateIndex(int replicateIndex)
        {
            if (replicateIndex == _peakGroups.Replicate?.ReplicateIndex)
            {
                return;
            }

            _peakGroups.Replicate = new Replicate(_dataSchema, replicateIndex);
        }

        private void UpdateRowSource()
        {
            _peakGroups.Replicate = new Replicate(_dataSchema, SkylineWindow.SelectedResultsIndex);
            _peakGroups.SetSelectedIdentityPaths(SelectedIdentityPaths);
        }

        private HashSet<IdentityPath> GetPrecursorIdentityPaths(SrmDocument document, IEnumerable<IdentityPath> identityPaths)
        {
            return identityPaths.Where(path => path.Length >= 3).Select(path => path.GetPathTo(2)).ToHashSet();
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
    }
}
