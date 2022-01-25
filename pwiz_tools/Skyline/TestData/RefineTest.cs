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

namespace pwiz.SkylineTestData
{
    /// <summary>
    /// Summary description for RefineTest
    /// </summary>
    [TestClass]
    public class RefineTest : AbstractUnitTestEx
    {
        [TestMethod]
        public void RefineDocumentTest()
        {
            var document = InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.none);

            // First check a few refinements which should not change the document
            var refineSettings = new RefinementSettings();
            Assert.AreSame(document, refineSettings.Refine(document));
            refineSettings.MinPeptidesPerProtein = 3;
            Assert.AreSame(document, refineSettings.Refine(document));
            refineSettings.MinTransitionsPepPrecursor = 2;
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
            AssertEx.IsDocumentState(docRefined, null, 1, 1, 1, 4);
            // Perform the operation again without protein removal
            refineSettings.MinPeptidesPerProtein = null;
            docRefined = refineSettings.Refine(document);
            AssertEx.IsDocumentState(docRefined, null, 4, 1, 1, 4);
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

        }

        [TestMethod]
        public void RefineResultsTest()
        {
            Settings.Default.RTCalculatorName = Settings.Default.RTScoreCalculatorList.GetDefaults().First().Name;

            var document = InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.none);

            // First check a few refinements which should not change the document
            var refineSettings = new RefinementSettings { RTRegressionThreshold = 0.3 };
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
            var nodePepGroupRefined = (PeptideGroupDocNode)docRefined.Children[3];
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
                if (!nodeGroup.HasResults || nodeGroup.Results[0].IsEmpty)
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
            settingsNew = settingsNew.ChangePeptideModifications(m => m.ChangeModifications(IsotopeLabelType.heavy, new[]
                {
                    new StaticMod("13C K", "K", ModTerminus.C, null, LabelAtoms.C13, null, null),
                    new StaticMod("13C R", "R", ModTerminus.C, null, LabelAtoms.C13, null, null),
                }));
            var docPrepareAdd = docRefineMaxPeaks.ChangeSettings(settingsNew);
            refineSettings = new RefinementSettings { RefineLabelType = IsotopeLabelType.heavy, AddLabelType = true };
            var docHeavy = refineSettings.Refine(docPrepareAdd);
            Assert.AreEqual(docRefineMaxPeaks.PeptideTransitionCount * 2, docHeavy.PeptideTransitionCount);
            // Verify that the precursors were added with the right transitions
            foreach (var nodePep in docHeavy.Peptides)
            {
                Assert.AreEqual(2, nodePep.Children.Count);
                var lightGroup = (TransitionGroupDocNode)nodePep.Children[0];
                Assert.AreEqual(IsotopeLabelType.light, lightGroup.TransitionGroup.LabelType);
                var heavyGroup = (TransitionGroupDocNode)nodePep.Children[1];
                Assert.AreEqual(IsotopeLabelType.heavy, heavyGroup.TransitionGroup.LabelType);
                Assert.AreEqual(lightGroup.TransitionGroup.PrecursorAdduct,
                    heavyGroup.TransitionGroup.PrecursorAdduct);
                Assert.AreEqual(lightGroup.Children.Count, heavyGroup.Children.Count);
                for (int i = 0; i < lightGroup.Children.Count; i++)
                {
                    var lightTran = (TransitionDocNode)lightGroup.Children[i];
                    var heavyTran = (TransitionDocNode)heavyGroup.Children[i];
                    Assert.AreEqual(lightTran.Transition.FragmentIonName, heavyTran.Transition.FragmentIonName);
                }
            }
            // Pick only the precursors with the max peak area
            document = InitRefineDocumentIprg();
            refineSettings = new RefinementSettings { MaxPrecursorPeakOnly = true };
            var docRefineMaxPrecursorPeakOnly = refineSettings.Refine(document);
            VerifyPrecursorOnlyNodeCounts(docRefineMaxPrecursorPeakOnly, 4, 12);
            VerifyChargeCounts(docRefineMaxPrecursorPeakOnly, 2, 2);

            // Pick only the precursors with the max peak area, do not ignore standard types
            document = InitRefineDocumentSprg();
            docRefineMaxPrecursorPeakOnly = refineSettings.Refine(document);
            VerifyPrecursorOnlyNodeCounts(docRefineMaxPrecursorPeakOnly, 3, 27);
            VerifyChargeCounts(docRefineMaxPrecursorPeakOnly, 1, 2);
        }

        private void VerifyPrecursorOnlyNodeCounts(SrmDocument doc, int peptides, int transitions)
        {
            Assert.AreEqual(peptides, doc.PeptideCount);
            Assert.AreEqual(doc.PeptideCount, doc.PeptideTransitionGroupCount);
            Assert.AreEqual(transitions, doc.PeptideTransitionCount);
        }

        private void VerifyChargeCounts(SrmDocument doc, int charge2, int charge3)
        {
            var chargeCounts =
                (from g in doc.PeptideTransitionGroups
                    group g by g.TransitionGroup.PrecursorAdduct.Unlabeled
                    into gc
                    select new { Adduct = gc.Key.Unlabeled.AdductCharge, Count = gc.Count() }).ToArray();
            Assert.IsTrue(chargeCounts.Contains((x) => x.Adduct == 2 && x.Count == charge2));
            Assert.IsTrue(chargeCounts.Contains((x) => x.Adduct == 3 && x.Count == charge3));
        }

        [TestMethod]
        public void RefineConvertToSmallMoleculesTest()
        {
            // Exercise the code that helps match heavy labeled ion formulas with unlabled
            Assert.AreEqual("C5H12NO2S", BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula("C5H9H'3NO2S")); // Partially labeled
            Assert.AreEqual("C5H12NO2S", BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula("C'5H'9H3NO\"2S'")); // Completely labeled
            Assert.AreEqual("C5H14NO2STiDb", BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula("C5H9D2H'H\"TNO2STiDb"));
            Assert.IsNull(BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(""));
            Assert.IsNull(BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(null));

            InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        [TestMethod]
        public void RefineConvertToSmallMoleculeMassesTest()
        {
            InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
        }

        [TestMethod]
        public void RefineConvertToSmallMoleculeMassesAndNamesTest()
        {
            InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.masses_and_names);
        }

        private SrmDocument InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode mode)
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestData\Refine.zip", mode.ToString());
            if (mode == RefinementSettings.ConvertToSmallMoleculesMode.none)
            {
                var doc = ResultsUtil.DeserializeDocument(testFilesDir.GetTestPath("SRM_mini.sky"));
                AssertEx.IsDocumentState(doc, null, 4, 36, 38, 334);
                return doc;
            }
            var docPath = testFilesDir.GetTestPath("SRM_mini_single_replicate.sky");
            var dataPaths = new[] { testFilesDir.GetTestPath("worm1.mzML") };
            var converted = ConvertToSmallMolecules(null, ref docPath, dataPaths, mode);
            AssertEx.IsDocumentState(converted, null, 4, 36, 38, 334);
            return converted;

        }

        private SrmDocument InitRefineDocumentIprg()
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestData\Refine.zip");
            var doc = ResultsUtil.DeserializeDocument(testFilesDir.GetTestPath("iPRG 2015 Study-mini.sky"));
            AssertEx.IsDocumentState(doc, null, 1, 4, 6, 18);
            return doc;
        }

        private SrmDocument InitRefineDocumentSprg()
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestData\Refine.zip");
            var doc = ResultsUtil.DeserializeDocument(testFilesDir.GetTestPath("sprg_all_charges-mini.sky"));
            AssertEx.IsDocumentState(doc, null, 1, 3, 6, 54);
            return doc;
        }
    }
}