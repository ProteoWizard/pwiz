﻿/*
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
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class ReportsAddForm : Form
    {
        private readonly IMainUiControl _uiControl;
        public ReportsAddForm(IMainUiControl uiControl, ReportInfo editingReport = null)
        {
            InitializeComponent();
            _uiControl = uiControl;

            if (editingReport != null)
            {
                textReportName.Text = editingReport.Name;
                textReportPath.Text = editingReport.ReportPath;
                foreach (var scriptAndVersion in editingReport.RScripts)
                {
                    dataGridScripts.Rows.Add(scriptAndVersion.Item1, scriptAndVersion.Item2);
                }
            }
            foreach (var version in Settings.Default.RVersions.Keys)
            {
                rVersionsDropDown.Items.Add(version);
            }
        }

        public ReportInfo NewReportInfo { get; private set; }

        private void btnAddRScript_Click(object sender, EventArgs e)
        {
            if (Settings.Default.RVersions.Count == 0)
            {
                // Prevent user from adding R script if R is not installed
                _uiControl.DisplayError(Resources.ReportsAddForm_btnAddRScript_Click_Could_not_find_any_R_Installations_in__ + Environment.NewLine + 
                                                                    RInstallations.RLocation + Environment.NewLine +
                                                                    Environment.NewLine +
                                                                    Resources.ReportsAddForm_btnAddRScript_Click_Please_install_R_before_adding_R_scripts_to_this_configuration_);
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
            openDialog.InitialDirectory = TextUtil.GetInitialDirectory(path);
            if (openDialog.ShowDialog() != DialogResult.OK)
                return new string[]{};
            return openDialog.FileNames;
        }

        private void btnRemove_Click(object sender, EventArgs e)
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
            openDialog.InitialDirectory = TextUtil.GetInitialDirectory(textReportPath.Text);
            openDialog.ShowDialog();
            textReportPath.Text = openDialog.FileName;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                NewReportInfo = new ReportInfo(textReportName.Text, textReportPath.Text, GetScriptsFromUi());
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
                btnRemove.Enabled = false;
                return;
            }

            btnRemove.Enabled = dataGridScripts.SelectedCells[0].ColumnIndex == 0;
        }

        private void SelectRVersion(string version)
        {
            for (int i = 0; i < rVersionsDropDown.Items.Count; i++)
            {
                ((ToolStripMenuItem)rVersionsDropDown.Items[i]).Checked = rVersionsDropDown.Items[i].AccessibilityObject.Name == version;
            }
        }

        private void dataGridScripts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 1 && e.RowIndex > -1 && !string.IsNullOrEmpty((string)dataGridScripts.SelectedCells[0].Value))
            {
                var rectangle = dataGridScripts.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                var showLocation = new Point(rectangle.X, rectangle.Bottom);
                SelectRVersion((string)dataGridScripts.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
                rVersionsDropDown.Show(dataGridScripts, showLocation);
            }
        }

        private void rVersionsDropDown_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            SelectRVersion(e.ClickedItem.Name);
            dataGridScripts.SelectedCells[0].Value = e.ClickedItem.AccessibilityObject.Name;
        }
    }
}
