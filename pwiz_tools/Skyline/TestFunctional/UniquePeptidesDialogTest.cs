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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Properties;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test the Unique Peptides dialog
    /// </summary>
    [TestClass]
    public class UniquePeptidesDialogTest : AbstractFunctionalTestEx
    {

        private const string TEXT_FASTA_SPROT = // A protein not in the background proteome
            ">sp|Q13790|APOF_HUMAN Apolipoprotein F OS=Homo sapiens GN=APOF PE=1 SV=1\n" + TEXT_FASTA;

        private const string TEXT_FASTA_NONSENSE = // A protein not in the background proteome
            ">nonsense\n" + TEXT_FASTA;

        private const string TEXT_FASTA = // A protein not in the background proteome
            "MIPVELLLCYLLLHPVDATSYGKQTNVLMHFPLSLESQTPSSDPLSCQFLHPKSLPGFSH\n" +
            "MAPLPKFLVSLALRNALEEAGCQADVWALQLQLYRQGGVNATQVLIQHLRGLQKGRSTER\n" +
            "NVSVEALASALQLLAREQQSTGRVGRSLPTEDCENEKEQAVHNVVQLLPGVGTFYNLGTA\n" +
            "LYYATQNCLGKARERGRDGAIDLGYDLLMTMAGMSGGPMGLAISAALKPALRSGVQQLIQ\n" +
            "YYQDQKDANISQPETTKEGLRAISDVSDLEETTTLASFISEVVSSAPYWGWAIIKSYDLD\n" +
            "PGAGSLEI*";

        [TestMethod]
        public void TestUniquePeptidesDialog()
        {
            TestFilesZip = @"TestFunctional\UniquePeptidesDialogTest.zip";
            RunFunctionalTest();
        }

        private const UniquePeptidesDlg.UniquenessType EXCLUDE_BACKGROUND = UniquePeptidesDlg.UniquenessType.protein - 1;

        const int NODE_ATPB_HUMAN = 0;
        const int NODE_ATPB_MOUSE = 1;
        const int NODE_HOP_1 = 3;
        const int NODE_TAU_MOUSE = 17;
        const int NODE_APOF_HUMAN = 24;

        const int INITIAL_MOLECULE_COUNT = 423;

        const int NON_UNIQUE_PEPTIDES_BY_PROTEIN_ATPB_MOUSE = 18;
        const int NON_UNIQUE_PEPTIDES_BY_PROTEIN_TAU_MOUSE = 13;

        const int NON_UNIQUE_PEPTIDES_BY_GENE_ATPB_MOUSE = 3;
        const int NON_UNIQUE_PEPTIDES_BY_GENE_TAU_MOUSE = 12;

        const int NON_UNIQUE_PEPTIDES_BY_SPECIES_ATPB_MOUSE = 18;
        const int NON_UNIQUE_PEPTIDES_BY_SPECIES_TAU_MOUSE = 12;
        const int TOTAL_PEPTIDES_HOP1 = 23;

        private void ResetDocument(bool bogus = false)
        {
            OpenDocument(bogus ? "UniqueTestBogus.sky": "UniqueTest.sky");  // Contains every protein in the protDB file, bogus version has missing metadata in the protdb

            // CONSIDER: Should WaitForDocumentLoaded() also ensure that the background proteome is loaded?
            WaitForBackgroundProteomeLoaderCompleted();

            // Finish digesting and get protein metadata (should not require web access for these well formed fasta headers)
            WaitForConditionUI(() =>
            {
                var peptideSettings = SkylineWindow.DocumentUI.Settings.PeptideSettings;
                var backgroundProteome = peptideSettings.BackgroundProteome;
                return !backgroundProteome.NeedsProteinMetadataSearch && !SkylineWindow.DocumentUI.IsProteinMetadataPending;
            });

            // Add FASTA sequence that's not in the library
            using (new WaitDocumentChange())
//            using (new ImportFastaDocChangeLogger()) // Log any document changes that are not due to Import Fasta
            {
                RunUI(() => SkylineWindow.Paste(bogus ? TEXT_FASTA_NONSENSE : TEXT_FASTA_SPROT));
            }
            WaitForProteinMetadataBackgroundLoaderCompletedUI();
        }


        private void scenario(int nodeNum, int expectedMatches, int expectedMoleculeFilteredCount, UniquePeptidesDlg.UniquenessType testType, bool bogus = false)
        {
            scenario(new [] {nodeNum}, expectedMatches, expectedMoleculeFilteredCount, testType, bogus);
        }

        private void scenario(int[] nodes, int expectedMatches, int expectedMoleculeFilteredCount, UniquePeptidesDlg.UniquenessType testType, bool bogus = false)
        {
            var rowcount = 0;
            RunUI(() =>
            {
                var node = SkylineWindow.SequenceTree.Nodes[nodes[0]];
                SkylineWindow.SequenceTree.SelectedNode = node; // Clears any existing selection
                foreach (var i in nodes)
                {
                    node = SkylineWindow.SequenceTree.Nodes[i];
                    SkylineWindow.SequenceTree.SelectNode((TreeNodeMS) node, true);
                    rowcount += node.GetNodeCount(false);
                }
            });

            var upgradeBackgroundProteome = ShowDialog<AlertDlg>(SkylineWindow.ShowUniquePeptidesDlg);
            var uniquePeptidesDlg = ShowDialog<UniquePeptidesDlg>(upgradeBackgroundProteome.ClickNo);
            WaitForConditionUI(() => uniquePeptidesDlg.GetDataGridView().RowCount == rowcount);
            if (bogus)
            {
                uniquePeptidesDlg.BeginInvoke(new Action(() => uniquePeptidesDlg.SelectUnique(testType)));
                // Expect a warning about missing metadata
                var errorDlg = WaitForOpenForm<MessageDlg>();
                var expectedErr = testType == UniquePeptidesDlg.UniquenessType.gene ?
                    Resources.UniquePeptidesDlg_SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold_Some_background_proteome_proteins_did_not_have_gene_information__this_selection_may_be_suspect_ :
                    Resources.UniquePeptidesDlg_SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold_Some_background_proteome_proteins_did_not_have_species_information__this_selection_may_be_suspect_;
                Assert.IsTrue(errorDlg.Message.Contains(expectedErr));
                RunUI(() => errorDlg.OkDialog());
            }
            else
            {
                RunUI(() =>
                {
                    Assert.AreEqual(2 + expectedMatches, uniquePeptidesDlg.GetDataGridView().ColumnCount);
                    if (testType == EXCLUDE_BACKGROUND)
                        uniquePeptidesDlg.ExcludeBackgroundProteome();
                    else
                        uniquePeptidesDlg.SelectUnique(testType);
                });
            }
            var doc = SkylineWindow.Document;
            OkDialog(uniquePeptidesDlg, uniquePeptidesDlg.OkDialog);
            if (expectedMoleculeFilteredCount > 0)
                doc = WaitForDocumentChange(doc);
            AssertEx.IsDocumentState(doc, null, 25, INITIAL_MOLECULE_COUNT - expectedMoleculeFilteredCount, null, null);
        }

        protected override void DoTest()
        {

            ResetDocument();
            scenario(new [] { NODE_ATPB_MOUSE, NODE_HOP_1, NODE_APOF_HUMAN }, 2, NON_UNIQUE_PEPTIDES_BY_PROTEIN_ATPB_MOUSE, 
                UniquePeptidesDlg.UniquenessType.protein); // Multiple selection ATPB_MOUSE, HOP_1, APOF_HUMAN, only ATPB_MOUSE is affected

            ResetDocument();
            scenario(new[] { NODE_ATPB_MOUSE, NODE_TAU_MOUSE, NODE_APOF_HUMAN }, 16, NON_UNIQUE_PEPTIDES_BY_SPECIES_ATPB_MOUSE + NON_UNIQUE_PEPTIDES_BY_SPECIES_TAU_MOUSE, 
                UniquePeptidesDlg.UniquenessType.species); // Multiple selection ATPB_MOUSE, TAU_MOUSE, APOF_HUMAN - many mouse peptides also appear in yeast proteins in protdb

            ResetDocument();
            scenario(new[] { NODE_ATPB_MOUSE, NODE_HOP_1, NODE_APOF_HUMAN }, 2, NON_UNIQUE_PEPTIDES_BY_GENE_ATPB_MOUSE, 
                UniquePeptidesDlg.UniquenessType.gene); // Multiple selection ATPB_MOUSE, HOP_1, APOF_HUMAN

            ResetDocument();
            scenario(new[] { NODE_ATPB_MOUSE, NODE_HOP_1, NODE_TAU_MOUSE, NODE_APOF_HUMAN }, 16, NON_UNIQUE_PEPTIDES_BY_GENE_ATPB_MOUSE + NON_UNIQUE_PEPTIDES_BY_GENE_TAU_MOUSE, 
                UniquePeptidesDlg.UniquenessType.gene); // Multiple selection ATPB_MOUSE, HOP_1, TAU_MOUSE, APOF_HUMAN

            ResetDocument();
            scenario(new[] { NODE_ATPB_MOUSE, NODE_HOP_1, NODE_TAU_MOUSE, NODE_APOF_HUMAN }, 16, 57, 
                EXCLUDE_BACKGROUND); // Multiple selection ATPB_MOUSE, HOP_1, TAU_MOUSE, APOF_HUMAN, only APOF_HUMAN is not in background proteome

            ResetDocument();
            scenario(NODE_HOP_1, 0, 0, 
                UniquePeptidesDlg.UniquenessType.protein); // HOP_1 - should leave everything as is, as there is no overlap with anything in the background proteome even though HOP_1 itself is a member
            scenario(NODE_APOF_HUMAN, 0, 0, 
                UniquePeptidesDlg.UniquenessType.protein); // APOF_HUMAN - not in the background proteome, and no overlap, should change nothing 
            scenario(NODE_APOF_HUMAN, 0, 0,
                EXCLUDE_BACKGROUND); // APOF_HUMAN - not in the background proteome, and no overlap, should change nothing 
            scenario(NODE_ATPB_MOUSE,  2,  NON_UNIQUE_PEPTIDES_BY_PROTEIN_ATPB_MOUSE, 
                UniquePeptidesDlg.UniquenessType.protein); // should leave only two peptides in ATPB_MOUSE since most appear in other proteins as well
            scenario(NODE_TAU_MOUSE, 14, NON_UNIQUE_PEPTIDES_BY_PROTEIN_ATPB_MOUSE + NON_UNIQUE_PEPTIDES_BY_PROTEIN_TAU_MOUSE, 
                UniquePeptidesDlg.UniquenessType.protein); // should completely remove TAU_MOUSE peptides, since it has no unique peptides
            scenario(NODE_HOP_1, 0, NON_UNIQUE_PEPTIDES_BY_PROTEIN_ATPB_MOUSE + NON_UNIQUE_PEPTIDES_BY_PROTEIN_TAU_MOUSE + TOTAL_PEPTIDES_HOP1, 
               EXCLUDE_BACKGROUND); // HOP_1 - should remove all peptides even though there's no overlap, since it's in the background proteome

            ResetDocument();
            scenario(NODE_HOP_1, 0, 0, 
                UniquePeptidesDlg.UniquenessType.species); // HOP_1 - should leave everything as is, as there is no overlap with anything in the background proteome even though HOP_1 itself is a member
            scenario(NODE_APOF_HUMAN, 0, 0,
                UniquePeptidesDlg.UniquenessType.species); // APOF_HUMAN - not in the background proteome, and no overlap, should change nothing 
            scenario(NODE_APOF_HUMAN, 0, 0, 
                EXCLUDE_BACKGROUND); // APOF_HUMAN - not in the background proteome, and no overlap, should change nothing 
            scenario(NODE_ATPB_MOUSE, 2, NON_UNIQUE_PEPTIDES_BY_PROTEIN_ATPB_MOUSE, 
                UniquePeptidesDlg.UniquenessType.species); // should leave only two peptides in ATPB_MOUSE, since most are also found in human and yeast
            scenario(NODE_TAU_MOUSE, 14, NON_UNIQUE_PEPTIDES_BY_PROTEIN_ATPB_MOUSE + NON_UNIQUE_PEPTIDES_BY_SPECIES_TAU_MOUSE, 
                UniquePeptidesDlg.UniquenessType.species); // should remove all but one TAU_MOUSE peptides, since most are also found in human
            scenario(NODE_HOP_1, 0, NON_UNIQUE_PEPTIDES_BY_PROTEIN_ATPB_MOUSE + NON_UNIQUE_PEPTIDES_BY_SPECIES_TAU_MOUSE + TOTAL_PEPTIDES_HOP1, 
                EXCLUDE_BACKGROUND); // HOP_1 - should remove all peptides even though there's no overlap, since it's in the background proteome

            ResetDocument();
            scenario(NODE_HOP_1, 0, 0, 
                UniquePeptidesDlg.UniquenessType.gene); // HOP_1 - should leave everything as is, as there is no overlap with anything in the background proteome even though HOP_1 itself is a member
            scenario(NODE_APOF_HUMAN, 0, 0, 
                UniquePeptidesDlg.UniquenessType.gene); // APOF_HUMAN - not in the background proteome, and no overlap, should change nothing 
            scenario(NODE_APOF_HUMAN, 0, 0, 
               EXCLUDE_BACKGROUND); // APOF_HUMAN - not in the background proteome, and no overlap, should change nothing 
            scenario(NODE_ATPB_MOUSE, 2, NON_UNIQUE_PEPTIDES_BY_GENE_ATPB_MOUSE, 
                UniquePeptidesDlg.UniquenessType.gene);  // ATPB_MOUSE, drop the three peptides associated with genes ATPB5 and ATP2
            scenario(NODE_TAU_MOUSE, 14, NON_UNIQUE_PEPTIDES_BY_GENE_ATPB_MOUSE + NON_UNIQUE_PEPTIDES_BY_GENE_TAU_MOUSE, 
                UniquePeptidesDlg.UniquenessType.gene); // all but two TAU_MOUSE peptides are associated with other genes in addition to MAPT
            scenario(NODE_HOP_1, 0, NON_UNIQUE_PEPTIDES_BY_GENE_ATPB_MOUSE + NON_UNIQUE_PEPTIDES_BY_GENE_TAU_MOUSE + TOTAL_PEPTIDES_HOP1, 
                EXCLUDE_BACKGROUND); // HOP_1 - should remove all peptides even though there's no overlap, since it's in the background proteome

            // Test our handling of data without gene or species info - expect to see a warning dialog
            ResetDocument(bogus:true); // Load the bogus data
            scenario(NODE_ATPB_MOUSE, 2, 0, UniquePeptidesDlg.UniquenessType.gene, true); // Can't filter by gene when there's no metadata
            scenario(NODE_ATPB_HUMAN, 2, 0, UniquePeptidesDlg.UniquenessType.species, true); // Can't filter by species when there's no metadata

        }

    }
}