using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class DefineExtractedMetadataDlg : Form
    {
        private SkylineDataSchema _dataSchema;

        public DefineExtractedMetadataDlg(IDocumentContainer documentContainer)
        {
            InitializeComponent();
            _dataSchema = new SkylineDataSchema(documentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var viewContext = new SkylineViewContext(_dataSchema, new RowSourceInfo[]
            {
                new RowSourceInfo(typeof(ExtractedMetadataResultRow), new StaticRowSource(new ExtractedMetadataResultRow[0]), new []{GetDefaultViewInfo(MetadataExtractor.Rule.EMPTY, new List<ExtractedMetadataResultRow>())}), 
            });
            bindingListSource1.SetViewContext(viewContext);
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(ResultFile));
            var allColumns = GetAllTextColumnWrappers(rootColumn).ToList();
            var sources = allColumns.Where(IsSource).ToArray();
            comboSourceText.Items.AddRange(sources);
            comboMetadataTarget.Items.AddRange(allColumns.Where(IsTarget).ToArray());
            comboSourceText.SelectedIndex = sources.IndexOf(item =>
                item.PropertyPath.Equals(PropertyPath.Root.Property(nameof(ResultFile.FileName))));
            FormatCultureInfo = CultureInfo.InvariantCulture;
        }

        public CultureInfo FormatCultureInfo { get; set; }

        public void UpdateRows()
        {
            var rule = new ExtractedMetadataRule();
            var sourceItem = comboSourceText.SelectedItem as TextColumnWrapper;
            if (sourceItem != null)
            {
                rule = rule.ChangeSourceColumn(sourceItem.PropertyPath.ToString());
            }

            if (!string.IsNullOrEmpty(tbxRegularExpression.Text))
            {
                rule = rule.ChangeMatchRegularExpression(tbxRegularExpression.Text);
            }
            var targetItem = comboMetadataTarget.SelectedItem as TextColumnWrapper;
            if (targetItem != null)
            {
                rule = rule.ChangeTargetColumn(targetItem.PropertyPath.ToString());
            }
            var ruleSet = new ExtractedMetadataRuleSet(typeof(ResultFile).FullName, new []{rule});
            var metadataExtractor = new MetadataExtractor(_dataSchema, typeof(ResultFile), ruleSet);
            var resolvedRule = metadataExtractor.ResolveRule(rule);
            var rows = new List<ExtractedMetadataResultRow>();
            foreach (var resultFile in _dataSchema.ResultFileList.Values)
            {
                var row = new ExtractedMetadataResultRow(resultFile);
                var result = metadataExtractor.ApplyRule(resultFile, resolvedRule);
                row.AddRuleResult(null, result);
                rows.Add(row);
            }

            var columns = new List<ColumnSpec>();
            var ruleResults = PropertyPath.Root.Property(nameof(ExtractedMetadataResultRow.RuleResults))
                .LookupAllItems();
            columns.Add(new ColumnSpec(PropertyPath.Root.Property(nameof(ExtractedMetadataResultRow.SourceObject))).SetCaption(ColumnCaptions.ResultFile));
            if (resolvedRule.Source != null)
            {
                columns.Add(new ColumnSpec(ruleResults.Property(nameof(ExtractedMetadataRuleResult.Source))).SetCaption(resolvedRule.Source.DisplayName));
            }

            if (rows.Any(row => !row.RuleResults.FirstOrDefault()?.Match == true))
            {
                columns.Add(new ColumnSpec(ruleResults.Property(nameof(ExtractedMetadataRuleResult.Match))));
            }

            columns.Add(new ColumnSpec(ruleResults.Property(nameof(ExtractedMetadataRuleResult.ExtractedText))));
            if (resolvedRule.Target != null)
            {
                columns.Add(new ColumnSpec(ruleResults.Property(nameof(ExtractedMetadataRuleResult.TargetValue))).SetCaption(resolvedRule.Target.DisplayName));
            }

            var viewInfo = GetDefaultViewInfo(resolvedRule, rows);
            bindingListSource1.SetView(viewInfo, new StaticRowSource(rows));
        }

        public ViewInfo GetDefaultViewInfo(MetadataExtractor.Rule resolvedRule, ICollection<ExtractedMetadataResultRow> rows)
        {
            var columns = new List<ColumnSpec>();
            var ruleResults = PropertyPath.Root.Property(nameof(ExtractedMetadataResultRow.RuleResults))
                .LookupAllItems();
            columns.Add(new ColumnSpec(PropertyPath.Root.Property(nameof(ExtractedMetadataResultRow.SourceObject))).SetCaption(ColumnCaptions.ResultFile));
            if (resolvedRule.Source != null)
            {
                columns.Add(new ColumnSpec(ruleResults.Property(nameof(ExtractedMetadataRuleResult.Source))).SetCaption(resolvedRule.Source.DisplayName));
            }

            if (rows.Any(row => !row.RuleResults.FirstOrDefault()?.Match == true))
            {
                columns.Add(new ColumnSpec(ruleResults.Property(nameof(ExtractedMetadataRuleResult.Match))));
            }

            columns.Add(new ColumnSpec(ruleResults.Property(nameof(ExtractedMetadataRuleResult.ExtractedText))));
            if (resolvedRule.Target != null)
            {
                columns.Add(new ColumnSpec(ruleResults.Property(nameof(ExtractedMetadataRuleResult.TargetValue))).SetCaption(resolvedRule.Target.DisplayName));
            }

            var viewSpec = new ViewSpec().SetColumns(columns).SetSublistId(ruleResults);
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(ExtractedMetadataResultRow));
            return new ViewInfo(rootColumn, viewSpec);
        }

        public void ShowRegexError(Exception e)
        {
            if (e == null)
            {
                tbxRegularExpression.BackColor = SystemColors.Window;
            }
            else
            {
                tbxRegularExpression.BackColor = Color.Red;
            }
        }

        private void tbxRegularExpression_Leave(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void comboSourceText_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void linkLabelRegularExpression_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenRegexDocLink(this);
        }

        public IEnumerable<TextColumnWrapper> GetAllTextColumnWrappers(ColumnDescriptor columnDescriptor)
        {
            var columns = Enumerable.Empty<TextColumnWrapper>();
            if (columnDescriptor.CollectionInfo != null || columnDescriptor.IsAdvanced)
            {
                return columns;
            }

            if (!typeof(SkylineObject).IsAssignableFrom(columnDescriptor.PropertyType)
                && !@"Locator".Equals(columnDescriptor.PropertyPath.Name))
            {
                columns = columns.Append(new TextColumnWrapper(columnDescriptor));
            }

            columns = columns.Concat(columnDescriptor.GetChildColumns().SelectMany(GetAllTextColumnWrappers));

            return columns;
        }

        public bool IsSource(TextColumnWrapper column)
        {
            return !IsTarget(column);
        }

        public bool IsTarget(TextColumnWrapper column)
        {
            if (column.IsImportable)
            {
                return true;
            }

            if (column.PropertyPath.Name.StartsWith(AnnotationDef.ANNOTATION_PREFIX))
            {
                return true;
            }

            return false;
        }

        private void comboMetadataTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void dataGridView1_RowErrorTextNeeded(object sender, DataGridViewRowErrorTextNeededEventArgs e)
        {
        }

        private void dataGridView1_CellErrorTextNeeded(object sender, DataGridViewCellErrorTextNeededEventArgs e)
        {
            var column = boundDataGridView1.Columns[e.ColumnIndex];
            var propertyDescriptor =
                bindingListSource1.ItemProperties.FirstOrDefault(pd => pd.Name == column.DataPropertyName) as ColumnPropertyDescriptor;
            var parentColumn = propertyDescriptor?.DisplayColumn?.ColumnDescriptor?.Parent;
            if (parentColumn == null || parentColumn.PropertyType != typeof(ExtractedMetadataRuleResult))
            {
                return;
            }

            if (propertyDescriptor.PropertyPath.Name != nameof(ExtractedMetadataRuleResult.ExtractedText))
            {
                return;
            }

            var ruleResult =
                parentColumn.GetPropertyValue((RowItem) bindingListSource1[e.RowIndex], null) as
                    ExtractedMetadataRuleResult;
            e.ErrorText = ruleResult?.ErrorText;
        }
    }
}
