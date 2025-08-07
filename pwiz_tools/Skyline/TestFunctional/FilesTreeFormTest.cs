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
            TestEmptyDocument();

            TestSaveAs();

            TestRatPlasmaDocument();
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

            AssertFilesTreeOnlyIncludesFilesTreeNodes(SkylineWindow.FilesTree.Root);

            AssertTopLevelFiles(new[] {typeof(SkylineAuditLog)}, SkylineWindow);

            Assert.IsFalse(SkylineWindow.FilesTree.IsMonitoringFileSystem());
            Assert.AreEqual(string.Empty, SkylineWindow.FilesTree.PathMonitoredForFileSystemChanges());

            const string fileName = "savedFileName.sky";
            var monitoredPath = Path.Combine(TestFilesDir.FullPath, fileName);
            RunUI(() => SkylineWindow.SaveDocument(monitoredPath));

            WaitForCondition(() => File.Exists(monitoredPath) && SkylineWindow.FilesTree.IsMonitoringFileSystem());

            Assert.AreEqual(Path.GetDirectoryName(monitoredPath), SkylineWindow.FilesTree.PathMonitoredForFileSystemChanges());

            // After saving, Window Layout (.sky.view) is present
            AssertTopLevelFiles(new[] { typeof(SkylineAuditLog), typeof(SkylineViewFile) }, SkylineWindow);

            // Skyline File
            Assert.AreEqual(fileName, SkylineWindow.FilesTree.Root.Name);
            AssertFileState(true, FileState.available, SkylineWindow.FilesTree.Root);
            // Audit Log
            AssertFileState(true, FileState.available, SkylineWindow.FilesTree.Root.NodeAt(0));
            // Window Layout
            AssertFileState(true, FileState.available, SkylineWindow.FilesTree.Root.NodeAt(1));

            // Close FilesTreeForm so test framework doesn't fail the test due to an unexpected open dialog
            RunUI(() => { SkylineWindow.DestroyFilesTreeForm(); });
        }

        protected void TestSaveAs()
        {
            const string origFileName = "SaveAsOne.sky";
            const string saveAsFileName = "SaveAsTwo.sky";

            var emptyDocument = SrmDocumentHelper.MakeEmptyDocument();
            RunUI(() => SkylineWindow.SwitchDocument(emptyDocument, null));
            WaitForDocumentLoaded();

            RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            // .sky file's local file path should not be initialized yet
            Assert.AreEqual(FileState.not_initialized, SkylineWindow.FilesTree.Root.NodeAt(0).FileState);
            Assert.IsNull(SkylineWindow.FilesTree.Root.NodeAt(0).LocalFilePath);

            // Save document for the first time
            var monitoredPath = Path.Combine(TestFilesDir.FullPath, origFileName);
            RunUI(() => SkylineWindow.SaveDocument(monitoredPath));
            WaitForCondition(() => File.Exists(monitoredPath) && SkylineWindow.FilesTree.IsMonitoringFileSystem());

            Assert.AreEqual(FileState.available, SkylineWindow.FilesTree.Root.NodeAt(0).FileState);
            Assert.IsNotNull(SkylineWindow.FilesTree.Root.NodeAt(0).LocalFilePath);
            Assert.AreEqual(Path.GetDirectoryName(monitoredPath), SkylineWindow.FilesTree.PathMonitoredForFileSystemChanges());

            // Save As - existing document saved to a new location
            monitoredPath = Path.Combine(TestFilesDir.FullPath, saveAsFileName);
            RunUI(() => SkylineWindow.SaveDocument(monitoredPath));
            WaitForCondition(() => File.Exists(monitoredPath) && SkylineWindow.FilesTree.IsMonitoringFileSystem());

            // Verify file state after SaveAs - files are available and paths updated
            var tree = SkylineWindow.FilesTree;
            Assert.AreEqual(Path.GetDirectoryName(monitoredPath), tree.PathMonitoredForFileSystemChanges());

            AssertTopLevelFiles(new[] { typeof(SkylineAuditLog), typeof(SkylineViewFile) }, SkylineWindow);

            Assert.AreEqual(saveAsFileName, tree.Root.Name);
            Assert.AreEqual(monitoredPath, tree.Root.FilePath);
            AssertFileState(true, FileState.available, tree.Root);

            Assert.AreEqual(SrmDocument.GetAuditLogPath(monitoredPath), tree.Root.NodeAt(0).LocalFilePath);
            AssertFileState(true, FileState.available, tree.Root.NodeAt(0));

            Assert.AreEqual(SkylineWindow.GetViewFile(monitoredPath), tree.Root.NodeAt(1).LocalFilePath);
            AssertFileState(true, FileState.available, tree.Root.NodeAt(1));

            // Close FilesTreeForm so test framework doesn't fail the test due to an unexpected open dialog
            RunUI(() => { SkylineWindow.DestroyFilesTreeForm(); });
        }

        protected void TestRatPlasmaDocument()
        {
            var documentPath = TestFilesDir.GetTestPath(RAT_PLASMA_FILE_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            // wait until UI loaded and Sequence Tree is visible
            WaitForConditionUI(() => SkylineWindow.SequenceTreeFormIsVisible);

            // make sure files tree exists but isn't visible
            var list = new List<FilesTreeForm>(SkylineWindow.DockPanel.Contents.OfType<FilesTreeForm>());
            Assert.AreEqual(1, list.Count);
            WaitForConditionUI(() => !SkylineWindow.FilesTreeFormIsVisible);

            // now, show files tree
            RunUI(() => SkylineWindow.ShowFilesTreeForm(true));
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible && SkylineWindow.FilesTree.IsMonitoringFileSystem());

            Assert.AreEqual(RAT_PLASMA_FILE_NAME, SkylineWindow.FilesTree.Root.Text);

            AssertTopLevelFiles(new[] { typeof(SkylineViewFile), typeof(ReplicatesFolder) , typeof(SpectralLibrariesFolder)}, SkylineWindow);

            Assert.AreEqual(Path.GetDirectoryName(documentPath), SkylineWindow.FilesTree.PathMonitoredForFileSystemChanges());
            AssertFileState(true, FileState.available, SkylineWindow.FilesTree.Root);

            AssertFilesTreeOnlyIncludesFilesTreeNodes(SkylineWindow.FilesTree.Root);

            TestRestoreViewState();

            //
            // Replicates
            //
            var doc = SkylineWindow.Document;

            // Check SrmSettings matches Model matches FilesTree UI
            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);

            //
            // Rename replicate by updating SkylineDocument directly, which should trigger FilesTree update
            //
            RunUI(() => SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[3].Expand());
            WaitForConditionUI(() => SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[3].IsExpanded);

            var newName = "Test this name";
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument("Rename replicate in FilesTree test", srmDoc =>
                {
                    var chromSet = doc.MeasuredResults.Chromatograms[3];
                    var newChromSet = (ChromatogramSet)chromSet.ChangeName(newName);

                    var measuredResults = doc.MeasuredResults;
                    var chromatograms = measuredResults.Chromatograms.ToArray();

                    chromatograms[3] = newChromSet; // replace existing with modified ChromatogramSet

                    measuredResults = measuredResults.ChangeChromatograms(chromatograms);
                    return doc.ChangeMeasuredResults(measuredResults);
                });
            });

            doc = WaitForDocumentChange(doc);

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);

            // make sure no new top-level folders were added
            Assert.AreEqual(3, SkylineWindow.FilesTree.Nodes[0].Nodes.Count);

            Assert.AreEqual(newName, doc.Settings.MeasuredResults.Chromatograms[3].Name);
            Assert.AreEqual(newName, SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[3].Name);

            // Expanded folder should remain expanded after a document update
            RunUI(() => Assert.IsTrue(SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[3].IsExpanded));

            //
            // Rename replicate by editing tree node's label
            //
            newName = "NEW REPLICATE NAME";
            var treeNode = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().NodeAt(0);
            RunUI(() => SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, newName));
            Assert.AreEqual(newName, SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[0].Name);

            //
            // Activating a replicate should update selected graphs
            //
            const int selectedIndex = 4;
            var filesTreeNode = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().NodeAt(selectedIndex);
            RunUI(() => { SkylineWindow.FilesTreeForm.ActivateReplicate(filesTreeNode); });
            WaitForGraphs();
            RunUI(() => Assert.AreEqual(selectedIndex, SkylineWindow.SelectedResultsIndex));

            //
            // Reorder replicate by editing SkylineDocument directly - should trigger FilesTree update
            //
            doc = SkylineWindow.Document;
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument("Reverse order of all replicates in FilesTree test", srmDoc =>
                {
                    var measuredResults = doc.MeasuredResults;
                    var chromatograms = measuredResults.Chromatograms.ToArray();
                    var reversedChromatograms = new List<ChromatogramSet>(chromatograms.Reverse());

                    measuredResults = measuredResults.ChangeChromatograms(reversedChromatograms);
                    return doc.ChangeMeasuredResults(measuredResults);
                });
            });

            WaitForDocumentChange(doc);

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);

            //
            // Remove replicate by editing SkylineDocument directly
            //
            doc = SkylineWindow.Document;
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument("Delete several replicates in FilesTree test", srmDoc =>
                {
                    var measuredResults = doc.MeasuredResults;
                    var chromatograms = new List<ChromatogramSet>(measuredResults.Chromatograms);
                    chromatograms.RemoveAt(3);
                    chromatograms.RemoveAt(5);
                    chromatograms.RemoveAt(7);
                    chromatograms.RemoveAt(9);

                    measuredResults = measuredResults.ChangeChromatograms(chromatograms);
                    return doc.ChangeMeasuredResults(measuredResults);
                });
            });
            doc = WaitForDocumentChange(doc);

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT - 4);

            //
            // Undo deletion of replicate should trigger FilesTree update and restore previously deleted nodes
            //
            RunUI(() => SkylineWindow.Undo());
            WaitForDocumentChange(doc);

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);

            // 
            // Hide FilesTree's tab and re-show making sure tree matches document and expected nodes are expanded
            //
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

            TestRemoveAllReplicates();

            TestRemoveSelectedReplicates();

            TestSpectralLibraries();

            TestMonitoringFileSystem();
            
            TestDragAndDrop();

            TestDragAndDropOnParentNode();

            // Close FilesTreeForm so test framework doesn't fail the test due to an unexpected open dialog
            RunUI(() => { SkylineWindow.DestroyFilesTreeForm(); });
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

        private static void TestRestoreViewState()
        {
            var treeNodes = SkylineWindow.FilesTree.Nodes;

            RunUI(() =>
            {
                // <file>.sky/
                //      Window Layout
                //      Replicates/
                //          <Replicate-Name>/
                //              <Replicate-Sample-File>.raw
                //      Spectral Libraries/
                //          ...
                Assert.IsTrue(treeNodes[0].IsExpanded);
                Assert.IsTrue(treeNodes[0].Nodes[1].IsExpanded);
                Assert.IsTrue(treeNodes[0].Nodes[1].Nodes[0].IsExpanded);
                Assert.IsTrue(treeNodes[0].Nodes[1].Nodes[10].IsExpanded);
                Assert.IsTrue(treeNodes[0].Nodes[2].IsExpanded);
            });
        }

        // Assumes rat-plasma.sky is loaded
        protected void TestMonitoringFileSystem()
        {
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
                var dnd = DragAndDrop<ReplicatesFolder>(new[] { 0 }, 1, DragDirection.down);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // A,B,C,D => Drag A to C => B,A,C,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 0 }, 2, DragDirection.down);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // B,A,C,D => Drag A to B => A,B,C,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 1 }, 0, DragDirection.up);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // A,B,C,D => Drag A to D => B,C,A,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 0 }, indexOfLastReplicate, DragDirection.down, MoveType.move_last);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // B,C,A,D => Drag D to B => A,B,C,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { indexOfLastReplicate }, 0, DragDirection.up);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // A,B,C,D,E => Drag A,B,C to E => D,A,B,C,E
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 3, 4, 5 }, 9, DragDirection.down);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                // D,A,B,C,E => Drag A,B,C to D => A,B,C,D,E
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 7, 8, 9 }, 3, DragDirection.up);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                dnd = DragAndDrop<ReplicatesFolder>(new[] { 3, 4, 5 }, indexOfLastReplicate, DragDirection.down);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);

                dnd = DragAndDrop<ReplicatesFolder>(new[] { indexOfLastReplicate - 2, indexOfLastReplicate - 1, indexOfLastReplicate }, 0, DragDirection.up);
                AssertDragAndDropResults<ReplicatesFolder>(dnd);
            }

            // Drag-and-drop - spectral libraries
            {
                var dnd = DragAndDrop<SpectralLibrariesFolder>(new[] { 0 }, 1, DragDirection.down, MoveType.move_last);
                AssertDragAndDropResults<SpectralLibrariesFolder>(dnd);

                dnd = DragAndDrop<SpectralLibrariesFolder>(new[] { 1 }, 0, DragDirection.up);
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
            var dndParams = DragAndDrop<ReplicatesFolder>(new[] { 1, 2, 3 }, -1, DragDirection.down, MoveType.move_last);
            AssertDragAndDropResults<ReplicatesFolder>(dndParams);

            dndParams = DragAndDrop<SpectralLibrariesFolder>(new[] { 0 }, -1, DragDirection.down, MoveType.move_last);
            AssertDragAndDropResults<SpectralLibrariesFolder>(dndParams);
        }

        private static DragAndDropParams DragAndDrop<T>(int[] dragNodeIndexes,
                                                        int dropNodeIndex,
                                                        DragDirection direction,
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

        private static void AssertDragAndDropResults<TFolder>(DragAndDropParams d) where TFolder : FileNode
        {
            // Check SrmSettings changed after a DnD operation
            // CONSIDER: do a more specific assert of changes to SrmSettings
            Assert.AreNotSame(d.OldDoc.Settings, d.NewDoc.Settings);

            // Verify document nodes match tree nodes
            AssertTreeFolderMatchesDocumentAndModel<TFolder>(d.Folder.Nodes.Count);

            // Is this action dropping dragged nodes on their parent folder?
            var doDropOnParentFolder = d.DropNodeIndex == -1;

            // Now, calculate the index where dragged nodes went when dropped on the tree
            int dropNodeExpectedIndex;
            if (doDropOnParentFolder)
            {
                dropNodeExpectedIndex = d.Folder.Nodes.Count - d.DraggedNodes.Count;
            }
            else if (d.MoveType == MoveType.move_last)
            {
                dropNodeExpectedIndex = d.DropNodeIndex - d.DraggedNodes.Count;
            }
            else if (d.Direction == DragDirection.down)
            {
                dropNodeExpectedIndex = d.DropNodeIndex;
            }
            else
            {
                dropNodeExpectedIndex = d.DropNodeIndex + d.DragNodeIndexes.Length;
            }

            // Assert the drop target is in the correct location
            if (doDropOnParentFolder)
                Assert.AreEqual(d.DropNode, d.Folder);
            else 
                Assert.AreEqual(d.DropNode, d.Folder.Nodes[dropNodeExpectedIndex]);

            // Compute the expected index of each dragged node and verify it's in the correct place
            for (var i = 0; i < d.DragNodeIndexes.Length; i++)
            {
                int expectedIndex;
                if (doDropOnParentFolder)
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

        private static void AssertTreeFolderMatchesDocumentAndModel<T>(int expectedCount) 
            where T : FileNode
        {
            var filesModel = SkylineFile.Create(SkylineWindow.Document, SkylineWindow.DocumentFilePath);

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
        
                Assert.AreSame(docNodes[i], modelNodes[i].Immutable);
                Assert.AreSame(docNodes[i], filesTreeNode.Model.Immutable);
        
                Assert.AreEqual(docNodes[i].Id, modelNodes[i].IdentityPath.GetIdentity(0));
                Assert.AreEqual(docNodes[i].Id, filesTreeNode.Model.IdentityPath.GetIdentity(0));
        
                Assert.AreEqual(docNodes[i].Name, modelNodes[i].Name);
                Assert.AreEqual(docNodes[i].Name, treeNodes[i].Name);
            }
        }

        // Make sure FilesTree only contains subclasses of FilesTreeNode
        private static void AssertFilesTreeOnlyIncludesFilesTreeNodes(TreeNode node)
        {
            Assert.IsInstanceOfType(node, typeof(FilesTreeNode));

            foreach (TreeNode treeNode in node.Nodes)
            {
                AssertFilesTreeOnlyIncludesFilesTreeNodes(treeNode);
            }
        }

        private static void AssertIsExpectedImage(ImageId expected, FilesTreeNode actual)
        {
            Assert.AreEqual((int)expected, actual.ImageIndex);
        }

        protected static void AssertTopLevelFiles(Type[] expectedModelTypes, SkylineWindow skylineWindow)
        {
            var filesTree = SkylineWindow.FilesTree;

            Assert.AreEqual(expectedModelTypes.Length, filesTree.Root.Nodes.Count);

            for (var i = 0; i < expectedModelTypes.Length; i++)
            {
                var filesTreeNode = filesTree.Root.NodeAt(i);

                Assert.IsInstanceOfType(filesTreeNode.Model, expectedModelTypes[i]);
            }
        }

        protected static void AssertFileState(bool expectedIsFileInitialized, FileState expectedFileState, FilesTreeNode filesTreeNode)
        {
            Assert.IsTrue(filesTreeNode.IsFileInitialized());
            Assert.AreEqual(FileState.available, filesTreeNode.FileState);
        }
    }

    internal enum DragDirection { up, down }

    internal class DragAndDropParams
    {
        internal int[] DragNodeIndexes;
        internal int DropNodeIndex; // Setting to -1 causes dragged nodes to drop on their parent folder
        internal DragDirection Direction;
        internal SrmDocument OldDoc;
        internal SrmDocument NewDoc;
        internal FilesTreeNode Folder;
        internal IList<FilesTreeNode> DraggedNodes;
        internal FilesTreeNode DropNode;
        internal MoveType MoveType;
    }
}
