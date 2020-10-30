/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
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
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class ConfigManager
    {
        private readonly Dictionary<string, ConfigRunner> _configRunners;
        private readonly bool _runningUi;
        private readonly ISkylineBatchLogger _logger;
        private readonly IMainUiControl _uiControl;



        public ConfigManager(ISkylineBatchLogger logger, IMainUiControl uiControl = null)
        {
            _logger = logger;
            _uiControl = uiControl;
            _runningUi = uiControl != null;
            _configRunners = new Dictionary<string, ConfigRunner>();

            LoadConfigList();
        }


        public List<SkylineBatchConfig> ConfigList { get; private set; }

        public enum Operation
        {
            Add,
            Insert,
            Remove,
            Replace
        }

        private void LoadConfigList()
        {
            ConfigList = new List<SkylineBatchConfig>();
            foreach (var config in Settings.Default.ConfigList)
            {
                ConfigList.Add(config);
                var runner =new ConfigRunner(config, _logger, _uiControl);
                _configRunners.Add(config.Name, runner);
            }
        }

        private void SaveConfigList()
        {
            var updatedConfigs = new ConfigList();
            foreach (var config in ConfigList)
            {
                try
                {
                    config.Validate();
                    updatedConfigs.Add(config);
                }
                catch (ArgumentException e)
                {
                    if (_runningUi)
                    {
                        _uiControl.DisplayError(Resources.Save_configuration_error,
                            string.Format(Resources.Could_not_save_configuration_error_message, config.Name, e.Message));
                    }
                    else
                    {
                        throw e;
                    }
                    return;
                }

            }
            Settings.Default.ConfigList = updatedConfigs;
            Settings.Default.Save();
        }

        public ConfigRunner GetConfigRunnerAtIndex(int index)
        {
            return _configRunners[ConfigList[index].Name];
        }

        private void UpdateIsRunning(bool isRunning)
        {
            if (_runningUi)
                _uiControl.UpdateRunningButtons(isRunning);
        }


        #region Run

        public async Task RunAll(int startStep = 0)
        {
            if (ConfigsRunning())
            {
                if(_runningUi)
                    _uiControl.DisplayError(Resources.Run_error_title, Resources.Cannot_run_busy_configurations);
                return;
            }
            UpdateIsRunning(true);
            foreach (var runner in _configRunners.Values)
            {
                runner.ChangeStatus(ConfigRunner.RunnerStatus.Waiting);
            }
            var nextConfig = GetNextWaitingConfig();
            while (!string.IsNullOrEmpty(nextConfig))
            {
                await _configRunners[nextConfig].Run(startStep);
                nextConfig = GetNextWaitingConfig();
            }
            UpdateIsRunning(false);
        }

        private string GetNextWaitingConfig()
        {
            foreach (var config in ConfigList)
            {
                if (_configRunners[config.Name].IsWaiting())
                {
                    return config.Name;
                }
            }

            return null;
        }

        public bool ConfigsRunning()
        {
            foreach (var runner in _configRunners.Values)
            {
                if (runner.IsRunning())
                    return true;
            }
            return false;
        }

        public void CancelRunners()
         {
            foreach (var configRunner in _configRunners.Values)
            {
                configRunner.Cancel();
            }
            UpdateIsRunning(false);
        }

        #endregion


        #region Edit List

        public SkylineBatchConfig CreateConfiguration()
        {
            Program.LogInfo("Creating new configuration");
            var configCount = ConfigList.Count;
            return configCount > 0 ? ConfigList[configCount - 1].MakeChild() : SkylineBatchConfig.GetDefault();
        }

        public SkylineBatchConfig MakeNoNameCopy(SkylineBatchConfig config)
        {
            Program.LogInfo(string.Format("Copying configuration \"{0}\"", config.Name));
            var newConfig = config.Copy();
            newConfig.Name = null;
            return newConfig;
        }

        public void AddConfiguration(SkylineBatchConfig config)
        {
            InsertConfiguration(config, _configRunners.Count, Operation.Add);
        }
        public void InsertConfiguration(SkylineBatchConfig config, int index, Operation operation = Operation.Insert)
        {

            CheckIfExists(config, false, operation);
            config.Validate();
            Program.LogInfo(string.Format("Adding configuration \"{0}\"", config.Name));
            ConfigList.Insert(index, config);

            var newRunner = new ConfigRunner(config, _logger, _uiControl);
            _configRunners.Add(config.Name, newRunner);
        }

        public void Remove(SkylineBatchConfig config)
        {
            CheckIfExists(config, true, Operation.Remove);
            ConfigList.Remove(config);
            var configRunner = _configRunners[config.Name];
            configRunner.Cancel();
            _configRunners.Remove(config.Name);
        }

        public void MoveConfig(int currentIndex, int newIndex)
        {
            var movingConfig = ConfigList[currentIndex];
            ConfigList.Remove(movingConfig);
            ConfigList.Insert(newIndex, movingConfig);
        }


        public void ReplaceConfig(SkylineBatchConfig oldConfig, SkylineBatchConfig newConfig)
        {
            CheckIfExists(oldConfig, true, Operation.Replace);
            newConfig.Validate();
            if (!string.Equals(oldConfig.Name, newConfig.Name))
                CheckIfExists(newConfig, false, Operation.Replace);
            var index = IndexOf(oldConfig);
            Remove(oldConfig);
            InsertConfiguration(newConfig, index);
        }

        private void CheckIfExists(SkylineBatchConfig config, bool expectedValue, Operation typeOperation)
        {
            bool exists = _configRunners.Keys.Contains(config.Name);
            if (exists != expectedValue)
            {
                var message = expectedValue
                        ? string.Format(Resources.Operation_fail_config_nonexistant, typeOperation.ToString(), config.Name)
                        : string.Format(Resources.Operation_fail_config_exists, typeOperation.ToString(), config.Name);
                throw new ArgumentException(message);
            }
                
        }

        #endregion

        #region Import/Export

        public string Import(string filePath)
        {
            var readConfigs = new List<SkylineBatchConfig>();
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    using (var reader = XmlReader.Create(stream))
                    {
                        while (reader.Name != "skylinebatch_config")
                        {
                            if (reader.Name == "userSettings" && !reader.IsStartElement()) // there are no configurations in the file
                                break;
                            reader.Read();
                        }
                        while (reader.IsStartElement())
                        {
                            if (reader.Name == "skylinebatch_config")
                            {
                                var config = SkylineBatchConfig.Deserialize(reader);
                                readConfigs.Add(config);
                            }
                            reader.Read();
                            reader.Read();
                        }
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show(string.Format(Resources.No_configs_imported, filePath),
                    Resources.Import_configs_error_title,
                    MessageBoxButtons.OK);
                return null;
            }

            if (readConfigs.Count == 0)
            {
                MessageBox.Show(string.Format(Resources.No_configs_imported, filePath),
                    Resources.Import_configs_error_title,
                    MessageBoxButtons.OK);
            }

            var validationErrors = new List<string>();
            var duplicateConfigs = new List<string>();
            var numAdded = 0;
            foreach (SkylineBatchConfig config in readConfigs)
            {
                // Make sure that the configuration name is unique
                if (GetConfig(config.Name) != null)
                {
                    // If a configuration with the same name already exists, don't add it
                    duplicateConfigs.Add(config.Name);
                    continue;
                }

                try
                {
                    config.Validate();
                }
                catch (Exception ex)
                {
                    validationErrors.Add(string.Format(Resources.Configuration_error_message, config.Name, ex.Message));
                    continue;
                }
                
                AddConfiguration(config);
                numAdded++;
            }
            var message = new StringBuilder(Resources.Number_configs_imported);
            message.Append(numAdded).Append(Environment.NewLine);
            if (duplicateConfigs.Count > 0)
            {
                message.Append(Resources.Number_configs_duplicates)
                    .Append(Environment.NewLine);
                foreach (var name in duplicateConfigs)
                {
                    message.Append("\"").Append(name).Append("\"").Append(Environment.NewLine);
                }
            }
            if (validationErrors.Count > 0)
            {
                message.Append(Resources.Number_configs_not_valid)
                    .Append(Environment.NewLine);
                foreach (var error in validationErrors)
                {
                    message.Append(error).Append(Environment.NewLine);
                }
            }
            return message.ToString();
        }

        public void ExportAll(string filePath)
        {
            SaveConfigList();

            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            config.SaveAs(filePath);
        }
        #endregion
        public string GetDisplayStatus(SkylineBatchConfig config)
        {
            return _configRunners[config.Name].GetDisplayStatus();
        }

       

        public void CloseConfigs()
        {
            SaveConfigList();
            CancelRunners();
        }

        public SkylineBatchConfig GetConfig(string name)
        {
            if (!_configRunners.Keys.Contains(name))
                return null;
            return _configRunners[name].Config;
        }

        public bool HasConfigs()
        {
            return _configRunners.Keys.Count > 0;
        }

      


        private int IndexOf(SkylineBatchConfig config)
        {
            for (int i = 0; i < ConfigList.Count; i++)
            {
                if (ConfigList[i].Equals(config))
                    return i;
            }
            return -1;
        }

    }
}
