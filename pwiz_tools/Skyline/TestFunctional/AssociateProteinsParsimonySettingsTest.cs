/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Proteome;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests choosing different parsimony settings in <see cref="AssociateProteinsDlg"/>
    /// </summary>
    [TestClass]
    public class AssociateProteinsParsimonySettingsTest : AbstractFunctionalTest
    {
        // What fraction of scenarios to test. 1=Every scenario, 3=Every third scenario
        private int _scenarioFractionToTest = 1;
        protected override bool IsRecordMode => false;
        private string GetExpectedValuesFilePath()
        {
            return Path.Combine(ExtensionTestContext.GetProjectDirectory(
                @"TestFunctional\AssociateProteinsParsimonySettingsTest.data"), "ExpectedValues.json");
        }
        private const string _testFileZipPath = @"TestFunctional\AssociateProteinsParsimonySettingsTest.zip";


        [TestMethod]
        public void TestAssociateProteinsParsimonySettings()
        {
            TestFilesZip = _testFileZipPath;
            RunFunctionalTest();
        }

        /// <summary>
        /// Cycles through the same scenarios as <see cref="TestAssociateProteinsParsimonySettings"/> but
        /// only waits for a quarter of the scenarios to finish. This verifies that
        /// <see cref="AssociateProteinsResults.PRODUCER"/> does not have any threading issues.
        /// </summary>
        [TestMethod]
        public void TestQuarterOfAssociateProteinParsimonySettings()
        {
            _scenarioFractionToTest = 4;
            TestFilesZip = _testFileZipPath;
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            if (_scenarioFractionToTest != 1)
            {
                Assert.IsFalse(IsRecordMode);
            }
            var parsimonySettingsList = EnumerateParsimonySettings().ToList();
            MappingResults[] expectedValues;
            string expectedValuesPath = GetExpectedValuesFilePath();
            if (IsRecordMode)
            {
                expectedValues = new MappingResults[parsimonySettingsList.Count];
            }
            else
            {
                using var streamReader = File.OpenText(expectedValuesPath);
                using var jsonReader = new JsonTextReader(streamReader);
                expectedValues = JsonSerializer.Create().Deserialize<MappingResults[]>(jsonReader);
                Assert.AreEqual(parsimonySettingsList.Count, expectedValues.Length);
            }

            RunUI(() =>
            {
                SkylineWindow.Paste(File.ReadAllText(TestFilesDir.GetTestPath("Peptides.txt")));
            });
            var associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            RunUI(()=>associateProteinsDlg.FastaFileName = TestFilesDir.GetTestPath("human_yeast_ecoli_iRT_singleline.fasta"));
            for (int iScenario = 0; iScenario < parsimonySettingsList.Count; iScenario++)
            {
                var parsimonySettings = parsimonySettingsList[iScenario];
                RunUI(() =>
                {
                    associateProteinsDlg.GroupProteins = parsimonySettings.GroupProteins;
                    associateProteinsDlg.GeneLevelParsimony = parsimonySettings.GeneLevelParsimony;
                    associateProteinsDlg.FindMinimalProteinList = parsimonySettings.FindMinimalProteinList;
                    associateProteinsDlg.RemoveSubsetProteins = parsimonySettings.RemoveSubsetProteins;
                    associateProteinsDlg.SelectedSharedPeptides = parsimonySettings.SharedPeptides;
                });
                if (0 != iScenario % _scenarioFractionToTest)
                {
                    Thread.Sleep(50);
                    continue;
                }
                WaitForCondition(() => associateProteinsDlg.IsComplete);
                Assert.IsNotNull(associateProteinsDlg.FinalResults);
                var actualMappingResults = GetMappingResults(associateProteinsDlg);
                if (IsRecordMode)
                {
                    expectedValues[iScenario] = actualMappingResults;
                }
                else
                {
                    var expectedMappingResults = expectedValues[iScenario];
                    Assert.AreEqual(expectedMappingResults.ToString(), actualMappingResults.ToString(), "Mismatch at scenario #{0}", iScenario);
                }
            }

            if (IsRecordMode)
            {
                using var streamWriter = new StreamWriter(expectedValuesPath);
                using var jsonTextWriter = new JsonTextWriter(streamWriter);
                JsonSerializer.Create(new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                }).Serialize(jsonTextWriter, expectedValues);
            }

            WaitForCondition(() => associateProteinsDlg.IsOkEnabled);
            OkDialog(associateProteinsDlg, associateProteinsDlg.OkDialog);
        }

        private IEnumerable<ProteinAssociation.ParsimonySettings> EnumerateParsimonySettings()
        {
            foreach (var groupProteins in new[] { false, true })
            {
                foreach (var geneLevelParsimony in groupProteins ? new[] { false } : new[] { false, true })
                {
                    foreach (var findMinimalProteinList in new[] { false, true })
                    {
                        foreach (var removeSubsetProteins in new[] { false, true })
                        {
                            foreach (ProteinAssociation.SharedPeptides sharedPeptides in Enum.GetValues(
                                         typeof(ProteinAssociation.SharedPeptides)))
                            {
                                yield return new ProteinAssociation.ParsimonySettings(groupProteins,
                                    geneLevelParsimony, findMinimalProteinList, removeSubsetProteins,
                                    sharedPeptides, 1);
                            }
                        }
                    }
                }
            }
        }

        private MappingResults GetMappingResults(AssociateProteinsDlg associateProteinsDlg)
        {
            var finalResults = associateProteinsDlg.FinalResults;
            return new MappingResults
            {
                MappedProteins = finalResults.ProteinsMapped,
                UnmappedProteins = finalResults.ProteinsUnmapped,
                TargetProteins = finalResults.FinalProteinCount,
                MappedPeptides = finalResults.PeptidesMapped,
                UnmappedPeptides = finalResults.PeptidesUnmapped,
                TargetPeptides = finalResults.FinalPeptideCount,
                MappedSharedPeptides = finalResults.TotalSharedPeptideCount,
                TargetSharedPeptides = finalResults.FinalSharedPeptideCount
            };
        }

        private class MappingResults
        {
            public int MappedProteins { get; set; }
            public int UnmappedProteins { get; set; }
            public int TargetProteins { get; set; }
            public int MappedPeptides { get; set; }
            public int UnmappedPeptides { get; set; }
            public int TargetPeptides { get; set; }
            public int MappedSharedPeptides { get; set; }
            public int TargetSharedPeptides { get; set; }

            public override string ToString()
            {
                return string.Join(", ", $"MappedProteins: {MappedProteins}",
                    $"UnmappedProteins: {UnmappedProteins}",
                    $"TargetProteins: {TargetProteins}",
                    $"MappedPeptides: {MappedPeptides}",
                    $"UnmappedPeptides: {UnmappedPeptides}",
                    $"TargetPeptides: {TargetPeptides}",
                    $"MappedSharedPeptides: {MappedSharedPeptides}",
                    $"TargetSharedPeptides: {TargetSharedPeptides}");
            }
        }
    }
}
