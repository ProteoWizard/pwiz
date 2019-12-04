/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DecoyTargetMatchTest : AbstractFunctionalTest
    {
        //[TestMethod]  TODO(kaipot): Enable this when checking is re-enabled
        public void TestDecoyTargetMatching()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestFunctional\DecoyTargetMatchTest.zip");

            // Add some peptides
            var targets = new[] {"PEPTIDES", "LIONS", "CATS", "DOGS", "ELVIS"};
            PeptideDocNode[] nodePepTargets = null;
            RunUI(() =>
            {
                SkylineWindow.Paste(TextUtil.LineSeparate(targets));
                Assert.AreEqual(targets.Length, SkylineWindow.DocumentUI.PeptideCount);
                nodePepTargets = SkylineWindow.DocumentUI.Peptides.Where(pep => !pep.IsDecoy).ToArray();
            });

            const int numDecoys = 5;
            RunDlg<GenerateDecoysDlg>(() => SkylineWindow.ShowGenerateDecoysDlg(), decoysDlg =>
            {
                decoysDlg.NumDecoys = numDecoys;
                decoysDlg.DecoysMethod = DecoyGeneration.SHUFFLE_SEQUENCE;
                decoysDlg.OkDialog();
            });

            WaitForConditionUI(() => targets.Length + numDecoys == SkylineWindow.DocumentUI.PeptideCount);

            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("test.sky")));

            VerifyDecoyStatus(5, 0, 0);
            
            // remove a target
            SetTargets(nodePepTargets.Take(4));
            VerifyDecoyStatus(5, 1, 0);

            // remove 2 targets
            SetTargets(nodePepTargets.Take(3));
            VerifyDecoyStatus(5, 2, 0);

            // remove a transition from the first target
            var newPeps = new List<PeptideDocNode>(nodePepTargets);
            newPeps[0] = RemoveTransition(newPeps[0]);
            SetTargets(newPeps);
            VerifyDecoyStatus(5, 0, 1);

            // remove a transition from the second target
            newPeps[1] = RemoveTransition(newPeps[1]);
            SetTargets(newPeps);
            VerifyDecoyStatus(5, 0, 2);

            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void SetTargets(IEnumerable<PeptideDocNode> nodePeps)
        {
            RunUI(() =>
            {
                var newNodes = new List<DocNode> {SkylineWindow.DocumentUI.PeptideGroups.First().ChangeChildren(nodePeps.Cast<DocNode>().ToList())};
                newNodes.AddRange(SkylineWindow.DocumentUI.PeptideGroups.Skip(1));
                SkylineWindow.ModifyDocument("Set peptides", doc => SkylineWindow.DocumentUI.ChangeChildren(newNodes) as SrmDocument);
            });
        }

        private PeptideDocNode RemoveTransition(PeptideDocNode nodePep)
        {
            var precursors = nodePep.TransitionGroups.ToList();
            precursors[0] = (TransitionGroupDocNode) precursors[0].ChangeChildren(precursors[0].Children.Skip(1).ToList());
            return (PeptideDocNode) nodePep.ChangeChildren(precursors.Cast<DocNode>().ToList());
        }

        private void VerifyDecoyStatus(int expectedDecoys, int expectedNoSource, int expectedWrongTransitionCount)
        {
            int numDecoys = -1, numNoSource = -1, numWrongTransitionCount = -1;
            RunUI(() => SkylineWindow.CheckDecoys(SkylineWindow.DocumentUI, out numDecoys, out numNoSource, out numWrongTransitionCount));
            Assert.AreEqual(expectedDecoys, numDecoys);
            Assert.AreEqual(expectedNoSource, numNoSource);
            Assert.AreEqual(expectedWrongTransitionCount, numWrongTransitionCount);

            if (numNoSource > 0 || numWrongTransitionCount > 0)
            {
                RunDlg<MultiButtonMsgDlg>(SkylineWindow.ImportResults, dlg =>
                {
                    Assert.IsTrue(
                        dlg.Message.StartsWith(string.Format(
                            Resources.SkylineWindow_ImportResults_The_document_contains_decoys_that_do_not_match_the_targets__Out_of__0__decoys_, numDecoys)));
                    if (numNoSource == 1)
                        Assert.IsTrue(dlg.Message.Contains(Resources.SkylineWindow_ImportResults_1_decoy_does_not_have_a_matching_target));
                    else if (numNoSource > 1)
                        Assert.IsTrue(dlg.Message.Contains(string.Format(
                            Resources.SkylineWindow_ImportResults__0__decoys_do_not_have_a_matching_target, numNoSource)));
                    if (numWrongTransitionCount == 1)
                        Assert.IsTrue(dlg.Message.Contains(Resources.SkylineWindow_ImportResults_1_decoy_does_not_have_the_same_number_of_transitions_as_its_matching_target));
                    else if (numWrongTransitionCount > 1)
                        Assert.IsTrue(dlg.Message.Contains(string.Format(
                            Resources.SkylineWindow_ImportResults__0__decoys_do_not_have_the_same_number_of_transitions_as_their_matching_targets, numWrongTransitionCount)));
                    dlg.BtnCancelClick();
                });
            }
        }
    }
}
