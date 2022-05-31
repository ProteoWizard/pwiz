/*
 * Original author: Yuval Boss <yuval .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    
    [TestClass]
    public class AssociateProteinsDlgTest : AbstractFunctionalTest
    {
        private enum ImportType { FASTA, BGPROTEOME }
        private String _fastaFile;

        [TestMethod]
        public void TestAssociateProteins()
        {
            TestFilesZip = @"TestFunctional\AssociateProteinsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            _fastaFile = TestFilesDir.GetTestPath("AssociateProteinMatches.fasta");
            TestUseFasta();
            TestUseBackgroundProteome();
            TestParsimonyOptions();
        }

        /// <summary>
        /// Tests using a FASTA file to match
        /// </summary>
        private void TestUseFasta()

        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AssociateProteinsTest.sky")));
            TestDialog(ImportType.FASTA);

            // test again without needing to set the FASTA
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AssociateProteinsTest.sky")));
            TestDialog(ImportType.FASTA);
        }

        /// <summary>
        /// Test using background-proteome to match
        /// </summary>
        private void TestUseBackgroundProteome()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AssociateProteinsTest.sky")));
            TestDialog(ImportType.BGPROTEOME);
        }

        /// <summary>
        /// tests the form
        /// makes sure correct number of matches were found
        /// unchecks all boxes to make sure apply button disables
        /// </summary>
        private void TestDialog(ImportType type)
        {
            int initialPeptideCount = SkylineWindow.Document.PeptideCount;
            var dlg2 = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            if (type == ImportType.FASTA)
            {
                if (Settings.Default.LastProteinAssociationFastaFilepath.IsNullOrEmpty())
                    RunUI(() => dlg2.FastaFileName = _fastaFile);
            }
            else
            {
                RunUI(dlg2.UseBackgroundProteome);
            }

            //PauseTest();
            OkDialog(dlg2, dlg2.AcceptButton.PerformClick);
            //IsPauseForAuditLog = true;
            //PauseForAuditLog();

            RunUI(() => {
                List<PeptideGroupDocNode> proteins = new List<PeptideGroupDocNode>();
                List<PeptideGroupDocNode> nonProteins = new List<PeptideGroupDocNode>();
                foreach (var docNode in SkylineWindow.Document.MoleculeGroups)
                {
                    if (docNode.IsProtein)
                        proteins.Add(docNode);
                    else
                        nonProteins.Add(docNode);
                }
                Assert.AreEqual(5, proteins.Count);
                Assert.AreEqual(1, nonProteins.Count);
                // +4 because peptides that associate with two proteins get duplicated and there are 4 in this test data file
                Assert.AreEqual(initialPeptideCount + 4, SkylineWindow.Document.PeptideCount);
            });
        }


        private class ParsimonyTestCase
        {
            public string[] Proteins;
            public string[] Peptides;
            public int ExpectedPeptidesMapped, ExpectedPeptidesUnmapped;
            public int ExpectedProteinsMapped, ExpectedProteinsUnmapped;
            public OptionsAndResult[] OptionsAndResults;

            public class OptionsAndResult
            {
                public bool GroupProteins;
                public bool FindMinimalProteinList;
                public ProteinAssociation.SharedPeptides SharedPeptides;
                public int MinPeptidesPerProtein = 1;

                public int ExpectedFinalPeptides, ExpectedFinalProteins;

                private bool? _removeSubsetProteins;
                public bool RemoveSubsetProteins
                {
                    get => _removeSubsetProteins ?? FindMinimalProteinList;
                    set => _removeSubsetProteins = value;
                }
            }
        }

        private static ParsimonyTestCase[] _parsimonyTestCases = new[]
        {
            new ParsimonyTestCase
            {
                Proteins = new[] {"AKAAK", "AKAAKAAAK", "ARAAR", "ARAARAAAR", "ELVISWASHERE" },
                Peptides = new[] {"AK", "AAK", "AAAK", "AR", "AAR", "PEPTIDE" },
                ExpectedPeptidesMapped = 5,
                ExpectedPeptidesUnmapped = 1,
                ExpectedProteinsMapped = 4,
                ExpectedProteinsUnmapped = 1,
                OptionsAndResults = new []
                {
                    // No grouping, keep all proteins
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = false,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToFirstProtein,
                        ExpectedFinalPeptides = 5,
                        ExpectedFinalProteins = 3, // empty protein removed without an option?
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = false,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein,
                        ExpectedFinalPeptides = 7,
                        ExpectedFinalProteins = 3,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = false,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.Removed,
                        ExpectedFinalPeptides = 1, // only AAAK is unique
                        ExpectedFinalProteins = 1,
                    },
                    
                    // DuplicatedBetweenProteins should be last option so that all peptides are kept in order to test a second round of protein association
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = false,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 9,
                        ExpectedFinalProteins = 4,
                    },
                }
            },

            new ParsimonyTestCase
            {
                Proteins = new[] {"AKAAK", "AKAAKAAAK", "ARAAR", "ARAARAAAR", "ARVK", "ARVKR",  "FFFFGGGGHH", "FFGG", "FFGGHH", "FFFGGGHHH", "FGGHHII", "HHHIIII", "ELVISWASHERE" },
                Peptides = new[] {"AK", "AAK", "AAAK", "AAR", "AR", "VK", "FFFF", "FG", "GH", "HI", "HII", "HIII", "HIIII", "IIII", "PEPTIDE" },
                ExpectedPeptidesMapped = 14,
                ExpectedPeptidesUnmapped = 1,
                ExpectedProteinsMapped = 12,
                ExpectedProteinsUnmapped = 1,
                OptionsAndResults = new []
                {
                    // Grouping, keep all proteins
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToFirstProtein,
                        ExpectedFinalPeptides = 14,
                        ExpectedFinalProteins = 7,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein,
                        ExpectedFinalPeptides = 15,
                        ExpectedFinalProteins = 6,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.Removed,
                        ExpectedFinalPeptides = 7,
                        ExpectedFinalProteins = 5,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = false,
                        RemoveSubsetProteins = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 19,
                        ExpectedFinalProteins = 6,
                    },
                    
                    // DuplicatedBetweenProteins should be last option so that all peptides are kept in order to test a second round of protein association
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 24,
                        ExpectedFinalProteins = 9,
                    },
                }
            },

            new ParsimonyTestCase
            {
                Proteins = new[] {"AKKAA", "AKAAK", "AKAAKAAAK", "AKAAKAMAK", "ARAMR", "ARAMRAAAR", "ARVK", "AMRVKR", "ELVISWASHERE" },
                Peptides = new[] {"AK", "KAA", "AAK", "AMAK", "AM[16]AK", "AR", "AM[16]R", "AMR", "VK", "PEPTIDE" },
                ExpectedPeptidesMapped = 7,
                ExpectedPeptidesUnmapped = 1,
                ExpectedProteinsMapped = 8,
                ExpectedProteinsUnmapped = 1,
                OptionsAndResults = new []
                {
                    // Grouping, minimal protein list
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToFirstProtein,
                        ExpectedFinalPeptides = 9,
                        ExpectedFinalProteins = 5,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein,
                        ExpectedFinalPeptides = 11,
                        ExpectedFinalProteins = 3,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein,
                        ExpectedFinalPeptides = 11,
                        ExpectedFinalProteins = 3,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.Removed,
                        ExpectedFinalPeptides = 2, // only AMAK is unique
                        ExpectedFinalProteins = 1,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        MinPeptidesPerProtein = 4,
                        ExpectedFinalPeptides = 5, // only AMAK is unique
                        ExpectedFinalProteins = 1,
                    },

                    // DuplicatedBetweenProteins should be last option so that all peptides are kept in order to test a second round of protein association
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 11,
                        ExpectedFinalProteins = 3,
                    },
                }
            }
        };


        private void TestParsimonyOptions()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var staticMods =
                srmSettings.PeptideSettings.Modifications.StaticModifications
                    .Append(UniMod.GetModification("Water Loss (D, E, S, T)", true))
                    .Append(UniMod.GetModification("Oxidation (M)", true).ChangeVariable(true))
                    .ToList();
            srmSettings = srmSettings.ChangePeptideSettings(
                srmSettings.PeptideSettings.ChangeModifications(
                    srmSettings.PeptideSettings.Modifications.ChangeStaticModifications(staticMods)));
            var modificationMatcher = new ModificationMatcher();

            for (int i=0; i < _parsimonyTestCases.Length; ++i)
            {
                var testCase = _parsimonyTestCases[i];
                string fastaFilePath = TestFilesDir.GetTestPath("testProteins.fasta");
                using (var fastaFile = new StreamWriter(fastaFilePath))
                {
                    for (int j = 0; j < testCase.Proteins.Length; ++j)
                        fastaFile.WriteLine($">Protein{j + 1}{Environment.NewLine}{testCase.Proteins[j]}");
                }

                modificationMatcher.CreateMatches(srmSettings, testCase.Peptides, Settings.Default.StaticModList, Settings.Default.HeavyModList);

                RunUI(() =>
                {
                    SkylineWindow.NewDocument(true);
                    var peptideNodes = new List<PeptideDocNode>();
                    foreach (var peptide in testCase.Peptides)
                        peptideNodes.Add(modificationMatcher.GetModifiedNode(peptide));
                    
                    var transSettings = srmSettings.TransitionSettings;
                    transSettings = transSettings.ChangeFullScan(
                        transSettings.FullScan.ChangePrecursorIsotopes(FullScanPrecursorIsotopes.Count, 3, null)
                            .ChangeAcquisitionMethod(FullScanAcquisitionMethod.DDA, null));
                    srmSettings = srmSettings.ChangePeptideSettings(srmSettings.PeptideSettings.ChangeFilter(new PeptideFilter(0, 2, 20,
                            new List<PeptideExcludeRegex>(), true, PeptideFilter.PeptideUniquenessConstraint.none)))
                        .ChangeTransitionSettings(transSettings);

                    var peptideList = new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY, "Peptides", string.Empty, peptideNodes.ToArray());
                    var peptideList2 = new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY, "DuplicatePeptideNodes", string.Empty, peptideNodes.ToArray());
                    SkylineWindow.ModifyDocument("Set peptides",
                        doc => (SkylineWindow.Document.ChangeChildren(new DocNode[] { peptideList, peptideList2 }) as SrmDocument)
                            ?.ChangeSettings(srmSettings));
                });

                // test all cases on newly populated document
                {
                    var dlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
                    RunUI(() => { dlg.FastaFileName = fastaFilePath; });

                    for (int j = 0; j < testCase.OptionsAndResults.Length; ++j)
                    {
                        var optionsAndResult = testCase.OptionsAndResults[j];
                        RunUI(() =>
                        {
                            dlg.GroupProteins = optionsAndResult.GroupProteins;
                            dlg.FindMinimalProteinList = optionsAndResult.FindMinimalProteinList;
                            dlg.RemoveSubsetProteins = optionsAndResult.RemoveSubsetProteins;
                            dlg.SelectedSharedPeptides = optionsAndResult.SharedPeptides;
                            dlg.MinPeptidesPerProtein = optionsAndResult.MinPeptidesPerProtein;
                        });
                        //PauseTest();
                        RunUI(() =>
                        {
                            Assert.AreEqual(optionsAndResult.ExpectedFinalProteins, dlg.FinalResults.FinalProteinCount, $"Test case {i + 1}.{j + 1} FinalProteinCount");
                            Assert.AreEqual(optionsAndResult.ExpectedFinalPeptides, dlg.FinalResults.FinalPeptideCount, $"Test case {i + 1}.{j + 1} FinalPeptideCount");

                            Assert.AreEqual(testCase.ExpectedPeptidesMapped, dlg.FinalResults.PeptidesMapped, $"Test case {i + 1}.{j + 1} PeptidesMapped");
                            Assert.AreEqual(testCase.ExpectedPeptidesUnmapped, dlg.FinalResults.PeptidesUnmapped, $"Test case {i + 1}.{j + 1} PeptidesUnmapped");
                            Assert.AreEqual(testCase.ExpectedProteinsMapped, dlg.FinalResults.ProteinsMapped, $"Test case {i + 1}.{j + 1} ProteinsMapped");
                            Assert.AreEqual(testCase.ExpectedProteinsUnmapped, dlg.FinalResults.ProteinsUnmapped, $"Test case {i + 1}.{j + 1} ProteinsUnmapped");
                        });
                    }
                    OkDialog(dlg, dlg.AcceptButton.PerformClick);
                }

                // test all cases again after an association has already been applied (all peptides should have been kept so the results should be the same)
                { 
                    var dlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
                    RunUI(() => { dlg.FastaFileName = fastaFilePath; });

                    for (int j = 0; j < testCase.OptionsAndResults.Length; ++j)
                    {
                        var optionsAndResult = testCase.OptionsAndResults[j];
                        RunUI(() =>
                        {
                            dlg.GroupProteins = optionsAndResult.GroupProteins;
                            dlg.FindMinimalProteinList = optionsAndResult.FindMinimalProteinList;
                            dlg.RemoveSubsetProteins = optionsAndResult.RemoveSubsetProteins;
                            dlg.SelectedSharedPeptides = optionsAndResult.SharedPeptides;
                            dlg.MinPeptidesPerProtein = optionsAndResult.MinPeptidesPerProtein;
                        });
                        //PauseTest();
                        RunUI(() =>
                        {
                            Assert.AreEqual(optionsAndResult.ExpectedFinalProteins, dlg.FinalResults.FinalProteinCount, $"Test case {i + 1}.{j + 1} FinalProteinCount");
                            Assert.AreEqual(optionsAndResult.ExpectedFinalPeptides, dlg.FinalResults.FinalPeptideCount, $"Test case {i + 1}.{j + 1} FinalPeptideCount");

                            Assert.AreEqual(testCase.ExpectedPeptidesMapped, dlg.FinalResults.PeptidesMapped, $"Test case {i + 1}.{j + 1} PeptidesMapped");
                            Assert.AreEqual(0, dlg.FinalResults.PeptidesUnmapped, $"Test case {i + 1}.{j + 1} PeptidesUnmapped");
                            Assert.AreEqual(testCase.ExpectedProteinsMapped, dlg.FinalResults.ProteinsMapped, $"Test case {i + 1}.{j + 1} ProteinsMapped");
                            Assert.AreEqual(testCase.ExpectedProteinsUnmapped, dlg.FinalResults.ProteinsUnmapped, $"Test case {i + 1}.{j + 1} ProteinsUnmapped");
                        });
                    }

                    OkDialog(dlg, dlg.AcceptButton.PerformClick);

                    RunUI(() =>
                    {
                        Assert.AreEqual(dlg.FinalResults.FinalProteinCount, SkylineWindow.Document.MoleculeGroups.Count(), $"Test case {i + 1} Document.MoleculeGroups.Count");
                        Assert.AreEqual(dlg.FinalResults.FinalPeptideCount, SkylineWindow.Document.PeptideCount, $"Test case {i + 1} Document.PeptideCount");
                    });
                }
            }
        }
    }
}
