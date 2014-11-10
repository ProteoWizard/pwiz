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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class DocLoadLibraryTest : AbstractUnitTest
    {
        const string TEST_ZIP_PATH = @"TestA\DocLoadLibraryTest.zip";

        /// <summary>
        /// Test to make sure library info on peptides and transitions is not recalculated
        /// on document load, because this has serious performance impact on opening documents,
        /// requiring spectra to be loaded from libraries for every peptide in the document
        /// every time it is opened.
        /// </summary>
        [TestMethod]
        public void DocLoadLibrary()
        {
            // Load the document
            var testFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            string loadPath = testFilesDir.GetTestPath("DocWithLibrary.sky");
            string libraryPath = testFilesDir.GetTestPath("Yeast_MRMer_min.blib");
            var doc = ResultsUtil.DeserializeDocument(loadPath);
            doc = doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(
                lib => lib.ChangeLibrarySpecs(new[] {new BiblioSpecLiteSpec(lib.Libraries[0].Name, libraryPath),})));

            // Cause library load and subsequent document update
            var docContainer = new ResultsTestDocumentContainer(null, loadPath);
            docContainer.SetDocument(doc, null, true);
            docContainer.AssertComplete();

            // Check that library info on peptides and transitions were not recalculated
            // during document load
            var docLoaded = docContainer.Document;
            Assert.AreEqual(6, docLoaded.PeptideCount);
            Assert.AreEqual(36, docLoaded.PeptideTransitionCount);
            var transitions = docLoaded.PeptideTransitions.ToArray();
            Assert.AreEqual("y12", transitions[0].FragmentIonName);
            Assert.AreEqual(1, transitions[0].LibInfo.Rank);
            Assert.AreEqual("y12", transitions[3].FragmentIonName);
            Assert.AreEqual(1, transitions[3].LibInfo.Rank);
            Assert.AreEqual("b3", transitions[14].FragmentIonName);
            Assert.AreEqual(2, transitions[14].LibInfo.Rank);
            Assert.AreEqual("b3", transitions[17].FragmentIonName);
            Assert.AreEqual(2, transitions[17].LibInfo.Rank);

            var docLibraryChanged = docLoaded.ChangeSettings(docLoaded.Settings.ChangePeptideLibraries(
                lib => lib.ChangeLibraries(new LibrarySpec[0], new Library[0])
                          .ChangeLibrarySpecs(new[] {new BiblioSpecLiteSpec("Test reload", libraryPath),})));
            docContainer.SetDocument(docLibraryChanged, docLoaded, true);
            var docChangedLoaded = docContainer.Document;

            // Check that document changed to be in synch with the library
            Assert.AreEqual(3, docChangedLoaded.PeptideCount);
            Assert.AreEqual(18, docChangedLoaded.PeptideTransitionCount);
            var transitionsNew = docChangedLoaded.PeptideTransitions.ToArray();
            Assert.AreEqual("y7", transitionsNew[0].FragmentIonName);
            Assert.AreEqual(1, transitionsNew[0].LibInfo.Rank);
            Assert.AreEqual("y7", transitionsNew[3].FragmentIonName);
            Assert.AreEqual(1, transitionsNew[3].LibInfo.Rank);
            Assert.AreEqual("y6", transitionsNew[8].FragmentIonName);
            Assert.AreEqual(2, transitionsNew[8].LibInfo.Rank);
            Assert.AreEqual("y6", transitionsNew[11].FragmentIonName);
            Assert.AreEqual(2, transitionsNew[11].LibInfo.Rank);
            for (int i = 1; i < 3; i++)
            {
                Assert.AreSame(transitions[i], transitionsNew[i]);
                Assert.AreSame(transitions[i+3], transitionsNew[i+3]);
            }
            for (int i = 12; i < 14; i++)
            {
                Assert.AreSame(transitions[i], transitionsNew[i-6]);
                Assert.AreSame(transitions[i+3], transitionsNew[i-3]);
            }
            for (int i = 24; i < 27; i++)
            {
                Assert.AreSame(transitions[i], transitionsNew[i-12]);
                Assert.AreSame(transitions[i+3], transitionsNew[i-9]);
            }

            // Release open streams
            docContainer.Release();
        }
    }
}