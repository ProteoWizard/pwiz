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
using System.IO;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class ReportsAddForm : Form
    {
        private readonly IMainUiControl _uiControl;
       // private readonly Dictionary<string,string> rScriptVersions;
        public ReportsAddForm(IMainUiControl uiControl, ReportInfo editingReport = null)
        {
            InitializeComponent();
            _uiControl = uiControl;
            //rScriptVersions = SkylineSettings.GetRscriptExeList();

            if (editingReport != null)
            {
                textReportName.Text = editingReport.Name;
                textReportPath.Text = editingReport.ReportPath;
                //var rScripts = editingReport.GetRScripts();
                foreach (var scriptAndVersion in editingReport.RScripts)
                {
                    dataGridScripts.Rows.Add(scriptAndVersion.Item1, scriptAndVersion.Item2);
                }
            }

            foreach (var version in Settings.Default.RVersions.Keys)
            {
                rVersionsDropDown.Items.Add(version);
            }
            //rVersionsDropDown.Items.AddRange(SkylineSettings.GetRscriptExeList());

        }
        public ReportInfo NewReportInfo { get; private set; }

        private void btnAddRScript_Click(object sender, EventArgs e)
        {
            var openDialog = new OpenFileDialog();
            openDialog.Filter = Resources.ReportsAddForm_R_file_extension;
            openDialog.Title = Resources.ReportsAddForm_Open_R_Script;
            openDialog.Multiselect = true;
            openDialog.ShowDialog();
            foreach (var fileName in openDialog.FileNames)
            {
                dataGridScripts.Rows.Add(fileName, rVersionsDropDown.Items[rVersionsDropDown.Items.Count - 1].AccessibilityObject.Name);
            }
            
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            //dataGridScripts.SelectedCells[0].RowIndex;
            if (dataGridScripts.SelectedCells.Count > 0)
            {
                dataGridScripts.Rows.RemoveAt(dataGridScripts.SelectedCells[0].RowIndex);
            }
        }

        private void btnReportPath_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = Resources.ReportsAddForm_Skyr_file_extension;
            openDialog.Title = Resources.ReportsAddForm_Open_Report;
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
                _uiControl.DisplayError("Error", ex.Message);
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

        private void dataGridScripts_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 0 || string.IsNullOrEmpty((string) dataGridScripts.SelectedCells[0].Value))
                return;
               
            var selectedCell = dataGridScripts.SelectedCells[0];
            var openDialog = new OpenFileDialog();
            openDialog.Filter = Resources.ReportsAddForm_R_file_extension;
            openDialog.Title = Resources.ReportsAddForm_Open_R_Script;
            openDialog.Multiselect = false;
            openDialog.InitialDirectory = Path.GetDirectoryName((string)selectedCell.Value);
            openDialog.ShowDialog();
            if (!string.IsNullOrEmpty(openDialog.FileName))
            {
                selectedCell.Value = openDialog.FileName;
            }
        }
    }
}
