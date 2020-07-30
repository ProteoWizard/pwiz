using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class MetadataRuleEditor : FormEx
    {
        private SkylineDataSchema _dataSchema;
        private List<RuleRow> _ruleRowList;
        private bool _inChangeRuleSet;
        private MetadataExtractor _metadataExtractor;
        private ImmutableList<MetadataRule> _existing;
        private string _originalName;
        public MetadataRuleEditor(IDocumentContainer documentContainer, MetadataRule metadataRule, IEnumerable<MetadataRule> existing)
        {
            InitializeComponent();
            DocumentContainer = documentContainer;
            _originalName = metadataRule.Name;
            _dataSchema = new SkylineDataSchema(documentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(ExtractedMetadataResultRow));
            var viewInfo = new ViewInfo(rootColumn, GetDefaultViewSpec());
            var skylineViewContext= new MetadataResultViewContext(rootColumn, new StaticRowSource(new MetadataStepResult[0]));
            bindingListSourceResults.SetViewContext(skylineViewContext, viewInfo);
            _metadataExtractor = new MetadataExtractor(_dataSchema, typeof(ResultFile));
            _ruleRowList = new List<RuleRow>();
            bindingSourceRules.DataSource = new BindingList<RuleRow>(_ruleRowList);
            MetadataRule = metadataRule;
            _existing = ImmutableList.ValueOfOrEmpty(existing);
        }

        public IDocumentContainer DocumentContainer { get; private set; }

        public MetadataRule MetadataRule
        {
            get
            {
                return new MetadataRule(typeof(ResultFile))
                    .ChangeName(tbxName.Text)
                    .ChangeSteps(Enumerable.Range(0, StepCount).Select(i=>_ruleRowList[i].Rule));
            }
            set
            {
                bool inChangeRuleOld = _inChangeRuleSet;
                try
                {
                    _inChangeRuleSet = true;
                    _ruleRowList.Clear();
                    _ruleRowList.AddRange(value.Steps.Select(rule=>new RuleRow(rule)));
                    tbxName.Text = value.Name;
                    UpdateRuleSet();
                    bindingSourceRules.ResetBindings(false);
                }
                finally
                {
                    _inChangeRuleSet = inChangeRuleOld;
                }
            }
        }

        public int StepCount
        {
            get
            {
                int ruleCount = _ruleRowList.Count;
                if (ruleCount > 0 && dataGridViewRules.RowCount >= ruleCount && 
                    dataGridViewRules.Rows[ruleCount - 1].IsNewRow)
                {
                    ruleCount--;
                }

                return ruleCount;
            }
        }

        private void MoveSteps(bool upwards)
        {
            var selectedIndexes = GetSelectedRuleRowIndexes().ToList();
            var ruleSet = MetadataRule;
            int ruleCount = ruleSet.Steps.Count;
            ruleSet = ruleSet.ChangeSteps(ListViewHelper.MoveItems(ruleSet.Steps, selectedIndexes, upwards));
            MetadataRule = ruleSet;
            var newSelectedIndexes = ListViewHelper.MoveSelectedIndexes(ruleCount, selectedIndexes, upwards).ToList();
            SelectRows(dataGridViewRules, newSelectedIndexes);
            var currentCell = dataGridViewRules.CurrentCellAddress;
            if (!newSelectedIndexes.Contains(currentCell.Y))
            {
                dataGridViewRules.CurrentCell = dataGridViewRules.Rows[newSelectedIndexes.First()].Cells[currentCell.X];
            }
        }

        private void BtnUpOnClick(object sender, EventArgs e)
        {
            MoveSteps(true);
        }

        private void BtnDownOnClick(object sender, EventArgs e)
        {
            MoveSteps(false);
        }

        public void UpdateRuleSet()
        {
            var emptyTuple = Tuple.Create(string.Empty, (PropertyPath) null);
            colTarget.DisplayMember = colSource.DisplayMember = nameof(emptyTuple.Item1);
            colTarget.ValueMember = colSource.ValueMember = nameof(emptyTuple.Item2);
            var sources = _metadataExtractor.GetSourceColumns().Select(col => Tuple.Create(col.DisplayName, col.PropertyPath))
                .ToList();
            var sourceNames = sources.Select(tuple => tuple.Item2).ToHashSet();
            var targets = _metadataExtractor.GetTargetColumns()
                .Select(col => Tuple.Create(col.DisplayName, col.PropertyPath)).ToList();
            var targetNames = targets.Select(tuple => tuple.Item2).ToHashSet();
            foreach (var rule in _ruleRowList)
            {
                if (rule.Source != null && sourceNames.Add(rule.Source))
                {
                    sources.Add(Tuple.Create(rule.Source.ToString(), rule.Source));
                }

                if (rule.Target != null && targetNames.Add(rule.Target))
                {
                    targets.Add(Tuple.Create(rule.Target.ToString(), rule.Target));
                }
            }
            sources.Insert(0, emptyTuple);
            targets.Insert(0, emptyTuple);

            if (!sources.SequenceEqual(colSource.Items.Cast<object>()))
            {
                colSource.Items.Clear();
                colSource.Items.AddRange(sources.ToArray());
            }

            if (!targets.SequenceEqual(colTarget.Items.Cast<object>()))
            {
                colTarget.Items.Clear();
                colTarget.Items.AddRange(targets.ToArray());
            }
            UpdateResults();
        }

        public void UpdateResults()
        {
            var rules = MetadataRule.Steps.Select(rule => _metadataExtractor.ResolveStep(rule, null)).ToList();
            var rows = new List<ExtractedMetadataResultRow>();
            foreach (var resultFile in _dataSchema.ResultFileList.Values)
            {
                var row = new ExtractedMetadataResultRow(resultFile);
                foreach (var rule in rules)
                {
                    ExtractedMetadataResultRow.ColumnKey columnKey = null;
                    if (rule.Target != null)
                    {
                        columnKey = new ExtractedMetadataResultRow.ColumnKey(rule.Target.PropertyPath, rule.Target.DisplayName);
                    }
                    row.AddRuleResult(columnKey, _metadataExtractor.ApplyStep(resultFile, rule));
                }
                rows.Add(row);
            }
            bindingListSourceResults.RowSource = new StaticRowSource(rows);
            UpdateButtons();
        }

        public void UpdateButtons()
        {
            var rowIndexes = GetSelectedRuleRowIndexes().ToList();
            btnUp.Enabled = ListViewHelper.IsMoveEnabled(_ruleRowList.Count, rowIndexes, true);
            btnDown.Enabled = ListViewHelper.IsMoveEnabled(_ruleRowList.Count, rowIndexes, false);
            btnEdit.Enabled = rowIndexes.Count == 1;
            btnRemove.Enabled = rowIndexes.Count > 0;
        }

        public IEnumerable<int> GetSelectedRuleRowIndexes()
        {
            int ruleCount = StepCount;
            return dataGridViewRules.SelectedRows.OfType<DataGridViewRow>()
                .Select(row => row.Index)
                .Where(i => i < ruleCount);
        }

        public static ViewSpec GetDefaultViewSpec()
        {
            var columns = new List<ColumnSpec>();
            columns.Add(new ColumnSpec(PropertyPath.Root.Property(nameof(ExtractedMetadataResultRow.SourceObject))).SetCaption(ColumnCaptions.ResultFile));
            columns.Add(new ColumnSpec(
                PropertyPath.Root.Property(nameof(ExtractedMetadataResultRow.Values)).LookupAllItems()
                    .Property("Value").Property(nameof(ExtractedMetadataResultColumn.ExtractedValue))
            ));
            return new ViewSpec().SetColumns(columns);
        }

        private void btnAddNewRule_Click(object sender, EventArgs e)
        {
            var newRule = ShowRuleEditor(null);
            if (newRule != null)
            {
                MetadataRule = MetadataRule.ChangeSteps(MetadataRule.Steps.Append(newRule));
            }
        }

        public MetadataRuleStep ShowRuleEditor(MetadataRuleStep rule)
        {
            using (var dlg = new MetadataRuleStepEditor(DocumentContainer))
            {
                if (rule != null)
                {
                    dlg.MetadataRuleStep = rule;
                }
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    return dlg.MetadataRuleStep;
                }
            }

            return null;
        }

        public void EditRule(int ruleIndex)
        {
            if (ruleIndex < 0 || ruleIndex >= MetadataRule.Steps.Count)
            {
                return;
            }

            var newRule = ShowRuleEditor(MetadataRule.Steps[ruleIndex]);
            if (newRule == null)
            {
                return;
            }

            MetadataRule = MetadataRule.ChangeSteps(MetadataRule.Steps.ReplaceAt(ruleIndex, newRule));
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            string name;
            if (!helper.ValidateNameTextBox(tbxName, out name))
            {
                return;
            }

            if (name != _originalName && _existing.Any(existingRuleSet=>existingRuleSet.Name == name))
            {
                helper.ShowTextBoxError(tbxName, string.Format("There is already a metadata rule set named '{0}'.", name));
                return;
            }

            var ruleSet = MetadataRule;
            for (int rowIndex = 0; rowIndex < ruleSet.Steps.Count; rowIndex++)
            {
                var rule = ruleSet.Steps[rowIndex];
                if (rule.Source == null)
                {
                    MessageDlg.Show(this, string.Format("{0} cannot be blank", colSource.HeaderText));
                    SelectCell(dataGridViewRules, colSource, rowIndex);
                    return;
                }

                if (!string.IsNullOrEmpty(rule.Pattern))
                {
                    try
                    {
                        var _ = new Regex(rule.Pattern);
                    }
                    catch (Exception exception)
                    {
                        MessageDlg.ShowWithException(this, "This is not a valid regular expression.", exception);
                        SelectCell(dataGridViewRules, colFilter, rowIndex);
                        return;
                    }
                }

                if (rule.Target == null)
                {
                    MessageDlg.Show(this, string.Format("{0} cannot be blank", colTarget.HeaderText));
                    SelectCell(dataGridViewRules, colTarget, rowIndex);
                    return;
                }
            }


            DialogResult = DialogResult.OK;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            var selectedIndices = GetSelectedRuleRowIndexes().ToHashSet();
            var ruleSet = MetadataRule;
            MetadataRule = ruleSet.ChangeSteps(Enumerable.Range(0, ruleSet.Steps.Count)
                .Where(i => !selectedIndices.Contains(i)).Select(i => ruleSet.Steps[i]));
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditRule(dataGridViewRules.CurrentCellAddress.Y);
        }

        public class RuleRow
        {
            [UsedImplicitly]
            public RuleRow() : this(new MetadataRuleStep().ChangeSource(PropertyPath.Root.Property(nameof(ResultFile.FileName))))
            {

            }
            public RuleRow(MetadataRuleStep rule)
            {
                Rule = rule;
            }

            public MetadataRuleStep Rule { get; private set; }

            public PropertyPath Source
            {
                get
                {
                    return Rule.Source;
                }
                set
                {
                    Rule = Rule.ChangeSource(value);
                }
            }

            public string Filter
            {
                get
                {
                    return Rule.Pattern;
                }
                set
                {
                    Rule = Rule.ChangePattern(value);
                }

            }

            public string Replacement
            {
                get
                {
                    return Rule.Replacement;
                }
                set
                {
                    Rule = Rule.ChangeReplacement(value);
                }
            }

            public PropertyPath Target
            {
                get
                {
                    return Rule.Target;
                }
                set
                {
                    Rule = Rule.ChangeTarget(value);
                }
            }
        }

        private void bindingSourceRules_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (_inChangeRuleSet)
            {
                return;
            }
            UpdateResults();
        }

        private void dataGridViewRules_CurrentCellChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private class MetadataResultViewContext : SkylineViewContext
        {
            private static readonly PropertyPath propertyPathValues = PropertyPath.Root
                .Property(nameof(ExtractedMetadataResultRow.Values))
                .LookupAllItems();
            private static readonly PropertyPath propertyPathNewValue = propertyPathValues
                .Property(nameof(KeyValuePair<string, object>.Value))
                .Property(nameof(ExtractedMetadataResultColumn.ExtractedValue));
            public MetadataResultViewContext(ColumnDescriptor parentColumn, IRowSource rowSource) : base(parentColumn,
                rowSource)
            {
            }

            protected override TColumn InitializeColumn<TColumn>(TColumn column, PropertyDescriptor propertyDescriptor)
            {
                TColumn result = base.InitializeColumn(column, propertyDescriptor);
                var columnPropertyDescriptor = propertyDescriptor as ColumnPropertyDescriptor;
                // If we are showing the ExtractedValue column, change its caption to be just the DisplayName of the
                // column it is going into
                if (columnPropertyDescriptor != null &&
                    Equals(columnPropertyDescriptor.DisplayColumn.PropertyPath, propertyPathNewValue))
                {
                    var columnKey = columnPropertyDescriptor.PivotKey.FindValue(propertyPathValues) 
                        as ExtractedMetadataResultRow.ColumnKey;
                    if (columnKey != null)
                    {
                        column.HeaderText = columnKey.DisplayName;
                    }
                }
                return result;
            }
        }

        private void dataGridViewRules_SelectionChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        public static void SelectCell(DataGridView dataGridView, DataGridViewColumn column, int rowIndex)
        {
            SelectRows(dataGridView, ImmutableList.Singleton(rowIndex));
            dataGridView.CurrentCell = dataGridView.Rows[rowIndex].Cells[column.Index];
            dataGridView.Focus();
        }

        public static void SelectRows(DataGridView dataGridView, ICollection<int> newRowIndexes)
        {
            var oldRowIndexes = dataGridView.SelectedRows.OfType<DataGridViewRow>().Select(row => row.Index).ToList();
            foreach (var rowIndex in oldRowIndexes.Except(newRowIndexes))
            {
                dataGridView.Rows[rowIndex].Selected = false;
            }

            foreach (var rowIndex in newRowIndexes.Except(oldRowIndexes))
            {
                dataGridView.Rows[rowIndex].Selected = true;
            }
        }
    }
}
