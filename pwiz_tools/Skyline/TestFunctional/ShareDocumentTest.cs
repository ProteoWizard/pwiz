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
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Ionic.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
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
            TestFilesZipPaths = new[] { @"TestFunctional\PrecursorTest.zip",
                                        @"TestFunctional\LibraryShareTest.zip",
                                        @"TestFunctional\LibraryShareTestPeakAnnotations.zip"};
            RunFunctionalTest();
        }

        private const string DOCUMENT_NAME = "PrecursorTest.sky";
       
        /// <summary>
        /// Test Skyline document sharing with libraries.
        /// </summary>
        protected override void DoTest()
        {
            ShareLibraryWithPeakAnnotationsTest();

            ShareDocTest();

            ShareLibraryTest();
        }

        private void ShareLibraryWithPeakAnnotationsTest()
        {
            const string docName = "PeakAnnotations.sky";
            const string zipFileMin = "ShareMinimizedLibPA.zip";
            const string nameComplete = "ShareCompleteLibPA";
            const string zipNameComplete = nameComplete+".zip";
            const string blibName = "lc_all.blib";


            // Remember original files
            var origFileSet = new Dictionary<string, ZipEntry>();
            var newFileSet = new Dictionary<string, ZipEntry>();
            var zipPath = TestContext.GetProjectDirectory(TestFilesZipPaths[2]);
            using (ZipFile zipFile = ZipFile.Read(zipPath))
            {
                foreach (ZipEntry zipEntry in zipFile)
                {
                    origFileSet.Add(zipEntry.FileName, zipEntry);
                }
            }

            // Open the .sky file
            var documentPath = TestFilesDirs[2].GetTestPath(docName);
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForDocumentLoaded();

            // Share the complete document.
            var shareDocPath = TestFilesDirs[2].GetTestPath(zipNameComplete);
            Share(shareDocPath, true, origFileSet, newFileSet, docName, false);
            WaitForLibraries();

            var unzippedPath = TestFilesDirs[2].GetTestPath(nameComplete);
            TestContext.ExtractTestFiles(TestFilesDirs[2].GetTestPath(zipNameComplete), unzippedPath, new string[] { }, string.Empty); // Unzip for inspection
            var fullLibPath = GetPathToBlibFile(unzippedPath, blibName);
            var originalLibPath = TestFilesDirs[2].GetTestPath(blibName);
            Assert.IsTrue(GetCount(originalLibPath, "RefSpectra") == GetCount(fullLibPath, "RefSpectra"));
            Assert.IsTrue(GetCount(originalLibPath, "RefSpectraPeaks") == GetCount(fullLibPath, "RefSpectraPeaks"));
            Assert.IsTrue(GetCount(originalLibPath, "RefSpectraPeakAnnotations") == GetCount(fullLibPath, "RefSpectraPeakAnnotations"));

            // Share the minimal document.
            var shareMinPath = TestFilesDirs[2].GetTestPath(zipFileMin);
            RunDlg<ShareTypeDlg>(SkylineWindow.ShareDocument);
            Share(shareMinPath, false, origFileSet, newFileSet, docName);
            WaitForLibraries();

            var minimalLibPath = GetPathToBlibFile(shareMinPath, blibName);
            Assert.IsTrue(GetCount(originalLibPath, "RefSpectra") > GetCount(minimalLibPath, "RefSpectra"));
            Assert.IsTrue(GetCount(originalLibPath, "RefSpectraPeaks") > GetCount(minimalLibPath, "RefSpectraPeaks"));
            Assert.IsTrue(GetCount(originalLibPath, "RefSpectraPeakAnnotations") > GetCount(minimalLibPath, "RefSpectraPeakAnnotations"));

            // Open and inspect the shared complete document
            VerifySharedDocLibraryAnnotations(shareDocPath);

            // Open and inspect the shared minimal document
            VerifySharedDocLibraryAnnotations(shareMinPath);

        }

        private static void VerifySharedDocLibraryAnnotations(string shareDocPath)
        {
            RunUI(() => SkylineWindow.OpenSharedFile(shareDocPath));
            var doc = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(doc, null, 1, 4, 4, 4); // int revision, int groups, int peptides, int tranGroups, int transitions
            SpectrumPeaksInfo spectrum;
            IsotopeLabelType label;
            // ReSharper disable PossibleNullReferenceException
            doc.Settings.TryLoadSpectrum(doc.Molecules.FirstOrDefault().Target, Adduct.FromStringAssumeChargeOnly("M+NH4"),
                null, out label, out spectrum);
            Assume.IsTrue(spectrum.Annotations.Count() == 1);
            var spectrumPeakAnnotations = (spectrum.Annotations.FirstOrDefault() ?? new SpectrumPeakAnnotation[0]).ToArray();
            Assume.IsTrue(spectrumPeakAnnotations.Length == 1);
            Assume.IsTrue(spectrumPeakAnnotations.FirstOrDefault().Ion.Name == "GP");
            // ReSharper restore PossibleNullReferenceException
        }

        private void ShareLibraryTest()
        {
            const string docName = "LibraryShareTest.sky";
            const string zipNameComplete = "ShareCompleteLib.zip";
            const string zipNameCompleteNoMS1 = "ShareCompleteLibNoMs1.zip";
            const string zipNameMinNoMS1 = "ShareMinLibNoMs1.zip";
            const string zipFileMin = "ShareMinLib.zip";
            const string zipFileMin2 = "ShareMinLib2.zip";
            const string zipFileMin3 = "ShareMinLib3.zip";
            const string blibName = "Bereman_5proteins_spikein.blib";
            const string redundantBlibName = "Bereman_5proteins_spikein.redundant.blib";


            // Remember original files
            var origFileSet = new Dictionary<string, ZipEntry>();
            var newFileSet = new Dictionary<string, ZipEntry>();
            string zipPath = TestContext.GetProjectDirectory(TestFilesZipPaths[1]);
            using (ZipFile zipFile = ZipFile.Read(zipPath))
            {
                foreach (ZipEntry zipEntry in zipFile)
                    origFileSet.Add(zipEntry.FileName, zipEntry);
            }

            // Open the .sky file
            string documentPath = TestFilesDirs[1].GetTestPath(docName);
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForDocumentLoaded();

            // Share the complete document.
            // The zip file should include the redundant library
            string shareDocPath = TestFilesDirs[1].GetTestPath(zipNameComplete);
            Share(shareDocPath, true, origFileSet, newFileSet, docName, false);
            WaitForLibraries();

            // Share the minimal document.
            string shareMinPath = TestFilesDirs[1].GetTestPath(zipFileMin);
            RunDlg<ShareTypeDlg>(SkylineWindow.ShareDocument);
            Share(shareMinPath, false, origFileSet, newFileSet, docName);
            // The blib schema changes over time, stuff gets added, this isn't a reliable check
            // Assert.AreEqual(origFileSet[blibName].UncompressedSize,
            //                newFileSet[blibName].UncompressedSize);
            // Schema changed to support document libraries, adding information
            long newSize = newFileSet[redundantBlibName].UncompressedSize;
            Assert.IsTrue(origFileSet[redundantBlibName].UncompressedSize < newSize);
            WaitForLibraries();

            // Remove the last peptide in the document.
            // Share the minimal document
            DeleteLastPeptide();
            string shareMinPath2 = TestFilesDirs[1].GetTestPath(zipFileMin2);
            Share(shareMinPath2, false, origFileSet, newFileSet, docName);
            String originalLibPath = TestFilesDirs[1].GetTestPath(blibName);
            String minimalLibPath2 = GetPathToBlibFile(shareMinPath2, blibName);
            Assert.IsTrue(GetCount(originalLibPath, "RefSpectra") > GetCount(minimalLibPath2, "RefSpectra"));
            Assert.IsTrue(GetCount(originalLibPath, "RefSpectraPeaks") > GetCount(minimalLibPath2, "RefSpectraPeaks"));
            // The blib schema changes over time, stuff gets added, this isn't a reliable check
            //Assert.IsTrue(origFileSet[blibName].UncompressedSize >
            //              newFileSet[blibName].UncompressedSize);
            Assert.IsTrue(newSize >
                          newFileSet[redundantBlibName].UncompressedSize);
            WaitForDocumentLoaded();

            // Remove the last replicate from the document.
            // Share the minimal document
            var doc = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            {
                var doc1 = doc;
                RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults,
                    dlg =>
                    {
                        var chromatograms =
                            doc1.Settings.MeasuredResults.Chromatograms;
                        Assert.AreEqual(2, chromatograms.Count);
                        dlg.SelectedChromatograms = new[] { chromatograms[1] };
                        Assert.AreEqual(1, dlg.SelectedChromatograms.Count());
                        dlg.RemoveReplicates();
                        dlg.OkDialog();
                    });
            }
            WaitForDocumentChange(doc);

            string shareMinPath3 = TestFilesDirs[1].GetTestPath(zipFileMin3);
            var origFileSet2 = new Dictionary<string, ZipEntry>(newFileSet);
            Share(shareMinPath3, false, origFileSet, newFileSet, docName);

            string blibPath1 = GetPathToBlibFile(shareMinPath2, blibName);
            string blibPath2 = GetPathToBlibFile(shareMinPath3, blibName);
            Assert.AreNotSame(blibPath1, blibPath2);
            // Retention times no longer discarded, as they can be useful
            // later for peak picking and scheduling, nor are their redundant spectra
            Assert.AreEqual(GetCount(blibPath1, "RetentionTimes"),
                GetCount(blibPath2, "RetentionTimes"));
            Assert.AreEqual(origFileSet2[redundantBlibName].UncompressedSize,
                newFileSet[redundantBlibName].UncompressedSize);
            // This does not work.  Both the original and new files are the same size even though
            // the number of entries in the RetentionTimes table is smaller in the new file.
            //            Assert.IsTrue(origFileSet2[blibName].UncompressedSize >
            //                          newFileSet[blibName].UncompressedSize);
            WaitForLibraries();

            // Open the original .sky file
            doc = SkylineWindow.Document;
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForDocumentChangeLoaded(doc);

            // Disable MS1 filtering
            // Share the complete document.
            // The zip file should not contain the redundant library.
            DisableMS1Filtering();
            origFileSet.Remove(redundantBlibName);
            shareDocPath = TestFilesDirs[1].GetTestPath(zipNameCompleteNoMS1);
            Share(shareDocPath, true, origFileSet, newFileSet, docName, false);
            WaitForLibraries();

            // Share the minimal document
            // The zip file should not contain the redundant library
            string shareMinPath4 = TestFilesDirs[1].GetTestPath(zipNameMinNoMS1);
            Share(shareMinPath4, false, origFileSet, newFileSet, docName);
        }


        private void ShareDocTest()
        {
            // Remember original files
            var origFileSet = new Dictionary<string, ZipEntry>();
            var newFileSet = new Dictionary<string, ZipEntry>();
            string zipPath = TestContext.GetProjectDirectory(TestFilesZipPaths[0]);
            using (ZipFile zipFile = ZipFile.Read(zipPath))
            {
                foreach (ZipEntry zipEntry in zipFile)
                    origFileSet.Add(zipEntry.FileName, zipEntry);
            }

            // Open the .sky file
            string documentPath = TestFilesDirs[0].GetTestPath(DOCUMENT_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            string shareCompletePath = TestFilesDirs[0].GetTestPath("ShareComplete.zip");
            Share(shareCompletePath, true, origFileSet, newFileSet, DOCUMENT_NAME, false);
            WaitForLibraries();

            const string blibLibName = "Michrom_QTRAP_v4.blib";
            string nistLibName = "YeastMini.msp";
            var nistLibEntry = origFileSet[nistLibName];
            origFileSet.Remove(nistLibName);
            nistLibName = Path.ChangeExtension(nistLibName, ".blib");
            origFileSet[nistLibName] = nistLibEntry;

            string shareMin1Path = TestFilesDirs[0].GetTestPath("ShareMin1.zip");
            Share(shareMin1Path, false, origFileSet, newFileSet, DOCUMENT_NAME);
            // This test no longer works since the schema in the minimized library
            // now has one additional table -- RetentionTimes.
            //            Assert.AreEqual(origFileSet[blibLibName].UncompressedSize,
            //                newFileSet[blibLibName].UncompressedSize);
            String originalLibPath = TestFilesDirs[0].GetTestPath(blibLibName);
            String minimalLibPath = GetPathToBlibFile(shareMin1Path, blibLibName);
            Assert.AreNotSame(originalLibPath, minimalLibPath);

            Assert.AreEqual(GetCount(originalLibPath, "RefSpectra"),
                          GetCount(minimalLibPath, "RefSpectra"));
            Assert.AreEqual(GetCount(originalLibPath, "RefSpectraPeaks"),
                         GetCount(minimalLibPath, "RefSpectraPeaks"));

            WaitForLibraries();

            SelectNode(SrmDocument.Level.Molecules, 0);
            WaitForGraphs();
            string prefix = Path.GetFileNameWithoutExtension(DOCUMENT_NAME);

            RunUI(() =>
            {
                Assert.AreEqual("Michrom_QTRAP_v4 (" + prefix + ")", SkylineWindow.GraphSpectrum.LibraryName);
                Assert.IsTrue(SkylineWindow.GraphSpectrum.PeaksRankedCount > 0);
            });
            SelectNode(SrmDocument.Level.Molecules, SkylineWindow.Document.PeptideCount - 1);
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

            string shareMin2Path = TestFilesDirs[0].GetTestPath("ShareMin2.zip");
            Share(shareMin2Path, false, origFileSet, newFileSet, DOCUMENT_NAME);
            String minimalLibPath2 = GetPathToBlibFile(shareMin2Path, blibLibName);
            Assert.IsTrue(GetCount(originalLibPath, "RefSpectra") > GetCount(minimalLibPath2, "RefSpectra"));
            Assert.IsTrue(GetCount(originalLibPath, "RefSpectraPeaks") > GetCount(minimalLibPath2, "RefSpectraPeaks"));
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
        }

        private static string GetPathToBlibFile(string zipFilePath, string blibFileName)
        {
            string extractDir = Path.Combine(Path.GetDirectoryName(zipFilePath) ?? "",
                                              Path.GetFileNameWithoutExtension(zipFilePath) ?? "");

            return Path.Combine(extractDir, blibFileName);
        }

        private static void Share(string zipPath, bool completeSharing,
            IDictionary<string, ZipEntry> origFileSet,
            IDictionary<string, ZipEntry> newFileSet,
            string documentName,
            bool expectAuditLog = true)
        {
            var shareType = new ShareType(completeSharing, null);
            RunUI(() => SkylineWindow.ShareDocument(zipPath, shareType));

            bool extract = !completeSharing;
            string extractDir = Path.Combine(Path.GetDirectoryName(zipPath) ?? "",
                Path.GetFileNameWithoutExtension(zipPath) ?? "");

            if (extract)
                Directory.CreateDirectory(extractDir);

            using (ZipFile zipFile = ZipFile.Read(zipPath))
            {
                Assert.AreEqual(origFileSet.Count + (expectAuditLog ? 1 : 0), zipFile.Count);
                newFileSet.Clear();
                foreach (ZipEntry zipEntry in zipFile)
                {
                    if (expectAuditLog && zipEntry.FileName == Path.GetFileNameWithoutExtension(documentName) + AuditLogList.EXT)
                    {
                        expectAuditLog = false;
                    }
                    else
                    {
                        ZipEntry origEntry;
                        Assert.IsTrue(origFileSet.TryGetValue(zipEntry.FileName, out origEntry),
                            string.Format("Found new entry {0} in complete sharing zip file.", zipEntry.FileName));
                        if (extract)
                        {
                            zipEntry.Extract(extractDir, ExtractExistingFileAction.OverwriteSilently);
                        }
                        else
                        {
                            // If not extracting, then test to make sure files are same size as originals
                            Assert.AreEqual(origEntry.UncompressedSize, zipEntry.UncompressedSize,
                                string.Format("File sizes for {0} differ: expected <{1}>, found <{2}>",
                                    zipEntry.FileName, origEntry.UncompressedSize, zipEntry.UncompressedSize));
                        }
                    }
                    newFileSet.Add(zipEntry.FileName, zipEntry);
                }
            }

            if (extract)
                RunUI(() => SkylineWindow.OpenFile(Path.Combine(extractDir, documentName)));
        }

        private static void WaitForLibraries()
        {
            WaitForConditionUI(() => SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries.IsLoaded);
        }

        private static void DeleteLastProtein()
        {
            var docCurrent = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            SelectNode(SrmDocument.Level.MoleculeGroups, docCurrent.PeptideGroupCount - 1);
            RunUI(SkylineWindow.EditDelete);
            WaitForDocumentChange(docCurrent);
        }

        private static void DeleteLastPeptide()
        {
            var docCurrent = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            SelectNode(SrmDocument.Level.Molecules, docCurrent.PeptideCount - 1);
            RunUI(SkylineWindow.EditDelete);
            WaitForDocumentChange(docCurrent);
        }

        private static int GetCount(string blibPath, string tableName)
        {
            string connectionString = string.Format("Data Source={0};Version=3", blibPath);

            using (SQLiteConnection conn = new SQLiteConnection(connectionString))
            {

                conn.Open();

                using (SQLiteCommand select = new SQLiteCommand(conn))
                {
                    select.CommandText = string.Format("SELECT count(*) FROM [{0}]", tableName);
                    try
                    {
                        using (SQLiteDataReader reader = select.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidDataException(
                                    string.Format("Unable to get a valid count of all spectra in the library {0}", blibPath));
                            int rows = reader.GetInt32(0);
                            return rows;
                        }
                    }
                    catch (SQLiteException)
                    {
                        return 0;
                    }
                }
            }
        }

        private static void DisableMS1Filtering()
        {
            var doc = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            var docNew = doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs =>
                fs.ChangePrecursorIsotopes(FullScanPrecursorIsotopes.None, null, null)));
            Assert.IsTrue(SkylineWindow.SetDocument(docNew, doc));
            // TODO: Understand better why RetentionTimeManager needs to reload RT alignments due to this change
            docNew = WaitForDocumentChangeLoaded(doc);
            Assert.IsFalse(ReferenceEquals(SkylineWindow.Document, doc));
            Assert.IsTrue(ReferenceEquals(SkylineWindow.Document, docNew));
        }
    }
}
