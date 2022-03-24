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
using System.IO;
using System.Threading;
using AutoQC;
using SharedBatch;
using SharedBatch.Properties;
using SharedBatchTest;

namespace AutoQCTest
{
    public class TestUtils
    {
        public static string GetTestFilePath(string fileName)
        {
            return Path.Combine(GetTestDataPath(), fileName);
        }

        private static string GetTestDataPath()
        {
            return Path.Combine(GetAutoQcPath(), "TestData");
        }

        private static string GetAutoQcPath()
        {
            // ExtensionTestContext looks for paths relative to Skyline.sln.
            return ExtensionTestContext.GetProjectDirectory(@"Executables\AutoQC");
        }

        public static MainSettings GetTestMainSettings() => GetTestMainSettings(string.Empty, string.Empty, string.Empty);

        public static MainSettings GetTestMainSettings(string changedVariable, string value) => GetTestMainSettings(string.Empty, changedVariable, value);

        public static MainSettings GetTestMainSettings(string skyFilePath, string changedVariable, string value)
        {
            var skylineFilePath = !string.Empty.Equals(skyFilePath) ? skyFilePath : GetTestFilePath("emptyTemplate.sky");
            var folderToWatch = changedVariable.Equals("folderToWatch")? value : GetTestDataPath();

            var includeSubfolders = changedVariable.Equals("includeSubfolders") && value.Equals("true");
            var fileFilter = MainSettings.GetDefaultQcFileFilter();
            var resultsWindow = MainSettings.GetDefaultResultsWindow();
            var removeResults = MainSettings.GetDefaultRemoveResults();
            var acquisitionTime = "0"; // Set to 0 so that AutoQC does not wait to import results files
            var instrumentType = MainSettings.GetDefaultInstrumentType();
            
            
            return new MainSettings(skylineFilePath, folderToWatch, includeSubfolders, fileFilter, removeResults, resultsWindow, instrumentType, acquisitionTime);
        }

        public static PanoramaSettings GetTestPanoramaSettings(bool publishToPanorama = true)
        {
            var panoramaServerUrl = publishToPanorama ? "https://panoramaweb.org/" : "";
            var panoramaUserEmail = publishToPanorama ? "skyline_tester@proteinms.net" : "";
            var panoramaPassword = publishToPanorama ? "lclcmsms" : "";
            var panoramaProject = publishToPanorama ? "/SkylineTest" : "";

            return new PanoramaSettings(publishToPanorama, panoramaServerUrl, panoramaUserEmail, panoramaPassword, panoramaProject);
        }

        public static PanoramaSettings GetNoPublishPanoramaSettings()
        {
            return new PanoramaSettings(false, null, null, null, null);
        }

        public static SkylineSettings GetTestSkylineSettings()
        {
            if (SkylineInstallations.FindSkyline())
            {
                if (SkylineInstallations.HasSkyline)
                    return new SkylineSettings(SkylineType.Skyline, null);
                if (SkylineInstallations.HasSkylineDaily)
                    return new SkylineSettings(SkylineType.SkylineDaily, null);
            }

            return null;
        }

        public static AutoQcConfig GetTestConfig(string name)
        {
            return new AutoQcConfig(name, false, DateTime.MinValue, DateTime.MinValue, GetTestMainSettings(), GetTestPanoramaSettings(), GetTestSkylineSettings());
        }

        public static ConfigRunner GetTestConfigRunner(string configName = "Config")
        {
            var testConfig = GetTestConfig(configName);
            return new ConfigRunner(testConfig, GetTestLogger(testConfig));
        }

        public static Logger GetTestLogger(AutoQcConfig config)
        {
            var logFile = Path.Combine(config.GetConfigDir(), "AutoQC.log");
            return new Logger(logFile, config.Name, false);
        }

        public static List<AutoQcConfig> ConfigListFromNames(string[] names)
        {
            var configList = new List<AutoQcConfig>();
            foreach (var name in names)
            {
                configList.Add(GetTestConfig(name));
            }
            return configList;
        }

        public static AutoQcConfigManager GetTestConfigManager(List<AutoQcConfig> configs = null)
        {
            var testConfigManager = new AutoQcConfigManager();
          
            if (configs == null)
            {
                configs = new List<AutoQcConfig>
                {
                    GetTestConfig("one"),
                    GetTestConfig("two"),
                    GetTestConfig("three")
                };
            }

            foreach (var config in configs)
                testConfigManager.SetState(testConfigManager.State,
                    testConfigManager.State.UserAddConfig(config, null));
            
            return testConfigManager;
        }

        public static void WaitForCondition(Func<bool> condition, TimeSpan timeout, int timestep, string errorMessage)
        {
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < timeout)
            {
                if (condition()) return;
                Thread.Sleep(timestep);
            }
            throw new Exception(errorMessage);
        }

        public static void InitializeSettingsImportExport()
        {
            ConfigList.Importer = AutoQcConfig.ReadXml;
            ConfigList.XmlVersion = AutoQC.Properties.Settings.Default.XmlVersion;
        }
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

}
