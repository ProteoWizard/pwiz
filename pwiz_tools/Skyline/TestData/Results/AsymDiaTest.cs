/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Summary description for AsymDiaTest
    /// </summary>
    [TestClass]
    public class AsymDiaTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\AsymDIA.zip";

        [TestMethod]
        public void AsymmetricIsolationTest()
        {
            DoAsymmetricIsolationTest(RefinementSettings.ConvertToSmallMoleculesMode.none);
        }

        [TestMethod]
        public void AsymmetricIsolationTestAsSmallMolecules()
        {
            DoAsymmetricIsolationTest(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        [TestMethod]
        public void AsymmetricIsolationTestAsSmallMoleculeMasses()
        {
            DoAsymmetricIsolationTest(RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
        }

        public void DoAsymmetricIsolationTest(RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules)
        {
            if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none && SkipSmallMoleculeTestVersions())
            {
                return;
            }

            LocalizationHelper.InitThread();    // TODO: All unit tests should be correctly initialized

            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("Asym_DIA.sky");
            string cachePath = ChromatogramCache.FinalPathForName(docPath, null);
            FileEx.SafeDelete(cachePath);
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            var refine = new RefinementSettings();
            doc = refine.ConvertToSmallMolecules(doc, testFilesDir.FullPath, asSmallMolecules);
            const int expectedMoleculeCount = 1;   // At first small molecules did not support multiple charge states, and this was 2 for that test mode
            AssertEx.IsDocumentState(doc, null, 1, expectedMoleculeCount, 2, 4);
            var fullScanInitial = doc.Settings.TransitionSettings.FullScan;
            Assert.IsTrue(fullScanInitial.IsEnabledMsMs);
            Assert.AreEqual(FullScanAcquisitionMethod.DIA, fullScanInitial.AcquisitionMethod);
            Assert.AreEqual(25, fullScanInitial.PrecursorFilter);
            AssertEx.Serializable(doc);
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                // Import the first RAW file (or mzML for international)
                string rawPath = testFilesDir.GetTestPath("Asym_DIA_data.mzML");
                var measuredResults = new MeasuredResults(new[] { new ChromatogramSet("Single", new[] { rawPath }) });
                TransitionGroupDocNode nodeGroup;
                double ratio;

                const double poorRatio = 0.25;
                const double fixedRatio = 1.05;
                {
                    // Import with symmetric isolation window
                    SrmDocument docResults = docContainer.ChangeMeasuredResults(measuredResults, expectedMoleculeCount, 1, 1, 2, 2);
                    ratio = GetFirstTransitionGroupRatio(docResults).Value;
                    // The expected ratio is 1.0, but the symmetric isolation window should produce poor results
                    if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)  // Can't use labels without a formula
                        Assert.AreEqual(poorRatio, ratio, 0.05);

                    // Revert to original document, and get rid of results cache
                    Assert.IsTrue(docContainer.SetDocument(doc, docResults, false));
                    FileEx.SafeDelete(testFilesDir.GetTestPath("Asym_DIA.skyd"));
                }

                {
                    // Import with asymmetric isolation window
                    SrmDocument docAsym = doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fullScan =>
                        fullScan.ChangeAcquisitionMethod(fullScan.AcquisitionMethod, new IsolationScheme("Test asym", 5, 20))));
                    AssertEx.Serializable(docAsym);
                    Assert.IsTrue(docContainer.SetDocument(docAsym, doc, false));

                    SrmDocument docResults = docContainer.ChangeMeasuredResults(measuredResults, expectedMoleculeCount, 1, 1, 2, 2);
                    ratio = GetFirstTransitionGroupRatio(docResults).Value;
                    // Asymmetric should be a lot closer to 1.0
                    if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)  // Can't use labels without a formula
                        Assert.AreEqual(fixedRatio, ratio, 0.05);

                    // Revert to original document, and get rid of results cache
                    Assert.IsTrue(docContainer.SetDocument(doc, docResults, false));
                    FileEx.SafeDelete(testFilesDir.GetTestPath("Asym_DIA.skyd"));
                }

                {
                    // Import with prespecified isolation windows
                    var windowList = new List<IsolationWindow>
                    {
                        new IsolationWindow(999.2702214, 1024.270221),
                        new IsolationWindow(1024.27267, 1049.27267)
                    };
                    SrmDocument docPrespecified = doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fullScan =>
                        fullScan.ChangeAcquisitionMethod(fullScan.AcquisitionMethod, new IsolationScheme("Test prespecified", windowList))));
                    AssertEx.Serializable(docPrespecified);
                    Assert.IsTrue(docContainer.SetDocument(docPrespecified, doc, false));

                    SrmDocument docResults = docContainer.ChangeMeasuredResults(measuredResults, expectedMoleculeCount, 1, 1, 2, 2);
                    nodeGroup = docResults.MoleculeTransitionGroups.First();
                    var normalizedValueCalculator = new NormalizedValueCalculator(docResults);
                    ratio = normalizedValueCalculator.GetTransitionGroupValue(
                        normalizedValueCalculator.GetFirstRatioNormalizationMethod(),
                        docResults.Molecules.First(), nodeGroup, nodeGroup.Results[0][0]).Value;
                    // Asymmetric should be a lot closer to 1.0
                    if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)  // Can't use labels without a formula
                        Assert.AreEqual(fixedRatio, ratio, 0.05);

                    // Revert to original document, and get rid of results cache
                    Assert.IsTrue(docContainer.SetDocument(doc, docResults, false));
                    FileEx.SafeDelete(testFilesDir.GetTestPath("Asym_DIA.skyd"));
                }

                {
                    // Import with prespecified targets
                    var windowList = new List<IsolationWindow>
                    {
                        new IsolationWindow(999.2702214, 1024.270221, 1004.27),
                        new IsolationWindow(1024.27267, 1049.27267, 1029.27)
                    };
                    SrmDocument docPrespecified = doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fullScan =>
                        fullScan.ChangeAcquisitionMethod(fullScan.AcquisitionMethod, new IsolationScheme("Test target", windowList))));
                    AssertEx.Serializable(docPrespecified);
                    Assert.IsTrue(docContainer.SetDocument(docPrespecified, doc, false));

                    SrmDocument docResults = docContainer.ChangeMeasuredResults(measuredResults, expectedMoleculeCount, 1, 1, 2, 2);
                    ratio = GetFirstTransitionGroupRatio(docResults).Value;
                    // Asymmetric should be a lot closer to 1.0
                    if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)  // Can't use labels without a formula
                        Assert.AreEqual(fixedRatio, ratio, 0.05);

                    // Revert to original document, and get rid of results cache
                    Assert.IsTrue(docContainer.SetDocument(doc, docResults, false));
                    FileEx.SafeDelete(testFilesDir.GetTestPath("Asym_DIA.skyd"));
                }

                {
                    // Import with ambiguous prespecified targets
                    var windowList = new List<IsolationWindow>
                    {
                        new IsolationWindow(999.2702214, 1024.270221, 1004.27),
                        new IsolationWindow(1000.0, 1049.27267, 1004.28)
                    };
                    SrmDocument docAmbiguous = doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fullScan =>
                        fullScan.ChangeAcquisitionMethod(fullScan.AcquisitionMethod, new IsolationScheme("Test ambiguous", windowList))));
                    AssertEx.Serializable(docAmbiguous);
                    Assert.IsTrue(docContainer.SetDocument(docAmbiguous, doc, false));

                    try
                    {
                        docContainer.ChangeMeasuredResults(measuredResults, expectedMoleculeCount, 1, 1, 2, 2);
                        Assert.Fail("Expected ambiguous isolation targets.");
                    }
                    catch (Exception x)
                    {
                        AssertEx.AreComparableStrings(Resources.SpectrumFilter_FindFilterPairs_Two_isolation_windows_contain_targets_which_match_the_isolation_target__0__, x.Message, 1);
                    }

                    // Revert to original document, and get rid of results cache
                    Assert.IsTrue(docContainer.SetDocument(doc, docContainer.Document, false));
                    FileEx.SafeDelete(testFilesDir.GetTestPath("Asym_DIA.skyd"));
                }

                {
                    // Import with one isolation window, so one result is discarded.
                    var windowList = new List<IsolationWindow>
                    {
                        new IsolationWindow(999.2702214, 1024.270221),
                    };
                    SrmDocument docOneWindow = doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fullScan =>
                        fullScan.ChangeAcquisitionMethod(fullScan.AcquisitionMethod, new IsolationScheme("Test one window", windowList))));
                    AssertEx.Serializable(docOneWindow);
                    Assert.IsTrue(docContainer.SetDocument(docOneWindow, doc, false));

                    SrmDocument docResults = docContainer.ChangeMeasuredResults(measuredResults, 1, 1, 0, 2, 0);
                    Assert.IsNull(GetFirstTransitionGroupRatio(docResults));

                    // Revert to original document, and get rid of results cache
                    Assert.IsTrue(docContainer.SetDocument(doc, docResults, false));
                    FileEx.SafeDelete(testFilesDir.GetTestPath("Asym_DIA.skyd"));
                }
            }

            testFilesDir.Dispose();
        }

        private double? GetFirstTransitionGroupRatio(SrmDocument document)
        {
            var normalizedValueCalculator = new NormalizedValueCalculator(document);
            var normalizationMethod = normalizedValueCalculator.GetFirstRatioNormalizationMethod();
            Assert.IsInstanceOfType(normalizationMethod, typeof(NormalizationMethod.RatioToLabel));
            var peptideDocNode = document.Molecules.First();
            var transitionGroupDocNode = peptideDocNode.TransitionGroups.First();
            return normalizedValueCalculator.GetTransitionGroupValue(normalizationMethod, peptideDocNode,
                transitionGroupDocNode, transitionGroupDocNode.Results[0][0]);
        }
    }
}