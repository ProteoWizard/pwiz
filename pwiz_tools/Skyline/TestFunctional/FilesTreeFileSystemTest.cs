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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.FilesTree;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Files;
using pwiz.SkylineTestUtil;

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
            Assert.AreEqual(1, fileSystemService.MonitoredDirectories().Count);
            Assert.IsTrue(fileSystemService.MonitoredDirectories().Contains(DirForPath(documentPath)));

            // Import replicate in Main\
            var sampleFilePath = Path.Combine(TestFilesDirs[0].FullPath, @"Main", @"small-01-main-directory.mzml");
            ImportResultsFile(sampleFilePath);
            WaitForFilesTree();
            RunUI(() => SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Expand());

            Assert.AreEqual(FileSystemType.local_file_system, fileSystemService.FileSystemType);
            Assert.AreEqual(1, fileSystemService.MonitoredDirectories().Count);
            Assert.IsTrue(fileSystemService.MonitoredDirectories().Contains(DirForPath(sampleFilePath)));

            // Import replicate in Main\SubDirectory\
            sampleFilePath = Path.Combine(TestFilesDirs[0].FullPath, @"Main", @"SubDirectory", @"small-02-sub-directory.mzml");
            ImportResultsFile(sampleFilePath);
            WaitForFilesTree();

            Assert.AreEqual(FileSystemType.local_file_system, fileSystemService.FileSystemType);
            Assert.AreEqual(2, fileSystemService.MonitoredDirectories().Count);
            Assert.IsTrue(fileSystemService.MonitoredDirectories().Contains(DirForPath(sampleFilePath)));

            // Import replicate in Main\..\SiblingDirectory01\
            sampleFilePath = Path.Combine(TestFilesDirs[0].FullPath, @"SiblingDirectory01", @"small-03-sibling-directory01.mzml");
            ImportResultsFile(sampleFilePath);
            WaitForFilesTree();

            Assert.AreEqual(FileSystemType.local_file_system, fileSystemService.FileSystemType);
            Assert.AreEqual(3, fileSystemService.MonitoredDirectories().Count);
            Assert.IsTrue(fileSystemService.MonitoredDirectories().Contains(DirForPath(sampleFilePath)));

            // Import replicate in Main\..\SiblingDirectory02\
            sampleFilePath = Path.Combine(TestFilesDirs[0].FullPath, @"SiblingDirectory02", @"small-04-sibling-directory02.mzml");
            ImportResultsFile(sampleFilePath);
            WaitForFilesTree();

            Assert.AreEqual(FileSystemType.local_file_system, fileSystemService.FileSystemType);
            Assert.AreEqual(4, fileSystemService.MonitoredDirectories().Count);
            Assert.IsTrue(fileSystemService.MonitoredDirectories().Contains(DirForPath(sampleFilePath)));

            Assert.IsTrue(ReferenceEquals(fileSystemService, SkylineWindow.FilesTree.FileSystemService));

            // Import replicate in Main\..\SiblingDirectory03\ChildDirectory01\ChildDirectory02\
            sampleFilePath = Path.Combine(TestFilesDirs[0].FullPath, @"SiblingDirectory03", @"ChildDirectory01", @"ChildDirectory02", @"small-03-child-directory-02.mzml");
            ImportResultsFile(sampleFilePath);
            WaitForFilesTree();

            Assert.AreEqual(FileSystemType.local_file_system, fileSystemService.FileSystemType);
            Assert.AreEqual(5, fileSystemService.MonitoredDirectories().Count);
            Assert.IsTrue(fileSystemService.MonitoredDirectories().Contains(DirForPath(sampleFilePath)));

            Assert.IsTrue(ReferenceEquals(fileSystemService, SkylineWindow.FilesTree.FileSystemService));

            { // Delete sample file in subdirectory
                var fileName = Path.Combine(TestFilesDirs[0].FullPath, @"SiblingDirectory02", @"small-04-sibling-directory02.mzml");
                File.Delete(fileName);
                WaitForFileState(fileName, FileState.missing);
                WaitForFilesTree();

                var filesTreeNode = FindNodeForPath(fileName);
                Assert.IsFalse(fileSystemService.IsFileAvailable(fileName));
                Assert.AreEqual(FileState.missing, filesTreeNode.FileState);
            }

            { // Rename sample file in subdirectory
                var fileName = Path.Combine(TestFilesDirs[0].FullPath, @"Main", @"SubDirectory", @"small-02-sub-directory.mzml");
                File.Move(fileName, fileName + @"RENAME");
                WaitForFileState(fileName, FileState.missing);
                WaitForFilesTree();

                var filesTreeNode = FindNodeForPath(fileName);
                Assert.IsFalse(fileSystemService.IsFileAvailable(fileName));
                Assert.AreEqual(FileState.missing, filesTreeNode.FileState);

                File.Move(fileName + @"RENAME", fileName);
                WaitForFileState(fileName, FileState.available);
                WaitForFilesTree();

                Assert.IsTrue(fileSystemService.IsFileAvailable(fileName));
                Assert.AreEqual(FileState.available, filesTreeNode.FileState);
            }

            { // Delete subdirectory
                var dirName = Path.Combine(TestFilesDirs[0].FullPath, @"SiblingDirectory02");
                Directory.Delete(dirName);
                WaitForFilesTree();

                var filesTreeNodes = FindNodesForDir(dirName);
                foreach (var node in filesTreeNodes)
                {
                    Assert.IsFalse(fileSystemService.IsFileAvailable(node.LocalFilePath));
                    Assert.AreEqual(FileState.missing, node.FileState);
                }
            }

            { // Rename subdirectory
                var dirName = Path.Combine(TestFilesDirs[0].FullPath, @"SiblingDirectory01");
                var filesTreeNodes = FindNodesForDir(dirName);

                Directory.Move(dirName, dirName + @"RENAME");
                ((LocalFileSystemService)SkylineWindow.FilesTree.FileSystemService.Delegate).TriggerAvailabilityMonitor();
                WaitForConditionUI(() => filesTreeNodes.All(node => node.FileState == FileState.missing));
                WaitForFilesTree();

                foreach (var node in filesTreeNodes)
                {
                    Assert.IsFalse(fileSystemService.IsFileAvailable(node.LocalFilePath));
                    Assert.AreEqual(FileState.missing, node.FileState);
                }

                Directory.Move(dirName + @"RENAME", dirName);
                ((LocalFileSystemService)SkylineWindow.FilesTree.FileSystemService.Delegate).TriggerAvailabilityMonitor();
                WaitForConditionUI(() => filesTreeNodes.All(node => node.FileState == FileState.available));
                WaitForFilesTree();

                foreach (var node in filesTreeNodes)
                {
                    Assert.IsTrue(fileSystemService.IsFileAvailable(node.LocalFilePath));
                    Assert.AreEqual(FileState.available, node.FileState);
                }
            }

            // { // Delete parent directory - tests (1) FileSystemHealthMonitor and (2) error handling in ManagedFileSystemWatcher
            //     var dirName = Path.Combine(TestFilesDirs[0].FullPath, @"SiblingDirectory03", @"ChildDirectory01");
            //     Directory.Move(dirName, dirName + @"RENAME");
            //     ((LocalFileSystemService)SkylineWindow.FilesTree.FileSystemService.Delegate).TriggerAvailabilityMonitor();
            //     Thread.Sleep(250); // Wait briefly for the availability monitor to trigger
            //     WaitForFilesTree();
            //
            //     var filesTreeNodes = FindNodesForDir(dirName);
            //     foreach (var node in filesTreeNodes)
            //     {
            //         Assert.IsFalse(fileSystemService.IsFileAvailable(node.LocalFilePath));
            //         Assert.AreEqual(FileState.missing, node.FileState);
            //     }
            // }
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

        private static FilesTreeNode FindNodeForPath(string filePath)
        {
            return RunUIFunc(() => SkylineWindow.FilesTree.FindNodesByFilePath(filePath)[0]);
        }

        private static IList<FilesTreeNode> FindNodesForDir(string dirPath)
        {
            return RunUIFunc(() => SkylineWindow.FilesTree.FindNodesByFilePath(dirPath));
        }

        /// <summary>
        /// Waits for FileSystemWatcher to detect a file state change and update the node.
        /// </summary>
        private static void WaitForFileState(string filePath, FileState expectedState)
        {
            WaitForConditionUI(() =>
            {
                var nodes = SkylineWindow.FilesTree.FindNodesByFilePath(filePath);
                return nodes.Count > 0 && nodes[0].FileState == expectedState;
            }, $"Timeout waiting for file state {expectedState} on path: {filePath}");
        }
    }
}
