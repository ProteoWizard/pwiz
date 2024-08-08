/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Test Shimadzu triple quadrupole SRM data type and import
    /// </summary>
    [TestClass]
    public class ShimadzuSrmTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\ShimadzuSrm.zip";

        [TestMethod]
        public void ShimadzuFileTypeTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string extRaw = ExtensionTestContext.ExtShimadzuRaw;

            // Do file type check
            using (var msData = new MsDataFileImpl(TestFilesDir.GetTestPath("BSA-digest__MRM_optimisation_SL_scheduled_001" + extRaw)))
            {
                Assert.IsTrue(msData.IsShimadzuFile);
                
                // check time is minutes
                msData.GetChromatogram(msData.ChromatogramCount - 1, out _, out float[] times, out _);
                Assert.AreEqual(new KeyValuePair<float, float>(5.908167f, 8.905833f), new KeyValuePair<float, float>(times.First(), times.Last()));
            }
        }

        [TestMethod]
        public void ShimadzuFormatsTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string docPath;
            SrmDocument doc = InitShimadzuDocument(TestFilesDir, out docPath);

            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                const string replicateName = "ShimadzuTest";
                string extRaw = ExtensionTestContext.ExtShimadzuRaw;
                var chromSets = new[]
                {
                    new ChromatogramSet(replicateName, new[]
                        { new MsDataFilePath(TestFilesDir.GetTestPath("BSA-digest__MRM_optimisation_SL_scheduled_001" + extRaw)),  }),
                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();
                docResults = docContainer.Document;
                AssertResult.IsDocumentResultsState(docResults, replicateName,
                    doc.PeptideCount, doc.PeptideTransitionGroupCount, 0, doc.PeptideTransitionCount, 0);
            }
        }

        private static SrmDocument InitShimadzuDocument(TestFilesDir testFilesDir, out string docPath)
        {
            docPath = testFilesDir.GetTestPath("Alan_BSA_new.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 1, 15, 72);
            return doc;
        }
    }
}