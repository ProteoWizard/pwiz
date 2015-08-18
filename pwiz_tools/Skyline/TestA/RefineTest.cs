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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for RefineTest
    /// </summary>
    [TestClass]
    public class RefineTest : AbstractUnitTest
    {
        [TestMethod]
        public void RefineDocumentTest()
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestA\Refine.zip");

            var document = InitRefineDocument(testFilesDir);
            AssertEx.Serializable(document);  // This checks a longstanding schema error

            // First check a few refinements which should not change the document
            var refineSettings = new RefinementSettings();
            Assert.AreSame(document, refineSettings.Refine(document));
            refineSettings.MinPeptidesPerProtein = (TestSmallMolecules ? 1 : 3); // That magic small molecule node has just one child
            Assert.AreSame(document, refineSettings.Refine(document));
            refineSettings.MinTransitionsPepPrecursor = (TestSmallMolecules ? 1 : 2);
            Assert.AreSame(document, refineSettings.Refine(document));

            // Remove the protein with only 3 peptides
            refineSettings.MinPeptidesPerProtein = 4;
            Assert.AreEqual(document.PeptideGroupCount - 1, refineSettings.Refine(document).PeptideGroupCount);
            refineSettings.MinPeptidesPerProtein = 1;
            // Remove the precursor with only 2 transitions
            refineSettings.MinTransitionsPepPrecursor = 3;
            Assert.AreEqual(document.PeptideTransitionGroupCount - 1, refineSettings.Refine(document).PeptideTransitionGroupCount);
            refineSettings.MinTransitionsPepPrecursor = null;
            // Remove the heavy precursor
            refineSettings.RefineLabelType = IsotopeLabelType.heavy;
            Assert.AreEqual(document.PeptideTransitionGroupCount - 1, refineSettings.Refine(document).PeptideTransitionGroupCount);
            // Remove everything but the heavy precursor
            refineSettings.RefineLabelType = IsotopeLabelType.light;
            var docRefined = refineSettings.Refine(document);
            AssertEx.IsDocumentState(docRefined, 1, 1, 1, 1, 4);
            // Perform the operation again without protein removal
            refineSettings.MinPeptidesPerProtein = null;
            docRefined = refineSettings.Refine(document);
            AssertEx.IsDocumentState(docRefined, 1, 4, 1, 1, 4);
            refineSettings.RefineLabelType = null;
            // Remove repeated peptides
            refineSettings.RemoveRepeatedPeptides = true;
            Assert.AreEqual(document.PeptideCount - 2, refineSettings.Refine(document).PeptideCount);
            // Remove duplicate peptides
            refineSettings.RemoveDuplicatePeptides = true;
            Assert.AreEqual(document.PeptideCount - 3, refineSettings.Refine(document).PeptideCount);

            // Try settings that remove everything from the document
            refineSettings = new RefinementSettings { MinPeptidesPerProtein = 20 };
            Assert.AreEqual(0, refineSettings.Refine(document).PeptideGroupCount);
            refineSettings.MinPeptidesPerProtein = 1;
            refineSettings.MinTransitionsPepPrecursor = 20;
            Assert.AreEqual(0, refineSettings.Refine(document).PeptideGroupCount);

            testFilesDir.Dispose();
        }

        [TestMethod]
        public void RefineResultsTest()
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestA\Refine.zip");
            Settings.Default.RTCalculatorName = Settings.Default.RTScoreCalculatorList.GetDefaults().First().Name;

            var document = InitRefineDocument(testFilesDir);

            // First check a few refinements which should not change the document
            var refineSettings = new RefinementSettings {RTRegressionThreshold = 0.3};
            Assert.AreSame(document, refineSettings.Refine(document));
            refineSettings.RTRegressionThreshold = null;
            refineSettings.DotProductThreshold = Statistics.AngleToNormalizedContrastAngle(0.1);    // Convert form original cos(angle) dot-product
            Assert.AreSame(document, refineSettings.Refine(document));
            refineSettings.DotProductThreshold = null;
            refineSettings.MinPeakFoundRatio = 0;
            refineSettings.MaxPeakFoundRatio = 1.0;
            Assert.AreSame(document, refineSettings.Refine(document));
            refineSettings.MinPeakFoundRatio = refineSettings.MaxPeakFoundRatio = null;
            // refineSettings.MaxPeakRank = 15;  This will remove unmeasured transitions
            Assert.AreSame(document, refineSettings.Refine(document));

            // Remove nodes without results
            refineSettings.MinPeptidesPerProtein = 1;
            refineSettings.RemoveMissingResults = true;
            var docRefined = refineSettings.Refine(document);
            Assert.AreEqual(document.PeptideGroupCount, docRefined.PeptideGroupCount);
            // First three children should be unchanged
            for (int i = 0; i < 3; i++)
                Assert.AreSame(document.Children[i], docRefined.Children[i]);
            var nodePepGroupRefined = (PeptideGroupDocNode) docRefined.Children[3];
            Assert.AreEqual(1, nodePepGroupRefined.MoleculeCount);
            Assert.AreEqual(1, nodePepGroupRefined.TransitionGroupCount);
            Assert.AreEqual(5, nodePepGroupRefined.TransitionCount);

            // Filter for dot product, ignoring nodes without results
            refineSettings.RemoveMissingResults = false;
            double dotProductThreshold = Statistics.AngleToNormalizedContrastAngle(0.9);    // Convert form original cos(angle) dot-product
            refineSettings.DotProductThreshold = dotProductThreshold;
            docRefined = refineSettings.Refine(document);
            int missingResults = 0;
            foreach (var nodeGroup in docRefined.PeptideTransitionGroups)
            {
                if (!nodeGroup.HasResults || nodeGroup.Results[0] == null)
                    missingResults++;
                else
                    Assert.IsTrue(nodeGroup.Results[0][0].LibraryDotProduct >= dotProductThreshold);
            }
            Assert.AreNotEqual(0, missingResults);
            Assert.IsTrue(missingResults < docRefined.PeptideTransitionGroupCount);

            // Further refine with retention time refinement
            refineSettings.RTRegressionThreshold = 0.95;
            refineSettings.RTRegressionPrecision = 2;   // Backward compatibility
            var docRefinedRT = refineSettings.Refine(document);
            Assert.AreNotEqual(docRefined.PeptideCount, docRefinedRT.PeptideCount);
            // And peak count ratio
            refineSettings.MinPeakFoundRatio = 1.0;
            var docRefinedRatio = refineSettings.Refine(document);
            Assert.AreNotEqual(docRefinedRT.PeptideCount, docRefinedRatio.PeptideCount);
            Assert.IsTrue(ArrayUtil.EqualsDeep(docRefinedRatio.Children,
                refineSettings.Refine(docRefinedRT).Children));
            foreach (var nodeGroup in docRefinedRatio.PeptideTransitionGroups)
            {
                Assert.IsTrue(nodeGroup.HasResults);
                Assert.IsTrue(nodeGroup.HasLibInfo);
                Assert.AreEqual(1.0, nodeGroup.Results[0][0].PeakCountRatio);
            }
            Assert.AreEqual(2, docRefinedRatio.PeptideGroupCount);
            Assert.AreEqual(7, docRefinedRatio.PeptideTransitionGroupCount);

            // Pick only most intense transtions
            refineSettings.MaxPeakRank = 4;
            var docRefineMaxPeaks = refineSettings.Refine(document);
            Assert.AreEqual(28, docRefineMaxPeaks.PeptideTransitionCount);
            // Make sure the remaining peaks really started as the right rank,
            // and did not change.
            var dictIdTran = new Dictionary<int, TransitionDocNode>();
            foreach (var nodeTran in document.PeptideTransitions)
                dictIdTran.Add(nodeTran.Id.GlobalIndex, nodeTran);
            foreach (var nodeGroup in docRefineMaxPeaks.PeptideTransitionGroups)
            {
                Assert.AreEqual(refineSettings.MaxPeakRank, nodeGroup.TransitionCount);
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    int rank = nodeTran.Results[0][0].Rank;
                    Assert.IsTrue(rank <= refineSettings.MaxPeakRank);

                    var nodeTranOld = dictIdTran[nodeTran.Id.GlobalIndex];
                    Assert.AreEqual(nodeTranOld.Results[0][0].Rank, nodeTran.Results[0][0].Rank);
                }
            }

            // Pick only most intenst peptides
            refineSettings = new RefinementSettings { MaxPepPeakRank = 5 };
            var docRefinePepMaxPeaks = refineSettings.Refine(document);
            // 4 groups, one unmeasured and one with only 3 peptides
            Assert.AreEqual(13, docRefinePepMaxPeaks.PeptideCount);
            Assert.AreEqual(docRefinePepMaxPeaks.PeptideCount, docRefinePepMaxPeaks.PeptideTransitionGroupCount);

            // Add heavy labeled precursors for everything
            var settingsNew = docRefineMaxPeaks.Settings.ChangeTransitionFilter(f => f.ChangeAutoSelect(false));
            settingsNew = settingsNew.ChangePeptideModifications(m => m.ChangeHeavyModifications(new[]
                {
                    new StaticMod("13C K", "K", ModTerminus.C, null, LabelAtoms.C13, null, null),
                    new StaticMod("13C R", "R", ModTerminus.C, null, LabelAtoms.C13, null, null),
                }));
            var docPrepareAdd = docRefineMaxPeaks.ChangeSettings(settingsNew);
            refineSettings = new RefinementSettings {RefineLabelType = IsotopeLabelType.heavy, AddLabelType = true};
            var docHeavy = refineSettings.Refine(docPrepareAdd);
            Assert.AreEqual(docRefineMaxPeaks.PeptideTransitionCount*2, docHeavy.PeptideTransitionCount);
            // Verify that the precursors were added with the right transitions
            foreach (var nodePep in docHeavy.Peptides)
            {
                Assert.AreEqual(2, nodePep.Children.Count);
                var lightGroup = (TransitionGroupDocNode) nodePep.Children[0];
                Assert.AreEqual(IsotopeLabelType.light, lightGroup.TransitionGroup.LabelType);
                var heavyGroup = (TransitionGroupDocNode)nodePep.Children[1];
                Assert.AreEqual(IsotopeLabelType.heavy, heavyGroup.TransitionGroup.LabelType);
                Assert.AreEqual(lightGroup.TransitionGroup.PrecursorCharge,
                    heavyGroup.TransitionGroup.PrecursorCharge);
                Assert.AreEqual(lightGroup.Children.Count, heavyGroup.Children.Count);
                for (int i = 0; i < lightGroup.Children.Count; i++)
                {
                    var lightTran = (TransitionDocNode) lightGroup.Children[i];
                    var heavyTran = (TransitionDocNode) heavyGroup.Children[i];
                    Assert.AreEqual(lightTran.Transition.FragmentIonName, heavyTran.Transition.FragmentIonName);
                }
            }
            testFilesDir.Dispose();
        }

        [TestMethod]
        public void RefineConvertToSmallMoleculesTest()
        {
            // Exercise the code that helps match heavy labeled ion formulas with unlabled
            Assert.AreEqual("C5H12NO2S", BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula("C5H9H'3NO2S"));
            Assert.IsNull(BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(""));
            Assert.IsNull(BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(null));

            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestA\Refine.zip");

            var document = InitRefineDocument(testFilesDir);
            var refineSettings = new RefinementSettings();
            var converted = refineSettings.ConvertToSmallMolecules(document);
            AssertEx.ConvertedSmallMoleculeDocumentIsSimilar(document, converted);
        }

        [TestMethod]
        public void RefineConvertToSmallMoleculeMassesTest()
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestA\Refine.zip");

            var document = InitRefineDocument(testFilesDir);
            var refineSettings = new RefinementSettings();
            var converted = refineSettings.ConvertToSmallMolecules(document, RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
            AssertEx.ConvertedSmallMoleculeDocumentIsSimilar(document, converted);
        }

        [TestMethod]
        public void RefineConvertToSmallMoleculeMassesAndNamesTest()
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestA\Refine.zip");

            var document = InitRefineDocument(testFilesDir);
            var refineSettings = new RefinementSettings();
            var converted = refineSettings.ConvertToSmallMolecules(document, RefinementSettings.ConvertToSmallMoleculesMode.masses_and_names);
            AssertEx.ConvertedSmallMoleculeDocumentIsSimilar(document, converted);
        }

        private static SrmDocument InitRefineDocument(TestFilesDir testFilesDir)
        {
            string docPath = testFilesDir.GetTestPath("SRM_mini.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 4, 36, 38, 334);
            return doc;
        }
    }
}