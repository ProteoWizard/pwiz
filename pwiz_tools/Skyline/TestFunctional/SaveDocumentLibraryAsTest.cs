/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that "File > Save As" to a new name updates the library name that is stored on
    /// each precursor's <c>bibliospec_spectrum_info</c> element when the document has a document
    /// library. Before this was fixed, the saved document still referenced the old library name,
    /// which forced a slow settings update every time the renamed document was opened until it was
    /// saved a second time.
    /// </summary>
    [TestClass]
    public class SaveDocumentLibraryAsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSaveDocumentLibraryAs()
        {
            TestFilesZip = @"TestFunctional\ManageLibraryRunsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ManageLibraryRunsTest.sky")));
            WaitForDocumentLoaded();

            // The document starts out with a document library named after the original document.
            Assert.AreEqual("ManageLibraryRunsTest", GetDocumentLibraryName(SkylineWindow.Document));
            AssertAllLibInfoNames(SkylineWindow.Document, "ManageLibraryRunsTest");

            // Save the document to a new name. This renames the document library.
            const string newName = "Renamed";
            string renamedPath = TestFilesDir.GetTestPath(newName + SrmDocument.EXT);
            RunUI(() => Assert.IsTrue(SkylineWindow.SaveDocument(renamedPath)));

            // The document library files should have been copied to the new name.
            Assert.IsTrue(File.Exists(BiblioSpecLiteSpec.GetLibraryFileName(renamedPath)));

            // The in-memory document should now refer to the library by its new name on every node.
            Assert.AreEqual(newName, GetDocumentLibraryName(SkylineWindow.Document));
            AssertAllLibInfoNames(SkylineWindow.Document, newName);

            // The saved file must persist the new library name so that reopening it does not require
            // a slow settings update. Reload the document from disk and confirm.
            var savedDoc = ResultsUtil.DeserializeDocument(renamedPath);
            AssertAllLibInfoNames(savedDoc, newName);

            // Opening the renamed document should not require any library reconciliation, since the
            // saved names already match the (derived) document library name.
            RunUI(() => SkylineWindow.OpenFile(renamedPath));
            WaitForDocumentLoaded();
            Assert.AreEqual(newName, GetDocumentLibraryName(SkylineWindow.Document));
            AssertAllLibInfoNames(SkylineWindow.Document, newName);
        }

        private static string GetDocumentLibraryName(SrmDocument document)
        {
            return document.Settings.PeptideSettings.Libraries.LibrarySpecs
                .First(spec => spec != null && spec.IsDocumentLibrary).Name;
        }

        /// <summary>
        /// Asserts that every precursor which has library information refers to the library by the
        /// expected name, and that at least one such precursor exists.
        /// </summary>
        private static void AssertAllLibInfoNames(SrmDocument document, string expectedName)
        {
            int countChecked = 0;
            foreach (var nodeGroup in document.MoleculeTransitionGroups)
            {
                if (nodeGroup.LibInfo == null)
                    continue;
                Assert.AreEqual(expectedName, nodeGroup.LibInfo.LibraryName);
                countChecked++;
            }
            Assert.AreNotEqual(0, countChecked);
        }
    }
}
