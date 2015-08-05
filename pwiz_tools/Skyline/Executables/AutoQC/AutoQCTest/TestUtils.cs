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
    class TestLogger: IAutoQCLogger
    {
        private readonly StringBuilder log = new StringBuilder();

        public void Log(string message, object[] args)
        {
            AddToLog(message, args);
        }

        public void Log(string message, int blankLinesBefore, int blankLinesAfter, params object[] args)
        {
            AddToLog(message, args);
        }

        public void LogError(string message, object[] args)
        {
            AddToLog(message, args);
        }

        public void LogError(string message, int blankLinesBefore, int blankLinesAfter, params object[] args)
        {
            AddToLog(message, args);
        }

        public void LogException(Exception exception)
        {
            AddToLog(exception.Message);
        }

        public void LogOutput(string message, object[] args)
        {
            AddToLog(message, args);
        }

        public void LogOutput(string message, int blankLinesBefore, int blankLinesAfter, params object[] args)
        {
            LogOutput(message, args);
        }

        public void LogErrorOutput(string error, object[] args)
        {
            AddToLog(error, args);
        }

        public void LogErrorOutput(string error, int blankLinesBefore, int blankLinesAfter, params object[] args)
        {
            LogError(error, args);
        }

        private void AddToLog(string message, params object[] args)
        {
            log.Append(string.Format(message, args)).AppendLine();
            System.Diagnostics.Debug.WriteLine(message, args);
        }

        public String GetLog()
        {
            return log.ToString();
        }

        public void Clear()
        {
            log.Clear();
        }
    }

    class TestAppControl : IAppControl
    {
        private MainSettings _mainSettings = new MainSettings();
        private PanoramaSettings _panoramaSettings = new PanoramaSettings();

        public bool Waiting { get; set; }
        public bool Stopped { get; set; }

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

        public void SetUISprocopSettings(SprocopSettings sprocopSettings)
        {
            throw new NotImplementedException();
        }

        public SprocopSettings GetUISprocopSettings()
        {
            throw new NotImplementedException();
        }

        public void DisableSprocopSettings()
        {
            throw new NotImplementedException();
        }
    }

    class TestImportContext : ImportContext
    {
        public DateTime OldestFileDate;
        public TestImportContext(string resultsFile) : base(resultsFile)
        {
        }

        public TestImportContext(List<string> resultsFiles) : base(resultsFiles)
        {
        }

        public override DateTime GetOldestImportedFileDate(DateTime lastAcqDate)
        {
            return OldestFileDate;
        }
    }
}
