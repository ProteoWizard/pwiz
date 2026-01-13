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

using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Controls.FilesTree;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Files;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using static pwiz.Skyline.Model.Files.FileModel;
using BackgroundProteome = pwiz.Skyline.Model.Files.BackgroundProteome;
using IonMobilityLibrary = pwiz.Skyline.Model.Files.IonMobilityLibrary;
using OptimizationLibrary = pwiz.Skyline.Model.Files.OptimizationLibrary;

// CONSIDER: verify right-click menu includes open containing folder only for files found locally
// CONSIDER: expand drag-and-drop tests - scenarios: disjoint selection, tree disallows dragging un-draggable nodes
// CONSIDER: handling of non-local paths from SrmSettings (ex: replicate sample file paths cannot be found locally)
// CONSIDER: new test making sure clicking 'x' upper RHC of confirm dialog does not delete Replicate / Spectral Library

// ReSharper disable WrongIndentSize
namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FilesTreeFormTest : AbstractFunctionalTestEx
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

            using (new AuditLogList.IgnoreTestChecksScope())    // Keep !IgnoreTestChecks from causing confusion in audit log tests
                RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            return TestFilesDirs[0].GetTestPath(relativePath);
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

            var monitoredPath = GetTestPath(Path.Combine(@"TestSave", fileName));
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
            var monitoredPath = GetTestPath(Path.Combine(@"TestSaveAs", origFileName));
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
                monitoredPath = GetTestPath(Path.Combine(@"TestSaveAs", saveAsFileName));
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
            var documentPath = GetTestPath(Path.Combine(@"TestUpgradeExistingDocumentWithFilesTree", @"UpgradeWithFilesTree.sky"));

            OpenDocument(documentPath);

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
            });

            OpenDocument(documentPath);

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
            var documentPath = GetTestPath(RAT_PLASMA_FILE_NAME);
            OpenDocument(documentPath);
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

            AssertTopLevelFiles(typeof(SkylineViewFile), typeof(SpectralLibrariesFolder), typeof(SkylineChromatogramCache), typeof(ReplicatesFolder));

            Assert.AreEqual(RAT_PLASMA_FILE_NAME, SkylineWindow.FilesTree.Root.Text);
            AssertFileState(FileState.available, SkylineWindow.FilesTree.Root);

            ValidateViewStateRestored();

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);

            // Enable audit logging and test double-click opens the audit log form
            AddAuditLog();

            AssertTopLevelFiles(typeof(SkylineAuditLog), typeof(SkylineViewFile), typeof(SpectralLibrariesFolder), typeof(SkylineChromatogramCache), typeof(ReplicatesFolder));

            //
            // Edit SrmDocument directly and make sure FilesTree updates correctly
            //

            // Rename replicate - expand node first to make sure expanded state is preserved after update
            RunUI(() => SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Nodes[3].Expand());
            WaitForConditionUI(() => SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Nodes[3].IsExpanded);

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
            Assert.AreEqual(newName, SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Nodes[3].Name);

            // Expanded folder should remain expanded after a document update
            RunUI(() => Assert.IsTrue(SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Nodes[3].IsExpanded));

            //
            // Edit tree node labels
            //
            newName = "NEW REPLICATE NAME";
            var treeNode = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().NodeAt(0);
            RunUI(() =>
            {
                var auditLogSize = SkylineWindow.Document.AuditLog.AuditLogEntries.Count;
                var cancelEvent = SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, newName);
                Assert.IsFalse(cancelEvent);
                // Check one entry was added to the audit log
                Assert.AreEqual(auditLogSize + 1, SkylineWindow.Document.AuditLog.AuditLogEntries.Count);
            });
            WaitForFilesTree();

            Assert.AreEqual(newName, SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Nodes[0].Name);

            // Input validation - attempt to rename node to the same name.
            newName = @"D_138_REP1";
            treeNode = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().NodeAt(9);
            RunUI(() =>
            {
                var auditLogSize = SkylineWindow.Document.AuditLog.AuditLogEntries.Count;
                var cancelEvent = SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, newName);

                Assert.IsTrue(cancelEvent);
                // Check no new entries were added to the audit log
                Assert.AreEqual(auditLogSize, SkylineWindow.Document.AuditLog.AuditLogEntries.Count);
            });
            WaitForFilesTree();
            Assert.AreEqual(newName, SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Nodes[9].Name);

            // Input validation - attempt to rename a node to a name used by another node in the collection
            treeNode = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().NodeAt(12);
            var currentName = treeNode.Name;
            var cancelEvent = false;

            var auditLogSize = SkylineWindow.Document.AuditLog.AuditLogEntries.Count;
            var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() => cancelEvent = SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, newName));
            OkDialog(confirmDlg, confirmDlg.ClickOk);

            Assert.IsTrue(cancelEvent);
            Assert.AreEqual(auditLogSize, SkylineWindow.Document.AuditLog.AuditLogEntries.Count);

            WaitForFilesTree();
            Assert.AreEqual(currentName, SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Nodes[12].Name);

            //
            // Activating a replicate should update selected graphs
            //
            const int selectedIndex = 4;
            var filesTreeNode = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().NodeAt(selectedIndex);
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

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);

            TestShowAndHideRestoresState();

            TestRemoveAllReplicates();

            TestRemoveSelectedReplicates();

            TestSpectralLibraries();

            TestMonitoringFileSystem();
            
            TestDragAndDrop();

            TestDragAndDropOnParentNode();

            TestDragSimulation();

            TestReplicateLabelEdit();

            TestOtherFileTypes();

            TestToolTips();

            TestRightClickMenus();

            TestShowFileNames();
        }

        /// <summary>
        /// Test adding and renaming other file types: Background Proteome, iRT Calculator, Ion Mobility Library, Optimization Library
        /// </summary>
        protected void TestOtherFileTypes()
        {
            // Add Background Proteome (.protdb), verify it appears, and test rename
            AddBackgroundProteome("Rat_mini" + ProteomeDb.EXT_PROTDB);

            // Add iRT Calculator (Rat_Prosit.blib - a .blib file with embedded iRT table), verify it appears, and test rename
            const string prositBlibName = "Rat_Prosit" + BiblioSpecLiteSpec.EXT;
            AddIrtCalculator(prositBlibName);

            // Add spectral library with same .blib file, then edit iRT calc to change standard (triggers save-as)
            AddAndEditPrositLibrary(prositBlibName);

            // Add Ion Mobility Library (.imsdb), verify it appears, and test rename
            AddIonMobilityLibrary("Rat_ims" + IonMobilityLibrarySpec.EXT);

            // Add Optimization Library (.optdb), verify it appears, and test rename
            AddOptimizationLibrary("Rat_settings" + OptimizationDb.EXT);
        }

        /// <summary>
        /// Enable audit logging via the AuditLogForm and verify double-click opens the audit log form.
        /// </summary>
        private void AddAuditLog()
        {
            // Enable audit logging
            {
                var auditLogForm = ShowDialog<AuditLogForm>(SkylineWindow.ShowAuditLog);

                using (new WaitDocumentChange())
                    RunUI(() => auditLogForm.EnableAuditLogging(true));
                WaitForFilesTree();

                Assert.IsTrue(SkylineWindow.Document.Settings.DataSettings.AuditLogging);

                OkDialog(auditLogForm, auditLogForm.Close);
            }

            // Verify audit log node appears in FilesTree
            {
                var auditLogNode = SkylineWindow.FilesTree.RootChild<SkylineAuditLog>();
                Assert.IsNotNull(auditLogNode, "SkylineAuditLog node should appear in FilesTree when audit logging is enabled");

                // Test double-click opens the audit log form
                var auditLogForm = ShowDialog<AuditLogForm>(() => SkylineWindow.FilesTreeForm.DoubleClickNode(auditLogNode));
                OkDialog(auditLogForm, auditLogForm.Close);
            }
        }

        /// <summary>
        /// Add a background proteome from an existing .protdb file, verify it appears in FilesTree, and test renaming
        /// </summary>
        private void AddBackgroundProteome(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            string filePath = GetTestPath(fileName);
            using (new WaitDocumentChange())
            {
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunDlg<BuildBackgroundProteomeDlg>(peptideSettingsUI.AddBackgroundProteome,
                    dlg =>
                    {
                        dlg.BackgroundProteomeName = name;
                        dlg.OpenBackgroundProteome(filePath);
                        dlg.OkDialog();
                    });
                OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            }

            // Verify background proteome was added with correct name and path
            var node = ValidateSingleEntry<BackgroundProteome, BackgroundProteomeSpec>(
                name, Settings.Default.BackgroundProteomeList, filePath);

            // Test double-click rename
            string rename = name + "_renamed";
            using (new WaitDocumentChange())
            {
                RunDlg<BuildBackgroundProteomeDlg>(() => SkylineWindow.FilesTreeForm.DoubleClickNode(node),
                    dlg =>
                    {
                        dlg.BackgroundProteomeName = rename;
                        dlg.OkDialog();
                    });
            }

            // Verify background proteome was renamed (path stays the same)
            ValidateSingleEntry<BackgroundProteome, BackgroundProteomeSpec>(
                rename, Settings.Default.BackgroundProteomeList, filePath);
        }

        /// <summary>
        /// Add an iRT calculator from an existing .blib file with embedded iRT table, verify it appears in FilesTree, and test renaming
        /// </summary>
        private void AddIrtCalculator(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            string filePath = GetTestPath(fileName);
            using (new WaitDocumentChange())
            {
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunDlg<EditIrtCalcDlg>(peptideSettingsUI.AddCalculator, dlg =>
                {
                    dlg.CalcName = name;
                    dlg.OpenDatabase(filePath);
                    dlg.OkDialog();
                });
                var createRegressionDlg = ShowDialog<AddIrtStandardsToDocumentDlg>(peptideSettingsUI.OkDialog);
                OkDialog(createRegressionDlg, createRegressionDlg.BtnNoClick);
            }

            // Verify iRT calculator was added with correct name and path
            var node = ValidateSingleEntry<RTCalc, RetentionScoreCalculatorSpec>(
                name, Settings.Default.RTScoreCalculatorList, filePath);

            // Test Edit rename
            string rename = name + "_renamed";
            using (new WaitDocumentChange())
            {
                RunDlg<EditIrtCalcDlg>(() => SkylineWindow.FilesTreeForm.EditNode(node),
                    dlg =>
                    {
                        dlg.CalcName = rename;
                        dlg.OkDialog();
                    });
            }

            // Verify iRT calculator was renamed (path stays the same)
            ValidateSingleEntry<RTCalc, RetentionScoreCalculatorSpec>(
                rename, Settings.Default.RTScoreCalculatorList, filePath);
        }

        /// <summary>
        /// Add spectral library using same .blib file as iRT calculator, then edit iRT calc to change standard.
        /// Since .blib files cannot be modified, changing the standard triggers a "save as" flow to create a new .irtdb file.
        /// </summary>
        private void AddAndEditPrositLibrary(string blibFileName)
        {
            string name = Path.GetFileNameWithoutExtension(blibFileName);
            string blibPath = GetTestPath(blibFileName);
            string irtDbPath = Path.ChangeExtension(blibPath, IrtDb.EXT);

            // Add spectral library using the same .blib file as the iRT calculator
            using (new WaitDocumentChange())
            {
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                var libListDlg = ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
                RunUI(libListDlg.SelectLastItem);
                RunDlg<EditLibraryDlg>(libListDlg.AddItem, dlg =>
                {
                    dlg.LibraryName = name;
                    dlg.LibraryPath = blibPath;
                    dlg.OkDialog();
                });
                OkDialog(libListDlg, libListDlg.OkDialog);
                RunUI(() => peptideSettingsUI.PickedLibraries = peptideSettingsUI.PickedLibraries.Append(name).ToArray());

                // TODO(nicksh): If GraphSpectrum updates while the OK button is being pressed it might leak a PooledSqliteConnection
                WaitForConditionUI(() => !SkylineWindow.GraphSpectrum.IsGraphUpdatePending);

                OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            }

            // Verify spectral library was added
            WaitForFilesTree();
            RunUI(() =>
            {
                var libraryFolder = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>();
                Assert.AreEqual(3, libraryFolder?.Nodes.Count ?? 0);
                var libNode = (FilesTreeNode) libraryFolder?.Nodes[2];
                Assert.AreEqual(blibPath, libNode?.FilePath);
            });

            // Edit iRT calculator to change standard - this triggers "save as" since .blib cannot be modified
            var irtNode = SkylineWindow.FilesTree.RootChild<RTCalc>();
            using (new WaitDocumentChange())
            {
                var editDlg = ShowDialog<EditIrtCalcDlg>(() => SkylineWindow.FilesTreeForm.EditNode(irtNode));
                RunUI(() => editDlg.IrtStandards = IrtStandard.BIOGNOSYS_10);
                // Dismiss the message about needing to save as new file, and provide the save path
                RunDlg<MessageDlg>(() => editDlg.OkDialog(irtDbPath), msgDlg => msgDlg.OkDialog());
            }
            // Verify iRT calculator path changed to .irtdb file
            ValidateSingleEntry<RTCalc, RetentionScoreCalculatorSpec>(
                irtNode.Name, Settings.Default.RTScoreCalculatorList, irtDbPath);
        }

        /// <summary>
        /// Add an ion mobility library from an existing .imsdb file, verify it appears in FilesTree, and test renaming
        /// </summary>
        private void AddIonMobilityLibrary(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            string filePath = GetTestPath(fileName);
            using (new WaitDocumentChange())
            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunDlg<EditIonMobilityLibraryDlg>(transitionSettingsUI.IonMobilityControl.AddIonMobilityLibrary,
                    dlg =>
                    {
                        dlg.LibraryName = name;
                        dlg.OpenDatabase(filePath);
                        dlg.OkDialog();
                    });
                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            }

            // Verify ion mobility library was added with correct name and path
            var node = ValidateSingleEntry<IonMobilityLibrary, Skyline.Model.IonMobility.IonMobilityLibrary>(
                name, Settings.Default.IonMobilityLibraryList, filePath);

            // Test double-click rename
            string rename = name + "_renamed";
            using (new WaitDocumentChange())
            {
                RunDlg<EditIonMobilityLibraryDlg>(() => SkylineWindow.FilesTreeForm.DoubleClickNode(node),
                    dlg =>
                    {
                        dlg.LibraryName = rename;
                        dlg.OkDialog();
                    });
            }

            // Verify ion mobility library was renamed (path stays the same)
            ValidateSingleEntry<IonMobilityLibrary, Skyline.Model.IonMobility.IonMobilityLibrary>(
                rename, Settings.Default.IonMobilityLibraryList, filePath);
        }

        /// <summary>
        /// Add an optimization library from an existing .optdb file, verify it appears in FilesTree, and test renaming
        /// </summary>
        private void AddOptimizationLibrary(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            string filePath = GetTestPath(fileName);
            using (new WaitDocumentChange())
            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunDlg<EditOptimizationLibraryDlg>(transitionSettingsUI.AddToOptimizationLibraryList,
                    dlg =>
                    {
                        dlg.LibName = name;
                        dlg.OpenDatabase(filePath);
                        dlg.OkDialog();
                    });
                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            }

            // Verify optimization library was added with correct name and path
            var node = ValidateSingleEntry<OptimizationLibrary, Skyline.Model.Optimization.OptimizationLibrary>(
                name, Settings.Default.OptimizationLibraryList, filePath);

            // Test Edit rename
            string rename = name + "_renamed";
            using (new WaitDocumentChange())
            {
                RunDlg<EditOptimizationLibraryDlg>(() => SkylineWindow.FilesTreeForm.EditNode(node),
                    dlg =>
                    {
                        dlg.LibName = rename;
                        dlg.OkDialog();
                    });
            }

            // Verify optimization library was renamed (path stays the same)
            ValidateSingleEntry<OptimizationLibrary, Skyline.Model.Optimization.OptimizationLibrary>(
                rename, Settings.Default.OptimizationLibraryList, filePath);
        }

        /// <summary>
        /// Validate that a settings list and corresponding FilesTree file both contain exactly one entry with the expected name and path.
        /// And return the single FilesTreeNode for the file.
        /// </summary>
        /// <param name="expectedName">Expected resource name of the file</param>
        /// <param name="settingsList">Settings list to check for the entry</param>
        /// <param name="expectedFilePath">Optional expected file path; if provided, validates the node's FilePath matches</param>
        private static FilesTreeNode ValidateSingleEntry<TFileModel, TSettings>(string expectedName, SettingsList<TSettings> settingsList, string expectedFilePath = null)
            where TFileModel : FileModel
            where TSettings : IKeyContainer<string>, IXmlSerializable
        {
            WaitForFilesTree();
            FilesTreeNode fileNode = null;
            RunUI(() =>
            {
                // Validate settings list
                var defaults = settingsList.GetDefaults();
                int countDefaults = defaults.Count();
                Assert.AreEqual(1, settingsList.Count - countDefaults,
                    $"Settings list should contain exactly one {typeof(TSettings).Name}");
                Assert.AreEqual(expectedName, settingsList[countDefaults].GetKey(), $"Settings list entry should be named '{expectedName}'");

                // Validate FilesTree - find the file directly in the tree
                fileNode = FindFileInTree<TFileModel>(SkylineWindow.FilesTree.Root, expectedName);
                Assert.IsNotNull(fileNode, $"{typeof(TFileModel).Name} file should exist in FilesTree");
                Assert.AreEqual(expectedName, fileNode.Name, $"FilesTree node should be named '{expectedName}'");

                // Validate file path if expected path is provided
                if (expectedFilePath != null)
                {
                    Assert.AreEqual(expectedFilePath, fileNode.Model.FilePath,
                        $"{typeof(TFileModel).Name} file path mismatch");
                }
            });
            return fileNode;
        }

        /// <summary>
        /// Recursively search the tree for a file node of the specified type and name
        /// </summary>
        private static FilesTreeNode FindFileInTree<TFileModel>(FilesTreeNode node, string name) where TFileModel : FileModel
        {
            if (node.Model is TFileModel && node.Name == name)
                return node;

            foreach (FilesTreeNode child in node.Nodes)
            {
                var result = FindFileInTree<TFileModel>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// Test tooltips in FilesTree for various node types
        /// </summary>
        protected void TestToolTips()
        {
            // Test tooltip for replicate node
            var replicatesFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
            var replicateNode = (FilesTreeNode)replicatesFolder.Nodes[0];
            ShowFilesTreeNodeTip(replicateNode);

            // Test tooltip for replicate sample file node
            RunUI(() => replicateNode.Expand());    // Closed during undo
            var sampleFileNode = (FilesTreeNode)replicateNode.Nodes[0];
            ShowFilesTreeNodeTip(sampleFileNode);

            // Test tooltip for spectral library node
            var librariesFolder = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>();
            var libraryNode = (FilesTreeNode)librariesFolder.Nodes[0];
            ShowFilesTreeNodeTip(libraryNode, true);

            // Test tooltip for the root .sky file node
            ShowFilesTreeNodeTip(SkylineWindow.FilesTree.Root, true);

            // Clear tooltip at the end
            ShowFilesTreeNodeTip(null);
        }

        /// <summary>
        /// Helper method to test tooltips for FilesTree nodes, adapted from MethodEditTutorialTest.ShowNodeTip
        /// Uses the existing FilesTreeForm tooltip infrastructure
        /// </summary>
        private void ShowFilesTreeNodeTip(FilesTreeNode node, bool debugTip = false)
        {
            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.MoveMouseOnTree(new Point(-1, -1));
                Assert.IsFalse(SkylineWindow.FilesTreeForm.IsTipVisible);
            });

            if (node == null)
                return;

            SkylineWindow.FilesTreeForm.IgnoreFocus = true;
            FilesTreeNode.ShowDebugTipText = debugTip;
            RunUI(() =>
            {
                Point GetNodeCenter(FilesTreeNode n)
                {
                    var rect = n.Bounds;
                    return new Point((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
                }

                var pt = GetNodeCenter(node);

                // Check if the point is within the FilesTree client rectangle
                // If not, set TopNode to ensure the node is visible and recalculate the point
                if (!SkylineWindow.FilesTree.ClientRectangle.Contains(pt))
                {
                    SkylineWindow.FilesTree.TopNode = node;
                    pt = GetNodeCenter(node);
                }

                SkylineWindow.FilesTreeForm.MoveMouseOnTree(pt);
            });

            WaitForConditionUI(NodeTip.TipDelayMs * 10, () => SkylineWindow.FilesTreeForm.IsTipVisible);
            RunUI(() =>
            {
                string tipText = SkylineWindow.FilesTreeForm.NodeTipText;
                AssertEx.Contains(tipText, node.Text, node.FilePath);
                if (debugTip)
                    AssertEx.Contains(tipText, "Debug Info");
            });
            FilesTreeNode.ShowDebugTipText = false;
            SkylineWindow.FilesTreeForm.IgnoreFocus = false;
        }

        /// <summary>
        /// Test right-click context menus for various node types in FilesTree
        /// </summary>
        protected void TestRightClickMenus()
        {
            var tree = SkylineWindow.FilesTree;
            var menu = SkylineWindow.FilesTreeForm;
            var minimumFileMenu = new[] { menu.OpenContainingFolderMenuItem };
            var singleObjectMenu = new[] { menu.OpenContainingFolderMenuItem, menu.EditMenuItem };

            // First test nodes with non-empty menus
            var replicatesFolder = tree.RootChild<ReplicatesFolder>();
            ShowFilesTreeNodeMenu(replicatesFolder, new[]
                { menu.ManageResultsMenuItem, menu.RemoveAllMenuItem });

            var replicateNode = (FilesTreeNode)replicatesFolder.Nodes[0];
            ShowFilesTreeNodeMenu(replicateNode, new[]
                { menu.SelectReplicateMenuItem, menu.RemoveMenuItem });

            var librariesFolder = tree.RootChild<SpectralLibrariesFolder>();
            ShowFilesTreeNodeMenu(librariesFolder, new[]
                { menu.LibraryExplorerMenuItem, menu.RemoveAllMenuItem });

            var libraryNode = (FilesTreeNode)librariesFolder.Nodes[0];
            ShowFilesTreeNodeMenu(libraryNode, new[]
                { menu.OpenContainingFolderMenuItem, menu.OpenLibraryInLibraryExplorerMenuItem, menu.EditMenuItem, menu.RemoveMenuItem });

            // Test types added earlier in TestOtherFileTypes
            ShowFilesTreeNodeMenu(tree.RootChild<BackgroundProteome>(), singleObjectMenu);
            ShowFilesTreeNodeMenu(tree.RootChild<RTCalc>(), singleObjectMenu);
            ShowFilesTreeNodeMenu(tree.RootChild<OptimizationLibrary>(), singleObjectMenu);
            ShowFilesTreeNodeMenu(tree.RootChild<IonMobilityLibrary>(), singleObjectMenu);

            // Finally test the root .sky file node and its companion files
            ShowFilesTreeNodeMenu(tree.Root, minimumFileMenu);
            // Audit log was enabled in AddAuditLog() so it should be in the FilesTree
            ShowFilesTreeNodeMenu(tree.RootChild<SkylineAuditLog>(), new[]
                { menu.OpenContainingFolderMenuItem, menu.OpenAuditLogMenuItem });
            ShowFilesTreeNodeMenu(tree.RootChild<SkylineViewFile>(), minimumFileMenu);
        }

        /// <summary>
        /// Helper method to test context menus for FilesTree nodes.
        /// Shows the context menu for a node and validates the visible menu items.
        /// </summary>
        private void ShowFilesTreeNodeMenu(FilesTreeNode node, IList<ToolStripMenuItem> visibleItems)
        {
            Assert.IsNotNull(node);
            var filesTreeForm = SkylineWindow.FilesTreeForm;
            visibleItems = visibleItems.Concat(new[] { filesTreeForm.ShowFileNamesMenuItem }).ToList();
            using var scope = new ScopedAction(
                initAction: () => RunUI(() => filesTreeForm.TreeContextMenu.Closing += DenyMenuClosing),
                disposeAction: () =>
                {
                    RunUI(() => filesTreeForm.TreeContextMenu.Closing -= DenyMenuClosing);
                    if (visibleItems.Count != 0)
                        RunUI(filesTreeForm.TreeContextMenu.Close);
                });

            RunUI(() => filesTreeForm.ShowContextMenuForNode(node));

            // Wait for the context menu Opening event to complete
            WaitForConditionUI(() => filesTreeForm.ContextMenuShown.HasValue);

            RunUI(() =>
            {
                var menu = filesTreeForm.TreeContextMenu;
                var actualVisibleItems = menu.Items.OfType<ToolStripMenuItem>()
                    .Where(item => item.Visible && !filesTreeForm.IsDebugMenu(item)).ToArray();
                Assert.IsTrue(actualVisibleItems.SequenceEqual(visibleItems),
                    string.Format("Unexpected menu items: Expected<{0}>, Actual<{1}>",
                        TextUtil.LineSeparate(visibleItems.Select(item => item.Text)),
                        TextUtil.LineSeparate(actualVisibleItems.Select(item => item.Text))));
                Assert.AreEqual(!visibleItems.IsNullOrEmpty(), filesTreeForm.ContextMenuShown.Value);
            });
        }

        /// <summary>
        /// Test the "Show File Names" toggle feature in the context menu.
        /// Verifies that toggling the setting correctly changes the display text of nodes
        /// from resource names (e.g., "Rat mini") to file names (e.g., "Rat_mini.protdb").
        /// </summary>
        protected void TestShowFileNames()
        {
            var tree = SkylineWindow.FilesTree;
            var filesTreeForm = SkylineWindow.FilesTreeForm;

            // Start with ShowFileNames = false (resource names displayed)
            RunUI(() => filesTreeForm.DoShowFileNames(false));
            WaitForFilesTree();

            // Verify display text for types WITH FileTypeText prefix (format: "Type - Name")
            AssertNodeDisplayText<BackgroundProteome>(tree, BackgroundProteome.TypeText, showFileName: false);
            AssertNodeDisplayText<RTCalc>(tree, RTCalc.TypeText, showFileName: false);
            AssertNodeDisplayText<IonMobilityLibrary>(tree, IonMobilityLibrary.TypeText, showFileName: false);
            AssertNodeDisplayText<OptimizationLibrary>(tree, OptimizationLibrary.TypeText, showFileName: false);

            // Verify display text for types WITHOUT FileTypeText prefix (just the name)
            var replicatesFolder = tree.RootChild<ReplicatesFolder>();
            var replicateNode = (FilesTreeNode)replicatesFolder?.Nodes[0];
            Assert.IsNotNull(replicateNode);
            RunUI(() =>
            {
                // Replicate has no type prefix, so display text should just be the name
                var expectedText = GetDisplayText(string.Empty, replicateNode.Model.Name, replicateNode.Model.FilePath, showFileName: false);
                Assert.AreEqual(expectedText, replicateNode.Text,
                    $"Replicate node text should be resource name when ShowFileNames=false");
            });

            // Toggle to ShowFileNames = true (file names displayed)
            RunUI(() => filesTreeForm.DoShowFileNames(true));
            WaitForFilesTree();

            // Verify display text changes to file names for types WITH FileTypeText prefix
            AssertNodeDisplayText<BackgroundProteome>(tree, BackgroundProteome.TypeText, showFileName: true);
            AssertNodeDisplayText<RTCalc>(tree, RTCalc.TypeText, showFileName: true);
            AssertNodeDisplayText<IonMobilityLibrary>(tree, IonMobilityLibrary.TypeText, showFileName: true);
            AssertNodeDisplayText<OptimizationLibrary>(tree, OptimizationLibrary.TypeText, showFileName: true);

            // Verify replicate node also changes
            RunUI(() =>
            {
                var expectedText = GetDisplayText(string.Empty, replicateNode.Model.Name, replicateNode.Model.FilePath, showFileName: true);
                Assert.AreEqual(expectedText, replicateNode.Text,
                    $"Replicate node text should be file name when ShowFileNames=true");
            });

            // Test special cases: nodes with empty Name (e.g., AuditLog, ViewFile, ChromatogramCache)
            // These should show just the TypeText when ShowFileNames=false, and TypeText - FileName when ShowFileNames=true
            AssertNodeDisplayText<SkylineAuditLog>(tree, SkylineAuditLog.TypeText, showFileName: true);
            AssertNodeDisplayText<SkylineViewFile>(tree, SkylineViewFile.TypeText, showFileName: true);
            AssertNodeDisplayText<SkylineChromatogramCache>(tree, SkylineChromatogramCache.TypeText, showFileName: true);

            // Toggle back to ShowFileNames = false and verify
            RunUI(() => filesTreeForm.DoShowFileNames(false));
            WaitForFilesTree();

            AssertNodeDisplayText<SkylineAuditLog>(tree, SkylineAuditLog.TypeText, showFileName: false);
            AssertNodeDisplayText<SkylineViewFile>(tree, SkylineViewFile.TypeText, showFileName: false);
            AssertNodeDisplayText<SkylineChromatogramCache>(tree, SkylineChromatogramCache.TypeText, showFileName: false);
        }

        /// <summary>
        /// Helper to assert that a node's display text matches the expected format based on ShowFileNames setting.
        /// </summary>
        private void AssertNodeDisplayText<TFileModel>(FilesTree tree, string typeText, bool showFileName) where TFileModel : FileModel
        {
            var node = tree.RootChild<TFileModel>();
            Assert.IsNotNull(node, $"{typeof(TFileModel).Name} node should exist in tree");

            RunUI(() =>
            {
                var model = node.Model;
                var expectedText = GetDisplayText(typeText, model.Name, model.FilePath, showFileName);
                Assert.AreEqual(expectedText, node.Text,
                    $"{typeof(TFileModel).Name} node text mismatch. ShowFileNames={showFileName}");
            });
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
                var replicatesFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
                SkylineWindow.FilesTreeForm.RemoveAll(replicatesFolder);
            });
            OkDialog(confirmDlg, confirmDlg.ClickYes);

            doc = WaitForDocumentChangeAndFilesTree(doc);
            Assert.IsNull(SkylineWindow.FilesTree.RootChild<ReplicatesFolder>());

            RunUI(() => SkylineWindow.Undo());
            WaitForDocumentChangeAndFilesTree(doc);
            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT);
        }

        private static void TestRemoveSelectedReplicates()
        {
            var doc = SkylineWindow.Document;
            var replicatesFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
            var nodesToDelete = new List<FilesTreeNode>
            {
                replicatesFolder.NodeAt(4),
                replicatesFolder.NodeAt(5),
                replicatesFolder.NodeAt(7),
            };

            var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.FilesTreeForm.RemoveSelected(nodesToDelete));
            OkDialog(confirmDlg, confirmDlg.ClickYes);

            doc = WaitForDocumentChangeAndFilesTree(doc);
            replicatesFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();

            AssertTreeFolderMatchesDocumentAndModel<ReplicatesFolder>(RAT_PLASMA_REPLICATE_COUNT - 3);

            // Deleted nodes should be gone
            Assert.IsFalse(replicatesFolder.HasChildWithName(nodesToDelete[0].Name));
            Assert.IsFalse(replicatesFolder.HasChildWithName(nodesToDelete[1].Name));
            Assert.IsFalse(replicatesFolder.HasChildWithName(nodesToDelete[2].Name));

            RunUI(() => SkylineWindow.Undo());
            WaitForDocumentChangeAndFilesTree(doc);

            replicatesFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
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

                var replicateNodes = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
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
                var replicateNodes = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
                Assert.IsTrue(replicateNodes.Nodes[1].IsExpanded);
                Assert.IsTrue(replicateNodes.Nodes[7].IsExpanded);
                Assert.IsTrue(replicateNodes.Nodes[11].IsExpanded);
                Assert.IsTrue(replicateNodes.Nodes[12].IsExpanded);
            });
        }

        private static void TestSpectralLibraries()
        {
            const string libName0 = "Rat (NIST) (Rat_plasma2)";
            Assert.AreEqual(2, SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>()?.Nodes.Count);
            Assert.AreEqual(libName0, SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name);
            Assert.AreEqual("Rat (GPM) (Rat_plasma2)", SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[1].Name);

            var peptideLibraryTreeNode = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>()?.NodeAt(0);
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

            // Test Edit rename of a library
            {
                // Test Edit rename
                string rename = libName0 + "_renamed";
                int librariesInSettings = Settings.Default.SpectralLibraryList.Count;
                using (new WaitDocumentChange())
                {
                    RunDlg<EditLibraryDlg>(() =>
                        {
                            var node = FindFileInTree<SpectralLibrary>(SkylineWindow.FilesTree.Root, libName0);
                            SkylineWindow.FilesTreeForm.EditNode(node);
                        },
                        dlg =>
                        {
                            dlg.LibraryName = rename;
                            dlg.OkDialog();
                        });
                }

                // Verify optimization library was renamed
                WaitForFilesTree();
                RunUI(() =>
                {
                    // The number of libraries in settings should remain the same and contain the renamed library
                    Assert.AreEqual(librariesInSettings, Settings.Default.SpectralLibraryList.Count,
                        $"Settings list should contain exactly one {nameof(LibrarySpec)}");
                    Assert.IsNotNull(Settings.Default.SpectralLibraryList[rename]);

                    // Validate FilesTree - make sure the renamed library appears in its original position
                    var libraryFolder = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>();
                    Assert.AreEqual(rename, ((FilesTreeNode)libraryFolder.Nodes[0]).Name);
                });
                RunUI(() =>
                {
                    SkylineWindow.Undo();
                    // Restore Settings.Default to original state, since leaving it will cause problems for other tests
                    Settings.Default.SpectralLibraryList[0] = SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries.LibrarySpecs[0];
                });
            }

            // Remove All
            FilesTreeNode librariesFolder;
            {
                var doc = SkylineWindow.Document;
                var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() =>
                {
                    librariesFolder = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>();
                    SkylineWindow.FilesTreeForm.RemoveAll(librariesFolder);
                });
                OkDialog(confirmDlg, confirmDlg.ClickYes);

                doc = WaitForDocumentChangeAndFilesTree(doc);
                Assert.IsNull(SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>());

                RunUI(() => SkylineWindow.Undo());
                WaitForDocumentChangeAndFilesTree(doc);
                AssertTreeFolderMatchesDocumentAndModel<SpectralLibrariesFolder>(2);
            }

            // Remove selected
            {
                var doc = SkylineWindow.Document;
                librariesFolder = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>();
                var nodesToDelete = new List<FilesTreeNode>
                {
                    librariesFolder.NodeAt(1),
                };

                var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.FilesTreeForm.RemoveSelected(nodesToDelete));
                OkDialog(confirmDlg, confirmDlg.ClickYes);

                doc = WaitForDocumentChangeAndFilesTree(doc);
                Assert.IsNull(SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>());
                var libraryFile = SkylineWindow.FilesTree.RootChild<SpectralLibrary>();
                Assert.IsNotNull(libraryFile);
                Assert.AreEqual(libName0, libraryFile.Name);

                RunUI(() => SkylineWindow.Undo());
                WaitForDocumentChangeAndFilesTree(doc);

                AssertTreeFolderMatchesDocumentAndModel<SpectralLibrariesFolder>(2);

                // Removed libraries should be available again
                librariesFolder = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>();
                Assert.IsTrue(librariesFolder.HasChildWithName(nodesToDelete[0].Name));
            }

            // Rename spectral library
            {
                librariesFolder = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>();
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

                Assert.AreEqual(newName, SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>().Nodes[0].Name);
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
                Assert.AreEqual(newName, SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>().Nodes[0].Name);

                // Input validation - attempt to rename to a name used by another library
                var secondLibraryName = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>().Nodes[1].Name;
                var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() =>
                {
                    var cancelEvent = SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, secondLibraryName);
                    Assert.IsTrue(cancelEvent);
                });
                OkDialog(confirmDlg, confirmDlg.ClickOk);
                WaitForFilesTree();
                Assert.AreEqual(newName, SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>().Nodes[0].Name);

                // Undo rename
                var doc = SkylineWindow.Document;
                RunUI(() => SkylineWindow.Undo());
                WaitForDocumentChangeAndFilesTree(doc);
                Assert.AreEqual(originalName, SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>().Nodes[0].Name);
                Assert.AreEqual(originalName, SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name);
            }
        }

        // Assumes rat-plasma.sky is loaded
        private void ValidateViewStateRestored()
        {
            RunUI(() =>
            {
                // Check expansion
                Assert.IsTrue(SkylineWindow.FilesTree.Root.IsExpanded);

                var replicatesFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
                Assert.IsTrue(replicatesFolder.IsExpanded);
                Assert.IsTrue(replicatesFolder.Nodes[0].IsExpanded);
                Assert.IsTrue(replicatesFolder.Nodes[10].IsExpanded);

                var spectralLibrariesFolder = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>();
                Assert.IsTrue(spectralLibrariesFolder.IsExpanded);

                // These checks fail due to the ChromatogramCache which was present when the indices for TopNode / SelectedNode
                // were written but is not present when view state is restored, which results in  off-by-one errors when setting
                // top and selected nodes. For now, adjust indices from the .view file to work in tree without a Chromatogram
                // Cache.

                // Check top node
                const int expectedTopNodeIndex = 2;
                var node = GetNthNode(SkylineWindow.FilesTree, expectedTopNodeIndex);
                Assert.AreEqual(SkylineWindow.FilesTree.TopNode, node);
                Assert.IsTrue(ReferenceEquals(SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>(), node));

                // Check selection
                const int expectedSelectedNodeIndex = 11;
                node = GetNthNode(SkylineWindow.FilesTree, expectedSelectedNodeIndex);
                Assert.AreEqual(SkylineWindow.FilesTree.SelectedNode, node);
                Assert.AreEqual(@"D_103_REP1", node.Name);
            });
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

            var replicateFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
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

        private void RestoreAuditLogDocument()
        {
            RestoreOriginalDocument(1); // Audit log is added as the first operation in the test
        }

        protected void TestDragAndDrop()
        {
            {
                // Reset to original document state instead of reopening file - much faster
                RestoreAuditLogDocument();
                WaitForFilesTree();

                var indexOfLastReplicate = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Nodes.Count - 1;

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
                var replicateFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
                var replicateNode = replicateFolder.NodeAt(0);

                var libraryFolder = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>();
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
            // Reset to original document state instead of reopening file - much faster
            RestoreAuditLogDocument();
            WaitForFilesTree();

            // A, B, C, D => Drag B, C to Parent Node => A, D, B, C
            var dndParams = DragAndDrop<ReplicatesFolder>(new[] { 1, 2, 3 }, -1, DragAndDropDirection.down, MoveType.move_last);
            AssertDragAndDropResults<ReplicatesFolder>(dndParams);

            dndParams = DragAndDrop<SpectralLibrariesFolder>(new[] { 0 }, -1, DragAndDropDirection.down, MoveType.move_last);
            AssertDragAndDropResults<SpectralLibrariesFolder>(dndParams);
        }

        protected void TestReplicateLabelEdit()
        {
            RestoreAuditLogDocument();
            WaitForFilesTree();

            var replicatesFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
            Assert.AreEqual(@"D_102_REP1", replicatesFolder.Nodes[0].Name);

            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.EditTreeNodeLabel((FilesTreeNode)replicatesFolder.Nodes[0], @"NEW NAME");
            });
            WaitForFilesTree();

            Assert.AreEqual(@"NEW NAME", replicatesFolder.Nodes[0].Name);
            Assert.AreEqual(@"NEW NAME", SkylineWindow.Document.MeasuredResults.Chromatograms[0].Name);
            Assert.AreEqual(@"NEW NAME", SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Nodes[0].Name);

            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.EditTreeNodeLabel((FilesTreeNode)replicatesFolder.Nodes[1], @"ANOTHER NEW NAME");
            });
            WaitForFilesTree();

            Assert.AreEqual(@"ANOTHER NEW NAME", replicatesFolder.Nodes[1].Name);
            Assert.AreEqual(@"ANOTHER NEW NAME", SkylineWindow.Document.MeasuredResults.Chromatograms[1].Name);
            Assert.AreEqual(@"ANOTHER NEW NAME", SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Nodes[1].Name);

            // Make sure the first name remains intact
            Assert.AreEqual(@"NEW NAME", replicatesFolder.Nodes[0].Name);
            Assert.AreEqual(@"NEW NAME", SkylineWindow.Document.MeasuredResults.Chromatograms[0].Name);
            Assert.AreEqual(@"NEW NAME", SkylineWindow.FilesTree.RootChild<ReplicatesFolder>().Nodes[0].Name);
        }

        /// <summary>
        /// Tests the drag-drop event handlers using DragDropSimulator to exercise code paths
        /// that require mouse interaction in production.
        /// </summary>
        protected void TestDragSimulation()
        {
            // Reset to original document state instead of reopening file - much faster
            RestoreAuditLogDocument();
            WaitForFilesTree();

            var filesTreeForm = SkylineWindow.FilesTreeForm;
            var simulator = new DragDropSimulator(filesTreeForm);

            RunUI(() => filesTreeForm.DragDropHandler = simulator);

            // Test 1: Initiate drag from a replicate node
            var replicatesFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
            var dragNode = replicatesFolder.NodeAt(0);

            RunUI(() =>
            {
                // Select the node and simulate mouse down + move to initiate drag
                SkylineWindow.FilesTree.SelectedNode = dragNode;
                var nodeBounds = dragNode.Bounds;
                var startPoint = new Point(nodeBounds.X + 10, nodeBounds.Y + nodeBounds.Height / 2);

                filesTreeForm.SimulateMouseDown(startPoint);
                // Move enough to trigger drag (past MoveThreshold)
                var dragPoint = new Point(startPoint.X + 20, startPoint.Y + 20);
                filesTreeForm.SimulateMouseMoveWithLeftButton(dragPoint);
            });

            // Verify drag was initiated
            Assert.IsTrue(simulator.IsDragging);
            Assert.IsNotNull(simulator.DragData);
            Assert.AreEqual(DragDropEffects.Move, simulator.AllowedEffects);

            RunUI(() => Assert.IsTrue(filesTreeForm.IsRemoveDropTargetVisible));

            // Test 2: DragEnter event
            var dropNode = replicatesFolder.NodeAt(2);
            Point screenPoint = Point.Empty;
            RunUI(() =>
            {
                var nodeBounds = dropNode.Bounds;
                var clientPoint = new Point(nodeBounds.X + nodeBounds.Width / 2, nodeBounds.Y + nodeBounds.Height / 2);
                screenPoint = SkylineWindow.FilesTree.PointToScreen(clientPoint);
                simulator.SimulateDragEnter(screenPoint);
            });

            Assert.AreEqual(DragDropEffects.Move, simulator.LastEffect);

            // Test 3: DragOver on valid target (another replicate)
            RunUI(() => simulator.SimulateDragOver(screenPoint));
            Assert.AreEqual(DragDropEffects.Move, simulator.LastEffect);

            // Test 4: DragOver on invalid target (spectral library)
            var librariesFolder = SkylineWindow.FilesTree.RootChild<SpectralLibrariesFolder>();
            var invalidDropNode = librariesFolder.NodeAt(0);
            RunUI(() =>
            {
                var nodeBounds = invalidDropNode.Bounds;
                var clientPoint = new Point(nodeBounds.X + nodeBounds.Width / 2, nodeBounds.Y + nodeBounds.Height / 2);
                var invalidScreenPoint = SkylineWindow.FilesTree.PointToScreen(clientPoint);
                simulator.SimulateDragOver(invalidScreenPoint);
            });

            Assert.AreEqual(DragDropEffects.None, simulator.LastEffect);

            // Test 5: DragLeave (mouse leaves the tree area)
            RunUI(() => simulator.SimulateDragLeave());
            // Note: DragLeave only hides effects if mouse is outside bounds

            // Test 6: Cancel drag with Escape
            simulator.EndDrag();
            RunUI(() =>
            {
                // Start a new drag
                SkylineWindow.FilesTree.SelectedNode = dragNode;
                var nodeBounds = dragNode.Bounds;
                var startPoint = new Point(nodeBounds.X + 10, nodeBounds.Y + nodeBounds.Height / 2);
                filesTreeForm.SimulateMouseDown(startPoint);
                var dragPoint = new Point(startPoint.X + 20, startPoint.Y + 20);
                filesTreeForm.SimulateMouseMoveWithLeftButton(dragPoint);
            });

            Assert.IsTrue(simulator.IsDragging);
            RunUI(() => simulator.SimulateEscapeCancel());
            Assert.IsFalse(simulator.IsDragging);
            RunUI(() => Assert.IsFalse(filesTreeForm.IsRemoveDropTargetVisible));

            // Test 7: Successful drop via DragDrop event
            RunUI(() =>
            {
                SkylineWindow.FilesTree.SelectedNode = dragNode;
                var nodeBounds = dragNode.Bounds;
                var startPoint = new Point(nodeBounds.X + 10, nodeBounds.Y + nodeBounds.Height / 2);
                filesTreeForm.SimulateMouseDown(startPoint);
                var dragPoint = new Point(startPoint.X + 20, startPoint.Y + 20);
                filesTreeForm.SimulateMouseMoveWithLeftButton(dragPoint);
            });

            Assert.IsTrue(simulator.IsDragging);

            var oldDoc = SkylineWindow.Document;
            RunUI(() =>
            {
                var targetNode = replicatesFolder.NodeAt(2);
                var nodeBounds = targetNode.Bounds;
                var clientPoint = new Point(nodeBounds.X + nodeBounds.Width / 2, nodeBounds.Y + nodeBounds.Height / 2);
                var dropScreenPoint = SkylineWindow.FilesTree.PointToScreen(clientPoint);
                simulator.SimulateDragEnter(dropScreenPoint);
                simulator.SimulateDragOver(dropScreenPoint);
                simulator.SimulateDrop(dropScreenPoint);
            });

            Assert.IsFalse(simulator.IsDragging);
            WaitForDocumentChangeAndFilesTree(oldDoc);

            // Test 8: Drop on remove target
            replicatesFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
            var nodeToRemove = replicatesFolder.NodeAt(0);
            var nodeToRemoveName = nodeToRemove.Name;

            RunUI(() =>
            {
                SkylineWindow.FilesTree.SelectedNode = nodeToRemove;
                var nodeBounds = nodeToRemove.Bounds;
                var startPoint = new Point(nodeBounds.X + 10, nodeBounds.Y + nodeBounds.Height / 2);
                filesTreeForm.SimulateMouseDown(startPoint);
                var dragPoint = new Point(startPoint.X + 20, startPoint.Y + 20);
                filesTreeForm.SimulateMouseMoveWithLeftButton(dragPoint);
            });

            Assert.IsTrue(simulator.IsDragging);
            RunUI(() => Assert.IsTrue(filesTreeForm.IsRemoveDropTargetVisible));

            oldDoc = SkylineWindow.Document;
            var confirmDlg = ShowDialog<MultiButtonMsgDlg>(() =>
            {
                var removeTargetScreenPoint = Point.Empty; // Doesn't matter for remove target
                simulator.SimulateRemoveTargetDragEnter(removeTargetScreenPoint);
                simulator.SimulateRemoveTargetDrop(removeTargetScreenPoint);
            });
            OkDialog(confirmDlg, confirmDlg.ClickYes);

            Assert.IsFalse(simulator.IsDragging);
            WaitForDocumentChangeAndFilesTree(oldDoc);

            // Verify the node was removed
            replicatesFolder = SkylineWindow.FilesTree.RootChild<ReplicatesFolder>();
            Assert.IsFalse(replicatesFolder.Nodes.Cast<TreeNode>().Any(n => n.Name == nodeToRemoveName));

            // Clean up
            RunUI(() => filesTreeForm.DragDropHandler = null);
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
            var folder = SkylineWindow.FilesTree.RootChild<T>();

            var dragNodes = dragNodeIndexes.Select(index => folder.Nodes[index]).Cast<FilesTreeNode>().ToList();

            var selectedNode = dragNodes.Last();
            var dropNode = dropNodeIndex == -1 ? folder : folder.NodeAt(dropNodeIndex);

            var oldDoc = SkylineWindow.Document;
            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.DropNodes(dragNodes, selectedNode, dropNode, moveType, DragDropEffects.Move);
            });
            var newDoc = WaitForDocumentChangeLoaded(oldDoc);
            WaitForFilesTree();

            folder = SkylineWindow.FilesTree.RootChild<T>();

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
            var treeNodes = SkylineWindow.FilesTree.RootChild<T>().Nodes;
        
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
        /// Wait for a document change and then wait for FilesTree to process the change.
        /// Use this when a document change is triggered by something outside FilesTree (e.g., Undo/Redo).
        /// </summary>
        private static SrmDocument WaitForDocumentChangeAndFilesTree(SrmDocument docCurrent)
        {
            var doc = WaitForDocumentChange(docCurrent);
            WaitForFilesTree();
            return doc;
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
