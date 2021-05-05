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
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkylineBatch;
using SharedBatch;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SkylineBatchTest
{
    public class TestUtils
    {
        public static string GetTestFilePath(string fileName)
        {
            var currentPath = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(currentPath, "SkylineCmd.exe")))
                currentPath = Path.Combine(currentPath, "..", "..", "..", "Executables", "SkylineBatch", "SkylineBatchTest");
            else
                currentPath = Path.Combine(currentPath, "..", "..");

            var batchTestPath = Path.Combine(currentPath, "Test");
            if (!Directory.Exists(batchTestPath))
                throw new DirectoryNotFoundException("Unable to find test data directory at: " + batchTestPath);
            return Path.Combine(batchTestPath, fileName);
        }

        public static SkylineBatchConfig GetChangedConfig(SkylineBatchConfig baseConfig, Dictionary<string, object> changedVariables)
        {
            var name = baseConfig.Name;
            var enabled = baseConfig.Enabled;
            var modified = baseConfig.Modified;
            foreach (var variable in changedVariables.Keys)
            {
                if ("Name".Equals(variable))
                    name = (string) changedVariables[variable];
                else if ("Enabled".Equals(variable))
                    enabled = (bool)changedVariables[variable];
                else if ("Modified".Equals(variable))
                    modified = (DateTime)changedVariables[variable];
            }
            
            return new SkylineBatchConfig(name, enabled, modified, GetChangedMainSettings(baseConfig.MainSettings, changedVariables), 
                GetChangedFileSettings(baseConfig.FileSettings, changedVariables),
                GetChangedRefineSettings(baseConfig.RefineSettings, changedVariables), GetChangedReportSettings(baseConfig.ReportSettings, changedVariables),
                GetChangedSkylineSettings(baseConfig.SkylineSettings, changedVariables));
        }

        public static MainSettings GetChangedMainSettings(MainSettings baseSettings, Dictionary<string, object> changedVariables)
        {
            var template = baseSettings.TemplateFilePath;
            var analysisFolder = baseSettings.AnalysisFolderPath;
            var dataFolder = baseSettings.DataFolderPath;
            var annotationsFile = baseSettings.AnnotationsFilePath;
            var namingPattern = baseSettings.ReplicateNamingPattern;
            var dependentConfig = baseSettings.DependentConfigName;

            foreach (var variable in changedVariables.Keys)
            {
                if ("TemplateFilePath".Equals(variable))
                    template = (string)changedVariables[variable];
                else if ("AnalysisFolderPath".Equals(variable))
                    analysisFolder = (string)changedVariables[variable];
                else if ("DataFolderPath".Equals(variable))
                    dataFolder = (string)changedVariables[variable];
                else if ("AnnotationsFilePath".Equals(variable))
                    annotationsFile = (string)changedVariables[variable];
                else if ("ReplicateNamingPattern".Equals(variable))
                    namingPattern = (string)changedVariables[variable];
                else if ("DependentConfigName".Equals(variable))
                    dependentConfig = (string)changedVariables[variable];
            }
            return new MainSettings(template, analysisFolder, dataFolder, null,  annotationsFile, namingPattern, dependentConfig);
        }

        public static FileSettings GetChangedFileSettings(FileSettings baseSettings,
            Dictionary<string, object> changedVariables)
        {
            var msOneResolvingPower = baseSettings.MsOneResolvingPower;
            var msMsResolvingPower = baseSettings.MsMsResolvingPower;
            var retentionTime = baseSettings.RetentionTime;
            var addDecoys = baseSettings.AddDecoys;
            var shuffleDecoys = baseSettings.ShuffleDecoys;
            var trainMProphet = baseSettings.TrainMProphet;

            foreach (var variable in changedVariables.Keys)
            {
                if ("MsOneResolvingPower".Equals(variable))
                    msOneResolvingPower = (int?) changedVariables[variable];
                else if ("MsMsResolvingPower".Equals(variable))
                    msMsResolvingPower = (int?) changedVariables[variable];
                else if ("RetentionTime".Equals(variable))
                    retentionTime = (int?) changedVariables[variable];
                else if ("AddDecoys".Equals(variable))
                    addDecoys = (bool) changedVariables[variable];
                else if ("ShuffleDecoys".Equals(variable))
                    shuffleDecoys = (bool) changedVariables[variable];
                else if ("TrainMProphet".Equals(variable))
                    trainMProphet = (bool) changedVariables[variable];
            }
            return new FileSettings(msOneResolvingPower, msMsResolvingPower, retentionTime, addDecoys, shuffleDecoys,
                trainMProphet);
        }

        public static ReportSettings GetChangedReportSettings(ReportSettings baseSettings,
            Dictionary<string, object> changedVariables)
        {
            var reports = baseSettings.Reports.ToList();

            foreach (var variable in changedVariables.Keys)
            {
                if ("Reports".Equals(variable))
                    reports = (List<ReportInfo>)changedVariables[variable];
            }
            return new ReportSettings(reports);
        }

        public static RefineSettings GetChangedRefineSettings(RefineSettings baseSettings,
            Dictionary<string, object> changedVariables)
        {
            var commandValues = baseSettings.CommandValuesCopy;
            var removeDecoys = baseSettings.RemoveDecoys;
            var removeResults = baseSettings.RemoveResults;
            var outputFilePath = baseSettings.OutputFilePath;

            foreach (var variable in changedVariables.Keys)
            {
                if ("CommandValues".Equals(variable))
                    commandValues = (RefineInputObject)changedVariables[variable];
                else if ("RemoveDecoys".Equals(variable))
                    removeDecoys = (bool)changedVariables[variable];
                else if ("RemoveResults".Equals(variable))
                    removeResults = (bool)changedVariables[variable];
                else if ("OutputFilePath".Equals(variable))
                    outputFilePath = (string)changedVariables[variable];
            }
            return new RefineSettings(commandValues, removeDecoys, removeResults, outputFilePath);
        }

        public static SkylineSettings GetChangedSkylineSettings(SkylineSettings baseSettings,
            Dictionary<string, object> changedVariables)
        {
            var type = baseSettings.Type;
            var cmdPath = baseSettings.CmdPath;

            foreach (var variable in changedVariables.Keys)
            {
                if ("Type".Equals(variable))
                    type = (SkylineType)changedVariables[variable];
                else if ("CmdPath".Equals(variable))
                    cmdPath = (string)changedVariables[variable];
            }
            return new SkylineSettings(type, cmdPath);
        }

        public static MainSettings GetTestMainSettings()
        {
            return new MainSettings(GetTestFilePath("emptyTemplate.sky"), GetTestFilePath("analysis"), GetTestFilePath("emptyData"), null, string.Empty,  string.Empty, string.Empty);
        }

        public static FileSettings GetTestFileSettings()
        {
            return new FileSettings(null, null, null, false, false, true);
        }

        public static RefineSettings GetTestRefineSettings()
        {
            return new RefineSettings(new RefineInputObject(), false, false, string.Empty);
        }

        public static ReportSettings GetTestReportSettings()
        {
            var reportList = new List<ReportInfo>{GetTestReportInfo() };
            return new ReportSettings(reportList);
        }

        public static ReportInfo GetTestReportInfo()
        {
            return new ReportInfo("UniqueReport", false, GetTestFilePath("UniqueReport.skyr"),
                new List<Tuple<string, string>> {new Tuple<string, string>(GetTestFilePath("testScript.r"), "4.0.3")}, false);
        }

        public static SkylineSettings GetTestSkylineSettings()
        {
            return new SkylineSettings(SkylineType.Custom, GetSkylineDir());
        }

        public static SkylineBatchConfig GetTestConfig(string name = "name")
        {
            return new SkylineBatchConfig(name, true, DateTime.MinValue, GetTestMainSettings(), GetTestFileSettings(), 
                GetTestRefineSettings(), GetTestReportSettings(), GetTestSkylineSettings());
        }

        public static SkylineBatchConfig GetTestDependentConfig(string name, SkylineBatchConfig baseConfig)
        {
            if (string.IsNullOrEmpty(baseConfig.RefineSettings.OutputFilePath))
                throw new Exception("Config does not have refine output path and will not create dependency");
            var newMainSettings = GetTestMainSettings()
                .UpdateDependent(baseConfig.Name, baseConfig.RefineSettings.OutputFilePath);
            var populatedRefineSettings = new RefineSettings(new RefineInputObject(), true, true, GetTestFilePath("test.sky"));

            return new SkylineBatchConfig(name, true, DateTime.MinValue, newMainSettings, GetTestFileSettings(),
                populatedRefineSettings, GetTestReportSettings(), GetTestSkylineSettings());
        }

        public static SkylineBatchConfig GetFullyPopulatedConfig(string name = "TestConfig")
        {
            var main = new MainSettings(GetTestFilePath("emptyTemplate.sky"), GetTestFilePath("analysis"),
                GetTestFilePath("emptyData"), null,  GetTestFilePath("fakeAnnotations.csv"), "testNamingPattern", string.Empty);
            var file = FileSettings.FromUi("5", "4", "3", true, true, true);
            var refine = new RefineSettings(new RefineInputObject() 
                {
                    cv_remove_above_cutoff = 20,
                    cv_global_normalize = RefineInputObject.CvGlobalNormalizeValues.equalize_medians,
                    qvalue_cutoff = 0.01,
                    cv_transitions_count = 2
                },  false, false, GetTestFilePath("RefineOutput.sky"));

            var reportList = new List<ReportInfo>();
            var script = new List<Tuple<string, string>>()
                {new Tuple<string, string>(GetTestFilePath("testScript.R"), "4.0.2")};
            reportList.Add(new ReportInfo("Unique Report", false, GetTestFilePath("uniqueReport.skyr"), script, false));
            reportList.Add(new ReportInfo("Another Unique Report", true, GetTestFilePath("uniqueReport.skyr"), script, true));
            var reports = new ReportSettings(reportList);
            var skyline = GetTestSkylineSettings();
            return new SkylineBatchConfig(name, true, DateTime.Now, main, file, refine, reports, skyline);
        }

        public static ConfigRunner GetTestConfigRunner(string configName = "name")
        {
            return new ConfigRunner(GetTestConfig(configName), GetTestLogger());
        }

        public static List<IConfig> ConfigListFromNames(List<string> names)
        {
            var configList = new List<IConfig>();
            foreach (var name in names)
            {
                configList.Add(GetTestConfig(name));
            }
            return configList;
        }

        public static SkylineBatchConfigManager GetTestConfigManager()
        {
            var testConfigManager = new SkylineBatchConfigManager(GetTestLogger());
            testConfigManager.UserAddConfig(GetTestConfig("one"));
            testConfigManager.UserAddConfig(GetTestConfig("two"));
            testConfigManager.UserAddConfig(GetTestConfig("three"));
            return testConfigManager;
        }

        public static string GetSkylineDir()
        {
            return GetProjectDirectory("bin\\x64\\Release");
        }

        public static string GetProjectDirectory(string relativePath)
        {
            for (String directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                directory != null && directory.Length > 10;
                directory = Path.GetDirectoryName(directory))
            {
                if (File.Exists(Path.Combine(directory, "Skyline.sln")))
                    return Path.Combine(directory, relativePath);
            }

            return null;
        }

        public static Logger GetTestLogger(string logFolder = "")
        {
            logFolder = string.IsNullOrEmpty(logFolder) ? GetTestFilePath("OldLogs") : logFolder;
            var logName = "TestLog" + DateTime.Now.ToString("_HHmmssfff") + ".log";
            return new Logger(Path.Combine(logFolder, logName), logName);
        }

        public static void InitializeRInstallation()
        {
            Assert.IsTrue(RInstallations.FindRDirectory());
        }

        public static List<string> GetAllLogFiles(string directory = null)
        {
            directory = directory == null ? GetTestFilePath("OldLogs\\TestTinyLog") : directory;
            var files = Directory.GetFiles(directory);
            var logFiles = new List<string>();
            foreach (var fullName in files)
            {
                var file = Path.GetFileName(fullName);
                if (file.EndsWith(".log"))
                    logFiles.Add(fullName);
            }
            return logFiles;
        }

        public delegate bool ConditionDelegate();

        public static void WaitForCondition(ConditionDelegate condition, TimeSpan timeout, int timestep, string errorMessage)
        {
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < timeout)
            {
                if (condition()) return;
                Thread.Sleep(timestep);
            }
            throw new Exception(errorMessage);
        }
    }
}
