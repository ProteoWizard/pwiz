/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Test SIM scan results.
    /// </summary>
    [TestClass]
    public class ImportSimTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\ImportSim.zip";
        private const string DOCUMENT_NAME = "ImportSimTest.sky";
        private const string RESULTS_NAME = "ImportSimTest.mzML";
        private const string RESULTS_NAME2 = "ImportSimTest2.mzML";

        [TestMethod]
        public void TestImportSim()
        {
            DoTestImportSim(false);
        }

        [TestMethod]
        public void TestImportSimAsSmallMolecules()
        {
            DoTestImportSim(true);
        }

        private void DoTestImportSim(bool asSmallMolecules)
        {
            if (asSmallMolecules && SkipSmallMoleculeTestVersions())
            {
                return;
            }

            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath(DOCUMENT_NAME);
            string cachePath = ChromatogramCache.FinalPathForName(docPath, null);
            FileEx.SafeDelete(cachePath);
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);

            var pepdoc = doc;
            if (asSmallMolecules)
            {
                var refine = new RefinementSettings();
                doc = refine.ConvertToSmallMolecules(pepdoc, TestContext.ResultsDirectory);
            }

            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                // Import the mzML file and verify Mz range
                Import(docContainer, testFilesDir.GetTestPath(RESULTS_NAME), 510, 512);
                Import(docContainer, testFilesDir.GetTestPath(RESULTS_NAME2), 555, 557);
            }
        }

        private void Import(ResultsTestDocumentContainer docContainer, string resultsPath, double minMz, double maxMz)
        {
            var measuredResults = new MeasuredResults(new[] { new ChromatogramSet("Single", new[] { resultsPath }) });
            docContainer.ChangeMeasuredResults(measuredResults, 1, 1, 3);

            // Check expected Mz range.
            foreach (var nodeTran in docContainer.Document.MoleculeTransitions)
            {
                Assert.IsTrue(nodeTran.HasResults, "No results for transition Mz: {0}", nodeTran.Mz);
                if (nodeTran.Results[0].IsEmpty)
                    continue;
                //Assert.IsNotNull(nodeTran.Results[0], "Null results for transition Mz: {0}", nodeTran.Mz);
                if (minMz > nodeTran.Mz || nodeTran.Mz > maxMz)
                    Assert.IsTrue(nodeTran.Results[0][0].IsEmpty, "Non-empty transition Mz: {0}", nodeTran.Mz);
                else
                    Assert.IsFalse(nodeTran.Results[0][0].IsEmpty, "Empty transition Mz: {0}", nodeTran.Mz);
            }
        }
    }
}
