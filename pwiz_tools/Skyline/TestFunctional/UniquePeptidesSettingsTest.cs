/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test the Peptide uniqueness settings logic.
    /// 
    /// Some notes on the protdb contents, provided by Jarret E. :
    /// HOP1 -- this protein has no isoforms, and is only present in yeast.
    /// PLMN -- this protein is only present in human and mouse (it's in the blood), not in yeast.
    /// ATPB -- this protein is present in yeast, mouse, and human, and is highly conserved (i.e. the sequence between the three is very similar)
    /// HPT and HPTR -- HPT (haptoglobin) is a protein that is present in both human and mouse (not yeast).  In humans, there are two isoforms of HPT, in mouse there's only one.  Additionally, HPT is very similar to another protein in human -- HPTR.  Which I also include in the file.  Lots of peptides in common between HPT and HPTR
    /// TAU -- this protein is is seen in human, and mouse.  In both cases, there are a ton of isoforms (9 for human, 6 for mouse).
    /// </summary>
    [TestClass]
    public class UniquePeptidesSettingsTest : AbstractFunctionalTestEx
    {
        
        [TestMethod]
        public void TestUniquePeptidesSettings()
        {
            TestFilesZip = @"TestFunctional\UniquePeptidesDialogTest.zip";
            RunFunctionalTest();
        }
        const int PROTEINS_COUNT = 24;
        const int UNFILTERED_PEPTIDE_COUNT = 414;
        const int UNIQUE_BY_PROTEINS_PEPTIDE_COUNT = 76;
        const int UNIQUE_BY_GENE_PEPTIDE_COUNT = 104;
        const int UNIQUE_BY_SPECIES_PEPTIDE_COUNT = 81;
        const int HOP1_PEPTIDE_COUNT = 23;
        const int ATPB_HUMAN_PEPTIDE_COUNT = 21;
        const int ATPB_MOUSE_PEPTIDE_COUNT = 21;
        const int ATPB_HUMAN_UNIQUE_PEPTIDE_COUNT = 3;
        const int ATPB_MOUSE_UNIQUE_PEPTIDE_COUNT = 3;
        const int TAU_HUMAN_PEPTIDE_COUNT = 29;
        const int TAU_HUMAN_9_UNIQUE_PEPTIDE_COUNT = 2; // ATKQVQRRPPPAGPRSE portion of sequence is unique to this protein

        protected override void DoTest()
        {
            scenario(0, 0, PeptideFilter.PeptideUniquenessConstraint.gene, new Dictionary<string, int>()); // Verify that empty docs don't cause problems

            OpenDocument("UniqueTest.sky"); // Contains all the same proteins that are found in the protDB file

            scenario(UNFILTERED_PEPTIDE_COUNT, PROTEINS_COUNT, PeptideFilter.PeptideUniquenessConstraint.none, // No change from initial state
                new Dictionary<string, int>
                {
                    {"sp|P20050|HOP1_YEAST", HOP1_PEPTIDE_COUNT},
                    {"sp|P06576|ATPB_HUMAN", ATPB_HUMAN_PEPTIDE_COUNT}, // "ATPB -- this protein is present in yeast, mouse, and human, and is highly conserved (i.e. the sequence between the three is very similar)"
                    {"sp|P56480|ATPB_MOUSE", ATPB_MOUSE_PEPTIDE_COUNT}, // "ATPB -- this protein is present in yeast, mouse, and human, and is highly conserved (i.e. the sequence between the three is very similar)"
                    {"sp|P10636|TAU_HUMAN", TAU_HUMAN_PEPTIDE_COUNT}, // "this protein is is seen in human, and mouse.  In both cases, there are a ton of isoforms (9 for human, 6 for mouse)."
                });

            scenario(UNIQUE_BY_PROTEINS_PEPTIDE_COUNT, PROTEINS_COUNT, PeptideFilter.PeptideUniquenessConstraint.protein, 
                new Dictionary<string, int>
                {
                    {"sp|P20050|HOP1_YEAST", HOP1_PEPTIDE_COUNT}, // Unaffected - "HOP1 -- this protein has no isoforms, and is only present in yeast."
                    {"sp|P06576|ATPB_HUMAN", ATPB_HUMAN_UNIQUE_PEPTIDE_COUNT}, // "ATPB -- this protein is present in yeast, mouse, and human, and is highly conserved (i.e. the sequence between the three is very similar)"
                    {"sp|P56480|ATPB_MOUSE", ATPB_MOUSE_UNIQUE_PEPTIDE_COUNT}, // "ATPB -- this protein is present in yeast, mouse, and human, and is highly conserved (i.e. the sequence between the three is very similar)"
                    {"sp|P10636|TAU_HUMAN", 0}, // "this protein is is seen in human, and mouse.  In both cases, there are a ton of isoforms (9 for human, 6 for mouse)."
                });

            scenario(UNIQUE_BY_GENE_PEPTIDE_COUNT, PROTEINS_COUNT, PeptideFilter.PeptideUniquenessConstraint.gene,
                new Dictionary<string, int>
                {
                    {"sp|P20050|HOP1_YEAST", HOP1_PEPTIDE_COUNT}, // Unaffected - "HOP1 -- this protein has no isoforms, and is only present in yeast."
                    {"sp|P06576|ATPB_HUMAN", ATPB_HUMAN_UNIQUE_PEPTIDE_COUNT}, // "ATPB -- this protein is present in yeast, mouse, and human, and is highly conserved (i.e. the sequence between the three is very similar)"
                    {"sp|P10636|TAU_HUMAN", 0}, // Isoforms, so they'll not be unique per gene
                    {"sp|P10636-2|TAU_HUMAN", 0},
                    {"sp|P10636-3|TAU_HUMAN", 0},
                    {"sp|P10636-4|TAU_HUMAN", 0},
                    {"sp|P10636-5|TAU_HUMAN", 0},
                    {"sp|P10636-6|TAU_HUMAN", 0},
                    {"sp|P10636-7|TAU_HUMAN", 0},
                    {"sp|P10636-8|TAU_HUMAN", 0},
                    {"sp|P10636-9|TAU_HUMAN", TAU_HUMAN_9_UNIQUE_PEPTIDE_COUNT}, // ATKQVQRRPPPAGPRSE portion of sequence is unique to this protein
                });

            scenario(UNIQUE_BY_SPECIES_PEPTIDE_COUNT, PROTEINS_COUNT, PeptideFilter.PeptideUniquenessConstraint.species,
                new Dictionary<string, int>
                {
                    {"sp|P20050|HOP1_YEAST", HOP1_PEPTIDE_COUNT}, // Unaffected - "HOP1 -- this protein has no isoforms, and is only present in yeast."
                    {"sp|P06576|ATPB_HUMAN", ATPB_HUMAN_UNIQUE_PEPTIDE_COUNT}, // "ATPB -- this protein is present in yeast, mouse, and human, and is highly conserved (i.e. the sequence between the three is very similar)"
                    {"sp|P10636|TAU_HUMAN", 0}, // "this protein is is seen in human, and mouse.  In both cases, there are a ton of isoforms (9 for human, 6 for mouse)."
                    {"sp|P10636-2|TAU_HUMAN", 1},
                    {"sp|P10636-3|TAU_HUMAN", 0},
                    {"sp|P10636-4|TAU_HUMAN", 0},
                    {"sp|P10636-5|TAU_HUMAN", 0},
                    {"sp|P10636-6|TAU_HUMAN", 1},
                    {"sp|P10636-7|TAU_HUMAN", 0},
                    {"sp|P10636-8|TAU_HUMAN", 0},
                    {"sp|P10636-9|TAU_HUMAN", TAU_HUMAN_9_UNIQUE_PEPTIDE_COUNT}, // ATKQVQRRPPPAGPRSE portion of sequence is unique to this protein
                });

            scenario(UNIQUE_BY_PROTEINS_PEPTIDE_COUNT, PROTEINS_COUNT, PeptideFilter.PeptideUniquenessConstraint.protein,
                new Dictionary<string, int>
                {
                    {"sp|P20050|HOP1_YEAST", HOP1_PEPTIDE_COUNT}, // Unaffected - "HOP1 -- this protein has no isoforms, and is only present in yeast."
                    {"sp|P06576|ATPB_HUMAN", ATPB_HUMAN_UNIQUE_PEPTIDE_COUNT}, // "ATPB -- this protein is present in yeast, mouse, and human, and is highly conserved (i.e. the sequence between the three is very similar)"
                    {"sp|P56480|ATPB_MOUSE", ATPB_MOUSE_UNIQUE_PEPTIDE_COUNT}, // "ATPB -- this protein is present in yeast, mouse, and human, and is highly conserved (i.e. the sequence between the three is very similar)"
                    {"sp|P10636|TAU_HUMAN", 0}, // "this protein is is seen in human, and mouse.  In both cases, there are a ton of isoforms (9 for human, 6 for mouse)."
                    {"sp|P10636-2|TAU_HUMAN", 0},
                    {"sp|P10636-3|TAU_HUMAN", 0},
                    {"sp|P10636-4|TAU_HUMAN", 0},
                    {"sp|P10636-5|TAU_HUMAN", 0},
                    {"sp|P10636-6|TAU_HUMAN", 0},
                    {"sp|P10636-7|TAU_HUMAN", 0},
                    {"sp|P10636-8|TAU_HUMAN", 0},
                    {"sp|P10636-9|TAU_HUMAN", 2}, // ATKQVQRRPPPAGPRSE portion of sequence is unique to this protein
                });

            scenario(UNFILTERED_PEPTIDE_COUNT, PROTEINS_COUNT, PeptideFilter.PeptideUniquenessConstraint.none,
                new Dictionary<string, int>
                {
                    {"sp|P20050|HOP1_YEAST", HOP1_PEPTIDE_COUNT},
                    {"sp|P06576|ATPB_HUMAN", ATPB_HUMAN_PEPTIDE_COUNT}, // "ATPB -- this protein is present in yeast, mouse, and human, and is highly conserved (i.e. the sequence between the three is very similar)"
                    {"sp|P10636|TAU_HUMAN", TAU_HUMAN_PEPTIDE_COUNT}, // "this protein is is seen in human, and mouse.  In both cases, there are a ton of isoforms (9 for human, 6 for mouse)."
                });

        }

        public static void scenario(int finalPeptideCount, int proteinsCount, PeptideFilter.PeptideUniquenessConstraint testType,
            Dictionary<string,int> dictProteinPrecursorCounts)
        {
            var doc = SkylineWindow.Document;
            // Open the peptide settings dialog, select uniqueness type, click OK
            var peptideSettingsUI = ShowPeptideSettings();
            bool expectChange = false;
            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Digest;
                expectChange = peptideSettingsUI.ComboPeptideUniquenessConstraintSelected != testType;
                peptideSettingsUI.ComboPeptideUniquenessConstraintSelected = testType;
                peptideSettingsUI.OkDialog();
            });
            if (expectChange)
            {
                WaitForDocumentChange(doc);
            }
            AssertEx.IsDocumentState(SkylineWindow.Document, null, proteinsCount, finalPeptideCount, finalPeptideCount, null);

            // Check selected proteins for proper peptide count
            if (dictProteinPrecursorCounts != null)
            {
                foreach (var proteinPrecursorCount in dictProteinPrecursorCounts)
                {
                    var count = proteinPrecursorCount;
                    foreach (var node in SkylineWindow.Document.PeptideGroups.Where(p => Equals(count.Key, p.Name)))
                    {
                        Assert.AreEqual(count.Value, node.Peptides.Count(), string.Format("unexpected peptide count in filter {0} for protein {1}",testType, count.Key));
                    }
                }
            }
        }

    }
}