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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test the Unique Peptides dialog
    /// </summary>
    [TestClass]
    public class UniquePeptidesDialogTest : AbstractFunctionalTestEx
    {

        private const string TEXT_FASTA_SPROT = // A protein not in the background proteome
            ">sp|Q13790|APOF_HUMAN Apolipoprotein F OS=Homo sapiens GN=APOF PE=1 SV=1\n" +
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

        enum TestType
        {
            selectUnique,
            excludeBackground
        };

        private void scenario(int nodeNum, int expectedMatches, int finalMoleculeGroupCount, int finalMoleculeCount, int finalTransitionCount, TestType testType)
        {
            var rowcount = 0;
            RunUI(() =>
            {
                var node = SkylineWindow.SequenceTree.Nodes[nodeNum]; 
                SkylineWindow.SequenceTree.SelectedNode = node;
                rowcount = node.GetNodeCount(false);
            });
            using (new CheckDocumentState(finalMoleculeGroupCount, finalMoleculeCount, finalMoleculeCount, finalTransitionCount))
            {
                var uniquePeptidesDlg = ShowDialog<UniquePeptidesDlg>(SkylineWindow.ShowUniquePeptidesDlg);
                WaitForConditionUI(() => uniquePeptidesDlg.GetDataGridView().RowCount == rowcount);
                RunUI(() =>
                {
                    Assert.AreEqual(2 + expectedMatches, uniquePeptidesDlg.GetDataGridView().ColumnCount);
                    if (testType == TestType.excludeBackground)
                        uniquePeptidesDlg.ExcludeBackgroundProteome();
                    else
                        uniquePeptidesDlg.SelectUnique();
                });
                OkDialog(uniquePeptidesDlg, uniquePeptidesDlg.OkDialog);
            }
        }

        protected override void DoTest()
        {
            OpenDocument("UniqueTest.sky");
            // Add FASTA sequence that's not in the library
            RunUI(() => SkylineWindow.Paste(TEXT_FASTA_SPROT));

            // Finish digesting
            WaitForCondition(() =>
            {
                var peptideSettings = SkylineWindow.Document.Settings.PeptideSettings;
                var backgroundProteome = peptideSettings.BackgroundProteome;
                return backgroundProteome.HasDigestion(peptideSettings);
            });
            scenario(3, 0, 25, 423, 1576, TestType.selectUnique); // HOP_1 - should leave everything as is, as there is no overlap with anything in the background proteome even though HOP_1 itself is a member
            scenario(24, 0, 25, 423, 1576, TestType.selectUnique); // not in the background proteome, and no overlap, should change nothing 
            scenario(24, 0, 25, 423, 1576, TestType.excludeBackground); // not in the background proteome, and no overlap, should change nothing 
            scenario(1, 2, 25, 405, 1514, TestType.selectUnique); // should leave only two peptides in ATPB_HUMAN
            scenario(17, 14, 25, 392, 1463, TestType.selectUnique); // should completely remove TAU_MOUSE peptides, since it has no unique peptides
            scenario(3, 0, 25, 369, 1388, TestType.excludeBackground); // HOP_1 - should remove all peptides even though there's no overlap, since it's in the background proteome
        }
    }
}