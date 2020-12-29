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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkylineBatch;

namespace SkylineBatchTest
{

    [TestClass]
    public class SkylineBatchConfigTest
    {

        

        [TestMethod]
        public void TestValidateConfig()
        {
            TestUtils.InitializeInstallations();
            var validName = "Name";
            var invalidName = "";
            var validSkyr = TestUtils.GetTestFilePath("UniqueReport.skyr");
            var invalidSkyr = "invalidPath.skyr";
            var validRScripts = new List<Tuple<string,string>>();
            var invalidRscripts = new List<Tuple<string, string>>{new Tuple<string, string>("invalidPath.r", "1.2.3")};
            TestValidateReportSettings(validName, invalidSkyr, validRScripts, "Report \"Name\": Report path invalidPath.skyr is not a valid path.");
            TestValidateReportSettings(validName, validSkyr, invalidRscripts, "Report \"Name\": R script path invalidPath.r is not a valid path.");
            TestValidateReportSettings(invalidName, validSkyr, validRScripts, "Report must have name.");
            try
            {
                var validReport = new ReportInfo(validName, validSkyr, validRScripts);
                validReport.Validate();
            }
            catch (Exception)
            {
                Assert.Fail("Should have validated valid ReportInfo");
            }

            var validTemplatePath = TestUtils.GetTestFilePath("emptyTemplate.sky");
            var invalidTemplatePath = TestUtils.GetTestFilePath("nonexistent.sky");
            var validAnalysisFolder = TestUtils.GetTestFilePath("") + "folderToCreate";
            var invalidAnalysisFolder = TestUtils.GetTestFilePath("") + @"nonexistentOne\nonexistentTwo\";
            var validDataDir = TestUtils.GetTestFilePath("emptyData");
            var invalidDataDir = TestUtils.GetTestFilePath("nonexistentData");
            var validPattern = "";

            TestValidateMainSettings(invalidTemplatePath, validAnalysisFolder, validDataDir, validPattern,
                string.Format("Skyline file {0} does not exist.", invalidTemplatePath));
            TestValidateMainSettings(validTemplatePath, invalidAnalysisFolder, validDataDir, validPattern,
                string.Format("Analysis folder directory {0} does not exist.", TestUtils.GetTestFilePath("") + @"nonexistentOne\nonexistentTwo"));
            TestValidateMainSettings(validTemplatePath, validAnalysisFolder, invalidDataDir, validPattern,
                string.Format("Data folder {0} does not exist.", invalidDataDir));
            TestValidateMainSettings(invalidTemplatePath, invalidAnalysisFolder, invalidDataDir, validPattern,
                string.Format("Skyline file {0} does not exist.", invalidTemplatePath));
            try
            {
                var testValidMainSettings = new MainSettings(validTemplatePath, validAnalysisFolder, validDataDir, null);
                testValidMainSettings.Validate();
            }
            catch (Exception)
            {
                Assert.Fail("Should have validated valid MainSettings");
            }

            var validMainSettings = new MainSettings(validTemplatePath, validAnalysisFolder, validDataDir, null);
            var validReportSettings = new ReportSettings(new List<ReportInfo>());
            var validSkylineSettings = new SkylineSettings(SkylineType.Custom, "C:\\Program Files\\Skyline");

            try
            {
                var invalidConfig = new SkylineBatchConfig(invalidName, DateTime.MinValue, DateTime.MinValue, validMainSettings, validReportSettings, validSkylineSettings);
                invalidConfig.Validate();
                Assert.Fail("Should have failed to validate invalid config");
            }
            catch (Exception e)
            {
                Assert.AreEqual("Please enter a name for the configuration.", e.Message);
            }

            try
            {
                var validConfig = new SkylineBatchConfig(validName, DateTime.MinValue, DateTime.MinValue, validMainSettings, validReportSettings, validSkylineSettings);
                validConfig.Validate();
            }
            catch (Exception e)
            {
                Assert.Fail("Should have validated valid config");
            }
        }

        private void TestValidateReportSettings(string name, string path, List<Tuple<string, string>> scripts, string expectedError)
        {
            try
            {
                var invalidReport = new ReportInfo(name, path, scripts);
                invalidReport.Validate();
                Assert.Fail("Should have failed to validate ReportInfo");
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(expectedError, e.Message);
            }
        }

        private void TestValidateMainSettings(string template, string analysis, string data, string pattern, string expectedError)
        {
            var invalidMainSettings = new MainSettings(template, analysis, data, pattern);
            try
            {
                invalidMainSettings.Validate();
                Assert.Fail("Should have failed to validate MainSettings");
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(expectedError, e.Message);
            }
        }

        [TestMethod]
        public void TestMainSettingsEquals()
        {
            var testMainSettings = TestUtils.GetTestMainSettings();
            Assert.IsTrue(Equals(testMainSettings, TestUtils.GetTestMainSettings()));
            var differentMainSettings = new MainSettings(testMainSettings.TemplateFilePath, 
                testMainSettings.AnalysisFolderPath, testMainSettings.DataFolderPath, "differentPattern");
            Assert.IsFalse(Equals(testMainSettings, null));
            Assert.IsFalse(Equals(testMainSettings, differentMainSettings));
        }

        [TestMethod]
        public void TestReportSettingsEquals()
        {
            TestUtils.InitializeInstallations();
            var testReportInfoNoScript = new ReportInfo("Name", TestUtils.GetTestFilePath("UniqueReport.skyr"), new List<Tuple<string, string>>());
            var testReportInfoWithScript = TestUtils.GetTestReportInfo();
            Assert.IsTrue(Equals(testReportInfoNoScript,
                new ReportInfo("Name", TestUtils.GetTestFilePath("UniqueReport.skyr"), new List<Tuple<string, string>>())));
            Assert.IsFalse(Equals(testReportInfoNoScript, testReportInfoWithScript));
            //TestUtils.GetTestReportSettings();
            var emptyReportSettings = new ReportSettings(new List<ReportInfo>());
            var reportSettingsWithScript = TestUtils.GetTestReportSettings();
            Assert.IsTrue(Equals(emptyReportSettings, new ReportSettings(new List<ReportInfo>())));

            var reportList = new List<ReportInfo>();
            reportList.Add(testReportInfoNoScript);
            var changedReportSettings = new ReportSettings(reportList);
            Assert.IsFalse(Equals(emptyReportSettings, changedReportSettings));
            Assert.IsFalse(Equals(changedReportSettings, reportSettingsWithScript));
        }

        [TestMethod]
        public void TestConfigEquals()
        {
            TestUtils.InitializeInstallations();
            var testConfig = TestUtils.GetTestConfig();
            Assert.IsTrue(Equals(testConfig, TestUtils.GetTestConfig()));
            Assert.IsFalse(Equals(testConfig, TestUtils.GetTestConfig("other")));

            var differentReportSettings = new SkylineBatchConfig("name", DateTime.MinValue, DateTime.MinValue, TestUtils.GetTestMainSettings(), new ReportSettings(new List<ReportInfo>()), new SkylineSettings(SkylineType.Skyline));
            Assert.IsFalse(Equals(testConfig, differentReportSettings));

            var differentMain = new MainSettings(testConfig.MainSettings.TemplateFilePath,
                TestUtils.GetTestFilePath(""), testConfig.MainSettings.DataFolderPath,
                testConfig.MainSettings.ReplicateNamingPattern);
            var differentMainSettings = new SkylineBatchConfig("name", DateTime.MinValue, DateTime.MinValue, differentMain, TestUtils.GetTestReportSettings(), new SkylineSettings(SkylineType.Skyline));
            Assert.IsFalse(Equals(testConfig, differentMainSettings));
        }
        
    }
}