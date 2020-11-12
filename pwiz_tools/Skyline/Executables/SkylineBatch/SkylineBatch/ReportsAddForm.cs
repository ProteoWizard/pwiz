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
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class ReportsAddForm : Form
    {
        private readonly IMainUiControl _uiControl;
        public ReportsAddForm(IMainUiControl uiControl, ReportInfo editingReport = null)
        {
            _uiControl = uiControl;
            InitializeComponent();

            if (editingReport != null)
            {
                textReportName.Text = editingReport.Name;
                textReportPath.Text = editingReport.ReportPath;
                //var rScripts = editingReport.GetRScripts();
                foreach (var report in editingReport.RScripts)
                {
                    boxRScripts.Items.Add(report);
                }
            }

        }
        public ReportInfo NewReportInfo { get; private set; }

        private void btnAddRScript_Click(object sender, EventArgs e)
        {
            var openDialog = new OpenFileDialog();
            openDialog.Filter = Resources.R_file_extension;
            openDialog.Title = Resources.Open_R_Script;
            openDialog.Multiselect = true;
            openDialog.ShowDialog();
            foreach (var fileName in openDialog.FileNames)
            {
                boxRScripts.Items.Add(fileName);
            }
            
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            while (boxRScripts.SelectedItems.Count > 0)
            {
                boxRScripts.Items.Remove(boxRScripts.SelectedItems[0]);
            }
        }

        private void boxRScripts_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRemove.Enabled = boxRScripts.SelectedItems.Count > 0;
        }

        private void btnReportPath_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = Resources.Skyr_file_extension;
            openDialog.Title = Resources.Open_Report;
            openDialog.ShowDialog();
            textReportPath.Text = openDialog.FileName;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                NewReportInfo = new ReportInfo(textReportName.Text, textReportPath.Text, GetScriptsFromUi());
            }
            catch (ArgumentException ex)
            {
                _uiControl.DisplayError("Error", ex.Message);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private List<string> GetScriptsFromUi()
        {
            var scripts = new List<string>();
            foreach (var item in boxRScripts.Items)
            {
                scripts.Add((string)item);
            }
            return scripts;
        }
    }
}
