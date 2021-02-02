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
using System.IO;
using System.Windows.Forms;
using AutoQC.Properties;

namespace AutoQC
{
    public enum ConfigAction
    {
        Add, Edit, Copy
    }

    public partial class AutoQcConfigForm : Form
    {
        // Allows a user to create a new configuration and add it to the list of configurations,
        // or replace an existing configuration.
        // Currently running configurations cannot be replaced, and will be opened in a read only mode.


        private readonly IMainUiControl _mainControl;
        private readonly ConfigAction _action;
        private readonly DateTime _initialCreated;

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
            InitSkylineTab();
            if (config == null)
            {
                SetDefaultMainSettings();
                SetDefaultPanoramaSettings();
                return;
            }

            if (_action == ConfigAction.Edit)
                textConfigName.Text = config.Name;

            SetInitialMainSettings(config.MainSettings);
            SetInitialPanoramaSettings(config.PanoramaSettings);
            SetInitialSkylineSettings(config);
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
            if (parentControl is ButtonBase && parentControl.Text != @"OK")
                ((ButtonBase)parentControl).Enabled = false;
            
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
            OpenFile(Resources.AutoQcConfigForm_btnSkylineFilePath_Click_Skyline_Files___sky____sky_All_Files__________, textSkylinePath);
        }

        private void OpenFile(string filter, TextBox textbox)
        {
            var dialog = new OpenFileDialog { Filter = filter };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textbox.Text = dialog.FileName;
            }
        }

        private void btnFolderToWatch_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = Resources.AutoQcConfigForm_btnFolderToWatch_Click_Directory_where_the_instrument_will_write_QC_files_;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    textFolderToWatchPath.Text = dialog.SelectedPath;
                }
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
            if (!cbPublishToPanorama.Checked)
            {
                return new PanoramaSettings();
            }

            return new PanoramaSettings(cbPublishToPanorama.Checked, textPanoramaUrl.Text, textPanoramaEmail.Text, textPanoramaPasswd.Text, textPanoramaFolder.Text);
        }

        private void cbPublishToPanorama_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxPanorama.Enabled = cbPublishToPanorama.Checked;
        }



        #endregion

        #region Skyline Settings

        private void InitSkylineTab()
        {
            radioButtonSkyline.Enabled = Installations.HasSkyline;
            radioButtonSkylineDaily.Enabled = Installations.HasSkylineDaily;
            if (!string.IsNullOrEmpty(Settings.Default.SkylineCustomCmdPath))
                textSkylineInstallationPath.Text = Path.GetDirectoryName(Settings.Default.SkylineCustomCmdPath);

            radioButtonSpecifySkylinePath.Checked = true;
            radioButtonSkylineDaily.Checked = radioButtonSkylineDaily.Enabled;
            radioButtonSkyline.Checked = radioButtonSkyline.Enabled;
        }

        private void SetInitialSkylineSettings(AutoQcConfig config)
        {
            radioButtonSkyline.Checked = config.UsesSkyline;
            radioButtonSkylineDaily.Checked = config.UsesSkylineDaily;
            radioButtonSpecifySkylinePath.Checked = config.UsesCustomSkylinePath;
            if (config.UsesCustomSkylinePath)
            {
                textSkylineInstallationPath.Text = Path.GetDirectoryName(config.SkylineSettings.CmdPath);
            }
        }

        private SkylineSettings GetSkylineSettingsFromUi()
        {
            var skylineType = SkylineType.Custom;
            if (radioButtonSkyline.Checked)
                skylineType = SkylineType.Skyline;
            if (radioButtonSkylineDaily.Checked)
                skylineType = SkylineType.SkylineDaily;
            return new SkylineSettings(skylineType, textSkylineInstallationPath.Text);
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var folderBrowserDlg = new FolderBrowserDialog())
            {
                folderBrowserDlg.Description =
                    string.Format(Resources.FindSkylineForm_btnBrowse_Click_Select_the__0__installation_directory,
                        Installations.Skyline);
                folderBrowserDlg.ShowNewFolderButton = false;
                folderBrowserDlg.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (folderBrowserDlg.ShowDialog() == DialogResult.OK)
                {
                    textSkylineInstallationPath.Text = folderBrowserDlg.SelectedPath;
                }
            }
        }

        private void radioButtonSpecifySkylinePath_CheckedChanged(object sender, EventArgs e)
        {
            textSkylineInstallationPath.Enabled = radioButtonSpecifySkylinePath.Checked;
            btnBrowse.Enabled = radioButtonSpecifySkylinePath.Checked;
        }


        #endregion


        #region Save config

        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            Save();
        }

        private AutoQcConfig GetConfigFromUi()
        {
            //AutoQcConfig config  = new AutoQcConfig();
            var name = textConfigName.Text;
            var mainSettings = GetMainSettingsFromUi();
            var panoramaSettings = GetPanoramaSettingsFromUi();
            var skylineSettings = GetSkylineSettingsFromUi();
            var created = _action == ConfigAction.Edit ? _initialCreated : DateTime.Now;
            return new AutoQcConfig(name, false, created, DateTime.Now, mainSettings, panoramaSettings, skylineSettings);
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


        #endregion



        

        private void ShowErrorDialog(string message)
        {
            _mainControl.DisplayError(Resources.AutoQcConfigForm_ShowErrorDialog_Configuration_Validation_Error + Environment.NewLine + 
                                      message);
        }

        private void btnOkConfig_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
