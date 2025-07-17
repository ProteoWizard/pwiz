using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Proteome;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AssociateProteinsParsimonySettingsTest : AbstractFunctionalTest
    {
        protected override bool IsRecordMode => true;

        [TestMethod]
        public void TestAssociateProteinsOptions()
        {
            TestFilesZip = @"TestFunctional\AssociateProteinsParsimonySettingsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var parsimonySettingsList = EnumerateParsimonySettings().ToList();
            MappingResults[] expectedValues;
            string expectedValuesPath = Path.Combine(ExtensionTestContext.GetProjectDirectory(
                @"TestFunctional\AssociateProteinsParsimonySettingsTest.data"), "ExpectedValues.json");
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
                WaitForCondition(() => associateProteinsDlg.IsComplete);
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
        }

        public IEnumerable<ProteinAssociation.ParsimonySettings> EnumerateParsimonySettings()
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
