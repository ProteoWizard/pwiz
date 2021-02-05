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
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkylineBatch;
using SkylineBatch.Properties;
using SharedAutoQcBatch;

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

        public static MainSettings GetTestMainSettings()
        {
            return new MainSettings(GetTestFilePath("emptyTemplate.sky"), GetTestFilePath("analysis"), GetTestFilePath("emptyData"), string.Empty);
        }

        public static FileSettings GetTestFileSettings()
        {
            return new FileSettings(string.Empty, string.Empty, string.Empty, false, false, true);
        }

        public static ReportSettings GetTestReportSettings()
        {
            var reportList = new List<ReportInfo>{GetTestReportInfo() };
            return new ReportSettings(reportList);
        }

        public static ReportInfo GetTestReportInfo()
        {
            return new ReportInfo("UniqueReport", GetTestFilePath("UniqueReport.skyr"),
                new List<Tuple<string, string>> {new Tuple<string, string>(GetTestFilePath("testScript.r"), "4.0.3")});
        }

        public static SkylineSettings GetTestSkylineSettings()
        {
            return new SkylineSettings(SkylineType.Custom, GetSkylineDir());
        }

        public static SkylineBatchConfig GetTestConfig(string name = "name")
        {
            return new SkylineBatchConfig(name, true, DateTime.MinValue, GetTestMainSettings(), GetTestFileSettings(), 
                GetTestReportSettings(), GetTestSkylineSettings());
        }

        public static ConfigRunner GetTestConfigRunner(string configName = "name")
        {
            return new ConfigRunner(GetTestConfig(configName), GetTestLogger());
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
            var testConfigManager = new ConfigManager(GetTestLogger());
            while (testConfigManager.HasConfigs())
            {
                testConfigManager.SelectConfig(0);
                testConfigManager.RemoveSelected();
            }
            testConfigManager.AddConfiguration(GetTestConfig("one"));
            testConfigManager.AddConfiguration(GetTestConfig("two"));
            testConfigManager.AddConfiguration(GetTestConfig("three"));
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

        public static SkylineBatchLogger GetTestLogger(string logFolder = "")
        {
            SkylineBatchLogger.LOG_FOLDER = string.IsNullOrEmpty(logFolder) ? GetTestFilePath("OldLogs") : logFolder;
            return new SkylineBatchLogger("TestLog" + DateTime.Now.ToString("_HHmmssfff") + ".log");
        }

        public static void InitializeRInstallation()
        {
            Assert.IsTrue(RInstallations.FindRDirectory());
        }

        public static void ClearSavedConfigurations()
        {
            var logger = GetTestLogger();
            var testConfigManager = new ConfigManager(logger);
            while (testConfigManager.HasConfigs())
            {
                testConfigManager.SelectConfig(0);
                testConfigManager.RemoveSelected();
            }
            testConfigManager.Close();
            logger.Delete();
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
        
    }
}
