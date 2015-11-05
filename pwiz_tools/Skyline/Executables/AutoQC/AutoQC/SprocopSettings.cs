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
using AutoQC.Properties;

namespace AutoQC
{
    public class SprocopSettings
    {
        public bool RunSprocop { get; set; }
        public string RScriptPath { get; set; }
        public int Threshold { get; set; }
        public int MMA { get; set; }
        public bool IsHighRes { get; set; }

        public static SprocopSettings InitializeFromDefaults()
        {
            var settings = new SprocopSettings()
            {
                RunSprocop = Settings.Default.RunSprocop,
                RScriptPath = Settings.Default.RScriptPath,
                Threshold = 3
            };

            return settings;
        }

        public void Save()
        {
            Settings.Default.RunSprocop = RunSprocop;
            Settings.Default.RScriptPath = RScriptPath;    
        }
    }

    public class SprocopSettingsTab: SettingsTab
    {
        private SprocopSettings Settings { get; set; }

        public SprocopSettingsTab(IAppControl appControl, IAutoQCLogger logger)
            : base(appControl, logger)
        {
        }

        public String ReportFilePath
        {
            get { return Path.Combine(Directory.GetCurrentDirectory(), "SProCoP_report.skyr"); }
        }

        public string SProCoPrScript
        {
            get { return Path.Combine(Directory.GetCurrentDirectory(), "QCplotsRgui2.R"); }
        }

        public override void InitializeFromDefaultSettings()
        {
            Settings = SprocopSettings.InitializeFromDefaults();
            _appControl.SetUISprocopSettings(Settings);
            if (!Settings.RunSprocop)
            {
                _appControl.DisableSprocopSettings();
            }
        }

        public override bool IsSelected()
        {
            return false;
        }

        public override bool ValidateSettings()
        {
            var settingsFromUI = _appControl.GetUISprocopSettings();

            if (!settingsFromUI.RunSprocop)
            {
                Log("Will NOT run SProCoP.");
                Settings.RunSprocop = false;
                return true;
            }

            if (string.IsNullOrWhiteSpace(settingsFromUI.RScriptPath))
            {
                LogErrorOutput("Please specify path to Rscript.exe.");
                return false;
            }

            Settings = settingsFromUI;
            return true;
        }

        public override void SaveSettings()
        {
            Settings.Save();
        }

        public override void PrintSettings()
        {
            throw new NotImplementedException();
        }

        public override string SkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            var exportReportPath = Path.GetDirectoryName(ReportFilePath) + "\\" + "report.csv";
            var args =
                String.Format(
                    @""" --report-conflict-resolution=overwrite --report-add=""{0}"" --report-name=""{1}"" --report-file=""{2}""",
                    ReportFilePath, "SProCoP Input", exportReportPath);

            return args;

        }

        public override ProcessInfo RunBefore(ImportContext importContext)
        {
            return null;
        }

        public override ProcessInfo RunAfter(ImportContext importContext)
        {
            var thresholdCount = Settings.Threshold;
            if (thresholdCount > importContext.TotalImportCount)
            {
                // Don't do anything if we have imported fewer files than the number of required threshold files.
                return null;
            }

            var saveQcPath = Path.GetDirectoryName(importContext.GetCurrentFile()) + "\\QC.pdf";
            var args = String.Format(@"""{0}"" ""{1}"" {2} {3} 1 {4} ""{5}""",
                SProCoPrScript,
                ReportFilePath,
                thresholdCount,
                Settings.IsHighRes ? 1 : 0,
                Settings.MMA, 
                saveQcPath);
            return new ProcessInfo(Settings.RScriptPath, args);
        }
    }
}