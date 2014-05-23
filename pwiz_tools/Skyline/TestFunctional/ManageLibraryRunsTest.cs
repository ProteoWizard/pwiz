/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for managing the library runs in a document library
    /// </summary>
    [TestClass]
    public class ManageLibraryRunsTest : AbstractFunctionalTest
    {
        private string DocumentPath { get; set; }

        private static PeptideLibraries Libraries
        {
            get { return SkylineWindow.Document.Settings.PeptideSettings.Libraries; }
        }

        private static LibrarySpec DocumentLibrarySpec 
        { 
            get
            {
                return Libraries.LibrarySpecs.First(x => x.IsDocumentLibrary);
            }
        }

        private static Library DocumentLibrary
        {
            get
            {
                return Libraries.GetLibrary(DocumentLibrarySpec.Name);
            }
        }

        [TestMethod]
        public void TestManageLibraryRuns()
        {
            TestFilesZip = @"TestFunctional\ManageLibraryRunsTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Test managing library runs in a document library.
        /// </summary>
        protected override void DoTest()
        {
            // Open the .sky file
            DocumentPath = TestFilesDir.GetTestPath("ManageLibraryRunsTest.sky");
            RunUI(() => SkylineWindow.OpenFile(DocumentPath));
            WaitForDocumentLoaded();

            // Ensure it has a document library, and the library blib and redundant.blib files exist
            Assert.IsTrue(VerifyDocumentLibrary());

            TestLibraryInfo();
            TestRemoveOneLibraryRunAndCorrespondingReplicates();
            TestRemoveOneReplicateAndCorrespondingLibraryRuns();
            TestRemoveAllLibraryRuns();
        }

        /// <summary>
        /// Added to show <see cref="SpectrumLibraryInfoDlg"/>
        /// </summary>
        private void TestLibraryInfo()
        {
            var libExplore = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunDlg<SpectrumLibraryInfoDlg>(libExplore.ShowLibDetails,
                dlg => dlg.Close());
            OkDialog(libExplore, libExplore.CancelDialog);
        }

        private void TestRemoveOneReplicateAndCorrespondingLibraryRuns()
        {
            var chromatograms = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms;
            var chromToRemove = chromatograms[0];
            List<string> dataFilesToRemove = new List<string>();
            foreach (var chromFileInfo in chromToRemove.MSDataFileInfos)
            {
                foreach (var dataFile in DocumentLibrary.LibraryDetails.DataFiles)
                {
                    if (MeasuredResults.IsBaseNameMatch(chromFileInfo.FilePath.GetFileNameWithoutExtension(),
                                                        Path.GetFileNameWithoutExtension(dataFile)))
                    {
                        dataFilesToRemove.Add(dataFile);
                    }
                }
            }

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.SelectReplicatesTab();
                dlg.SelectedChromatograms = new[] { chromToRemove };
                dlg.RemoveReplicates();
                dlg.IsRemoveCorrespondingLibraries = true;
                dlg.OkDialog();
            });

            WaitForDocumentLoaded();

            // Make sure the replicate was removed
            var updatedChromatograms = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms;
            var findChrom = updatedChromatograms.FirstOrDefault(chrom => ReferenceEquals(chrom, chromToRemove));
            Assert.IsNull(findChrom);

            // Make sure the library runs corresponding to the replicate were removed
            foreach (var dataFile in dataFilesToRemove)
            {
                Assert.IsFalse(DocumentLibrary.LibraryDetails.DataFiles.Contains(dataFile));
            }
        }

        private void TestRemoveOneLibraryRunAndCorrespondingReplicates()
        {
            List<string> dataFiles = new List<string>(DocumentLibrary.LibraryDetails.DataFiles);
            string dataFileToRemove = dataFiles[0];

            var matchingFile = SkylineWindow.Document.Settings.MeasuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(dataFileToRemove));
            Assert.IsNotNull(matchingFile);

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.SelectLibraryRunsTab();
                List<string> libRuns = dlg.LibraryRuns.ToList();
                Assert.IsTrue(ArrayUtil.ReferencesEqual(dataFiles, libRuns));
                dlg.SelectedLibraryRuns = new[] { dataFileToRemove };
                dlg.RemoveLibraryRuns();
                Assert.IsFalse(dlg.LibraryRuns.Contains(dataFileToRemove));
                dlg.IsRemoveCorrespondingReplicates = true;
                dlg.OkDialog();
            });

            WaitForDocumentLoaded();

            // Make sure the data file was removed, but the document library still exisits
            Assert.IsNotNull(DocumentLibrary);
            Assert.IsFalse(DocumentLibrary.LibraryDetails.DataFiles.Contains(dataFileToRemove));
            Assert.IsTrue(VerifyDocumentLibrary());

            // Make sure the replicates corresponding to the data file was removed
            var chromatograms = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms;
            var findChrom = chromatograms.FirstOrDefault(chrom => ReferenceEquals(chrom, matchingFile.Chromatograms));
            Assert.IsNull(findChrom);
        }

        private void TestRemoveAllLibraryRuns()
        {
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.SelectLibraryRunsTab();
                List<string> libRuns = dlg.LibraryRuns.ToList();
                List<string> dataFiles = new List<string>(DocumentLibrary.LibraryDetails.DataFiles);
                Assert.IsTrue(ArrayUtil.ReferencesEqual(dataFiles, libRuns));
                dlg.RemoveAllLibraryRuns();
                libRuns = dlg.LibraryRuns.ToList();
                Assert.IsTrue(libRuns.Count == 0);
                dlg.OkDialog();
            });

            Assert.IsFalse(VerifyDocumentLibrary());
        }

        private bool VerifyDocumentLibrary()
        {
            string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(DocumentPath);
            string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(DocumentPath);
            var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;

            return File.Exists(docLibPath) && File.Exists(redundantDocLibPath) && librarySettings.HasDocumentLibrary;
        }
    }
}
