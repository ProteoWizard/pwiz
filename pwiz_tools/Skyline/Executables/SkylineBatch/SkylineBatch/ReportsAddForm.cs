/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class ReportsAddForm : Form
    {
        private readonly IMainUiControl _uiControl;
        private readonly RDirectorySelector _rDirectorySelector;
        private readonly Dictionary<string, PanoramaFile> _remoteFiles;

        public ReportsAddForm(IMainUiControl uiControl, RDirectorySelector rDirectorySelector, bool hasRefineFile, SkylineBatchConfigManagerState state, ReportInfo editingReport = null)
        {
            InitializeComponent();
            Icon = Program.Icon();
            _uiControl = uiControl;
            State = state;
            _rDirectorySelector = rDirectorySelector;
            _remoteFiles = new Dictionary<string, PanoramaFile>();
            radioResultsFile.Checked = true;
            radioRefinedFile.Enabled = hasRefineFile;

            if (editingReport != null)
            {
                textReportName.Text = editingReport.Name;
                checkBoxCultureInvariant.Checked = !editingReport.CultureSpecific;
                textReportPath.Text = editingReport.ReportPath ?? string.Empty;
                checkBoxImport.Checked = !string.IsNullOrEmpty(editingReport.ReportPath);
                radioRefinedFile.Checked = editingReport.UseRefineFile;
                foreach (var scriptAndVersion in editingReport.RScripts)
                {
                    var server = editingReport.RScriptServers.ContainsKey(scriptAndVersion.Item1)
                        ? editingReport.RScriptServers[scriptAndVersion.Item1]
                        : null;
                    var url = server != null ? server.URI.AbsoluteUri : string.Empty;
                    dataGridScripts.Rows.Add(scriptAndVersion.Item1, url, scriptAndVersion.Item2);
                }
                foreach (var entry in editingReport.RScriptServers)
                    _remoteFiles.Add(entry.Key, entry.Value);
            }
        }

        public SkylineBatchConfigManagerState State { get; private set; }
        public ReportInfo NewReportInfo { get; private set; }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            Add();
        }

        private void Add()
        {
            var rScriptForm = new RScriptForm(null, null, null, _rDirectorySelector, _uiControl, State);
            if (DialogResult.OK != rScriptForm.ShowDialog(this))
                return;
            if (rScriptForm.RemoteFile != null)
            {
                dataGridScripts.Rows.Add(rScriptForm.Path, rScriptForm.RemoteFile.URI.AbsoluteUri, rScriptForm.Version);
                _remoteFiles.Add(rScriptForm.Path, rScriptForm.RemoteFile);
            }
            else
            {
                dataGridScripts.Rows.Add(rScriptForm.Path, string.Empty, rScriptForm.Version);
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditSelected();
        }

        private void dataGridScripts_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridScripts.SelectedRows.Count == 0)
                return;
            if (dataGridScripts.SelectedRows[0].Index == dataGridScripts.Rows.Count - 1)
                Add();
            else
                EditSelected();
        }

        private void EditSelected()
        {
            var oldPath = (string)dataGridScripts.SelectedCells[0].Value;
            var rowSelected = dataGridScripts.SelectedRows[0].Index;
            var remoteFile = _remoteFiles.ContainsKey(oldPath) ? _remoteFiles[oldPath] : null;
            var rScriptForm = new RScriptForm(oldPath, (string)dataGridScripts.SelectedCells[2].Value, remoteFile, _rDirectorySelector, _uiControl, State);
            if (DialogResult.OK != rScriptForm.ShowDialog(this))
                return;
            State = rScriptForm.State;
            dataGridScripts.Rows.RemoveAt(rowSelected);
            if (_remoteFiles.ContainsKey(oldPath))
                _remoteFiles.Remove(oldPath);
            if (rScriptForm.RemoteFile != null)
            {
                dataGridScripts.Rows.Insert(rowSelected, rScriptForm.Path, rScriptForm.RemoteFile.URI.AbsoluteUri, rScriptForm.Version);
                _remoteFiles.Add(rScriptForm.Path, rScriptForm.RemoteFile);
            }
            else
            {
                dataGridScripts.Rows.Insert(rowSelected, rScriptForm.Path, string.Empty, rScriptForm.Version);
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dataGridScripts.SelectedCells.Count > 0)
            {
                dataGridScripts.Rows.RemoveAt(dataGridScripts.SelectedCells[0].RowIndex);
            }
        }

        private void btnReportPath_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = TextUtil.FILTER_SKYR;
            openDialog.InitialDirectory = FileUtil.GetInitialDirectory(textReportPath.Text);
            openDialog.ShowDialog();
            textReportPath.Text = openDialog.FileName;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            var reportPath = checkBoxImport.Checked ? textReportPath.Text : string.Empty;
            try
            {
                NewReportInfo = new ReportInfo(textReportName.Text, !checkBoxCultureInvariant.Checked,
                    reportPath, GetScriptsFromUi(), _remoteFiles, radioRefinedFile.Checked);
                NewReportInfo.Validate();
            }
            catch (ArgumentException ex)
            {
                _uiControl.DisplayError(ex.Message);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private List<Tuple<string, string>> GetScriptsFromUi()
        {
            var scripts = new List<Tuple<string, string>>();
            foreach (DataGridViewRow row in dataGridScripts.Rows)
            {
                if (row.Cells[1].Value == null)
                    break;
                scripts.Add(new Tuple<string, string>((string)row.Cells[0].Value, (string)row.Cells[2].Value));
            }
            return scripts;
        }
        
        private void dataGridScripts_SelectionChanged(object sender, EventArgs e)
        {
            var emptyRowSelected = dataGridScripts.SelectedRows[0].Index == dataGridScripts.Rows.Count - 1;
            btnDelete.Enabled = dataGridScripts.SelectedRows.Count > 0 && !emptyRowSelected;
            btnEdit.Enabled = dataGridScripts.SelectedRows.Count > 0 && !emptyRowSelected;
        }

        private void checkBoxImport_CheckedChanged(object sender, EventArgs e)
        {
            labelReportPath.Enabled = checkBoxImport.Checked;
            textReportPath.Enabled = checkBoxImport.Checked;
            btnReportPath.Enabled = checkBoxImport.Checked;
        }

        private void checkBoxCultureInvariant_CheckedChanged(object sender, EventArgs e)
        {
            checkBoxCultureInvariant.CheckedChanged -= checkBoxCultureInvariant_CheckedChanged;
            if (!checkBoxCultureInvariant.Checked)
            {
                var continueChecked = DialogResult.Cancel != AlertDlg.ShowOkCancel(this, Program.AppName(),
                    Resources.ReportsAddForm_checkBoxCultureSpecific_CheckedChanged_A_culture_invariant_report_ensures_a_CSV_file_with_period_decimal_points_and_full_precision_numbers_ + Environment.NewLine + 
                    Resources.ReportsAddForm_checkBoxCultureSpecific_CheckedChanged_Do_you_want_to_continue_);
                if (!continueChecked)
                    checkBoxCultureInvariant.Checked = true;
            }
        }

        private void ReportsAddForm_Load(object sender, EventArgs e)
        {
            checkBoxCultureInvariant.CheckedChanged += checkBoxCultureInvariant_CheckedChanged;
        }
    }
}
