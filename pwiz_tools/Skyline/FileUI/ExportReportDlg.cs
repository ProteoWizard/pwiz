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
using System.IO;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class ExportReportDlg : Form, IReportDatabaseProvider
    {
        private readonly IDocumentUIContainer _documentUiContainer;
        private Database _database;

        private bool _clickedOk;

        public ExportReportDlg(IDocumentUIContainer documentUiContainer)
        {
            _documentUiContainer = documentUiContainer;

            InitializeComponent();

            LoadList();

            if (listboxReports.Items.Count > 0)
                listboxReports.SelectedIndex = 0;
        }

        private ListBox ListBox { get { return listboxReports; } }

        private static ReportSpecList List { get { return Settings.Default.ReportSpecList; } }

        public Database Database
        {
            get
            {
                EnsureDatabase();

                return _database;
            }
        }

        private Report GetReport()
        {
            int previewIndex = ListBox.SelectedIndex;
            ReportSpec reportSpec = List[previewIndex];
            return Report.Load(reportSpec);
        }

        private Database EnsureDatabase()
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
            Cursor cursorOld = Cursor;
            try
            {
                Cursor = Cursors.WaitCursor;
                Database database = new Database();
                database.AddSrmDocument(document);
                _database = database;
                return _database;
            }
            finally
            {
                Cursor = cursorOld;
            }
        }

        private void listboxReports_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool selReport = listboxReports.SelectedIndex != -1;
            btnPreview.Enabled = selReport;
            btnOk.Enabled = selReport;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_clickedOk)
            {
                _clickedOk = false; // Reset in case of failure.

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
                    dlg.FileName = Path.GetFileNameWithoutExtension(_documentUiContainer.DocumentFilePath) + ".csv";

                if (dlg.ShowDialog(this) == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                else
                {
                    Settings.Default.ExportDirectory = Path.GetDirectoryName(dlg.FileName);

                    bool success;
                    // 1-based index
                    switch (dlg.FilterIndex)
                    {
                        // TSV
                        case 2:
                            success = ExportReport(dlg.FileName, '\t');
                            break;
                        // CSV
                        default:
                            success = ExportReport(dlg.FileName, ',');
                            break;
                    }
                    if (!success)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }

            base.OnClosing(e);
        }

        private bool ExportReport(string fileName, char separator)
        {
            try
            {
                using (new LongOp(this))
                using (var saver = new FileSaver(fileName))
                {
                    if (!saver.CanSave(true))
                        return false;

                    using (var writer = new StreamWriter(saver.SafeName))
                    {
                        Report report = GetReport();
                        Database database = EnsureDatabase();
                        ResultSet resultSet = report.Execute(database);
                        for (int i = 0; i < resultSet.ColumnInfos.Count; i++)
                        {
                            var columnInfo = resultSet.ColumnInfos[i];
                            if (columnInfo.IsHidden)
                                continue;

                            if (i > 0)
                                writer.Write(separator);
                            writer.Write(columnInfo.Caption);
                        }
                        writer.WriteLine();
                        for (int iRow = 0; iRow < resultSet.RowCount; iRow++)
                        {
                            for (int iColumn = 0; iColumn < resultSet.ColumnInfos.Count; iColumn++)
                            {
                                var columnInfo = resultSet.ColumnInfos[iColumn];
                                if (columnInfo.IsHidden)
                                    continue;

                                if (iColumn > 0)
                                    writer.Write(separator);
                                string value = resultSet.FormatValue(iRow, iColumn);
                                if (value.IndexOf(separator) == -1)
                                    writer.Write(value);
                                else
                                {
                                    // Quote fields that contain the separator value
                                    writer.Write("\"");
                                    writer.Write(value);
                                    writer.Write("\"");
                                }
                            }
                            writer.WriteLine();
                        }
                        writer.Flush();
                        writer.Close();

                        saver.Commit();

                        return true;
                    }
                }
            }
            catch (Exception x)
            {
                MessageBox.Show(string.Format("Failed exporting to {0}.\n{1}", fileName, x.Message));
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

        public void EditList()
        {
            IEnumerable<ReportSpec> listNew = List.EditList(this);
            if (listNew != null)
            {
                List.Clear();
                List.AddRange(listNew);

                // Reload from the edited list.
                LoadList();
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditList();
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            try
            {
                PreviewReportDlg previewReportDlg = new PreviewReportDlg();
                previewReportDlg.SetResults(GetReport().Execute(EnsureDatabase()));
                previewReportDlg.Show(Owner);
            }
            catch (Exception)
            {
                MessageBox.Show(this, string.Format("An unexpected error occurred attempting to display the report '{0}'.", listboxReports.SelectedItem), Program.Name);
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            _clickedOk = true;
        }

        private const string REPORT_DEFINITION_FILTER = "Skyline Reports (*.skyr)|*.skyr|All Files|*.*";

        private void btnShare_Click(object sender, EventArgs e)
        {
            var dlg = new ShareListDlg<ReportSpecList, ReportSpec>(Settings.Default.ReportSpecList)
                          {
                              Label = "Report Definitions",
                              Filter = REPORT_DEFINITION_FILTER
                          };
            dlg.ShowDialog(this);
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            if (ShareListDlg<ReportSpecList, ReportSpec>.Import(this,
                    Settings.Default.ReportSpecList, REPORT_DEFINITION_FILTER))
                LoadList();
        }

        private void listboxReports_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            btnOk_Click(sender, new EventArgs());
            Close();
        }
    }
}
