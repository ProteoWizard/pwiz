/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class UnnamedTransitionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestUnnamedTransitions()
        {
            TestFilesZip = @"TestFunctional\UnnamedTransitionTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("UnnamedTransitionTest.sky"));
            });

            {
                var document = SkylineWindow.Document;
                CollectionAssert.AreEqual(new[]{true}, document.MoleculeTransitions.Select(t=>t.ExplicitQuantitative).Distinct().ToList());
                CollectionAssert.AreEqual(new[]{string.Empty}, document.MoleculeTransitions.Select(t=>t.CustomIon.Name).Distinct().ToList());
                Assert.AreEqual(2, document.MoleculeTransitionGroupCount);
                Assert.AreEqual(4, document.MoleculeTransitionGroups.First().Transitions.Count());
                Assert.AreEqual(6, document.MoleculeTransitionCount);
            }
            VerifyPeptideChromInfos(SkylineWindow.Document);
            // Select the first transition in the document and delete it, and make sure that 
            // no other transitions get deleted
            RunUI(()=>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(3, 0);
                SkylineWindow.EditDelete();
            });
            Assert.AreEqual(5, SkylineWindow.Document.MoleculeTransitionCount);
            RunUI(()=>SkylineWindow.Undo());
            Assert.AreEqual(6, SkylineWindow.Document.MoleculeTransitionCount);
            PeptideTreeNode peptideTreeNode = null;
            RunUI(()=>
            {
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(1, 0);
                peptideTreeNode = (PeptideTreeNode) SkylineWindow.SequenceTree.SelectedNode;
            });

            // Give the one transition in each precursor a name
            SetTransitionName((TransitionTreeNode)peptideTreeNode.Nodes[0].Nodes[3], "A");
            SetTransitionName((TransitionTreeNode)peptideTreeNode.Nodes[1].Nodes[0], "A");
            VerifyPeptideChromInfos(SkylineWindow.Document);
            // Select the named transition in the document and delete it, and make sure that 
            // no other transitions get deleted
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(3, 3);
                SkylineWindow.EditDelete();
            });
            Assert.AreEqual(4, SkylineWindow.Document.MoleculeTransitionCount);
            RunUI(() => SkylineWindow.Undo());
            Assert.AreEqual(6, SkylineWindow.Document.MoleculeTransitionCount);
            SetTransitionName((TransitionTreeNode)peptideTreeNode.Nodes[0].Nodes[2],"B");
            SetTransitionName((TransitionTreeNode)peptideTreeNode.Nodes[1].Nodes[1], "B");
            SetTransitionName((TransitionTreeNode)peptideTreeNode.Nodes[0].Nodes[1], "C");
            SetTransitionName((TransitionTreeNode)peptideTreeNode.Nodes[0].Nodes[0], "D");
            VerifyPeptideChromInfos(SkylineWindow.Document);
            for (int i = 0; i < 6; i++)
            {
                RunUI(() =>
                {
                    SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(3, i);
                    SkylineWindow.MarkQuantitative(false);
                });
                VerifyPeptideChromInfos(SkylineWindow.Document);
            }
            for (int i = 0; i < 6; i++)
            {
                RunUI(() =>
                {
                    SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(3, 5 - i);
                    SkylineWindow.MarkQuantitative(true);
                });
                VerifyPeptideChromInfos(SkylineWindow.Document);
            }
        }

        public void VerifyPeptideChromInfos(SrmDocument document)
        {
            foreach (var peptideDocNode in document.Molecules)
            {
                var lightPrecursor = peptideDocNode.TransitionGroups.First();
                var heavyPrecursor = peptideDocNode.TransitionGroups.Skip(1).First();
                bool allTransitionsHaveNames = peptideDocNode.TransitionGroups
                    .SelectMany(precursor => precursor.Transitions)
                    .All(transition => !string.IsNullOrEmpty(transition.CustomIon.Name));
                var heavyByNames = heavyPrecursor.Transitions.Where(t => !string.IsNullOrEmpty(t.CustomIon.Name))
                    .ToDictionary(t => t.CustomIon.Name);
                for (int replicateIndex = 0;
                    replicateIndex < document.Settings.MeasuredResults.Chromatograms.Count;
                    replicateIndex++)
                {
                    var peptideChromInfo = peptideDocNode.Results[replicateIndex].First();
                    var labelRatio = peptideChromInfo.LabelRatios.FirstOrDefault(ratio =>
                        IsotopeLabelType.light.Equals(ratio.LabelType) &&
                        IsotopeLabelType.heavy.Equals(ratio.StandardType));
                    Assert.IsNotNull(labelRatio);
                    var numerators = new List<double>();
                    var denominators = new List<double>();
                    foreach (var transition in lightPrecursor.Transitions)
                    {
                        if (!transition.ExplicitQuantitative)
                        {
                            continue;
                        }

                        var chromInfo = transition.Results[replicateIndex].FirstOrDefault();
                        if (chromInfo == null || chromInfo.IsEmpty)
                        {
                            continue;
                        }

                        if (allTransitionsHaveNames)
                        {
                            TransitionDocNode heavyTransition;
                            heavyByNames.TryGetValue(transition.CustomIon.Name, out heavyTransition);
                            if (heavyTransition == null)
                            {
                                continue;
                            }

                            var heavyChromInfo = heavyTransition.Results[replicateIndex].FirstOrDefault();
                            if (heavyChromInfo == null || heavyChromInfo.IsEmpty)
                            {
                                continue;
                            }
                            denominators.Add(chromInfo.Area);
                        }
                        numerators.Add(chromInfo.Area);
                    }

                    if (!allTransitionsHaveNames)
                    {
                        foreach (var transition in heavyPrecursor.Transitions)
                        {
                            if (!transition.ExplicitQuantitative)
                            {
                                continue;
                            }

                            var chromInfo = transition.Results[replicateIndex].FirstOrDefault();
                            if (chromInfo == null || chromInfo.IsEmpty)
                            {
                                continue;
                            }
                            denominators.Add(chromInfo.Area);
                        }
                    }

                    if (numerators.Count == 0 || denominators.Count == 0)
                    {
                        Assert.IsNull(labelRatio.Ratio);
                        continue;
                    }
                    if (allTransitionsHaveNames)
                    {
                        Assert.IsFalse(float.IsNaN(labelRatio.Ratio.StdDev));
                    }
                    else
                    {
                        Assert.IsTrue(float.IsNaN(labelRatio.Ratio.StdDev));
                    }

                    var expectedRatio = numerators.Sum() / denominators.Sum();
                    Assert.AreEqual((float) expectedRatio, labelRatio.Ratio.Ratio);
                }
            }
        }

        private void SetTransitionName(TransitionTreeNode transitionTreeNode, string name)
        {
            RunUI(()=>
            {
                SkylineWindow.SequenceTree.SelectedNode = transitionTreeNode;
              
            });
            RunDlg<EditCustomMoleculeDlg>(() => SkylineWindow.ModifyTransition(transitionTreeNode), dlg =>
            {
                dlg.NameText = name;
                dlg.OkDialog();
            });
        }

        private void SetTransitionQuantitative(TransitionTreeNode transitionTreeNode, bool quantitative)
        {
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = transitionTreeNode;
                SkylineWindow.MarkQuantitative(quantitative);
            });

        }
    }
}
