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

        [TestMethod]
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
            TestDialog( ImportType.FASTA);
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
            OkDialog(associateProteinsDlg, associateProteinsDlg.CancelButton.PerformClick);
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AssociateProteinsTest.sky")));
            var associateProteinsDlg2 = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            RunUI(associateProteinsDlg2.UseBackgroundProteome);
            TestDialog(ImportType.BGPROTEOME);
        }


        /// <summary>
        /// tests the form
        /// makes sure correct number of matches were found
        /// unchecks all boxes to make sure apply button disables
        /// </summary>
        private void TestDialog(ImportType type)
        {
            var dlg2 = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            if (type == ImportType.FASTA)
            {
                RunUI(() => dlg2.FastaFileName = _fastaFile);
            }
            else
            {
                RunUI(dlg2.UseBackgroundProteome);
            }

            WaitForCondition(() => dlg2.FinalResults != null);

            //PauseTest();
            RunUI(() =>
            {
                Assert.AreEqual(425484, dlg2.FinalResults.PeptidesMapped);
                Assert.AreEqual(0, dlg2.FinalResults.PeptidesUnmapped);
                Assert.AreEqual(84463, dlg2.FinalResults.ProteinsMapped);
                Assert.AreEqual(4016, dlg2.FinalResults.ProteinsUnmapped);

                dlg2.GroupProteins = true;
                dlg2.FindMinimalProteinList = false;
                dlg2.SelectedSharedPeptides = ProteinAssociation.SharedPeptides.AssignedToBestProtein;
                //Console.WriteLine($"KeepAllProteins AssignedToBestProtein {dlg2.FinalResults.FinalProteinCount} {dlg2.FinalResults.FinalPeptideCount}");
                Assert.AreEqual(466483, dlg2.FinalResults.FinalPeptideCount);
                Assert.AreEqual(42491, dlg2.FinalResults.FinalProteinCount);

                dlg2.FindMinimalProteinList = true;
                dlg2.SelectedSharedPeptides = ProteinAssociation.SharedPeptides.DuplicatedBetweenProteins;
                //Console.WriteLine($"MinimalProteinList DuplicatedBetweenProteins {dlg2.FinalResults.FinalProteinCount} {dlg2.FinalResults.FinalPeptideCount}");
                Assert.AreEqual(721703, dlg2.FinalResults.FinalPeptideCount);
                Assert.AreEqual(43364, dlg2.FinalResults.FinalProteinCount);

                dlg2.FindMinimalProteinList = false;
                dlg2.SelectedSharedPeptides = ProteinAssociation.SharedPeptides.Removed;
                //Console.WriteLine($"KeepAllProteins Removed {dlg2.FinalResults.FinalProteinCount} {dlg2.FinalResults.FinalPeptideCount}");
                Assert.AreEqual(159725, dlg2.FinalResults.FinalPeptideCount);
                Assert.AreEqual(35598, dlg2.FinalResults.FinalProteinCount);

                dlg2.MinPeptidesPerProtein = 10;
                //Console.WriteLine($"MinPeptidesPerProtein 10 {dlg2.FinalResults.FinalProteinCount} {dlg2.FinalResults.FinalPeptideCount}");
                Assert.AreEqual(87720, dlg2.FinalResults.FinalPeptideCount);
                Assert.AreEqual(3963, dlg2.FinalResults.FinalProteinCount);
            });
            //PauseTest();
            OkDialog(dlg2, dlg2.AcceptButton.PerformClick);
            //IsPauseForAuditLog = true;
            //PauseForAuditLog();
            RunUI(() =>
            {
                List<PeptideGroupDocNode> proteins = new List<PeptideGroupDocNode>();
                List<PeptideGroupDocNode> nonProteins = new List<PeptideGroupDocNode>();
                foreach (var docNode in SkylineWindow.Document.MoleculeGroups)
                {
                    if (docNode.IsProtein)
                        proteins.Add(docNode);
                    else
                        nonProteins.Add(docNode);
                }
                Assert.AreEqual(3963, proteins.Count);
                Assert.AreEqual(0, nonProteins.Count);
            });
        }
    }
}
