/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using AutoQC.Properties;
using SharedBatch;

namespace AutoQC
{
    public partial class AutoQcConfigForm : Form
    {
        // Allows a user to create a new configuration and add it to the list of configurations,
        // or replace an existing configuration.
        // Currently running configurations cannot be replaced, and will be opened in a read only mode.


        private readonly IMainUiControl _mainControl;
        private readonly ConfigAction _action;
        private readonly DateTime _initialCreated;

        private SkylineTypeControl _skylineTypeControl;
        private string _lastEnteredPath; // last entered path to Skyline .sky file
        private string _lastEnteredAnnotationsFilePath;
        private TabPage _lastSelectedTab;
        private SkylineSettings _currentSkylineSettings;

        public AutoQcConfigForm(IMainUiControl mainControl, AutoQcConfig config, ConfigAction action, AutoQcConfigManagerState state, RunnerStatus status = RunnerStatus.Stopped)
        {
            InitializeComponent();
            
            _action = action;
            _initialCreated = config?.Created ?? DateTime.MinValue;
            _mainControl = mainControl;
            State = state;

            // Initialize file filter combobox
            var filterOptions = new object[]
            {
                AllFileFilter.FilterName, 
                StartsWithFilter.FilterName, 
                EndsWithFilter.FilterName, 
                ContainsFilter.FilterName,
                RegexFilter.FilterName
            };
            comboBoxFileFilter.Items.AddRange(filterOptions);

            InitInputFieldsFromConfig(config);

            lblConfigRunning.Hide();

            if (ConfigRunner.IsBusy(status))
            {
                lblConfigRunning.Text = string.Format(Resources.AutoQcConfigForm_AutoQcConfigForm_The_configuration_is__0__and_cannot_be_edited_, status);
                lblConfigRunning.Show();
                btnSaveConfig.Hide(); // save and cancel buttons are replaced with OK button
                btnCancelConfig.Hide();
                btnOkConfig.Show();
                AcceptButton = btnOkConfig;
                DisableUserInputs();
            }

            ActiveControl = textConfigName;
        }

        public AutoQcConfigManagerState State { get; private set; }

        private void InitInputFieldsFromConfig(AutoQcConfig config)
        {
            if (config != null) _lastEnteredPath = config.MainSettings.SkylineFilePath;
            InitSkylineTab(config);
            
            if (_action == ConfigAction.Add || config == null)
            {
                SetDefaultMainSettings();
                // If we are given a config (e.g. the most recently modified config) take the Panorama server URL from the config
                // so that the user does not have to enter the URL again. But don't copy anything else.
                SetDefaultPanoramaSettings(config?.PanoramaSettings.PanoramaServerUrl);
                return;
            }
            textConfigName.Text = config.Name;
            SetInitialMainSettings(config.MainSettings);
            SetInitialPanoramaSettings(config.PanoramaSettings);
        }

        public void DisableUserInputs(Control parentControl = null)
        {
            if (parentControl == null) parentControl = Controls[0];

            if (parentControl is TextBoxBase)
                ((TextBoxBase)parentControl).ReadOnly = true;
            if (parentControl is CheckBox)
                ((CheckBox)parentControl).Enabled = false;
            if (parentControl is ComboBox)
                ((ComboBox)parentControl).Enabled = false;
            if (parentControl is ButtonBase buttonBase && !buttonBase.Text.Equals(btnOkConfig.Text))
                buttonBase.Enabled = false;
            
            foreach (Control control in parentControl.Controls)
            {
                DisableUserInputs(control);
            }
        }

        #region Main settings

        private void SetInitialMainSettings(MainSettings mainSettings)
        {
            textSkylinePath.Text = mainSettings.SkylineFilePath;
            textFolderToWatchPath.Text = mainSettings.FolderToWatch;
            includeSubfoldersCb.Checked = mainSettings.IncludeSubfolders;
            textQCFilePattern.Text = mainSettings.QcFileFilter.Pattern;
            comboBoxFileFilter.SelectedItem = mainSettings.QcFileFilter.Name();
            textResultsTimeWindow.Text = mainSettings.ResultsWindow.ToString();
            checkBoxRemoveResults.Checked = mainSettings.RemoveResults;
            textAquisitionTime.Text = mainSettings.AcquisitionTime.ToString();
            comboBoxInstrumentType.SelectedItem = mainSettings.InstrumentType;
            comboBoxInstrumentType.SelectedIndex = comboBoxInstrumentType.FindStringExact(mainSettings.InstrumentType);
            if (mainSettings.HasAnnotationsFile())
            {
                textAnnotationsFilePath.Text = mainSettings.AnnotationsFilePath;
            }
        }

        private void SetDefaultMainSettings()
        {
            comboBoxFileFilter.SelectedItem = MainSettings.GetDefaultQcFileFilter().Name();
            textResultsTimeWindow.Text = MainSettings.GetDefaultResultsWindow();
            checkBoxRemoveResults.Checked = MainSettings.GetDefaultRemoveResults();
            textAquisitionTime.Text = MainSettings.GetDefaultAcquisitionTime();
            comboBoxInstrumentType.SelectedItem = MainSettings.GetDefaultInstrumentType();
            comboBoxInstrumentType.SelectedIndex = comboBoxInstrumentType.FindStringExact(MainSettings.GetDefaultInstrumentType());
        }

        private MainSettings GetMainSettingsFromUi()
        {
            var skylineFilePath = textSkylinePath.Text;
            var folderToWatch = textFolderToWatchPath.Text;
            var includeSubfolders = includeSubfoldersCb.Checked;
            var qcFileFilter = FileFilter.GetFileFilter(comboBoxFileFilter.SelectedItem.ToString(),
                textQCFilePattern.Text);
            var removeResults = checkBoxRemoveResults.Checked;
            var resultsWindow = textResultsTimeWindow.Text;
            var instrumentType = comboBoxInstrumentType.SelectedItem.ToString();
            var acquisitionTime = textAquisitionTime.Text;
            var annotationsFilePath = textAnnotationsFilePath.Text;
            var mainSettings = new MainSettings(skylineFilePath, folderToWatch, includeSubfolders, qcFileFilter, removeResults, resultsWindow, instrumentType, acquisitionTime, annotationsFilePath);
            return mainSettings;
        }

        private void btnSkylineFilePath_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = TextUtil.FILTER_SKY,
                InitialDirectory = FileUtil.GetInitialDirectory(textSkylinePath.Text, _lastEnteredPath)
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textSkylinePath.Text = dialog.FileName;
                _lastEnteredPath = dialog.FileName;
            }
        }
        
        private void btnFolderToWatch_Click(object sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.SelectedPath = FileUtil.GetInitialDirectory(textFolderToWatchPath.Text, _lastEnteredPath);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textFolderToWatchPath.Text = dialog.SelectedPath;
                _lastEnteredPath = dialog.SelectedPath;
            }
        }

        private void btnAnnotationsFilePath_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = TextUtil.FileDialogFilter("CSV (Comma delimited)", TextUtil.EXT_CSV),
                InitialDirectory = FileUtil.GetInitialDirectory(_lastEnteredAnnotationsFilePath, textSkylinePath.Text)
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textAnnotationsFilePath.Text = dialog.FileName;
                _lastEnteredAnnotationsFilePath = dialog.FileName;
            }
        }

        private void comboBoxFileFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedItem = comboBoxFileFilter.SelectedItem;
            if (selectedItem.Equals(AllFileFilter.FilterName))
            {
                textQCFilePattern.Hide();
                labelQcFilePattern.Hide();
            }
            else
            {
                textQCFilePattern.Show();
                labelQcFilePattern.Show();
            }
        }

        private void checkBoxRemoveResults_CheckedChanged(object sender, EventArgs e)
        {
            textResultsTimeWindow.Enabled = checkBoxRemoveResults.Checked;
            labelAccumulationTimeWindow.Enabled = checkBoxRemoveResults.Checked;
            labelDays.Enabled = checkBoxRemoveResults.Checked;
        }

        #endregion

        #region Panorama settings

        private void SetInitialPanoramaSettings(PanoramaSettings panoramaSettings)
        {
            textPanoramaUrl.Text = panoramaSettings.PanoramaServerUrl;
            
            if (_action != ConfigAction.Copy)
            {
                // Do not set the email and password when copying from a configuration.  AutoQC Loader is run on computers accessible to more than
                // one user. We don't want the Panorama email and password for one user to be used by another user.
                textPanoramaEmail.Text = panoramaSettings.PanoramaUserEmail;
                textPanoramaPasswd.Text = panoramaSettings.PanoramaPassword;
            }

            textPanoramaFolder.Text = panoramaSettings.PanoramaFolder;
            cbPublishToPanorama.Checked = panoramaSettings.PublishToPanorama;
            groupBoxPanorama.Enabled = panoramaSettings.PublishToPanorama;
        }


        private void SetDefaultPanoramaSettings(string serverUrl = null)
        {
            cbPublishToPanorama.Checked = PanoramaSettings.GetDefaultPublishToPanorama();
            groupBoxPanorama.Enabled = PanoramaSettings.GetDefaultPublishToPanorama();
            if (serverUrl != null)
            {
                textPanoramaUrl.Text = serverUrl;
            }
        }

        private PanoramaSettings GetPanoramaSettingsFromUi()
        {
            return new PanoramaSettings(cbPublishToPanorama.Checked, textPanoramaUrl.Text, textPanoramaEmail.Text, textPanoramaPasswd.Text, textPanoramaFolder.Text);
        }

        private void cbPublishToPanorama_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxPanorama.Enabled = cbPublishToPanorama.Checked;
        }
        
        #endregion

        #region Skyline Settings

        private void InitSkylineTab(AutoQcConfig config)
        {
            if (config != null)
                _skylineTypeControl = new SkylineTypeControl(_mainControl, config.UsesSkyline, config.UsesSkylineDaily, config.UsesCustomSkylinePath, config.SkylineSettings.CmdPath, State.GetRunningConfigs(), State.BaseState);
            else
                _skylineTypeControl = new SkylineTypeControl();

            _skylineTypeControl.Dock = DockStyle.Fill;
            _skylineTypeControl.Show();
            panelSkylineSettings.Controls.Add(_skylineTypeControl);
            _currentSkylineSettings = GetSkylineSettingsFromUi();
        }

        private void TabEnter(object sender, EventArgs e)
        {
            // Ask if the user wants to update all SkylineSettings if they are leaving the Skyline tab
            // after changing settings
            var selectingTab = tabControl.SelectedTab;
            if (tabSkylineSettings.Equals(_lastSelectedTab) && !tabSkylineSettings.Equals(selectingTab))
                CheckIfSkylineChanged();
            _lastSelectedTab = selectingTab;
        }

        private void CheckIfSkylineChanged()
        {
            var changedSkylineSettings = GetSkylineSettingsFromUi();
            if (!changedSkylineSettings.Equals(_currentSkylineSettings))
            {
                _currentSkylineSettings = changedSkylineSettings;
                State.ReplaceSkylineSettings(_currentSkylineSettings, _mainControl, out bool? replaced);
            }
        }

        private SkylineSettings GetSkylineSettingsFromUi()
        {
            return new SkylineSettings(_skylineTypeControl.Type, null, _skylineTypeControl.CommandPath);
        }

        #endregion

        #region Save config

        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab.Equals(tabSkylineSettings))
                CheckIfSkylineChanged();
            Save();
        }

        private AutoQcConfig GetConfigFromUi()
        {
            var name = textConfigName.Text;
            var mainSettings = GetMainSettingsFromUi();
            var panoramaSettings = GetPanoramaSettingsFromUi();
            var skylineSettings = GetSkylineSettingsFromUi();
            var created = _action == ConfigAction.Edit ? _initialCreated : DateTime.Now;
            return new AutoQcConfig(name, false, created, DateTime.Now, mainSettings, panoramaSettings, skylineSettings);
        }

        private void Save()
        {
            AutoQcConfig newConfig = GetConfigFromUi();
            try
            {
                State.BaseState.AssertUniqueName(newConfig.Name, _action == ConfigAction.Edit);
                newConfig.Validate(true);
            }
            catch (Exception e)
            {
                AlertDlg.ShowError(this, e.Message);
                return;
            }

            if (_action == ConfigAction.Edit)
                State.ReplaceSelectedConfig(newConfig, _mainControl);
            else
                State.UserAddConfig(newConfig, _mainControl);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnOkConfig_Click(object sender, EventArgs e)
        {
            Close();
        }

        #endregion

        #region Methods used for tests
        public void SetConfigName(string name)
        {
            textConfigName.Text = name;
        }

        public void SetSkylineDocPath(string skyDocPath)
        {
            textSkylinePath.Text = skyDocPath;
        }

        public void SetFolderToWatch(string folderToWatch)
        {
            textFolderToWatchPath.Text = folderToWatch;
        }

        public void UncheckRemoveResults()
        {
            checkBoxRemoveResults.Checked = false;
        }

        public void CheckRemoveResults()
        {
            checkBoxRemoveResults.Checked = true;
        }

        public void SetAnnotationsFilePath(string annotationsFilePath)
        {
            textAnnotationsFilePath.Text = annotationsFilePath;
        }

        public void ClickSave()
        {
            btnSaveConfig.PerformClick();
        }

        public void ClickCancel()
        {
            btnCancelConfig.PerformClick();
        }

        public void ClickOk()
        {
            btnOkConfig.PerformClick();
        }

        public bool SaveButtonVisible()
        {
            return btnSaveConfig.Visible;
        }

        public bool ConfigNotEditableLabelVisible()
        {
            return lblConfigRunning.Visible;
        }

        public void SelectPanoramaTab()
        {
            tabPanoramaSettings.Select();
        }

        public void CheckUploadToPanorama()
        {
            cbPublishToPanorama.Checked = true;
        }

        public void SetPanoramaServer(string serverUri)
        {
            textPanoramaUrl.Text = serverUri;
        }

        public void SetPanoramaUser(string username)
        {
            textPanoramaEmail.Text = username;
        }

        public void SetPanoramaPassword(string password)
        {
            textPanoramaPasswd.Text = password;
        }

        public void SetPanoramaFolder(string folder)
        {
            textPanoramaFolder.Text = folder;
        }

        #endregion
    }
}
