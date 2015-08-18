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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for isotope label type synchronization, for small molecules.
    /// </summary>
    [TestClass]
    public class SynchSiblingsSmallMoleculesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSynchSiblingsSmallMolecules()
        {
            TestFilesZip = @"TestFunctional\SynchSiblingsSmallMoleculesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const int molCount = 1;
            var doc = SkylineWindow.Document; 
            for (var loop=0; loop < 3; loop++)
            {
                // Should be able to synchronize of both sibs have formula, or neither, but not one of each
                string testname;
                switch (loop)
                {
                    case 0:
                        testname = "ions_testing_mixed.sky";
                        break;
                    case 1:
                        testname = "ions_testing.sky";
                        break;
                    default:
                        testname = "ions_testing_masses.sky";
                        break;
                }
                string testPath = TestFilesDir.GetTestPath(testname);
                RunUI(() => SkylineWindow.OpenFile(testPath));
                doc = WaitForDocumentChange(doc); 
            
                SelectNode(SrmDocument.Level.Molecules, 0);

                Assert.AreEqual(molCount, SkylineWindow.Document.MoleculeCount);
                RunUI(SkylineWindow.ExpandPrecursors);

                Settings.Default.SynchronizeIsotopeTypes = true;

                // Select the first transition group
                SelectNode(SrmDocument.Level.TransitionGroups, 0);

                // Add precursor transition to it
                var pickList = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
                RunUI(() =>
                {
                    pickList.ApplyFilter(false);
                    pickList.SetItemChecked(0, true);
                    pickList.AutoManageChildren = false;
                    pickList.IsSynchSiblings = true; // Cause a precursor transition to be added to heavy group
                });
                OkDialog(pickList, pickList.OnOk);
                WaitForClosedForm(pickList);
                doc = WaitForDocumentChange(doc);
                if (loop == 0)
                {
                    // No synch is possible - one ion is formula based, the other mass only
                    Assert.AreEqual(1, doc.MoleculeTransitions.Count());
                }
                else
                {
                    // There should now be a precursor transition for the second, heavy labeled transition group, and it
                    // should match the mz of the transition group
                    Assert.AreNotEqual(doc.MoleculeTransitionGroups.ToArray()[0].PrecursorMz, doc.MoleculeTransitions.ToArray()[1].Mz);
                    Assert.AreEqual(doc.MoleculeTransitionGroups.ToArray()[1].PrecursorMz, doc.MoleculeTransitions.ToArray()[1].Mz);
                }                
            }

            //
            // Now with isotope distributions
            //
            var documentPath = TestFilesDir.GetTestPath("ions_testing_isotopes.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForDocumentLoaded();
 
            SelectNode(SrmDocument.Level.Molecules, 0);

            Assert.AreEqual(molCount, SkylineWindow.Document.MoleculeCount);
            RunUI(SkylineWindow.ExpandPrecursors);

            Settings.Default.SynchronizeIsotopeTypes = true;
 
            // Select the first transition group
            SelectNode(SrmDocument.Level.TransitionGroups, 0);

            // Add isotope labeled precursor transitions to it
            var pickList0 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                pickList0.ApplyFilter(false);
                pickList0.SetItemChecked(0, true);
                pickList0.SetItemChecked(1, true);
                pickList0.SetItemChecked(2, true);
                pickList0.AutoManageChildren = false;
                Assert.IsTrue(pickList0.CanSynchSiblings);
                pickList0.IsSynchSiblings = true; // Cause precursor transitions to be added to heavy group
            });
            OkDialog(pickList0, pickList0.OnOk);
            WaitForClosedForm(pickList0);
            Assert.AreEqual(SkylineWindow.Document.MoleculeTransitionGroups.ToArray()[1].PrecursorMz,
                SkylineWindow.Document.MoleculeTransitions.ToArray()[4].Mz);
            Assert.AreEqual(SkylineWindow.Document.MoleculeTransitionGroups.ToArray()[1].PrecursorMz + 1.00349556477,
                SkylineWindow.Document.MoleculeTransitions.ToArray()[5].Mz, 1e-6);

            // Verify that adding a custom transition prevents the synch siblings checkbox from appearing
            RunUI(() =>
            {
                SkylineWindow.ExpandPrecursors();
                var node = SkylineWindow.SequenceTree.Nodes[0].FirstNode.FirstNode;
                SkylineWindow.SequenceTree.SelectedNode = node;
            });
            var moleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            var C12H12 = "C12H12";
            var testNametextA = "y1";
            RunUI(() =>
            {
                moleculeDlg.FormulaBox.Formula = C12H12;
                moleculeDlg.NameText = testNametextA;
                moleculeDlg.Charge = 1;
            });
            OkDialog(moleculeDlg, moleculeDlg.OkDialog);
            var newDoc = SkylineWindow.Document;
            var compareIon = new DocNodeCustomIon(C12H12, testNametextA);
            const int transY1Index = 3;
            Assert.AreEqual(compareIon, newDoc.MoleculeTransitions.ElementAt(transY1Index).Transition.CustomIon);
            Assert.AreEqual(1, newDoc.MoleculeTransitions.ElementAt(transY1Index).Transition.Charge);
            var pickList1 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                pickList1.AutoManageChildren = true;
                Assert.IsFalse(pickList1.CanSynchSiblings);
            });
            OkDialog(pickList1, pickList1.OnOk);
            Assert.AreEqual(7, newDoc.MoleculeTransitions.Count());

            // Long as we're here, check mz filtering
            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettings.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                transitionSettings.MinMz = 160; // Should drop that custom ion
            }); 
            OkDialog(transitionSettings, transitionSettings.OkDialog);
            newDoc = WaitForDocumentChange(newDoc);
            Assert.AreEqual(6, newDoc.MoleculeTransitions.Count());
            RunUI(() => { SkylineWindow.Undo(); });
            newDoc = WaitForDocumentChange(newDoc);
            Assert.AreEqual(7, newDoc.MoleculeTransitions.Count());

            RunUI(() =>
            {
                SkylineWindow.ExpandPrecursors();
                var node = SkylineWindow.SequenceTree.Nodes[0].FirstNode.Nodes[1];
                SkylineWindow.SequenceTree.SelectedNode = node;
            });
            var pickList2 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                Assert.IsFalse(pickList2.CanSynchSiblings); // The light set has a non-precursor transition
            });
            OkDialog(pickList2, pickList2.OnOk);

            var transitionSettings2 = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettings2.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                transitionSettings2.MinMz = 300; // Should drop that custom ion
            });
            OkDialog(transitionSettings2, transitionSettings2.OkDialog);
            newDoc = WaitForDocumentChange(newDoc);
            Assert.AreEqual(6, newDoc.MoleculeTransitions.Count());

            var pickList3 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                Assert.IsTrue(pickList3.CanSynchSiblings); 
            });
            OkDialog(pickList3, pickList3.OnOk);

        }
    }
}