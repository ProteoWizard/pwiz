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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Results
{
    /// <summary>
    /// Summary description for SmallWiffTest
    /// </summary>
    [TestClass]
    public class SmallWiffTest
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

        private const string ZIP_FILE = @"Test\Results\SmallWiff.zip";

        [TestMethod]
        public void FileTypeTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string extWiff = ExtensionTestContext.ExtAbWiff;
            string suffix = ExtensionTestContext.CanImportAbWiff ? "" : "-test";

            // Do file type checks
            MsDataFileImpl msData = new MsDataFileImpl(testFilesDir.GetTestPath("051309_digestion" + suffix + extWiff));
            Assert.IsTrue(msData.IsABFile);
            msData.Dispose();

            msData = new MsDataFileImpl(testFilesDir.GetTestPath("051309_digestion-s3.mzXML"));
            Assert.IsTrue(msData.IsABFile);
            Assert.IsTrue(msData.IsMzWiffXml);
            msData.Dispose();
        }

        [TestMethod]
        public void WiffResultsTest()
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            SrmDocument doc = InitWiffDocument(testFilesDir);
            var docContainer = new ResultsTestDocumentContainer(doc,
                testFilesDir.GetTestPath("SimpleWiffTest.sky"));
            File.Delete(ChromatogramCache.FinalPathForName(docContainer.DocumentFilePath, null));

            var listChromatograms = new List<ChromatogramSet>();

            if (ExtensionTestContext.CanImportAbWiff)
            {
                string pathWiff = testFilesDir.GetTestPath("051309_digestion.wiff");
                string[] dataIds = MsDataFileImpl.ReadIds(pathWiff);

                for (int i = 0; i < dataIds.Length; i++)
                {
                    string nameSample = dataIds[i];
                    if (!Equals(nameSample, "test") && listChromatograms.Count == 0)
                        continue;
                    string pathSample = SampleHelp.EncodePath(pathWiff, nameSample, i);
                    listChromatograms.Add(new ChromatogramSet(nameSample, new[] { pathSample }));
                }
            }
            else
            {
                listChromatograms.Add(new ChromatogramSet("test",
                    new[] { testFilesDir.GetTestPath("051309_digestion-test.mzML") }));
                listChromatograms.Add(new ChromatogramSet("rfp9,before,h,1",
                    new[] { testFilesDir.GetTestPath("051309_digestion-rfp9,before,h,1.mzML") }));
            }

            // Should have added test and one after
            Assert.AreEqual(2, listChromatograms.Count);

            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
            docContainer.AssertComplete();

            docResults = docContainer.Document;

            AssertEx.IsDocumentState(docResults, 6, 9, 9, 18, 54);
            Assert.IsTrue(docResults.Settings.MeasuredResults.IsLoaded);

            foreach (var nodeTran in docResults.Transitions)
            {
                Assert.IsTrue(nodeTran.HasResults);
                Assert.AreEqual(2, nodeTran.Results.Count);
            }

            // Remove the last chromatogram
            listChromatograms.RemoveAt(1);

            var docResultsSingle = docResults.ChangeMeasuredResults(new MeasuredResults(listChromatograms));

            AssertResult.IsDocumentResultsState(docResultsSingle, "test", 9, 2, 9, 8, 27);

            // Add mzXML version of test sample
            listChromatograms.Add(new ChromatogramSet("test-mzXML", new[] { testFilesDir.GetTestPath("051309_digestion-s3.mzXML") }));

            var docMzxml = docResults.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            Assert.IsTrue(docContainer.SetDocument(docMzxml, docResults, true));
            docContainer.AssertComplete();
            docMzxml = docContainer.Document;
            // Verify mzXML and native contained same results
            // Unfortunately mzWiff produces chromatograms with now zeros, which
            // need to be interpolated into place.  This means a .wiff file and
            // its mzWiff mzXML file will never be the same.
            AssertResult.MatchChromatograms(docMzxml, 0, 1, -1, 0);
            // Release all file handels
            Assert.IsTrue(docContainer.SetDocument(doc, docContainer.Document));

            // TODO: Switch to a using clause when PWiz is fixed, and this assertion fails
//            AssertEx.ThrowsException<IOException>(() => testFilesDir.Dispose());
        }

        private static SrmDocument InitWiffDocument(TestFilesDir testFilesDir)
        {
            const LabelAtoms labelAtoms = LabelAtoms.C13 | LabelAtoms.N15;
            List<StaticMod> heavyMods = new List<StaticMod>
                {
                    new StaticMod("Heavy K", "K", ModTerminus.C, null, labelAtoms, null, null),
                    new StaticMod("Heavy R", "R", ModTerminus.C, null, labelAtoms, null, null),
                };
            SrmSettings settings = SrmSettingsList.GetDefault();
            settings = settings.ChangePeptideModifications(mods => mods.ChangeHeavyModifications(heavyMods));
            SrmDocument doc = new SrmDocument(settings);

            IdentityPath selectPath;
            string path = testFilesDir.GetTestPath("051309_transition list.csv");
            IFormatProvider provider = CultureInfo.InvariantCulture;
            using (var reader = new StreamReader(path))
            {
                // Product m/z out of range
                var docError = doc;
                AssertEx.ThrowsException<InvalidDataException>(
                    () => docError.ImportMassList(reader, provider, ',', null, out selectPath));
            }

            using (var reader = new StreamReader(path))
            {
                doc = doc.ChangeSettings(settings.ChangeTransitionInstrument(inst => inst.ChangeMaxMz(1800)));
                doc = doc.ImportMassList(reader, provider, ',', null, out selectPath);
            }

            AssertEx.IsDocumentState(doc, 2, 9, 9, 18, 54);
            return doc;
        }
    }
}