using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class DefineExtractedMetadataDlg : Form
    {
        private SkylineDataSchema _dataSchema;
        private DocumentAnnotations _documentAnnotations;
        private SortableBindingList<ResultRow> _bindingList;

        public DefineExtractedMetadataDlg(IDocumentContainer documentContainer)
        {
            InitializeComponent();
            _dataSchema = new SkylineDataSchema(documentContainer, DataSchemaLocalizer.INVARIANT);
            _documentAnnotations = new DocumentAnnotations(_dataSchema);
            _bindingList = new SortableBindingList<ResultRow>();
            _bindingList.AddRange(_dataSchema.ResultFileList.Select(entry => new ResultRow(entry.Value)));
            var resultFileMetadataSources =
                _documentAnnotations.GetResultFileMetadataSources().ToArray();
            comboSourceText.Items.AddRange(resultFileMetadataSources);
            comboMetadataTarget.Items.AddRange(_documentAnnotations.GetImportableResultFileMetadataTargets().Cast<object>().ToArray());
            comboSourceText.SelectedIndex = resultFileMetadataSources.IndexOf(item =>
                item.PropertyPath.Equals(PropertyPath.Root.Property(nameof(ResultFile.FileName))));
            bindingSource1.DataSource = _bindingList;
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
            public object ExtractedValue { get; set; }
        }

        public void UpdateRows()
        {
            MetadataTarget sourceMetadata = (MetadataTarget) comboSourceText.SelectedItem;
            if (sourceMetadata != null)
            {
                colSource.HeaderText = sourceMetadata.DisplayName;
            }
            else
            {
                colSource.HeaderText = "Source Text";
            }

            MetadataTarget target = (MetadataTarget) comboMetadataTarget.SelectedItem;
            if (target != null)
            {
                colExtractedValue.HeaderText = target.DisplayName;
            }
            else
            {
                colExtractedValue.HeaderText = "Extracted Value";
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
            foreach (var resultRow in _bindingList)
            {
                var resultFile = resultRow.ResultFile;
                if (sourceMetadata != null)
                {
                    resultRow.SourceText = sourceMetadata.GetFormattedValue(CultureInfo.CurrentCulture, resultFile);
                }
                else
                {
                    resultRow.SourceText = string.Empty;
                }
                if (regex != null)
                {
                    var match = regex.Match(resultRow.SourceText);
                    if (match.Success)
                    {
                        resultRow.Match = true;
                        string matchValue = match.Groups[Math.Min(match.Groups.Count - 1, 1)].ToString();
                        resultRow.ExtractedValue = matchValue;
                    }
                    else
                    {
                        resultRow.Match = false;
                        resultRow.ExtractedValue = string.Empty;
                    }
                }
                else
                {
                    resultRow.Match = !string.IsNullOrEmpty(resultRow.SourceText);
                    resultRow.ExtractedValue = resultRow.SourceText;
                }
            }
            _bindingList.ResetBindings();
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
    }
}
