/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Proteome;
using pwiz.SkylineTestUtil;
using Resources = pwiz.Skyline.Properties.Resources;

namespace TestPerf
{
    [TestClass]
    public class PerfAssociateProteinsHugeTest : AbstractFunctionalTest
    {
        private enum ImportType { FASTA, BGPROTEOME }
        private String _fastaFile;

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)] // Doesn't do well on slower VMs
        public void TestHugeAssociateProteins()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfAssociateProteinsHugeTest.zip");
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            _fastaFile = TestFilesDir.GetTestPath("2014_01_HUMAN.fasta");
            TestUseFasta();
            //TestUseBackgroundProteome() ;
        }

        /// <summary>
        /// Tests using a FASTA file to match
        /// </summary>
        private void TestUseFasta()

        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PerfAssociateProteinsHugeTest.sky")));
            //PauseTest();
            TestDialog(ImportType.FASTA);
        }

        /// <summary>
        /// Test using background-proteome to match
        /// </summary>
        private void TestUseBackgroundProteome()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AssociateProteinsTest-NoBg.sky")));
            var associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            RunDlg<MessageDlg>(associateProteinsDlg.UseBackgroundProteome, messageDlg =>
            {
                AssertEx.AreComparableStrings(Resources.AssociateProteinsDlg_UseBackgroundProteome_No_background_proteome_defined, messageDlg.Message);
                messageDlg.OkDialog();
            });
            OkDialog(associateProteinsDlg, associateProteinsDlg.CancelDialog);
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AssociateProteinsTest.sky")));
            var proteinsDlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            RunUI(proteinsDlg.UseBackgroundProteome);
            TestDialog(ImportType.BGPROTEOME);
        }


        /// <summary>
        /// Tests the form
        /// - Makes sure correct number of matches were found
        /// </summary>
        private void TestDialog(ImportType type)
        {
            var proteinsDlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            if (type == ImportType.FASTA)
            {
                RunUI(() => proteinsDlg.FastaFileName = _fastaFile);
            }
            else
            {
                RunUI(proteinsDlg.UseBackgroundProteome);
            }

            WaitForCondition(() => !proteinsDlg.IsBusy);

            //PauseTest();
            RunUI(() =>
            {
                Assert.AreEqual(425484, proteinsDlg.FinalResults.PeptidesMapped);
                Assert.AreEqual(0, proteinsDlg.FinalResults.PeptidesUnmapped);
                Assert.AreEqual(84198, proteinsDlg.FinalResults.ProteinsMapped);
                Assert.AreEqual(4281, proteinsDlg.FinalResults.ProteinsUnmapped);

                proteinsDlg.GroupProteins = true;
                proteinsDlg.FindMinimalProteinList = false;
                proteinsDlg.SelectedSharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein;
                //Console.WriteLine($"KeepAllProteins AssignedToBestProtein {proteinsDlg.FinalResults.FinalProteinCount} {proteinsDlg.FinalResults.FinalPeptideCount}");
                Assert.AreEqual(471387, proteinsDlg.FinalResults.FinalPeptideCount);
                Assert.AreEqual(48572, proteinsDlg.FinalResults.FinalProteinCount);

                proteinsDlg.FindMinimalProteinList = true;
                proteinsDlg.SelectedSharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins;
                //Console.WriteLine($"MinimalProteinList DuplicatedBetweenProteins {proteinsDlg.FinalResults.FinalProteinCount} {proteinsDlg.FinalResults.FinalPeptideCount}");
                Assert.AreEqual(744438, proteinsDlg.FinalResults.FinalPeptideCount);
                Assert.AreEqual(49193, proteinsDlg.FinalResults.FinalProteinCount);

                proteinsDlg.FindMinimalProteinList = false;
                proteinsDlg.SelectedSharedPeptides = ProteinAssociation.SharedPeptides.Removed;
                //Console.WriteLine($"KeepAllProteins Removed {proteinsDlg.FinalResults.FinalProteinCount} {proteinsDlg.FinalResults.FinalPeptideCount}");
                Assert.AreEqual(167189, proteinsDlg.FinalResults.FinalPeptideCount);
                Assert.AreEqual(41606, proteinsDlg.FinalResults.FinalProteinCount);

                proteinsDlg.MinPeptidesPerProtein = 10;
                //Console.WriteLine($"MinPeptidesPerProtein 10 {proteinsDlg.FinalResults.FinalProteinCount} {proteinsDlg.FinalResults.FinalPeptideCount}");
                Assert.AreEqual(88242, proteinsDlg.FinalResults.FinalPeptideCount);
                Assert.AreEqual(3987, proteinsDlg.FinalResults.FinalProteinCount);
            });
            // PauseTest();
            using (new WaitDocumentChange(null, true))
            {
                OkDialog(proteinsDlg, proteinsDlg.OkDialog);
            }
            //IsPauseForAuditLog = true;
            //PauseForAuditLog();
            RunUI(() =>
            {
                List<PeptideGroupDocNode> proteins = new List<PeptideGroupDocNode>();
                List<PeptideGroupDocNode> nonProteins = new List<PeptideGroupDocNode>();
                foreach (var docNode in SkylineWindow.DocumentUI.MoleculeGroups)
                {
                    if (docNode.IsProtein)
                        proteins.Add(docNode);
                    else
                        nonProteins.Add(docNode);
                }
                Assert.AreEqual(3987, proteins.Count);
                Assert.AreEqual(0, nonProteins.Count);
            });
        }
    }
}
