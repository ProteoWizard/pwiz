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
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Database;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
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

            // Save As copies the library the document library spec names, which a document built in
            // memory may hold somewhere other than beside its .sky file. Mark a copy held elsewhere
            // so the test can tell which file reached the new name.
            string elsewhereDir = TestFilesDir.GetTestPath("Elsewhere");
            Directory.CreateDirectory(elsewhereDir);
            string elsewhereLibPath = Path.Combine(elsewhereDir, "Elsewhere" + BiblioSpecLiteSpec.EXT);
            File.Copy(BiblioSpecLiteSpec.GetLibraryFileName(renamedPath), elsewhereLibPath);
            ExecuteSql(elsewhereLibPath, "CREATE TABLE " + MARKER_TABLE + " (Id INTEGER)");
            RunUI(() => SkylineWindow.ModifyDocument("Move document library", doc =>
                doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(libs =>
                    libs.ChangeLibrarySpecs(libs.LibrarySpecs.Select(spec => spec.IsDocumentLibrary
                        ? spec.ChangeFilePath(elsewhereLibPath)
                        : spec).ToArray())))));
            WaitForDocumentLoaded();
            Assert.AreEqual(elsewhereLibPath, GetDocumentLibrarySpec(SkylineWindow.Document).FilePath);

            const string movedName = "Moved";
            string movedPath = TestFilesDir.GetTestPath(movedName + SrmDocument.EXT);
            RunUI(() => Assert.IsTrue(SkylineWindow.SaveDocument(movedPath)));
            string movedLibPath = BiblioSpecLiteSpec.GetLibraryFileName(movedPath);
            Assert.AreEqual(movedLibPath, GetDocumentLibrarySpec(SkylineWindow.Document).FilePath);
            AssertAllLibInfoNames(SkylineWindow.Document, movedName);
            Assert.IsTrue(HasMarkerTable(movedLibPath));
        }

        private const string MARKER_TABLE = "SaveDocumentLibraryAsMarker";

        private static void ExecuteSql(string libraryPath, string sql)
        {
            using var connection = new SQLiteConnection(string.Format("Data Source='{0}';Version=3", libraryPath));
            connection.Open();
            using var command = new SQLiteCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        private static bool HasMarkerTable(string libraryPath)
        {
            using var connection = new SQLiteConnection(string.Format("Data Source='{0}';Version=3", libraryPath));
            connection.Open();
            return SqliteOperations.TableExists(connection, MARKER_TABLE);
        }

        private static LibrarySpec GetDocumentLibrarySpec(SrmDocument document)
        {
            return document.Settings.PeptideSettings.Libraries.LibrarySpecs
                .First(spec => spec != null && spec.IsDocumentLibrary);
        }

        private static string GetDocumentLibraryName(SrmDocument document)
        {
            return GetDocumentLibrarySpec(document).Name;
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
