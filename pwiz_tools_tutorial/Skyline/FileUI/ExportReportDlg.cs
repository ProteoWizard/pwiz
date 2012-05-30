/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class ExportReportDlg : FormEx, IReportDatabaseProvider
    {
        private readonly IDocumentUIContainer _documentUiContainer;
        private Database _database;

        public ExportReportDlg(IDocumentUIContainer documentUiContainer)
        {
            _documentUiContainer = documentUiContainer;

            InitializeComponent();

            CultureInfo = CultureInfo.CurrentCulture;

            LoadList();

            if (listboxReports.Items.Count > 0)
                listboxReports.SelectedIndex = 0;
        }

        protected override void  DestroyHandle()
        {
            if (_database != null)
                _database.Dispose();
 	        base.DestroyHandle();
        }

        public string ReportName
        {
            get { return ListBox.SelectedItem != null ? ListBox.SelectedItem.ToString() : null; }
            set { ListBox.SelectedItem = value; }
        }

        public CultureInfo CultureInfo { get; set; }

        private ListBox ListBox { get { return listboxReports; } }

        private static ReportSpecList List { get { return Settings.Default.ReportSpecList; } }

        public Database GetDatabase(Control owner)
        {
            if (_database == null)
            {
                var longWait = new LongWaitDlg { Text = "Generating Report Data", Message = "Analyzing document..." };
                longWait.PerformWork(owner, 1500, broker => EnsureDatabase(broker, 100));
            }

            return _database;
        }

        private Report GetReport()
        {
            int previewIndex = ListBox.SelectedIndex;
            ReportSpec reportSpec = List[previewIndex];
            return Report.Load(reportSpec);
        }

        public Database EnsureDatabase(ILongWaitBroker longWaitBroker, int percentOfWait)
        {
            var document = _documentUiContainer.Document;
            if (_database != null)
            {
                if (_database.SrmDocumentRevisionIndex == document.RevisionIndex)
                {
                    return _database;
                }
                _database = null;
            }
            Database database = new Database(document.Settings)
                                    {
                                        LongWaitBroker = longWaitBroker,
                                        PercentOfWait = percentOfWait
                                    };
            database.AddSrmDocument(document);
            _database = database;
            return _database;
        }

        private void listboxReports_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool selReport = listboxReports.SelectedIndex != -1;
            btnPreview.Enabled = selReport;
            btnOk.Enabled = selReport;
        }

        public void OkDialog()
        {
            SaveFileDialog dlg = new SaveFileDialog
            {
                Title = "Export Report",
                InitialDirectory = Settings.Default.ExportDirectory,
                OverwritePrompt = true,
                DefaultExt = "csv",
                Filter = string.Join("|", new[]
                {
                    "CSV (Comma delimited) (*.csv)|*.csv",
                    "TSV (Tab delimited) (*.tsv)|*.tsv",
                    "All Files (*.*)|*.*"
                })
            };
            if (!string.IsNullOrEmpty(_documentUiContainer.DocumentFilePath))
            {
                dlg.InitialDirectory = Path.GetDirectoryName(_documentUiContainer.DocumentFilePath);
                dlg.FileName = Path.GetFileNameWithoutExtension(_documentUiContainer.DocumentFilePath) + ".csv";                
            }

            if (dlg.ShowDialog(this) == DialogResult.Cancel)
                return;

            string fileName = dlg.FileName;
            char separator;
            // 1-based index
            switch (dlg.FilterIndex)
            {
                // TSV
                case 2:
                    separator = '\t';
                    break;
                // CSV
                default:
                    // Use the local culture CSV separator
                    separator = TextUtil.GetCsvSeparator(CultureInfo);
                    break;
            }

            OkDialog(fileName, separator);
        }

        public void OkDialog(string fileName, char separator)
        {
            Settings.Default.ExportDirectory = Path.GetDirectoryName(fileName);

            if (!ExportReport(fileName, separator))
                return;

            DialogResult = DialogResult.OK;
            Close();
        }

        private bool ExportReport(string fileName, char separator)
        {
            try
            {
                using (var saver = new FileSaver(fileName))
                {
                    if (!saver.CanSave(true))
                        return false;

                    using (var writer = new StreamWriter(saver.SafeName))
                    {
                        Report report = GetReport();
                        bool success = false;

                        var longWait = new LongWaitDlg { Text = "Generating Report" };
                        longWait.PerformWork(this, 1500, broker =>
                        {
                            broker.Message = "Analyzing document...";
                            broker.ProgressValue = 0;
                            Database database = EnsureDatabase(broker, 80);
                            if (broker.IsCanceled)
                                return;
                            broker.Message = "Building report...";
                            ResultSet resultSet = report.Execute(database);
                            if (broker.IsCanceled)
                                return;
                            broker.ProgressValue = 95;
                            broker.Message = "Writing report...";
                            
                            ResultSet.WriteReportHelper(resultSet, separator, writer, CultureInfo);

                            writer.Flush();
                            writer.Close();

                            if (broker.IsCanceled)
                                return;
                            broker.ProgressValue = 100;

                            saver.Commit();
                            success = true;
                        });

                        return success;
                    }
                }
            }
            catch (Exception x)
            {
                MessageDlg.Show(this, string.Format("Failed exporting to {0}.\n{1}", fileName, GetExceptionDisplayMessage(x)));
                return false;
            }
        }

        public void LoadList()
        {
            string selectedItemLast = null;
            if (listboxReports.SelectedItem != null)
                selectedItemLast = ListBox.SelectedItem.ToString();
            LoadList(selectedItemLast);
        }

        public void LoadList(string selectedItemLast)
        {
            ListBox.BeginUpdate();
            ListBox.Items.Clear();
            foreach (ReportSpec item in List)
            {
                string name = item.GetKey();
                int i = ListBox.Items.Add(name);

                // Select the previous selection if it is seen.
                if (ListBox.Items[i].ToString() == selectedItemLast)
                    ListBox.SelectedIndex = i;
            }
            ListBox.EndUpdate();
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditList();
        }

        public void EditList()
        {
            CheckDisposed();
            IEnumerable<ReportSpec> listNew = List.EditList(this, this);
            if (listNew != null)
            {
                List.Clear();
                List.AddRange(listNew);

                // Reload from the edited list.
                LoadList();
            }
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            ShowPreview();
        }

        public void ShowPreview()
        {
            try
            {
                PreviewReportDlg previewReportDlg = new PreviewReportDlg();
                previewReportDlg.SetResults(GetReport().Execute(GetDatabase(this)));
                previewReportDlg.Show(Owner);
            }
            catch (Exception x)
            {
                string message = GetExceptionDisplayMessage(x);
                MessageBox.Show(this, string.Format("An unexpected error occurred attempting to display the report '{0}'.\n{1}", listboxReports.SelectedItem, message), Program.Name);
            }
        }

        private static readonly Regex REGEX_MISSING_COLUMN =
            new Regex("^could not resolve property: (.*) of: (.*)$");

        private static string GetExceptionDisplayMessage(Exception x)
        {
            var match = REGEX_MISSING_COLUMN.Match(x.Message);
            if (match.Success)
            {
                try
                {
                    string columnName = match.Groups[1].ToString();
                    if (AnnotationDef.IsAnnotationProperty(columnName))
                        columnName = AnnotationDef.GetColumnDisplayName(columnName);
                    else if (RatioPropertyAccessor.IsRatioProperty(columnName))
                        columnName = RatioPropertyAccessor.GetDisplayName(columnName);
                    return string.Format("The field {0} does not exist in this document.", columnName);
                }
// ReSharper disable EmptyGeneralCatchClause
                catch
                {
                    // Could throw a variety of SQLiteException & NHibernat exceptions
                }
// ReSharper restore EmptyGeneralCatchClause
            }

            return x.Message;
        }

        private const string REPORT_DEFINITION_FILTER = "Skyline Reports (*.skyr)|*.skyr|All Files|*.*";

        private void btnShare_Click(object sender, EventArgs e)
        {
            ShowShare();
        }

        public void ShowShare()
        {
            CheckDisposed();
            using (var dlg = new ShareListDlg<ReportSpecList, ReportSpec>(Settings.Default.ReportSpecList)
                          {
                              Label = "Report Definitions",
                              Filter = REPORT_DEFINITION_FILTER
                          })
            {
                dlg.ShowDialog(this);
            }
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            Import(ShareListDlg<ReportSpecList, ReportSpec>.GetImportFileName(this,
                REPORT_DEFINITION_FILTER));
        }

        public void Import(string fileName)
        {
            if (ShareListDlg<ReportSpecList, ReportSpec>.ImportFile(this,
                    Settings.Default.ReportSpecList, fileName))
                LoadList();
        }

        private void listboxReports_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            OkDialog();
        }

        public void CancelClick()
        {
            // Use for testing.
            btnCancel.PerformClick();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}
