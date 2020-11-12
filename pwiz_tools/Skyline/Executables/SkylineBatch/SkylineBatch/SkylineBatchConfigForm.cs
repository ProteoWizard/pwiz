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
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public enum ConfigAction
    {
        Add, Edit, Copy
    }

    public partial class SkylineBatchConfigForm : Form
    {
        // Allows a user to create a new configuration and add it to the list of configurations,
        // or replace an existing configuration.
        // Currently running configurations cannot be replaced, and will be opened in a read only mode.
        

        private readonly IMainUiControl _mainControl;
        private readonly bool _isBusy;
        private readonly ConfigAction _action;
        private readonly DateTime _initialCreated;
        private readonly List<ReportInfo> _newReportList;

        public SkylineBatchConfigForm(IMainUiControl mainControl, SkylineBatchConfig config, ConfigAction action, bool isBusy)
        {
            _action = action;
            _initialCreated = config?.Created ?? DateTime.MinValue;
            _newReportList = new List<ReportInfo>();

            _mainControl = mainControl;
            _isBusy = isBusy;
            InitializeComponent();


            if (config != null)
            {
                InitializeInputFields(config);
            }
            
            btnSaveConfig.Show();

            if (isBusy)
            {
                // configuration is running and cannot be edited
                lblConfigRunning.Show();
                DisableEverything();
                // save and cancel buttons are replaced with OK button
                btnSaveConfig.Hide(); 
                btnCancelConfig.Text = @"OK";
            }
            else
            {
                lblConfigRunning.Hide();
            }

            ActiveControl = textConfigName;
        }

        private void InitializeInputFields(SkylineBatchConfig config)
        {
            var mainSettings = config.MainSettings;
            switch (_action)
            {
                case ConfigAction.Add:
                    textConfigName.Text = "";
                    textAnalysisPath.Text = Path.GetDirectoryName(mainSettings.AnalysisFolderPath) + @"\";
                    textNamingPattern.Text = "";
                    break;
                case ConfigAction.Copy:
                    textConfigName.Text = "";
                    textAnalysisPath.Text = config.MainSettings.AnalysisFolderPath;
                    textNamingPattern.Text = mainSettings.ReplicateNamingPattern;
                    InitializeReportSettings(config);
                    break;
                case ConfigAction.Edit:
                    textConfigName.Text = config.Name;
                    textAnalysisPath.Text = config.MainSettings.AnalysisFolderPath;
                    textNamingPattern.Text = mainSettings.ReplicateNamingPattern;
                    InitializeReportSettings(config);
                    break;
                default:
                    return; // never get here
            }
            textSkylinePath.Text = mainSettings.TemplateFilePath;
            textDataPath.Text = mainSettings.DataFolderPath;

            textConfigName.TextChanged += textConfigName_TextChanged;
        }

        public void DisableEverything()
        {
            textConfigName.ReadOnly = true;
            textSkylinePath.ReadOnly = true;
            textAnalysisPath.ReadOnly = true;
            textDataPath.ReadOnly = true;
            textNamingPattern.ReadOnly = true;
            btnAddReport.Enabled = false;
            btnAnalysisPath.Enabled = false;
            btnDataPath.Enabled = false;
            btnSkylineFilePath.Enabled = false;
        }



        #region Edit main settings

        

        private MainSettings GetMainSettingsFromUi()
        {
            var templateFilePath = textSkylinePath.Text;
            var analysisFolderPath = textAnalysisPath.Text;
            var dataFolderPath = textDataPath.Text;
            var replicateNamingPattern = textNamingPattern.Text;
            return new MainSettings(templateFilePath, analysisFolderPath, dataFolderPath, replicateNamingPattern);
        }

        private void textConfigName_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(textAnalysisPath.Text))
            {
                var parentPath = Path.GetDirectoryName(textAnalysisPath.Text);
                textAnalysisPath.Text = Path.Combine(parentPath ?? string.Empty, textConfigName.Text);
            }

        }

        private void btnSkylineFilePath_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = Resources.Sky_file_extension;
            openDialog.Title = Resources.Open_skyline_file;
            openDialog.ShowDialog();
            textSkylinePath.Text = openDialog.FileName;
        }

        private void btnAnalysisFilePath_Click(object sender, EventArgs e)
        {
            OpenFolder(textAnalysisPath);
        }

        private void btnDataPath_Click(object sender, EventArgs e)
        {
            OpenFolder(textDataPath);
        }

        private void OpenFolder(TextBox textbox)
        {
            var dialog = new FolderBrowserDialog();
            dialog.SelectedPath = textbox.Text;
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                textbox.Text = dialog.SelectedPath;
            }
        }

        private void linkLabelRegex_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.regular-expressions.info/reference.html");
        }

        #endregion


        #region Reports

        private void InitializeReportSettings(SkylineBatchConfig config)
        {
            foreach (var report in config.ReportSettings.Reports)
            {
                _newReportList.Add(report);
                gridReportSettings.Rows.Add(report.AsArray());
            }
        }

        private void btnAddReport_Click(object sender, EventArgs e)
        {
            Program.LogInfo("Creating new report");
            ShowAddReportDialog(_newReportList.Count);
        }

        private void ShowAddReportDialog(int addingIndex, ReportInfo editingReport = null)
        {
            var addReportsForm = new ReportsAddForm(_mainControl, editingReport);
            addReportsForm.StartPosition = FormStartPosition.CenterParent;
            var addReportResult = addReportsForm.ShowDialog();

            if (addReportResult == DialogResult.OK)
            {
                var newReportInfo = addReportsForm.NewReportInfo;
                if (addingIndex == _newReportList.Count)
                    _newReportList.Add(newReportInfo);
                else
                {
                    _newReportList[addingIndex] = newReportInfo;
                    gridReportSettings.Rows.RemoveAt(addingIndex);
                }
                gridReportSettings.Rows.Insert(addingIndex, newReportInfo.AsArray());
            }
        }

        private void btnEditReport_Click(object sender, EventArgs e)
        {
            Program.LogInfo("Editing report");
            var indexSelected = gridReportSettings.SelectedRows[0].Index;
            if (indexSelected == _newReportList.Count)
            {
                ShowAddReportDialog(_newReportList.Count);
            }
            else
            {
                var editingReport = _newReportList[indexSelected];
                ShowAddReportDialog(indexSelected, editingReport);
            }
        }

        private void btnDeleteReport_Click(object sender, EventArgs e)
        {
            var indexToDelete = gridReportSettings.SelectedRows[0].Index;
            _newReportList.RemoveAt(indexToDelete);
            gridReportSettings.Rows.RemoveAt(indexToDelete);

        }

        private void gridReportSettings_SelectionChanged(object sender, EventArgs e)
        {
            if (_isBusy)
                gridReportSettings.ClearSelection();
            var selectedRows = gridReportSettings.SelectedRows;
            btnEditReport.Enabled = selectedRows.Count > 0;
            btnDeleteReport.Enabled = selectedRows.Count > 0 && selectedRows[0].Index < _newReportList.Count;

        }

        #endregion


        #region Save config
        
        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            Save();
        }

        private SkylineBatchConfig GetConfigFromUi()
        {
            return new SkylineBatchConfig(textConfigName.Text, DateTime.Now, DateTime.Now, GetMainSettingsFromUi(), new ReportSettings(_newReportList));
        }

        private void Save()
        {
            SkylineBatchConfig newConfig;
            try
            {
                newConfig = GetConfigFromUi();
            }
            catch (ArgumentException e)
            {
                ShowErrorDialog(e.Message);
                return;
            }

            if (_action == ConfigAction.Edit)
            {
                var editedConfig = new SkylineBatchConfig(newConfig.Name, _initialCreated, DateTime.Now, newConfig.MainSettings, newConfig.ReportSettings);

                try
                {
                    _mainControl.EditSelectedConfiguration(editedConfig);
                }
                catch (ArgumentException e)
                {
                    ShowErrorDialog(e.Message);
                    return;
                }
            }
            else
            {
                // Both add and copy create an entirely new configuration
                try
                {
                    _mainControl.AddConfiguration(newConfig);
                }
                catch (ArgumentException e)
                {
                    ShowErrorDialog(e.Message);
                    return;
                }
            }

            Close();
        }

        private void ShowErrorDialog(string message)
        {
            _mainControl.DisplayError("Configuration Validation Error", message);
        }

        #endregion

    }
}
