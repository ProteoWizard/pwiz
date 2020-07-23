using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
        private SortableBindingList<ResultRow> _resultRows;

        public DefineExtractedMetadataDlg(IDocumentContainer documentContainer)
        {
            InitializeComponent();
            _dataSchema = new SkylineDataSchema(documentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            _resultRows = new SortableBindingList<ResultRow>(_dataSchema.ResultFileList.Values.Select(value=>new ResultRow(value)).ToList());
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(ResultFile));
            var allColumns = GetAllTextColumnWrappers(rootColumn).ToList();
            var sources = allColumns.Where(IsSource).ToArray();
            comboSourceText.Items.AddRange(sources);
            comboMetadataTarget.Items.AddRange(allColumns.Where(IsTarget).ToArray());
            comboSourceText.SelectedIndex = sources.IndexOf(item =>
                item.PropertyPath.Equals(PropertyPath.Root.Property(nameof(ResultFile.FileName))));
            bindingSource1.DataSource = _resultRows;
            FormatCultureInfo = CultureInfo.InvariantCulture;
        }

        public class ResultRow
        {
            public ResultRow(ResultFile resultFile)
            {
                ResultFile = resultFile;
            }

            public ResultFile ResultFile { get; private set; }

            public string FileName
            {
                get { return ResultFile.FileName; }
            }

            public string SourceText { get; set; }
            public bool Match { get; set; }
            public string ExtractedText { get; set; }
            public object TargetValue { get; set; }
            public string ErrorText { get; set; }

            public bool ExtractedTextDifferent()
            {
                if (!Match)
                {
                    return false;
                }
                if (SourceText == ExtractedText)
                {
                    return false;
                }

                if (ExtractedText == Convert.ToString(TargetValue))
                {
                    return false;
                }

                return true;
            }
        }

        public CultureInfo FormatCultureInfo { get; set; }

        public void UpdateRows()
        {
            
            TextColumnWrapper source = (TextColumnWrapper) comboSourceText.SelectedItem;
            if (source != null)
            {
                colSource.HeaderText = source.DisplayName;
                colSource.Visible = true;
            }
            else
            {
                colSource.Visible = false;
            }
            

            TextColumnWrapper target = (TextColumnWrapper) comboMetadataTarget.SelectedItem;
            if (target != null)
            {
                colTarget.Visible = true;
                colTarget.HeaderText = target.DisplayName;
                colTarget.ValueType = target.ColumnDescriptor.PropertyType;
            }
            else
            {
                colTarget.Visible = false;
            }

            Regex regex = null;
            if (!string.IsNullOrEmpty(tbxRegularExpression.Text))
            {
                try
                {
                    regex = new Regex(tbxRegularExpression.Text);
                    ShowRegexError(null);
                }
                catch (Exception x)
                {
                    ShowRegexError(x);
                }
            }

            foreach (var row in _resultRows)
            {
                var resultFile = row.ResultFile;
                string strSourceText;
                if (source != null)
                {
                    strSourceText = source.GetTextValue(FormatCultureInfo, resultFile);
                }
                else
                {
                    strSourceText = null;
                }

                row.SourceText = strSourceText;
                string strExtractedValue;
                if (regex != null && !string.IsNullOrEmpty(strSourceText))
                {
                    var match = regex.Match(strSourceText);
                    if (match.Success)
                    {
                        strExtractedValue = match.Groups[Math.Min(match.Groups.Count - 1, 1)].ToString();
                    }
                    else
                    {
                        strExtractedValue = null;
                    }
                }
                else
                {
                    strExtractedValue = strSourceText;
                }

                row.ExtractedText = strExtractedValue;
                row.TargetValue = null;
                row.ErrorText = null;
                if (strExtractedValue != null)
                {
                    row.Match = true;
                    if (target != null)
                    {
                        try
                        {
                            row.TargetValue = target.ParseTextValue(FormatCultureInfo, strExtractedValue);
                        }
                        catch (Exception x)
                        {
                            row.ErrorText = x.Message;
                        }
                    }
                }
                else
                {
                    row.Match = false;
                }
            }

            colMatch.Visible = colSource.Visible && _resultRows.Any(row => !row.Match);
            colExtractedValue.Visible = _resultRows.Any(row=>row.ExtractedTextDifferent());
                                        
            _resultRows.ResetBindings();
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
            e.ErrorText = null;
            if (e.RowIndex >= 0 && e.RowIndex < _resultRows.Count)
            {
                var errorColumn = colExtractedValue.Visible ? colExtractedValue : colSource;
                if (errorColumn.Index == e.ColumnIndex)
                {
                    e.ErrorText = _resultRows[e.RowIndex].ErrorText;
                }
            }
        }
    }
}
