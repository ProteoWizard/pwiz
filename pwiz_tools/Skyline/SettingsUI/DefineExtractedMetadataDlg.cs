using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;

namespace pwiz.Skyline.SettingsUI
{
    public partial class DefineExtractedMetadataDlg : Form
    {
        private SkylineDataSchema _dataSchema;
        private DocumentAnnotations _documentAnnotations;

        public DefineExtractedMetadataDlg(IDocumentContainer documentContainer)
        {
            InitializeComponent();
            _dataSchema = new SkylineDataSchema(documentContainer, DataSchemaLocalizer.INVARIANT);
            _documentAnnotations = new DocumentAnnotations(_dataSchema);
            comboMetadataTarget.Items.AddRange(_documentAnnotations.GetResultFileMetadataTargets().Cast<object>().ToArray());
        }

        public class ResultRow
        {
            public string FileName { get; set; }
            public string SourceText { get; set; }
            public bool Match { get; set; }
            public object ExtractedValue { get; set; }
        }

        public void UpdateRows()
        {
            var resultRows = new List<ResultRow>();
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
            foreach (var resultFileEntry in _dataSchema.ResultFileList)
            {
                var resultFile = resultFileEntry.Value;
                var resultRow = new ResultRow
                {
                    FileName = resultFile.FileName,
                    SourceText = resultFile.FileName,
                };
                if (regex != null)
                {
                    var match = regex.Match(resultRow.SourceText);
                    string matchValue;
                    if (match.Success)
                    {
                        resultRow.Match = true;
                        if (match.Groups.Count > 0)
                        {
                            matchValue = match.Groups[0].ToString();
                        }
                        else
                        {
                            matchValue = match.ToString();
                        }

                        resultRow.ExtractedValue = matchValue;
                    }
                    else
                    {
                        resultRow.Match = false;
                    }
                }
            }
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
    }
}
