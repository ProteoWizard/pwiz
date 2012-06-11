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
    public class AgilentMixTest
    {
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes

        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //

        #endregion

        private const string ZIP_FILE = @"Test\Results\AgilentMix.zip";

        [TestMethod]
        public void AgilentFileTypeTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string extRaw = ExtensionTestContext.ExtAgilentRaw;

            // Do file type check
            MsDataFileImpl msData = new MsDataFileImpl(testFilesDir.GetTestPath("081809_100fmol-MichromMix-05" + extRaw));
            Assert.IsTrue(msData.IsAgilentFile);
            msData.Dispose();
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
                                        { testFilesDir.GetTestPath("081809_100fmol-MichromMix-05" + extRaw) }),
                                };
            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
            Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
            docContainer.AssertComplete();
            docResults = docContainer.Document;
            AssertResult.IsDocumentResultsState(docResults, replicateName,
                doc.PeptideCount, doc.TransitionGroupCount, 0, doc.TransitionCount, 0);

            // Release file handles
            Assert.IsTrue(docContainer.SetDocument(doc, docResults));

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