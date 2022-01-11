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
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{

    [TestClass]
    public class SkylineBatchConfigTest
    {

        

        [TestMethod]
        public void TestValidateConfig()
        {
            TestUtils.InitializeRInstallation();
            var validName = "Name";
            var invalidName = string.Empty;
            var validSkyr = TestUtils.GetTestFilePath("UniqueReport.skyr");
            var invalidSkyr = "invalidPath.skyr";
            var validRScripts = new List<Tuple<string,string>>();
            var invalidRscripts = new List<Tuple<string, string>>{new Tuple<string, string>("invalidPath.r", "1.2.3")};
            TestValidateReportSettings(validName, invalidSkyr, validRScripts, "Report path invalidPath.skyr is not a valid path.\r\nPlease enter a path to an existing file.");
            TestValidateReportSettings(validName, validSkyr, invalidRscripts, "R script path invalidPath.r is not a valid path.\r\nPlease enter a path to an existing file.");
            TestValidateReportSettings(invalidName, validSkyr, validRScripts, "Report must have name.");
            try
            {
                var validReport = new ReportInfo(validName, false, validSkyr, validRScripts, false);
                validReport.Validate();
            }
            catch (Exception e)
            {
                Assert.Fail("Should have validated valid ReportInfo. Threw exception: " + e.Message);
            }

            var validTemplatePath = TestUtils.GetTestFilePath("emptyTemplate.sky");
            var invalidTemplatePath = "U:\\nonexistent.sky";
            var validAnalysisFolder = TestUtils.GetTestFilePath(string.Empty) + "folderToCreate";
            var invalidAnalysisFolder = TestUtils.GetTestFilePath(string.Empty) + @"nonexistentOne\nonexistentTwo\";
            var validDataDir = TestUtils.GetTestFilePath("emptyData");
            var invalidDataDir = TestUtils.GetTestFilePath("nonexistentData");
            var validPattern = string.Empty;

            TestValidateMainSettings(invalidTemplatePath, validAnalysisFolder, validDataDir, validPattern,
                string.Format("The Skyline template file {0} does not exist.\r\nPlease provide a valid file.", invalidTemplatePath));
            TestValidateMainSettings(validTemplatePath, invalidAnalysisFolder, validDataDir, validPattern,
                string.Format(SkylineBatch.Properties.Resources.MainSettings_ValidateAnalysisFolder_The__parent_directory_of_the_analysis_folder__0__does_not_exist_, TestUtils.GetTestFilePath(string.Empty) + @"nonexistentOne\nonexistentTwo") + Environment.NewLine +
                SkylineBatch.Properties.Resources.MainSettings_ValidateAnalysisFolder_Please_provide_a_valid_folder_);
            TestValidateMainSettings(validTemplatePath, validAnalysisFolder, invalidDataDir, validPattern,
                string.Format("The data folder {0} does not exist.\r\nPlease provide a valid folder.", invalidDataDir));
            TestValidateMainSettings(invalidTemplatePath, invalidAnalysisFolder, invalidDataDir, validPattern,
                string.Format("The Skyline template file {0} does not exist.\r\nPlease provide a valid file.", invalidTemplatePath));
            try
            {
                var testValidMainSettings = new MainSettings(validTemplatePath, validAnalysisFolder, validDataDir, null, string.Empty, string.Empty, string.Empty);
                testValidMainSettings.Validate();
            }
            catch (Exception e)
            {
                Assert.Fail("Should have validated valid MainSettings. Threw exception: " + e.Message);
            }

            var validMainSettings = new MainSettings(validTemplatePath, validAnalysisFolder, validDataDir, null, string.Empty, string.Empty, string.Empty);
            var validReportSettings = new ReportSettings(new List<ReportInfo>());
            var validSkylineSettings = new SkylineSettings(SkylineType.Custom, TestUtils.GetSkylineDir());
            var validFileSettings = FileSettings.FromUi(string.Empty, string.Empty, string.Empty, false, false, true);
            var validRefineSettings = new RefineSettings(new RefineInputObject(), false, false, string.Empty);

            var validatedNoName = false;
            try
            {
                var invalidConfig = new SkylineBatchConfig(invalidName, false, DateTime.MinValue, validMainSettings, validFileSettings, validRefineSettings, validReportSettings, validSkylineSettings);
                invalidConfig.Validate();
                validatedNoName = true;
            }
            catch (Exception e)
            {
                Assert.AreEqual("\"\" is not a valid name for the configuration.\r\nPlease enter a name.", e.Message);
            }
            Assert.IsTrue(!validatedNoName, "Should have failed to validate invalid config");

            try
            {
                var validConfig = new SkylineBatchConfig(validName, false, DateTime.MinValue, validMainSettings, validFileSettings, validRefineSettings, validReportSettings, validSkylineSettings);
                validConfig.Validate();
            }
            catch (Exception x)
            {
                Assert.Fail("Should have validated valid config: " + x.Message);
            }
        }

        private void TestValidateReportSettings(string name, string path, List<Tuple<string, string>> scripts, string expectedError)
        {
            var validatedInvalidReportInfo = false;
            try
            {
                var invalidReport = new ReportInfo(name, false, path, scripts, false);
                invalidReport.Validate();
                validatedInvalidReportInfo = true;
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(expectedError, e.Message);
            }
            Assert.IsTrue(!validatedInvalidReportInfo, "Should have failed to validate ReportInfo");
        }

        private void TestValidateMainSettings(string template, string analysis, string data, string pattern, string expectedError)
        {
            var invalidMainSettings = new MainSettings(template, analysis, data, null, string.Empty,  pattern, string.Empty);
            var validatedInvalidMainSettings = false;
            try
            {
                invalidMainSettings.Validate();
                validatedInvalidMainSettings = true;
            }
            catch (ArgumentException e)
            {
                Assert.AreEqual(expectedError, e.Message);
            }
            Assert.IsTrue(!validatedInvalidMainSettings, "Should have failed to validate MainSettings");
        }

        [TestMethod]
        public void TestMainSettingsEquals()
        {
            var testMainSettings = TestUtils.GetTestMainSettings();
            Assert.IsTrue(Equals(testMainSettings, TestUtils.GetTestMainSettings()));
            var differentMainSettings = new MainSettings(testMainSettings.TemplateFilePath, 
                testMainSettings.AnalysisFolderPath, testMainSettings.DataFolderPath, null, string.Empty, "differentPattern", string.Empty);
            Assert.IsFalse(Equals(testMainSettings, null));
            Assert.IsFalse(Equals(testMainSettings, differentMainSettings));
        }

        [TestMethod]
        public void TestReportSettingsEquals()
        {
            TestUtils.InitializeRInstallation();
            var testReportInfoNoScript = new ReportInfo("Name", false, TestUtils.GetTestFilePath("UniqueReport.skyr"), new List<Tuple<string, string>>(), false);
            var testReportInfoWithScript = TestUtils.GetTestReportInfo();
            Assert.IsTrue(Equals(testReportInfoNoScript,
                new ReportInfo("Name", false, TestUtils.GetTestFilePath("UniqueReport.skyr"), new List<Tuple<string, string>>(), false)));
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
            TestUtils.InitializeRInstallation();
            var testConfig = TestUtils.GetTestConfig();
            Assert.IsTrue(Equals(testConfig, TestUtils.GetTestConfig()));
            Assert.IsFalse(Equals(testConfig, TestUtils.GetTestConfig("other")));

            var differentReportSettings = new SkylineBatchConfig("name", false, DateTime.MinValue, 
                TestUtils.GetTestMainSettings(), TestUtils.GetTestFileSettings(), TestUtils.GetTestRefineSettings(), 
                new ReportSettings(new List<ReportInfo>()), TestUtils.GetTestSkylineSettings());
            Assert.IsFalse(Equals(testConfig, differentReportSettings));

            var differentMain = new MainSettings(testConfig.MainSettings.TemplateFilePath,
                TestUtils.GetTestFilePath(string.Empty), testConfig.MainSettings.DataFolderPath,
                null, string.Empty, testConfig.MainSettings.ReplicateNamingPattern, string.Empty);
            var differentMainSettings = new SkylineBatchConfig("name", false, DateTime.MinValue, 
                differentMain, TestUtils.GetTestFileSettings(), TestUtils.GetTestRefineSettings(), TestUtils.GetTestReportSettings(), 
                new SkylineSettings(SkylineType.Skyline));
            Assert.IsFalse(Equals(testConfig, differentMainSettings));
        }
    }
}