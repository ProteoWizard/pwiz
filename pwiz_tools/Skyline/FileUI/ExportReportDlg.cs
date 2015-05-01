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
using pwiz.Common.SystemUtil;
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

            CultureInfo = LocalizationHelper.CurrentCulture;

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
                var status = new ProgressStatus(Resources.ExportReportDlg_GetDatabase_Generating_Report_Data);
                using (var longWait = new LongWaitDlg
                    {
                        Text = status.Message,
                        Message = Resources.ExportReportDlg_GetDatabase_Analyzing_document
                    })
                {
                    longWait.PerformWork(owner, 1500, broker => EnsureDatabase(broker, 100, ref status));
                }
            }

            return _database;
        }

        private Report GetReport()
        {
            int previewIndex = ListBox.SelectedIndex;
            ReportSpec reportSpec = List[previewIndex];
            return Report.Load(reportSpec);
        }

        public Database EnsureDatabase(IProgressMonitor progressMonitor, int percentOfWait, ref ProgressStatus status)
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
                                        ProgressMonitor = progressMonitor,
                                        Status = status,
                                        PercentOfWait = percentOfWait
                                    };
            database.AddSrmDocument(document);
            status = database.Status;
            if (!progressMonitor.IsCanceled)
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
            using (var dlg = new SaveFileDialog
                {
                    Title = Resources.ExportReportDlg_OkDialog_Export_Report,
                    InitialDirectory = Settings.Default.ExportDirectory,
                    OverwritePrompt = true,
                    DefaultExt = TextUtil.EXT_CSV,
                    Filter = TextUtil.FileDialogFiltersAll(TextUtil.FILTER_CSV, TextUtil.FILTER_TSV)
                })
            {
                if (!string.IsNullOrEmpty(_documentUiContainer.DocumentFilePath))
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(_documentUiContainer.DocumentFilePath);
                    dlg.FileName = Path.GetFileNameWithoutExtension(_documentUiContainer.DocumentFilePath) + TextUtil.EXT_CSV;
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
                        separator = TextUtil.SEPARATOR_TSV;
                        break;
                    // CSV
                    default:
                        // Use the local culture CSV separator
                        separator = TextUtil.GetCsvSeparator(CultureInfo);
                        break;
                }
                OkDialog(fileName, separator);
            }
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
                    if (!saver.CanSave(this))
                        return false;

                    using (var writer = new StreamWriter(saver.SafeName))
                    {
                        Report report = GetReport();
                        bool success = false;

                        using (var longWait = new LongWaitDlg { Text = Resources.ExportReportDlg_ExportReport_Generating_Report })
                        {
                            longWait.PerformWork(this, 1500, broker =>
                            {
                                var status = new ProgressStatus(Resources.ExportReportDlg_GetDatabase_Analyzing_document);
                                broker.UpdateProgress(status);
                                Database database = EnsureDatabase(broker, 80, ref status);
                                if (broker.IsCanceled)
                                    return;
                                broker.UpdateProgress(status = status.ChangeMessage(Resources.ExportReportDlg_ExportReport_Building_report));
                                ResultSet resultSet = report.Execute(database);
                                if (broker.IsCanceled)
                                    return;
                                broker.UpdateProgress(status = status.ChangePercentComplete(95)
                                    .ChangeMessage(Resources.ExportReportDlg_ExportReport_Writing_report));

                                ResultSet.WriteReportHelper(resultSet, separator, writer, CultureInfo);

                                writer.Flush();
                                writer.Close();

                                if (broker.IsCanceled)
                                    return;
                                broker.UpdateProgress(status.Complete());

                                saver.Commit();
                                success = true;
                            });
                        }

                        return success;
                    }
                }
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(this, string.Format(Resources.ExportReportDlg_ExportReport_Failed_exporting_to, fileName, GetExceptionDisplayMessage(x)), x);
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
                var database = GetDatabase(this);
                // If database is null, than loading must have been cancelled.
                if (database == null)
                    return;

                PreviewReportDlg previewReportDlg = new PreviewReportDlg();
                previewReportDlg.SetResults(GetReport().Execute(database));
                previewReportDlg.Show(Owner);
            }
            catch (Exception x)
            {
                string message = GetExceptionDisplayMessage(x);
                string errorMessage = TextUtil.LineSeparate(
                    string.Format(Resources.ExportReportDlg_ShowPreview_An_unexpected_error_occurred_attempting_to_display_the_report___0___,listboxReports.SelectedItem),
                    message);
                MessageBox.Show(this, errorMessage, Program.Name);
            }
        }

        private static readonly Regex REGEX_MISSING_COLUMN =
            new Regex("^could not resolve property: (.*) of: (.*)$"); // Not L10N

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
                    else if (RatioPropertyAccessor.IsRatioOrRdotpProperty(columnName))
                        columnName = RatioPropertyAccessor.GetDisplayName(columnName);
                    return string.Format(Resources.ExportReportDlg_GetExceptionDisplayMessage_The_field__0__does_not_exist_in_this_document, columnName);
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

        private void btnShare_Click(object sender, EventArgs e)
        {
            ShowShare();
        }

        public void ShowShare()
        {
            CheckDisposed();
            using (var dlg = new ShareListDlg<ReportSpecList, ReportSpec>(Settings.Default.ReportSpecList)
                          {
                              Label = Resources.ExportReportDlg_ShowShare_Report_Definitions,
                              Filter = TextUtil.FileDialogFilterAll(Resources.ExportReportDlg_ShowShare_Skyline_Reports, ReportSpecList.EXT_REPORTS)
                          })
            {
                dlg.ShowDialog(this);
            }
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            Import(ShareListDlg<ReportSpecList, ReportSpec>.GetImportFileName(this,
                TextUtil.FileDialogFilterAll(Resources.ExportReportDlg_ShowShare_Skyline_Reports, ReportSpecList.EXT_REPORTS)));
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
