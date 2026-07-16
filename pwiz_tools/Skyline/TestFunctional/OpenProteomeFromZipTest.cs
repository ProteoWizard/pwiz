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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Database.FileSystems;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests reading a background proteome (.protdb, a SQLite database) that is stored uncompressed
    /// inside a shared .sky.zip, in place, without extracting it. The .protdb is read read-only
    /// through the zip VFS, driven from NHibernate via the connection string.
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

            // Share the document. The .protdb is a RandomAccessExtension, so it is stored uncompressed.
            string zipPath = TestFilesDir.GetTestPath("Shared.sky.zip");
            RunUI(() => SkylineWindow.ShareDocument(zipPath, new ShareType(true, null)));
            AssertEx.FileExists(zipPath);
            Assert.IsTrue(new RandomAccessZipFile(zipPath).AreEntriesStored(SrmDocumentSharing.RandomAccessExtensions),
                ".protdb was not stored uncompressed");

            // Open the background proteome directly from inside the .zip, without extracting it, and
            // verify it has the same proteins as the original on disk. A path into the .zip looks
            // like an ordinary path with the .zip as a component (C:\Doc.sky.zip\Bacillus.protdb).
            string protdbInZip = zipPath + Path.DirectorySeparatorChar + "Bacillus.protdb";
            Assert.IsTrue(new FilePath(protdbInZip).IsInZipFile);
            using (var proteomeDb = ProteomeDb.OpenProteomeDb(protdbInZip))
                Assert.AreEqual(expectedProteinCount, proteomeDb.GetProteinCount());
        }
    }
}
