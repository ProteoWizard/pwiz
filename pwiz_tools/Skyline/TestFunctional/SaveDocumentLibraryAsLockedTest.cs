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
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that a "File > Save As" of a document with a document library leaves Skyline with a
    /// consistent view of the world when the save fails because a file it must write is locked by
    /// another process. Saving to a new name copies the document library to the new name, so there
    /// are two files whose writes can fail: the .sky file and the .blib file. In either case the open
    /// document must be left exactly as it was, and saving again once the lock is released must
    /// produce the same result as if the failure had never happened.
    /// </summary>
    [TestClass]
    public class SaveDocumentLibraryAsLockedTest : AbstractFunctionalTest
    {
        private const string ORIGINAL_NAME = "ManageLibraryRunsTest";

        [TestMethod]
        public void TestSaveDocumentLibraryAsLocked()
        {
            TestFilesZip = @"TestFunctional\ManageLibraryRunsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            string originalPath = TestFilesDir.GetTestPath(ORIGINAL_NAME + SrmDocument.EXT);
            RunUI(() => SkylineWindow.OpenFile(originalPath));
            WaitForDocumentLoaded();
            AssertDocumentConsistent(originalPath, ORIGINAL_NAME);

            // Scenario 1: the .sky file cannot be written. Saving must fail, leave the open document
            // alone, and succeed on a second attempt once the lock is gone.
            SaveAsWithLockedFile(originalPath, "RenamedSkyLocked", path => path);

            // Scenario 2: the .blib file cannot be written. The document library is copied to a name
            // derived from the new document name, so this is the other file a Save As has to write.
            SaveAsWithLockedFile(TestFilesDir.GetTestPath("RenamedSkyLocked" + SrmDocument.EXT),
                "RenamedBlibLocked", BiblioSpecLiteSpec.GetLibraryFileName);
        }

        /// <summary>
        /// Attempts a "Save As" to <paramref name="newName"/> while the file selected by
        /// <paramref name="getFileToLock"/> is held open by another process, asserts that the failure
        /// leaves the document as it was, then releases the lock and asserts that saving again works.
        /// </summary>
        private void SaveAsWithLockedFile(string currentPath, string newName,
            Func<string, string> getFileToLock)
        {
            string currentName = Path.GetFileNameWithoutExtension(currentPath);
            string newPath = TestFilesDir.GetTestPath(newName + SrmDocument.EXT);
            string pathToLock = getFileToLock(newPath);

            // The file has to exist for another process to be holding it open.
            File.WriteAllText(pathToLock, "Locked by another process.");

            var docBeforeSave = SkylineWindow.Document;
            using (new FileStream(pathToLock, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                bool saveResult = true;
                RunDlg<MessageDlg>(() => saveResult = SkylineWindow.SaveDocument(newPath),
                    messageDlg => messageDlg.OkDialog());
                Assert.IsFalse(saveResult,
                    string.Format("Save As was expected to fail while {0} is locked", Path.GetFileName(pathToLock)));
            }

            // The failed save must not have changed the open document in any way that matters: it is
            // still saved under its old name, still refers to its own document library, and is not dirty.
            AssertDocumentConsistent(currentPath, currentName);
            Assert.AreEqual(docBeforeSave.UserRevisionIndex, SkylineWindow.Document.UserRevisionIndex);
            Assert.AreSame(docBeforeSave.Children, SkylineWindow.Document.Children);
            Assert.AreSame(docBeforeSave.Settings.PeptideSettings, SkylineWindow.Document.Settings.PeptideSettings);

            // The lock is now released. Saving again must behave exactly as it would have the first time.
            RunUI(() => Assert.IsTrue(SkylineWindow.SaveDocument(newPath),
                "Save As was expected to succeed once the lock was released"));
            AssertDocumentConsistent(newPath, newName);
            Assert.IsTrue(File.Exists(BiblioSpecLiteSpec.GetLibraryFileName(newPath)));

            // The saved file must persist the renamed library, so that reopening it does not require a
            // slow settings update.
            AssertAllLibInfoNames(ResultsUtil.DeserializeDocument(newPath), newName);
            RunUI(() => SkylineWindow.OpenFile(newPath));
            WaitForDocumentLoaded();
            AssertDocumentConsistent(newPath, newName);
        }

        /// <summary>
        /// Asserts that the open document is saved under <paramref name="expectedDocPath"/> and refers
        /// to its document library by <paramref name="expectedLibName"/> everywhere: in the library
        /// settings, in the library file path, and on every precursor's spectrum header info.
        /// </summary>
        private static void AssertDocumentConsistent(string expectedDocPath, string expectedLibName)
        {
            Assert.AreEqual(expectedDocPath, SkylineWindow.DocumentFilePath);
            var docLibSpec = SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs
                .First(spec => spec != null && spec.IsDocumentLibrary);
            Assert.AreEqual(expectedLibName, docLibSpec.Name);
            Assert.AreEqual(BiblioSpecLiteSpec.GetLibraryFileName(expectedDocPath), docLibSpec.FilePath);
            AssertAllLibInfoNames(SkylineWindow.Document, expectedLibName);
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
