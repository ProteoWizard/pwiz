using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class MetadataRuleSetEditor : FormEx
    {
        private SkylineDataSchema _dataSchema;
        private ExtractedMetadataRuleSet _ruleSet;
        private bool _inChangeRuleSet;
        private MetadataExtractor _metadataExtractor;
        public MetadataRuleSetEditor(IDocumentContainer documentContainer)
        {
            InitializeComponent();
            DocumentContainer = documentContainer;
            _dataSchema = new SkylineDataSchema(documentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(ExtractedMetadataResultRow));
            var viewInfo = new ViewInfo(rootColumn, GetDefaultViewSpec());
            var skylineViewContext= new SkylineViewContext(rootColumn, new StaticRowSource(new ExtractedMetadataRuleResult[0]));
            bindingListSource1.SetViewContext(skylineViewContext, viewInfo);
            _metadataExtractor = new MetadataExtractor(_dataSchema, typeof(ResultFile));
            RuleSet = new ExtractedMetadataRuleSet(typeof(ResultFile).FullName, new ExtractedMetadataRule[0]);
        }

        public IDocumentContainer DocumentContainer { get; private set; }

        public ExtractedMetadataRuleSet RuleSet
        {
            get
            {
                return _ruleSet;
            }
            set
            {
                bool inChangeRuleOld = _inChangeRuleSet;
                try
                {
                    _inChangeRuleSet = true;
                    _ruleSet = value;
                    UpdateRuleSet();
                }
                finally
                {
                    _inChangeRuleSet = inChangeRuleOld;
                }
            }
        }

        private void BtnUpOnClick(object sender, EventArgs e)
        {

        }

        private void BtnDownOnClick(object sender, EventArgs e)
        {

        }

        private void UpdateRuleSet()
        {
            ListViewHelper.ReplaceItems(listViewRules, RuleSet.Rules.Select(MakeListViewItem).ToList());
            var rules = new List<MetadataExtractor.Rule>();
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
        }

        public ListViewItem MakeListViewItem(ExtractedMetadataRule rule)
        {
            ListViewItem item = new ListViewItem(GetColumnDescription(rule.SourceColumn));
            item.SubItems.Add(new ListViewItem.ListViewSubItem(item, rule.MatchRegularExpression));
            item.SubItems.Add(new ListViewItem.ListViewSubItem(item, GetColumnDescription(rule.TargetColumn)));
            return item;
        }

        public String GetColumnDescription(string column)
        {
            if (string.IsNullOrEmpty(column))
            {
                return string.Empty;
            }

            TextColumnWrapper textColumn = _metadataExtractor.FindColumn(column);
            if (textColumn != null)
            {
                return textColumn.DisplayName;
            }

            return column;
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
            using (var dlg = new MetadataRuleEditor(DocumentContainer))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    RuleSet = RuleSet.ChangeRules(RuleSet.Rules.Append(dlg.ExtractedMetadataRule));
                }
            }
        }
    }
}
