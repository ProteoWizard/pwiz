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
using System.Linq;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class SkylineBatchConfigForm : Form
    {
        // Allows a user to create a new configuration and add it to the list of configurations,
        // or replace an existing configuration.
        // Running configurations cannot be replaced, and will be opened in a read only mode.

        private static int _selectedTab;

        private readonly IMainUiControl _mainControl;
        private readonly RDirectorySelector _rDirectorySelector;
        private readonly bool _configEnabled;
        private readonly bool _isBusy;
        private readonly ConfigAction _action;
        private readonly RefineInputObject _refineInput;
        private readonly List<ReportInfo> _newReportList;
        private readonly Dictionary<string, string> _possibleTemplates;
        private SkylineSettings _currentSkylineSettings;
        private bool _showChangeAllSkylineSettings;

        public DownloadingFileControl templateControl;
        public DownloadingFileControl dataControl;
        public DownloadingFileControl annotationsControl;

        private string _lastEnteredPath;

        public SkylineBatchConfigForm(IMainUiControl mainControl, RDirectorySelector rDirectorySelector, SkylineBatchConfig config, ConfigAction action, bool isBusy, SkylineBatchConfigManagerState configManagerStartState)
        {
            InitializeComponent();
            Icon = Program.Icon();

            _action = action;
            _refineInput = config != null ? config.RefineSettings.CommandValuesCopy : new RefineInputObject();
            _newReportList = new List<ReportInfo>();
            _rDirectorySelector = rDirectorySelector;
            _mainControl = mainControl;
            State = configManagerStartState;
            _possibleTemplates = State.Templates.ToDictionary(pair => pair.Key, pair => pair.Value);
            var numConfigs = State.ConfigRunners.Count;
            _showChangeAllSkylineSettings = (numConfigs == 1 && _action != ConfigAction.Edit) || numConfigs > 1;
            if (_action == ConfigAction.Edit && config != null && _possibleTemplates.ContainsKey(config.Name))
                _possibleTemplates.Remove(config.Name);
            if (config != null)
                _configEnabled = config.Enabled;
            _isBusy = isBusy;

            InitInputFieldsFromConfig(config);

            if (isBusy)
            {
                lblConfigRunning.Show();
                // save and cancel buttons are replaced with OK button
                btnSaveConfig.Hide();
                btnCancelConfig.Hide();
                btnOkConfig.Show();
                AcceptButton = btnOkConfig;

                DisableUserInputs();
            }

            tabsConfig.SelectedIndex = _action == ConfigAction.Edit ? _selectedTab : 0;

            ActiveControl = textConfigName;
        }

        public SkylineBatchConfigManagerState State { get; private set; }
        public SkylineTypeControl SkylineTypeControl { get; private set; }

        private bool ShowTemplateComboBox => _possibleTemplates.Count > 0 && !_isBusy;

        private void InitInputFieldsFromConfig(SkylineBatchConfig config)
        {
            textConfigName.Text = _action == ConfigAction.Add ? string.Empty : config.Name;
            textConfigName.TextChanged += textConfigName_TextChanged;
            _lastEnteredPath = config != null ? config.MainSettings.Template.DisplayPath : null;
            checkBoxLogTestFormat.Checked = config != null && !SkylineInstallations.HasLocalSkylineCmd && config.LogTestFormat;

            SetInitialMainSettings(config);
            SetInitialFileSettings(config);
            SetInitialRefineSettings(config);
            SetInitialReportSettings(config);
            InitSkylineTab(config);
        }

        public void DisableUserInputs(Control parentControl = null)
        {
            if (parentControl == null) parentControl = Controls[0];

            if (parentControl is TextBoxBase @base)
                @base.ReadOnly = true;
            if (parentControl is ComboBox comboBox)
                comboBox.Enabled = false;
            if (parentControl is ButtonBase buttonBase && !buttonBase.Text.Equals(btnOkConfig.Text))
                buttonBase.Enabled = false;
            if (parentControl is ToolStrip strip)
                strip.Enabled = false;
            if (parentControl is PropertyGrid grid)
            {
                var properties = ((RefineInputObject)grid.SelectedObject).GetProperties();
                foreach (GlobalizedPropertyDescriptor prop in properties)
                    prop.ReadOnly = true;
                return;
            }

            foreach (Control control in parentControl.Controls)
            {
                DisableUserInputs(control);
            }
        }

        #region Edit main settings

        private void InitMainSettingsTab(SkylineBatchConfig config)
        {
            var setMainState = new Action<SkylineBatchConfigManagerState> ((state) => State = state);
            var getMainState = new Func<SkylineBatchConfigManagerState>(() => State);
            templateControl = new DownloadingFileControl(Resources.SkylineBatchConfigForm_InitMainSettingsTab_Skyline_template_file_path, 
                Resources.SkylineBatchConfigForm_InitMainSettingsTab_Template_File, config?.MainSettings.Template.FilePath, 
                TextUtil.FILTER_SKY + "|" + TextUtil.FILTER_SKY_ZIP, config?.MainSettings.Template.PanoramaFile, false, "Download template from Panorama", _mainControl,
                setMainState, getMainState);
            templateControl.AddPathChangedHandler(templateControl_PathChanged);
            templateControl.Dock = DockStyle.Fill;
            templateControl.Show();
            panelTemplate.Controls.Add(templateControl);

            dataControl = new DownloadingFileControl(Resources.SkylineBatchConfigForm_InitMainSettingsTab_Data_directory,
                Resources.SkylineBatchConfigForm_InitMainSettingsTab_Data_directory, 
                config?.MainSettings.DataFolderPath, null, config?.MainSettings.Server, true, "Download data", _mainControl,
                setMainState, getMainState);
            dataControl.Dock = DockStyle.Fill;
            dataControl.Show();
            panelData.Controls.Add(dataControl);

            annotationsControl = new DownloadingFileControl(Resources.SkylineBatchConfigForm_InitMainSettingsTab_Annotations_file__optional_, 
                Resources.SkylineBatchConfigForm_InitMainSettingsTab_Annotations_File, config?.MainSettings.AnnotationsFilePath, 
                TextUtil.FILTER_CSV, config?.MainSettings.AnnotationsDownload, false, "Download annotations file from Panorama", _mainControl,
                setMainState, getMainState);
            annotationsControl.Dock = DockStyle.Fill;
            annotationsControl.Show();
            panelAnnotations.Controls.Add(annotationsControl);
        }

        private void SetInitialMainSettings(SkylineBatchConfig config)
        {
            InitMainSettingsTab(config);
            
            if (ShowTemplateComboBox)
            {
                comboTemplateFile.Visible = true;
                foreach (var possibleTemplate in _possibleTemplates.Values)
                    comboTemplateFile.Items.Add(possibleTemplate);
                templateControl.AddPathChangedHandler(templateControl_PathChangedCombo);
            }

            if (config != null)
            {
                var mainSettings = config.MainSettings;
                if (_action == ConfigAction.Add)
                {
                    var directoryName = Path.GetDirectoryName(mainSettings.AnalysisFolderPath);
                    textAnalysisPath.Text =
                        directoryName != null ? Path.Combine(directoryName, string.Empty) : string.Empty;
                }
                else
                {
                    textAnalysisPath.Text = mainSettings.AnalysisFolderPath;
                    checkBoxUseFolderName.Checked = mainSettings.UseAnalysisFolderName;
                    textReplicateNamingPattern.Text = mainSettings.ReplicateNamingPattern;
                }
                comboTemplateFile.Text = mainSettings.Template.FilePath;
                comboTemplateFile.TextChanged += comboTemplateFile_TextChanged;
            }
            UpdateAnalysisFileName();
        }

        private MainSettings GetMainSettingsFromUi()
        {
            var templateFilePath = templateControl.Path;
            string dependentConfig = null;
            foreach (var configName in _possibleTemplates.Keys)
            {
                if (_possibleTemplates[configName].Equals(templateFilePath))
                    dependentConfig = configName;
            }
            var panoramaFile = (PanoramaFile)templateControl.Server;
            panoramaFile = panoramaFile == null ? null : new PanoramaFile(State.FileSources[panoramaFile.FileSource.Name], panoramaFile.RelativePath, panoramaFile.DownloadFolder, panoramaFile.FileName);
            var template = SkylineTemplate.FromUi(templateFilePath, dependentConfig, panoramaFile);

            var analysisFolderPath = textAnalysisPath.Text;
            var useAnalysisFolderName = checkBoxUseFolderName.Checked;
            var dataFolderPath = dataControl.Path;
            var server = ((DataServerInfo)dataControl.Server);
            server = server == null ? null : new DataServerInfo(State.FileSources[server.FileSource.Name], server.RelativePath, server.DataNamingPattern, server.Folder);
            var annotationsFilePath = annotationsControl.Path;
            var annotationsDownload = (PanoramaFile)annotationsControl.Server;
            annotationsDownload = annotationsDownload == null ? null : new PanoramaFile(State.FileSources[annotationsDownload.FileSource.Name], annotationsDownload.RelativePath, annotationsDownload.DownloadFolder, annotationsDownload.FileName);
            var replicateNamingPattern = textReplicateNamingPattern.Text;
            
            return new MainSettings(template, analysisFolderPath, useAnalysisFolderName, dataFolderPath, server, annotationsFilePath, annotationsDownload, replicateNamingPattern);
        }

        private void textConfigName_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(textAnalysisPath.Text))
            {
                var parentPath = Path.GetDirectoryName(textAnalysisPath.Text);
                textAnalysisPath.Text = Path.Combine(parentPath ?? string.Empty, textConfigName.Text);
            }

        }

        private void templateControl_PathChangedCombo(object sender, EventArgs e)
        {
            if (!comboTemplateFile.Text.Equals(templateControl.Path))
                comboTemplateFile.Text = templateControl.Path;
            UpdateAnalysisFileName();
        }

        private void templateControl_PathChanged(object sender, EventArgs e)
        {
            UpdateAnalysisFileName();
        }

        private void comboTemplateFile_TextChanged(object sender, EventArgs e)
        {
            if (!comboTemplateFile.Text.Equals(templateControl.Path))
            {
                var newPath = comboTemplateFile.Text;
                // timer ensures the change isn't overridden by the SelectedIndexChanged event
                var timer = new Timer { Interval = 1 };
                timer.Tick += (senderObj, eventArgs) =>
                {
                    timer.Stop();
                    comboTemplateFile.TextChanged -= comboTemplateFile_TextChanged;
                    comboTemplateFile.SelectedIndex = -1;
                    comboTemplateFile.Text = newPath;
                    comboTemplateFile.TextChanged += comboTemplateFile_TextChanged;
                    templateControl.SetPath(newPath);
                    UpdateAnalysisFileName();
                };
                timer.Start();
            }
        }


        private void btnAnalysisFilePath_Click(object sender, EventArgs e)
        {
            var initialPath = FileUtil.GetInitialDirectory(textAnalysisPath.Text, _lastEnteredPath);
            var path = UiFileUtil.OpenFolder(initialPath);
            if (path != null)
            {
                textAnalysisPath.Text = path;
                _lastEnteredPath = path;
            }
        }

        private void UpdateAnalysisFileName()
        {
            try
            {
                var fileName = checkBoxUseFolderName.Checked ?
                     Path.GetFileName(textAnalysisPath.Text) + TextUtil.EXT_SKY : Path.GetFileName(templateControl.Path);
                textAnalysisFileName.Text = fileName.EndsWith(TextUtil.EXT_SKY_ZIP) ? fileName.Replace(TextUtil.EXT_SKY_ZIP, TextUtil.EXT_SKY) : fileName;

            }
            catch (Exception)
            {
                textAnalysisFileName.Text = string.Empty;
            }
        }

        private void checkBoxUseFolderName_CheckedChanged(object sender, EventArgs e)
        {
            UpdateAnalysisFileName();
        }

        private void textAnalysisPath_TextChanged(object sender, EventArgs e)
        {
            UpdateAnalysisFileName();
        }

        private void linkLabelRegex_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.regular-expressions.info/reference.html");
        }

        #endregion

        #region File Settings

        private void SetInitialFileSettings(SkylineBatchConfig config)
        {
            if (_action == ConfigAction.Add) return;
            var fileSettings = config.FileSettings;
            if (config.FileSettings.MsOneResolvingPower != null)
                textMsOneResolvingPower.Text = TextUtil.ToUiString(fileSettings.MsOneResolvingPower);
            if (config.FileSettings.MsMsResolvingPower != null)
                textMsMsResolvingPower.Text = TextUtil.ToUiString(fileSettings.MsMsResolvingPower);
            if (config.FileSettings.RetentionTime != null)
                textRetentionTime.Text = TextUtil.ToUiString(fileSettings.RetentionTime);
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
            return FileSettings.FromUi(textMsOneResolvingPower.Text, textMsMsResolvingPower.Text, textRetentionTime.Text,
                checkBoxDecoys.Checked, radioShuffleDecoys.Enabled && radioShuffleDecoys.Checked, checkBoxMProphet.Checked);
        }

        #endregion

        #region Refine Settings

        private void SetInitialRefineSettings(SkylineBatchConfig config)
        {
            var outputFilePath = config == null ? null : config.RefineSettings.OutputFilePath;
            gridRefineInputs.SelectedObject = _refineInput;

            if (config != null && _action != ConfigAction.Add && !string.IsNullOrEmpty(outputFilePath))
            {
                var refineSettings = config.RefineSettings;
                textRefinedFilePath.Text = refineSettings.OutputFilePath;
                checkBoxRemoveData.Checked = refineSettings.RemoveResults;
                checkBoxRemoveDecoys.Checked = refineSettings.RemoveDecoys;
            }
            else
            {
                ToggleRefineEnabled(false);
            }
        }

        private void textBoxRefinedFilePath_TextChanged(object sender, EventArgs e)
        {
            ToggleRefineEnabled(!string.IsNullOrEmpty(textRefinedFilePath.Text));
        }
        private bool _refineDefaultsActivated;

        private void ToggleRefineEnabled(bool enabled)
        {
            checkBoxRemoveDecoys.Enabled = enabled;
            checkBoxRemoveData.Enabled = enabled;
            var properties = _refineInput.GetProperties();
            foreach (GlobalizedPropertyDescriptor prop in properties)
                prop.ReadOnly = !enabled;
            gridRefineInputs.SelectedObject = _refineInput;

            if (enabled && !_refineDefaultsActivated)
            {
                _refineDefaultsActivated = true;
                checkBoxRemoveDecoys.Checked = true;
                checkBoxRemoveData.Checked = true;
            }

            if (!enabled)
            {
                checkBoxRemoveDecoys.Checked = false;
                checkBoxRemoveData.Checked = false;
            }

        }

        private RefineSettings GetRefineSettingsFromUi()
        {
            var removeDecoys = checkBoxRemoveDecoys.Checked;
            var removeData = checkBoxRemoveData.Checked;
            var outputFilePath = textRefinedFilePath.Text;
            return new RefineSettings(_refineInput, removeDecoys, removeData, outputFilePath);
        }

        private void btnRefinedFilePath_Click(object sender, EventArgs e)
        {
            var initialDirectory = FileUtil.GetInitialDirectory(textRefinedFilePath.Text, _lastEnteredPath);
            var file = UiFileUtil.OpenFile(initialDirectory, TextUtil.FILTER_SKY, true);
            if (file != null)
            {
                textRefinedFilePath.Text = file;
                _lastEnteredPath = file;
            }
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

        private ReportSettings GetReportSettingsFromUI()
        {
            var reportList = new List<ReportInfo>();
            foreach (var report in _newReportList)
            {
                var rScriptServers = new Dictionary<string, PanoramaFile>();
                foreach(var rScript in report.RScriptServers.Keys)
                {
                    var panoramaFile = report.RScriptServers[rScript];
                    rScriptServers.Add(rScript, panoramaFile.ReplacedRemoteFileSource(panoramaFile.FileSource, State.FileSources[panoramaFile.FileSource.Name], out _));
                }
                reportList.Add(new ReportInfo(report.Name, report.CultureSpecific, report.ReportPath, report.RScripts.ToList(), rScriptServers, report.UseRefineFile));
            }
            return new ReportSettings(reportList);
        }

        private void btnAddReport_Click(object sender, EventArgs e)
        {
            ProgramLog.Info(Resources.SkylineBatchConfigForm_btnAddReport_Click_Creating_new_report_);
            ShowAddReportDialog(_newReportList.Count);
        }

        private void ShowAddReportDialog(int addingIndex, ReportInfo editingReport = null)
        {
            var addReportsForm = new ReportsAddForm(_mainControl, _rDirectorySelector, !string.IsNullOrEmpty(textRefinedFilePath.Text), State, editingReport);
            var addReportResult = addReportsForm.ShowDialog();

            if (addReportResult == DialogResult.OK)
            {
                var newReportInfo = addReportsForm.NewReportInfo;
                if (addingIndex < _newReportList.Count) // existing report was edited
                {
                    _newReportList.RemoveAt(addingIndex);
                    gridReportSettings.Rows.RemoveAt(addingIndex);
                }
                _newReportList.Insert(addingIndex, newReportInfo);
                gridReportSettings.Rows.Insert(addingIndex, newReportInfo.AsObjectArray());
            }
        }

        private void btnEditReport_Click(object sender, EventArgs e)
        {
            ProgramLog.Info(Resources.SkylineBatchConfigForm_btnEditReport_Click_Editing_report_);
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

        private void InitSkylineTab(SkylineBatchConfig config)
        {
            if (SkylineInstallations.HasLocalSkylineCmd)
            {
                tabsConfig.TabPages.Remove(tabSkyline);
                return;
            }

            if (config != null)
                SkylineTypeControl = new SkylineTypeControl(_mainControl, config.UsesSkyline, config.UsesSkylineDaily, config.UsesCustomSkylinePath, config.SkylineSettings.CmdPath, State.ConfigsBusy(), State.BaseState);
            else
            {
                // Default to the first existing Skyline installation (Skyline, Skyline-daily, custom path)
                SkylineTypeControl = new SkylineTypeControl();
            }

            SkylineTypeControl.Dock = DockStyle.Fill;
            SkylineTypeControl.Show();
            panelSkylineSettings.Controls.Add(SkylineTypeControl);
            try
            {
                _currentSkylineSettings = GetSkylineSettingsFromUi();
            }
            catch (ArgumentException)
            {
                _currentSkylineSettings = null;
            }
        }

        private void TabEnter(object sender, EventArgs e)
        {
            // Ask if the user wants to update all SkylineSettings if they are leaving the Skyline tab
            // after changing settings
            if (tabsConfig.TabPages[_selectedTab].Equals(tabSkyline))
                CheckIfSkylineChanged();
            _selectedTab = tabsConfig.SelectedIndex;
        }

        private void CheckIfSkylineChanged()
        {
            if (_isBusy || !_showChangeAllSkylineSettings) return; // can't change Skyline settings if config is running
            SkylineSettings changedSkylineSettings;
            try
            {
                changedSkylineSettings = GetSkylineSettingsFromUi();
            }
            catch (ArgumentException)
            {
                changedSkylineSettings = null;
            }
            if (changedSkylineSettings != null && !changedSkylineSettings.Equals(_currentSkylineSettings))
            {
                _currentSkylineSettings = changedSkylineSettings;
                State.ReplaceSkylineSettings(_currentSkylineSettings, _mainControl, out bool? replaced);
                // only set this to false if user did not want to change all settings
                _showChangeAllSkylineSettings = replaced ?? true;
            }
        }

        private SkylineSettings GetSkylineSettingsFromUi()
        {
            if (SkylineInstallations.HasLocalSkylineCmd)
                return new SkylineSettings(SkylineType.Local, null);

            return (SkylineSettings)SkylineTypeControl.GetVariable();
        }

        #endregion

        #region Save config

        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            if (tabsConfig.SelectedTab.Equals(tabSkyline))
                CheckIfSkylineChanged();
            Save();
        }

        private SkylineBatchConfig GetConfigFromUi()
        {
            var name = textConfigName.Text;
            var enabled = _action == ConfigAction.Edit ? _configEnabled : true;
            var logTestFormat = checkBoxLogTestFormat.Checked;
            var mainSettings = GetMainSettingsFromUi();
            var fileSettings = GetFileSettingsFromUi();
            var refineSettings = GetRefineSettingsFromUi();
            var reportSettings = GetReportSettingsFromUI();
            var skylineSettings = GetSkylineSettingsFromUi();
            return new SkylineBatchConfig(name, enabled, logTestFormat, DateTime.Now, mainSettings, fileSettings, refineSettings, reportSettings, skylineSettings);
        }

        private void Save()
        {

            SkylineBatchConfig newConfig;
            try
            {
                newConfig = GetConfigFromUi();
                State.BaseState.AssertUniqueName(newConfig.Name, _action == ConfigAction.Edit);
                newConfig.Validate();
            }
            catch (ArgumentException e)
            {
                AlertDlg.ShowError(this, Program.AppName(), e.Message);
                return;
            }

            if (_action == ConfigAction.Edit)
                State.UserReplaceSelected(newConfig, _mainControl);
            else
                State.UserAddConfig(newConfig, _mainControl);

            Close();
        }

        #endregion

        #region Tests

        public void ClickAddReport() => btnAddReport_Click(new object(), new EventArgs());

        public void ClickEditReport(int index)
        {
            for(int i = 0; i < gridReportSettings.Rows.Count; i++)
                gridReportSettings.Rows[i].Selected = i == index;
            btnEditReport_Click(new object(), new EventArgs());

        }


        #endregion
    }
}
