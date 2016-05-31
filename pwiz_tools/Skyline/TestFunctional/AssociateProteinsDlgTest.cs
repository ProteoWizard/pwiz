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
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
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
            TestUseBackgroundProteome() ;
        }

        /// <summary>
        /// Tests using a FASTA file to match
        /// </summary>
        private void TestUseFasta()

        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AssociateProteinsTest.sky")));
            var associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            RunUI(() =>{ associateProteinsDlg.UseFastaFile(_fastaFile); });
            TestDialog(associateProteinsDlg, ImportType.FASTA);
        }

        /// <summary>
        /// Test using background-proteome to match
        /// </summary>
        private void TestUseBackgroundProteome()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AssociateProteinsTest.sky")));
            var associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            RunUI(associateProteinsDlg.UseBackgroundProteome);
            TestDialog(associateProteinsDlg, ImportType.BGPROTEOME);
        }


        /// <summary>
        /// tests the form
        /// makes sure correct number of matches were found
        /// unchecks all boxes to make sure apply button disables
        /// </summary>
        private void TestDialog(AssociateProteinsDlg dlg, ImportType type)
        {
            int initialPeptideCount = SkylineWindow.Document.PeptideCount;
            RunUI(() =>
            {
                var items = dlg.CheckboxListMatches.Items.Cast<object>().Select(item=>item.ToString()).ToArray();
                Assert.AreEqual(5, dlg.CheckboxListMatches.CheckedItems.Count); // 5 items checked
                Assert.AreEqual(5, items.Length);
                Assert.IsTrue(dlg.ApplyButton.Enabled);
                for (var i = 0; i < items.Length; i++)
                {
                    dlg.CheckboxListMatches.SetItemCheckState(i, CheckState.Unchecked);
                }
                Assert.IsFalse(dlg.ApplyButton.Enabled);
                Assert.AreEqual(0, dlg.CheckboxListMatches.CheckedItems.Count); // 0 items checked
                for (var i = 0; i < items.Length; i++)
                {
                    if (i == 0 || i == 1 || i == 2)
                        continue;

                    dlg.CheckboxListMatches.SetItemCheckState(i, CheckState.Checked);

                }
                Assert.AreEqual(2, dlg.CheckboxListMatches.CheckedItems.Count); // 2 items checked
                Assert.IsTrue(dlg.ApplyButton.Enabled);
            });
            OkDialog(dlg, dlg.ApplyButton.PerformClick);
            RunUI(() =>
            {
                // +4 because peptides that associate with two proteins get duplicated and there are 4 in this test data file
                Assert.AreEqual(initialPeptideCount + 4, SkylineWindow.Document.PeptideCount);
                List<PeptideGroupDocNode> proteins = new List<PeptideGroupDocNode>();
                List<PeptideGroupDocNode> peptideGroups = new List<PeptideGroupDocNode>();
                foreach (var docNode in SkylineWindow.Document.MoleculeGroups)
                {
                    if (docNode.IsProtein)
                        proteins.Add(docNode);
                    else
                        peptideGroups.Add(docNode);
                }
                Assert.AreEqual(2, proteins.Count);
                Assert.AreEqual(5, peptideGroups.Count);
                Assert.AreEqual(proteins[0].Children.Count, proteins[1].Children.Count);
          
                for(var i = 0; i < proteins[0].Children.Count; i++)
                {
                    Assert.AreEqual(proteins[0].Peptides.ElementAt(i).ToString(),
                        proteins[0].Peptides.ElementAt(i).ToString());
                }
            });
            var dlg2 = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            if (type == ImportType.FASTA)
            {
                RunUI(() => dlg2.UseFastaFile(_fastaFile));
            }
            else
            {
                RunUI(dlg2.UseBackgroundProteome);
            }

            RunUI(() =>
            {
                var items = dlg2.CheckboxListMatches.Items.Cast<object>().Select(item => item.ToString()).ToArray();
                Assert.AreEqual(3, dlg2.CheckboxListMatches.CheckedItems.Count); // 3 items checked
                Assert.AreEqual(3, items.Length);
                Assert.IsTrue(dlg2.ApplyButton.Enabled);
            });
            OkDialog(dlg2, dlg2.ApplyChanges);
            RunUI(()=> {
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
                Assert.AreEqual(2, nonProteins.Count);
                Assert.AreEqual(initialPeptideCount + 4, SkylineWindow.Document.PeptideCount);
            });
        }
    }
}
