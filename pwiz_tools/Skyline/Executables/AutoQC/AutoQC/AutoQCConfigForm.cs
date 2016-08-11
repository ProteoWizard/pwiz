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
            textConfigName.Text = _config.Name;
            SetUIMainSettings(_config.MainSettings);
            SetUIPanoramaSettings(_config.PanoramaSettings);

            if (configRunner != null && configRunner.IsBusy())
            {
                lblConfigRunning.Show();
                btnSaveConfig.Enabled = false;
            }
            else
            {
                lblConfigRunning.Hide();
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
                    ShowErrorDialog("A configuration with this name already exists.");
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

        private static bool ValidateConfig(AutoQcConfig newConfig)
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

        private static void ShowErrorDialog(string message)
        {
            MessageBox.Show(message, "Configuration Validation Error",
                MessageBoxButtons.OK);
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
                textQCFilePattern.Text = mainSettings.QcFilePattern;
                textResultsTimeWindow.Text = mainSettings.ResultsWindow.ToString();
                textAquisitionTime.Text = mainSettings.AcquisitionTime.ToString();
                comboBoxInstrumentType.SelectedItem = mainSettings.InstrumentType;
                comboBoxInstrumentType.SelectedIndex = comboBoxInstrumentType.FindStringExact(mainSettings.InstrumentType);
            });
        }

        private MainSettings GetMainSettingsFromUI()
        {
            var mainSettings = new MainSettings();
            mainSettings.SkylineFilePath = textSkylinePath.Text;
            mainSettings.FolderToWatch = textFolderToWatchPath.Text;
            mainSettings.QcFilePattern = textQCFilePattern.Text;
            mainSettings.ResultsWindow = ValidateIntTextField(textResultsTimeWindow.Text, "Results Window");
            mainSettings.InstrumentType = comboBoxInstrumentType.SelectedItem.ToString();
            mainSettings.AcquisitionTime = ValidateIntTextField(textAquisitionTime.Text, "Acquisition Time");
            return mainSettings;
        }

        private int ValidateIntTextField(string textToParse, string fieldName)
        {
            int parsedInt;
            if (!Int32.TryParse(textToParse, out parsedInt))
            {
                throw new ArgumentException(string.Format("Invalid value for \"{0}\": {1}.", fieldName, textToParse));
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
            var panoramaSettings = new PanoramaSettings();
            panoramaSettings.PublishToPanorama = cbPublishToPanorama.Checked;
            if (panoramaSettings.PublishToPanorama)
            {
                panoramaSettings.PanoramaServerUrl = textPanoramaUrl.Text;
                panoramaSettings.PanoramaUserEmail = textPanoramaEmail.Text;
                panoramaSettings.PanoramaPassword = textPanoramaPasswd.Text;
                panoramaSettings.PanoramaFolder = textPanoramaFolder.Text;
            }

            return panoramaSettings;
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
            OpenFile("Skyline Files(*.sky)|*.sky|All Files (*.*)|*.*", textSkylinePath);
        }

        private void btnFolderToWatch_Click(object sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "Directory where the instrument will write QC files."
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textFolderToWatchPath.Text = dialog.SelectedPath;
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

        #endregion
        
    }
}
