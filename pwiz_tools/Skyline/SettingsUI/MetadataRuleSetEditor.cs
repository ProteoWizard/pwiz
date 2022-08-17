/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.UserInterfaces;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class MetadataRuleSetEditor : FormEx
    {
        private SkylineDataSchema _dataSchema;
        private List<RuleRow> _ruleRowList;
        private bool _inChangeRuleSet;
        private MetadataExtractor _metadataExtractor;
        private ImmutableList<MetadataRuleSet> _existing;
        private string _originalName;
        public MetadataRuleSetEditor(IDocumentContainer documentContainer, MetadataRuleSet metadataRuleSet, IEnumerable<MetadataRuleSet> existing)
        {
            InitializeComponent();
            DocumentContainer = documentContainer;
            metadataRuleSet = metadataRuleSet ?? new MetadataRuleSet(typeof(ResultFile));
            _originalName = metadataRuleSet.Name;
            _dataSchema = new SkylineDataSchema(documentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(ExtractedMetadataResultRow));
            var viewInfo = new ViewInfo(rootColumn, GetDefaultViewSpec());
            var skylineViewContext= new MetadataResultViewContext(new GraphicalUserInterface(this), rootColumn, new StaticRowSource(new MetadataStepResult[0]));
            bindingListSourceResults.SetViewContext(skylineViewContext, viewInfo);
            _metadataExtractor = new MetadataExtractor(_dataSchema, typeof(ResultFile));
            _ruleRowList = new List<RuleRow>();
            bindingSourceRules.DataSource = new BindingList<RuleRow>(_ruleRowList);
            MetadataRuleSet = metadataRuleSet;
            _existing = ImmutableList.ValueOfOrEmpty(existing);
        }

        public IDocumentContainer DocumentContainer { get; private set; }

        public MetadataRuleSet MetadataRuleSet
        {
            get
            {
                return new MetadataRuleSet(typeof(ResultFile))
                    .ChangeName(tbxName.Text)
                    .ChangeRules(Enumerable.Range(0, StepCount).Select(i=>_ruleRowList[i].Rule));
            }
            set
            {
                bool inChangeRuleOld = _inChangeRuleSet;
                try
                {
                    _inChangeRuleSet = true;
                    _ruleRowList.Clear();
                    _ruleRowList.AddRange(value.Rules.Select(rule=>new RuleRow(rule)));
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
            var ruleSet = MetadataRuleSet;
            int ruleCount = ruleSet.Rules.Count;
            ruleSet = ruleSet.ChangeRules(ListViewHelper.MoveItems(ruleSet.Rules, selectedIndexes, upwards));
            MetadataRuleSet = ruleSet;
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
            var rules = MetadataRuleSet.Rules.Select(rule => _metadataExtractor.ResolveStep(rule, null)).ToList();
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
            if (rows.Count == 0)
            {
                splitContainer1.Panel2Collapsed = true;
            }
            else
            {
                splitContainer1.Panel2Collapsed = false;
            }
            UpdateButtons();
        }

        public void UpdateButtons()
        {
            var rowIndexes = GetSelectedRuleRowIndexes().ToList();
            btnUp.Enabled = ListViewHelper.IsMoveEnabled(_ruleRowList.Count, rowIndexes, true);
            btnDown.Enabled = ListViewHelper.IsMoveEnabled(_ruleRowList.Count, rowIndexes, false);
            btnEdit.Enabled = rowIndexes.Count <= 1;
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
                PropertyPath.Root.Property(nameof(ExtractedMetadataResultRow.Values))
                    .DictionaryValues().Property(nameof(ExtractedMetadataResultColumn.ExtractedValue))
            ));
            return new ViewSpec().SetColumns(columns);
        }

        public MetadataRule ShowRuleEditor(MetadataRule rule)
        {
            using (var dlg = new MetadataRuleEditor(DocumentContainer))
            {
                if (rule != null)
                {
                    dlg.MetadataRule = rule;
                }
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    return dlg.MetadataRule;
                }
            }

            return null;
        }

        public void EditRule(int ruleIndex)
        {
            if (ruleIndex < 0 || ruleIndex > MetadataRuleSet.Rules.Count)
            {
                return;
            }

            bool appendRule = ruleIndex == MetadataRuleSet.Rules.Count;

            var ruleStep = appendRule ? CreateNewStep():
                MetadataRuleSet.Rules[ruleIndex];
            var newRule = ShowRuleEditor(ruleStep);
            if (newRule == null)
            {
                return;
            }

            MetadataRuleSet = MetadataRuleSet.ChangeRules(appendRule
                ? MetadataRuleSet.Rules.Append(newRule)
                : MetadataRuleSet.Rules.ReplaceAt(ruleIndex, newRule));
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
                helper.ShowTextBoxError(tbxName, string.Format(Resources.MetadataRuleEditor_OkDialog_There_is_already_a_metadata_rule_named___0___, name));
                return;
            }

            var ruleSet = MetadataRuleSet;
            for (int rowIndex = 0; rowIndex < ruleSet.Rules.Count; rowIndex++)
            {
                var rule = ruleSet.Rules[rowIndex];
                if (rule.Source == null)
                {
                    MessageDlg.Show(this, string.Format(Resources.MetadataRuleEditor_OkDialog__0__cannot_be_blank, colSource.HeaderText));
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
                        MessageDlg.ShowWithException(this, Resources.MetadataRuleEditor_OkDialog_This_is_not_a_valid_regular_expression_, exception);
                        SelectCell(dataGridViewRules, colPattern, rowIndex);
                        return;
                    }
                }

                if (rule.Target == null)
                {
                    MessageDlg.Show(this, string.Format(Resources.MetadataRuleEditor_OkDialog__0__cannot_be_blank, colTarget.HeaderText));
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
            var ruleSet = MetadataRuleSet;
            MetadataRuleSet = ruleSet.ChangeRules(Enumerable.Range(0, ruleSet.Rules.Count)
                .Where(i => !selectedIndices.Contains(i)).Select(i => ruleSet.Rules[i]));
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditRule(dataGridViewRules.CurrentCellAddress.Y);
        }

        private static MetadataRule CreateNewStep()
        {
            return new MetadataRule().ChangeSource(PropertyPath.Root.Property(nameof(ResultFile.FileName)));
        }

        public class RuleRow
        {
            [UsedImplicitly]
            public RuleRow() : this(CreateNewStep())
            {

            }
            public RuleRow(MetadataRule rule)
            {
                Rule = rule;
            }

            public MetadataRule Rule { get; private set; }

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

            public string Pattern
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

        public DataGridView DataGridViewSteps => dataGridViewRules;

        public DataGridViewComboBoxColumn ColumnSource => colSource;

        public DataGridViewComboBoxColumn ColumnTarget => colTarget;

        public DataGridViewTextBoxColumn ColumnPattern => colPattern;

        public DataGridViewTextBoxColumn ColumnReplacement => colReplacement;


        public BoundDataGridView PreviewGrid
        {
            get
            {
                return boundDataGridViewEx1;
            }
        }

        private class MetadataResultViewContext : SkylineViewContext
        {
            private static readonly PropertyPath propertyPathValues = PropertyPath.Root
                .Property(nameof(ExtractedMetadataResultRow.Values))
                .LookupAllItems();
            private static readonly PropertyPath propertyPathNewValue = propertyPathValues
                .Property(nameof(KeyValuePair<string, object>.Value))
                .Property(nameof(ExtractedMetadataResultColumn.ExtractedValue));
            public MetadataResultViewContext(IUserInterface userInterface, ColumnDescriptor parentColumn, IRowSource rowSource) : base(userInterface, parentColumn,
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

        public string RuleName
        {
            get
            {
                return tbxName.Text;
            }
            set
            {
                tbxName.Text = value;
            }
        }
    }
}
