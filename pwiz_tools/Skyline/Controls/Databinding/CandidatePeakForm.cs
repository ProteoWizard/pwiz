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
            var viewContext =
                new SkylineViewContext(ColumnDescriptor.RootColumn(_dataSchema, typeof(CandidatePeakGroup)),
                    _peakGroups);
            BindingListSource.SetViewContext(viewContext);
            Text = TabText = "Candidate Peaks";
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
    }
}
