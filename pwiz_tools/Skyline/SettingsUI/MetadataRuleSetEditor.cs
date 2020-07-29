using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class MetadataRuleSetEditor : FormEx
    {
        private SkylineDataSchema _dataSchema;
        private MetadataRuleSet _ruleSet;
        private bool _inChangeRuleSet;
        private MetadataExtractor _metadataExtractor;
        private ImmutableList<MetadataRuleSet> _existing;
        private string _originalName;
        public MetadataRuleSetEditor(IDocumentContainer documentContainer, MetadataRuleSet ruleSet, IEnumerable<MetadataRuleSet> existing)
        {
            InitializeComponent();
            DocumentContainer = documentContainer;
            _originalName = ruleSet.Name;
            _dataSchema = new SkylineDataSchema(documentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(ExtractedMetadataResultRow));
            var viewInfo = new ViewInfo(rootColumn, GetDefaultViewSpec());
            var skylineViewContext= new SkylineViewContext(rootColumn, new StaticRowSource(new ExtractedMetadataRuleResult[0]));
            bindingListSource1.SetViewContext(skylineViewContext, viewInfo);
            _metadataExtractor = new MetadataExtractor(_dataSchema, typeof(ResultFile));
            RuleSet = ruleSet;
            _existing = ImmutableList.ValueOfOrEmpty(existing);
        }

        public IDocumentContainer DocumentContainer { get; private set; }

        public MetadataRuleSet RuleSet
        {
            get
            {
                return _ruleSet.ChangeName(tbxName.Text);
            }
            set
            {
                bool inChangeRuleOld = _inChangeRuleSet;
                try
                {
                    _inChangeRuleSet = true;
                    _ruleSet = value;
                    tbxName.Text = _ruleSet.Name;
                    UpdateRuleSet();
                }
                finally
                {
                    _inChangeRuleSet = inChangeRuleOld;
                }
            }
        }

        private void MoveRules(bool upwards)
        {
            var selectedIndexes = listViewRules.SelectedIndices.Cast<int>().ToArray();
            RuleSet = RuleSet.ChangeRules(ListViewHelper.MoveItems(RuleSet.Rules, selectedIndexes, upwards));
            ListViewHelper.SelectIndexes(listViewRules, ListViewHelper.MoveSelectedIndexes(listViewRules.Items.Count, selectedIndexes, upwards));
        }

        private void BtnUpOnClick(object sender, EventArgs e)
        {
            MoveRules(true);
        }

        private void BtnDownOnClick(object sender, EventArgs e)
        {
            MoveRules(false);
        }

        private void UpdateRuleSet()
        {
            ListViewHelper.ReplaceItems(listViewRules, RuleSet.Rules.Select(MakeListViewItem).ToList());
            var rules = new List<ResolvedMetadataRule>();
            foreach (var rule in RuleSet.Rules)
            {
                rules.Add(_metadataExtractor.ResolveRule(rule));
            }

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
                    row.AddRuleResult(columnKey, _metadataExtractor.ApplyRule(resultFile, rule));
                }
                rows.Add(row);
            }
            bindingListSource1.RowSource = new StaticRowSource(rows);
            UpdateButtons();
        }

        public void UpdateButtons()
        {
            btnUp.Enabled = ListViewHelper.IsMoveUpEnabled(listViewRules);
            btnDown.Enabled = ListViewHelper.IsMoveDownEnabled(listViewRules);
            btnRemove.Enabled = listViewRules.SelectedIndices.Count != 0;
            btnEdit.Enabled = listViewRules.SelectedIndices.Count == 1;
        }

        public ListViewItem MakeListViewItem(MetadataRule rule)
        {
            ListViewItem item = new ListViewItem(GetColumnDescription(rule.Source));
            item.SubItems.Add(new ListViewItem.ListViewSubItem(item, rule.Pattern));
            item.SubItems.Add(new ListViewItem.ListViewSubItem(item, rule.Replacement));
            item.SubItems.Add(new ListViewItem.ListViewSubItem(item, GetColumnDescription(rule.Target)));
            return item;
        }

        public String GetColumnDescription(PropertyPath column)
        {
            if (column == null)
            {
                return string.Empty;
            }

            TextColumnWrapper textColumn = _metadataExtractor.FindColumn(column);
            if (textColumn != null)
            {
                return textColumn.DisplayName;
            }

            return column.ToString();
        }

        public static ViewSpec GetDefaultViewSpec()
        {
            var columns = new List<ColumnSpec>();
            columns.Add(new ColumnSpec(PropertyPath.Root.Property(nameof(ExtractedMetadataResultRow.SourceObject))).SetCaption(ColumnCaptions.ResultFile));
            columns.Add(new ColumnSpec(
                PropertyPath.Root.Property(nameof(ExtractedMetadataResultRow.Values)).LookupAllItems()
                    .Property("Value").Property(nameof(ExtractedMetadataResultColumn.Value))
            ));
            return new ViewSpec().SetColumns(columns);
        }

        private void btnAddNewRule_Click(object sender, EventArgs e)
        {
            var newRule = ShowRuleEditor(null);
            if (newRule != null)
            {
                RuleSet = RuleSet.ChangeRules(RuleSet.Rules.Append(newRule));
            }
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
            if (ruleIndex < 0 || ruleIndex >= RuleSet.Rules.Count)
            {
                return;
            }

            var newRule = ShowRuleEditor(RuleSet.Rules[ruleIndex]);
            if (newRule == null)
            {
                return;
            }

            RuleSet = RuleSet.ChangeRules(RuleSet.Rules.ReplaceAt(ruleIndex, newRule));
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            string name;
            if (!helper.ValidateNameTextBox(tbxName, out name))
            {
                return;
            }

            if (name != _originalName && _existing.Any(ruleSet=>ruleSet.Name == name))
            {
                helper.ShowTextBoxError(tbxName, string.Format("There is already a metadata rule set named '{0}'.", name));
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            var selectedIndices = listViewRules.SelectedIndices.Cast<int>().ToHashSet();
            RuleSet = RuleSet.ChangeRules(Enumerable.Range(0, RuleSet.Rules.Count)
                .Where(i => !selectedIndices.Contains(i)).Select(i => RuleSet.Rules[i]));
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditRule(listViewRules.SelectedIndices.Cast<int>().FirstOrDefault());
        }

        private void listViewRules_ItemActivate(object sender, EventArgs e)
        {
            EditRule(listViewRules.FocusedItem.Index);
        }

        private void listViewRules_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }
    }
}
