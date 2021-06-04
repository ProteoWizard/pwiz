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
using System.Drawing;
using System.IO;
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

        private static int _selectedTab = 0;

        private readonly IMainUiControl _mainControl;
        private readonly RDirectorySelector _rDirectorySelector;
        private readonly bool _configEnabled;
        private readonly bool _isBusy;
        private readonly ConfigAction _action;
        private readonly RefineInputObject _refineInput;
        private readonly List<ReportInfo> _newReportList;
        private readonly Dictionary<string, string> _possibleTemplates;
        private readonly SkylineBatchConfigManager _configManager;
        private SkylineSettings _currentSkylineSettings;
        private readonly Image _downloadImage;
        private readonly Image _downloadSelectedImage;
        private DataServerInfo _dataServer;
        private bool _showChangeAllSkylineSettings;
        public Control templateFileControl;

        private string _lastEnteredPath;

        public SkylineBatchConfigForm(IMainUiControl mainControl, RDirectorySelector rDirectorySelector, SkylineBatchConfig config, ConfigAction action, bool isBusy, SkylineBatchConfigManager configManager)
        {
            InitializeComponent();
            Icon = Program.Icon();
            _downloadImage = (Image)Resources.ResourceManager.GetObject("download");
            _downloadSelectedImage = (Image)Resources.ResourceManager.GetObject("downloadSelected");

            _action = action;
            _refineInput = config != null ? config.RefineSettings.CommandValuesCopy : new RefineInputObject();
            _newReportList = new List<ReportInfo>();
            _rDirectorySelector = rDirectorySelector;
            _mainControl = mainControl;
            _configManager = configManager;
            _possibleTemplates = configManager.GetRefinedTemplates();
            var numConfigs = configManager.ConfigNamesAsObjectArray().Length;
            _showChangeAllSkylineSettings = (numConfigs == 1 && _action != ConfigAction.Edit) || numConfigs > 1;
            if (_action == ConfigAction.Edit && config != null && _possibleTemplates.ContainsKey(config.Name))
                _possibleTemplates.Remove(config.Name);
            if (config != null)
                _configEnabled = config.Enabled;
            _isBusy = isBusy;

            /*var dataServers = configManager.GetServerNames;
            foreach (var serverName in dataServers)
                comboDataServer.Items.Add(serverName);
            comboDataServer.Items.Add("<Add>");*/

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

            tabsConfig.SelectedIndex = _selectedTab;

            ActiveControl = textConfigName;
        }

        public SkylineTypeControl SkylineTypeControl { get; private set; }

        private bool ShowTemplateComboBox => _possibleTemplates.Count > 0 && !_isBusy;

        private void InitInputFieldsFromConfig(SkylineBatchConfig config)
        {
            textConfigName.Text = _action == ConfigAction.Add ? string.Empty : config.Name;
            textConfigName.TextChanged += textConfigName_TextChanged;
            _lastEnteredPath = config != null ? config.MainSettings.TemplateFilePath : null;

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

        private void SetInitialMainSettings(SkylineBatchConfig config)
        {
            templateFileControl = textTemplateFile;
            if (ShowTemplateComboBox)
            {
                textTemplateFile.Hide();
                comboTemplateFile.Visible = true;
                foreach (var possibleTemplate in _possibleTemplates.Values)
                    comboTemplateFile.Items.Add(possibleTemplate);
                templateFileControl = comboTemplateFile;
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
                    textReplicateNamingPattern.Text = mainSettings.ReplicateNamingPattern;
                    if (mainSettings.WillDownloadData)
                    {
                        _dataServer = mainSettings.Server;
                        ToggleDownloadData(true);
                        /*comboDataServer.SelectedIndex = comboDataServer.Items.IndexOf(mainSettings.Server.Name);
                        textDataNamingPatten.Text = mainSettings.DataNamingPattern;*/
                    }
                }
                templateFileControl.Text = mainSettings.TemplateFilePath;
                textDataPath.Text = mainSettings.DataFolderPath;
                textAnnotationsFile.Text = mainSettings.AnnotationsFilePath;
            }
        }

        private void ToggleDownloadData(bool downloading)
        {
            btnDownloadData.BackColor = downloading ? Color.SteelBlue : Color.Transparent;
            btnDownloadData.Image = downloading ? _downloadSelectedImage : _downloadImage;
        }

        private MainSettings GetMainSettingsFromUi()
        {
            var templateFilePath = templateFileControl.Text;
            string dependentConfig = null;
            foreach (var configName in _possibleTemplates.Keys)
            {
                if (_possibleTemplates[configName].Equals(templateFilePath))
                {
                    dependentConfig = configName;
                    break;
                }
            }

            var analysisFolderPath = textAnalysisPath.Text;
            var dataFolderPath = textDataPath.Text;
            var server = _dataServer; // null if none specified
            var annotationsFilePath = textAnnotationsFile.Text;
            var replicateNamingPattern = textReplicateNamingPattern.Text;

            return new MainSettings(templateFilePath, analysisFolderPath, dataFolderPath, server, annotationsFilePath, replicateNamingPattern, dependentConfig);
        }

        private void btnDownloadData_Click(object sender, EventArgs e)
        {
            var addServerForm = new AddServerForm(_dataServer);
            if (DialogResult.OK == addServerForm.ShowDialog(this))
            {
                _dataServer = addServerForm.Server;
                ToggleDownloadData(_dataServer != null);
            }
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
            Control templatePathControl = ShowTemplateComboBox ? (Control)comboTemplateFile : textTemplateFile;
            OpenFile(templatePathControl, TextUtil.FILTER_SKY);
        }

        private void btnAnnotationsFile_Click(object sender, EventArgs e)
        {
            OpenFile(textAnnotationsFile, TextUtil.FILTER_CSV);
        }

        private void btnAnalysisFilePath_Click(object sender, EventArgs e)
        {
            OpenFolder(textAnalysisPath);
        }

        private void btnDataPath_Click(object sender, EventArgs e)
        {
            OpenFolder(textDataPath);
        }

        /*private void comboDataServer_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboDataServer.SelectedIndex == comboDataServer.Items.Count - 1)
            {
                comboDataServer.SelectedIndexChanged -= comboDataServer_SelectedIndexChanged;
                var addServerForm = new AddServerForm();
                if (DialogResult.OK == addServerForm.ShowDialog(this))
                {
                    var server = (ServerInfo)addServerForm.Server;
                    _configManager.AddValidServer(server);
                    comboDataServer.Items.Insert(comboDataServer.Items.Count - 1, server.Name);
                    comboDataServer.SelectedItem = server.Name;
                }
                else
                {
                    comboDataServer.SelectedIndex = -1;
                }
                comboDataServer.SelectedIndexChanged += comboDataServer_SelectedIndexChanged;
            }
        }*/

        private void OpenFile(Control textBox, string filter, bool save = false)
        {
            FileDialog dialog = save ? (FileDialog)new SaveFileDialog() : new OpenFileDialog();
            var initialDirectory = FileUtil.GetInitialDirectory(textBox.Text, _lastEnteredPath);
            dialog.InitialDirectory = initialDirectory;
            dialog.Filter = filter;
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                textBox.Text = dialog.FileName;
                _lastEnteredPath = dialog.FileName;
            }
        }

        private void OpenFolder(Control textBox)
        {
            var dialog = new FolderBrowserDialog();
            var initialPath = FileUtil.GetInitialDirectory(textBox.Text, _lastEnteredPath);
            dialog.SelectedPath = initialPath;
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                textBox.Text = dialog.SelectedPath;
                _lastEnteredPath = dialog.SelectedPath;
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
            OpenFile(textRefinedFilePath, TextUtil.FILTER_SKY, true);
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
            ProgramLog.Info(Resources.SkylineBatchConfigForm_btnAddReport_Click_Creating_new_report_);
            ShowAddReportDialog(_newReportList.Count);
        }

        private void ShowAddReportDialog(int addingIndex, ReportInfo editingReport = null)
        {
            var addReportsForm = new ReportsAddForm(_mainControl, _rDirectorySelector, !string.IsNullOrEmpty(textRefinedFilePath.Text), editingReport);
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
                SkylineTypeControl = new SkylineTypeControl(_mainControl, config.UsesSkyline, config.UsesSkylineDaily, config.UsesCustomSkylinePath, config.SkylineSettings.CmdPath);
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
                var replaced = _mainControl.ReplaceAllSkylineVersions(_currentSkylineSettings);
                // only set this to false if user did not want to change all settings
                _showChangeAllSkylineSettings = replaced ?? true;
            }
        }

        private SkylineSettings GetSkylineSettingsFromUi()
        {
            if (SkylineInstallations.HasLocalSkylineCmd)
                return new SkylineSettings(SkylineType.Local);

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
            var mainSettings = GetMainSettingsFromUi();
            var fileSettings = GetFileSettingsFromUi();
            var refineSettings = GetRefineSettingsFromUi();
            var reportSettings = new ReportSettings(_newReportList);
            var skylineSettings = GetSkylineSettingsFromUi();
            return new SkylineBatchConfig(name, enabled, DateTime.Now, mainSettings, fileSettings, refineSettings, reportSettings, skylineSettings);
        }

        private void Save()
        {

            SkylineBatchConfig newConfig;
            try
            {
                newConfig = GetConfigFromUi();
                _mainControl.AssertUniqueConfigName(newConfig.Name, _action == ConfigAction.Edit);
                newConfig.Validate();
            }
            catch (ArgumentException e)
            {
                AlertDlg.ShowError(this, Program.AppName(), e.Message);
                return;
            }

            if (_action == ConfigAction.Edit)
                _mainControl.ReplaceSelectedConfig(newConfig);
            else
                _mainControl.AddConfiguration(newConfig);

            Close();
        }

        #endregion

    }
}
