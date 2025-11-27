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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.FilesTree;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Files;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using static pwiz.Skyline.Model.Files.FileModel;

// CONSIDER: test additional file types - imsdb, irtdb, protdb
// CONSIDER: double-click on a Background Proteome opens the correct dialog
// CONSIDER: verify right-click menu includes open containing folder only for files found locally
// CONSIDER: test tooltips, see example in MethodEditTutorialTest.ShowNodeTip
// CONSIDER: expand drag-and-drop tests - scenarios: disjoint selection, tree disallows dragging un-draggable nodes
// CONSIDER: handling of non-local paths from SrmSettings (ex: replicate sample file paths cannot be found locally)
// CONSIDER: new test making sure clicking 'x' upper RHC of confirm dialog does not delete Replicate / Spectral Library

// TODO: improve test readability with a helper that gets node by model type

// ReSharper disable WrongIndentSize
namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FilesTreeFormTest : AbstractFunctionalTest
    {
        internal const string RAT_PLASMA_FILE_NAME = @"Rat_plasma.sky";
        internal const int RAT_PLASMA_REPLICATE_COUNT = 42;

        // No parallel testing because SkylineException.txt is written to Skyline.exe
        // directory by design - and that directory is shared by all workers
        [TestMethod, NoParallelTesting(TestExclusionReason.SHARED_DIRECTORY_WRITE)]
        public void TestFilesTreeForm()
        {
            // These test files are large (30MB) so reuse rather than duplicate
            TestFilesZipPaths = new[]
            {
                @"TestFunctional\FilesTreeFormTest.zip",
                @"https://skyline.ms/tutorials/GroupedStudies.zip"  // Rat_plasma.sky
            };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Non-UI tests
            TestFileSystemWatcherIgnoreList();
            TestFileSystemHelpers();

            // UI tests
            TestEmptyDocument();
            TestSave();
            TestSaveAs();
            TestRatPlasmaDocument();
            TestUpgradeExistingDocumentWithFilesTree();
        }

        protected void TestFileSystemWatcherIgnoreList()
        {
            Assert.IsTrue(LocalFileSystemService.ShouldIgnoreFile(@"c:\Users\foobar\file.tmp"));
            Assert.IsTrue(LocalFileSystemService.ShouldIgnoreFile(@"c:\Users\foobar\file.bak"));
            Assert.IsTrue(LocalFileSystemService.ShouldIgnoreFile(null));
            Assert.IsTrue(LocalFileSystemService.ShouldIgnoreFile(string.Empty));
            Assert.IsTrue(LocalFileSystemService.ShouldIgnoreFile(@""));
            Assert.IsTrue(LocalFileSystemService.ShouldIgnoreFile(@"   "));

            Assert.IsFalse(LocalFileSystemService.ShouldIgnoreFile(@"c:\Users\foobar\file.sky"));
            Assert.IsFalse(LocalFileSystemService.ShouldIgnoreFile(@"c:\Users\foobar\file.xls"));
            Assert.IsFalse(LocalFileSystemService.ShouldIgnoreFile(@"c:\Users\foobar\file.txt"));
            Assert.IsFalse(LocalFileSystemService.ShouldIgnoreFile(@"c:\Users\foobar\file.raw"));
            Assert.IsFalse(LocalFileSystemService.ShouldIgnoreFile(@"c:\Users\foobar\file.RAW"));
        }

        protected void TestFileSystemHelpers()
        {
            // Is file contained in directory?
            Assert.IsTrue(FileSystemUtil.IsFileInDirectory(@"c:\Users\foobar\directory", @"c:\users\foobar\directory\child.txt"));
            Assert.IsTrue(FileSystemUtil.IsFileInDirectory(@"c:\Users\foobar\directory\", @"c:\users\foobar\directory\child.txt"));
            Assert.IsTrue(FileSystemUtil.IsFileInDirectory(@"c:\Users\foobar\directory\\", @"c:\users\foobar\directory\child.txt"));
            Assert.IsFalse(FileSystemUtil.IsFileInDirectory(@"c:\Users\foobar\", @"c:\users\foobar\directory\child.txt"));
            Assert.IsFalse(FileSystemUtil.IsFileInDirectory(@"c:\Users\", @"c:\users\foobar\directory\child.txt"));
            Assert.IsFalse(FileSystemUtil.IsFileInDirectory(@"c:\", @"c:\users\foobar\directory\child.txt"));

            Assert.IsFalse(FileSystemUtil.IsFileInDirectory(@"c:\tmp\rat-plasma\", @"c:\users\foobar\tmp"));
            Assert.IsFalse(FileSystemUtil.IsFileInDirectory(@"d:\", @"c:\users\foobar\tmp"));

            // Do two string paths refer to the same directory?
            Assert.IsTrue(FileSystemUtil.IsInOrSubdirectoryOf(@"c:\Users\foobar\directory", @"c:\users\foobar\directory"));
            Assert.IsTrue(FileSystemUtil.IsInOrSubdirectoryOf(@"c:\Users\foobar\directory", @"c:\users\foobar\directory\"));
            Assert.IsTrue(FileSystemUtil.IsInOrSubdirectoryOf(@"c:\Users\foobar\directory", @"c:\users\foobar\directory\\"));
            Assert.IsTrue(FileSystemUtil.IsInOrSubdirectoryOf(@"c:\Users\foobar\directory\", @"c:\users\foobar\directory"));
            Assert.IsTrue(FileSystemUtil.IsInOrSubdirectoryOf(@"c:\Users\foobar\directory\\", @"c:\users\foobar\directory"));
            Assert.IsTrue(FileSystemUtil.IsInOrSubdirectoryOf(@"c:\Users\foobar\directory\", @"c:\users\foobar\directory\\"));
            Assert.IsTrue(FileSystemUtil.IsInOrSubdirectoryOf(@"C:\USERS\FOOBAR\DIRECTORY\", @"c:\users\foobar\directory\\"));

            Assert.IsFalse(FileSystemUtil.IsInOrSubdirectoryOf(@"c:\Users\foobar\directory", @"c:\users\foobar\directory\subdirectory"));
            Assert.IsFalse(FileSystemUtil.IsInOrSubdirectoryOf(@"c:\Users\foobar\directory", @"c:\users\foobar\dir"));

            Assert.IsTrue(FileSystemUtil.PathEquals(@"C:\Users\Foobar\directory\foo.mzML", @"c:\users\foobar\directory\foo.mzml"));
        }

        protected void TestEmptyDocument()
        {
            // In a new, empty document, FilesTree should be non-null, be visible but not active, and docked as second tab next to SequenceTree
            Assert.IsNotNull(SkylineWindow.FilesTreeForm);
            Assert.IsTrue(SkylineWindow.SequenceTreeFormIsVisible);
            Assert.IsTrue(SkylineWindow.FilesTreeFormIsVisible);
            Assert.IsFalse(SkylineWindow.FilesTreeFormIsActivated);

            var dockPane = SkylineWindow.FilesTreeForm.ParentDockPane;
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

            Assert.AreEqual(0, SkylineWindow.FilesTree.MonitoredDirectories().Count);
        }

        protected void TestSave()
        {
            const string fileName = "SavedFile.sky";

            // Create new, empty document
            {
                var emptyDocument = SrmDocumentHelper.MakeEmptyDocument();
                RunUI(() => SkylineWindow.SwitchDocument(emptyDocument, null));
                WaitForDocumentLoaded();

                RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
                WaitForFilesTree();

                Assert.AreEqual(0, SkylineWindow.FilesTree.MonitoredDirectories().Count);
            }

            var monitoredPath = Path.Combine(TestFilesDirs[0].FullPath, @"TestSave", fileName);
            RunUI(() => SkylineWindow.SaveDocument(monitoredPath));
            WaitForFilesTree();

            Assert.AreEqual(FileSystemType.local_file_system, SkylineWindow.FilesTree.FileSystemType());
            Assert.AreEqual(1, SkylineWindow.FilesTree.MonitoredDirectories().Count);
            Assert.IsTrue(SkylineWindow.FilesTree.IsMonitoringDirectory(monitoredPath));

            // Window Layout (.sky.view) appears in tree after document saved
            AssertTopLevelFiles(typeof(SkylineAuditLog), typeof(SkylineViewFile));

            // Skyline File
            Assert.AreEqual(fileName, SkylineWindow.FilesTree.Root.Name);
            AssertFileState(FileState.available, SkylineWindow.FilesTree.Root); // .sky file
            AssertFileState(FileState.available, SkylineWindow.FilesTree.Root.NodeAt(0)); // .skyl file
            AssertFileState(FileState.available, SkylineWindow.FilesTree.Root.NodeAt(1)); // .view file
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

            RunUI(() => Assert.IsTrue(SkylineWindow.FilesTree.Root.IsExpanded));

            Assert.AreEqual(0, SkylineWindow.FilesTree.MonitoredDirectories().Count);

            // Audit Log
            Assert.IsNull(SkylineWindow.FilesTree.Root.NodeAt(0).LocalFilePath);

            // Save document for the first time
            var monitoredPath = Path.Combine(TestFilesDirs[0].FullPath, @"TestSaveAs", origFileName);
            {
                RunUI(() => SkylineWindow.SaveDocument(monitoredPath));
                WaitForFilesTree();
            }

            Assert.AreEqual(FileSystemType.local_file_system, SkylineWindow.FilesTree.FileSystemType());
            Assert.AreEqual(1, SkylineWindow.FilesTree.MonitoredDirectories().Count);
            Assert.IsTrue(SkylineWindow.FilesTree.IsMonitoringDirectory(monitoredPath));

            // Audit Log
            Assert.AreEqual(FileState.available, SkylineWindow.FilesTree.Root.NodeAt(0).FileState);
            Assert.IsNotNull(SkylineWindow.FilesTree.Root.NodeAt(0).LocalFilePath);

            // Save the document to a new location - to test "Save As"
            {
                monitoredPath = Path.Combine(TestFilesDirs[0].FullPath, @"TestSaveAs", saveAsFileName);
                RunUI(() => SkylineWindow.SaveDocument(monitoredPath));
                WaitForFilesTree();
            }

            // Check state of files and paths after saving
            var tree = SkylineWindow.FilesTree;
            Assert.AreEqual(FileSystemType.local_file_system, tree.FileSystemType());
            Assert.AreEqual(1, SkylineWindow.FilesTree.MonitoredDirectories().Count);
            Assert.IsTrue(SkylineWindow.FilesTree.IsMonitoringDirectory(monitoredPath));

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

        protected void TestUpgradeExistingDocumentWithFilesTree()
        {
            var documentPath = Path.Combine(TestFilesDirs[0].FullPath, @"TestUpgradeExistingDocumentWithFilesTree", @"UpgradeWithFilesTree.sky");

            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForDocumentLoaded();

            Assert.IsNotNull(SkylineWindow.FilesTreeForm);
            Assert.IsNotNull(SkylineWindow.FilesTree);

            Assert.IsTrue(SkylineWindow.SequenceTreeFormIsVisible);
            Assert.IsFalse(SkylineWindow.FilesTreeFormIsVisible);

            string persistentString = null;
            RunUI(() =>
            {
                // Trivial change so the document can be saved
                SkylineWindow.Paste(@"PEPTIDEK");

                var dockPane = SkylineWindow.FilesTreeForm.ParentDockPane;
                var contents = dockPane.Contents;

                Assert.IsTrue(dockPane.Visible);
                Assert.AreEqual(DockState.DockLeft, dockPane.DockState);
                Assert.AreEqual(2, contents.Count);
                Assert.IsTrue(contents[0] is SequenceTreeForm);
                Assert.IsTrue(contents[1] is FilesTreeForm);

                var sequenceTreeForm = SkylineWindow.SequenceTree.Parent.Parent as SequenceTreeForm;
                persistentString = sequenceTreeForm?.GetPersistentStringForTests();
            });

            Assert.IsNotNull(persistentString);
            Assert.IsTrue(persistentString.Contains(@"|" + FilesTree.FILES_TREE_SHOWN_ONCE_TOKEN));

            RunUI(() =>
            {
                SkylineWindow.ShowFilesTreeForm(false);
                SkylineWindow.SaveDocument();

                SkylineWindow.NewDocument();
                WaitForDocumentLoaded();

                SkylineWindow.OpenFile(documentPath);
            });

            // CONSIDER: Assert the .view file contains the FilesTreeShownOnce token

            // FilesTree should not be shown after it was shown and closed in the last session
            Assert.IsNotNull(SkylineWindow.SequenceTree);
            Assert.IsTrue(SkylineWindow.SequenceTreeFormIsVisible);
            Assert.IsFalse(SkylineWindow.FilesTreeFormIsVisible);

            // CONSIDER: count the number of tabs in the DigitalRune DockPane. Unclear how to 
            //           do this without using reflection since tab count is in a private member
            //           at dockPane.TabStripControl.Tabs.Count.
        }

        protected void TestRatPlasmaDocument()
        {
            PrepareRatPlasmaFile(TestFilesDirs[0], TestFilesDirs[1]);
            var documentPath = TestFilesDirs[0].GetTestPath(RAT_PLASMA_FILE_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            // Wait until SequenceTree is visible - does not wait on FilesTree because it's not visible yet
            WaitForConditionUI(() => SkylineWindow.SequenceTreeFormIsVisible);

            // FilesTree should exist but not be visible
            Assert.AreEqual(1, SkylineWindow.DockPanel.Contents.OfType<FilesTreeForm>().Count());
            WaitForConditionUI(() => !SkylineWindow.FilesTreeFormIsVisible);

            // Show FilesTree
            RunUI(() => SkylineWindow.ShowFilesTreeForm(true));
            WaitForDocumentLoaded(); // waits until .skyd file is ready
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible && SkylineWindow.FilesTree.IsComplete());

            Assert.AreEqual(FileSystemType.local_file_system, SkylineWindow.FilesTree.FileSystemType());
            Assert.AreEqual(1, SkylineWindow.FilesTree.MonitoredDirectories().Count);
            Assert.IsTrue(SkylineWindow.FilesTree.IsMonitoringDirectory(documentPath));

            AssertTreeOnlyHasFilesTreeNodes(SkylineWindow.FilesTree);

            AssertTopLevelFiles(typeof(SkylineViewFile), typeof(SkylineChromatogramCache), typeof(ReplicatesFolder), typeof(SpectralLibrariesFolder));

            Assert.AreEqual(RAT_PLASMA_FILE_NAME, SkylineWindow.FilesTree.Root.Text);
            AssertFileState(FileState.available, SkylineWindow.FilesTree.Root);

            TestRestoreViewState();

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
            // Edit tree node labels
            //
            newName = "NEW REPLICATE NAME";
            var treeNode = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().NodeAt(0);
            RunUI(() =>
            {
                var auditLogSize = SkylineWindow.Document.AuditLog.AuditLogEntries.Count;
                var cancelEvent = SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, newName);
                Assert.IsFalse(cancelEvent);
                // Check no new entries were added to the audit log
                Assert.AreEqual(auditLogSize + 1, SkylineWindow.Document.AuditLog.AuditLogEntries.Count);
            });
            WaitForFilesTree();

            Assert.AreEqual(newName, SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[0].Name);

            // Input validation - attempt to rename node to the same name.
            newName = @"D_138_REP1";
            treeNode = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().NodeAt(9);
            RunUI(() =>
            {
                var auditLogSize = SkylineWindow.Document.AuditLog.AuditLogEntries.Count;
                var cancelEvent = SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, newName);

                Assert.IsTrue(cancelEvent);
                // Check no new entries were added to the audit log
                Assert.AreEqual(auditLogSize, SkylineWindow.Document.AuditLog.AuditLogEntries.Count);
            });
            WaitForFilesTree();
            Assert.AreEqual(newName, SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[9].Name);

            // Input validation - attempt to rename a node to a name used by another node in the collection
            treeNode = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().NodeAt(12);
            var currentName = treeNode.Name;
            var cancelEvent = false;

            var auditLogSize = SkylineWindow.Document.AuditLog.AuditLogEntries.Count;
            var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() => cancelEvent = SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, newName));
            OkDialog(confirmDlg, confirmDlg.ClickOk);

            Assert.IsTrue(cancelEvent);
            Assert.AreEqual(auditLogSize, SkylineWindow.Document.AuditLog.AuditLogEntries.Count);

            WaitForFilesTree();
            Assert.AreEqual(currentName, SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[12].Name);

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

        /// <summary>
        /// Copies the supporting files for Rat_plasma.sky into the test directory from the tutorial data directory.
        /// </summary>
        public static void PrepareRatPlasmaFile(TestFilesDir ratPlasmaDestDir, TestFilesDir ratPlasmaSourceDir)
        {
            var ratPlasmaDestFolder = ratPlasmaDestDir.FullPath;
            var ratPlasmaSourceFolder = ratPlasmaSourceDir.GetTestPath(@"GroupedStudies\Heart Failure\raw");

            foreach (var file in Directory.EnumerateFiles(ratPlasmaSourceFolder))
            {
                string destFile = Path.Combine(ratPlasmaDestFolder, Path.GetFileName(file));
                if (!File.Exists(destFile))
                    File.Copy(file, destFile);
            }
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

            // Rename spectral library
            {
                librariesFolder = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>();
                var treeNode = librariesFolder.NodeAt(0);
                var originalName = treeNode.Name;
                var newName = "Renamed Library";

                RunUI(() =>
                {
                    var auditLogSize = SkylineWindow.Document.AuditLog.AuditLogEntries.Count;
                    var cancelEvent = SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, newName);
                    Assert.IsFalse(cancelEvent);
                    Assert.AreEqual(auditLogSize + 1, SkylineWindow.Document.AuditLog.AuditLogEntries.Count);
                });
                WaitForFilesTree();

                Assert.AreEqual(newName, SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>().Nodes[0].Name);
                Assert.AreEqual(newName, SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name);

                // Input validation - attempt to rename to the same name
                RunUI(() =>
                {
                    var auditLogSize = SkylineWindow.Document.AuditLog.AuditLogEntries.Count;
                    var cancelEvent = SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, newName);
                    Assert.IsTrue(cancelEvent);
                    Assert.AreEqual(auditLogSize, SkylineWindow.Document.AuditLog.AuditLogEntries.Count);
                });
                WaitForFilesTree();
                Assert.AreEqual(newName, SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>().Nodes[0].Name);

                // Input validation - attempt to rename to a name used by another library
                var secondLibraryName = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>().Nodes[1].Name;
                var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() =>
                {
                    var cancelEvent = SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, secondLibraryName);
                    Assert.IsTrue(cancelEvent);
                });
                OkDialog(confirmDlg, confirmDlg.ClickOk);
                WaitForFilesTree();
                Assert.AreEqual(newName, SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>().Nodes[0].Name);

                // Undo rename
                var doc = SkylineWindow.Document;
                RunUI(() => SkylineWindow.Undo());
                WaitForDocumentChange(doc);
                Assert.AreEqual(originalName, SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>().Nodes[0].Name);
                Assert.AreEqual(originalName, SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name);
            }
        }

        // Assumes rat-plasma.sky is loaded
        private void TestRestoreViewState()
        {
            RunUI(() =>
            {
                // Check expansion
                Assert.IsTrue(SkylineWindow.FilesTree.Root.IsExpanded);

                var replicatesFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
                Assert.IsTrue(replicatesFolder.IsExpanded);
                Assert.IsTrue(replicatesFolder.Nodes[0].IsExpanded);
                Assert.IsTrue(replicatesFolder.Nodes[10].IsExpanded);

                var spectralLibrariesFolder = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>();
                Assert.IsTrue(spectralLibrariesFolder.IsExpanded);

                // These checks fail due to the ChromatogramCache which was present when the indices for TopNode / SelectedNode
                // were written but is not present when view state is restored, which results in  off-by-one errors when setting
                // top and selected nodes. For now, adjust indices from the .view file to work in tree without a Chromatogram
                // Cache.

                // Check top node
                const int expectedTopNodeIndex = 2;
                var node = GetNthNode(SkylineWindow.FilesTree, AdjustForChromatogramCacheIssue(expectedTopNodeIndex));
                Assert.AreEqual(SkylineWindow.FilesTree.TopNode, node);
                Assert.IsTrue(ReferenceEquals(SkylineWindow.FilesTree.Folder<ReplicatesFolder>(), node));

                // Check selection
                const int expectedSelectedNodeIndex = 11;
                node = GetNthNode(SkylineWindow.FilesTree, AdjustForChromatogramCacheIssue(expectedSelectedNodeIndex));
                Assert.AreEqual(SkylineWindow.FilesTree.SelectedNode, node);
                Assert.AreEqual(@"D_108_REP2", node.Name);
            });
            return;

            int AdjustForChromatogramCacheIssue(int expectedIndex)
            {
                return expectedIndex + 1;
            }
        }

        // Assumes rat-plasma.sky is loaded
        protected void TestMonitoringFileSystem()
        {
            WaitForFilesTree();

            { // Smoke test finding nodes matching a directory name
                var documentFilePath = SkylineWindow.DocumentFilePath;
                var directoryName = Path.GetDirectoryName(documentFilePath);
                var matchingNodes = SkylineWindow.FilesTree.FindNodesByFilePath(directoryName);
                Assert.AreEqual(47, matchingNodes.Count);
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

            Assert.AreEqual(1, SkylineWindow.FilesTree.MonitoredDirectories().Count);
            Assert.IsTrue(SkylineWindow.FilesTree.IsMonitoringDirectory(SkylineWindow.DocumentFilePath));
        }

        protected void TestDragAndDrop()
        {
            {
                // Start with a clean document
                var documentPath = TestFilesDirs[0].GetTestPath(RAT_PLASMA_FILE_NAME);
                RunUI(() =>
                {
                    SkylineWindow.OpenFile(documentPath);
                    SkylineWindow.ShowFilesTreeForm(true);
                });
                WaitForDocumentLoaded();
                WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible && SkylineWindow.FilesTree.IsComplete());

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
            var documentPath = TestFilesDirs[0].GetTestPath(RAT_PLASMA_FILE_NAME);
            RunUI(() =>
            {
                SkylineWindow.OpenFile(documentPath);
                SkylineWindow.ShowFilesTreeForm(true);
            });
            WaitForDocumentLoaded();
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible && SkylineWindow.FilesTree.IsComplete());

            // A, B, C, D => Drag B, C to Parent Node => A, D, B, C
            var dndParams = DragAndDrop<ReplicatesFolder>(new[] { 1, 2, 3 }, -1, DragAndDropDirection.down, MoveType.move_last);
            AssertDragAndDropResults<ReplicatesFolder>(dndParams);

            dndParams = DragAndDrop<SpectralLibrariesFolder>(new[] { 0 }, -1, DragAndDropDirection.down, MoveType.move_last);
            AssertDragAndDropResults<SpectralLibrariesFolder>(dndParams);
        }

        protected void TestReplicateLabelEdit()
        {
            // Start with a clean document
            var documentPath = TestFilesDirs[0].GetTestPath(RAT_PLASMA_FILE_NAME);
            RunUI(() =>
            {
                SkylineWindow.OpenFile(documentPath);
                SkylineWindow.ShowFilesTreeForm(true);
            });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible && SkylineWindow.FilesTree.IsComplete());

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
            where T : FileModel
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
        private static void AssertDragAndDropResults<TFolder>(DragAndDropParams d) where TFolder : FileModel
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
            where T : FileModel
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

        /// <summary>
        /// Get the Nth node from a tree. <see cref="absoluteNodeIndex"/> is a zero-based index
        /// that only counts visible nodes starting from the tree's root.
        /// </summary>
        /// <param name="filesTree">Tree to traverse</param>
        /// <param name="absoluteNodeIndex">TreeNode to find. Returns null if the Nth node could not be found.</param>
        /// <returns></returns>
        private static TreeNode GetNthNode(FilesTree filesTree, int absoluteNodeIndex)
        {
            if (filesTree == null || absoluteNodeIndex < 0)
            {
                return null;
            }

            var currentNode = filesTree.Nodes[0];
            var count = 0;

            while (currentNode != null)
            {
                if (count == absoluteNodeIndex)
                {
                    return currentNode;
                }
                currentNode = currentNode.NextVisibleNode;
                count++;
            }

            return null; // Nth visible node not found
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
