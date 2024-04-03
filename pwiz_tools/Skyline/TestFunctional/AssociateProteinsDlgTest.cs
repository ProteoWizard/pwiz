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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    
    [TestClass]
    public class AssociateProteinsDlgTest : AbstractFunctionalTest
    {
        private enum ImportType { FASTA, BGPROTEOME, OVERRIDE }
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
            TestInvalidFasta();
            TestUseBackgroundProteome();
            TestFastaOverride();
            TestParsimonyOptions();

            Assert.IsFalse(IsRecordMode);
        }

        public bool IsRecordMode => false;

        private void TestInvalidFasta()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AssociateProteinsTest.sky")));

            AssociateProteinsDlg associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);

            string invalidFastaFilepath = TestFilesDir.GetTestPath("invalidFasta.fasta");
            // ReSharper disable LocalizableElement
            File.WriteAllLines(invalidFastaFilepath, new[]
            {
                ">FOOBAR\tThe first header line",
                "ELVISLIVES",
                ">BAZ|Another header. Where did it go wrong?",
                "PEPTID\u0002EK"
            });
            // ReSharper restore LocalizableElement
            var errorDlg = ShowDialog<MessageDlg>(() => associateProteinsDlg.FastaFileName = invalidFastaFilepath);
            AssertEx.Contains(errorDlg.Message, Resources.AssociateProteinsDlg_UseFastaFile_An_error_occurred_during_protein_association_, "\x02");
            OkDialog(errorDlg, errorDlg.OkDialog);
            OkDialog(associateProteinsDlg, associateProteinsDlg.CancelDialog);
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
        /// Test that setting the FASTA path programatically can override the user's ability to control the protein source
        /// </summary>
        private void TestFastaOverride()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AssociateProteinsTest.sky")));
            TestDialog(ImportType.OVERRIDE);

            // test again with existing associations
            TestDialog(ImportType.OVERRIDE, SkylineWindow.Document.PeptideCount - 4);
        }

        /// <summary>
        /// tests the form
        /// makes sure correct number of matches were found
        /// unchecks all boxes to make sure apply button disables
        /// </summary>
        private void TestDialog(ImportType type, int? initialPeptideCount = null)
        {
            initialPeptideCount = initialPeptideCount ?? SkylineWindow.Document.PeptideCount;
            AssociateProteinsDlg associateProteinsDlg;
            if (type == ImportType.FASTA)
            {
                associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
                if (Settings.Default.LastProteinAssociationFastaFilepath.IsNullOrEmpty())
                    RunUI(() => associateProteinsDlg.FastaFileName = _fastaFile);
            }
            else if (type == ImportType.OVERRIDE)
            {
                associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(() =>
                {
                    using (var dlg = new AssociateProteinsDlg(SkylineWindow.Document, _fastaFile, IrtStandard.EMPTY, null, 0))
                    {
                        if (dlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                            SkylineWindow.ModifyDocument(Resources.AssociateProteinsDlg_ApplyChanges_Associated_proteins,
                                current => dlg.DocumentFinal, dlg.FormSettings.EntryCreator.Create);
                    }
                });
            }
            else
            {
                associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
                RunUI(associateProteinsDlg.UseBackgroundProteome);
            }

            //PauseTest();
            OkAssociateProteinsDialog(associateProteinsDlg);

            //IsPauseForAuditLog = true;
            //PauseForAuditLog();

            List<PeptideGroupDocNode> proteins = new List<PeptideGroupDocNode>();
            List<PeptideGroupDocNode> peptideLists = new List<PeptideGroupDocNode>();
            List<PeptideGroupDocNode> nonProteins = new List<PeptideGroupDocNode>();
            RunUI(() => {
                foreach (var docNode in SkylineWindow.Document.MoleculeGroups)
                {
                    if (docNode.IsProtein)
                        proteins.Add(docNode);
                    else if (docNode.IsProteomic && docNode.IsPeptideList)
                        peptideLists.Add(docNode);
                    else
                        nonProteins.Add(docNode);
                }
            });
            Assert.AreEqual(5, proteins.Count);
            Assert.AreEqual(2, peptideLists.Count);
            Assert.AreEqual(1, nonProteins.Count);
            // +4 because peptides that associate with two proteins get duplicated and there are 4 in this test data file
            Assert.AreEqual(initialPeptideCount + 4, SkylineWindow.Document.PeptideCount);
        }

        /// <summary>
        /// Special function for calling <see cref="AssociateProteinsDlg.OkDialog"/> because
        /// it does background processing before enabling the OK button.
        /// </summary>
        private void OkAssociateProteinsDialog(AssociateProteinsDlg dlg)
        {
            WaitForConditionUI(() => dlg.IsOkEnabled);
            OkDialog(dlg, dlg.OkDialog);
        }

        private class ParsimonyTestCase
        {
            public string[] Proteins;
            public string[] ProteinDescriptions;
            public string[] Peptides;
            public int ExpectedPeptidesMapped, ExpectedPeptidesUnmapped;
            public int ExpectedProteinsMapped, ExpectedProteinsUnmapped;
            public OptionsAndResult[] OptionsAndResults;

            public class OptionsAndResult
            {
                public bool GroupProteins;
                public bool GeneLevelParsimony;
                public bool FindMinimalProteinList;
                public ProteinAssociation.SharedPeptides SharedPeptides;
                public int MinPeptidesPerProtein = 1;

                public int ExpectedFinalPeptides, ExpectedFinalProteins;
                public int ExpectedMappedSharedPeptides, ExpectedFinalSharedPeptides;

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
                Proteins = new[]
                {
                    // two homolog proteins from different genes
                    "SAMESAME",
                    "SAMESAME",

                    // two partial homolog proteins from different genes, each with a distinct peptide
                    "AAAACCCC",
                    "AAADCCCD",

                    // two partial homolog proteins from different genes, no distinct peptide to distinguish them
                    "DDDDEEEE",
                    "DDDFEEEF",

                    // isoforms of the same gene, each with a distinct peptide
                    "FFFFGGGGHHHH",
                    "FFFFGGHHHH",

                    // isoforms of the same gene, no distinct peptide to distinguish them
                    "IIIIKKKK",
                    "IIIGKKKG",

                    // isoforms of the same gene, some with distinct peptides and some without
                    "LLLLNNNN",
                    "LLLGNNNG",
                    "LLGGNNNG",
                },
                ProteinDescriptions = new []
                {
                    "GN=Same",
                    "GN=Same",

                    "GN=PartialHomologsDistinctA",
                    "GN=PartialHomologsDistinctB",

                    "GN=PartialHomologsNonDistinctA",
                    "GN=PartialHomologsNonDistinctB",

                    "GN=IsoformsDistinct",
                    "GN=IsoformsDistinct",

                    "GN=IsoformsNonDistinct",
                    "GN=IsoformsNonDistinct",

                    "accession:ISD1 GN=IsoformsSomeDistinct",
                    "accession:ISD2 GN=IsoformsSomeDistinct",
                    "accession:ISD3_NonDistinct GN=IsoformsSomeDistinct",
                },
                Peptides = new[]
                {
                    "SAM", "SAMES", "AMES", // M will be generated with and without +16

                    "AAA", "CCC",
                    "AACC", "AAACCC",
                    "AAD", "CCCD",

                    "DD", "EE", "DDD", "EEE",

                    "FFFFGG", "FFGG", "GGHHHH",
                    "FGGGGH", "GGGGH",
                    "FGGH", "FGGHHHH",

                    "II", "KK", "III", "KKK",

                    "NNN", // all 3
                    "LLLL", // only ISD1
                    "LLLG", // only ISD2
                    "GNNNG", // ISD2 and ISD3
                },
                ExpectedPeptidesMapped = 28,
                ExpectedPeptidesUnmapped = 0,
                ExpectedProteinsMapped = 13,
                ExpectedProteinsUnmapped = 0,
                OptionsAndResults = new []
                {
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GeneLevelParsimony = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.Removed,
                        ExpectedFinalPeptides = 29,
                        ExpectedFinalProteins = 7,
                        ExpectedMappedSharedPeptides = 4,
                        ExpectedFinalSharedPeptides = 0,
                    },
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.Removed,
                        ExpectedFinalPeptides = 24,
                        ExpectedFinalProteins = 9,
                        ExpectedMappedSharedPeptides = 15,
                        ExpectedFinalSharedPeptides = 0,
                    },
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GeneLevelParsimony = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein,
                        ExpectedFinalPeptides = 33,
                        ExpectedFinalProteins = 7,
                        ExpectedMappedSharedPeptides = 4,
                        ExpectedFinalSharedPeptides = 4,
                    },
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein,
                        ExpectedFinalPeptides = 36,
                        ExpectedFinalProteins = 9,
                        ExpectedMappedSharedPeptides = 15,
                        ExpectedFinalSharedPeptides = 10,
                    },
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GeneLevelParsimony = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 33,
                        ExpectedFinalProteins = 7,
                        ExpectedMappedSharedPeptides = 4,
                        ExpectedFinalSharedPeptides = 4,
                    },
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 39,
                        ExpectedFinalProteins = 10,
                        ExpectedMappedSharedPeptides = 15,
                        ExpectedFinalSharedPeptides = 15,
                    },
                }
            },
            new ParsimonyTestCase
            {
                Proteins = new[]
                {
                    // histone isoforms
                    "KSAPATGGVKKPHRYRPGTVALREIRRYQKSTELLIRKLPFQRLMREIAQDFKTDLRFQSSAVMALQEACE",
                    "KSAPATGGVKKPHRYRPGTVALREIRRYQKSTELLIRKLPFQRLVREIAQDFKTDLRFQSSAVMALQEACE",
                    "KSAPATGGVKKPHRYRPGTVALREIRRYQKSTELLIRKLPFQRLVREIAQDFKTDLRFQSSAVMALQEASE"
                },
                ProteinDescriptions = new []
                {
                    "accession:Q16695 GN=HIST3H3",
                    "accession:P68431 GN=HIST1H3A",
                    "accession:Q71DI3 GN=HIST2H3A",
                },
                Peptides = new[]
                {
                    "SAPATGGVK",
                    "YRPGTVALR",
                },
                ExpectedPeptidesMapped = 2,
                ExpectedPeptidesUnmapped = 0,
                ExpectedProteinsMapped = 3,
                ExpectedProteinsUnmapped = 0,
                OptionsAndResults = new []
                {
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GeneLevelParsimony = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 2,
                        ExpectedFinalProteins = 1,
                        ExpectedMappedSharedPeptides = 0,
                        ExpectedFinalSharedPeptides = 0,
                    },
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 2,
                        ExpectedFinalProteins = 1,
                        ExpectedMappedSharedPeptides = 0,
                        ExpectedFinalSharedPeptides = 0,
                    },
                }
            },
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
                        ExpectedMappedSharedPeptides = 8,
                        ExpectedFinalSharedPeptides = 0,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = false,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein,
                        ExpectedFinalPeptides = 7,
                        ExpectedFinalProteins = 3,
                        ExpectedMappedSharedPeptides = 8,
                        ExpectedFinalSharedPeptides = 4,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = false,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.Removed,
                        ExpectedFinalPeptides = 1, // only AAAK is unique
                        ExpectedFinalProteins = 1,
                        ExpectedMappedSharedPeptides = 8,
                        ExpectedFinalSharedPeptides = 0,
                    },
                    
                    // DuplicatedBetweenProteins should be last option so that all peptides are kept in order to test a second round of protein association
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = false,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 9,
                        ExpectedFinalProteins = 4,
                        ExpectedMappedSharedPeptides = 8,
                        ExpectedFinalSharedPeptides = 8,
                    },
                }
            },

            new ParsimonyTestCase
            {
                Proteins = new[] {"AKAAK", "AKAAKAAAK", "ARAAR", "ARAARAAAR", "ARVK", "ARVKR",  "FFFFGGGGHH", "FFGG", "FFGGHH", "FFFGGGHHH", "FGGHHII", "HHHIIII", "ELVISWASHERE" },
                ProteinDescriptions = new [] {"GN=AAK", "GN=AAK", "GN=Pirate", "GN=Pirate", "GN=Aardvark", "GN=Aardvark", "GN=FGH", "GN=FG", "GN=FGH", "GN=FGH", "GN=FGHI", "GN=HI", "RockandrollNotAGene"},
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
                        ExpectedMappedSharedPeptides = 17,
                        ExpectedFinalSharedPeptides = 0,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        GeneLevelParsimony = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToFirstProtein,
                        ExpectedFinalPeptides = 14,
                        ExpectedFinalProteins = 6,
                        ExpectedMappedSharedPeptides = 11,
                        ExpectedFinalSharedPeptides = 0,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein,
                        ExpectedFinalPeptides = 15,
                        ExpectedFinalProteins = 6,
                        ExpectedMappedSharedPeptides = 17,
                        ExpectedFinalSharedPeptides = 2,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        GeneLevelParsimony = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein,
                        ExpectedFinalPeptides = 15,
                        ExpectedFinalProteins = 6,
                        ExpectedMappedSharedPeptides = 11,
                        ExpectedFinalSharedPeptides = 2,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.Removed,
                        ExpectedFinalPeptides = 7,
                        ExpectedFinalProteins = 5,
                        ExpectedMappedSharedPeptides = 17,
                        ExpectedFinalSharedPeptides = 0,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        GeneLevelParsimony = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.Removed,
                        ExpectedFinalPeptides = 9,
                        ExpectedFinalProteins = 5,
                        ExpectedMappedSharedPeptides = 11,
                        ExpectedFinalSharedPeptides = 0,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = false,
                        RemoveSubsetProteins = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 19,
                        ExpectedFinalProteins = 6,
                        ExpectedMappedSharedPeptides = 17,
                        ExpectedFinalSharedPeptides = 10,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        GeneLevelParsimony = true,
                        FindMinimalProteinList = false,
                        RemoveSubsetProteins = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 19,
                        ExpectedFinalProteins = 6,
                        ExpectedMappedSharedPeptides = 11,
                        ExpectedFinalSharedPeptides = 10,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 24,
                        ExpectedFinalProteins = 9,
                        ExpectedMappedSharedPeptides = 17,
                        ExpectedFinalSharedPeptides = 17,
                    },
                    
                    // DuplicatedBetweenProteins should be last option so that all peptides are kept in order to test a second round of protein association
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        GeneLevelParsimony = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 20,
                        ExpectedFinalProteins = 7,
                        ExpectedMappedSharedPeptides = 11,
                        ExpectedFinalSharedPeptides = 11,
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
                        ExpectedMappedSharedPeptides = 16,
                        ExpectedFinalSharedPeptides = 0,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein,
                        ExpectedFinalPeptides = 11,
                        ExpectedFinalProteins = 3,
                        ExpectedMappedSharedPeptides = 16,
                        ExpectedFinalSharedPeptides = 4,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = false,
                        SharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein,
                        ExpectedFinalPeptides = 11,
                        ExpectedFinalProteins = 3,
                        ExpectedMappedSharedPeptides = 16,
                        ExpectedFinalSharedPeptides = 4,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.Removed,
                        ExpectedFinalPeptides = 2, // only AMAK is unique
                        ExpectedFinalProteins = 1,
                        ExpectedMappedSharedPeptides = 16,
                        ExpectedFinalSharedPeptides = 0,
                    },

                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        MinPeptidesPerProtein = 4,
                        ExpectedFinalPeptides = 5, // only AMAK is unique
                        ExpectedFinalProteins = 1,
                        ExpectedMappedSharedPeptides = 16,
                        ExpectedFinalSharedPeptides = 0,
                    },

                    // DuplicatedBetweenProteins should be last option so that all peptides are kept in order to test a second round of protein association
                    new ParsimonyTestCase.OptionsAndResult
                    {
                        GroupProteins = true,
                        FindMinimalProteinList = true,
                        SharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins,
                        ExpectedFinalPeptides = 11,
                        ExpectedFinalProteins = 3,
                        ExpectedMappedSharedPeptides = 16,
                        ExpectedFinalSharedPeptides = 4,
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
                    Assume.IsTrue(testCase.ProteinDescriptions.IsNullOrEmpty() || testCase.Proteins.Length == testCase.ProteinDescriptions.Length);
                    if (testCase.ProteinDescriptions != null && testCase.ProteinDescriptions.Length > 0)
                    {
                        for (int j = 0; j < testCase.Proteins.Length; ++j)
                            fastaFile.WriteLine($">Protein{j + 1} {testCase.ProteinDescriptions[j]}{Environment.NewLine}{testCase.Proteins[j]}");
                    }
                    else
                    {
                        for (int j = 0; j < testCase.Proteins.Length; ++j)
                            fastaFile.WriteLine($">Protein{j + 1}{Environment.NewLine}{testCase.Proteins[j]}");
                    }
                }

                modificationMatcher.CreateMatches(srmSettings, testCase.Peptides, Settings.Default.StaticModList, Settings.Default.HeavyModList);

                RunUI(() =>
                {
                    SkylineWindow.NewDocument(true);
                    Settings.Default.Reset();
                    var peptideNodes = new List<PeptideDocNode>();
                    foreach (var peptide in testCase.Peptides)
                        peptideNodes.Add(modificationMatcher.GetModifiedNode(peptide));
                    
                    var transSettings = srmSettings.TransitionSettings;
                    transSettings = transSettings.ChangeFullScan(
                        transSettings.FullScan.ChangePrecursorIsotopes(FullScanPrecursorIsotopes.Count, 3, null)
                            .ChangeAcquisitionMethod(FullScanAcquisitionMethod.DDA, null));
                    srmSettings = srmSettings.ChangePeptideSettings(srmSettings.PeptideSettings.ChangeFilter(
                                new PeptideFilter(0, 2, 20, new List<PeptideExcludeRegex>(), true,
                                    PeptideFilter.PeptideUniquenessConstraint.none))
                            .ChangeEnzyme(new Enzyme("non-specific", "ACDEFGHIKLMNPQRSTUVWY", ""))
                            .ChangeDigestSettings(new DigestSettings(9, false)))
                        .ChangeTransitionSettings(transSettings);

                    var peptideList = new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY, "Peptides", string.Empty, peptideNodes.ToArray());
                    var peptideList2 = new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY, "DuplicatePeptideNodes", string.Empty, peptideNodes.ToArray());
                    var peptideList3 = new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY, "EmptyPeptideList", string.Empty, Array.Empty<PeptideDocNode>());
                    SkylineWindow.ModifyDocument("Set peptides",
                        doc => (SkylineWindow.Document.ChangeChildren(new DocNode[] { peptideList, peptideList2, peptideList3 }) as SrmDocument)
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
                            dlg.GeneLevelParsimony = optionsAndResult.GeneLevelParsimony;
                            dlg.FindMinimalProteinList = optionsAndResult.FindMinimalProteinList;
                            dlg.RemoveSubsetProteins = optionsAndResult.RemoveSubsetProteins;
                            dlg.SelectedSharedPeptides = optionsAndResult.SharedPeptides;
                            dlg.MinPeptidesPerProtein = optionsAndResult.MinPeptidesPerProtein;
                        });

                        RunUI(() =>
                        {
                            if (IsRecordMode)
                            {
                                Console.WriteLine();
                                Console.WriteLine(string.Join(", ", testCase.Proteins));
                                Console.WriteLine(string.Join(", ", testCase.ProteinDescriptions ?? Array.Empty<string>()));
                                Console.WriteLine(string.Join(", ", testCase.Peptides));
                                Console.WriteLine();

                                Settings.Default.ShowPeptidesDisplayMode = optionsAndResult.GeneLevelParsimony ? "ByGene" : "ByName";

                                // print mapping of peptide group nodes to peptide nodes
                                foreach (var pg in dlg.DocumentFinal.PeptideGroups)
                                {
                                    var webSearchCompleted = pg.ProteinMetadata.SetWebSearchCompleted();
                                    var peptideGroupDocNode = pg.ChangeProteinMetadata(webSearchCompleted);
                                    var text = ProteinMetadataManager.ProteinModalDisplayText(peptideGroupDocNode);
                                    Console.WriteLine($@"{text} -> {string.Join(", ", pg.Peptides.Select(p => p.SourceTextId))}");
                                }
                                Console.WriteLine();
                                if (optionsAndResult.GroupProteins) Console.WriteLine(@"GroupProteins = true,");
                                if (optionsAndResult.GeneLevelParsimony) Console.WriteLine(@"GeneLevelParsimony = true,");
                                if (optionsAndResult.FindMinimalProteinList) Console.WriteLine(@"FindMinimalProteinList = true,");
                                if (optionsAndResult.RemoveSubsetProteins) Console.WriteLine(@"RemoveSubsetProteins = true,");
                                Console.WriteLine($@"SharedPeptides = ProteinAssociation.SharedPeptides.{Enum.GetName(typeof(ProteinAssociation.SharedPeptides), optionsAndResult.SharedPeptides)},");
                                Console.WriteLine($@"ExpectedFinalPeptides = {dlg.FinalResults.FinalPeptideCount},");
                                Console.WriteLine($@"ExpectedFinalProteins = {dlg.FinalResults.FinalProteinCount},");
                                Console.WriteLine($@"ExpectedMappedSharedPeptides = {dlg.FinalResults.TotalSharedPeptideCount},");
                                Console.WriteLine($@"ExpectedFinalSharedPeptides = {dlg.FinalResults.FinalSharedPeptideCount},");
                            }
                            else
                            {
                                Assert.AreEqual(optionsAndResult.ExpectedFinalProteins, dlg.FinalResults.FinalProteinCount, $"Test case {i + 1}.{j + 1} FinalProteinCount");
                                Assert.AreEqual(optionsAndResult.ExpectedFinalPeptides, dlg.FinalResults.FinalPeptideCount, $"Test case {i + 1}.{j + 1} FinalPeptideCount");
                                Assert.AreEqual(optionsAndResult.ExpectedMappedSharedPeptides, dlg.FinalResults.TotalSharedPeptideCount, $"Test case {i + 1}.{j + 1} TotalSharedPeptideCount");
                                Assert.AreEqual(optionsAndResult.ExpectedFinalSharedPeptides, dlg.FinalResults.FinalSharedPeptideCount, $"Test case {i + 1}.{j + 1} FinalSharedPeptideCount");

                                Assert.AreEqual(testCase.ExpectedPeptidesMapped, dlg.FinalResults.PeptidesMapped, $"Test case {i + 1}.{j + 1} PeptidesMapped");
                                Assert.AreEqual(testCase.ExpectedPeptidesUnmapped, dlg.FinalResults.PeptidesUnmapped, $"Test case {i + 1}.{j + 1} PeptidesUnmapped");
                                Assert.AreEqual(testCase.ExpectedProteinsMapped, dlg.FinalResults.ProteinsMapped, $"Test case {i + 1}.{j + 1} ProteinsMapped");
                                Assert.AreEqual(testCase.ExpectedProteinsUnmapped, dlg.FinalResults.ProteinsUnmapped, $"Test case {i + 1}.{j + 1} ProteinsUnmapped");
                            }
                        });
                    }
                    OkAssociateProteinsDialog(dlg);
                }

                // test all cases again after an association has already been applied (all peptides should have been kept so the results should be the same)
                if (!IsRecordMode)
                { 
                    var dlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
                    RunUI(() => { dlg.FastaFileName = fastaFilePath; });

                    for (int j = 0; j < testCase.OptionsAndResults.Length; ++j)
                    {
                        var optionsAndResult = testCase.OptionsAndResults[j];
                        RunUI(() =>
                        {
                            dlg.GroupProteins = optionsAndResult.GroupProteins;
                            dlg.GeneLevelParsimony = optionsAndResult.GeneLevelParsimony;
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
                            Assert.AreEqual(optionsAndResult.ExpectedMappedSharedPeptides, dlg.FinalResults.TotalSharedPeptideCount, $"Test case {i + 1}.{j + 1} TotalSharedPeptideCount");
                            Assert.AreEqual(optionsAndResult.ExpectedFinalSharedPeptides, dlg.FinalResults.FinalSharedPeptideCount, $"Test case {i + 1}.{j + 1} FinalSharedPeptideCount");

                            Assert.AreEqual(testCase.ExpectedPeptidesMapped, dlg.FinalResults.PeptidesMapped, $"Test case {i + 1}.{j + 1} PeptidesMapped");
                            Assert.AreEqual(testCase.ExpectedPeptidesUnmapped, dlg.FinalResults.PeptidesUnmapped, $"Test case {i + 1}.{j + 1} PeptidesUnmapped");
                            Assert.AreEqual(testCase.ExpectedProteinsMapped, dlg.FinalResults.ProteinsMapped, $"Test case {i + 1}.{j + 1} ProteinsMapped");
                            Assert.AreEqual(testCase.ExpectedProteinsUnmapped, dlg.FinalResults.ProteinsUnmapped, $"Test case {i + 1}.{j + 1} ProteinsUnmapped");
                        });
                    }

                    OkAssociateProteinsDialog(dlg);

                    if (testCase.OptionsAndResults.Last().GroupProteins)
                    {
                        AssertEx.Serializable(SkylineWindow.Document);
                    }

                    int extraUnmappedPeptides = testCase.ExpectedPeptidesUnmapped * 2;
                    int expectedItems = testCase.OptionsAndResults.Last().ExpectedFinalSharedPeptides + extraUnmappedPeptides;
                    if (expectedItems > 0)
                    {
                        var findNodeDlg = ShowDialog<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg);
                        RunUI(() =>
                        {
                            findNodeDlg.AdvancedVisible = true;
                            findNodeDlg.FindOptions = new FindOptions().ChangeCustomFinders(Finders.ListAllFinders().Where(f => f is DuplicatedPeptideFinder));
                            var duplicatePeptideNodes = new List<TreeNodeMS>();
                            for (int k = 0; k < expectedItems; ++k)
                            {
                                findNodeDlg.FindNext();
                                duplicatePeptideNodes.Add(SkylineWindow.SelectedNode);
                            }
                            Assert.AreEqual(expectedItems, duplicatePeptideNodes.Count);

                            findNodeDlg.FindAll();
                        });
                        OkDialog(findNodeDlg, findNodeDlg.Close);

                        var findView = WaitForOpenForm<FindResultsForm>();
                        try
                        {
                            WaitForConditionUI(1000, () => findView.ItemCount == expectedItems);
                        }
                        catch (AssertFailedException)
                        {
                            RunUI(() => Assert.AreEqual(expectedItems, findView.ItemCount));
                        }
                        OkDialog(findView, findView.Close);
                    }

                    RunUI(() =>
                    {
                        Assert.AreEqual(dlg.FinalResults.FinalProteinCount + extraUnmappedPeptides, SkylineWindow.Document.MoleculeGroups.Count(), $"Test case {i + 1} Document.MoleculeGroups.Count");
                        Assert.AreEqual(dlg.FinalResults.FinalPeptideCount + extraUnmappedPeptides, SkylineWindow.Document.PeptideCount, $"Test case {i + 1} Document.PeptideCount");
                    });
                }
            }
        }
    }
}
