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
using SharedAutoQcBatch;
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
        // Running configurations cannot be replaced, and will be opened in a read only mode.
        
        private readonly IMainUiControl _mainControl;
        private readonly bool _initialEnabled;
        private readonly bool _isBusy;
        private readonly ConfigAction _action;
        private readonly List<ReportInfo> _newReportList;
        private readonly bool _canEditSkylineSettings;

        private SkylineTypeControl _skylineTypeControl;

        public SkylineBatchConfigForm(IMainUiControl mainControl, SkylineBatchConfig config, ConfigAction action, bool isBusy)
        {
            InitializeComponent();
            _action = action;
            _newReportList = new List<ReportInfo>();

            _mainControl = mainControl;
            _initialEnabled = config.Enabled;
            _isBusy = isBusy;

            _canEditSkylineSettings = !SkylineInstallations.HasLocalSkylineCmd;
            if (!_canEditSkylineSettings)
                tabsConfig.TabPages[2].Hide();
            
            InitInputFieldsFromConfig(config);
            lblConfigRunning.Hide();

            if (isBusy)
            {
                lblConfigRunning.Show();
                btnSaveConfig.Hide(); // save and cancel buttons are replaced with OK button
                btnCancelConfig.Hide();
                btnOkConfig.Show();
                AcceptButton = btnOkConfig;
                DisableUserInputs();
            }

            ActiveControl = textConfigName;
        }

        private void InitInputFieldsFromConfig(SkylineBatchConfig config)
        {
            if (config == null)
                return;

            textConfigName.Text = _action == ConfigAction.Add ? string.Empty : config.Name;
            textConfigName.TextChanged += textConfigName_TextChanged;
            // Initialize UI input values using config
            SetInitialMainSettings(config);
            SetInitialFileSettings(config);
            SetInitialReportSettings(config);
            SetInitialSkylineSettings(config);
        }

        public void DisableUserInputs(Control parentControl = null)
        {
            if (parentControl == null) parentControl = Controls[0];

            if (parentControl is TextBoxBase @base)
                @base.ReadOnly = true;
            if (parentControl is ButtonBase buttonBase && !buttonBase.Text.Equals(btnOkConfig.Text))
                buttonBase.Enabled = false;
            if (parentControl is ToolStrip strip)
                strip.Enabled = false;

            foreach (Control control in parentControl.Controls)
            {
                DisableUserInputs(control);
            }
        }
        
        #region Edit main settings

        private void SetInitialMainSettings(SkylineBatchConfig config)
        {
            var mainSettings = config.MainSettings;
            textAnalysisPath.Text = mainSettings.AnalysisFolderPath;
            textNamingPattern.Text = mainSettings.ReplicateNamingPattern;
            if (_action == ConfigAction.Add)
            {
                textAnalysisPath.Text = Path.GetDirectoryName(mainSettings.AnalysisFolderPath) + @"\";
                textNamingPattern.Text = string.Empty;
            }

            textSkylinePath.Text = mainSettings.TemplateFilePath;
            textDataPath.Text = mainSettings.DataFolderPath;
        }

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
            openDialog.Filter = TextUtil.FILTER_SKY;
            openDialog.InitialDirectory = Path.GetDirectoryName(textSkylinePath.Text);
            if (openDialog.ShowDialog()== DialogResult.OK)
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
            var initialPath = textbox.Text;
            while (!Directory.Exists(initialPath) && !string.IsNullOrEmpty(initialPath))
                initialPath = Path.GetDirectoryName(initialPath);
            dialog.SelectedPath = initialPath;
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
        
        #region File Settings

        private void SetInitialFileSettings(SkylineBatchConfig config)
        {
            if (config.FileSettings.MsOneResolvingPower != null)
                textMsOneResolvingPower.Text = config.FileSettings.MsOneResolvingPower;
            if (config.FileSettings.MsMsResolvingPower != null)
                textMsMsResolvingPower.Text = config.FileSettings.MsMsResolvingPower;
            if (config.FileSettings.RetentionTime != null)
                textRetentionTime.Text = config.FileSettings.RetentionTime;
            checkBoxDecoys.Checked = config.FileSettings.AddDecoys;
            radioShuffleDecoys.Checked = config.FileSettings.ShuffleDecoys;
            checkBoxMProphet.Checked = config.FileSettings.TrainMProphet;
        }

        private void checkBoxDecoys_CheckedChanged(object sender, EventArgs e)
        {
            radioShuffleDecoys.Enabled = checkBoxDecoys.Checked;
            radioReverseDecoys.Enabled = checkBoxDecoys.Checked;
        }

        private FileSettings GetFileSettingsFromUi()
        {
            return new FileSettings(textMsOneResolvingPower.Text, textMsMsResolvingPower.Text, textRetentionTime.Text, 
                checkBoxDecoys.Checked, radioShuffleDecoys.Enabled && radioShuffleDecoys.Checked, checkBoxMProphet.Checked);
        }
        
        #endregion
        
        #region Reports

        private void SetInitialReportSettings(SkylineBatchConfig config)
        {
            if (_action == ConfigAction.Add)
                return;

            foreach (var report in config.ReportSettings.Reports)
            {
                _newReportList.Add(report);
                gridReportSettings.Rows.Add(report.AsObjectArray());
            }
        }

        private void btnAddReport_Click(object sender, EventArgs e)
        {
            ProgramLog.LogInfo(Resources.SkylineBatchConfigForm_btnAddReport_Click_Creating_new_report_);
            ShowAddReportDialog(_newReportList.Count);
        }

        private void ShowAddReportDialog(int addingIndex, ReportInfo editingReport = null)
        {
            var addReportsForm = new ReportsAddForm(_mainControl, editingReport);
            var addReportResult = addReportsForm.ShowDialog();

            if (addReportResult == DialogResult.OK)
            {
                var newReportInfo = addReportsForm.NewReportInfo;
                if (addingIndex < _newReportList.Count) // existing report was edited
                {
                    _newReportList.RemoveAt(addingIndex);
                    gridReportSettings.Rows.RemoveAt(addingIndex);
                }
                _newReportList.Insert(addingIndex,newReportInfo);
                gridReportSettings.Rows.Insert(addingIndex, newReportInfo.AsObjectArray());
            }
        }

        private void btnEditReport_Click(object sender, EventArgs e)
        {
            ProgramLog.LogInfo(Resources.SkylineBatchConfigForm_btnEditReport_Click_Editing_report_);
            var indexSelected = gridReportSettings.SelectedRows[0].Index;
            var editingReport = _newReportList.Count > indexSelected ? _newReportList[indexSelected] : null;
            ShowAddReportDialog(indexSelected, editingReport);
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
        
        #region Skyline Settings
        
        private void SetInitialSkylineSettings(SkylineBatchConfig config)
        {
            if (!_canEditSkylineSettings) return;

            _skylineTypeControl = new SkylineTypeControl(config.UsesSkyline, config.UsesSkylineDaily, config.UsesCustomSkylinePath, config.SkylineSettings.CmdPath);
            _skylineTypeControl.Dock = DockStyle.Fill;
            _skylineTypeControl.Show();
            panelSkylineSettings.Controls.Add(_skylineTypeControl);
        }
        
        private SkylineSettings GetSkylineSettingsFromUi()
        {
            if (!_canEditSkylineSettings)
                return new SkylineSettings(SkylineType.Local);
            
            return new SkylineSettings(_skylineTypeControl.Type, _skylineTypeControl.CommandPath);
        }

        #endregion
        
        #region Save config

        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            Save();
        }

        private SkylineBatchConfig GetConfigFromUi()
        {
            var name = textConfigName.Text;
            var enabled = _action == ConfigAction.Edit ? _initialEnabled : true;
            var mainSettings = GetMainSettingsFromUi();
            var fileSettings = GetFileSettingsFromUi();
            var reportSettings = new ReportSettings(_newReportList);
            var skylineSettings = GetSkylineSettingsFromUi();
            return new SkylineBatchConfig(name, enabled, DateTime.Now, mainSettings, fileSettings, reportSettings, skylineSettings);
        }

        private void Save()
        {
            try
            {
                //throws ArgumentException if any fields are invalid
                var newConfig = GetConfigFromUi();
                newConfig.Validate();
                //throws ArgumentException if config has a duplicate name
                if (_action == ConfigAction.Edit)
                    _mainControl.EditSelectedConfiguration(newConfig);
                else
                    _mainControl.AddConfiguration(newConfig);
            }
            catch (ArgumentException e)
            {
                ShowErrorDialog(e.Message);
                return;
            }

            Close();
        }

        private void ShowErrorDialog(string message)
        {
            AlertDlg.ShowError(this, message);
        }
        
        #endregion
    }
}
