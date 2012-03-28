/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for CE Optimization.
    /// </summary>
    [TestClass]
    public class SynchSiblingsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSynchSiblings()
        {
            TestFilesZip = @"TestFunctional\SynchSiblingsTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Test CE optimization.  Creates optimization transition lists,
        /// imports optimization data, shows graphs, recalculates linear equations,
        /// and exports optimized method.
        /// </summary>
        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ANL_N15_mini.sky")));
            RunUI(SkylineWindow.ExpandPrecursors);

            Settings.Default.SynchronizeIsotopeTypes = true;

            // Wait until the document contains a fully loaded library
            WaitForConditionUI(() => SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries.IsLoaded);

            var docOrig = SkylineWindow.Document;
            var pathPeptide = docOrig.GetPathTo((int) SrmDocument.Level.Peptides, 0);

            // Select the first transition group
            SelectNode(SrmDocument.Level.TransitionGroups, 0);

            // Add two new transitions to it
            var pickList0 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                pickList0.ApplyFilter(false);
                pickList0.SetItemChecked(5, true);
                pickList0.SetItemChecked(6, true);
                pickList0.AutoManageChildren = false;
            });
            OkDialog(pickList0, pickList0.OnOk);
            WaitForClosedForm(pickList0);

            VerifySynchronized(SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes);

            // Uncheck one transition without synchronizing
            var pickList1 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                pickList1.ApplyFilter(false);
                pickList1.SetItemChecked(5, false);
                pickList1.IsSynchSiblings = false;
            });
            OkDialog(pickList1, pickList1.OnOk);
            WaitForClosedForm(pickList1);

            var nodes = SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes;
            Assert.AreEqual(6, nodes[0].Nodes.Count);
            Assert.AreEqual(7, nodes[1].Nodes.Count);
            Assert.AreEqual(7, nodes[2].Nodes.Count);

            // Now synchronize the other two
            var pickList2 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                Assert.IsFalse(pickList2.IsSynchSiblings);
                pickList2.IsSynchSiblings = true;
            });
            OkDialog(pickList2, pickList2.OnOk);
            WaitForClosedForm(pickList2);

            VerifySynchronized(SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes);

            // Change selection to one of the heavy nodes
            // and synchronize siblings to automanage
            SelectNode(SrmDocument.Level.TransitionGroups, 1);
            var pickList3 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                pickList3.AutoManageChildren = true;
            });
            OkDialog(pickList3, pickList3.OnOk);
            WaitForClosedForm(pickList3);

            VerifySynchronized(SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes);

            // This should return the first peptide to its original state
            var docMatching = SkylineWindow.Document;
            Assert.AreEqual(docOrig.FindNode(pathPeptide), docMatching.FindNode(pathPeptide));

            // Delete the two heavy nodes
            RunUI(SkylineWindow.EditDelete);
            RunUI(SkylineWindow.EditDelete);

            // Verify not possible to synch at various levels
            VerifyCannotSynch(SrmDocument.Level.PeptideGroups);
            VerifyCannotSynch(SrmDocument.Level.Peptides);
            VerifyCannotSynch(SrmDocument.Level.TransitionGroups);

            // Undo and make sure it is possible to synch again
            RunUI(SkylineWindow.Undo);
            var pickList4 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() => Assert.IsTrue(pickList4.CanSynchSiblings));
            OkDialog(pickList4, pickList4.OnCancel);
            WaitForClosedForm(pickList4);

            // Pick different charge states of the same label type,
            // and make sure it is not possible to synchronize siblings
            SelectNode(SrmDocument.Level.Peptides, 0);
            var pickListPep = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() =>
            {
                pickListPep.ApplyFilter(false);
                pickListPep.SelectAll = false;
                // Charge 1 is not measurable within the instrument settings
                for (int i = 0; i < 4; i++)
                    pickListPep.SetItemChecked(i * 3, true);
            });
            OkDialog(pickListPep, pickListPep.OnOk);
            WaitForClosedForm(pickListPep);

            VerifyCannotSynch(SrmDocument.Level.TransitionGroups);
        }

        private static void VerifySynchronized(TreeNodeCollection nodes)
        {
            // Make sure nodes have been added with siblings synchronized
            Assert.AreEqual(3, nodes.Count);
            var regexTran = new Regex(@"[A-Z] \[([^\]]+)\] - [^+]*(\++) (\(rank \d+\))");
            for (int i = 0; i < nodes[0].Nodes.Count; i++)
            {
                var match0 = MatchTransitionText(nodes[0].Nodes[i], regexTran);
                var match1 = MatchTransitionText(nodes[1].Nodes[i], regexTran);
                var match2 = MatchTransitionText(nodes[2].Nodes[i], regexTran);
                for (int j = 1; j < match0.Groups.Count; j++)
                {
                    Assert.AreEqual(match0.Groups[j].ToString(), match1.Groups[j].ToString());
                    Assert.AreEqual(match0.Groups[j].ToString(), match2.Groups[j].ToString());
                }
            }
        }

        private static Match MatchTransitionText(TreeNode nodeTreeTran, Regex regexTran)
        {
            var match = regexTran.Match(nodeTreeTran.Text);
            Assert.IsTrue(match.Success, string.Format("The transition node text '{0}' did not match the expected pattern.", nodeTreeTran.Text));
            return match;
        }

        private static void VerifyCannotSynch(SrmDocument.Level levelNode)
        {
            SelectNode(levelNode, 0);
            var pickList = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            RunUI(() => Assert.IsFalse(pickList.CanSynchSiblings));
            OkDialog(pickList, pickList.OnCancel);
        }
    }
}