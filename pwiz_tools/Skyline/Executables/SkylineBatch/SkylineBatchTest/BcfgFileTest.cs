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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;
using Server = SkylineBatch.Server;

namespace SkylineBatchTest
{
    [TestClass]
    public class BcfgFileTest
    {
        
        [TestMethod]
        public void TestCurrentBcfgFiles()
        {
            var folderPath = TestUtils.GetTestFilePath("BcfgTestFiles");
            var configManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger());

            var minimalConfigPath = Path.Combine(folderPath, "MinimalConfig.bcfg");
            var expectedMinimalConfig = new SkylineBatchConfig("Minimal", false, false, DateTime.MinValue, 
                new MainSettings(SkylineTemplate.FromUi(TestUtils.GetTestFilePath("emptyTemplate.sky"), null, null), Path.Combine(folderPath, "Minimal"), TestUtils.GetTestFilePath("emptyData"), null, string.Empty, null, string.Empty),
                new FileSettings(null, null, null, false, false, false), RefineSettings.Empty(), new ReportSettings(new List<ReportInfo>()), TestUtils.GetTestSkylineSettings());
            CheckValues(configManager, minimalConfigPath, new List<IConfig> { expectedMinimalConfig});

            var templateOnlyPath = Path.Combine(folderPath, "DownloadTemplateOnly.bcfg");
            var expectedTemplateOnly = new SkylineBatchConfig("TemplateOnly", false, false, DateTime.MinValue,
                new MainSettings(SkylineTemplate.FromUi(TestUtils.GetTestFilePath("Selevsek.sky.zip"), null, 
                    new PanoramaFile(new Server("https://panoramaweb.org/MacCoss/brendan/Instruction/2021-DIA-PUBS/2015-Selevsek/targetedms-showPrecursorList.view?fileName=Selevsek.sky.zip",  string.Empty, string.Empty, true), Path.GetDirectoryName(folderPath), "Selevsek.sky.zip")), Path.Combine(folderPath, "TemplateOnly"), TestUtils.GetTestFilePath("emptyData"), null, string.Empty, null, string.Empty),
                expectedMinimalConfig.FileSettings, expectedMinimalConfig.RefineSettings, expectedMinimalConfig.ReportSettings, expectedMinimalConfig.SkylineSettings);
            CheckValues(configManager, templateOnlyPath, new List<IConfig> { expectedTemplateOnly});

            var dataOnlyPath = Path.Combine(folderPath, "DownloadDataOnly.bcfg");
            var expectedDataOnly = new SkylineBatchConfig("DataOnly", false, false, DateTime.MinValue,
                new MainSettings(expectedMinimalConfig.MainSettings.Template, Path.Combine(folderPath, "DataOnly"), TestUtils.GetTestFilePath("emptyData"), 
                    DataServerInfo.ServerFromUi("https://panoramaweb.org/_webdav/MacCoss/brendan/Instruction/2021-DIA-PUBS/2015-Selevsek/%40files/RawFiles/wiff-rep/", string.Empty, string.Empty, true, string.Empty, TestUtils.GetTestFilePath("emptyData")), string.Empty, null, string.Empty),
                expectedMinimalConfig.FileSettings, expectedMinimalConfig.RefineSettings, expectedMinimalConfig.ReportSettings, expectedMinimalConfig.SkylineSettings);
            CheckValues(configManager, dataOnlyPath, new List<IConfig> { expectedDataOnly});

            var annotationsOnlyPath = Path.Combine(folderPath, "DownloadAnnotationsOnly.bcfg");
            var expectedAnnotationsOnly = new SkylineBatchConfig("AnnotationsOnly", false, false, DateTime.MinValue,
                new MainSettings(expectedMinimalConfig.MainSettings.Template, Path.Combine(folderPath, "AnnotationsOnly"), TestUtils.GetTestFilePath("emptyData"),
                    null, Path.Combine(folderPath, "Selevsek-os-annotations.csv"), 
                    new PanoramaFile(new Server("https://panoramaweb.org/_webdav/MacCoss/brendan/Instruction/2021-DIA-PUBS/2015-Selevsek/%40files/reports/Selevsek-os-annotations.csv", string.Empty, string.Empty, true), folderPath, "Selevsek-os-annotations.csv"), string.Empty),
                expectedMinimalConfig.FileSettings, expectedMinimalConfig.RefineSettings, expectedMinimalConfig.ReportSettings, expectedMinimalConfig.SkylineSettings);
            CheckValues(configManager, annotationsOnlyPath, new List<IConfig> { expectedAnnotationsOnly});

            var downloadAllPath = Path.Combine(folderPath, "DownloadAll.bcfg");
            var expectedDownloadAll = new SkylineBatchConfig("DownloadAll", false, false, DateTime.MinValue,
                new MainSettings(expectedTemplateOnly.MainSettings.Template, Path.Combine(folderPath, "DownloadAll"), TestUtils.GetTestFilePath("emptyData"),
                    expectedDataOnly.MainSettings.Server, Path.Combine(folderPath, "Selevsek-os-annotations.csv"),
                    expectedAnnotationsOnly.MainSettings.AnnotationsDownload, string.Empty),
                expectedMinimalConfig.FileSettings, expectedMinimalConfig.RefineSettings, expectedMinimalConfig.ReportSettings, expectedMinimalConfig.SkylineSettings);
            CheckValues(configManager, downloadAllPath, new List<IConfig> { expectedDownloadAll});

            TestUtils.InitializeRInstallation();
            var fullConfigPath = Path.Combine(folderPath, "FullConfig.bcfg");
            var expectedFullConfig = new SkylineBatchConfig("FullConfig", false, false, DateTime.MinValue,
                new MainSettings(expectedDownloadAll.MainSettings.Template, Path.Combine(folderPath, "FullConfig"), TestUtils.GetTestFilePath("emptyData"),
                    expectedDownloadAll.MainSettings.Server, Path.Combine(folderPath, "Selevsek-os-annotations.csv"),
                    expectedDownloadAll.MainSettings.AnnotationsDownload, string.Empty),
                new FileSettings(1,2,3,true,true, true),
                new RefineSettings(RefineInputObject.FromInvariantCommandList(new List<Tuple<RefineVariable, string>>
                {
                    new Tuple<RefineVariable, string>(RefineVariable.min_peptides, "1"),
                    new Tuple<RefineVariable, string>(RefineVariable.min_transitions, "5"),
                    new Tuple<RefineVariable, string>(RefineVariable.add_label_type, "True"),
                    new Tuple<RefineVariable, string>(RefineVariable.max_peptide_peak_rank, "4"),
                    new Tuple<RefineVariable, string>(RefineVariable.cv_remove_above_cutoff, "0.01"),
                    new Tuple<RefineVariable, string>(RefineVariable.cv_global_normalize, "equalize_medians"),
                }), true, true, TestUtils.GetTestFilePath("RefinedOutput.sky") ),
                new ReportSettings(new List<ReportInfo>
                {
                    new ReportInfo("test", true, TestUtils.GetTestFilePath("UniqueReport.skyr"), new List<Tuple<string, string>>
                    {
                        new Tuple<string, string>(TestUtils.GetTestFilePath("testScript.R"), "4.0.3"),
                        new Tuple<string, string>(TestUtils.GetTestFilePath("testScript.R"), "4.0.3"),
                    }, new Dictionary<string, PanoramaFile>(), true),
                    new ReportInfo("test2", false, TestUtils.GetTestFilePath("UniqueReport.skyr"), new List<Tuple<string, string>>
                    {
                        new Tuple<string, string>(TestUtils.GetTestFilePath("testScript.R"), "4.0.3"),
                    }, new Dictionary<string, PanoramaFile>(), false)
                }), expectedMinimalConfig.SkylineSettings);
            CheckValues(configManager, fullConfigPath, new List<IConfig> { expectedFullConfig});

            var multipleConfigsPath = Path.Combine(folderPath, "MultipleConfigs.bcfg");
            var expectedFirstConfig = new SkylineBatchConfig("FirstConfig", false, false, DateTime.MinValue,
                new MainSettings(expectedDownloadAll.MainSettings.Template, Path.Combine(folderPath, "FirstConfig"),
                    TestUtils.GetTestFilePath("emptyData"),
                    expectedFullConfig.MainSettings.Server, Path.Combine(folderPath, "Selevsek-os-annotations.csv"),
                    expectedFullConfig.MainSettings.AnnotationsDownload, string.Empty),
                expectedFullConfig.FileSettings, expectedFullConfig.RefineSettings, expectedFullConfig.ReportSettings,
                expectedFullConfig.SkylineSettings);
            var expectedSecondConfig = new SkylineBatchConfig("SecondConfig", false, false, DateTime.MinValue,
                new MainSettings(expectedDownloadAll.MainSettings.Template, Path.Combine(folderPath, "SecondConfig"),
                    TestUtils.GetTestFilePath("emptyData"),
                    expectedFullConfig.MainSettings.Server, Path.Combine(folderPath, "Selevsek-os-annotations.csv"),
                    expectedFullConfig.MainSettings.AnnotationsDownload, string.Empty),
                expectedFullConfig.FileSettings, expectedFullConfig.RefineSettings, expectedFullConfig.ReportSettings,
                expectedFullConfig.SkylineSettings);

            CheckValues(configManager, multipleConfigsPath, new List<IConfig>{expectedFirstConfig, expectedSecondConfig});


        }

        private void CheckValues(SkylineBatchConfigManager configManager, string importPath, List<IConfig> expectedConfigs)
        {
            ClearConfigs(configManager);
            configManager.Import(importPath, null);
            Assert.AreEqual(expectedConfigs.Count, configManager.ConfigNamesAsObjectArray().Length, $"Expected {expectedConfigs.Count} downloaded config but instead got {configManager.ConfigNamesAsObjectArray().Length}.");
            Assert.AreEqual(true, configManager.IsConfigValid(0), "Expected imported configuration to be valid");
            Assert.AreEqual(true, configManager.ConfigListEquals(expectedConfigs), $"Configurations did not have same values as expected configurations: {Path.GetFileName(importPath)}");
        }

        private void ClearConfigs(SkylineBatchConfigManager configManager)
        {
            while (configManager.ConfigNamesAsObjectArray().Length > 0)
            {
                configManager.SelectConfig(0);
                configManager.UserRemoveSelected();
            }
        }


        [TestMethod]
        public void TestOldBcfgFiles()
        {
            var folderPath = TestUtils.GetTestFilePath("BcfgTestFiles");
            var configManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger());

            var OldMinimalConfigPath = Path.Combine(folderPath, "OldMinimalConfig.bcfg");
            var expectedMinimalConfig = new SkylineBatchConfig("Minimal", false, false, DateTime.MinValue,
                new MainSettings(SkylineTemplate.FromUi(TestUtils.GetTestFilePath("emptyTemplate.sky"), null, null), Path.Combine(folderPath, "Minimal"), TestUtils.GetTestFilePath("emptyData"), null, string.Empty, null, string.Empty),
                new FileSettings(null, null, null, false, false, false), RefineSettings.Empty(), new ReportSettings(new List<ReportInfo>()), TestUtils.GetTestSkylineSettings());
            CheckValues(configManager, OldMinimalConfigPath, new List<IConfig> { expectedMinimalConfig });

            var templateOnlyPath = Path.Combine(folderPath, "OldDownloadTemplateOnly.bcfg");
            var expectedTemplateOnly = new SkylineBatchConfig("OldTemplateOnly", false, false, DateTime.MinValue,
                new MainSettings(SkylineTemplate.FromUi(TestUtils.GetTestFilePath("Selevsek.sky.zip"), null,
                    new PanoramaFile(new Server("https://panoramaweb.org/MacCoss/brendan/Instruction/2021-DIA-PUBS/2015-Selevsek/targetedms-showPrecursorList.view?fileName=Selevsek.sky.zip", string.Empty, string.Empty, true), Path.GetDirectoryName(folderPath), "Selevsek.sky.zip")), Path.Combine(folderPath, "OldTemplateOnly"), TestUtils.GetTestFilePath("emptyData"), null, string.Empty, null, string.Empty),
                expectedMinimalConfig.FileSettings, expectedMinimalConfig.RefineSettings, expectedMinimalConfig.ReportSettings, expectedMinimalConfig.SkylineSettings);
            CheckValues(configManager, templateOnlyPath, new List<IConfig> { expectedTemplateOnly });

           var dataOnlyPath = Path.Combine(folderPath, "OldDownloadDataOnly.bcfg");
            var expectedDataOnly = new SkylineBatchConfig("OldDataOnly", false, false, DateTime.MinValue,
                new MainSettings(expectedMinimalConfig.MainSettings.Template, Path.Combine(folderPath, "OldDataOnly"), TestUtils.GetTestFilePath("emptyData"),
                    DataServerInfo.ServerFromUi("https://panoramaweb.org/_webdav/MacCoss/brendan/Instruction/2021-DIA-PUBS/2015-Selevsek/%40files/RawFiles/wiff-rep/", string.Empty, string.Empty, true, string.Empty, TestUtils.GetTestFilePath("emptyData")), string.Empty, null, string.Empty),
                expectedMinimalConfig.FileSettings, expectedMinimalConfig.RefineSettings, expectedMinimalConfig.ReportSettings, expectedMinimalConfig.SkylineSettings);
            CheckValues(configManager, dataOnlyPath, new List<IConfig> { expectedDataOnly });

            var annotationsOnlyPath = Path.Combine(folderPath, "OldDownloadAnnotationsOnly.bcfg");
            var expectedAnnotationsOnly = new SkylineBatchConfig("OldAnnotationsOnly", false, false, DateTime.MinValue,
                new MainSettings(expectedMinimalConfig.MainSettings.Template, Path.Combine(folderPath, "OldAnnotationsOnly"), TestUtils.GetTestFilePath("emptyData"),
                    null, TestUtils.GetTestFilePath("fakeAnnotations.csv"), null, string.Empty),
                expectedMinimalConfig.FileSettings, expectedMinimalConfig.RefineSettings, expectedMinimalConfig.ReportSettings, expectedMinimalConfig.SkylineSettings);
            CheckValues(configManager, annotationsOnlyPath, new List<IConfig> { expectedAnnotationsOnly });

            var downloadAllPath = Path.Combine(folderPath, "OldDownloadAll.bcfg");
            var expectedDownloadAll = new SkylineBatchConfig("OldDownloadAll", false, false, DateTime.MinValue,
                new MainSettings(expectedTemplateOnly.MainSettings.Template, Path.Combine(folderPath, "OldDownloadAll"), TestUtils.GetTestFilePath("emptyData"),
                    expectedDataOnly.MainSettings.Server, expectedAnnotationsOnly.MainSettings.AnnotationsFilePath,
                    expectedAnnotationsOnly.MainSettings.AnnotationsDownload, string.Empty),
                expectedMinimalConfig.FileSettings, expectedMinimalConfig.RefineSettings, expectedMinimalConfig.ReportSettings, expectedMinimalConfig.SkylineSettings);
            CheckValues(configManager, downloadAllPath, new List<IConfig> { expectedDownloadAll });

            TestUtils.InitializeRInstallation();
            var fullConfigPath = Path.Combine(folderPath, "OldFullConfig.bcfg");
            var expectedFullConfig = new SkylineBatchConfig("OldFullConfig", false, false, DateTime.MinValue,
                new MainSettings(expectedDownloadAll.MainSettings.Template, Path.Combine(folderPath, "OldFullConfig"), TestUtils.GetTestFilePath("emptyData"),
                    expectedDownloadAll.MainSettings.Server, expectedAnnotationsOnly.MainSettings.AnnotationsFilePath,
                    expectedDownloadAll.MainSettings.AnnotationsDownload, string.Empty),
                new FileSettings(1, 2, 3, true, true, true),
                new RefineSettings(RefineInputObject.FromInvariantCommandList(new List<Tuple<RefineVariable, string>>
                {
                    new Tuple<RefineVariable, string>(RefineVariable.min_peptides, "1"),
                    new Tuple<RefineVariable, string>(RefineVariable.min_transitions, "5"),
                    new Tuple<RefineVariable, string>(RefineVariable.add_label_type, "True"),
                    new Tuple<RefineVariable, string>(RefineVariable.max_peptide_peak_rank, "4"),
                    new Tuple<RefineVariable, string>(RefineVariable.cv_remove_above_cutoff, "0.01"),
                    new Tuple<RefineVariable, string>(RefineVariable.cv_global_normalize, "equalize_medians"),
                }), true, true, TestUtils.GetTestFilePath("RefinedOutput.sky")),
                new ReportSettings(new List<ReportInfo>
                {
                    new ReportInfo("test", true, TestUtils.GetTestFilePath("UniqueReport.skyr"), new List<Tuple<string, string>>
                    {
                        new Tuple<string, string>(TestUtils.GetTestFilePath("testScript.R"), "4.0.3"),
                        new Tuple<string, string>(TestUtils.GetTestFilePath("testScript.R"), "4.0.3"),
                    }, new Dictionary<string, PanoramaFile>(), true),
                    new ReportInfo("test2", false, TestUtils.GetTestFilePath("UniqueReport.skyr"), new List<Tuple<string, string>>
                    {
                        new Tuple<string, string>(TestUtils.GetTestFilePath("testScript.R"), "4.0.3"),
                    }, new Dictionary<string, PanoramaFile>(), false)
                }), expectedMinimalConfig.SkylineSettings);
            CheckValues(configManager, fullConfigPath, new List<IConfig> { expectedFullConfig });
            
            var multipleConfigsPath = Path.Combine(folderPath, "OldMultipleConfigs.bcfg");
            var expectedFirstConfig = new SkylineBatchConfig("FirstConfig", false, false, DateTime.MinValue,
                new MainSettings(expectedDownloadAll.MainSettings.Template, Path.Combine(folderPath, "FirstConfig"),
                    TestUtils.GetTestFilePath("emptyData"),
                    expectedFullConfig.MainSettings.Server, expectedFullConfig.MainSettings.AnnotationsFilePath,
                    expectedFullConfig.MainSettings.AnnotationsDownload, string.Empty),
                expectedFullConfig.FileSettings, expectedFullConfig.RefineSettings, expectedFullConfig.ReportSettings,
                expectedFullConfig.SkylineSettings);
            var expectedSecondConfig = new SkylineBatchConfig("SecondConfig", false, false, DateTime.MinValue,
                new MainSettings(expectedDownloadAll.MainSettings.Template, Path.Combine(folderPath, "SecondConfig"),
                    TestUtils.GetTestFilePath("emptyData"),
                    expectedFullConfig.MainSettings.Server, expectedFullConfig.MainSettings.AnnotationsFilePath,
                    expectedFullConfig.MainSettings.AnnotationsDownload, string.Empty),
                expectedFullConfig.FileSettings, expectedFullConfig.RefineSettings, expectedFullConfig.ReportSettings,
                expectedFullConfig.SkylineSettings);

            CheckValues(configManager, multipleConfigsPath, new List<IConfig> { expectedFirstConfig, expectedSecondConfig });
        }



        [TestMethod]
        public void TestOldestBcfgFile()
        {
            var folderPath = TestUtils.GetTestFilePath("BcfgTestFiles");
            var configManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger());

            var OldestConfigPath = Path.Combine(folderPath, "AncientBcfg.bcfg");
            var expectedMinimalConfig = new SkylineBatchConfig("Oldest", false, false, DateTime.MinValue,
                new MainSettings(SkylineTemplate.FromUi(TestUtils.GetTestFilePath("emptyTemplate.sky"), null, null), Path.Combine(folderPath, "Oldest"), TestUtils.GetTestFilePath("emptyData"), null, string.Empty, null, "Tester"),
                new FileSettings(null, null, null, false, false, false), RefineSettings.Empty(), new ReportSettings(new List<ReportInfo>()), TestUtils.GetTestSkylineSettings());
            CheckValues(configManager, OldestConfigPath, new List<IConfig> { expectedMinimalConfig });
        }
    }
}
