/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ChargedLossesLibraryTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestChargedLossesLibrary()
        {
            TestFilesZip = @"TestFunctional\ChargedLossesLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => { SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ChargedLossesLibraryTest.sky")); });
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                SkylineWindow.ShowGraphSpectrum(true);
                SkylineWindow.ShowPrecursorIon(true);
            });
            
            // Use the RefineDlg to auto select transitions for all precursors
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.AutoTransitions = true;
                refineDlg.OkDialog();
            });

            // Make sure that the peptide "LNGTK" has the transitions that we expect
            FindNode("LNGTK");
            WaitForGraphs();
            PeptideDocNode peptideDocNode = null;
            RunUI(() =>
            {
                peptideDocNode =
                    (SkylineWindow.SelectedNode as PeptideTreeNode)?.DocNode;
            });
            Assert.AreEqual(1, peptideDocNode.TransitionGroupCount);
            var transitionGroupDocNode = peptideDocNode.TransitionGroups.First();

            // Expected: precursor-972.3, precursor-1175.4, precursor-1378
            AssertTransitionMzs(transitionGroupDocNode, IonType.precursor, 938.4676, 735.3883, 532.3089);
            // Expected: y4-1378.5, y3, y1
            AssertTransitionMzs(transitionGroupDocNode, IonType.y, 419.2248, 305.1819, 147.1128);
            // Expected: b2-1175.4, b2-1378.5, b3-1378.5, b4-1378.5
            AssertTransitionMzs(transitionGroupDocNode, IonType.b, 431.2136, 228.1342, 285.1557, 386.2034);
        }

        /// <summary>
        /// Asserts that the subset of transitions with the specified ion type have the specified m/z values
        /// </summary>
        private void AssertTransitionMzs(TransitionGroupDocNode transitionGroupDocNode, IonType ionType, params double[] mzs)
        {
            var transitions = transitionGroupDocNode.Transitions.Where(t => t.Transition.IonType == ionType).ToList();
            Assert.AreEqual(transitions.Count, mzs.Length);
            for (int i = 0; i < mzs.Length; i++)
            {
                var transition = transitions[i];
                Assert.AreEqual(mzs[i], transition.Mz, 0.0001, "Mismatch at Position:{0} Transition:{1}", i, transition.Transition);
            }
        }
    }
}
