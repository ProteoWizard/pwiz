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
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using Process = System.Diagnostics.Process;

// CONSIDER: using IdentityPath (and DocNode.ReplaceChild) to simplify replicate name changes
//           But replicates do not have IdentityPath support because ChromatogramSet does not
//           inherit from DocNode. Will revisit later.
// ReSharper disable WrongIndentSize
namespace pwiz.Skyline.Controls.FilesTree
{
    public partial class FilesTreeForm : DockableFormEx, ITipDisplayer
    {
        private NodeTip _nodeTip;
        private Panel _dropTargetRemove;
        private readonly MoveThreshold _moveThreshold = new MoveThreshold(5, 5);

        public FilesTreeForm()
        {
            InitializeComponent();
        }

        public FilesTreeForm(SkylineWindow skylineWindow) : this()
        {
            SkylineWindow = skylineWindow;

            // FilesTree
            filesTree.LabelEdit = true;
            filesTree.AllowDrop = true;
            filesTree.NodeMouseDoubleClick += FilesTree_TreeNodeMouseDoubleClick;
            filesTree.MouseMove += FilesTree_MouseMove;
            filesTree.LostFocus += FilesTree_LostFocus;
            filesTree.BeforeLabelEdit += FilesTree_BeforeLabelEdit;
            filesTree.AfterLabelEdit += FilesTree_AfterLabelEdit;
            filesTree.ItemDrag += FilesTree_ItemDrag;
            filesTree.DragEnter += FilesTree_DragEnter;
            filesTree.DragLeave += FilesTree_DragLeave;
            filesTree.DragOver += FilesTree_DragOver;
            filesTree.DragDrop += FilesTree_DragDrop;
            filesTree.QueryContinueDrag += FilesTree_QueryContinueDrag;
            filesTree.KeyDown += FilesTree_KeyDown;

            // FilesTree => context menu
            filesTreeContextMenu.Opening += FilesTree_ContextMenuStrip_Opening;
            filesTree.ContextMenuStrip = filesTreeContextMenu;

            // FilesTree => tooltips
            _nodeTip = new NodeTip(this) { Parent = TopLevelControl };

            // FilesTree => floating drop target
            _dropTargetRemove = CreateDropTarget(FilesTreeResources.Trash, DockStyle.None);
            _dropTargetRemove.Visible = false;
            _dropTargetRemove.AllowDrop = true;
            _dropTargetRemove.DragEnter += DropTargetRemove_DragEnter;
            _dropTargetRemove.DragDrop += DropTargetRemove_DragDrop;
            _dropTargetRemove.QueryContinueDrag += FilesTree_QueryContinueDrag;

            filesTree.Controls.Add(_dropTargetRemove);

            filesTree.InitializeTree(SkylineWindow);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FilesTree FilesTree => filesTree;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkylineWindow SkylineWindow { get; }

        protected override string GetPersistentString()
        {
            return base.GetPersistentString() + @"|" + FilesTree.GetPersistentString();
        }

        public void OpenAuditLog()
        {
            SkylineWindow.ShowAuditLog();
        }

        public void OpenContainingFolder(TreeNode selectedNode)
        {
            if (FilesTree.SelectedNode is FilesTreeNode filesTreeNode)
            {
                Process.Start(@"explorer.exe", $@"/select,""{filesTreeNode.LocalFilePath}""");
            }
        }

        public void OpenLibraryExplorerDialog(TreeNode selectedNode)
        {
            var model = (SpectralLibrary)((FilesTreeNode)selectedNode).Model;
            var libraryName = model.Name;

            var index = SkylineWindow.OwnedForms.IndexOf(form => form is ViewLibraryDlg);

            // Change the selected library if Library Explorer is already available
            if (index != -1)
            {
                var viewLibraryDlg = SkylineWindow.OwnedForms[index] as ViewLibraryDlg;
                viewLibraryDlg?.Activate();
                viewLibraryDlg?.ChangeSelectedLibrary(libraryName);
            }
            // If not, open a new Library Explorer.
            else
            {
                SkylineWindow.OpenLibraryExplorer(libraryName);
            }
        }

        // Replicates open using the replicate name, which is pulled from the parent tree node which
        // avoids adding a pointer to the parent in SrmSettings and adding a new field on the
        // replicate file's data model
        public void ActivateReplicate(TreeNode selectedNode)
        {
            var filesTreeNode = (FilesTreeNode)selectedNode;
            Replicate model;
            switch (filesTreeNode.Model)
            {
                case ReplicateSampleFile _:
                    model = (Replicate)((FilesTreeNode)filesTreeNode.Parent).Model;
                    break;
                case Replicate replicate:
                    model = replicate;
                    break;
                default:
                    return;
            }

            SkylineWindow.ActivateReplicate(model.Name);
        }

        public void OpenManageResultsDialog()
        {
            SkylineWindow.ManageResults();
        }

        private void OpenImportResultsDialog()
        {
            SkylineWindow.ImportResults();
        }

        public void OpenLibraryExplorerDialog()
        {
            SkylineWindow.ViewSpectralLibraries();
        }

        private DialogResult ConfirmItemDeletion(string confirmMsg)
        {
            var confirmDlg = new MultiButtonMsgDlg(confirmMsg, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false);

            // Activate the "No" button
            confirmDlg.ActiveControl = confirmDlg.VisibleButtons.Last();
            confirmDlg.KeyUp += (sender, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    confirmDlg.BtnCancelClick();
                }
            };

            return confirmDlg.ShowAndDispose(this);
        }

        public void RemoveAll(FilesTreeNode node)
        {
            var type = node.Model.GetType();
            Assume.IsTrue(type == typeof(ReplicatesFolder) || type == typeof(SpectralLibrariesFolder));

            var confirmMsg = type == typeof(ReplicatesFolder)
                ? FilesTreeResources.FilesTreeForm_ConfirmRemoveMany_Replicates
                : FilesTreeResources.FilesTreeForm_ConfirmRemoveMany_Spectral_Libraries;

            if (ConfirmItemDeletion(confirmMsg) == DialogResult.No)
                return;

            if (type == typeof(ReplicatesFolder))
            {
                SkylineWindow.ModifyDocument(FilesTreeResources.Remove_All_Replicate_Nodes,
                    document =>
                    {
                        var newDoc = document.ChangeMeasuredResults(null);
                        newDoc.ValidateResults();
                        return newDoc;
                    },
                    docPair =>
                        AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_nodes_remove_all,
                            docPair.NewDocumentType)
                );
            }
            else if (type == typeof(SpectralLibrariesFolder))
            {
                SkylineWindow.ModifyDocument(FilesTreeResources.Remove_All_Spectral_Library_Nodes,
                    document =>
                    {
                        var newPepLibraries =
                            document.Settings.PeptideSettings.Libraries.ChangeLibraries(Array.Empty<LibrarySpec>(), Array.Empty<Library>());
                        var newPepSettings = document.Settings.PeptideSettings.ChangeLibraries(newPepLibraries);
                        var newSettings = document.Settings.ChangePeptideSettings(newPepSettings);
                        var newDoc = document.ChangeSettings(newSettings);
                        return newDoc;
                    },
                    docPair =>
                        AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_nodes_remove_all, docPair.NewDocumentType)
                );
            }
        }

        public void RemoveSelected(IList<FilesTreeNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            var type = nodes.First().Model.GetType();
            Assume.IsTrue(type == typeof(Replicate) || type == typeof(SpectralLibrary));

            string confirmMsg;
            if (type == typeof(Replicate))
            {
                confirmMsg = nodes.Count == 1
                    ? FilesTreeResources.FilesTreeForm_ConfirmRemove_Replicate
                    : FilesTreeResources.FilesTreeForm_ConfirmRemoveMany_Replicates;
            }
            else if (type == typeof(SpectralLibrary))
            {
                confirmMsg = nodes.Count == 1
                    ? FilesTreeResources.FilesTreeForm_ConfirmRemove_Spectral_Library
                    : FilesTreeResources.FilesTreeForm_ConfirmRemoveMany_Spectral_Libraries;
            }
            else return;

            if (ConfirmItemDeletion(confirmMsg) == DialogResult.No)
                return;

            var selectedIds = nodes.Select(item => item.Model.IdentityPath.Child).ToList();
            if (type == typeof(Replicate))
            {
                SkylineWindow.ModifyDocument(FilesTreeResources.Remove_Replicate_Node,
                    document =>
                    {
                        var oldMeasuredResults = SkylineWindow.Document.MeasuredResults;
                        var oldChromatograms = oldMeasuredResults.Chromatograms.ToArray();

                        var newChromatograms = new List<ChromatogramSet>(oldChromatograms.Length - selectedIds.Count);
                        for (var i = 0; i < oldChromatograms.Length; i++)
                        {
                            // skip items selected for removal
                            if (!Utils.ContainsCheckReferenceEquals(selectedIds, oldChromatograms[i].Id))
                            {
                                newChromatograms.Add(oldChromatograms[i]);
                            }
                        }

                        var newMeasuredResults = oldMeasuredResults.ChangeChromatograms(newChromatograms);
                        var newDoc = document.ChangeMeasuredResults(newMeasuredResults);
                        newDoc.ValidateResults();
                        return newDoc;
                    },
                    docPair => AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_node_remove, docPair.NewDocumentType, selectedIds)
                );
            }
            else if (type == typeof(SpectralLibrary))
            {
                SkylineWindow.ModifyDocument(FilesTreeResources.Remove_Spectral_Library_Node,
                    document =>
                    {
                        var oldLibrarySpecs = document.Settings.PeptideSettings.Libraries.LibrarySpecs;
                        var newLibrarySpecs = new List<LibrarySpec>();

                        foreach (var librarySpec in oldLibrarySpecs)
                        {
                            // skip items selected for removal
                            if (!Utils.ContainsCheckReferenceEquals(selectedIds, librarySpec.Id))
                            {
                                newLibrarySpecs.Add(librarySpec);
                            }
                        }

                        var newPepLibraries = document.Settings.PeptideSettings.Libraries.ChangeLibrarySpecs(newLibrarySpecs);
                        var newPepSettings = document.Settings.PeptideSettings.ChangeLibraries(newPepLibraries);
                        var newSettings = document.Settings.ChangePeptideSettings(newPepSettings);
                        var newDoc = document.ChangeSettings(newSettings);
                        return newDoc; 
                    },
                    docPair => AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_node_remove, docPair.NewDocumentType, selectedIds));
            }
        }

        public bool EditTreeNodeLabel(FilesTreeNode node, string newLabel)
        {
            if (string.IsNullOrEmpty(newLabel))
                return false;

            var chromatogramSetId = (ChromatogramSetId)node.Model.IdentityPath.GetIdentity(0);
            var chromatogram = SkylineWindow.Document.MeasuredResults.FindChromatogramSet(chromatogramSetId);

            if (chromatogram == null)
                return true;

            var oldName = chromatogram.Name;
            var newName = newLabel;

            SkylineWindow.ModifyDocument(FilesTreeResources.Change_ReplicateName, 
                doc =>
                {
                    var newChromatogram = (ChromatogramSet)chromatogram.ChangeName(newName);
                    var measuredResults = SkylineWindow.Document.MeasuredResults;

                    var chromatograms = measuredResults.Chromatograms.ToArray();
                    for (var i = 0; i < chromatograms.Length; i++)
                    {
                        if (ReferenceEquals(chromatograms[i].Id, newChromatogram.Id))
                        {
                            chromatograms[i] = newChromatogram;
                        }
                    }

                    measuredResults = measuredResults.ChangeChromatograms(chromatograms);
                    var newDoc = doc.ChangeMeasuredResults(measuredResults);
                    newDoc.ValidateResults();
                    return newDoc;
                },
                docPair => 
                    AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_node_renamed, docPair.NewDocumentType, oldName, newName)
            );

            return false;
        }

        // CONSIDER: use IdentityPath to save and restore selected nodes? Caveat, all draggable nodes
        //           types must subclass DocNode, which is not true of replicates.
        public void DropNodes(FilesTreeNode dropNode, FilesTreeNode primaryDraggedNode, IList<FilesTreeNode> draggedNodes, DragDropEffects effect)
        {
            try
            {
                if (effect != DragDropEffects.Move || dropNode == null || !dropNode.IsDroppable() || draggedNodes.Count == 0 || draggedNodes.Contains(dropNode))
                    return;

                var type = primaryDraggedNode.Model.GetType();
                if (type == typeof(Replicate))
                {
                    SkylineWindow.ModifyDocument(FilesTreeResources.Drag_and_Drop_Nodes,
                        doc =>
                        {
                            var draggedImmutables = draggedNodes.Select(item => (ChromatogramSet)item.Model.Immutable).ToList();
                            var newChromatogramSets = new List<ChromatogramSet>(doc.MeasuredResults.Chromatograms);

                            var primaryDraggedNodeIndex = newChromatogramSets.IndexOf((ChromatogramSet)primaryDraggedNode.Model.Immutable);

                            foreach (var item in draggedImmutables)
                            {
                                newChromatogramSets.Remove(item);
                            }

                            if (dropNode.Model.GetType() == typeof(ReplicatesFolder))
                            {
                                newChromatogramSets.InsertRange(0, draggedImmutables);
                            }
                            else
                            {
                                var dropNodeIndex = newChromatogramSets.IndexOf((ChromatogramSet)dropNode.Model.Immutable);

                                if (primaryDraggedNodeIndex < dropNodeIndex)
                                {
                                    newChromatogramSets.InsertRange(dropNodeIndex + 1, draggedImmutables);
                                }
                                else if (primaryDraggedNodeIndex > dropNodeIndex)
                                {
                                    newChromatogramSets.InsertRange(dropNodeIndex, draggedImmutables);
                                }
                                else if (primaryDraggedNodeIndex == dropNodeIndex)
                                {
                                    newChromatogramSets.InsertRange(dropNodeIndex + 1, draggedImmutables);
                                }
                            }

                            var newMeasuredResults = doc.MeasuredResults.ChangeChromatograms(newChromatogramSets);
                            var newDoc = doc.ChangeMeasuredResults(newMeasuredResults);
                            newDoc.ValidateResults();
                            return newDoc;
                        },
                        docPair =>
                        {
                            var entry = AuditLogEntry.CreateCountChangeEntry(
                                MessageType.files_tree_node_drag_and_drop,
                                MessageType.files_tree_nodes_drag_and_drop,
                                docPair.NewDocumentType,
                                draggedNodes.Select(node => node.Text),
                                draggedNodes.Count,
                                str => MessageArgs.Create(str, dropNode.Text),
                                MessageArgs.Create(draggedNodes.Count, dropNode.Text)
                            );

                            if (draggedNodes.Count > 1)
                            {
                                entry = entry.ChangeAllInfo(draggedNodes.Select(node =>
                                    new MessageInfo(MessageType.files_tree_node_drag_and_drop, docPair.NewDocumentType,
                                        node.Text, dropNode.Text)).ToList());
                            }

                            return entry;
                        }
                    );
                }
                else if (type == typeof(SpectralLibrary))
                {
                    SkylineWindow.ModifyDocument(FilesTreeResources.Drag_and_Drop_Nodes,
                        doc => {
                            var draggedImmutables = draggedNodes.Select(item => (LibrarySpec)item.Model.Immutable).ToList();
                            var newLibSpecs = new List<LibrarySpec>(doc.Settings.PeptideSettings.Libraries.LibrarySpecs);

                            var primaryDraggedNodeIndex = newLibSpecs.IndexOf((LibrarySpec)primaryDraggedNode.Model.Immutable);

                            foreach (var item in draggedImmutables)
                            {
                                newLibSpecs.Remove(item);
                            }

                            if (dropNode.Model.GetType() == typeof(SpectralLibrariesFolder))
                            {
                                newLibSpecs.InsertRange(0, draggedImmutables);
                            }
                            else
                            {
                                var dropNodeIndex = newLibSpecs.IndexOf((LibrarySpec)dropNode.Model.Immutable);

                                if (primaryDraggedNodeIndex < dropNodeIndex)
                                {
                                    newLibSpecs.InsertRange(dropNodeIndex + 1, draggedImmutables);
                                }
                                else if (primaryDraggedNodeIndex > dropNodeIndex)
                                {
                                    newLibSpecs.InsertRange(dropNodeIndex, draggedImmutables);
                                }
                                else if (primaryDraggedNodeIndex == dropNodeIndex)
                                {
                                    newLibSpecs.InsertRange(dropNodeIndex + 1, draggedImmutables);
                                }
                            }

                            var newLibs = new Library[newLibSpecs.Count]; // Required by PeptideSettings.Validate() 
                            var newPepLibraries = doc.Settings.PeptideSettings.Libraries.ChangeLibraries(newLibSpecs, newLibs);
                            var newPepSettings = doc.Settings.PeptideSettings.ChangeLibraries(newPepLibraries);
                            var newSettings = doc.Settings.ChangePeptideSettings(newPepSettings);
                            var newDoc = doc.ChangeSettings(newSettings);
                            return newDoc;
                        },
                        docPair =>
                        {
                            var entry = AuditLogEntry.CreateCountChangeEntry(
                                MessageType.files_tree_node_drag_and_drop,
                                MessageType.files_tree_nodes_drag_and_drop,
                                docPair.NewDocumentType,
                                draggedNodes.Select(node => node.Text),
                                draggedNodes.Count,
                                str => MessageArgs.Create(str, dropNode.Text),
                                MessageArgs.Create(draggedNodes.Count, dropNode.Text)
                            );

                            if (draggedNodes.Count > 1)
                            {
                                entry = entry.ChangeAllInfo(draggedNodes.Select(node => new MessageInfo(MessageType.files_tree_node_drag_and_drop, docPair.NewDocumentType, node.Text, dropNode.Text)).ToList());
                            }

                            return entry;
                        }
                    );
                }

                // After the drop, reset selection so dragged nodes are blue
                filesTree.SelectedNodes.Clear();

                filesTree.SelectedNode = primaryDraggedNode;

                foreach (var node in draggedNodes)
                {
                    filesTree.SelectNode(node, true);
                }

                filesTree.Invalidate();
                filesTree.Focus();
            }
            finally
            {
                HideDropTargets(_dropTargetRemove);
            }
        }

        #region ITipDisplayer implementation

        public Rectangle ScreenRect => Screen.GetBounds(FilesTree);

        public bool AllowDisplayTip => FilesTree.Focused;

        public Rectangle RectToScreen(Rectangle r)
        {
            return FilesTree.RectangleToScreen(r);
        }

        #endregion

        // TreeNode => Open Context Menu
        private void FilesTree_ContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            var filesTreeNode = (FilesTreeNode)FilesTree.SelectedNode;

            _nodeTip.HideTip();

            libraryExplorerMenuItem.Visible = false;
            manageResultsMenuItem.Visible = false;
            importResultsMenuItem.Visible = false;
            openAuditLogMenuItem.Visible = false;
            openLibraryInLibraryExplorerMenuItem.Visible = false;
            openContainingFolderMenuItem.Visible = false;
            selectReplicateMenuItem.Visible = false;
            removeAllMenuItem.Visible = false;
            removeMenuItem.Visible = false;

            // Only offer "Open Containing Folder" if (1) supported by this tree node
            // and (2) the file's state is "available"
            if (filesTreeNode.SupportsOpenContainingFolder())
            {
                openContainingFolderMenuItem.Visible = true;
                openContainingFolderMenuItem.Enabled = filesTreeNode.FileState == FileState.available;
            }

            if (filesTreeNode.SupportsRemoveItem())
                removeMenuItem.Visible = true;

            if (filesTreeNode.SupportsRemoveAllItems())
                removeAllMenuItem.Visible = true;

            switch (filesTreeNode.Model)
            {
                case ReplicatesFolder _:
                    manageResultsMenuItem.Visible = true;
                    importResultsMenuItem.Visible = true;
                    return;
                case Replicate _:
                case ReplicateSampleFile _:
                    selectReplicateMenuItem.Visible = true;
                    break;
                case SpectralLibrariesFolder _:
                    libraryExplorerMenuItem.Visible = true;
                    return;
                case SpectralLibrary _:
                    openLibraryInLibraryExplorerMenuItem.Visible = true;
                    break;
                case SkylineAuditLog _:
                    openAuditLogMenuItem.Visible = true;
                    break;
            }
        }

        private void FilesTree_TreeNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var filesTreeNode = e.Node as FilesTreeNode;

            switch (filesTreeNode?.Model)
            {
                case SkylineAuditLog _:
                    OpenAuditLog();
                    break;
                case SpectralLibrary _:
                    OpenLibraryExplorerDialog(filesTreeNode); 
                    break;
                case ReplicateSampleFile _:
                    ActivateReplicate(filesTreeNode);
                    break;
            }
        }

        private void FilesTree_OpenContainingFolderMenuItem(object sender, EventArgs e)
        {
            OpenContainingFolder(FilesTree.SelectedNode);
        }

        private void FilesTree_ManageResultsMenuItem(object sender, EventArgs e)
        {
            OpenManageResultsDialog();
        }

        private void FilesTree_ImportResultsMenuItem(object sender, EventArgs e)
        {
            OpenImportResultsDialog();
        }

        private void FilesTree_OpenLibraryExplorerMenuItem(object sender, EventArgs e) 
        {
            OpenLibraryExplorerDialog();
        }

        private void FilesTree_OpenAuditLogMenuItem(object sender, EventArgs e)
        {
            OpenAuditLog();
        }

        private void FilesTree_ActivateReplicateMenuItem(object sender, EventArgs e)
        {
            ActivateReplicate(FilesTree.SelectedNode);
        }

        private void FilesTree_OpenLibraryInLibraryExplorerMenuItem(object sender, EventArgs e)
        {
            OpenLibraryExplorerDialog(FilesTree.SelectedNode);
        }

        private void FilesTree_RemoveAllMenuItem(object sender, EventArgs e)
        {
            var selection = (FilesTreeNode)FilesTree.SelectedNode;
            RemoveAll(selection);
        }

        private void FilesTree_RemoveMenuItem(object sender, EventArgs e)
        {
            var selection = FilesTree.SelectedNodes.Cast<FilesTreeNode>().ToList();
            RemoveSelected(selection);
        }

        // FilesTree => display ToolTip
        private void FilesTree_MouseMove(object sender, MouseEventArgs e)
        {
            FilesTree_MouseMove(e.Location);
        }

        private void FilesTree_LostFocus(object sender, EventArgs e)
        {
            _nodeTip?.HideTip();
        }

        private void FilesTree_MouseMove(Point location)
        {
            if (!_moveThreshold.Moved(location))
                return;

            _moveThreshold.Location = null;

            var node = FilesTree.GetNodeAt(location) as TreeNodeMS;
            var tipProvider = node as ITipProvider;

            if (tipProvider != null && !tipProvider.HasTip)
                tipProvider = null;

            if (tipProvider != null)
            {
                var rectCapture = node.BoundsMS;
                if (!rectCapture.Contains(location))
                    _nodeTip.HideTip();
                else
                    _nodeTip.SetTipProvider(tipProvider, rectCapture, location);
            }
            else
            {
                _nodeTip?.HideTip();
            }
        }

        private static void FilesTree_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            var filesTreeNode = (FilesTreeNode)e.Node;

            // Only allow edit on Replicate tree nodes
            if (!filesTreeNode.SupportsRename())
            {
                e.CancelEdit = true;
            }
        }

        private void FilesTree_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            var cancel = EditTreeNodeLabel((FilesTreeNode)e.Node, e.Label);
            if (cancel)
            {
                e.CancelEdit = true;
            }
        }

        private void FilesTree_KeyDown(object sender, KeyEventArgs e)
        {
            _nodeTip.HideTip();
        }

        // Select the expected drop location
        private void FilesTree_DragOver(object sender, DragEventArgs e)
        {
            var point = filesTree.PointToClient(new Point(e.X, e.Y));
            var node = (FilesTreeNode)filesTree.GetNodeAt(point);
            var primaryDraggedNode = (FilesTreeNode)e.Data.GetData(typeof(PrimarySelectedNode));

            if (node == null)
                return;

            // Select node at the drop target
            if (PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(node, primaryDraggedNode))
            {
                e.Effect = DragDropEffects.Move;
                filesTree.SelectedNode = node;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }

            // CONSIDER: does setting AutoScroll on the form negate the need for this code?
            // Auto-scroll if near the top or bottom edge.
            var ptView = FilesTree.PointToClient(new Point(e.X, e.Y));
            if (ptView.Y < 10)
            {
                var nodeTop = FilesTree.TopNode;
                if (nodeTop != null && nodeTop.PrevVisibleNode != null)
                    FilesTree.TopNode = nodeTop.PrevVisibleNode;
            }
            if (ptView.Y > FilesTree.Bottom - 10)
            {
                var nodeTop = FilesTree.TopNode;
                if (nodeTop != null && nodeTop.NextVisibleNode != null)
                    FilesTree.TopNode = nodeTop.NextVisibleNode;
            }
        }

        private void FilesTree_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(FilesTreeNode)))
                e.Effect = DragDropEffects.Move;

            ShowDropTargets(_dropTargetRemove);
        }

        private void FilesTree_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var selectedNode = FilesTree.SelectedNode;
            var selectedNodes = FilesTree.SelectedNodes.Cast<FilesTreeNode>().ToList();

            // Order matters! Do this first to either short-circuit if attempting to select an un-draggable
            // node or collect nodes to drag
            var draggedNodes = new List<FilesTreeNode>();
            foreach (var node in selectedNodes)
            {
                if (node.IsDraggable())
                {
                    draggedNodes.Add(node);
                }
                // Cannot drag the .sky file and need to prevent running the next check on Root
                else if (ReferenceEquals(node, FilesTree.Root))
                {
                    return;
                }
                else if (((FilesTreeNode)node.Parent).IsDraggable() && selectedNodes.Contains(node.Parent))
                {
                    /* ignore node and keep going - node's parent is selected and will be added to draggedNodes */
                }
                else return;
            }

            _nodeTip.HideTip();

            if (!_dropTargetRemove.Visible)
            {
                // TODO: adjust sizing dynamically (hard-coded width, center vertically, adjust for scrollbars, handle Skyline resize)
                var x = FilesTree.Bounds.X + FilesTree.Bounds.Width - 80;
                var y = FilesTree.SelectedNode.Bounds.Y;

                _dropTargetRemove.Location = new Point(x, y);
            }

            ShowDropTargets(_dropTargetRemove);

            var dataObj = new DataObject();
            dataObj.SetData(typeof(PrimarySelectedNode), selectedNode);
            dataObj.SetData(typeof(FilesTreeNode), draggedNodes);

            filesTree.DoDragDrop(dataObj, DragDropEffects.Move);
        }

        private void FilesTree_DragDrop(object sender, DragEventArgs e)
        {
            var dropPoint = filesTree.PointToClient(new Point(e.X, e.Y));
            var dropNode = (FilesTreeNode)filesTree.GetNodeAt(dropPoint);

            var primaryDraggedNode = (FilesTreeNode)e.Data.GetData(typeof(PrimarySelectedNode));
            var dragNodeList = (IList<FilesTreeNode>)e.Data.GetData(typeof(FilesTreeNode));

            DropNodes(dropNode, primaryDraggedNode, dragNodeList, e.Effect);
        }

        private void FilesTree_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (e.EscapePressed)
            {
                e.Action = DragAction.Cancel;
                HideDropTargets(_dropTargetRemove);
            }
        }

        // TODO: de-select all items in tree when mouse enters a drop target. Possible
        //       there's a bug in TreeNodeMS (?) keeping an item highlighted (light blue) 
        //       despite clearing SelectedNode/Nodes, even when invalidating tree
        private void DropTargetRemove_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.AllowedEffect;

            filesTree.SelectedNode = null;
            filesTree.SelectedNodes.Clear();

            ShowDropTargets(_dropTargetRemove, true);
        }

        private void DropTargetRemove_DragDrop(object sender, DragEventArgs e)
        {
            var nodes = (IList<FilesTreeNode>)e.Data.GetData(typeof(FilesTreeNode));

            try
            {
                RemoveSelected(nodes);
            }
            finally
            {
                HideDropTargets(_dropTargetRemove);
            }
        }

        private void FilesTree_DragLeave(object sender, EventArgs e)
        {
            var mouseLocation = MousePosition;
            var inside = Utils.IsPointInRectangle(mouseLocation, Bounds);

            if (!inside)
            {
                HideDropTargets(_dropTargetRemove);
            }
        }

        private static void ShowDropTargets(Panel panel, bool highlight = false)
        {
            panel.Visible = true;
            panel.BackColor = highlight ? Color.LightYellow : Color.WhiteSmoke;
        }

        private static void HideDropTargets(Panel panel)
        {
            panel.Visible = false;
            panel.BackColor = Color.WhiteSmoke;
        }

        private static Panel CreateDropTarget(Image icon, DockStyle dockStyle)
        {
            var dropTarget = new Panel
            {
                Dock = dockStyle,
                Padding = new Padding(2),
                Size = new Size { Height = 30, Width = 30 },
                BorderStyle = BorderStyle.FixedSingle
            };

            var pictureBox = new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Dock = DockStyle.Fill,
            };

            dropTarget.Controls.Add(pictureBox);

            return dropTarget;
        }
    }

    internal class Utils
    {
        internal static bool ContainsCheckReferenceEquals(IList<Identity> list, Identity item)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                    return true;
            }
            return false;
        }

        internal static bool IsPointInRectangle(Point point, Rectangle rectangle)
        {
            return point.X >= rectangle.X && 
                   point.X <= rectangle.X + rectangle.Width && 
                   point.Y >= rectangle.Y && 
                   point.Y <= rectangle.Y + rectangle.Height;
        }
    }

    // Used as a key for passing drag-and-drop data between event handlers
    public sealed class PrimarySelectedNode
    {
        public static bool DoesDropTargetAcceptDraggedNode(FilesTreeNode possibleDropTarget, FilesTreeNode primaryDraggedNode)
        {
            if (primaryDraggedNode == null)
                return false;
            else return possibleDropTarget.Model.GetType() == primaryDraggedNode.Model.GetType() || 
                        possibleDropTarget.Model.GetType() == ((FilesTreeNode)primaryDraggedNode.Parent).Model.GetType();
        }
    }
}