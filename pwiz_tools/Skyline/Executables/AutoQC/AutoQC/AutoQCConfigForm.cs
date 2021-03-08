﻿/*
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
        private string _lastEnteredPath;

        public AutoQcConfigForm(IMainUiControl mainControl, AutoQcConfig config, ConfigAction action, bool isBusy)
        {
            InitializeComponent();
            
            _action = action;
            _initialCreated = config?.Created ?? DateTime.MinValue;
            _mainControl = mainControl;

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


        private void InitInputFieldsFromConfig(AutoQcConfig config)
        {
            if (config != null) _lastEnteredPath = config.MainSettings.SkylineFilePath;
            InitSkylineTab(config);
            SetInitialPanoramaSettings(config);
            if (_action == ConfigAction.Add || config == null)
            {
                SetDefaultMainSettings();
                return;
            }
            textConfigName.Text = config.Name;
            SetInitialMainSettings(config.MainSettings);
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
            var mainSettings = new MainSettings(skylineFilePath, folderToWatch, includeSubfolders, qcFileFilter, removeResults, resultsWindow, instrumentType, acquisitionTime);
            return mainSettings;
        }

        private void btnSkylineFilePath_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = TextUtil.FILTER_SKY,
                InitialDirectory = TextUtil.GetInitialDirectory(textSkylinePath.Text, _lastEnteredPath)
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
            dialog.SelectedPath = TextUtil.GetInitialDirectory(textFolderToWatchPath.Text, _lastEnteredPath);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textFolderToWatchPath.Text = dialog.SelectedPath;
                _lastEnteredPath = dialog.SelectedPath;
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

        private void SetInitialPanoramaSettings(AutoQcConfig config)
        {
            if (config == null)
            {
                SetDefaultPanoramaSettings();
                return;
            }
            var panoramaSettings = config.PanoramaSettings;
            textPanoramaUrl.Text = panoramaSettings.PanoramaServerUrl;
            textPanoramaEmail.Text = panoramaSettings.PanoramaUserEmail;
            textPanoramaPasswd.Text = panoramaSettings.PanoramaPassword;
            textPanoramaFolder.Text = panoramaSettings.PanoramaFolder;
            cbPublishToPanorama.Checked = panoramaSettings.PublishToPanorama;
            groupBoxPanorama.Enabled = panoramaSettings.PublishToPanorama;
        }

        private void SetDefaultPanoramaSettings()
        {
            cbPublishToPanorama.Checked = PanoramaSettings.GetDefaultPublishToPanorama();
            groupBoxPanorama.Enabled = PanoramaSettings.GetDefaultPublishToPanorama();
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
                _skylineTypeControl = new SkylineTypeControl(config.UsesSkyline, config.UsesSkylineDaily, config.UsesCustomSkylinePath, config.SkylineSettings.CmdPath);
            else
                _skylineTypeControl = new SkylineTypeControl();

            _skylineTypeControl.Dock = DockStyle.Fill;
            _skylineTypeControl.Show();
            panelSkylineSettings.Controls.Add(_skylineTypeControl);
        }

        private SkylineSettings GetSkylineSettingsFromUi()
        {
            return new SkylineSettings(_skylineTypeControl.Type, _skylineTypeControl.CommandPath);
        }

        #endregion


        #region Save config

        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
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
            var newConfig = GetConfigFromUi();
            try
            {
                if (_action == ConfigAction.Edit)
                    _mainControl.ReplaceSelectedConfig(newConfig);
                else
                    _mainControl.AddConfiguration(newConfig);
            }
            catch (ArgumentException e)
            {
                AlertDlg.ShowError(this, Program.AppName, e.Message);
                return;
            }

            Close();
        }

        private void btnOkConfig_Click(object sender, EventArgs e)
        {
            Close();
        }

        #endregion
    }
}
