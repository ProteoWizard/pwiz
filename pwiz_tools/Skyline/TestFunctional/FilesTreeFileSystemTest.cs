/*
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.FilesTree;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Files;
using pwiz.SkylineTestUtil;

// TODO: change directory name containing raw files
// TODO: use raw files in directory that's a sibling of directory containing .sky file

// ReSharper disable WrongIndentSize
namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FilesTreeFileSystemTest : AbstractFunctionalTest
    {
        // No parallel testing because SkylineException.txt is written to Skyline.exe
        // directory by design - and that directory is shared by all workers
        [TestMethod, NoParallelTesting(TestExclusionReason.SHARED_DIRECTORY_WRITE)]
        public void TestFilesTreeFileSystem()
        {
            TestFilesZipPaths = new[] { @"TestFunctional\FilesTreeFileSystemTest.zip" };

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestFileSystemService();
        }

        // TODO: assert file states
        // TODO: change file names, delete files, change directory names, delete directories
        // TODO: include sample file missing by default. Adding it to expected path marks file as available
        private void TestFileSystemService()
        {
            Assert.AreEqual(0, SkylineWindow.FilesTree.FileSystemService.MonitoredDirectories().Count);

            // Open Skyline document
            var documentPath = Path.Combine(TestFilesDirs[0].FullPath, @"Main", @"test.sky");
            RunUI(() =>
            {
                SkylineWindow.OpenFile(documentPath);
                SkylineWindow.ShowFilesTreeForm(true);
            });
            WaitForFilesTree();

            var fileSystemService = SkylineWindow.FilesTree.FileSystemService;
            Assert.AreEqual(FileSystemType.local_file_system, fileSystemService.FileSystemType);
            Assert.AreEqual(2, fileSystemService.MonitoredDirectories().Count);
            Assert.IsTrue(fileSystemService.MonitoredDirectories().Contains(DirForPath(documentPath)));

            // Import replicate in Main\
            var sampleFilePath = Path.Combine(TestFilesDirs[0].FullPath, @"Main", @"small-01-main-directory.mzml");
            ImportResultsFile(sampleFilePath);
            RunUI(() => SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Expand());
            WaitForFilesTree();

            Assert.AreEqual(FileSystemType.local_file_system, fileSystemService.FileSystemType);
            Assert.AreEqual(2, fileSystemService.MonitoredDirectories().Count);
            Assert.IsTrue(fileSystemService.MonitoredDirectories().Contains(DirForPath(sampleFilePath)));

            // Import replicate in Main\SubDirectory\
            sampleFilePath = Path.Combine(TestFilesDirs[0].FullPath, @"Main", @"SubDirectory", @"small-02-sub-directory.mzml");
            ImportResultsFile(sampleFilePath);
            WaitForFilesTree();

            Assert.AreEqual(FileSystemType.local_file_system, fileSystemService.FileSystemType);
            Assert.AreEqual(3, fileSystemService.MonitoredDirectories().Count);
            Assert.IsTrue(fileSystemService.MonitoredDirectories().Contains(DirForPath(sampleFilePath)));

            // Import replicate in Main\..\SiblingDirectory01\
            sampleFilePath = Path.Combine(TestFilesDirs[0].FullPath, @"SiblingDirectory01", @"small-03-sibling-directory01.mzml");
            ImportResultsFile(sampleFilePath);
            WaitForFilesTree();

            Assert.AreEqual(FileSystemType.local_file_system, fileSystemService.FileSystemType);
            Assert.AreEqual(4, fileSystemService.MonitoredDirectories().Count);
            Assert.IsTrue(fileSystemService.MonitoredDirectories().Contains(DirForPath(sampleFilePath)));

            // Import replicate in Main\..\SiblingDirectory02\
            sampleFilePath = Path.Combine(TestFilesDirs[0].FullPath, @"SiblingDirectory02", @"small-04-sibling-directory02.mzml");
            ImportResultsFile(sampleFilePath);
            WaitForFilesTree();

            Assert.AreEqual(FileSystemType.local_file_system, fileSystemService.FileSystemType);
            Assert.AreEqual(5, fileSystemService.MonitoredDirectories().Count);
            Assert.IsTrue(fileSystemService.MonitoredDirectories().Contains(DirForPath(sampleFilePath)));

            Assert.IsTrue(ReferenceEquals(fileSystemService, SkylineWindow.FilesTree.FileSystemService));
        }

        private static string DirForPath(string fullPath)
        {
            var normalizedFullPath = Path.GetFullPath(fullPath);
            return Path.GetDirectoryName(normalizedFullPath);
        }

        private static void ImportResultsFile(string path)
        {
            RunLongDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                RunDlg<OpenDataSourceDialog>(() => importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFile(null),
                    openDataSourceDialog =>
                    {
                        openDataSourceDialog.SelectFile(path);
                        openDataSourceDialog.Open();
                    });
                WaitForConditionUI(() => importResultsDlg.NamedPathSets != null);
            }, importResultsDlg => importResultsDlg.OkDialog());
        }

        /// <summary>
        /// Wait for the FilesTree to finish loading and processing any changes to the document. Includes finishing work
        /// queued for async processing on a background thread or on the UI thread.
        /// </summary>
        private static void WaitForFilesTree()
        {
            WaitForDocumentLoaded();
            WaitForConditionUI(() => SkylineWindow.FilesTree.IsComplete());
        }
    }
}