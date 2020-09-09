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

namespace AutoQC
{
    public partial class AutoQcConfigForm : Form
    {
        private readonly IMainUiControl _mainControl;
        private readonly AutoQcConfig _config;

        public AutoQcConfigForm(IMainUiControl mainControl) : this(AutoQcConfig.GetDefault(), null, mainControl)
        {
        }

        public AutoQcConfigForm(AutoQcConfig config, ConfigRunner configRunner, IMainUiControl mainControl)
        {
            _mainControl = mainControl;
            _config = config;
            InitializeComponent();

            // Initialize file filter combobox
            var filterOptions = new object[]
            {
                AllFileFilter.NAME, 
                StartsWithFilter.NAME, 
                EndsWithFilter.NAME, 
                ContainsFilter.NAME,
                RegexFilter.NAME
            };
            comboBoxFileFilter.Items.AddRange(filterOptions);

            textConfigName.Text = _config.Name;
            SetUIMainSettings(_config.MainSettings);
            SetUIPanoramaSettings(_config.PanoramaSettings);

            if (configRunner != null && configRunner.IsBusy())
            {
                lblConfigRunning.Show();
                btnSaveConfig.Hide();
                btnCancelConfig.Hide();
                btnOkConfig.Show();
            }
            else
            {
                lblConfigRunning.Hide();
                btnSaveConfig.Show();
                btnCancelConfig.Show();
                btnOkConfig.Hide();
            } 
        }

        private void Save()
        {
            AutoQcConfig newConfig;
            try
            {
                newConfig = GetConfigFromUi();
            }
            catch (ArgumentException e)
            {
                ShowErrorDialog(e.Message);
                return;
            }

            if (!ValidateConfigName(newConfig) || !ValidateConfig(newConfig))
                return;

            if (string.IsNullOrEmpty(_config.Name))
            {
                // If the original configuration that we started with does not have a name,
                // it means this is a brand new configuration
                newConfig.Created = DateTime.Now;
                newConfig.Modified = DateTime.Now;

                _mainControl.AddConfiguration(newConfig);     
            }

            else if (!newConfig.Equals(_config))
            {
                // If the original configuration has a name it means the user is editing an existing configuration
                // and some changes have been made.
                newConfig.Created = _config.Created;
                newConfig.Modified = DateTime.Now;

                _mainControl.UpdateConfiguration(_config, newConfig);
            }

            Close();      
        }

        private bool ValidateConfigName(AutoQcConfig newConfig)
        {
            if (string.IsNullOrEmpty(_config.Name) || !_config.Name.Equals(newConfig.Name))
            {
                // Make sure that the configuration name is unique.
                if (!IsUniqueConfigName(newConfig))
                {
                    ShowErrorDialog(Resources.AutoQcConfigForm_ValidateConfigName_A_configuration_with_this_name_already_exists_);
                    return false;    
                }
            }
            return true;
        }

        private bool IsUniqueConfigName(AutoQcConfig newConfig)
        {
            // Make sure that the configuration name is unique
            var savedConfig = _mainControl.GetConfig(newConfig.Name);
            if (savedConfig != null && !ReferenceEquals(newConfig, savedConfig))
            {
                return false;
            }
            return true;
        }

        private bool ValidateConfig(AutoQcConfig newConfig)
        {
            try
            {
                newConfig.Validate();
            }
            catch (ArgumentException e)
            {
                ShowErrorDialog(e.Message);
                return false;   
            }

            return true;
        }

        private void ShowErrorDialog(string message)
        {
            _mainControl.DisplayError(Resources.AutoQcConfigForm_ShowErrorDialog_Configuration_Validation_Error, message);
        }

        private AutoQcConfig GetConfigFromUi()
        {
            AutoQcConfig config  = new AutoQcConfig();
            config.Name = textConfigName.Text;
            config.MainSettings = GetMainSettingsFromUI();
            config.PanoramaSettings = GetPanoramaSettingsFromUI();
            config.User = config.PanoramaSettings.PanoramaUserEmail;
            return config;
        }

        private void SetUIMainSettings(MainSettings mainSettings)
        {
            RunUI(() =>
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
            });
        }

        private MainSettings GetMainSettingsFromUI()
        {
            var skylineFilePath = textSkylinePath.Text;
            var folderToWatch = textFolderToWatchPath.Text;
            var includeSubfolders = includeSubfoldersCb.Checked;
            var qcFileFilter = FileFilter.GetFileFilter(comboBoxFileFilter.SelectedItem.ToString(),
                textQCFilePattern.Text);
            var removeResults = checkBoxRemoveResults.Checked;
            var resultsWindow = ValidateIntTextField(textResultsTimeWindow.Text,
                Resources.AutoQcConfigForm_GetMainSettingsFromUI_Results_Window);
            var instrumentType = comboBoxInstrumentType.SelectedItem.ToString();
            var acquisitionTime = ValidateIntTextField(textAquisitionTime.Text,
                Resources.AutoQcConfigForm_GetMainSettingsFromUI_Acquisition_Time);
            return new MainSettings(skylineFilePath, folderToWatch, includeSubfolders, qcFileFilter, removeResults, resultsWindow, instrumentType, acquisitionTime);
        }

        private int ValidateIntTextField(string textToParse, string fieldName)
        {
            int parsedInt;
            if (!Int32.TryParse(textToParse, out parsedInt))
            {
                throw new ArgumentException(string.Format(
                    Resources.AutoQcConfigForm_ValidateIntTextField_Invalid_value_for___0_____1__, fieldName,
                    textToParse));
            }
            return parsedInt;
        }

        private void SetUIPanoramaSettings(PanoramaSettings panoramaSettings)
        {
            RunUI(() =>
            {
                textPanoramaUrl.Text = panoramaSettings.PanoramaServerUrl;
                textPanoramaEmail.Text = panoramaSettings.PanoramaUserEmail;
                textPanoramaPasswd.Text = panoramaSettings.PanoramaPassword;
                textPanoramaFolder.Text = panoramaSettings.PanoramaFolder;
                cbPublishToPanorama.Checked = panoramaSettings.PublishToPanorama;
                groupBoxPanorama.Enabled = panoramaSettings.PublishToPanorama;
            });
        }

        private PanoramaSettings GetPanoramaSettingsFromUI()
        {
            if (cbPublishToPanorama.Checked)
            {
                return new PanoramaSettings(cbPublishToPanorama.Checked, textPanoramaUrl.Text, textPanoramaFolder.Text, textPanoramaEmail.Text, textPanoramaPasswd.Text);
            }

            return new PanoramaSettings();
        }

        public void RunUI(Action action)
        {
            if (InvokeRequired)
            {
                try
                {
                    Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                }
            }
            else
            {
                action();
            }
        }

        private void OpenFile(string filter, TextBox textbox)
        {
            var dialog = new OpenFileDialog { Filter = filter };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textbox.Text = dialog.FileName;
            }
        }

        #region [UI event handlers]
       
        private void btnSkylineFilePath_Click(object sender, EventArgs e)
        {
            OpenFile(Resources.AutoQcConfigForm_btnSkylineFilePath_Click_Skyline_Files___sky____sky_All_Files__________, textSkylinePath);
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

        private void cbPublishToPanorama_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxPanorama.Enabled = cbPublishToPanorama.Checked;
        }

        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void btnOkConfig_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void comboBoxFileFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedItem = comboBoxFileFilter.SelectedItem;
            if (selectedItem.Equals(AllFileFilter.NAME))
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

        #endregion

        private void checkBoxRemoveResults_CheckedChanged(object sender, EventArgs e)
        {
            textResultsTimeWindow.Enabled = checkBoxRemoveResults.Checked;
            labelAccumulationTimeWindow.Enabled = checkBoxRemoveResults.Checked;
            labelDays.Enabled = checkBoxRemoveResults.Checked;
        }
    }
}
