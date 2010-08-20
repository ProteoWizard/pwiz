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
using System.Collections.Generic;
using System.IO;
using Ionic.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for CE Optimization.
    /// </summary>
    [TestClass]
    public class ShareDocumentTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDocumentSharing()
        {
            TestDirectoryName = "ShareDocumentTest";
            TestFilesZip = @"TestFunctional\PrecursorTest.zip";
            RunFunctionalTest();
        }

        private const string DOCUMENT_NAME = "PrecursorTest.sky";

        /// <summary>
        /// Test Skyline document sharing with libraries.
        /// </summary>
        protected override void DoTest()
        {
            // Remember original files
            var origFileSet = new Dictionary<string, ZipEntry>();
            var newFileSet = new Dictionary<string, ZipEntry>();
            string zipPath = TestContext.GetProjectDirectory(TestFilesZip);
            using (ZipFile zipFile = ZipFile.Read(zipPath))
            {
                foreach (ZipEntry zipEntry in zipFile)
                    origFileSet.Add(zipEntry.FileName, zipEntry);
            }

            // Open the .sky file
            string documentPath = TestFilesDir.GetTestPath(DOCUMENT_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            string shareCompletePath = TestFilesDir.GetTestPath("ShareComplete.zip");
            Share(shareCompletePath, true, origFileSet, newFileSet);
            WaitForLibraries();

            const string blibLibName = "Michrom_QTRAP_v4.blib";
            string nistLibName = "YeastMini.msp";
            var nistLibEntry = origFileSet[nistLibName];
            origFileSet.Remove(nistLibName);
            nistLibName = Path.ChangeExtension(nistLibName, ".blib");
            origFileSet[nistLibName] = nistLibEntry;

            string shareMin1Path = TestFilesDir.GetTestPath("ShareMin1.zip");
            Share(shareMin1Path, false, origFileSet, newFileSet);
            Assert.AreEqual(origFileSet[blibLibName].UncompressedSize,
                newFileSet[blibLibName].UncompressedSize);
            WaitForLibraries();

            SelectNode(SrmDocument.Level.Peptides, 0);
            WaitForGraphs();
            string prefix = Path.GetFileNameWithoutExtension(DOCUMENT_NAME);
            RunUI(() =>
                      {
                          Assert.AreEqual("Michrom_QTRAP_v4 (" + prefix + ")", SkylineWindow.GraphSpectrum.LibraryName);
                          Assert.IsTrue(SkylineWindow.GraphSpectrum.PeaksRankedCount > 0);
                      });
            SelectNode(SrmDocument.Level.Peptides, SkylineWindow.Document.PeptideCount - 1);
//*
            WaitForGraphs();
            RunUI(() =>
                      {
                          Assert.AreEqual("YeastMini (" + prefix + ")", SkylineWindow.GraphSpectrum.LibraryName);
                          Assert.IsTrue(SkylineWindow.GraphSpectrum.PeaksRankedCount > 0);
                      });

            DeleteLastProtein();
            DeleteLastProtein();

            // Deleting the only peptide used by the NIST library should have
            // gotten rid of it.
            origFileSet.Remove(nistLibName);

            string shareMin2Path = TestFilesDir.GetTestPath("ShareMin2.zip");
            Share(shareMin2Path, false, origFileSet, newFileSet);
            Assert.IsTrue(origFileSet[blibLibName].UncompressedSize >
                newFileSet[blibLibName].UncompressedSize);
            WaitForLibraries();

            SelectNode(SrmDocument.Level.Transitions, 0);
            WaitForGraphs();

            RunUI(() =>
            {
                Assert.AreEqual("Michrom_QTRAP_v4 (" + prefix + ")", SkylineWindow.GraphSpectrum.LibraryName);
                Assert.IsTrue(SkylineWindow.GraphSpectrum.PeaksRankedCount > 0);
                SkylineWindow.NewDocument();
                Assert.AreEqual(0, Settings.Default.SpectralLibraryList.Count);
            });
 //*/
        }

        private static void Share(string zipPath, bool completeSharing,
            IDictionary<string, ZipEntry> origFileSet,
            IDictionary<string, ZipEntry> newFileSet)
        {
            RunUI(() => SkylineWindow.ShareDocument(zipPath, completeSharing));

            bool extract = !completeSharing;
            string extractDir = Path.Combine(Path.GetDirectoryName(zipPath),
                Path.GetFileNameWithoutExtension(zipPath));

            if (extract)
                Directory.CreateDirectory(extractDir);

            using (ZipFile zipFile = ZipFile.Read(zipPath))
            {
                Assert.AreEqual(origFileSet.Count, zipFile.Count);
                newFileSet.Clear();
                foreach (ZipEntry zipEntry in zipFile)
                {
                    ZipEntry origEntry;
                    Assert.IsTrue(origFileSet.TryGetValue(zipEntry.FileName, out origEntry),
                        string.Format("Found new entry {0} in complete sharing zip file.", zipEntry.FileName));
                    if (extract)
                    {
                        zipEntry.Extract(extractDir);                        
                    }
                    else
                    {
                        // If not extracting, then test to make sure files are same size as originals
                        Assert.AreEqual(origEntry.UncompressedSize, zipEntry.UncompressedSize,
                            string.Format("File sizes for {0} differ: expected <{1}>, found <{2}>",
                                zipEntry.FileName, origEntry.UncompressedSize, zipEntry.UncompressedSize));
                    }
                    newFileSet.Add(zipEntry.FileName, zipEntry);
                }
            }

            if (extract)
                RunUI(() => SkylineWindow.OpenFile(Path.Combine(extractDir, DOCUMENT_NAME)));
        }

        private static void WaitForLibraries()
        {
            WaitForConditionUI(() => SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries.IsLoaded);
        }

        private static void DeleteLastProtein()
        {
            var docCurrent = SkylineWindow.Document;
            SelectNode(SrmDocument.Level.PeptideGroups, docCurrent.PeptideGroupCount - 1);
            RunUI(SkylineWindow.EditDelete);
            WaitForDocumentChange(docCurrent);
        }
    }
}
