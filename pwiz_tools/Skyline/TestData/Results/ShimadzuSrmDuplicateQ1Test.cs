/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Regression test for "Times (390) and intensities (779) disagree in point count"
    /// when importing a Shimadzu MRM file whose method has two acquisition events
    /// at the same Q1 but with disjoint Q3 sets — e.g., Wesley Vermaelen's steroid
    /// hormone panel where Deoxycorticosterone (DOC) and 17α-hydroxyprogesterone
    /// both sit at Q1 = 331.2 but are scheduled as separate events with non-
    /// overlapping product-ion sets.
    ///
    /// Without the fix in <see cref="ChromCollector"/>.AddPoint / FillZeroes,
    /// SpectraChromDataProvider's "missing-ion" trailing zero-fill in
    /// ProcessExtractedSpectrum would append intensities to the unique-Q3
    /// collectors without matching times (each ChromCollector owns its own
    /// times in single-time SRM mode), desyncing the per-transition arrays
    /// and aborting import.
    ///
    /// Reported on
    /// https://skyline.ms/home/support/announcements-thread.view?rowId=66356
    /// </summary>
    [TestClass]
    public class ShimadzuSrmDuplicateQ1Test : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\ShimadzuSrmDuplicateQ1.zip";

        [TestMethod]
        public void ShimadzuSrmDuplicateQ1ImportTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string docPath = TestFilesDir.GetTestPath("Wesley.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);

            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                const string replicateName = "WesleyLCD";
                string extRaw = ExtensionTestContext.ExtShimadzuRaw;
                string dataPath = TestFilesDir.GetTestPath("Labsolutions_PackA_MA_alt" + extRaw);
                var chromSets = new[]
                {
                    new ChromatogramSet(replicateName, new[]
                        { new MsDataFilePath(dataPath) }),
                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                // Without the fix, AssertComplete throws InvalidDataException with
                // "Times (390) and intensities (779) disagree in point count".
                docContainer.AssertComplete();
            }
        }
    }
}
