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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls.FilesTree;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Files;
using Peptide = pwiz.Skyline.Model.Peptide;

// TODO: Replicate => verify right-click menu includes Open Containing Folder
// TODO: Test Tooltips. See MethodEditTutorialTest.ShowNodeTip
// TODO: add .sky file with imsdb / irtdb
// TODO: add an Audit Log scenario
// TODO: drag-and-drop disjoint selection
// TODO: cannot drag non-draggable nodes
// TODO: use non-local file paths in SrmSettings (ex: replicate sample files with paths to c:\brendanx\foo\bar

// ReSharper disable WrongIndentSize
namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FilesTreeFormTest : AbstractFunctionalTest
    {
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

            TestSyntheticDocument();

            TestRealDocument();
        }

        protected void TestEmptyDocument()
        {
            RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            Assert.AreEqual(FileResources.FileModel_NewDocument, SkylineWindow.FilesTree.RootNodeText());
            Assert.AreEqual(1, SkylineWindow.FilesTree.Nodes.Count);
            Assert.AreEqual(2, SkylineWindow.FilesTree.Nodes[0].GetNodeCount(false));

            Assert.AreEqual(string.Empty, SkylineWindow.FilesTree.PathMonitoredForFileSystemChanges());
            var monitoredPath = Path.Combine(TestFilesDir.FullPath, "savedFileName.sky");
            RunUI(() => SkylineWindow.SaveDocument(monitoredPath));
            WaitForCondition(() => SkylineWindow.FilesTree.IsMonitoringFileSystem());
            Assert.AreEqual(Path.GetDirectoryName(monitoredPath), SkylineWindow.FilesTree.PathMonitoredForFileSystemChanges());

            AssertFilesTreeOnlyIncludesFilesTreeNodes(SkylineWindow.FilesTree.Root);

            // Close FilesTreeForm so test framework doesn't fail the test due to an unexpected open dialog
            RunUI(() => { SkylineWindow.DestroyFilesTreeForm(); });
        }

        // CONSIDER: does this add value?
        protected void TestSyntheticDocument()
        {
            var emptyDocument = SrmDocumentHelper.MakeEmptyDocument();
            SrmDocumentHelper.AddProteinsToDocument(emptyDocument, 50);

            RunUI(() =>
            {
                SkylineWindow.SwitchDocument(emptyDocument, null);
                SkylineWindow.ShowFilesTreeForm(true);
            });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            Assert.AreEqual(FileResources.FileModel_NewDocument, SkylineWindow.FilesTree.RootNodeText());

            // FilesTree should only have one set of nodes after opening a new document
            Assert.AreEqual(1, SkylineWindow.FilesTree.Nodes.Count);
            Assert.AreEqual(2, SkylineWindow.FilesTree.Nodes[0].GetNodeCount(false));

            AssertFilesTreeOnlyIncludesFilesTreeNodes(SkylineWindow.FilesTree.Root);

            // Close FilesTreeForm so test framework doesn't fail the test due to an unexpected open dialog
            RunUI(() => { SkylineWindow.DestroyFilesTreeForm(); });
        }

        protected void TestRealDocument()
        {
            //
            // SCENARIO - rat_plasma.sky
            //
            var documentPath = TestFilesDir.GetTestPath("Rat_plasma.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            // wait until UI loaded and Sequence Tree is visible
            WaitForConditionUI(() => SkylineWindow.SequenceTreeFormIsVisible);

            // make sure files tree exists but isn't visible
            var list = new List<FilesTreeForm>(SkylineWindow.DockPanel.Contents.OfType<FilesTreeForm>());
            Assert.AreEqual(1, list.Count);
            WaitForConditionUI(() => !SkylineWindow.FilesTreeFormIsVisible);

            // now, show files tree
            RunUI(() => SkylineWindow.ShowFilesTreeForm(true));
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);
            WaitForConditionUI(() => SkylineWindow.FilesTree.IsMonitoringFileSystem());

            Assert.AreEqual("Rat_plasma.sky", SkylineWindow.FilesTree.RootNodeText());
            Assert.AreEqual(3, SkylineWindow.FilesTree.Nodes[0].Nodes.Count);

            // 
            // Restoring View State
            // 
            var treeNodes = SkylineWindow.FilesTree.Nodes;
            RunUI(() =>
            {
                // <file>.sky/
                //      Replicates/
                //          <Replicate-Name>/
                //              <Replicate-Sample-File>.raw
                Assert.IsTrue(treeNodes[0].IsExpanded);
                Assert.IsTrue(treeNodes[0].Nodes[0].IsExpanded);
                Assert.IsTrue(treeNodes[0].Nodes[0].Nodes[0].IsExpanded);
                Assert.IsTrue(treeNodes[0].Nodes[0].Nodes[10].IsExpanded);
                Assert.IsTrue(treeNodes[0].Nodes[1].IsExpanded);
                Assert.IsTrue(treeNodes[0].Nodes[2].IsExpanded);
            });

            //
            // FilesTree should only contain nodes of type FilesTreeNode
            //
            AssertFilesTreeOnlyIncludesFilesTreeNodes(SkylineWindow.FilesTree.Root);

            //
            // Replicates
            //
            var doc = SkylineWindow.Document;

            CheckEquivalenceOfReplicates(42);

            //
            // Rename replicate by updating SkylineDocument directly, which should trigger FilesTree update
            //
            RunUI(() => SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[3].Expand());
            WaitForConditionUI(() => SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[3].IsExpanded);

            string newName = "Test this name";
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

            CheckEquivalenceOfReplicates(42);

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
            var treeNode = (FilesTreeNode)SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[0];
            RunUI(() => SkylineWindow.FilesTreeForm.EditTreeNodeLabel(treeNode, newName));
            Assert.AreEqual(newName, SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[0].Name);

            //
            // Activating a replicate should update selected graphs
            //
            var filesTreeNode = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes[4] as FilesTreeNode;
            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.ActivateReplicate(filesTreeNode);
            });
            WaitForGraphs();
            RunUI(() => Assert.AreEqual(4, SkylineWindow.SelectedResultsIndex));

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

            CheckEquivalenceOfReplicates(42);

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

            CheckEquivalenceOfReplicates(38);

            //
            // Undo deletion of replicate should trigger FilesTree update and restore previously deleted nodes
            //
            RunUI(() => SkylineWindow.Undo());
            WaitForDocumentChange(doc);

            CheckEquivalenceOfReplicates(42);

            RunUI(() =>
            {
                SkylineWindow.FilesTree.Folder<ReplicatesFolder>();

                var replicateNodes = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
                replicateNodes.Nodes[1].Expand();
                replicateNodes.Nodes[7].Expand();
                replicateNodes.Nodes[11].Expand();
                replicateNodes.Nodes[12].Expand();
            });

            // 
            // Hide tree and re-show FilesTree via View Menu making sure tree matches document and expected nodes are expanded
            // 
            RunUI(() =>
            {
                SkylineWindow.ShowFilesTreeForm(true);
            });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            CheckEquivalenceOfReplicates(42);

            RunUI(() =>
            {
                var replicateNodes = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
                // Check expanded nodes
                Assert.IsTrue(replicateNodes.Nodes[1].IsExpanded);
                Assert.IsTrue(replicateNodes.Nodes[7].IsExpanded);
                Assert.IsTrue(replicateNodes.Nodes[11].IsExpanded);
                Assert.IsTrue(replicateNodes.Nodes[12].IsExpanded);
            });

            //
            // TODO: Test Remove and Remove All
            // 

            //
            // Spectral Libraries
            //
            Assert.AreEqual(2, SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>()?.Nodes.Count);
            Assert.AreEqual("Rat (NIST) (Rat_plasma2)", SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name);
            Assert.AreEqual("Rat (GPM) (Rat_plasma2)", SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[1].Name);

            var peptideLibraryTreeNode = (FilesTreeNode)SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>()?.Nodes[0];
            var peptideLibraryModel = (SpectralLibrary)peptideLibraryTreeNode?.Model;
            Assert.AreEqual(SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs[0].Name, peptideLibraryModel?.Name);

            //
            // Spectral Library explorer dialog opens with correct library selected
            //
            RunUI(() => SkylineWindow.FilesTreeForm.OpenLibraryExplorerDialog(peptideLibraryTreeNode));
            var libraryDlg = WaitForOpenForm<ViewLibraryDlg>();
            RunUI(() =>
            {
                Assert.IsTrue(libraryDlg.HasSelectedLibrary);
                Assert.AreEqual(0, libraryDlg.SelectedIndex);
                libraryDlg.Close();
            });
            WaitForConditionUI(() => !libraryDlg.Visible);

            //
            // Project Files folder
            //
            var projectFilesRoot = SkylineWindow.FilesTree.Folder<ProjectFilesFolder>();
            RunUI(() =>
            {
                SkylineWindow.FilesTree.ScrollToFolder<ProjectFilesFolder>();
            });
            WaitForConditionUI(() => projectFilesRoot.IsVisible);

            Assert.AreEqual(2, projectFilesRoot.Nodes.Count);
            Assert.IsTrue(projectFilesRoot.Nodes.ContainsKey(FileResources.FileModel_ViewFile));
            Assert.IsTrue(projectFilesRoot.Nodes.ContainsKey(FileResources.FileModel_ChromatogramCache));

            //
            // Folders not included in this .sky file should not be present in FilesTree
            //
            Assert.IsNull(SkylineWindow.FilesTree.Folder<BackgroundProteomeFolder>());
            Assert.IsNull(SkylineWindow.FilesTree.Folder<IonMobilityLibraryFolder>());
            Assert.IsNull(SkylineWindow.FilesTree.Folder<RTCalcFolder>());
            Assert.IsNull(SkylineWindow.FilesTree.Folder<OptimizationLibraryFolder>());

            TestMonitoringFileSystem();

            TestDragAndDrop();

            // Close FilesTreeForm so test framework doesn't fail the test due to an unexpected open dialog
            RunUI(() => { SkylineWindow.DestroyFilesTreeForm(); });
        }

        // Assumes rat-plasma.sky is loaded
        protected void TestMonitoringFileSystem()
        {
            //
            // File Renamed
            //
            var replicateFolderModel = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var sampleFileTreeNode = (FilesTreeNode)replicateFolderModel.Nodes[0].Nodes[0];
            var sampleFileModel = sampleFileTreeNode.Model as ReplicateSampleFile;

            // Assert file exists and icons are set correctly
            Assert.IsNotNull(sampleFileModel);
            var filePath = sampleFileTreeNode.LocalFilePath;
            Assert.IsTrue(File.Exists(filePath));
            Assert.AreEqual(FileState.available, sampleFileTreeNode.FileState);

            // Rename file for a replicate tree node
            File.Move(filePath, filePath + "RENAMED");
            WaitForConditionUI(() => sampleFileTreeNode.ImageIndex == (int)sampleFileTreeNode.ImageMissing);

            // Assert tree updates given now missing file
            Assert.AreEqual(FileState.missing, sampleFileTreeNode.FileState);
            Assert.AreEqual((int)sampleFileTreeNode.ImageMissing, sampleFileTreeNode.ImageIndex);

            // Assert parent node icons changed given missing file. Node for .sky file should not be affected
            Assert.AreEqual((int)((FilesTreeNode)sampleFileTreeNode.Parent).ImageMissing, sampleFileTreeNode.Parent.ImageIndex);
            Assert.AreEqual((int)((FilesTreeNode)sampleFileTreeNode.Parent.Parent).ImageMissing, sampleFileTreeNode.Parent.Parent.ImageIndex);
            Assert.AreEqual((int)ImageId.skyline, sampleFileTreeNode.Parent.Parent.Parent.ImageIndex);

            // Restore renamed file to original name
            File.Move(filePath + "RENAMED", filePath);
            WaitForConditionUI(() => sampleFileTreeNode.ImageIndex == (int)sampleFileTreeNode.ImageAvailable);

            // Assert file state and icons update correctly
            Assert.AreEqual(FileState.available, sampleFileTreeNode.FileState);
            Assert.AreEqual((int)sampleFileTreeNode.ImageAvailable, sampleFileTreeNode.ImageIndex);

            // Assert parent node icons change back and .sky file remains unchanged
            Assert.AreEqual((int)((FilesTreeNode)sampleFileTreeNode.Parent).ImageAvailable, sampleFileTreeNode.Parent.ImageIndex);
            Assert.AreEqual((int)((FilesTreeNode)sampleFileTreeNode.Parent.Parent).ImageAvailable, sampleFileTreeNode.Parent.Parent.ImageIndex);
            Assert.AreEqual((int)ImageId.skyline, sampleFileTreeNode.Parent.Parent.Parent.ImageIndex);

            CheckEquivalenceOfReplicates(42);

            //
            // File Deleted
            //
            sampleFileTreeNode = (FilesTreeNode)replicateFolderModel.Nodes[2].Nodes[0]; 
            sampleFileModel = (ReplicateSampleFile)sampleFileTreeNode.Model;

            Assert.IsNotNull(sampleFileModel);

            filePath = sampleFileTreeNode.LocalFilePath;
            Assert.IsTrue(File.Exists(filePath));

            File.Delete(filePath);
            WaitForCondition(() => !File.Exists(filePath));
            WaitForConditionUI(() => sampleFileTreeNode.ImageIndex == (int)sampleFileTreeNode.ImageMissing);

            // replicate sample file
            Assert.AreEqual(FileState.missing, sampleFileTreeNode.FileState);
            Assert.AreEqual((int)sampleFileTreeNode.ImageMissing, sampleFileTreeNode.ImageIndex);

            // and that parent nodes (replicate, replicate folder) changed their icons and sky file remains unchanged
            Assert.AreEqual((int)((FilesTreeNode)sampleFileTreeNode.Parent).ImageMissing, sampleFileTreeNode.Parent.ImageIndex);
            Assert.AreEqual((int)((FilesTreeNode)sampleFileTreeNode.Parent.Parent).ImageMissing, sampleFileTreeNode.Parent.Parent.ImageIndex);
            Assert.AreEqual((int)ImageId.skyline, sampleFileTreeNode.Parent.Parent.Parent.ImageIndex);

            CheckEquivalenceOfReplicates(42);
        }

        protected void TestDragAndDrop()
        {
            {
                // Re-open a clean document
                var documentPath = TestFilesDir.GetTestPath("Rat_plasma.sky");
                RunUI(() => {
                    SkylineWindow.OpenFile(documentPath);
                    SkylineWindow.ShowFilesTreeForm(true);
                });
                WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

                var lastItemIndex = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes.Count - 1;

                // A,B,C,D => Drag A to B => A,B,C,D
                var dnd = DragAndDrop<ReplicatesFolder>(new[] { 0 }, 1, DragDirection.down);
                VerifyDragAndDrop(dnd, CheckEquivalenceOfReplicates);

                // A,B,C,D => Drag A to C => B,A,C,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 0 }, 2, DragDirection.down);
                VerifyDragAndDrop(dnd, CheckEquivalenceOfReplicates);

                // B,A,C,D => Drag A to B => A,B,C,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 1 }, 0, DragDirection.up);
                VerifyDragAndDrop(dnd, CheckEquivalenceOfReplicates);

                // A,B,C,D => Drag A to D => B,C,A,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 0 }, lastItemIndex, DragDirection.down, true);
                VerifyDragAndDrop(dnd, CheckEquivalenceOfReplicates);

                // B,C,A,D => Drag D to B => A,B,C,D
                dnd = DragAndDrop<ReplicatesFolder>(new[] { lastItemIndex }, 0, DragDirection.up);
                VerifyDragAndDrop(dnd, CheckEquivalenceOfReplicates);

                // A,B,C,D,E => Drag A,B,C to E => D,A,B,C,E
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 3, 4, 5 }, 9, DragDirection.down);
                VerifyDragAndDrop(dnd, CheckEquivalenceOfReplicates);

                // D,A,B,C,E => Drag A,B,C to D => A,B,C,D,E
                dnd = DragAndDrop<ReplicatesFolder>(new[] { 7, 8, 9 }, 3, DragDirection.up);
                VerifyDragAndDrop(dnd, CheckEquivalenceOfReplicates);

                dnd = DragAndDrop<ReplicatesFolder>(new[] { 3, 4, 5 }, lastItemIndex, DragDirection.down);
                VerifyDragAndDrop(dnd, CheckEquivalenceOfReplicates);

                dnd = DragAndDrop<ReplicatesFolder>(new[] { lastItemIndex - 2, lastItemIndex - 1, lastItemIndex }, 0, DragDirection.up);
                VerifyDragAndDrop(dnd, CheckEquivalenceOfReplicates);
            }

            // Drag-and-drop - spectral libraries
            {
                var dnd = DragAndDrop<SpectralLibrariesFolder>(new[] { 0 }, 1, DragDirection.down, true);
                VerifyDragAndDrop(dnd, CheckEquivalenceOfSpectralLibraries);

                dnd = DragAndDrop<SpectralLibrariesFolder>(new[] { 1 }, 0, DragDirection.up);
                VerifyDragAndDrop(dnd, CheckEquivalenceOfSpectralLibraries);
            }

            // Drag-and-drop internals 
            {
                var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
                var replicateNode = (FilesTreeNode)replicateFolder.Nodes[0];

                Assert.IsTrue(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode((FilesTreeNode)replicateFolder.Nodes[1], replicateNode));
                Assert.IsTrue(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(replicateFolder, replicateNode));

                var libraryFolder = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>();
                Assert.IsFalse(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode((FilesTreeNode)libraryFolder.Nodes[0], replicateNode));
                Assert.IsFalse(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(libraryFolder, replicateNode));

                var libraryNode = (FilesTreeNode)libraryFolder.Nodes[0];
                Assert.IsTrue(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode((FilesTreeNode)libraryFolder.Nodes[0], libraryNode));
                Assert.IsTrue(PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(libraryFolder, libraryNode));
            }
        }

        private static DragAndDropParams DragAndDrop<T>(int[] dragNodeIndexes, 
                                                        int dropNodeIndex, 
                                                        DragDirection direction, 
                                                        bool dropBelowLastNode = false) 
            where T : FileNode
        {
            var folder = SkylineWindow.FilesTree.Folder<T>();

            var dragNodes = new List<FilesTreeNode>();
            foreach (var index in dragNodeIndexes)
            {
                dragNodes.Add((FilesTreeNode)folder.Nodes[index]);
            }

            var selectedNode = dragNodes[0];
            var dropNode = (FilesTreeNode)folder.Nodes[dropNodeIndex];

            var oldDoc = SkylineWindow.Document;
            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.DropNodes(dragNodes, selectedNode, dropNode, dropBelowLastNode, DragDropEffects.Move);
            });
            var newDoc = WaitForDocumentChangeLoaded(oldDoc);
            
            folder = SkylineWindow.FilesTree.Folder<T>();

            return new DragAndDropParams {
                DragNodeIndexes = dragNodeIndexes,
                DraggedNodes = dragNodes,

                Direction = direction,

                DropNodeIndex = dropNodeIndex,
                DropNode = dropNode,
                DropBelowLastNode = dropBelowLastNode,

                Folder = folder,

                OldDoc = oldDoc,
                NewDoc = newDoc
            };
        }

        // CONSIDER: use a more generalized way to check equivalence of SrmSettings with the tree
        private delegate void CheckEquivalence(int expectedCount);

        private static void VerifyDragAndDrop(DragAndDropParams d, CheckEquivalence callback)
        {
            // Check SrmSettings changed after a DnD operation
            // CONSIDER: use a more precise check asserting change(s) to a specific part of SrmSettings
            Assert.IsFalse(ReferenceEquals(d.OldDoc.Settings, d.NewDoc.Settings));

            // Verify document nodes match tree nodes
            callback(d.Folder.Nodes.Count);

            // Now, make sure DnD put dragged nodes and the drop target in the correct places

            // Compute the expected index of the drop target
            int dropNodeExpectedIndex;

            if (d.DropBelowLastNode)
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
            Assert.AreEqual(d.DropNode, d.Folder.Nodes[dropNodeExpectedIndex]);

            // Compute the expected index of each dragged node and verify it's in the correct place
            for (var i = 0; i < d.DragNodeIndexes.Length; i++)
            {
                int expectedIndex;
                if (d.DropBelowLastNode)
                {
                    expectedIndex = dropNodeExpectedIndex + 1 + i;
                }
                else
                {
                    expectedIndex = dropNodeExpectedIndex - d.DraggedNodes.Count + i;
                }

                Assert.AreEqual(d.DraggedNodes[i], d.Folder.Nodes[expectedIndex]);
            }
        }

        // CONSIDER: reuse checks for different types of files. Inconvenient today because doc nodes from 
        //           SrmSettings don't share a common base class.
        private static void CheckEquivalenceOfReplicates(int expectedCount)
        {
            var docNodes = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms;

            var filesModel = new SkylineFile(SkylineWindow.Document, SkylineWindow.DocumentFilePath);
            var modelNodes = filesModel.Folder<ReplicatesFolder>();

            var treeNodes = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().Nodes;

            Assert.IsNotNull(docNodes);
            Assert.IsNotNull(modelNodes);
            Assert.IsNotNull(treeNodes);

            Assert.AreEqual(expectedCount, docNodes.Count);
            Assert.AreEqual(expectedCount, modelNodes.Files.Count);
            Assert.AreEqual(expectedCount, treeNodes.Count);

            // use docNodes as ground truth
            for (var i = 0; i < docNodes.Count; i++)
            {
                var filesTreeNode = (FilesTreeNode)treeNodes[i];

                // Check immutables all refer to the same instance
                Assert.IsTrue(ReferenceEquals(docNodes[i], modelNodes.Files[i].Immutable));
                Assert.IsTrue(ReferenceEquals(docNodes[i], filesTreeNode.Model.Immutable));

                // Check ID and Name attributes match
                Assert.AreEqual(docNodes[i].Id, modelNodes.Files[i].IdentityPath.GetIdentity(0));
                Assert.AreEqual(docNodes[i].Id, filesTreeNode.Model.IdentityPath.GetIdentity(0));

                Assert.AreEqual(docNodes[i].Name, modelNodes.Files[i].Name);
                Assert.AreEqual(docNodes[i].Name, treeNodes[i].Name);
            }
        }

        private static void CheckEquivalenceOfSpectralLibraries(int expectedCount)
        {
            var docNodes = SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs;

            var filesModel = new SkylineFile(SkylineWindow.Document, SkylineWindow.DocumentFilePath);
            var modelNodes = filesModel.Folder<SpectralLibrariesFolder>();

            var treeNodes = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>().Nodes;

            Assert.IsNotNull(docNodes);
            Assert.IsNotNull(modelNodes);
            Assert.IsNotNull(treeNodes);

            Assert.AreEqual(expectedCount, docNodes.Count);
            Assert.AreEqual(expectedCount, modelNodes.Files.Count);
            Assert.AreEqual(expectedCount, treeNodes.Count);

            for (var i = 0; i < docNodes.Count; i++)
            {
                var filesTreeNode = (FilesTreeNode)treeNodes[i];

                Assert.IsTrue(ReferenceEquals(docNodes[i], modelNodes.Files[i].Immutable));
                Assert.IsTrue(ReferenceEquals(docNodes[i], filesTreeNode.Model.Immutable));

                Assert.AreEqual(docNodes[i].Id, modelNodes.Files[i].IdentityPath.GetIdentity(0));
                Assert.AreEqual(docNodes[i].Id, filesTreeNode.Model.IdentityPath.GetIdentity(0));

                Assert.AreEqual(docNodes[i].Name, modelNodes.Files[i].Name);
                Assert.AreEqual(docNodes[i].Name, treeNodes[i].Name);
            }
        }

        // FilesTree only supports adding nodes of type FilesTreeNode so make sure other node types
        // were not inadvertently added to the tree
        private static void AssertFilesTreeOnlyIncludesFilesTreeNodes(TreeNode node)
        {
            Assert.IsInstanceOfType(node, typeof(FilesTreeNode));

            foreach (TreeNode treeNode in node.Nodes)
            {
                AssertFilesTreeOnlyIncludesFilesTreeNodes(treeNode);
            }
        }
    }

    internal enum DragDirection { up, down }

    internal class DragAndDropParams
    {
        internal int[] DragNodeIndexes;
        internal int DropNodeIndex;
        internal DragDirection Direction;
        internal SrmDocument OldDoc;
        internal SrmDocument NewDoc;
        internal FilesTreeNode Folder;
        internal IList<FilesTreeNode> DraggedNodes;
        internal FilesTreeNode DropNode;
        internal bool DropBelowLastNode;
    }

    // Borrowing for now from FindNodeCancelTest. Will consolidate if useful.
    internal static class SrmDocumentHelper
    {
        private const string ALL_AMINO_ACIDS = "ACDEFGHIKLMNPQRSTVWY";

        /// <summary>
        /// List of three character combinations which are used as the Peptide Group names
        /// in the test document and also the first three amino acids of all of the peptides
        /// in that Peptide Group
        /// </summary>
        private static readonly List<string> PEPTIDE_GROUP_NAMES = 
            ALL_AMINO_ACIDS.SelectMany(aa => ALL_AMINO_ACIDS.SelectMany(aa2 => ALL_AMINO_ACIDS.Select(aa3 => "" + aa + aa2 + aa3))).ToList();

        internal static SrmDocument MakeEmptyDocument()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var transitionSettings = srmSettings.TransitionSettings;
            transitionSettings = transitionSettings
                .ChangeInstrument(transitionSettings.Instrument.ChangeMinMz(50))
                .ChangeFilter(transitionSettings.Filter
                    .ChangePeptidePrecursorCharges(new[] { Adduct.SINGLY_PROTONATED })
                    .ChangePeptideProductCharges(new[] { Adduct.SINGLY_PROTONATED })
                    .ChangePeptideIonTypes(new[] { IonType.precursor, IonType.b, IonType.y }));
            srmSettings = srmSettings.ChangeTransitionSettings(transitionSettings);
            return new SrmDocument(srmSettings);
        }

        /// <summary>
        /// Add Peptide Groups to the document so that it has the <paramref name="newProteinCount"/>.
        /// The names of the Peptide Groups come from <see cref="PEPTIDE_GROUP_NAMES"/>.
        /// </summary>
        internal static SrmDocument AddProteinsToDocument(SrmDocument document, int newProteinCount)
        {
            var newProteins = new List<PeptideGroupDocNode>();
            for (int i = document.MoleculeGroupCount; i < newProteinCount; i++)
            {
                newProteins.Add(MakePeptideGroup(document.Settings, PEPTIDE_GROUP_NAMES[i]));
            }

            return (SrmDocument)document.ChangeChildren(document.Children.Concat(newProteins).ToArray());
        }

        /// <summary>
        /// Construct a Peptide Group whose name is <paramref name="prefix"/> and which has
        /// 400 Peptides where the peptide sequences are the prefix plus two amino acids.
        /// </summary>
        static PeptideGroupDocNode MakePeptideGroup(SrmSettings settings, string prefix)
        {
            var peptideDocNodes = new List<PeptideDocNode>();
            foreach (var firstAminoAcid in ALL_AMINO_ACIDS)
            {
                foreach (var secondAminoAcid in ALL_AMINO_ACIDS)
                {
                    var peptide = new Peptide(prefix + firstAminoAcid + secondAminoAcid);
                    var peptideDocNode = new PeptideDocNode(peptide, settings, ExplicitMods.EMPTY, null, null,
                        System.Array.Empty<TransitionGroupDocNode>(), true);
                    // PeptideGroupDocNode.GenerateColors is slow, so it's faster to just tell the peptide what color it should be
                    peptideDocNode = peptideDocNode.ChangeColor(Color.Black);
                    peptideDocNode = peptideDocNode.ChangeSettings(settings, SrmSettingsDiff.ALL);
                    peptideDocNodes.Add(peptideDocNode);
                }
            }
            var peptideGroupDocNode = new PeptideGroupDocNode(new PeptideGroup(), prefix, null, peptideDocNodes.ToArray());
            peptideGroupDocNode = peptideGroupDocNode.ChangeProteinMetadata(peptideGroupDocNode.ProteinMetadata.SetWebSearchCompleted());
            return peptideGroupDocNode;
        }
    }
}
