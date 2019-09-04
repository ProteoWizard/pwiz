/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
    /// Test Agilent CE optimzation.
    /// </summary>
    [TestClass]
    public class AgilentCEOptTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\AgilentCEOpt.zip";
        private const string DOCUMENT_NAME = "AgilentCE.sky";
        private const string RESULTS_NAME = "BisMet-1pgul-opt-01.d";

        [TestMethod]
        public void TestAgilentCEOpt()
        {
            DoTestAgilentCEOpt();
        }

        private void DoTestAgilentCEOpt()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath(DOCUMENT_NAME);
            string cachePath = ChromatogramCache.FinalPathForName(docPath, null);
            FileEx.SafeDelete(cachePath);
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);

            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                // Import the .d file
                Import(docContainer, testFilesDir.GetTestPath(RESULTS_NAME), true);
            }
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                // Import the .d file
                Import(docContainer, testFilesDir.GetTestPath(RESULTS_NAME), false);
            }
        }

        private void Import(ResultsTestDocumentContainer docContainer, string resultsPath, bool optFlag)
        {
            var optRegression = optFlag ? docContainer.Document.Settings.TransitionSettings.Prediction.CollisionEnergy : null;
            int optSteps = optRegression != null ? optRegression.StepCount*2 + 1 : 1;
            var resultsUri = new MsDataFilePath(resultsPath);
            var chromSet = new ChromatogramSet("Optimize", new[] {resultsUri}, Annotations.EMPTY, optRegression);
            var measuredResults = new MeasuredResults(new[] { chromSet });
            docContainer.ChangeMeasuredResults(measuredResults, 1, 1*optSteps, 3*optSteps);

            // Check expected optimization data.
            foreach (var nodeTran in docContainer.Document.MoleculeTransitions)
            {
                Assert.IsTrue(nodeTran.HasResults, "No results for transition Mz: {0}", nodeTran.Mz);
                Assert.IsNotNull(nodeTran.Results[0]);
                Assert.AreEqual(optSteps, nodeTran.Results[0].Count);
            }
        }
    }
}