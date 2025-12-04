/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests bringing up the Associate Proteins dialog while Skyline is in
    /// the process of extracting chromatograms from several .mzML files.
    /// </summary>
    [TestClass]
    public class AssociateProteinsWhileLoadingTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAssociateProteinsWhileLoading()
        {
            TestFilesZip = @"TestFunctional\AssociateProteinsWhileLoadingTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Document.sky")));
            // Add many permutations of the peptide GNPTVEVELTTEK to the document.
            const int peptideCount = 100;
            var peptideSequences = RescoreInPlaceTest.PermuteString("GNPTVEVELTTE").Distinct().Select(s => s + "K").Take(peptideCount);
            RunUI(() => SkylineWindow.Paste(TextUtil.LineSeparate(peptideSequences)));

            // At first, all the peptides are in one peptide list
            Assert.AreEqual(1, SkylineWindow.Document.PeptideGroupCount);
            Assert.AreEqual(peptideCount, SkylineWindow.Document.PeptideCount);

            // Create several copies of "S_1.mzML"
            const int fileCount = 5;
            List<string> filesToImport = new List<string>();
            for (int iFile = 1; iFile <= fileCount; iFile++)
            {
                var filePath = TestFilesDir.GetTestPath("S_" + iFile + ".mzML");
                if (filesToImport.Count > 0)
                {
                    File.Copy(filesToImport[0], filePath);
                }
                filesToImport.Add(filePath);
            }

            // Tell Skyline to start importing all the mzML files
            RunLongDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                RunUI(() =>
                {
                    importResultsDlg.ImportSimultaneousIndex = 0;
                });
                RunDlg<OpenDataSourceDialog>(
                    () => importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFile(null),
                    openDataSourceDialog =>
                    {
                        openDataSourceDialog.SelectAllFileType("mzML");
                        openDataSourceDialog.Open();
                    });
                WaitForConditionUI(() => importResultsDlg.NamedPathSets != null);
            }, importResultsDlg => importResultsDlg.OkDialog());

            // While Skyline is still importing bring up the Associate Proteins dialog
            RunLongDlg<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg, associateProteinsDlg =>
            {
                WaitForCondition(() => GetCompletedFileCount(SkylineWindow.Document) > 0);
                DeleteCompletedFiles(SkylineWindow.Document);
                // Choose the fasta file
                RunUI(()=>associateProteinsDlg.FastaFileName = TestFilesDir.GetTestPath("TestFasta.fasta"));

                bool clickedOk = false;
                for (;;)
                {
                    DeleteCompletedFiles(SkylineWindow.Document);
                    // Click the button as soon as the OK button is enabled
                    RunUI(() =>
                    {
                        if (associateProteinsDlg.IsOkEnabled)
                        {
                            associateProteinsDlg.NewTargetsFinalSync(out int proteins, out int peptides, out _, out _);
                            Assert.AreEqual(2, proteins);
                            Assert.AreEqual(peptideCount, peptides);
                            associateProteinsDlg.OkDialog();
                            clickedOk = true;
                        }
                    });
                    if (clickedOk)
                    {
                        break;
                    }
                }
            }, _ => { });
            // The peptides should have been sorted into one protein and one peptide list
            Assert.AreEqual(2, SkylineWindow.Document.PeptideGroupCount);
            Assert.AreEqual(peptideCount, SkylineWindow.Document.PeptideCount);

            // Wait for all the files to finish being imported
            WaitForDocumentLoaded();
            // Verify that the document has the correct number of replicates and peptide groups
            Assert.AreEqual(fileCount, GetCompletedFileCount(SkylineWindow.Document));
            Assert.AreEqual(2, SkylineWindow.Document.PeptideGroupCount);
            Assert.AreEqual(peptideCount, SkylineWindow.Document.PeptideCount);
        }

        private int GetCompletedFileCount(SrmDocument document)
        {
            return document.MeasuredResults?.Chromatograms.Sum(chromatogramSet =>
                chromatogramSet.MSDataFileInfos.Count(info => info.FileWriteTime.HasValue)) ?? 0;
        }

        /// <summary>
        /// Deletes all the .mzML files that have already been imported into the document.
        /// This is done to make sure that ChromatogramManager cannot compensate by reimporting
        /// the same file if the wrong version of the document was made current.
        /// </summary>
        private void DeleteCompletedFiles(SrmDocument document)
        {
            foreach (var chromatogramSet in document.Settings.MeasuredResults.Chromatograms)
            {
                foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    if (chromFileInfo.FileWriteTime.HasValue)
                    {
                        try
                        {
                            File.Delete(chromFileInfo.FilePath.GetFilePath());
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }
        }
    }
}
