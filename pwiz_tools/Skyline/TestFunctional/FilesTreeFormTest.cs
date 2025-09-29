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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline;
using pwiz.Skyline.Controls.FilesTree;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Files;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using static pwiz.Skyline.Model.Files.FileNode;

// CONSIDER: Replicate => verify right-click menu includes Open Containing Folder
// CONSIDER: Test Tooltips. See MethodEditTutorialTest.ShowNodeTip
// CONSIDER: add .sky file with imsdb, irtdb, protdb
// CONSIDER: add an Audit Log scenario
// CONSIDER: drag-and-drop disjoint selection
// CONSIDER: tree disallows dragging non-draggable nodes
// CONSIDER: use non-local file paths in SrmSettings (example: replicate sample files where SrmSettings paths point to directories that don't exist locally)

// TODO: test double-click on Background Proteome. Need an additional Skyline document with a Background Proteome
// TODO: test new .sky document, import asset backed file (ex: .protdb), assert file system watching before document saved for the first time
// TODO: add a new helper for getting a FilesTree node by model type to make this more readable: SkylineWindow.FilesTree.Root.NodeAt(0).FileState).
// TODO: add test making sure clicking upper RHC 'x' on confirm dialog does nto delete Replicate or Spectral Library

// TODO: local file system - change directory name containing raw files
// TODO: local file system - use raw files in directory that's a sibling of directory containing .sky file
// TODO: assert TopNode correctly restored from view state

// ReSharper disable WrongIndentSize
namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FilesTreeFormTest : AbstractFunctionalTest
    {
        internal const string RAT_PLASMA_FILE_NAME = "Rat_plasma.sky";
        internal const int RAT_PLASMA_REPLICATE_COUNT = 42;

        // No parallel testing because SkylineException.txt is written to Skyline.exe
        // directory by design - and that directory is shared by all workers
        [TestMethod, NoParallelTesting(TestExclusionReason.SHARED_DIRECTORY_WRITE)]
        public void TestFilesTreeForm()
        {
            TestFilesZip = @"TestFunctional\FilesTreeFormTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Non-UI tests
            TestFileSystemWatcherIgnoreList();
            TestSubdirectoryHelper();

            // UI tests
            TestEmptyDocument();
            ResetFilesTree();
            
            TestSave();
            ResetFilesTree();
            
            TestSaveAs();
            ResetFilesTree();

            TestRatPlasmaDocument();
            ResetFilesTree();
        }

        protected void TestFileSystemWatcherIgnoreList()
        {
            Assert.IsTrue(LocalFileSystem.IgnoreFileName(@"c:\Users\foobar\file.tmp"));
            Assert.IsTrue(LocalFileSystem.IgnoreFileName(@"c:\Users\foobar\file.bak"));
            Assert.IsTrue(LocalFileSystem.IgnoreFileName(null));
            Assert.IsTrue(LocalFileSystem.IgnoreFileName(string.Empty));
            Assert.IsTrue(LocalFileSystem.IgnoreFileName(@""));
            Assert.IsTrue(LocalFileSystem.IgnoreFileName(@"   "));

            Assert.IsFalse(LocalFileSystem.IgnoreFileName(@"c:\Users\foobar\file.sky"));
            Assert.IsFalse(LocalFileSystem.IgnoreFileName(@"c:\Users\foobar\file.xls"));
            Assert.IsFalse(LocalFileSystem.IgnoreFileName(@"c:\Users\foobar\file.txt"));
        }

        protected void TestSubdirectoryHelper()
        {
            Assert.IsTrue(LocalFileSystem.IsInDirectory(@"c:\Users\foobar\directory", @"c:\users\foobar\directory\child"));
            Assert.IsTrue(LocalFileSystem.IsInDirectory(@"c:\Users\foobar\directory\", @"c:\users\foobar\directory\child"));
            Assert.IsTrue(LocalFileSystem.IsInDirectory(@"c:\", @"c:\users\foobar\directory\child"));

            Assert.IsFalse(LocalFileSystem.IsInDirectory(@"c:\tmp\rat-plasma\", @"c:\users\foobar\tmp"));
            Assert.IsFalse(LocalFileSystem.IsInDirectory(@"d:\", @"c:\users\foobar\tmp"));
        }

        protected void TestEmptyDocument()
        {
            // In a new, empty document, FilesTree should be non-null, be visible but not active, and docked as second tab next to SequenceTree
            Assert.IsNotNull(SkylineWindow.FilesTreeForm);
            Assert.IsTrue(SkylineWindow.SequenceTreeFormIsVisible);
            Assert.IsTrue(SkylineWindow.FilesTreeFormIsVisible);
            Assert.IsFalse(SkylineWindow.FilesTreeFormIsActivated);

            var dockPane = (DigitalRune.Windows.Docking.DockPane)SkylineWindow.FilesTreeForm.Parent;
            Assert.AreEqual(2, dockPane.DisplayingContents.Count);
            Assert.IsInstanceOfType(dockPane.DisplayingContents[0], typeof(SequenceTreeForm));
            Assert.IsInstanceOfType(dockPane.DisplayingContents[1], typeof(FilesTreeForm));

            // Activate FilesTree
            RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            Assert.AreEqual(1, SkylineWindow.FilesTree.Nodes.Count);
            Assert.IsInstanceOfType(SkylineWindow.FilesTree.Root.Model, typeof(SkylineFile));
            Assert.AreEqual(FileResources.FileModel_NewDocument, SkylineWindow.FilesTree.Root.Text);

            AssertTreeOnlyHasFilesTreeNodes(SkylineWindow.FilesTree);

            AssertTopLevelFiles(typeof(SkylineAuditLog));

            Assert.AreEqual(FileSystemType.in_memory, SkylineWindow.FilesTree.FileSystemType);
            Assert.AreEqual(null, SkylineWindow.FilesTree.PathMonitoredForFileSystemChanges());
        }

        protected void TestSave() {

            const string fileName = "savedFileName.sky";

            // Create new, empty document
            {
                var emptyDocument = SrmDocumentHelper.MakeEmptyDocument();
                RunUI(() => SkylineWindow.SwitchDocument(emptyDocument, null));
                WaitForDocumentLoaded();

                RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
                WaitForFilesTree();

                Assert.AreEqual(FileSystemType.in_memory, SkylineWindow.FilesTree.FileSystemType);
                Assert.AreEqual(null, SkylineWindow.FilesTree.PathMonitoredForFileSystemChanges());
            }

            var monitoredPath = Path.Combine(TestFilesDir.FullPath, fileName);
            RunUI(() => SkylineWindow.SaveDocument(monitoredPath));
            WaitForFilesTree();

            Assert.AreEqual(FileSystemType.local_file_system, SkylineWindow.FilesTree.FileSystemType);
            Assert.AreEqual(Path.GetDirectoryName(monitoredPath), SkylineWindow.FilesTree.PathMonitoredForFileSystemChanges());

            // After saving, Window Layout (.sky.view) is present
            AssertTopLevelFiles(typeof(SkylineAuditLog), typeof(SkylineViewFile));

            // Skyline File
            Assert.AreEqual(fileName, SkylineWindow.FilesTree.Root.Name);
            AssertFileState(FileState.available, SkylineWindow.FilesTree.Root);

            // Audit Log
            AssertFileState(FileState.available, SkylineWindow.FilesTree.Root.NodeAt(0));

            // Window Layout
            AssertFileState(FileState.available, SkylineWindow.FilesTree.Root.NodeAt(1));
        }

        protected void TestSaveAs()
        {
            const string origFileName = "SaveAsOne.sky";
            const string saveAsFileName = "SaveAsTwo.sky";

            // Create new, empty document
            {
                var emptyDocument = SrmDocumentHelper.MakeEmptyDocument();
                RunUI(() => SkylineWindow.SwitchDocument(emptyDocument, null));
                WaitForDocumentLoaded();

                RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
                WaitForFilesTree();
            }

            Assert.AreEqual(FileSystemType.in_memory, SkylineWindow.FilesTree.FileSystemType);

            Assert.AreEqual(FileState.not_initialized, SkylineWindow.FilesTree.Root.NodeAt(0).FileState);
            Assert.IsNull(SkylineWindow.FilesTree.Root.NodeAt(0).LocalFilePath);

            // Save document for the first time
            var monitoredPath = Path.Combine(TestFilesDir.FullPath, origFileName);
            {
                RunUI(() => SkylineWindow.SaveDocument(monitoredPath));
                WaitForFilesTree();
            }

            Assert.AreEqual(Path.GetDirectoryName(monitoredPath), SkylineWindow.FilesTree.PathMonitoredForFileSystemChanges());

            // Audit Log
            Assert.AreEqual(FileState.available, SkylineWindow.FilesTree.Root.NodeAt(0).FileState);
            Assert.IsNotNull(SkylineWindow.FilesTree.Root.NodeAt(0).LocalFilePath);

            // Save the document to a new location - to test "Save As"
            {
                monitoredPath = Path.Combine(TestFilesDir.FullPath, saveAsFileName);
                RunUI(() => SkylineWindow.SaveDocument(monitoredPath));
                WaitForFilesTree();
            }

            // Check state of files and paths after saving
            var tree = SkylineWindow.FilesTree;
            Assert.AreEqual(FileSystemType.local_file_system, tree.FileSystemType);
            Assert.AreEqual(Path.GetDirectoryName(monitoredPath), tree.PathMonitoredForFileSystemChanges());

            AssertTopLevelFiles(typeof(SkylineAuditLog), typeof(SkylineViewFile));

            Assert.AreEqual(saveAsFileName, tree.Root.Name);
            Assert.AreEqual(monitoredPath, tree.Root.FilePath);
            AssertFileState(FileState.available, tree.Root);

            // Audit Log (.skyl)
            Assert.AreEqual(SrmDocument.GetAuditLogPath(monitoredPath), tree.Root.NodeAt(0).LocalFilePath);
            AssertFileState(FileState.available, tree.Root.NodeAt(0));

            // View Layout (.sky.view)
            Assert.AreEqual(SkylineWindow.GetViewFile(monitoredPath), tree.Root.NodeAt(1).LocalFilePath);
            AssertFileState(FileState.available, tree.Root.NodeAt(1));
        }

        protected void TestRatPlasmaDocument()
        {
            var documentPath = TestFilesDir.GetTestPath(RAT_PLASMA_FILE_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            // Wait until SequenceTree is visible - does not wait on FilesTree because it's not visible yet
            WaitForConditionUI(() => SkylineWindow.SequenceTreeFormIsVisible);

            // FilesTree should exist but not be visible
            Assert.AreEqual(1, SkylineWindow.DockPanel.Contents.OfType<FilesTreeForm>().Count());
            WaitForConditionUI(() => !SkylineWindow.FilesTreeFormIsVisible);

            // Show FilesTree
            RunUI(() => SkylineWindow.ShowFilesTreeForm(true));
            Assert.IsNotNull(SkylineWindow.FilesTree);
            WaitForFilesTree();

            Assert.AreEqual(RAT_PLASMA_FILE_NAME, SkylineWindow.FilesTree.Root.Text);

            AssertTopLevelFiles(typeof(SkylineViewFile), typeof(ReplicatesFolder), typeof(SpectralLibrariesFolder)); // ORIG
            // AssertTopLevelFiles(typeof(SkylineViewFile), typeof(SkylineChromatogramCache), typeof(ReplicatesFolder), typeof(SpectralLibrariesFolder));

            Assert.AreEqual(Path.GetDirectoryName(documentPath), SkylineWindow.FilesTree.PathMonitoredForFileSystemChanges());
            AssertFileState(FileState.available, SkylineWindow.FilesTree.Root);

            AssertTreeOnlyHasFilesTreeNodes(SkylineWindow.FilesTree);

            TestRestoreViewState();
            WaitForFilesTree();

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);

            //
            // Edit SrmDocument directly and make sure FilesTree updates correctly
            //

            // Rename replicate - expand node first to make sure expanded state is preserved after update
            RunUI(() => SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[3].Expand());
            WaitForConditionUI(() => SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[3].IsExpanded);

            // Check SrmSettings matches Model matches FilesTree UI
            var newName = "Test this name";
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument("Rename replicate in FilesTree test", srmDoc =>
                {
                    var chromSet = srmDoc.MeasuredResults.Chromatograms[3]; // D_103_REP1
                    var newChromSet = (ChromatogramSet)chromSet.ChangeName(newName);

                    var measuredResults = srmDoc.MeasuredResults;
                    var chromatograms = measuredResults.Chromatograms.ToArray();

                    chromatograms[3] = newChromSet; // replace existing with modified ChromatogramSet

                    measuredResults = measuredResults.ChangeChromatograms(chromatograms);
                    return srmDoc.ChangeMeasuredResults(measuredResults);
                });
            });
            WaitForFilesTree();

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);

            Assert.AreEqual(newName, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[3].Name);
            Assert.AreEqual(newName, SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[3].Name);

            // Expanded folder should remain expanded after a document update
            RunUI(() => Assert.IsTrue(SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[3].IsExpanded));

            //
            // Rename replicate by editing tree node's label
            //
            newName = "NEW REPLICATE NAME";
            var treeNode = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().NodeAt(0);
            RunUI(() => SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, newName));
            WaitForFilesTree();

            Assert.AreEqual(newName, SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[0].Name);

            //
            // Activating a replicate should update selected graphs
            //
            const int selectedIndex = 4;
            var filesTreeNode = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().NodeAt(selectedIndex);
            RunUI(() => { SkylineWindow.FilesTreeForm.ActivateReplicate(filesTreeNode); });
            WaitForFilesTree();
            RunUI(() => Assert.AreEqual(selectedIndex, SkylineWindow.SelectedResultsIndex));

            //
            // Reorder replicate by editing SkylineDocument directly - should trigger FilesTree update
            //
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument("Reverse order of all replicates in FilesTree test", srmDoc =>
                {
                    var measuredResults = srmDoc.MeasuredResults;
                    var chromatograms = measuredResults.Chromatograms.ToArray();
                    var reversedChromatograms = new List<ChromatogramSet>(chromatograms.Reverse());

                    measuredResults = measuredResults.ChangeChromatograms(reversedChromatograms);
                    return srmDoc.ChangeMeasuredResults(measuredResults);
                });
            });
            WaitForFilesTree();

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);
            // END

            //
            // Remove replicate by editing SkylineDocument directly
            //
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument("Delete several replicates in FilesTree test", srmDoc =>
                {
                    var measuredResults = srmDoc.MeasuredResults;
                    var chromatograms = new List<ChromatogramSet>(measuredResults.Chromatograms);

                    chromatograms.RemoveAt(3);
                    chromatograms.RemoveAt(5);
                    chromatograms.RemoveAt(7);
                    chromatograms.RemoveAt(9);

                    measuredResults = measuredResults.ChangeChromatograms(chromatograms);
                    return srmDoc.ChangeMeasuredResults(measuredResults);
                });
            });
            WaitForFilesTree();

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT - 4);

            RunUI(() => SkylineWindow.Undo());
            WaitForFilesTree();
            // END

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);

            TestShowAndHideRestoresState();

            TestRemoveAllReplicates();

            TestRemoveSelectedReplicates();

            TestSpectralLibraries();

            TestMonitoringFileSystem();
            
            TestDragAndDrop();

            TestDragAndDropOnParentNode();

            TestReplicateLabelEdit();
        }

        private static void TestRemoveAllReplicates()
        {
            var doc = SkylineWindow.Document;
            var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() =>
            {
                var replicatesFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
                SkylineWindow.FilesTreeForm.RemoveAll(replicatesFolder);
            });
            OkDialog(confirmDlg, confirmDlg.ClickYes);

            var replicatesFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            doc = WaitForDocumentChange(doc);
            Assert.IsNull(replicatesFolder);

            RunUI(() => SkylineWindow.Undo());
            WaitForDocumentChange(doc);
            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);
        }

        private static void TestRemoveSelectedReplicates()
        {
            var doc = SkylineWindow.Document;
            var replicatesFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var nodesToDelete = new List<FilesTreeNode>
            {
                replicatesFolder.NodeAt(4),
                replicatesFolder.NodeAt(5),
                replicatesFolder.NodeAt(7),
            };

            var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.FilesTreeForm.RemoveSelected(nodesToDelete));
            OkDialog(confirmDlg, confirmDlg.ClickYes);

            replicatesFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            doc = WaitForDocumentChange(doc);

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT - 3);

            // Deleted nodes should be gone
            Assert.IsFalse(replicatesFolder.HasChildWithName(nodesToDelete[0].Name));
            Assert.IsFalse(replicatesFolder.HasChildWithName(nodesToDelete[1].Name));
            Assert.IsFalse(replicatesFolder.HasChildWithName(nodesToDelete[2].Name));

            RunUI(() => SkylineWindow.Undo());
            WaitForDocumentChange(doc);

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);
            Assert.IsTrue(replicatesFolder.HasChildWithName(nodesToDelete[0].Name));
            Assert.IsTrue(replicatesFolder.HasChildWithName(nodesToDelete[1].Name));
            Assert.IsTrue(replicatesFolder.HasChildWithName(nodesToDelete[2].Name));
        }

        // Hide FilesTree's tab and re-show making sure tree matches document and expected nodes are expanded
        private static void TestShowAndHideRestoresState()
        {
            RunUI(() =>
            {
                SkylineWindow.ShowFilesTreeForm(false);

                var replicateNodes = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
                replicateNodes.Nodes[1].Expand();
                replicateNodes.Nodes[7].Expand();
                replicateNodes.Nodes[11].Expand();
                replicateNodes.Nodes[12].Expand();
            });
            WaitForConditionUI(() => !SkylineWindow.FilesTreeFormIsVisible);

            RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);

            RunUI(() =>
            {
                var replicateNodes = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
                Assert.IsTrue(replicateNodes.Nodes[1].IsExpanded);
                Assert.IsTrue(replicateNodes.Nodes[7].IsExpanded);
                Assert.IsTrue(replicateNodes.Nodes[11].IsExpanded);
                Assert.IsTrue(replicateNodes.Nodes[12].IsExpanded);
            });
        }

        private static void TestSpectralLibraries()
        {
            Assert.AreEqual(2, SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>()?.Nodes.Count);
            Assert.AreEqual("Rat (NIST) (Rat_plasma2)", SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name);
            Assert.AreEqual("Rat (GPM) (Rat_plasma2)", SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[1].Name);

            var peptideLibraryTreeNode = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>()?.NodeAt(0);
            var peptideLibraryModel = (SpectralLibrary)peptideLibraryTreeNode?.Model;
            Assert.AreEqual(SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name, peptideLibraryModel?.Name);

            // Open Library Explorer. Correct library should be selected.
            {
                RunUI(() => SkylineWindow.FilesTreeForm.OpenLibraryExplorerDialog(peptideLibraryTreeNode));
                var libraryDlg = WaitForOpenForm<ViewLibraryDlg>();
                RunUI(() =>
                {
                    Assert.IsTrue(libraryDlg.HasSelectedLibrary);
                    Assert.AreEqual(0, libraryDlg.SelectedIndex);
                    libraryDlg.Close();
                });
                WaitForConditionUI(() => !libraryDlg.Visible);
            }

            // Remove All
            FilesTreeNode librariesFolder;
            {
                var doc = SkylineWindow.Document;
                var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() =>
                {
                    librariesFolder = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>();
                    SkylineWindow.FilesTreeForm.RemoveAll(librariesFolder);
                });
                OkDialog(confirmDlg, confirmDlg.ClickYes);

                librariesFolder = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>();
                doc = WaitForDocumentChange(doc);
                Assert.IsNull(librariesFolder);

                RunUI(() => SkylineWindow.Undo());
                WaitForDocumentChange(doc);
                AssertTreeFolderMatchesDocumentAndModel<SpectralLibrariesFolder>(2);
            }

            // Remove selected
            {
                var doc = SkylineWindow.Document;
                librariesFolder = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>();
                var nodesToDelete = new List<FilesTreeNode>
                {
                    librariesFolder.NodeAt(1),
                };

                var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.FilesTreeForm.RemoveSelected(nodesToDelete));
                OkDialog(confirmDlg, confirmDlg.ClickYes);

                librariesFolder = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>();
                doc = WaitForDocumentChange(doc);

                Assert.AreEqual(1, librariesFolder.Nodes.Count);

                Assert.IsFalse(librariesFolder.HasChildWithName(nodesToDelete[0].Name));

                RunUI(() => SkylineWindow.Undo());
                WaitForDocumentChange(doc);

                AssertTreeFolderMatchesDocumentAndModel<SpectralLibrariesFolder>(2);

                // Removed libraries should be available again
                Assert.IsTrue(librariesFolder.HasChildWithName(nodesToDelete[0].Name));
            }
        }

        // Assumes rat-plasma.sky is loaded
        // Expected tree:
        //      <file>.sky/
        //          Window Layout
        //          Chromatograms
        //          Replicates/
        //              <Replicate-Name>/
        //                  <Replicate-Sample-File>.raw
        //              ...
        //          Spectral Libraries/
        //              <Library>
        //              ...
        private static void TestRestoreViewState()
        {
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.FilesTree.Root.IsExpanded);

                var replicatesFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
                Assert.IsTrue(replicatesFolder.IsExpanded);
                Assert.IsTrue(replicatesFolder.Nodes[0].IsExpanded);
                Assert.IsTrue(replicatesFolder.Nodes[10].IsExpanded);

                var spectralLibrariesFolder = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>();
                Assert.IsTrue(spectralLibrariesFolder.IsExpanded);
            });
        }

        // Assumes rat-plasma.sky is loaded
        protected void TestMonitoringFileSystem()
        {
            WaitForFilesTree();

            { // Smoke test finding nodes matching a directory name
                var documentFilePath = SkylineWindow.DocumentFilePath;
                var directoryName = Path.GetDirectoryName(documentFilePath);
                var matchingNodes = SkylineWindow.FilesTree.FindNodesByPath(directoryName);
                Assert.AreEqual(46, matchingNodes.Count);

                directoryName = Path.GetDirectoryName(directoryName);
                matchingNodes = SkylineWindow.FilesTree.FindNodesByPath(directoryName);
                Assert.AreEqual(46, matchingNodes.Count);
            }

            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicate = replicateFolder.NodeAt(0);
            var replicateSample = replicate.NodeAt(0);

            var filePath = replicateSample.LocalFilePath;
            Assert.IsTrue(File.Exists(filePath));
            Assert.AreEqual(FileState.available, replicateSample.FileState);

            // Rename a replicate sample file
            File.Move(filePath, filePath + "RENAMED");
            WaitForConditionUI(() => replicateSample.ImageIndex == (int)replicateSample.ImageMissing);

            // Check tree updates once the file is missing
            Assert.AreEqual(FileState.missing, replicateSample.FileState);
            AssertIsExpectedImage(replicateSample.ImageMissing, replicateSample);

            // Check parent tree nodes updated their icons to show the missing file. .sky file is not affected.
            AssertIsExpectedImage(replicate.ImageMissing, replicate);
            AssertIsExpectedImage(replicateFolder.ImageMissing, replicateFolder);
            AssertIsExpectedImage(ImageId.skyline, SkylineWindow.FilesTree.Root);

            // Restore file to its original name
            File.Move(filePath + "RENAMED", filePath);
            WaitForConditionUI(() => replicateSample.ImageIndex == (int)replicateSample.ImageAvailable);

            // Assert everything updates correctly
            Assert.AreEqual(FileState.available, replicateSample.FileState);
            AssertIsExpectedImage(replicateSample.ImageAvailable, replicateSample);

            AssertIsExpectedImage(replicate.ImageAvailable, replicate);
            AssertIsExpectedImage(replicateFolder.ImageAvailable, replicateFolder);
            AssertIsExpectedImage(ImageId.skyline, SkylineWindow.FilesTree.Root);

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);

            // Delete a replicate sample file
            replicate = replicateFolder.NodeAt(2);
            replicateSample = replicate.NodeAt(0);
            var replicateSampleModel = (ReplicateSampleFile)replicateSample.Model;

            Assert.IsNotNull(replicateSampleModel);

            filePath = replicateSample.LocalFilePath;
            Assert.IsTrue(File.Exists(filePath));

            File.Delete(filePath);
            WaitForCondition(() => !File.Exists(filePath));
            WaitForConditionUI(() => replicateSample.ImageIndex == (int)replicateSample.ImageMissing);

            // replicate sample file
            Assert.AreEqual(FileState.missing, replicateSample.FileState);
            AssertIsExpectedImage(replicateSample.ImageMissing, replicateSample);

            // and that parent nodes (replicate, replicate folder) changed their icons and sky file remains unchanged
            AssertIsExpectedImage(replicate.ImageMissing, replicate);
            AssertIsExpectedImage(replicateFolder.ImageMissing, replicateFolder);
            AssertIsExpectedImage(ImageId.skyline, SkylineWindow.FilesTree.Root);

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);
        }

        protected void TestDragAndDrop()
        {
            {
                // Start with a clean document
                var documentPath = TestFilesDir.GetTestPath(RAT_PLASMA_FILE_NAME);
                RunUI(() =>
                {
                    SkylineWindow.OpenFile(documentPath);
                    SkylineWindow.ShowFilesTreeForm(true);
                });
                WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

                var indexOfLastReplicate = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes.Count - 1;

                // A,B,C,D => Drag A to B => A,B,C,D
                var dnd = DragAndDrop<ReplicatesFolder>(new[] { 0 }, 1, DragAndDropDirection.down);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // A,B,C,D => Drag A to C => B,A,C,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 0 }, 2, DragAndDropDirection.down);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // B,A,C,D => Drag A to B => A,B,C,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 1 }, 0, DragAndDropDirection.up);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // A,B,C,D => Drag A to D => B,C,A,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 0 }, indexOfLastReplicate, DragAndDropDirection.down, MoveType.move_last);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // B,C,A,D => Drag D to B => A,B,C,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { indexOfLastReplicate }, 0, DragAndDropDirection.up);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // A,B,C,D,E => Drag A,B,C to E => D,A,B,C,E
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 3, 4, 5 }, 9, DragAndDropDirection.down);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // D,A,B,C,E => Drag A,B,C to D => A,B,C,D,E
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 7, 8, 9 }, 3, DragAndDropDirection.up);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                dnd = DragAndDrop<ReplicatesFolder>(new[] { 3, 4, 5 }, indexOfLastReplicate, DragAndDropDirection.down);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                dnd = DragAndDrop<ReplicatesFolder>(new[] { indexOfLastReplicate - 2, indexOfLastReplicate - 1, indexOfLastReplicate }, 0, DragAndDropDirection.up);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);
            }

            // Drag-and-drop - spectral libraries
            {
                // RunUI(() => { SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>().EnsureVisible(); });

                var dnd = DragAndDrop<SpectralLibrariesFolder>(new[] { 0 }, 1, DragAndDropDirection.down, MoveType.move_last);
                AssertDragAndDropResults<SpectralLibrariesFolder>(dnd);

                dnd = DragAndDrop<SpectralLibrariesFolder>(new[] { 1 }, 0, DragAndDropDirection.up);
                AssertDragAndDropResults<SpectralLibrariesFolder>(dnd);
            }

            // Drag-and-drop internals 
            {
                var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
                var replicateNode = replicateFolder.NodeAt(0);

                var libraryFolder = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>();
                var libraryNode = libraryFolder.NodeAt(0);

                // Replicate can be dropped on another replicate or parent folder but not on spectral library or library folder
                Assert.IsTrue(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(replicateFolder.NodeAt(1), replicateNode));
                Assert.IsTrue(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(replicateFolder, replicateNode));
                Assert.IsFalse(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(libraryFolder.NodeAt(0), replicateNode));
                Assert.IsFalse(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(libraryFolder, replicateNode));

                // Spectral library can be dropped on another library or parent folder but not replicate / replicate folder
                Assert.IsTrue(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(libraryFolder.NodeAt(0), libraryNode));
                Assert.IsTrue(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(libraryFolder, libraryNode));
                Assert.IsFalse(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(replicateFolder, libraryNode));
                Assert.IsFalse(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(replicateNode, libraryNode));
            }
        }

        protected void TestDragAndDropOnParentNode()
        {
            // Start with a clean document
            var documentPath = TestFilesDir.GetTestPath(RAT_PLASMA_FILE_NAME);
            RunUI(() =>
            {
                SkylineWindow.OpenFile(documentPath);
                SkylineWindow.ShowFilesTreeForm(true);
            });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            // A, B, C, D => Drag B, C to Parent Node => A, D, B, C
            var dndParams = DragAndDrop<ReplicatesFolder>(new[] { 1, 2, 3 }, -1, DragAndDropDirection.down, MoveType.move_last);
            AssertDragAndDropResults<ReplicatesFolder>(dndParams);

            dndParams = DragAndDrop<SpectralLibrariesFolder>(new[] { 0 }, -1, DragAndDropDirection.down, MoveType.move_last);
            AssertDragAndDropResults<SpectralLibrariesFolder>(dndParams);
        }

        protected void TestReplicateLabelEdit()
        {
            // Start with a clean document
            var documentPath = TestFilesDir.GetTestPath(RAT_PLASMA_FILE_NAME);
            RunUI(() =>
            {
                SkylineWindow.OpenFile(documentPath);
                SkylineWindow.ShowFilesTreeForm(true);
            });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            var replicatesFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            Assert.AreEqual(@"D_102_REP1", replicatesFolder.Nodes[0].Name);

            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.EditTreeNodeLabel((FilesTreeNode)replicatesFolder.Nodes[0], @"NEW NAME");
            });
            WaitForFilesTree();

            Assert.AreEqual(@"NEW NAME", replicatesFolder.Nodes[0].Name);
            Assert.AreEqual(@"NEW NAME", SkylineWindow.Document.MeasuredResults.Chromatograms[0].Name);
            Assert.AreEqual(@"NEW NAME", SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[0].Name);

            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.EditTreeNodeLabel((FilesTreeNode)replicatesFolder.Nodes[1], @"ANOTHER NEW NAME");
            });
            WaitForFilesTree();

            Assert.AreEqual(@"ANOTHER NEW NAME", replicatesFolder.Nodes[1].Name);
            Assert.AreEqual(@"ANOTHER NEW NAME", SkylineWindow.Document.MeasuredResults.Chromatograms[1].Name);
            Assert.AreEqual(@"ANOTHER NEW NAME", SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[1].Name);

            // Make sure the first name remains intact
            Assert.AreEqual(@"NEW NAME", replicatesFolder.Nodes[0].Name);
            Assert.AreEqual(@"NEW NAME", SkylineWindow.Document.MeasuredResults.Chromatograms[0].Name);
            Assert.AreEqual(@"NEW NAME", SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[0].Name);
        }

        private static void ResetFilesTree()
        {
            // Close FilesTreeForm so test framework doesn't fail the test due to an unexpected open dialog
            RunUI(() => { SkylineWindow.DestroyFilesTreeForm(); });
        }

        /// <summary>
        /// Helper method that performs a drag-and-drop operation on a FilesTree folder of type <typeparamref name="T"/>.
        /// For example, a folder of type <see cref="ReplicatesFolder"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dragNodeIndexes"></param>
        /// <param name="dropNodeIndex"></param>
        /// <param name="direction"></param>
        /// <param name="moveType"></param>
        /// <returns></returns>
        private static DragAndDropParams DragAndDrop<T>(int[] dragNodeIndexes,
                                                        int dropNodeIndex,
                                                        DragAndDropDirection direction,
                                                        MoveType moveType = MoveType.move_to)
            where T : FileNode
        {
            var folder = SkylineWindow.FilesTree.Folder<T>();

            var dragNodes = dragNodeIndexes.Select(index => folder.Nodes[index]).Cast<FilesTreeNode>().ToList();

            var selectedNode = dragNodes.Last();
            var dropNode = dropNodeIndex == -1 ? folder : folder.NodeAt(dropNodeIndex);

            var oldDoc = SkylineWindow.Document;
            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.DropNodes(dragNodes, selectedNode, dropNode, moveType, DragDropEffects.Move);
            });
            var newDoc = WaitForDocumentChangeLoaded(oldDoc);

            folder = SkylineWindow.FilesTree.Folder<T>();

            return new DragAndDropParams
            {
                DragNodeIndexes = dragNodeIndexes,
                DraggedNodes = dragNodes,

                Direction = direction,

                DropNodeIndex = dropNodeIndex,
                DropNode = dropNode,
                MoveType = moveType,

                Folder = folder,

                OldDoc = oldDoc,
                NewDoc = newDoc
            };
        }

        /// <summary>
        /// Assert the results of a drag-and-drop operation. Performs several checks:
        /// 
        ///     (1) SrmSettings changed
        ///     (2) A FilesTree's folder matches SrmDocument
        ///     (3) A FilesTree's model matches SrmDocument
        ///     (4) Dragged node(s) are the correct place
        ///     (5) Drop node is in the correct place
        ///     (6) Dragged node(s) remain selected
        ///     (7) Primary selected node is the last dragged node
        ///     (8) No other nodes are selected
        ///     (9) FilesTree only contains nodes assignable to <see cref="FilesTreeNode"/>
        ///
        /// Checking #2 and #3 requires the caller to specify the type of model to check. Each model maps to a different location in SrmDocument.
        /// </summary>
        /// <typeparam name="TFolder"></typeparam>
        /// <param name="d"></param>
        private static void AssertDragAndDropResults<TFolder>(DragAndDropParams d) where TFolder : FileNode
        {
            // CONSIDER: assert a more specific location in SrmSettings changed, which would differ depending on the type of {TFolder}
            // Check SrmSettings changed after a DnD operation
            Assert.AreNotSame(d.OldDoc.Settings, d.NewDoc.Settings);

            // Verify nodes in the SrmDocument match both the FilesTree model and what is displayed in the UI
            AssertTreeFolderMatchesDocumentAndModel<TFolder>(d.Folder.Nodes.Count);

            // Is this action dropping dragged nodes on their parent folder?
            var droppedOnParentFolder = d.DropNodeIndex == -1;

            // Calculate the index where the dragged nodes will appear after they were dropped on the tree
            int dropNodeExpectedIndex;
            if (droppedOnParentFolder)
            {
                dropNodeExpectedIndex = d.Folder.Nodes.Count - d.DraggedNodes.Count;
            }
            else if (d.MoveType == MoveType.move_last)
            {
                dropNodeExpectedIndex = d.DropNodeIndex - d.DraggedNodes.Count;
            }
            else if (d.Direction == DragAndDropDirection.down)
            {
                dropNodeExpectedIndex = d.DropNodeIndex;
            }
            else
            {
                dropNodeExpectedIndex = d.DropNodeIndex + d.DragNodeIndexes.Length;
            }

            // Assert the drop target is in the correct location
            if (droppedOnParentFolder)
            {
                Assert.AreEqual(d.DropNode, d.Folder);
            }
            else
            {
                Assert.AreEqual(d.DropNode, d.Folder.Nodes[dropNodeExpectedIndex]);
            }

            // Compute the expected index of each dragged node and verify it's in the correct place
            for (var i = 0; i < d.DragNodeIndexes.Length; i++)
            {
                int expectedIndex;
                if (droppedOnParentFolder)
                {
                    expectedIndex = dropNodeExpectedIndex + i;
                }
                else if (d.MoveType == MoveType.move_last)
                {
                    expectedIndex = dropNodeExpectedIndex + 1 + i;
                }
                else
                {
                    expectedIndex = dropNodeExpectedIndex - d.DraggedNodes.Count + i;
                }

                Assert.AreEqual(d.DraggedNodes[i], d.Folder.Nodes[expectedIndex]);

                // Dropped nodes should remain selected after drag-and-drop completes
                Assert.IsTrue(d.Folder.NodeAt(expectedIndex).IsInSelection);

                RunUI(() => Assert.AreEqual(d.DraggedNodes.Last(), SkylineWindow.FilesTree.SelectedNode));
            }
        }

        /// <summary>
        /// Assert a folder whose model is of type <typeparamref name="T"/> has nodes that match the current <see cref="SrmDocument"/> and FilesTree model.
        /// </summary>
        /// <typeparam name="T">Model to check. For example, <see cref="ReplicatesFolder"/>.</typeparam>
        /// <param name="expectedCount">Expected number of child nodes. Only counts direct children - not recursive children.</param>
        /// <exception cref="ArgumentException"></exception>
        private static void AssertTreeFolderMatchesDocumentAndModel<T>(int expectedCount) 
            where T : FileNode
        {
            var filesModel = SkylineFile.Create(SkylineWindow.Document, SkylineWindow.DocumentFilePath);

            // To get document nodes, we need to know which part of SrmDocument to check based on the type of model. 
            // Currently only supports checking the Replicates\ or Spectral Libraries\ folders. Add new cases to the
            // switch statement to check different types of models.
            IList<IFile> docNodes = typeof(T) switch
            {
                { } type when type == typeof(ReplicatesFolder) => 
                    SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Cast<IFile>().ToList(),
                { } type when type == typeof(SpectralLibrariesFolder) => 
                    SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs.Cast<IFile>().ToList(),
                _ => throw new ArgumentException($"Unsupported folder type {typeof(T)}")
            };
        
            var modelNodes = filesModel.Folder<T>().Files;
            var treeNodes = SkylineWindow.FilesTree.Folder<T>().Nodes;
        
            Assert.IsNotNull(docNodes);
            Assert.IsNotNull(modelNodes);
            Assert.IsNotNull(treeNodes);
        
            Assert.AreEqual(expectedCount, docNodes.Count);
            Assert.AreEqual(expectedCount, modelNodes.Count);
            Assert.AreEqual(expectedCount, treeNodes.Count);
        
            for (var i = 0; i < docNodes.Count; i++)
            {
                var filesTreeNode = (FilesTreeNode)treeNodes[i];
                

                Assert.AreEqual(docNodes[i].Id, modelNodes[i].IdentityPath.GetIdentity(0));
                Assert.AreEqual(docNodes[i].Id, filesTreeNode.Model.IdentityPath.GetIdentity(0));

                Assert.AreEqual(docNodes[i].Name, modelNodes[i].Name);
                Assert.AreEqual(docNodes[i].Name, treeNodes[i].Name);
            }
        }

        // FilesTree should only contain nodes assignable to FilesTreeNode
        /// <summary>
        /// Assert FilesTree only contains nodes assignable to <see cref="FilesTreeNode"/>.
        /// </summary>
        /// <param name="filesTree">Tree to check</param>
        private static void AssertTreeOnlyHasFilesTreeNodes(FilesTree filesTree)
        {
            CheckTree(filesTree.Root);
            return;

            void CheckTree(TreeNode node)
            {
                Assert.IsInstanceOfType(node, typeof(FilesTreeNode));

                foreach (TreeNode treeNode in node.Nodes)
                {
                    CheckTree(treeNode);
                }
            }
        }

        /// <summary>
        /// Assert the FilesTreeNode has the expected image.
        /// </summary>
        /// <param name="expected">Expected ImageId</param>
        /// <param name="actual">FilesTreeNode whose image to check</param>
        private static void AssertIsExpectedImage(ImageId expected, FilesTreeNode actual)
        {
            Assert.AreEqual((int)expected, actual.ImageIndex);
        }

        /// <summary>
        /// Assert tree nodes immediately below the root node (.sky file) match the expected model types and appear in the expected order.
        /// This check should only be used for nodes expected to be immediate children of FilesTree's root node (for the .sky file) and
        /// include nodes like <see cref="SkylineAuditLog"/>, <see cref="SkylineViewFile"/>, and <see cref="ReplicatesFolder"/>.
        /// </summary>
        /// <param name="expectedModelTypes">Expected model types</param>
        protected static void AssertTopLevelFiles(params Type[] expectedModelTypes)
        {
            var filesTree = SkylineWindow.FilesTree;

            Assert.AreEqual(expectedModelTypes.Length, filesTree.Root.Nodes.Count);

            for (var i = 0; i < expectedModelTypes.Length; i++)
            {
                var filesTreeNode = filesTree.Root.NodeAt(i);

                Assert.IsInstanceOfType(filesTreeNode.Model, expectedModelTypes[i]);
            }
        }

        /// <summary>
        /// Assert the FilesTreeNode has the expected <see cref="FileState"/>.
        /// </summary>
        /// <param name="expected">Expected FileState</param>
        /// <param name="filesTreeNode">FilesTreeNode whose file state to check</param>
        protected static void AssertFileState(FileState expected, FilesTreeNode filesTreeNode)
        {
            Assert.AreEqual(expected, filesTreeNode.FileState);
        }

        /// <summary>
        /// Wait for the FilesTree to finish loading and processing any changes to the document. Includes finishing work
        /// queued for async processing on a background thread or on the UI thread.
        /// </summary>
        private static void WaitForFilesTree()
        {
            WaitForConditionUI(() => SkylineWindow.FilesTree.IsComplete());
        }
    }

    /// <summary>
    /// Indicates whether a drag-and-drop operation is moving nodes up or down in the tree.
    /// </summary>
    internal enum DragAndDropDirection { up, down }

    /// <summary>
    /// Holds parameters and results of a drag-and-drop operation. Passed to the method that
    /// asserts the results of a drag-and-drop operation: 
    /// <see cref="FilesTreeFormTest.AssertDragAndDropResults{TFolder}(DragAndDropParams)"/>.
    /// </summary>
    internal class DragAndDropParams
    {
        internal int[] DragNodeIndexes;
        internal int DropNodeIndex; // Setting to -1 causes dragged nodes to drop on the parent folder
        internal DragAndDropDirection Direction;
        internal SrmDocument OldDoc;
        internal SrmDocument NewDoc;
        internal FilesTreeNode Folder;
        internal IList<FilesTreeNode> DraggedNodes;
        internal FilesTreeNode DropNode;
        internal MoveType MoveType;
    }
}