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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    /// <summary>
    /// Summary description for RefineTest
    /// </summary>
    [TestClass]
    public class CommandLineRefineTest : AbstractUnitTestEx
    {
        [TestMethod]
        public void ConsoleRefineDocumentTest()
        {
            string documentPath = InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.none);
            string outPath = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, "test.sky");

            // First check a few refinements which should not change the document
            int minPeptides = 1;
            string output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(minPeptides.ToString()),
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, Resources.CommandLine_RefineDocument_Refining_document___, Resources.CommandLine_LogNewEntries_Document_unchanged);
            int minTrans = TestSmallMolecules ? 1 : 2;
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_MIN_TRANSITIONS.GetArgumentTextWithValue(minTrans.ToString()),
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, Resources.CommandLine_RefineDocument_Refining_document___, Resources.CommandLine_LogNewEntries_Document_unchanged);

            // Remove the protein with only 3 peptides
            minPeptides = 4;
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(minPeptides.ToString()),
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_MinPeptidesPerProtein, minPeptides.ToString());
            IsDocumentState(outPath, 3, 33, 35, 301);

            // Remove the precursor with only 2 transitions
            minTrans = 3;
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_MIN_TRANSITIONS.GetArgumentTextWithValue(minTrans.ToString()),
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_MinTransitionsPepPrecursor, minTrans.ToString());
            IsDocumentState(outPath, 5, 35, 37, 332);

            // Remove the heavy precursor
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_LABEL_TYPE.GetArgumentTextWithValue(IsotopeLabelType.heavy.ToString()),
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_RefineLabelType, IsotopeLabelType.heavy.AuditLogText);
            IsDocumentState(outPath, 5, 37, 39, 334);
            // Remove everything but the heavy precursor
            minPeptides = 1;
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(minPeptides.ToString()),
                CommandArgs.ARG_REFINE_LABEL_TYPE.GetArgumentTextWithValue(IsotopeLabelType.light.ToString()),
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings,
                PropertyNames.RefinementSettings_MinPeptidesPerProtein, minPeptides.ToString(),
                PropertyNames.RefinementSettings_RefineLabelType, IsotopeLabelType.light.AuditLogText);
            IsDocumentState(outPath, 1, 1, 1, 4);
            // Add back the light for the one remaining peptide
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(outPath),
                CommandArgs.ARG_REFINE_LABEL_TYPE.GetArgumentTextWithValue(IsotopeLabelType.light.ToString()),
                CommandArgs.ARG_REFINE_ADD_LABEL_TYPE.ArgumentText,
                CommandArgs.ARG_SAVE.ArgumentText);
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_RefineLabelType,
                PropertyNames.RefinementSettings_AddLabelType, IsotopeLabelType.light.AuditLogText);
            IsDocumentState(outPath, 1, 1, 2, 8);
            // Perform the operation again without protein removal
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_LABEL_TYPE.GetArgumentTextWithValue(IsotopeLabelType.light.ToString()),
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_RefineLabelType, IsotopeLabelType.light.AuditLogText);
            IsDocumentState(outPath, 5, 1, 1, 4);
            // Remove repeated peptides
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_REMOVE_REPEATS.ArgumentText,
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_RemoveRepeatedPeptides);
            IsDocumentState(outPath, 5, 35, 38, 332);
            // Remove duplicate peptides
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_REMOVE_DUPLICATES.ArgumentText,
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_RemoveDuplicatePeptides);
            IsDocumentState(outPath, 5, 34, 36, 324);
            // Remove missing library
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_MISSING_LIBRARY.ArgumentText,
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_RemoveMissingLibrary);
            IsDocumentState(outPath, 5, 18, 18, 176);

            // Try settings that remove everything from the document
            minPeptides = 20;
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(minPeptides.ToString()),
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_MinPeptidesPerProtein, minPeptides.ToString());
            IsDocumentState(outPath, 0, 0, 0, 0);
            minPeptides = 1;
            minTrans = 20;
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(minPeptides.ToString()),
                CommandArgs.ARG_REFINE_MIN_TRANSITIONS.GetArgumentTextWithValue(minTrans.ToString()),
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings,
                PropertyNames.RefinementSettings_MinPeptidesPerProtein, minPeptides.ToString(),
                PropertyNames.RefinementSettings_MinTransitionsPepPrecursor, minTrans.ToString());
            IsDocumentState(outPath, 0, 0, 0, 0);

            // Refine to autoselection
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_AUTOSEL_TRANSITIONS.ArgumentText,
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_AutoPickTransitionsAll);
            IsDocumentState(outPath, 5, 37, 40, 354);
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_AUTOSEL_PRECURSORS.ArgumentText,
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_AutoPickPrecursorsAll);
            IsDocumentState(outPath, 5, 37, 38, 331);
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(documentPath),
                CommandArgs.ARG_REFINE_AUTOSEL_TRANSITIONS.ArgumentText,
                CommandArgs.ARG_REFINE_AUTOSEL_PRECURSORS.ArgumentText,
                CommandArgs.ARG_REFINE_AUTOSEL_PEPTIDES.ArgumentText,
                CommandArgs.ARG_OUT.GetArgumentTextWithValue(outPath));
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_AutoPickTransitionsAll,
                PropertyNames.RefinementSettings_AutoPickPrecursorsAll, PropertyNames.RefinementSettings_AutoPickPeptidesAll);
            IsDocumentState(outPath, 5, 61, 62, 529);
        }

        //        [TestMethod]
        public void ConsoleRefineResultsTest()
        {
            Settings.Default.RTCalculatorName = Settings.Default.RTScoreCalculatorList.GetDefaults().First().Name;

            var document = InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.none);

//            // First check a few refinements which should not change the document
//            var refineSettings = new RefinementSettings {RTRegressionThreshold = 0.3};
//            Assert.AreSame(document, refineSettings.Refine(document));
//            refineSettings.RTRegressionThreshold = null;
//            refineSettings.DotProductThreshold = Statistics.AngleToNormalizedContrastAngle(0.1);    // Convert form original cos(angle) dot-product
//            Assert.AreSame(document, refineSettings.Refine(document));
//            refineSettings.DotProductThreshold = null;
//            refineSettings.MinPeakFoundRatio = 0;
//            refineSettings.MaxPeakFoundRatio = 1.0;
//            Assert.AreSame(document, refineSettings.Refine(document));
//            refineSettings.MinPeakFoundRatio = refineSettings.MaxPeakFoundRatio = null;
//            // refineSettings.MaxPeakRank = 15;  This will remove unmeasured transitions
//            Assert.AreSame(document, refineSettings.Refine(document));
//
//            // Remove nodes without results
//            refineSettings.MinPeptidesPerProtein = 1;
//            refineSettings.RemoveMissingResults = true;
//            var docRefined = refineSettings.Refine(document);
//            Assert.AreEqual(document.PeptideGroupCount, docRefined.PeptideGroupCount);
//            // First three children should be unchanged
//            for (int i = 0; i < 3; i++)
//                Assert.AreSame(document.Children[i], docRefined.Children[i]);
//            var nodePepGroupRefined = (PeptideGroupDocNode) docRefined.Children[3];
//            Assert.AreEqual(1, nodePepGroupRefined.MoleculeCount);
//            Assert.AreEqual(1, nodePepGroupRefined.TransitionGroupCount);
//            Assert.AreEqual(5, nodePepGroupRefined.TransitionCount);
//
//            // Filter for dot product, ignoring nodes without results
//            refineSettings.RemoveMissingResults = false;
//            double dotProductThreshold = Statistics.AngleToNormalizedContrastAngle(0.9);    // Convert form original cos(angle) dot-product
//            refineSettings.DotProductThreshold = dotProductThreshold;
//            docRefined = refineSettings.Refine(document);
//            int missingResults = 0;
//            foreach (var nodeGroup in docRefined.PeptideTransitionGroups)
//            {
//                if (!nodeGroup.HasResults || nodeGroup.Results[0].IsEmpty)
//                    missingResults++;
//                else
//                    Assert.IsTrue(nodeGroup.Results[0][0].LibraryDotProduct >= dotProductThreshold);
//            }
//            Assert.AreNotEqual(0, missingResults);
//            Assert.IsTrue(missingResults < docRefined.PeptideTransitionGroupCount);
//
//            // Further refine with retention time refinement
//            refineSettings.RTRegressionThreshold = 0.95;
//            refineSettings.RTRegressionPrecision = 2;   // Backward compatibility
//            var docRefinedRT = refineSettings.Refine(document);
//            Assert.AreNotEqual(docRefined.PeptideCount, docRefinedRT.PeptideCount);
//            // And peak count ratio
//            refineSettings.MinPeakFoundRatio = 1.0;
//            var docRefinedRatio = refineSettings.Refine(document);
//            Assert.AreNotEqual(docRefinedRT.PeptideCount, docRefinedRatio.PeptideCount);
//            Assert.IsTrue(ArrayUtil.EqualsDeep(docRefinedRatio.Children,
//                refineSettings.Refine(docRefinedRT).Children));
//            foreach (var nodeGroup in docRefinedRatio.PeptideTransitionGroups)
//            {
//                Assert.IsTrue(nodeGroup.HasResults);
//                Assert.IsTrue(nodeGroup.HasLibInfo);
//                Assert.AreEqual(1.0, nodeGroup.Results[0][0].PeakCountRatio);
//            }
//            Assert.AreEqual(2, docRefinedRatio.PeptideGroupCount);
//            Assert.AreEqual(7, docRefinedRatio.PeptideTransitionGroupCount);
//
//            // Pick only most intense transtions
//            refineSettings.MaxPeakRank = 4;
//            var docRefineMaxPeaks = refineSettings.Refine(document);
//            Assert.AreEqual(28, docRefineMaxPeaks.PeptideTransitionCount);
//            // Make sure the remaining peaks really started as the right rank,
//            // and did not change.
//            var dictIdTran = new Dictionary<int, TransitionDocNode>();
//            foreach (var nodeTran in document.PeptideTransitions)
//                dictIdTran.Add(nodeTran.Id.GlobalIndex, nodeTran);
//            foreach (var nodeGroup in docRefineMaxPeaks.PeptideTransitionGroups)
//            {
//                Assert.AreEqual(refineSettings.MaxPeakRank, nodeGroup.TransitionCount);
//                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
//                {
//                    int rank = nodeTran.Results[0][0].Rank;
//                    Assert.IsTrue(rank <= refineSettings.MaxPeakRank);
//
//                    var nodeTranOld = dictIdTran[nodeTran.Id.GlobalIndex];
//                    Assert.AreEqual(nodeTranOld.Results[0][0].Rank, nodeTran.Results[0][0].Rank);
//                }
//            }
//
//            // Pick only most intenst peptides
//            refineSettings = new RefinementSettings { MaxPepPeakRank = 5 };
//            var docRefinePepMaxPeaks = refineSettings.Refine(document);
//            // 4 groups, one unmeasured and one with only 3 peptides
//            Assert.AreEqual(13, docRefinePepMaxPeaks.PeptideCount);
//            Assert.AreEqual(docRefinePepMaxPeaks.PeptideCount, docRefinePepMaxPeaks.PeptideTransitionGroupCount);
//
//            // Add heavy labeled precursors for everything
//            var settingsNew = docRefineMaxPeaks.Settings.ChangeTransitionFilter(f => f.ChangeAutoSelect(false));
//            settingsNew = settingsNew.ChangePeptideModifications(m => m.ChangeModifications(IsotopeLabelType.heavy, new[]
//            {
//                new StaticMod("13C K", "K", ModTerminus.C, null, LabelAtoms.C13, null, null),
//                new StaticMod("13C R", "R", ModTerminus.C, null, LabelAtoms.C13, null, null),
//            }));
//            var docPrepareAdd = docRefineMaxPeaks.ChangeSettings(settingsNew);
//            refineSettings = new RefinementSettings {RefineLabelType = IsotopeLabelType.heavy, AddLabelType = true};
//            var docHeavy = refineSettings.Refine(docPrepareAdd);
//            Assert.AreEqual(docRefineMaxPeaks.PeptideTransitionCount*2, docHeavy.PeptideTransitionCount);
//            // Verify that the precursors were added with the right transitions
//            foreach (var nodePep in docHeavy.Peptides)
//            {
//                Assert.AreEqual(2, nodePep.Children.Count);
//                var lightGroup = (TransitionGroupDocNode) nodePep.Children[0];
//                Assert.AreEqual(IsotopeLabelType.light, lightGroup.TransitionGroup.LabelType);
//                var heavyGroup = (TransitionGroupDocNode)nodePep.Children[1];
//                Assert.AreEqual(IsotopeLabelType.heavy, heavyGroup.TransitionGroup.LabelType);
//                Assert.AreEqual(lightGroup.TransitionGroup.PrecursorAdduct,
//                    heavyGroup.TransitionGroup.PrecursorAdduct);
//                Assert.AreEqual(lightGroup.Children.Count, heavyGroup.Children.Count);
//                for (int i = 0; i < lightGroup.Children.Count; i++)
//                {
//                    var lightTran = (TransitionDocNode) lightGroup.Children[i];
//                    var heavyTran = (TransitionDocNode) heavyGroup.Children[i];
//                    Assert.AreEqual(lightTran.Transition.FragmentIonName, heavyTran.Transition.FragmentIonName);
//                }
//            }
        }

//        [TestMethod]
        public void ConsoleRefineConvertToSmallMoleculesTest()
        {
            // Exercise the code that helps match heavy labeled ion formulas with unlabled
            Assert.AreEqual("C5H12NO2S", BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula("C5H9H'3NO2S"));
            Assert.IsNull(BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(""));
            Assert.IsNull(BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(null));

            InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

//        [TestMethod]
        public void ConsoleRefineConvertToSmallMoleculeMassesTest()
        {
            InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
        }

//        [TestMethod]
        public void ConsoleRefineConvertToSmallMoleculeMassesAndNamesTest()
        {
            InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.masses_and_names);
        }

        private string InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode mode)
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineRefine.zip", mode.ToString());
            string docPath = testFilesDir.GetTestPath("SRM_mini_single_replicate.sky");            
//            if (mode != RefinementSettings.ConvertToSmallMoleculesMode.none)
//            {
//                var dataPaths = new[] { testFilesDir.GetTestPath("worm1.mzML") };
//                doc = ConvertToSmallMolecules(null, ref docPath, dataPaths, mode);
//            }
            IsDocumentState(docPath, 5, 37, 40, 338);
            return docPath;
        }

        private void IsDocumentState(string docPath, int? groups, int? peptides,
            int? tranGroups, int? transitions)
        {
            var doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, null, groups, peptides, tranGroups, transitions);
        }
    }
}