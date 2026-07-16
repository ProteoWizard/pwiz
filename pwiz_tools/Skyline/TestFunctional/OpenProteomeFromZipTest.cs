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
using pwiz.Common.Database.FileSystems;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Opens a document whose background proteome (.protdb, a SQLite database) and audit log (.skyl)
    /// are stored inside the shared .sky.zip, in place, without extracting. The .protdb is read
    /// read-only through the zip VFS, and the .skyl is read sequentially from the zip.
    /// </summary>
    [TestClass]
    public class OpenProteomeFromZipTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestOpenProteomeFromZip()
        {
            TestFilesZip = @"TestFunctional\OpenDocWithBackgroundProteomeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PRM_Bacillus_heavy.sky")));
            var doc = WaitForDocumentLoaded();

            // Read the background proteome from disk and remember how many proteins it has.
            var proteome = doc.Settings.PeptideSettings.BackgroundProteome;
            Assert.IsFalse(proteome.IsNone);
            Assert.IsFalse(proteome.DatabaseInvalid);
            int expectedProteinCount;
            using (var proteomeDb = proteome.OpenProteomeDb())
                expectedProteinCount = proteomeDb.GetProteinCount();
            Assert.IsTrue(expectedProteinCount > 0);

            // Remove the spectral library so the shared .zip contains only files openable in place
            // (the NIST .msp library would be kept as-is and force extraction). Audit logging stays on,
            // so the share includes a .skyl, which must also be read in place.
            RunUI(() => SkylineWindow.ModifyDocument("Remove libraries", d =>
                d.ChangeSettings(d.Settings.ChangePeptideLibraries(lib =>
                    lib.ChangeLibraries(new LibrarySpec[0], new Library[0])))));
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());

            // Share the document. The .protdb is stored uncompressed.
            string inPlaceZip = TestFilesDir.GetTestPath("InPlaceProteome.sky.zip");
            RunUI(() => SkylineWindow.ShareDocument(inPlaceZip, new ShareType(true, null)));
            AssertEx.FileExists(inPlaceZip);

            var zip = new RandomAccessZipFile(inPlaceZip);
            Assert.IsTrue(zip.ContainsOnlyEntriesWithSuffixes(SrmDocumentSharing.OpenInPlaceExtensions),
                "shared .zip has entries that would prevent opening in place: " +
                string.Join(", ", zip.Entries.Select(e => e.FileName)));
            Assert.IsTrue(zip.AreEntriesStored(SrmDocumentSharing.RandomAccessExtensions),
                ".protdb was not stored uncompressed");

            // Read the background proteome directly from inside the .zip, without extracting it, and
            // verify it has the same proteins as the original on disk. This exercises reading a
            // .protdb in place through NHibernate + the zip VFS.
            string protdbInZip = inPlaceZip + Path.DirectorySeparatorChar + "Bacillus.protdb";
            Assert.IsTrue(new FilePath(protdbInZip).IsInZipFile);
            using (var proteomeDb = ProteomeDb.OpenProteomeDb(protdbInZip))
                Assert.AreEqual(expectedProteinCount, proteomeDb.GetProteinCount());

            // Clear the document, then open the shared file. Because the .zip has only files openable
            // in place (incl. the .protdb stored uncompressed and the .skyl read sequentially), it
            // opens in place rather than extracting. Reading the .skyl in place is required to get
            // here - a failure would have forced extraction, clearing SharedZipFilePath.
            RunUI(() => SkylineWindow.NewDocument());
            RunUI(() => SkylineWindow.OpenSharedFile(inPlaceZip));
            var inPlaceDoc = WaitForDocumentLoaded();

            Assert.AreEqual(inPlaceZip, SkylineWindow.SharedZipFilePath);
            Assert.IsTrue(new FilePath(SkylineWindow.DocumentFilePath).IsInZipFile,
                "expected the document to be opened from inside the .zip: " + SkylineWindow.DocumentFilePath);

            // The background proteome loaded (Skyline may resolve it to a local copy if one is
            // registered, which is fine; the in-place read path is covered directly above).
            var inPlaceProteome = inPlaceDoc.Settings.PeptideSettings.BackgroundProteome;
            Assert.IsFalse(inPlaceProteome.IsNone);
            Assert.IsFalse(inPlaceProteome.DatabaseInvalid, "background proteome failed to load");
        }
    }
}
