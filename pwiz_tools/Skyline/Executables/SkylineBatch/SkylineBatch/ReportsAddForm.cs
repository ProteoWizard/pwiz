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

namespace SkylineBatch
{
    public partial class ReportsAddForm : Form
    {
        private ReportInfo _report { get; set; }
        public ReportsAddForm(ReportInfo report)
        {
            _report = report;
            InitializeComponent();

            textReportName.Text = report.Name;
            textReportPath.Text = report.ReportPath;
            foreach (var script in report.rScripts)
            {
                boxRScripts.Items.Add(script);
            }
        }

        private void btnAddRScript_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = "R|*.r";
            openDialog.Title = "Open R Script";
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
            openDialog.Filter = "SKYR|*.skyr";
            openDialog.Title = "Open Report";
            openDialog.ShowDialog();
            textReportPath.Text = openDialog.FileName;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            var newReport = new ReportInfo(textReportName.Text, textReportPath.Text, GetScriptsFromUI());
            try
            {
                newReport.ValidateSettings();
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
            
            _report.Set(newReport.Name, newReport.ReportPath, newReport.rScripts);
            
            Close();
        }

        private List<string> GetScriptsFromUI()
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
