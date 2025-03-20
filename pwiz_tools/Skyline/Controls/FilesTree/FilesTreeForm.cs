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
// TODO: drag-and-drop for spectral libraries
// ReSharper disable WrongIndentSize
namespace pwiz.Skyline.Controls.FilesTree
{
    public partial class FilesTreeForm : DockableFormEx, ITipDisplayer
    {
        private NodeTip _nodeTip;
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
            filesTree.DragOver += FilesTree_DragOver;
            filesTree.DragDrop += FilesTree_DragDrop;
            filesTree.KeyDown += FilesTree_KeyDown;

            // FilesTree => context menu
            filesTreeContextMenu.Opening += FilesTree_ContextMenuStrip_Opening;
            filesTree.ContextMenuStrip = filesTreeContextMenu;

            // FilesTree => tooltips
            _nodeTip = new NodeTip(this) { Parent = TopLevelControl };

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

        public void OpenLibraryExplorerDialog()
        {
            SkylineWindow.ViewSpectralLibraries();
        }

        private DialogResult ConfirmItemDeletion(string confirmMsg)
        {
            return MultiButtonMsgDlg.Show(this, 
                                          confirmMsg,
                                          MultiButtonMsgDlg.BUTTON_YES, 
                                          MultiButtonMsgDlg.BUTTON_NO,
                                          false);
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
            if (nodes.Count == 1)
            {
                confirmMsg = type == typeof(Replicate)
                    ? FilesTreeResources.FilesTreeForm_ConfirmRemove_Replicate
                    : FilesTreeResources.FilesTreeForm_ConfirmRemove_Spectral_Library;
            }
            else
            {
                confirmMsg = type == typeof(ReplicatesFolder)
                    ? FilesTreeResources.FilesTreeForm_ConfirmRemoveMany_Replicates
                    : FilesTreeResources.FilesTreeForm_ConfirmRemoveMany_Spectral_Libraries;
            }

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

                        var newChromatograms = new List<ChromatogramSet>(oldChromatograms.Length - nodes.Count);
                        for (var i = 0; i < oldChromatograms.Length; i++)
                        {
                            // skip replicates selected for removal
                            if (!ContainsCheckReferenceEquals(selectedIds, oldChromatograms[i].Id))
                            {
                                newChromatograms.Add(oldChromatograms[i]);
                            }
                        }

                        var newMeasuredResults = oldMeasuredResults.ChangeChromatograms(newChromatograms);
                        return document.ChangeMeasuredResults(newMeasuredResults);
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
                            // skip replicates selected for removal
                            if (!ContainsCheckReferenceEquals(selectedIds, librarySpec.Id))
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
                document =>
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
                    return document.ChangeMeasuredResults(measuredResults);
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
            if (dropNode == null || !dropNode.IsDroppable() || draggedNodes.Count == 0 || draggedNodes.Contains(dropNode))
                return;

            if (effect == DragDropEffects.Move)
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

                        // CONSIDER: make it possible to drag to the bottom of the list without precisely dropping on the last
                        //           node. Maybe highlight a blue "bar" drop target when dragging below the last item?
                        if (dropNode.Model.GetType() == typeof(ReplicatesFolder))
                        {
                            newChromatogramSets.InsertRange(0, draggedImmutables);
                        }
                        else {
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

                        return newDoc;
                    },
                    docPair => {
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
                    manageResultsMenuItem.Enabled = true;
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
            var node = filesTree.GetNodeAt(point);

            // Select node at the drop target
            if (node != null)
            {
                e.Effect = DragDropEffects.Move;
                filesTree.SelectedNode = node;
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
        }

        private void FilesTree_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var selectedNode = FilesTree.SelectedNode;
            var selectedNodes = FilesTree.SelectedNodes.Cast<FilesTreeNode>().ToList();

            _nodeTip.HideTip();

            var draggedNodes = new List<FilesTreeNode>();
            foreach (var node in selectedNodes)
            {
                if (node.IsDraggable())
                {
                    draggedNodes.Add(node);
                }
                else if (((FilesTreeNode)node.Parent).IsDraggable() && selectedNodes.Contains(node.Parent))
                {
                    /* ignore node and keep going - node's parent is selected and will be added to draggedNodes */
                }
                else return;
            }

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

        private static bool ContainsCheckReferenceEquals(IList<Identity> list, Identity item)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                    return true;
            }
            return false;
        }
    }

    // Types used as keys for data passed around during drag-and-drop
    internal sealed class PrimarySelectedNode { }
}