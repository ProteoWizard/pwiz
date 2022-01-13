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
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;
using static SkylineBatch.RefineInputObject;

namespace SkylineBatchTest
{
    [TestClass]
    public class BcfgFileTest
    {

        private string AppendToFileName(string filePath, string appendText) => Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + appendText + Path.GetExtension(filePath));

        private string ImportFilePath(string version, string type) => Path.Combine(TestUtils.GetTestFilePath("BcfgTestFiles"), $"{version}_{type}{TextUtil.EXT_BCFG}");
        private string ExpectedFilePath(string version, string type) => AppendToFileName(ImportFilePath(version, type), "_expected");// Path.Combine(TestUtils.GetTestFilePath("BcfgTestFiles"), $"{version}_{type}_expected{TextUtil.EXT_BCFG}");

        private void TestThreeBcfgs(string version)
        {
            TestUtils.InitializeSettingsImportExport();
            TestUtils.InitializeRInstallation();

            TestBcfgFile(version, "basic");
            TestBcfgFile(version, "complex");
            TestBcfgFile(version, "multi");
        }

        private void TestBcfgFile(string version, string type)
        {
            /*var testFolderPath = TestUtils.GetTestFilePath(string.Empty);
            var rawImportFile = ImportFilePath(version, type);
            var updatedImportFile = TestUtils.CopyFileFindReplace(rawImportFile, TestUtils.GetTestFilePath(string.Empty), "REPLACE_TEXT", AppendToFileName(rawImportFile, "(1)"));
            var rawExpectedFile = ExpectedFilePath(version, type);
            var updatedExpectedFile = TestUtils.CopyFileFindReplace(rawExpectedFile, TestUtils.GetTestFilePath(string.Empty), "REPLACE_TEXT", AppendToFileName(rawExpectedFile, "(1)"));
            */
            // create bcfg files with file paths from this computer
            var testFolderPath = TestUtils.GetTestFilePath(string.Empty);
            var rawImportFile = ImportFilePath(version, type);
            var updatedImportFile = TestUtils.CopyFileFindReplace(rawImportFile, "REPLACE_TEXT", testFolderPath, AppendToFileName(rawImportFile, "_replaced"));
            var rawExpectedFile = ExpectedFilePath(version, type);
            var updatedExpectedFile = TestUtils.CopyFileFindReplace(rawExpectedFile, "REPLACE_TEXT", testFolderPath, AppendToFileName(rawExpectedFile, "_replaced"));
            
            // run tests
            CompareImports(updatedImportFile, updatedExpectedFile);
            ImportExportCompare(updatedImportFile, updatedExpectedFile);

            // delete uniqie bcfg files
            File.Delete(updatedImportFile);
            File.Delete(updatedExpectedFile);

        }

        private void CompareImports(string oldVersionFile, string currentVersionFile)
        {
            Assert.IsTrue(File.Exists(oldVersionFile), oldVersionFile + " does not exist. Must import old version configuration(s) from an existing file.");
            Assert.IsTrue(File.Exists(currentVersionFile), currentVersionFile + " does not exist. Please run test in SETUP MODE (see top of BcfgFileTest.cs).");


            var configManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger());
            configManager.Import(oldVersionFile, null);
            IConfig[] oldVersionConfigs = new IConfig[configManager.State.BaseState.ConfigList.Count];
            configManager.State.BaseState.ConfigList.CopyTo(oldVersionConfigs);
            ClearConfigs(configManager);
            configManager.Import(currentVersionFile, null);
            IConfig[] currentVersionConfigs = new IConfig[configManager.State.BaseState.ConfigList.Count];
            configManager.State.BaseState.ConfigList.CopyTo(currentVersionConfigs);

            Assert.AreEqual(oldVersionConfigs.Length, currentVersionConfigs.Length, 0.00001, $"Expected {currentVersionConfigs.Length}" +
                $" configurations to be imported, but instead were {oldVersionConfigs.Length}.");
            for (int i = 0; i < oldVersionConfigs.Length; i++)
            {
                Assert.IsTrue(oldVersionConfigs[i].Equals(currentVersionConfigs[i]), $"Configuration {currentVersionConfigs[i].GetName()} was not imported correctly.");
            }

        }

        private void ImportExportCompare(string filePathImport, string filePathExpectedExport)
        {
            if (!File.Exists(filePathImport))
                Assert.Fail(filePathImport + " does not exist. Must import from an existing file.");
            
            var filePathActualExport = filePathExpectedExport.Replace("expected", "actual");
            var configManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger());
            configManager.Import(filePathImport, null);
            int[] indiciesToSave = new int[configManager.State.BaseState.ConfigList.Count];
            for (int index = 0; index < indiciesToSave.Length; index++)
                indiciesToSave[index] = index;
            
            var configModifiedLinePattern = new Regex(@"^(  <skylinebatch_config name=.*modified=).*>$");//@"^  <skylinebatch_config name=(.*)[.enabled=(.*)]*modified=.*>$"
            configManager.State.BaseState.ExportConfigs(filePathActualExport, SkylineBatch.Properties.Settings.Default.XmlVersion, indiciesToSave);
            TestUtils.CompareFiles(filePathExpectedExport, filePathActualExport, new List<Regex> { configModifiedLinePattern });
            File.Delete(filePathActualExport);
        }

        // Updated Bcfg, includes program version number but no xml version number
        [TestMethod]
        public void TestCurrentBcfg()
        {
            // test version of expected bcfg files to make sure they import correctly
            var filePath = ExpectedFilePath("21_1_0_312", "complex_test");
            var configManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger());
            configManager.Import(filePath, null);
            var actualConfig = configManager.GetConfig(0);

            var expectedTemplate = new SkylineTemplate(null, @"Bruderer.sky.zip",
                null, new PanoramaFile(new RemoteFileSource("panoramaweb.org Bruderer.sky.zip", "https://panoramaweb.org/_webdav/Panorama Public/2021/MacCoss - 2015-Bruderer/%40files/Bruderer.sky.zip", "alimarsh@mit.edu",
                "test", false), string.Empty, TestUtils.GetTestFilePath(string.Empty), "Bruderer.sky"));
            var expectedDataServerInfo = new DataServerInfo(new RemoteFileSource(@"ftp://ftp.peptideatlas.org/", "ftp://ftp.peptideatlas.org/", "PASS00589",
                "WF6554orn", false), string.Empty, "0314_SGSDSsample.*_MHRM_", TestUtils.GetTestFilePath("EmptyData"));
            var expectedAnnotationsFile = new PanoramaFile(new RemoteFileSource("panoramaweb.org MSstats_annotations.sky", "https://panoramaweb.org/_webdav/Panorama%20Public/2021/MacCoss%20-%202015-Bruderer/%40files/reports/MSstats_annotations.sky", "alimarsh@mit.edu",
                "test", false), string.Empty, TestUtils.GetTestFilePath(string.Empty), "MSstats_annotations.sky");

            var expectedMainSettings = new MainSettings(expectedTemplate, TestUtils.GetTestFilePath(@"BcfgTestFiles\Complex"),
                true, TestUtils.GetTestFilePath("EmptyData"), expectedDataServerInfo,
                TestUtils.GetTestFilePath("MSstats_annotations.sky"), expectedAnnotationsFile, "Tester");

            var expectedFileSettings = new FileSettings(1, 1000, 100, true, true, true);

            var expectedRefineObject = new RefineInputObject()
            {
                min_peptides = 1,
                remove_repeats = true,
                remove_duplicates = true,
                missing_library = true,
                min_transitions = 1,
                label_type = "hello",
                add_label_type = true,
                auto_select_peptides = true,
                auto_select_precursors = true,
                auto_select_transitions = true,
                min_peak_found_ratio = 2,
                max_peak_found_ratio = 2,
                max_peptide_peak_rank = 1,
                max_transition_peak_rank = 1,
                max_precursor_only = true,
                prefer_larger_products = true,
                missing_results = true,
                min_time_correlation = 1.1,
                min_dotp = 0.5,
                min_idotp = 1,
                use_best_result = true,
                cv_remove_above_cutoff = 4,
                cv_reference_normalize = "heavy",
                cv_transitions_count = 3,
                qvalue_cutoff = 4.4,
                minimum_detections = 2,
                gc_p_value_cutoff = 3,
                gc_fold_change_cutoff = 1,
                gc_ms_level = 2,
                gc_name = "1",
                cv_global_normalize = CvGlobalNormalizeValues.global_standards,
                cv_transitions = CvTransitionsValues.best,
                cv_ms_level = CvMsLevelValues.products
            };


            var expectedRefineSettings = new RefineSettings(expectedRefineObject, true, true,
                TestUtils.GetTestFilePath("refinedTemplate.sky"));
            var expectedPanoramaDict = new Dictionary<string, PanoramaFile>() {{ TestUtils.GetTestFilePath("MSstats_Bruderer.R"), new PanoramaFile(
                    new RemoteFileSource("panoramaweb.org MSstats_Bruderer.R", "https://panoramaweb.org/_webdav/Panorama%20Public/2021/MacCoss%20-%202015-Bruderer/%40files/reports/MSstats_Bruderer.R", "alimarsh@mit.edu", "test", false),
                    string.Empty, TestUtils.GetTestFilePath(string.Empty), "MSstats_Bruderer.R") }};


            var expectedReportOne = new ReportInfo("MSstats Input-plus", false, null, new List<Tuple<string, string>>(){
                new Tuple<string, string>(TestUtils.GetTestFilePath("MSstats_Bruderer.R"), "4.0.3") },
                new Dictionary<string, PanoramaFile>() {{ TestUtils.GetTestFilePath("MSstats_Bruderer.R"), new PanoramaFile(
                    new RemoteFileSource("panoramaweb.org MSstats_Bruderer.R", "https://panoramaweb.org/_webdav/Panorama%20Public/2021/MacCoss%20-%202015-Bruderer/%40files/reports/MSstats_Bruderer.R", "alimarsh@mit.edu", "test", false),
                    string.Empty, TestUtils.GetTestFilePath(string.Empty), "MSstats_Bruderer.R") }}, false);

            var expectedReportTwo = new ReportInfo("Unique Report", false, TestUtils.GetTestFilePath("UniqueReport.skyr"),
                new List<Tuple<string, string>>(){
                new Tuple<string, string>(TestUtils.GetTestFilePath("testScript.R"), "4.0.3") }, new Dictionary<string, PanoramaFile>(), true);

            var expectedReportSettings = new ReportSettings(new List<ReportInfo>() { expectedReportOne, expectedReportTwo });

            var expectedSkylineSettings = new SkylineSettings(SkylineType.Custom, null, @"C:\Program Files\Skyline");

            var expectedConfig = new SkylineBatchConfig("Complex", false, false, DateTime.Now, expectedMainSettings, expectedFileSettings,
                expectedRefineSettings, expectedReportSettings, expectedSkylineSettings);

            Assert.IsTrue(expectedConfig.Equals(actualConfig));
        }

        [TestMethod]
        public void Test21_1_0_312()
        {
            TestThreeBcfgs("21_1_0_312");
        }

        [TestMethod]
        public void Test21_1_0_306()
        {
            TestThreeBcfgs("21_1_0_306");
        }

        [TestMethod]
        public void Test21_1_0_189()
        {
            TestThreeBcfgs("21_1_0_189");
        }

        [TestMethod]
        public void Test21_1_0_146()
        {
            TestThreeBcfgs("21_1_0_146");
        }

        [TestMethod]
        public void Test20_2_0_464()
        {
            TestThreeBcfgs("20_2_0_464");
        }

        [TestMethod]
        public void Test20_2_0_453()
        {
            TestThreeBcfgs("20_2_0_453");
        }

        // Earliest supported bcfg file
        [TestMethod]
        public void Test20_2_0_414()
        {
            TestThreeBcfgs("20_2_0_414");
        }

        private void ClearConfigs(SkylineBatchConfigManager configManager)
        {
            while (configManager.State.BaseState.HasConfigs())
            {
                configManager.SelectConfig(0);
                configManager.UserRemoveSelected();
            }
        }
    }
}
