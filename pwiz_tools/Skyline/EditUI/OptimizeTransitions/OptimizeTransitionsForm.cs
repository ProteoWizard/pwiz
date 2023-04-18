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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using Transition = pwiz.Skyline.Model.Transition;

namespace pwiz.Skyline.EditUI.OptimizeTransitions
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
        private Selection _detailsSelection;
        private string _originalTitle;
        private bool _updateTransitionPending;
        public OptimizeTransitionsForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            databoundGridControl.Parent.Controls.Remove(databoundGridControl);
            databoundGridControl.Dock = DockStyle.Fill;
            splitContainer1.Panel1.Controls.Add(databoundGridControl);
            SkylineWindow = skylineWindow;
            calibrationGraphControl1.SkylineWindow = skylineWindow;
            _bindingList = new BindingList<Row>(_rowList);
            _dataSchema = new SkylineWindowDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            BindingListSource.QueryLock = _dataSchema.QueryLock;
            _bindingList = new BindingList<Row>(_rowList);
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(Row));
            var rowSourceInfo = new RowSourceInfo(BindingListRowSource.Create(_bindingList),
                SkylineViewContext.GetDefaultViewInfo(rootColumn));
            BindingListSource.SetViewContext(new SkylineViewContext(_dataSchema, ImmutableList.Singleton(rowSourceInfo)));
            DataGridView.CurrentCellChanged += DataGridView_OnSelectionChanged;
            DataGridView.SelectionChanged += DataGridView_OnSelectionChanged;
            _originalTitle = Text;
            Icon = Resources.Skyline;
        }

        private void DataGridView_OnSelectionChanged(object sender, EventArgs e)
        {
            if (!_updateTransitionPending)
            {
                _updateTransitionPending = true;
                BeginInvoke(new Action(DisplayQuantLimitForSelection));
            }
        }

        private void DisplayQuantLimitForSelection()
        {
            _updateTransitionPending = false;
            var selectedRowIndexes = DataGridView.SelectedRows.Cast<DataGridViewRow>()
                .Select(row => row.Index).ToHashSet();
            var transitionIdentityPaths = new HashSet<IdentityPath>();
            foreach (var rowIndex in selectedRowIndexes)
            {
                var identityPath = GetRowTransitionIdentityPath(rowIndex);
                if (identityPath != null)
                {
                    transitionIdentityPaths.Add(identityPath);
                }
            }

            foreach (DataGridViewCell cell in DataGridView.SelectedCells)
            {
                if (selectedRowIndexes.Contains(cell.RowIndex))
                {
                    continue;
                }
                transitionIdentityPaths.UnionWith(GetCellTransitionIdentityPaths(cell));
            }

            if (transitionIdentityPaths.Count == 0)
            {
                transitionIdentityPaths.UnionWith(GetCellTransitionIdentityPaths(DataGridView.CurrentCell));
            }

            if (transitionIdentityPaths.Count == 0)
            {
                return;
            }

            DisplayTransitionQuantLimit(transitionIdentityPaths);

        }

        private IEnumerable<IdentityPath> GetCellTransitionIdentityPaths(DataGridViewCell cell)
        {
            IEnumerable<IdentityPath> identityPaths = Array.Empty<IdentityPath>();
            if (cell == null)
            {
                return identityPaths;
            }
            var rowIndex = cell.RowIndex;
            if (rowIndex < 0 || rowIndex >= BindingListSource.Count)
            {
                return identityPaths;
            }
            var row = (BindingListSource[rowIndex] as RowItem)?.Value as Row;
            if (row == null)
            {
                return identityPaths;
            }

            var transitionIdentityPath = row.Transition.IdentityPath;
            identityPaths = new[] { transitionIdentityPath };

            var columnIndex = cell.ColumnIndex;
            if (columnIndex < 0 || columnIndex >= DataGridView.ColumnCount)
            {
                return identityPaths;
            }
            var dataPropertyName = DataGridView.Columns[columnIndex]?.DataPropertyName;
            if (dataPropertyName == null)
            {
                return identityPaths;
            }
            var propertyPath =
                (BindingListSource.ItemProperties.FindByName(dataPropertyName) as ColumnPropertyDescriptor)
                ?.PropertyPath;
            if (propertyPath == null)
            {
                return identityPaths;
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

            if (quantLimitList != null)
            {
                var transitionQuantLimit = quantLimitList.FirstOrDefault(tql =>
                    Equals(transitionIdentityPath, tql.TransitionIdentityPaths.Last()));
                if (transitionQuantLimit != null)
                {
                    return transitionQuantLimit.TransitionIdentityPaths;
                }
            }
            return identityPaths;
        }

        private IdentityPath GetRowTransitionIdentityPath(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= BindingListSource.Count)
            {
                return null;
            }
            var row = (BindingListSource[rowIndex] as RowItem)?.Value as Row;
            if (row == null)
            {
                return null;
            }

            return row.Transition.IdentityPath;
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

            if (selection.Settings.OptimizeType == OptimizeType.LOD)
            {
                lblOriginal.Text = Resources.OptimizeTransitionsForm_SetDetails_Original_LOD;
                lblOptimized.Text = Resources.OptimizeTransitionsForm_SetDetails_Optimized_LOD;
            }
            else
            {
                lblOriginal.Text = Resources.OptimizeTransitionsForm_SetDetails_Original_LLOQ;
                lblOptimized.Text = Resources.OptimizeTransitionsForm_SetDetails_Optimized_LLOQ;
            }


            Text = TabText = selection.PeptideDocNode == null
                ? _originalTitle
                : TextUtil.ColonSeparate(_originalTitle, selection.PeptideDocNode.ModifiedSequenceDisplay);
            _rowList.Clear();
            _details = details;
            _detailsSelection = selection;
            lblOriginal.Text = FormatQuantLimitString(false, details?.Original?.QuantLimit);
            var optimizedQuantLimit = details?.Optimized?.QuantLimit;
            lblOptimized.Text = FormatQuantLimitString(true, optimizedQuantLimit);
            btnApply.Enabled = optimizedQuantLimit != null;
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
            DisplayQuantLimitForSelection();
        }

        private string FormatQuantLimitString(bool optimized, QuantLimit quantLimit)
        {
            string limitName;
            double? value;
            if (_selection.Settings.OptimizeType == OptimizeType.LOD)
            {
                limitName = optimized
                    ? Resources.OptimizeTransitionsForm_SetDetails_Optimized_LOD
                    : Resources.OptimizeTransitionsForm_SetDetails_Original_LOD;
                value = quantLimit?.Lod;
            }
            else
            {
                limitName = optimized
                    ? Resources.OptimizeTransitionsForm_SetDetails_Optimized_LLOQ
                    : Resources.OptimizeTransitionsForm_SetDetails_Original_LLOQ;
                value = quantLimit?.Loq;
            }

            string valueText;
            if (value.HasValue)
            {
                valueText = FiguresOfMerit.FormatValue(value.Value,
                    !string.IsNullOrEmpty(_selection.Document.Settings.PeptideSettings.Quantification.Units));
            }
            else
            {
                valueText = Resources.OptimizeTransitionsForm_FormatQuantLimitString_Unknown;
            }

            return TextUtil.ColonSeparate(limitName, valueText);
        }

        private OptimizeTransitionDetails OptimizeTransitions(CancellationToken cancellationToken, Selection selection)
        {
            var bilinearCurveFitter = new BilinearTransitionOptimizer()
            {
                CancellationToken = cancellationToken,
                OptimizeTransitionSettings = selection.Settings
            };
            var peptideQuantifier =
                OptimizeDocumentTransitionsForm.GetPeptideQuantifier(null, selection.Document,
                    selection.MoleculeIdentityPath, selection.Settings);
            if (peptideQuantifier == null)
            {
                return null;
            }

            if (!selection.Settings.PreserveNonQuantitative)
            {
                peptideQuantifier = peptideQuantifier.MakeAllTransitionsQuantitative();
            }

            var calibrationCurveFitter =
                selection.Settings.GetCalibrationCurveFitter(peptideQuantifier, selection.Document.Settings);
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
                    return (PeptideDocNode)PeptideGroupDocNode?.FindNode(MoleculeIdentityPath.GetIdentity(1));
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

        public void DisplayTransitionQuantLimit(HashSet<IdentityPath> transitionIdentityPaths)
        {
            var document = _selection.Document;
            var quantificationSettings = _selection.Settings.GetQuantificationSettings(_selection.Document.Settings);
            quantificationSettings = quantificationSettings.ChangeLodCalculation(LodCalculation.TURNING_POINT_STDERR);
            var peptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(_selection.Document,
                    _selection.PeptideGroupDocNode, _selection.PeptideDocNode)
                .WithQuantificationSettings(quantificationSettings)
                .WithQuantifiableTransitions(transitionIdentityPaths);
            var calibrationCurveFitter =
                _selection.Settings.GetCalibrationCurveFitter(peptideQuantifier, _selection.Document.Settings);
            var settings = new CalibrationGraphControl.Settings(document, calibrationCurveFitter)
                .ChangeGraphTitle(GetCalibrationCurveTitle(document, transitionIdentityPaths));
            calibrationGraphControl1.Update(settings);
        }

        public string GetCalibrationCurveTitle(SrmDocument document, ICollection<IdentityPath> transitionIdentityPaths)
        {
            if (transitionIdentityPaths.Count == 0)
            {
                return string.Empty;
            }

            if (transitionIdentityPaths.Count == 1)
            {
                return string.Format(Resources.OptimizeTransitionsForm_GetCalibrationCurveTitle_Calibration_curve_using_only__0__transition, GetTransitionLabel(document, transitionIdentityPaths.Single()));
            }

            return string.Format(Resources.OptimizeTransitionsForm_GetCalibrationCurveTitle_Calibration_curve_using__0__transitions, transitionIdentityPaths.Count);
        }

        public string GetTransitionLabel(SrmDocument document, IdentityPath transitionIdentityPath)
        {
            var peptideDocNode = (PeptideDocNode) document.FindNode(transitionIdentityPath.GetPathTo((int)SrmDocument.Level.Molecules));
            var transitionGroupDocNode =
                (TransitionGroupDocNode)peptideDocNode?.FindNode(
                    transitionIdentityPath.GetIdentity((int)SrmDocument.Level.TransitionGroups));
            var transitionDocNode =
                (TransitionDocNode)transitionGroupDocNode?.FindNode(
                    transitionIdentityPath.GetIdentity((int)SrmDocument.Level.Transitions));
            if (transitionDocNode == null)
            {
                return transitionIdentityPath.ToString();
            }

            var transition = transitionDocNode.Transition;
            string transitionText;
            if (transition.IsCustom() || transition.IsPrecursor())
            {
                transitionText = transition.ToString();
            }
            else
            {
                transitionText = string.Concat(transition.IonType.ToString().ToLowerInvariant(),
                    transition.Ordinal,
                    Transition.GetChargeIndicator(transition.Adduct));
            }
                
            if (peptideDocNode.Children.Count == 1)
            {
                return transitionText;
            }

            string precursorText = TransitionGroupTreeNode.GetLabel(transitionGroupDocNode.TransitionGroup,
                transitionGroupDocNode.PrecursorMz, string.Empty);
            return TextUtil.ColonSeparate(precursorText, transitionText);
        }

        private void btnOptimizeDocumentTransitions_Click(object sender, EventArgs e)
        {
            ShowOptimizeDocumentTransitionsForm();
        }

        public void ShowOptimizeDocumentTransitionsForm()
        {
            SkylineWindow.ShowOptimizeDocumentTransitionsForm();
        }

        private void lblOriginal_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            SelectOriginalTransitionRows();
        }

        public void SelectOriginalTransitionRows()
        {
            SelectTransitionRows(_details?.Original?.TransitionIdentityPaths);
        }

        private void lblOptimized_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            SelectOptimizedTransitionRows();
        }

        public void SelectOptimizedTransitionRows()
        {
            SelectTransitionRows(_details?.Optimized?.TransitionIdentityPaths);
        }

        public void SelectTransitionRows(IEnumerable<IdentityPath> transitionIdentityPaths)
        {
            if (transitionIdentityPaths == null)
            {
                return;
            }
            var set = transitionIdentityPaths.ToHashSet();
            DataGridView.ClearSelection();
            foreach (DataGridViewRow row in DataGridView.Rows)
            {
                var identityPath = GetRowTransitionIdentityPath(row.Index);
                if (identityPath != null)
                {
                    row.Selected = set.Contains(identityPath);
                }
            }
        }

        

        private void btnApply_Click(object sender, EventArgs e)
        {
            var optimizedTransitions = _details?.Optimized?.TransitionIdentityPaths;
            if (optimizedTransitions == null)
            {
                return;
            }

            SetQuantifiableTransitions(_selection.PeptideGroupDocNode.PeptideGroup, _selection.PeptideDocNode.Peptide,
                optimizedTransitions);
        }

        public void SetQuantifiableTransitions(PeptideGroup peptideGroup, Peptide peptide, IEnumerable<IdentityPath> identityPaths)
        {
            if (identityPaths == null)
            {
                return;
            }

            string message = string.Empty;
            var set = identityPaths.ToHashSet();
            SkylineWindow.ModifyDocument(Resources.OptimizeTransitionsForm_SetQuantifiableTransitions_Optimize_Transitions, doc =>
            {
                var peptideIdentityPath = new IdentityPath(peptideGroup, peptide);
                var peptideDocNode = (PeptideDocNode)doc.FindNode(peptideIdentityPath);
                if (peptideDocNode == null)
                {
                    return doc;
                }

                var newTransitionGroups = new List<DocNode>();
                foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
                {
                    var newTransitions = new List<DocNode>();
                    foreach (var transition in transitionGroupDocNode.Transitions)
                    {
                        var identityPath = new IdentityPath(peptideGroup, peptide,
                            transitionGroupDocNode.TransitionGroup, transition.Transition);
                        newTransitions.Add(transition.ChangeQuantitative(set.Contains(identityPath)));
                    }

                    if (newTransitions.SequenceEqual(transitionGroupDocNode.Transitions))
                    {
                        newTransitionGroups.Add(transitionGroupDocNode);
                    }
                    else
                    {
                        newTransitionGroups.Add(
                            (TransitionGroupDocNode)transitionGroupDocNode.ChangeChildren(newTransitions));
                    }
                }

                if (peptideDocNode.TransitionGroups.SequenceEqual(newTransitionGroups))
                {
                    return doc;
                }

                message = peptideDocNode.ModifiedSequenceDisplay;
                return (SrmDocument)doc.ReplaceChild(new IdentityPath(peptideGroup),
                    peptideDocNode.ChangeChildren(newTransitionGroups));
            }, docPair => AuditLogEntry.DiffDocNodes(MessageType.changed_quantitative, docPair, message));
        }

        public CalibrationGraphControl CalibrationGraphControl
        {
            get { return calibrationGraphControl1; }
        }

        public override bool IsComplete
        {
            get
            {
                return base.IsComplete && !_updateTransitionPending && Equals(_selection, _detailsSelection);
            }
        }
    }
}
    
