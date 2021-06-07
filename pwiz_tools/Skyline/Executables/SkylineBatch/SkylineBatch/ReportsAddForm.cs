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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class ReportsAddForm : Form
    {
        private readonly IMainUiControl _uiControl;
        private readonly RDirectorySelector _rDirectorySelector;
        public ReportsAddForm(IMainUiControl uiControl, RDirectorySelector rDirectorySelector, bool hasRefineFile, ReportInfo editingReport = null)
        {
            InitializeComponent();
            Icon = Program.Icon();
            _uiControl = uiControl;
            _rDirectorySelector = rDirectorySelector;
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
                    dataGridScripts.Rows.Add(scriptAndVersion.Item1, scriptAndVersion.Item2);
                }
            }
            UpdateRVersionDropDown();
        }

        public ReportInfo NewReportInfo { get; private set; }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (Settings.Default.RVersions.Count == 0)
            {
                if (!AddRDirectory())
                    return;
            }

            var fileNames = OpenRScript(textReportPath.Text, true);
            foreach (var fileName in fileNames)
            {
                dataGridScripts.Rows.Add(fileName, rVersionsDropDown.Items[rVersionsDropDown.Items.Count - 1].AccessibilityObject.Name);
            }
        }

        private void dataGridScripts_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 0 || string.IsNullOrEmpty((string)dataGridScripts.SelectedCells[0].Value))
                return;
            var selectedCell = dataGridScripts.SelectedCells[0];
            var fileNames = OpenRScript((string)selectedCell.Value, false);
            if (fileNames.Length > 0)
            {
                selectedCell.Value = fileNames[0];
            }
        }

        private string[] OpenRScript(string path, bool allowMultiSelect)
        {
            var openDialog = new OpenFileDialog();
            openDialog.Filter = TextUtil.FILTER_R;
            openDialog.Multiselect = allowMultiSelect;
            openDialog.InitialDirectory = FileUtil.GetInitialDirectory(path);
            if (openDialog.ShowDialog() != DialogResult.OK)
                return new string[]{};
            return openDialog.FileNames;
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
                    reportPath, GetScriptsFromUi(), radioRefinedFile.Checked);
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
                scripts.Add(new Tuple<string, string>((string)row.Cells[0].Value, (string)row.Cells[1].Value));
            }
            return scripts;
        }
        
        private void dataGridScripts_SelectionChanged(object sender, EventArgs e)
        {
            rVersionsDropDown.Hide();
            if (dataGridScripts.SelectedCells.Count == 0)
            {
                btnDelete.Enabled = false;
                return;
            }

            btnDelete.Enabled = dataGridScripts.SelectedCells[0].ColumnIndex == 0;
        }

        private void SelectRVersion(string version)
        {
            for (int i = 0; i < rVersionsDropDown.Items.Count; i++)
            {
                ((ToolStripMenuItem)rVersionsDropDown.Items[i]).Checked = rVersionsDropDown.Items[i].AccessibilityObject.Name == version;
            }
        }

        private void UpdateRVersionDropDown()
        {
            rVersionsDropDown.Items.Clear();
            var sortedRVersions = Settings.Default.RVersions.Keys.ToList();
            sortedRVersions.Sort();
            foreach (var version in sortedRVersions)
                rVersionsDropDown.Items.Add(version);
        }

        private bool AddRDirectory()
        {
            if (!_rDirectorySelector.RequiredDirectoryAdded())
                return false;
            UpdateRVersionDropDown();
            return true;
        }

        private void dataGridScripts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 1 && e.RowIndex > -1 && !string.IsNullOrEmpty((string)dataGridScripts.SelectedCells[0].Value))
            {
                var rectangle = dataGridScripts.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                var showLocation = new Point(rectangle.X, rectangle.Bottom);
                SelectRVersion((string)dataGridScripts.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
                var dropDown = true;
                if (Settings.Default.RVersions.Count == 0)
                    dropDown = AddRDirectory();
                if (dropDown)
                    rVersionsDropDown.Show(dataGridScripts, showLocation);
            }
        }

        private void rVersionsDropDown_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            SelectRVersion(e.ClickedItem.Name);
            dataGridScripts.SelectedCells[0].Value = e.ClickedItem.AccessibilityObject.Name;
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
