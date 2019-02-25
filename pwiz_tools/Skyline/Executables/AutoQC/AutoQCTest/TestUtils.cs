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
using System.Collections.Generic;
using System.Text;
using AutoQC;

namespace AutoQCTest
{
    class TestLogger: IAutoQcLogger
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

        public void DisableUiLogging()
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
        private PanoramaSettings _panoramaSettings = new PanoramaSettings();

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

        public MainSettings GetUIMainSettings()
        {
            return _mainSettings;
        }

        public void SetUIPanoramaSettings(PanoramaSettings panoramaSettings)
        {
            _panoramaSettings = panoramaSettings;
        }

        public PanoramaSettings GetUIPanoramaSettings()
        {
            return _panoramaSettings;
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

        public void AddConfiguration(AutoQcConfig config)
        {
            throw new NotImplementedException();
        }

        public void UpdateConfiguration(AutoQcConfig oldConfig, AutoQcConfig newConfig)
        {
            throw new NotImplementedException();
        }

        public void UpdatePanoramaServerUrl(AutoQcConfig config)
        {
            throw new NotImplementedException();
        }

        public AutoQcConfig GetConfig(string name)
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

    class TestImportContext : ImportContext
    {
        public DateTime OldestFileDate;
        public TestImportContext(string resultsFile, DateTime oldestFileDate) : base(resultsFile)
        {
            OldestFileDate = oldestFileDate;
        }

        public TestImportContext(List<string> resultsFiles) : base(resultsFiles)
        {
        }

        public override DateTime GetOldestImportedFileDate(DateTime lastAcqDate)
        {
            return OldestFileDate;
        }
    }

    class TestConfigRunner : IConfigRunner
    {
        private ConfigRunner.RunnerStatus _runnerStatus;
        public void ChangeStatus(ConfigRunner.RunnerStatus status)
        {
            _runnerStatus = status;
        }

        public bool IsRunning()
        {
            throw new NotImplementedException();
        }

        public bool IsStopped()
        {
            throw new NotImplementedException();
        }

        public bool IsDisconnected()
        {
            return false;
        }
    }
}
