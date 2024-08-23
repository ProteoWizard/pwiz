/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests switching between "Files to import simultaneously" settings in the middle of
    /// extracting chromatograms.
    /// This test creates Skyline documents in multiple different directories, starts importing
    /// results into one of them, then opens another document without saving the first document,
    /// and starts importing results into the next.
    /// </summary>
    [TestClass]
    public class ChangeImportSpeedTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestChangeImportSpeed()
        {
            TestFilesZip = @"TestFunctional\ChangeImportSpeedTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            int documentCount = 5;
            int peptideCount = 500;
            int massSpecFileCount = 3;

            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Document.sky")));
            // Add many permutations of the peptide GNPTVEVELTTEK to the document.
            var peptideSequences = PermuteString("GNPTVEVELTTE").Distinct().Select(s => s + "K").Take(peptideCount);
            RunUI(() => SkylineWindow.Paste(TextUtil.LineSeparate(peptideSequences)));

            for (int iMassSpecFile = 1; iMassSpecFile < massSpecFileCount; iMassSpecFile++)
            {
                File.Copy(GetMassSpecFilePath(0), GetMassSpecFilePath(iMassSpecFile));
            }

            for (int iDocument = 0; iDocument < documentCount; iDocument++)
            {
                string folder = GetDocumentFolderName(iDocument);
                Directory.CreateDirectory(folder);
                RunUI(() =>
                {
                    SkylineWindow.SaveDocument(GetDocumentFilePath(iDocument));
                });
            }

            MsDataFileUri massSpecFolder = new MsDataFilePath(Path.GetDirectoryName(GetMassSpecFilePath(0)));
            for (int iDocument = 0; iDocument < documentCount; iDocument++)
            {
                RunUI(() =>
                {
                    SkylineWindow.OpenFile(GetDocumentFilePath(iDocument));
                });
                WaitForDocumentLoaded();
                RunLongDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
                {
                    RunUI(()=>
                    {
                        importResultsDlg.ImportSimultaneousIndex = 2; //iDocument % 3;
                    });
                    RunDlg<OpenDataSourceDialog>(
                        () => importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFile(null),
                        openDataSourceDialog =>
                        {
                            openDataSourceDialog.CurrentDirectory = massSpecFolder;
                            for (int iMassSpecFile = 0; iMassSpecFile < massSpecFileCount; iMassSpecFile++)
                            {
                                openDataSourceDialog.SelectFile(GetMassSpecFileName(iMassSpecFile));
                            }
                            openDataSourceDialog.Open();
                        });
                    WaitForConditionUI(() => importResultsDlg.NamedPathSets != null);
                }, importResultsDlg => importResultsDlg.OkDialog());
            }
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Document.sky"));
            });
        }

        private string GetMassSpecFilePath(int iMassSpecFile)
        {
            return TestFilesDir.GetTestPath(GetMassSpecFileName(iMassSpecFile));
        }

        private string GetMassSpecFileName(int iMassSpecFile)
        {
            return "S_" + (iMassSpecFile + 1) + ".mzML";
        }

        private string GetDocumentFolderName(int iDocument)
        {
            return TestFilesDir.GetTestPath("Folder" + iDocument);
        }

        private string GetDocumentFilePath(int iDocument)
        {
            return Path.Combine(GetDocumentFolderName(iDocument), "Document.sky");
        }

        private MultiFileLoader.ImportResultsSimultaneousFileOptions GetImportSimultaneousOption(int iDocument)
        {
            switch (iDocument % 3)
            {
                case 0:
                    return MultiFileLoader.ImportResultsSimultaneousFileOptions.one_at_a_time;
                case 1:
                    return MultiFileLoader.ImportResultsSimultaneousFileOptions.several;
                default:
                    return MultiFileLoader.ImportResultsSimultaneousFileOptions.many;
            }
        }

        private static IEnumerable<string> PermuteString(string input)
        {
            return RescoreInPlaceTest.PermuteString(input);
        }
    }
}
