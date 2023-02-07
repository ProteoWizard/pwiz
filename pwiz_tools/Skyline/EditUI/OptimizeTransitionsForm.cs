using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    public partial class OptimizeTransitionsForm : DataboundGridForm
    {
        private Selection _selection;
        private CancellationTokenSource _cancellationTokenSource;
        private List<Row> _rowList = new List<Row>();
        private BindingList<Row> _bindingList;
        private SkylineDataSchema _dataSchema;
        private SequenceTree _sequenceTree;
        private OptimizeTransitionDetails _details;
        public OptimizeTransitionsForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _bindingList = new BindingList<Row>(_rowList);
            _dataSchema = new SkylineWindowDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            BindingListSource.QueryLock = _dataSchema.QueryLock;
            _bindingList = new BindingList<Row>(_rowList);
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(Row));
            var rowSourceInfo = new RowSourceInfo(BindingListRowSource.Create(_bindingList),
                SkylineViewContext.GetDefaultViewInfo(rootColumn));
            BindingListSource.SetViewContext(new SkylineViewContext(_dataSchema, ImmutableList.Singleton(rowSourceInfo)));
            DataGridView.CurrentCellChanged += DataGridView_OnCurrentCellChanged;
        }

        private void DataGridView_OnCurrentCellChanged(object sender, EventArgs e)
        {
            var currentCell = DataGridView.CurrentCell;
            if (currentCell == null)
            {
                return;
            }
            var rowIndex = currentCell.RowIndex;
            if (rowIndex < 0 || rowIndex >= BindingListSource.Count)
            {
                return;
            }

            var row = (BindingListSource[rowIndex] as RowItem)?.Value as Row;
            if (row == null)
            {
                return;
            }

            var transitionIdentityPath = row.Transition.IdentityPath;

            var columnIndex = currentCell.ColumnIndex;
            if (columnIndex < 0 || columnIndex >= DataGridView.ColumnCount)
            {
                return;
            }

            var dataPropertyName = DataGridView.Columns[columnIndex]?.DataPropertyName;
            if (dataPropertyName == null)
            {
                return;
            }

            var propertyPath =
                (BindingListSource.ItemProperties.FindByName(dataPropertyName) as ColumnPropertyDescriptor)
                ?.PropertyPath;
            if (propertyPath == null)
            {
                return;
            }

            IList<TransitionsQuantLimit> quantLimitList = null;
            if (propertyPath.StartsWith(PropertyPath.Root.Property(nameof(Row.SingleQuantLimit))))
            {
                quantLimitList = _details.SingleQuantLimits;
            }
            else if (propertyPath.StartsWith(PropertyPath.Root.Property(nameof(Row.AcceptedQuantLimit))))
            {
                quantLimitList = _details.AcceptedQuantLimits;
            }
            else if (propertyPath.StartsWith(PropertyPath.Root.Property(nameof(Row.RejectedQuantLimit))))
            {
                quantLimitList = _details.RejectedQuantLimits;
            }

            if (quantLimitList == null)
            {
                return;
            }

            var transitionQuantLimit = quantLimitList.FirstOrDefault(tql =>
                Equals(transitionIdentityPath, tql.TransitionIdentityPaths.Last()));
            if (transitionQuantLimit == null)
            {
                return;
            }

            DisplayTransitionQuantLimit(transitionQuantLimit);
        }

        public SkylineWindow SkylineWindow { get; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SkylineWindow.DocumentUIChangedEvent += SkylineWindow_OnDocumentUIChangedEvent;
            OnDocumentChanged();
        }

        private void SkylineWindow_OnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs e)
        {
            OnDocumentChanged();
        }

        private void OnDocumentChanged()
        {
            SetSequenceTree(SkylineWindow.SequenceTree);
            UpdateSelection();
        }

        public void UpdateSelection()
        {
            SetSelection(GetCurrentSelection());
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            SkylineWindow.DocumentUIChangedEvent -= SkylineWindow_OnDocumentUIChangedEvent;
            SetSequenceTree(null);
            base.OnHandleDestroyed(e);
        }

        private Selection GetCurrentSelection()
        {
            var identityPath = SkylineWindow.SelectedPath;
            if (identityPath.Length <= (int)SrmDocument.Level.Molecules)
            {
                return null;
            }

            return new Selection(optimizeTransitionsSettingsControl1.CurrentSettings, SkylineWindow.DocumentUI,
                identityPath.GetPathTo((int)SrmDocument.Level.Molecules));
        }

        private void SetSequenceTree(SequenceTree sequenceTree)
        {
            if (ReferenceEquals(sequenceTree, _sequenceTree))
            {
                return;
            }

            if (_sequenceTree != null)
            {
                _sequenceTree.AfterSelect -= SequenceTree_OnAfterSelect;
            }

            _sequenceTree = sequenceTree;
            if (_sequenceTree != null)
            {
                _sequenceTree.AfterSelect += SequenceTree_OnAfterSelect;
            }
        }

        private void SequenceTree_OnAfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdateSelection();
        }

        private void SetSelection(Selection newSelection)
        {
            if (Equals(newSelection, _selection))
            {
                return;
            }

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = null;
            _selection = newSelection;
            if (!Equals(newSelection?.MoleculeIdentityPath, _selection?.MoleculeIdentityPath))
            {
                _rowList.Clear();
            }

            if (newSelection != null)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _cancellationTokenSource.Token;
                ActionUtil.RunAsync(() =>
                {
                    var details = OptimizeTransitions(cancellationToken, newSelection);
                    CommonActionUtil.SafeBeginInvoke(this, () =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            SetDetails(newSelection, details);
                        }
                    });
                });
            }
        }

        private void SetDetails(Selection selection, OptimizeTransitionDetails details)
        {
            if (!Equals(selection, _selection))
            {
                return;
            }
            _rowList.Clear();
            _details = details;
            if (details != null)
            {
                foreach (var singleQuantLimit in details.SingleQuantLimits)
                {
                    var transition = new Model.Databinding.Entities.Transition(_dataSchema,
                        singleQuantLimit.TransitionIdentityPaths.Single());
                    var row = Row.CreateRow(transition, details);
                    _rowList.Add(row);
                }
            }
            _bindingList.ResetBindings();
        }

        private OptimizeTransitionDetails OptimizeTransitions(CancellationToken cancellationToken, Selection selection)
        {
            var bilinearCurveFitter = new BilinearCurveFitter()
            {
                CancellationToken = cancellationToken,
                OptimizeTransitionSettings = selection.Settings
            };
            var peptideQuantifier =
                OptimizeDocumentTransitionsForm.GetPeptideQuantifier(null, selection.Document,
                    selection.MoleculeIdentityPath);
            if (peptideQuantifier == null)
            {
                return null;
            }

            if (!selection.Settings.PreserveNonQuantitative)
            {
                peptideQuantifier = peptideQuantifier.MakeAllTransitionsQuantitative();
            }

            var calibrationCurveFitter = new CalibrationCurveFitter(peptideQuantifier, selection.Document.Settings);
            var details = new OptimizeTransitionDetails();
            bilinearCurveFitter.OptimizeTransitions(calibrationCurveFitter, details);
            return details;
        }

        public class Row
        {
            public Model.Databinding.Entities.Transition Transition { get; private set; }
            [ChildDisplayName("Single{0}")] public QuantLimit SingleQuantLimit { get; private set; }
            [ChildDisplayName("Accepted{0}")] public QuantLimit AcceptedQuantLimit { get; private set; }
            [ChildDisplayName("Rejected{0}")] public QuantLimit RejectedQuantLimit { get; private set; }

            public static Row CreateRow(Model.Databinding.Entities.Transition transition,
                OptimizeTransitionDetails details)
            {
                return new Row
                {
                    Transition = transition,
                    SingleQuantLimit = details.SingleQuantLimits.FirstOrDefault(tql =>
                        Equals(transition.IdentityPath, tql.TransitionIdentityPaths.Single()))?.QuantLimit,
                    AcceptedQuantLimit = details.AcceptedQuantLimits
                        .FirstOrDefault(tql => Equals(transition.IdentityPath, tql.TransitionIdentityPaths.Last()))
                        ?.QuantLimit,
                    RejectedQuantLimit = details.RejectedQuantLimits
                        .FirstOrDefault(tql => Equals(transition.IdentityPath, tql.TransitionIdentityPaths.Last()))
                        ?.QuantLimit
                };
            }
        }

        public OptimizeTransitionSettings Settings
        {
            get
            {
                return optimizeTransitionsSettingsControl1.CurrentSettings;
            }
            set
            {
                optimizeTransitionsSettingsControl1.CurrentSettings = value;
            }
        }

        private class Selection : Immutable
        {
            public Selection(OptimizeTransitionSettings settings, SrmDocument document, IdentityPath identityPath)
            {
                Settings = settings;
                Document = document;
                MoleculeIdentityPath = identityPath;
            }
            public IdentityPath MoleculeIdentityPath { get; private set; }
            public SrmDocument Document { get; private set; }
            public OptimizeTransitionSettings Settings { get; private set; }

            public PeptideGroupDocNode PeptideGroupDocNode
            {
                get
                {
                    return Document.FindPeptideGroup((PeptideGroup) MoleculeIdentityPath.GetIdentity(0));
                }
            }

            public PeptideDocNode PeptideDocNode
            {
                get
                {
                    return (PeptideDocNode)PeptideGroupDocNode.FindNode(MoleculeIdentityPath.GetIdentity(1));
                }
            }

            protected bool Equals(Selection other)
            {
                return MoleculeIdentityPath.Equals(other.MoleculeIdentityPath) 
                       && Document.Equals(other.Document) &&
                       Settings.Equals(other.Settings);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Selection)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = MoleculeIdentityPath.GetHashCode();
                    hashCode = (hashCode * 397) ^ Document.GetHashCode();
                    hashCode = (hashCode * 397) ^ Settings.GetHashCode();
                    return hashCode;
                }
            }
        }

        private void optimizeTransitionsSettingsControl1_SettingsChanged(object sender, EventArgs e)
        {
            UpdateSelection();
        }

        public void DisplayTransitionQuantLimit(TransitionsQuantLimit transitionQuantLimit)
        {
            var document = _selection.Document;
            var quantificationSettings = document.Settings.PeptideSettings.Quantification;
            quantificationSettings = quantificationSettings
                .ChangeRegressionFit(RegressionFit.BILINEAR)
                .ChangeRegressionWeighting(RegressionWeighting.ONE_OVER_X_SQUARED);

            var peptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(_selection.Document,
                    _selection.PeptideGroupDocNode, _selection.PeptideDocNode)
                .WithQuantifiableTransitions(transitionQuantLimit.TransitionIdentityPaths);
            var calibrationCurveFitter = new CalibrationCurveFitter(peptideQuantifier, _selection.Document.Settings);
            var settings = new CalibrationGraphControl.Settings(document, calibrationCurveFitter,
                Properties.Settings.Default.CalibrationCurveOptions);
            calibrationGraphControl1.Update(settings);
        }
    }
}
    
