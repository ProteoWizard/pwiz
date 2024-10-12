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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RefineShapeCorrelationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRefineShapeCorrelation()
        {
            TestFilesZip = @"TestFunctional\RefineShapeCorrelationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ShapeCorrelationTest.sky")));
            var document = SkylineWindow.Document;
            var bestCorrelations = GetShapeCorrelationValues(SkylineWindow.Document, true);
            Assert.AreEqual(document.MoleculeTransitionCount, bestCorrelations.Count);
            var worstCorrelations = GetShapeCorrelationValues(SkylineWindow.Document, false);
            Assert.AreEqual(document.MoleculeTransitionCount, worstCorrelations.Count);
            Assert.AreEqual(0, bestCorrelations.Values.Count(v=>!v.HasValue));

            var bestCorrelationValues = bestCorrelations.Values.ToList();
            bestCorrelationValues.Sort();
            var includedCutoff = (double) bestCorrelationValues[bestCorrelationValues.Count / 4];
            
            var worstCorrelationValues = worstCorrelations.Values.ToList();
            worstCorrelationValues.Sort();
            var quantitativeCutoff = (double) worstCorrelationValues[worstCorrelationValues.Count / 2];

            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.SelectedTab = RefineDlg.TABS.Consistency;
                refineDlg.SCIncludedComparisonType = RefinementSettings.ComparisonType.max;
                refineDlg.SCIncludedCutoff = includedCutoff;
                refineDlg.SCQuantitativeComparisonType = RefinementSettings.ComparisonType.min;
                refineDlg.SCQuantitativeCutoff = quantitativeCutoff;
                refineDlg.OkDialog();
            });

            var refinedDocument = SkylineWindow.Document;

            foreach (var entry in bestCorrelations)
            {
                var molecule = (PeptideDocNode) document.FindNode(entry.Key.GetPathTo((int)SrmDocument.Level.Molecules));
                var transition = (TransitionDocNode)refinedDocument.FindNode(entry.Key);

                if (molecule.GlobalStandardType != null)
                {
                    Assert.IsNotNull(transition, "Internal standard transition {0} not found", entry.Key);
                    Assert.IsTrue(transition.ExplicitQuantitative, "Internal standard transition {0} should be quantitative", entry.Key);
                    continue;
                }


                string includedMessage = string.Format("Transition {0} best correlation {1} included cutoff {2}",
                    entry.Key,
                    entry.Value, includedCutoff);
                if (entry.Value >= includedCutoff)
                {
                    AssertEx.IsNotNull(transition, includedMessage);
                    var worstCorrelation = worstCorrelations[entry.Key];
                    bool expectedQuantitative = worstCorrelation >= quantitativeCutoff;
                    var quantitativeMessage =
                        string.Format("Transition {0} worst correlation {1} quantitative cutoff {2}", entry.Key,
                            worstCorrelation, quantitativeCutoff);
                    AssertEx.AreEqual(expectedQuantitative, transition.ExplicitQuantitative, quantitativeMessage);
                }
                else
                {
                    AssertEx.IsNull(transition, includedMessage);
                }
            }
        }

        private Dictionary<IdentityPath, float?> GetShapeCorrelationValues(SrmDocument document,
            bool bestReplicate)
        {
            var dictionary = new Dictionary<IdentityPath, float?>();
            foreach (var moleculeList in document.MoleculeGroups)
            {
                foreach (var molecule in moleculeList.Molecules)
                {
                    foreach (var precursor in molecule.TransitionGroups)
                    {
                        foreach (var transition in precursor.Transitions)
                        {
                            var shapeCorrelations = new List<float>();
                            if (transition.Results != null)
                            {
                                shapeCorrelations.AddRange(transition.Results.SelectMany(r=>r.Select(transitionChromInfo=>transitionChromInfo.PeakShapeValues?.ShapeCorrelation)).OfType<float>());
                            }

                            var identityPath = new IdentityPath(moleculeList.PeptideGroup, molecule.Peptide,
                                precursor.TransitionGroup, transition.Transition);
                            float? shapeCorrelation = null;
                            if (shapeCorrelations.Count > 0)
                            {
                                shapeCorrelation = bestReplicate ? shapeCorrelations.Max() : shapeCorrelations.Min();
                            }

                            dictionary.Add(identityPath, shapeCorrelation);
                        }
                    }
                }
            }

            return dictionary;
        }
    }
}
