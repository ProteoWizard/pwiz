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
using AutoQC;
using SharedBatch;

namespace AutoQCTest
{
    public class TestUtils
    {
        public static string GetTestFilePath(string fileName)
        {
            var currentPath = Directory.GetCurrentDirectory();
            var autoQcTestPath = Path.GetDirectoryName(Path.GetDirectoryName(currentPath));
            return autoQcTestPath + "\\Test\\" + fileName;
        }

        public static string CreateTestFolder(string folderName)
        {
            var newFolder = GetTestFilePath(folderName);
            Directory.CreateDirectory(newFolder);
            return newFolder;
        }

        public static MainSettings GetTestMainSettings() => GetTestMainSettings(string.Empty, string.Empty);
        

        public static MainSettings GetTestMainSettings(string changedVariable, string value)
        {
            var skylineFilePath = GetTestFilePath("QEP_2015_0424_RJ.sky");
            var folderToWatch = changedVariable.Equals("folderToWatch")? GetTestFilePath(value) : GetTestFilePath("Config");

            var includeSubfolders = changedVariable.Equals("includeSubfolders") && value.Equals("true");
            var fileFilter = MainSettings.GetDefaultQcFileFilter();
            var resultsWindow = MainSettings.GetDefaultResultsWindow();
            var removeResults = MainSettings.GetDefaultRemoveResults();
            var acquisitionTime = MainSettings.GetDefaultAcquisitionTime();
            var instrumentType = MainSettings.GetDefaultInstrumentType();
            
            
            return new MainSettings(skylineFilePath, folderToWatch, includeSubfolders, fileFilter, removeResults, resultsWindow, instrumentType, acquisitionTime);
        }

        public static PanoramaSettings GetTestPanoramaSettings(bool publishToPanorama = true)
        {
            var panoramaServerUrl = publishToPanorama ? "https://panoramaweb.org/" : "";
            var panoramaUserEmail = publishToPanorama ? "skyline_tester@proteinms.net" : "";
            var panoramaPassword = publishToPanorama ? "lclcmsms" : "";
            var panoramaFolder = publishToPanorama ? "/SkylineTest" : "";

            return new PanoramaSettings(publishToPanorama, panoramaServerUrl, panoramaUserEmail, panoramaPassword, panoramaFolder);
        }

        public static SkylineSettings GetTestSkylineSettings()
        {
            return new SkylineSettings(SkylineType.Custom, "C:\\Program Files\\Skyline");
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
            var logFile = GetTestFilePath("TestLogs\\AutoQC.log");
            return new Logger(logFile, config.Name);
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

            foreach(var config in configs)
                testConfigManager.AddConfiguration(config);
            
            return testConfigManager;
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
