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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Results
{
    /// <summary>
    /// Summary description for AgilentMixTest
    /// </summary>
    [TestClass]
    public class AgilentMixTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"Test\Results\AgilentMix.zip";

        [TestMethod]
        public void AgilentFileTypeTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string extRaw = ExtensionTestContext.ExtAgilentRaw;

            // Do file type check
            using (var msData = new MsDataFileImpl(testFilesDir.GetTestPath("081809_100fmol-MichromMix-05" + extRaw)))
            {
                Assert.IsTrue(msData.IsAgilentFile);
            }
        }

        [TestMethod]
        public void AgilentFormatsTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string docPath;
            SrmDocument doc = InitAgilentDocument(testFilesDir, out docPath);

            var docContainer = new ResultsTestDocumentContainer(doc, docPath);
            const string replicateName = "AgilentTest";
            string extRaw = ExtensionTestContext.ExtAgilentRaw;
            var chromSets = new[]
                                {
                                    new ChromatogramSet(replicateName, new[]
                                        { new MsDataFilePath(testFilesDir.GetTestPath("081809_100fmol-MichromMix-05" + extRaw)),  }),
                                };
            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
            Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
            docContainer.AssertComplete();
            docResults = docContainer.Document;
            AssertResult.IsDocumentResultsState(docResults, replicateName,
                doc.PeptideCount, doc.PeptideTransitionGroupCount, 0, doc.PeptideTransitionCount, 0);

            // Release file handles
            docContainer.Release();
            testFilesDir.Dispose();
        }

        private static SrmDocument InitAgilentDocument(TestFilesDir testFilesDir, out string docPath)
        {
            docPath = testFilesDir.GetTestPath("Bovine_std_curated_seq_small2.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 4, 5, 20);
            return doc;
        }
    }
}