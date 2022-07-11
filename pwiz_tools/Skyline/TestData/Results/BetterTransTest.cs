/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Tests a case where a set of transitions with further precursor m/z has better transition matching
    /// than one with closer precursor m/z.
    /// https://skyline.ms/announcements/home/support/thread.view?rowId=30408
    /// </summary>
    [TestClass]
    public class BetterTransTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\BetterTransTest.zip";

        [TestMethod]
        public void BetterTransitionMatchingTest()
        {
            DoAsymmetricIsolationTest(RefinementSettings.ConvertToSmallMoleculesMode.none);
        }

        [TestMethod]
        public void BetterTransitionMatchingTestAsSmallMolecules()
        {
            DoAsymmetricIsolationTest(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        [TestMethod]
        public void BetterTransitionMatchingTestAsSmallMoleculeMasses()
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
            string docPath = testFilesDir.GetTestPath("TROUBLED_File.sky");
            string cachePath = ChromatogramCache.FinalPathForName(docPath, null);
            FileEx.SafeDelete(cachePath);
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            var refine = new RefinementSettings();
            doc = refine.ConvertToSmallMolecules(doc, testFilesDir.FullPath, asSmallMolecules);
            const int expectedMoleculeCount = 1;   // At first small molecules did not support multiple label types
            AssertEx.IsDocumentState(doc, null, 1, expectedMoleculeCount, 2, 6);

            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                // Import the first RAW file (or mzML for international)
                string rawPath = testFilesDir.GetTestPath("Rush_p3_96_21May16_Smeagol.mzML");
                var measuredResults = new MeasuredResults(new[] {new ChromatogramSet("Single", new[] {rawPath})});

                {
                    // Import with symmetric isolation window
                    var docResults =
                        docContainer.ChangeMeasuredResults(measuredResults, expectedMoleculeCount, 1, 1, 3, 3);
                    var nodeGroup = docResults.MoleculeTransitionGroups.First();
                    var normalizedValueCalculator = new NormalizedValueCalculator(docResults);
                    double ratio = normalizedValueCalculator.GetTransitionGroupValue(normalizedValueCalculator.GetFirstRatioNormalizationMethod(), 
                        docResults.Molecules.First(), nodeGroup, nodeGroup.Results[0][0]).GetValueOrDefault();
                    // The expected ratio is 1.0, but the symmetric isolation window should produce poor results
                    if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.masses_only) // Can't use labels without a formula
                        Assert.AreEqual(0.008, ratio, 0.001);
                }
            }

            testFilesDir.Dispose();
        }
    }
}