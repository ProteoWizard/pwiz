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
using System.IO;
using System.Text;
using SkylineBatch;

namespace SkylineBatchTest
{
    class TestLogger: ISkylineBatchLogger
    {
        private readonly StringBuilder log = new StringBuilder();
        private readonly  StringBuilder _programLog = new StringBuilder();





        public void Log(string message, object[] args)
        {
            AddToLog(message, args);
        }

        public void LogError(string message, object[] args)
        {
            AddToLog(message, args);
        }

        public void LogProgramError(string message, params object[] args)
        {
            AddToProgramLog(message, args);
        }

        public void LogException(Exception exception, string message, params object[] args)
        {
            AddToLog(message, args);
        }

        public string GetFile()
        {
            throw new NotImplementedException();
        }

        public string GetFileName()
        {
            throw new NotImplementedException();
        }

        public void DisableUiLogging()
        {
            throw new NotImplementedException();
        }

        public SkylineBatchLogger Archive()
        {
            throw new NotImplementedException();
        }

        public void LogToUi(IMainUiControl mainUi)
        {
            throw new NotImplementedException();
        }

        public void DisplayLog()
        {
            throw new NotImplementedException();
        }

        private void AddToLog(string message, params object[] args)
        {
            log.Append(string.Format(message, args)).AppendLine();
            System.Diagnostics.Debug.WriteLine(message, args);
        }

        private void AddToProgramLog(string message, params object[] args)
        {
            _programLog.Append(string.Format(message, args)).AppendLine();
        }

        public string GetLog()
        {
            return log.ToString();
        }

        public void Clear()
        {
            log.Clear();
        }
    }

    class TestAppControl : IMainUiControl
    {
        private MainSettings _mainSettings = new MainSettings();

        public bool Waiting { get; set; }
        public bool Stopped { get; set; }

        private ConfigRunner.RunnerStatus _runnerStatus;

        public void SetWaiting()
        {
            Waiting = true;
        }

        public void SetStopped()
        {
            Stopped = true;
        }

        public void SetUIMainSettings(MainSettings mainSettings)
        {
            _mainSettings = mainSettings;
        }

        public void UpdateUiConfigurations()
        {
        }

        public void UpdateUiLogFiles()
        {
        }

        public void ClearLog()
        {
        }

        public void UpdateRunningButtons(bool isRunning)
        {
        }

        public MainSettings GetUIMainSettings()
        {
            return _mainSettings;
        }

        public void DisablePanoramaSettings()
        {
            throw new NotImplementedException();
        }

        #region Implementation of IMainUiControl

        public void ChangeConfigUiStatus(ConfigRunner configRunner)
        {
            _runnerStatus = configRunner.GetStatus();
        }

        public void AddConfiguration(SkylineBatchConfig config)
        {
            throw new NotImplementedException();
        }

        public void UpdateConfiguration(SkylineBatchConfig oldConfig, SkylineBatchConfig newConfig)
        {
            throw new NotImplementedException();
        }

        public void UpdatePanoramaServerUrl(SkylineBatchConfig config)
        {
            throw new NotImplementedException();
        }

        public SkylineBatchConfig GetConfig(string name)
        {
            throw new NotImplementedException();
        }

        public void LogToUi(string text, bool scrollToEnd = true, bool trim = false)
        {
            throw new NotImplementedException();
        }

        public void LogErrorToUi(string text, bool scrollToEnd = true, bool trim = false)
        {
            throw new NotImplementedException();
        }

        public void LogLinesToUi(List<string> lines)
        {
            throw new NotImplementedException();
        }

        public void LogErrorLinesToUi(List<string> lines)
        {
            throw new NotImplementedException();
        }

        public void DisplayError(string title, string message)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class TestUtils
    {
        public static string GetTestFilePath(string fileName)
        {
            var currentPath = Directory.GetCurrentDirectory();
            var batchTestPath = Path.GetDirectoryName(Path.GetDirectoryName(currentPath));
            return batchTestPath + "\\Test\\" + fileName;
        }

        public static MainSettings GetTestMainSettings()
        {
            return new MainSettings()
            {
                AnalysisFolderPath = GetTestFilePath("analysis"),
                DataFolderPath = GetTestFilePath("emptyData"),
                TemplateFilePath = GetTestFilePath("emptyTemplate.sky"),
            };
        }

        public static ReportSettings GetTestReportSettings()
        {
            var testReports = new ReportSettings();
            testReports.Add(new ReportInfo("UniqueReport", GetTestFilePath("UniqueReport.skyr"), new List<string> { GetTestFilePath("testScript.r") }));
            return testReports;
        }

        public static SkylineBatchConfig GetTestConfig(string name = "name")
        {
            return new SkylineBatchConfig()
            {
                Name = name,
                Created = DateTime.MinValue,
                Modified = DateTime.MinValue,
                MainSettings = GetTestMainSettings(),
                ReportSettings = GetTestReportSettings()
            };
        }

        public static ConfigRunner GetTestConfigRunner(string configName = "name")
        {
            return new ConfigRunner(GetTestConfig(configName), new SkylineBatchLogger(GetTestFilePath("TestLog.log")));
        }

        public static List<SkylineBatchConfig> ConfigListFromNames(List<string> names)
        {
            var configList = new List<SkylineBatchConfig>();
            foreach (var name in names)
            {
                configList.Add(GetTestConfig(name));
            }
            return configList;
        }

        public static ConfigManager GetTestConfigManager()
        {
            var testConfigManager = new ConfigManager(new SkylineBatchLogger(GetTestFilePath("TestLog.log")));
            while (testConfigManager.HasConfigs())
            {
                testConfigManager.Remove(testConfigManager.ConfigList[0]);
            }
            testConfigManager.AddConfiguration(GetTestConfig("one"));
            testConfigManager.AddConfiguration(GetTestConfig("two"));
            testConfigManager.AddConfiguration(GetTestConfig("three"));
            return testConfigManager;
        }

        public static void ClearSavedConfigurations()
        {
            var testConfigManager = new ConfigManager(new SkylineBatchLogger(TestUtils.GetTestFilePath("TestLog.log")));
            while (testConfigManager.HasConfigs())
            {
                testConfigManager.Remove(testConfigManager.ConfigList[0]);
            }
            testConfigManager.Close();
        }
    }
}
