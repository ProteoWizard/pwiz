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
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class MetadataRuleEditor : FormEx
    {
        private SkylineDataSchema _dataSchema;
        private MetadataExtractor _metadataExtractor;

        public MetadataRuleEditor(SrmDocument document)
        {
            InitializeComponent();
            _dataSchema = SkylineDataSchema.MemoryDataSchema(document, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(ResultFile));
            var viewContext =
                new SkylineViewContext(rootColumn, new StaticRowSource(new ExtractedMetadataResultRow[0]));
            _metadataExtractor = new MetadataExtractor(_dataSchema, typeof(ResultFile));
            bindingListSource1.SetViewContext(viewContext);
            var sources = _metadataExtractor.GetSourceColumns().ToArray();
            comboSourceText.Items.AddRange(sources);
            comboMetadataTarget.Items.AddRange(_metadataExtractor.GetTargetColumns().ToArray());
            SelectItem(comboSourceText, PropertyPath.Root.Property(nameof(ResultFile.FileName)));
            FormatCultureInfo = CultureInfo.InvariantCulture;
        }

        public MetadataRule MetadataRule
        {
            get
            {
                var rule = new MetadataRule();
                var source = comboSourceText.SelectedItem as TextColumnWrapper;
                if (source != null)
                {
                    rule = rule.ChangeSource(source.PropertyPath);
                }

                if (!string.IsNullOrEmpty(tbxRegularExpression.Text))
                {
                    rule = rule.ChangePattern(tbxRegularExpression.Text);
                }

                if (!string.IsNullOrEmpty(tbxReplacement.Text))
                {
                    rule = rule.ChangeReplacement(tbxReplacement.Text);
                }

                var target = comboMetadataTarget.SelectedItem as TextColumnWrapper;
                if (target != null)
                {
                    rule = rule.ChangeTarget(target.PropertyPath);
                }

                return rule;
            }
            set
            {
                SelectItem(comboSourceText, value.Source);
                tbxRegularExpression.Text = value.Pattern;
                tbxReplacement.Text = value.Replacement;
                SelectItem(comboMetadataTarget, value.Target);
            }
        }

        private void SelectItem(ComboBox combo, PropertyPath propertyPath)
        {
            if (propertyPath != null && !propertyPath.IsRoot)
            {
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    var item = (TextColumnWrapper)combo.Items[i];
                    if (Equals(item.PropertyPath, propertyPath))
                    {
                        combo.SelectedIndex = i;
                        return;
                    }
                }
            }
            combo.SelectedIndex = -1;
        }

        public CultureInfo FormatCultureInfo { get; set; }

        public void UpdateRows()
        {
            var errors = new List<CommonException<MetadataExtractor.StepError>>();
            var resolvedRule = _metadataExtractor.ResolveStep(MetadataRule, errors);
            var regexError =
                errors.FirstOrDefault(error => error.ExceptionDetail.Property == nameof(MetadataRule.Pattern));
            ShowRegexError(regexError);
            var rows = new List<ExtractedMetadataResultRow>();
            foreach (var resultFile in _dataSchema.ResultFileList.Values)
            {
                var row = new ExtractedMetadataResultRow(resultFile);
                var result = _metadataExtractor.ApplyStep(resultFile, resolvedRule);
                row.AddRuleResult(null, result);
                rows.Add(row);
            }
            var viewInfo = GetDefaultViewInfo(resolvedRule, rows);
            bindingListSource1.SetView(viewInfo, new StaticRowSource(rows));
        }

        public ViewInfo GetDefaultViewInfo(MetadataExtractor.Step step, ICollection<ExtractedMetadataResultRow> rows)
        {
            var columns = new List<ColumnSpec>();
            var ruleResults = PropertyPath.Root.Property(nameof(ExtractedMetadataResultRow.RuleResults))
                .LookupAllItems();
            columns.Add(new ColumnSpec(PropertyPath.Root.Property(nameof(ExtractedMetadataResultRow.SourceObject))).SetCaption(ColumnCaptions.ResultFile));
            if (step.Source != null)
            {
                columns.Add(new ColumnSpec(ruleResults.Property(nameof(MetadataStepResult.Source))).SetCaption(step.Source.DisplayName));
            }

            if (rows.Any(row => !row.RuleResults.FirstOrDefault()?.Match == true))
            {
                columns.Add(new ColumnSpec(ruleResults.Property(nameof(MetadataStepResult.Match))));
            }

            if (rows.Any(ShowMatchedValueColumn))
            {
                columns.Add(new ColumnSpec(ruleResults.Property(nameof(MetadataStepResult.MatchedValue))));
            }

            if (rows.Any(ShowReplacedValueColumn))
            {
                columns.Add(new ColumnSpec(ruleResults.Property(nameof(MetadataStepResult.ReplacedValue))));
            }

            if (step.Target != null)
            {
                columns.Add(new ColumnSpec(ruleResults.Property(nameof(MetadataStepResult.TargetValue))).SetCaption(step.Target.DisplayName));
            }

            var viewSpec = new ViewSpec().SetColumns(columns).SetSublistId(ruleResults);
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(ExtractedMetadataResultRow));
            return new ViewInfo(rootColumn, viewSpec);
        }

        public bool ShowMatchedValueColumn(ExtractedMetadataResultRow row)
        {
            return row.RuleResults.Any(result =>
            {
                if (!result.Match)
                {
                    return false;
                }

                if (result.MatchedValue == result.Source)
                {
                    return false;
                }
                if (result.ReplacedValue == result.MatchedValue && result.MatchedValue == Convert.ToString(result.TargetValue))
                {
                    return false;
                }

                return true;
            });
        }

        public bool ShowReplacedValueColumn(ExtractedMetadataResultRow row)
        {
            if (!ShowMatchedValueColumn(row))
            {
                return false;
            }

            return row.RuleResults.Any(result =>
            {
                if (!string.IsNullOrEmpty(result.ErrorText))
                {
                    return true;
                }

                if (result.MatchedValue == result.ReplacedValue)
                {
                    return false;
                }

                if (result.ReplacedValue == Convert.ToString(result.TargetValue))
                {
                    return false;
                }

                return true;
            });
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

        private void comboMetadataTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            if (!(comboSourceText.SelectedItem is TextColumnWrapper))
            {
                helper.ShowTextBoxError(comboSourceText, Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty);
                return;
            }

            if (!string.IsNullOrEmpty(tbxRegularExpression.Text))
            {
                try
                {
                    var _ = new Regex(tbxRegularExpression.Text);
                }
                catch (Exception)
                {
                    helper.ShowTextBoxError(tbxRegularExpression,
                        string.Format(Resources.MetadataRuleStepEditor_OkDialog__0__must_either_be_a_valid_regular_expression_or_blank, tbxRegularExpression.Text));
                    return;
                }
            }

            if (!(comboMetadataTarget.SelectedItem is TextColumnWrapper))
            {
                helper.ShowTextBoxError(comboMetadataTarget, Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty);
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public BoundDataGridViewEx PreviewGrid
        {
            get
            {
                return boundDataGridView1;
            }
        }
    }
}
