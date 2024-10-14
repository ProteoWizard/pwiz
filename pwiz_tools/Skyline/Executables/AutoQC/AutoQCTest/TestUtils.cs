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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.PanoramaClient;
using SharedBatch;
using SharedBatch.Properties;
using SharedBatchTest;

namespace AutoQCTest
{
    public class TestUtils
    { 
        public const string PANORAMAWEB = "https://panoramaweb.org";
        // public const string PANORAMAWEB = "http://localhost:8080";
        public const string PANORAMAWEB_USER = "skyline_tester_admin@proteinms.net";
        public const string PANORAMAWEB_TEST_FOLDER = "SkylineTest/AutoQcTest";

        /// <summary>
        /// Set this environment variable on your system to the password for
        /// the PANORAMAWEB_USER above. Ask Vagisha or someone else with access
        /// to share this password with you.
        /// </summary>
        private const string PASSWORD_ENVT_VAR = "PANORAMAWEB_PASSWORD";

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
            var panoramaServerUrl = publishToPanorama ? PANORAMAWEB : "";
            var panoramaUserEmail = publishToPanorama ? PANORAMAWEB_USER : "";
            var panoramaPassword = publishToPanorama ? GetPanoramaWebPassword() : "";
            var panoramaProject = publishToPanorama ? PANORAMAWEB_TEST_FOLDER : "";

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
                testConfigManager.SetState(testConfigManager.AutoQcState,
                    testConfigManager.AutoQcState.UserAddConfig(config, null));
            
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

        public static void AssertTextsInThisOrder(string source, params string[] textsInOrder)
        {
            var previousIndex = -1;
            string previousString = null;

            Assert.IsFalse(string.IsNullOrWhiteSpace(source), "Source string cannot be blank");
            Assert.IsNotNull(textsInOrder, "No texts to compare");
            foreach (var part in textsInOrder)
            {
                var index = source.IndexOf(part, previousIndex == -1 ? 0 : previousIndex, StringComparison.Ordinal);

                if (index == -1)
                    Assert.Fail("Text '{0}' not found{1} in '{2}'.", part,
                        previousString == null ? string.Empty : $" after '{previousString}'",
                        source);

                previousIndex = index;
                previousString = part;
            }
        }

        public static string GetPanoramaWebPassword()
        {
            var panoramaWebPassword = Environment.GetEnvironmentVariable(PASSWORD_ENVT_VAR);
            if (string.IsNullOrWhiteSpace(panoramaWebPassword))
            {
                Assert.Fail(
                    $"Environment variable ({PASSWORD_ENVT_VAR}) with the PanoramaWeb password for {PANORAMAWEB_USER} is not set. Cannot run test.");
            }

            return panoramaWebPassword;
        }

        public static string CreatePanoramaWebTestFolder(WebPanoramaClient panoramaClient, string parentFolder, string folderName)
        {
            // Create a PanoramaWeb folder for the test
            var random = new Random();
            string uniqueFolderName;
            do
            {
                uniqueFolderName = folderName + random.Next(1000, 9999);
            }
            while (panoramaClient.FolderExists(parentFolder + @"/" + uniqueFolderName));

            AssertEx.NoExceptionThrown<Exception>(() => panoramaClient.CreateTargetedMsFolder(parentFolder, uniqueFolderName));
            return $"{parentFolder}/{uniqueFolderName}";
        }

        public static void DeletePanoramaWebTestFolder(WebPanoramaClient panoramaClient, string folderPath)
        {
            AssertEx.NoExceptionThrown<Exception>(() => panoramaClient?.DeleteFolderIfExists(folderPath));
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
