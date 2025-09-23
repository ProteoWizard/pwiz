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
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Files;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

// ReSharper disable WrongIndentSize
namespace pwiz.Skyline.Controls.FilesTree
{
    public class FilesTree : TreeViewMS
    {
        // Fields for editing the label of a FilesTreeNode
        private bool _inhibitOnAfterSelect;
        private FilesTreeNode _triggerLabelEditForNode;
        private TextBox _editTextBox;
        private string _editedLabel;

        /// <summary>
        /// Used to cancel any pending async work when the document changes including any
        /// lingering async tasks waiting in _fsWorkQueue or the UI event loop. This
        /// token source is re-instantiated whenever Skyline loads a new document. For example,
        /// creating a new document (File => New) or opening a different existing document (File => Open).
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        private readonly FileSystemService _fileSystemService;
        private readonly BackgroundActionService _backgroundActionService;

        public FilesTree()
        {
            _backgroundActionService = BackgroundActionService.Create(this);

            _fileSystemService = FileSystemService.Create(this, _backgroundActionService, FileDeleted, FileCreated, FileRenamed);

            _cancellationTokenSource = new CancellationTokenSource();
            _fileSystemService.StartWatching(null, _cancellationTokenSource.Token);

            ImageList = new ImageList
            {
                TransparentColor = Color.Magenta,
                ColorDepth = ColorDepth.Depth32Bit
            };

            ImageList.Images.Add(Resources.Blank);              // 1bpp
            ImageList.Images.Add(Resources.Folder);             // 32bpp
            ImageList.Images.Add(Resources.FolderMissing);      // 32bpp
            ImageList.Images.Add(Resources.File);               // 8bbb
            ImageList.Images.Add(Resources.FileMissing);        // 32bpp
            ImageList.Images.Add(Resources.Replicate);          // 24bpp
            ImageList.Images.Add(Resources.ReplicateMissing);   // 24bpp // TODO: improve icon
            ImageList.Images.Add(Resources.DataProcessing);     // 8bpp
            ImageList.Images.Add(Resources.PeptideLib);         // 4bpp
            ImageList.Images.Add(Resources.Skyline_Release);    // 24bpp
            ImageList.Images.Add(Resources.AuditLog);           // 32bpp
            ImageList.Images.Add(Resources.CacheFile);          // 32bpp
            ImageList.Images.Add(Resources.ViewFile);           // 32bpp
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FilesTreeNode Root => (FilesTreeNode)Nodes[0];

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDuringDragAndDrop { get; internal set; }

        #region Test helpers

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FileSystemType FileSystemType => _fileSystemService.FileSystemType;

        public string PathMonitoredForFileSystemChanges() => _fileSystemService.MonitoredDirectory;
        public bool IsComplete() => _backgroundActionService.IsComplete;

        #endregion

        public void ScrollToTop() => Nodes[0]?.EnsureVisible();

        /// <summary>
        /// Get the Folder associated with a model type.
        /// </summary>
        /// <typeparam name="T">The model type</typeparam>
        /// <returns></returns>
        // CONSIDER: does the model need a way to distinguish "folders" from other node types? Ex: with a marker interface?
        public FilesTreeNode Folder<T>() where T : FileNode
        {
            return Root.Model is T ? 
                Root : 
                Root.Nodes.Cast<FilesTreeNode>().FirstOrDefault(filesTreeNode => filesTreeNode.Model is T);
        }

        public void SelectNodeWithoutResettingSelection(FilesTreeNode node)
        {
            _inhibitOnAfterSelect = true;
            SelectedNode = node;
            _inhibitOnAfterSelect = false;
        }

        public void InitializeTree(IDocumentUIContainer documentUIContainer)
        {
            DocumentContainer = documentUIContainer;

            if (!IsHandleCreated)
                CreateHandle();

            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            // Enable the OnNotifyMessage callback, which is used to improve LabelEdit handling per:
            //      https://web.archive.org/web/20241113105420/https://www.codeproject.com/Articles/11931/Enhancing-TreeView-Customizing-LabelEdit
            SetStyle(ControlStyles.EnableNotifyMessage, true);
            LabelEdit = false;

            OnTextZoomChanged(); // Required to respect non-default fonts when FilesTree is created

            OnDocumentChanged(this, new DocumentChangedEventArgs(null));
        }

        public void OnDocumentSaved(object sender, DocumentSavedEventArgs args)
        {
            Assume.IsNotNull(DocumentContainer);

            if (args == null || DocumentContainer.Document == null)
                return;

            UpdateTree(isSaveAs:args.IsSaveAs);
        }

        public void OnDocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            Assume.IsNotNull(DocumentContainer);

            if (args == null || DocumentContainer.Document == null)
                return;

            // Check SrmSettings. If it didn't change, there's nothing to do  so short-circuit
            if (ReferenceEquals(DocumentContainer.Document.Settings, args.DocumentPrevious?.Settings))
                return;

            // Handle wholesale replacement of the previous document with a new one. Example scenario:
            // document foo.sky is open and user either creates a new (empty) document or opens a different document (bar.sky).
            var changeAll = args.DocumentPrevious != null && !ReferenceEquals(args.DocumentPrevious.Id, DocumentContainer.Document.Id);

            UpdateTree(isSaveAs:false, changeAll);
        }

        internal void UpdateTree(bool isSaveAs = false, bool changeAll = false)
        {
            var document = DocumentContainer.Document;
            var documentFilePath = DocumentContainer.DocumentFilePath;

            // // Logging useful for debugging issues related to document events and file system monitoring
            // {
            //     var nameMsg = documentFilePath != null ? $@"{documentFilePath}" : @"<unsaved>";
            //     var versionMsg = document != null ? @$"{document.RevisionIndex}" : @"null";
            //     Console.WriteLine($@"===== Updating document {nameMsg} with version {versionMsg}.");
            // }

            try
            {
                BeginUpdateMS();

                // Remove existing nodes from FilesTree if the document has changed completely
                if (changeAll)
                {
                    Nodes.Clear();
                }

                // Reset the FileSystemService if it's not monitoring the directory containing this document
                var documentDirectory = Path.GetDirectoryName(documentFilePath);
                if (isSaveAs || !_fileSystemService.IsMonitoringDirectory(documentDirectory))
                {
                    // Stop the out-of-date monitoring service, first triggering the CancellationToken
                    _cancellationTokenSource.Cancel();
                    _fileSystemService.StopWatching();

                    // Start watching the current directory using a new CancellationToken. 
                    _cancellationTokenSource = new CancellationTokenSource();
                    _fileSystemService.StartWatching(documentDirectory, _cancellationTokenSource.Token);
                }

                var files = SkylineFile.Create(document, documentFilePath);

                MergeNodes(new SingletonList<FileNode>(files), Nodes, FilesTreeNode.CreateNode, _cancellationTokenSource.Token);

                Root.Expand(); // Root node should always be expanded

                var cancellationToken = _cancellationTokenSource.Token;
                _backgroundActionService.RunUI(() => 
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Root.RefreshState();
                    }
                });
            }
            finally
            {
                EndUpdateMS();
            }
        }

        // CONSIDER: refactor for more code reuse with SrmTreeNode
        internal void MergeNodes(IList<FileNode> docFilesList, TreeNodeCollection treeNodes, Func<FileNode, FilesTreeNode> createTreeNodeFunc, CancellationToken cancellationToken)
        {
            FileNode nodeDoc = null;

            // Keep remaining tree nodes into a map by the identity global index.
            var remaining = new Dictionary<IdentityPath, FilesTreeNode>();

            // Enumerate as many tree nodes as possible that have either an
            // exact reference match with its corresponding DocNode in the list, or an
            // identity match with its corresponding DocNode.
            var i = 0;

            do
            {
                // Match as many document nodes to existing tree nodes as possible.
                var count = Math.Min(docFilesList.Count, treeNodes.Count);
                while (i < count)
                {
                    nodeDoc = docFilesList[i];
                    var nodeTree = (FilesTreeNode)treeNodes[i];

                    if (nodeTree == null)
                        break;

                    // If IDs, match replace the model.
                    if (nodeTree.Model.IdentityPath.Equals(nodeDoc.IdentityPath))
                    {
                        nodeTree.Model = nodeDoc;
                        LoadFile(nodeTree);
                    }
                    else
                    {
                        // If no usable equality, and not in the map of nodes already
                        // removed, then this loop cannot continue.
                        if (!remaining.TryGetValue(nodeDoc.IdentityPath, out nodeTree))
                            break;

                        // Found a match in the map of removed nodes so update its model and re-insert it
                        nodeTree.Model = nodeDoc;
                        LoadFile(nodeTree);

                        treeNodes.Insert(i, nodeTree);
                    }
                    
                    i++;
                }

                // Add unmatched nodes to a map by GlobalIndex, until the next
                // document node is encountered, or all remaining nodes have been
                // added.
                var remove = new Dictionary<IdentityPath, FilesTreeNode>();
                for (var iRemove = i; iRemove < treeNodes.Count; iRemove++)
                {
                    var nodeTree = (FilesTreeNode)treeNodes[iRemove];

                    if (nodeTree == null)
                        break;

                    // Stop removing, if the next node in the document is encountered.
                    if (nodeDoc != null && nodeTree.Model.IdentityPath.Equals(nodeDoc.IdentityPath))
                        break;

                    remove.Add(nodeTree.Model.IdentityPath, nodeTree);
                    remaining.Add(nodeTree.Model.IdentityPath, nodeTree);
                }

                // Remove the newly mapped children from the tree itself for now.
                foreach (var node in remove.Values)
                    node.Remove();
            }
            // Loop, if not all tree nodes have been removed or matched.
            while (i < treeNodes.Count && treeNodes[i] is FilesTreeNode);

            var firstInsertPosition = i;
            var nodesToInsert = new List<TreeNode>(docFilesList.Count - firstInsertPosition);

            // Enumerate remaining DocNodes adding to the tree either:
            //      (1) corresponding TreeNodes from the map or
            //      (2) creating new TreeNodes as necessary
            for (; i < docFilesList.Count; i++)
            {
                nodeDoc = docFilesList[i];
                if (remaining.TryGetValue(nodeDoc.IdentityPath, out var nodeTree))
                {
                    nodeTree.Model = nodeDoc;
                    nodesToInsert.Add(nodeTree);
                }
                else
                {
                    nodeTree = createTreeNodeFunc(nodeDoc);

                    nodesToInsert.Add(nodeTree);
                    LoadFile(nodeTree);
                }
            }

            if (firstInsertPosition == treeNodes.Count)
            {
                treeNodes.AddRange(nodesToInsert.ToArray());
            }
            else
            {
                for (var insertNodeIndex = 0; insertNodeIndex < nodesToInsert.Count; insertNodeIndex++)
                {
                    treeNodes.Insert(insertNodeIndex + firstInsertPosition, nodesToInsert[insertNodeIndex]);
                }
            }

            // Recursively merge any nested files
            for (i = 0; i < treeNodes.Count; i++)
            {
                var treeNode = (FilesTreeNode)treeNodes[i];
                var model = treeNode?.Model;

                if (model?.Files.Count > 0)
                {
                    MergeNodes(model.Files, treeNode.Nodes, createTreeNodeFunc, cancellationToken);
                }
            }

            return;

            // Load the file for this tree node. Update the node's UI if needed. Usually runs when (1) the node was just created or (2)
            // the node's model was replaced when merging models representing the latest SrmDocument with FilesTree's existing nodes.
            void LoadFile(FilesTreeNode node)
            {
                Assume.IsNotNull(node);
                Assume.IsNotNull(node.Model);

                if (!node.Model.ShouldInitializeLocalFile())
                    return;

                _fileSystemService.LoadFile(node.LocalFilePath, node.FilePath, node.FileName, node.Model.DocumentPath, (localFilePath, token) =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        node.UpdateState(localFilePath);
                    }
                });
            }
        }

        protected override void OnAfterSelect(TreeViewEventArgs e)
        {
            if (!_inhibitOnAfterSelect)
                base.OnAfterSelect(e);
        }

        [Browsable(true)]
        public event EventHandler<NodeLabelEditEventArgs> BeforeNodeEdit;

        [Browsable(false)]
        public event EventHandler<ValidateLabelEditEventArgs> ValidateLabelEdit;

        [Browsable(false)]
        public event EventHandler<NodeLabelEditEventArgs> AfterNodeEdit;

        // TODO (label edit): long click on right side of TreeNode fails to trigger label editing text box
        // CONSIDER (label edit): wrap textbox in new control derived from FormEx to vertically center text?
        protected override void OnBeforeLabelEdit(NodeLabelEditEventArgs e)
        {
            e.CancelEdit = true;
        }

        protected override void OnNotifyMessage(Message m)
        {
            // Warning - brittle, order matters! WM_TIMER messages only sent if
            // LabelEdit = false so be careful if re-ordering. This also affects
            // which part of a TreeNode triggers label editing.
            if (m.Msg == (int) User32.WinMessageType.WM_TIMER)
            {
                if (_triggerLabelEditForNode != null && !ReferenceEquals(_triggerLabelEditForNode, SelectedNode))
                {
                    // If the selected node has changed since the mouse edit,
                    // then cancel the label edit trigger.
                    _triggerLabelEditForNode = null;
                }

                if (_triggerLabelEditForNode != null)
                {
                    _triggerLabelEditForNode = null;
                    StartLabelEdit();
                }
            }
            base.OnNotifyMessage(m);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var node = (FilesTreeNode)SelectedNode;
                if (node == GetNodeAt(0, e.Y) && node.SupportsRename())
                {
                    _triggerLabelEditForNode = node;
                }
            }

            base.OnMouseUp(e);
        }

        public void StartLabelEdit()
        {
            var node = SelectedNode;

            BeforeNodeEdit?.Invoke(this, new NodeLabelEditEventArgs(node));

            _editedLabel = node.Text;

            LabelEdit = true;

            BeginEditNode(node);
        }

        private void BeginEditNode(TreeNode node)
        {
            _editTextBox = new TextBox
            {
                Text = node.Text,
                Bounds = ((FilesTreeNode)node).BoundsMS,
                Font = Font,
                BorderStyle = BorderStyle.FixedSingle
            };

            _editTextBox.KeyDown += LabelEditTextBox_KeyDown;
            _editTextBox.LostFocus += LabelEditTextBox_LostFocus;

            RepositionEditTextBox();

            Parent.Controls.Add(_editTextBox);
            Parent.Controls.SetChildIndex(_editTextBox, 0);

            _editTextBox.SelectAll();
            _editTextBox.Focus();
        }

        private void RepositionEditTextBox()
        {
            var node = ((TreeNodeMS)SelectedNode).BoundsMS;
            _editTextBox.Location = new Point(Location.X + node.Location.X, Location.Y + node.Location.Y);

            const int minWidth = 80;
            var maxWidth = Bounds.Width - 1 - node.Left;

            var size = TextRenderer.MeasureText(_editTextBox.Text, _editTextBox.Font, new Size(_editTextBox.Height, maxWidth));
            var dx = size.Width + 8;
            dx = Math.Max(dx, minWidth);
            dx = Math.Min(dx, maxWidth);

            _editTextBox.Width = dx;
        }

        protected void LabelEditTextBox_LostFocus(object sender, EventArgs e)
        {
            CommitEditBox(false);
        }

        protected void LabelEditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled)
                return;

            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter)
            {
                CommitEditBox(true);
                e.Handled = e.SuppressKeyPress = true;
            }
        }

        public void CommitEditBox(bool wasCancelled)
        {
            if (_editTextBox == null)
                return;

            var node = SelectedNode;
            var label = _editTextBox.Text;

            var editTextBox = _editTextBox;
            _editTextBox = null;
            editTextBox.Parent.Controls.Remove(editTextBox);
            editTextBox.KeyDown -= LabelEditTextBox_KeyDown;
            editTextBox.LostFocus -= LabelEditTextBox_LostFocus;
            _triggerLabelEditForNode = null;

            var nodeLabelEditEventArgs = new NodeLabelEditEventArgs(node, label)
            {
                CancelEdit = wasCancelled
            };
            OnAfterNodeEdit(nodeLabelEditEventArgs);
        }

        protected virtual void OnValidateLabelEdit(ValidateLabelEditEventArgs e)
        {
            ValidateLabelEdit?.Invoke(this, e);
        }

        protected void OnAfterNodeEdit(NodeLabelEditEventArgs e)
        {
            if (e.CancelEdit)
            {
                AfterNodeEdit?.Invoke(this, e);
            }
            else
            {
                LabelEdit = false;
                e.CancelEdit = true;
                if (e.Label == null)
                    return;

                var ea = new ValidateLabelEditEventArgs(e.Label);
                OnValidateLabelEdit(ea);

                if (ea.Cancel)
                {
                    e.Node.Text = _editedLabel;
                    LabelEdit = true;
                    BeginEditNode(e.Node);
                }
                else
                {
                    e.CancelEdit = false;
                    AfterNodeEdit?.Invoke(this, e);
                }
            }
        }

        #region Event handlers used by FileSystemService to monitor the local file system

        public void FileDeleted(string fileName, CancellationToken cancellationToken)
        {
            if (FindTreeNodeForFileName(Root, fileName, out var missingFileTreeNode))
            {
                missingFileTreeNode.FileState = FileState.missing;

                _backgroundActionService.RunUI(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        FilesTreeNode.UpdateImagesForTreeNode(missingFileTreeNode);
                    }
                });
            }
        }

        public void FileCreated(string fileName, CancellationToken cancellationToken)
        {
            // Look for a tree node associated with the new file name. If Files Tree isn't aware
            // of a file with that name, ignore the event.
            if (FindTreeNodeForFileName(Root, fileName, out var availableFileTreeNode))
            {
                availableFileTreeNode.FileState = FileState.available;

                _backgroundActionService.RunUI(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        FilesTreeNode.UpdateImagesForTreeNode(availableFileTreeNode);
                    }
                });
            }
        }

        public void FileRenamed(string oldName, string newName, CancellationToken cancellationToken)
        {
            // Look for a tree node with the file's previous name. If a node with that name is found
            // treat the file as missing.
            if (FindTreeNodeForFileName(Root, oldName, out var treeNodeToUpdate))
            {
                treeNodeToUpdate.FileState = FileState.missing;
            }
            // Now, look for a tree node with the new file name. If found, a file was restored with
            // a name Files Tree is aware of, so mark the file as available.
            else if (FindTreeNodeForFileName(Root, newName, out treeNodeToUpdate))
            {
                treeNodeToUpdate.FileState = FileState.available;
            }

            // If neither the old nor new file names are known to Files Tree, ignore the event.
            if (treeNodeToUpdate == null)
                return;

            _backgroundActionService.RunUI(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    FilesTreeNode.UpdateImagesForTreeNode(treeNodeToUpdate);
                }
            });
        }

        #endregion

        protected override bool IsParentNode(TreeNode node)
        {
            return node.Nodes.Count == 0;
        }

        protected override int EnsureChildren(TreeNode node)
        {
            return node != null ? node.Nodes.Count : 0;
        }

        protected override void Dispose(bool disposing)
        {
            _cancellationTokenSource.Cancel();

            if (_backgroundActionService != null)
            {
                _backgroundActionService.Shutdown();
                _backgroundActionService.Dispose();
            }

            if (_fileSystemService != null)
            {
                _fileSystemService.StopWatching();
                _fileSystemService.Dispose();
            }

            if (_editTextBox != null)
            {
                _editTextBox.KeyDown -= LabelEditTextBox_KeyDown;
                _editTextBox.LostFocus -= LabelEditTextBox_LostFocus;
                _editTextBox.Dispose();
                _editTextBox = null;
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Recursively search FilesTree for a node whose LocalFilePath matches the specified file path.
        /// If found, return true and set value to the matching node. Otherwise, return false and set
        /// value to null.
        /// </summary>
        /// <param name="filesTreeNode">Current tree node</param>
        /// <param name="filePath">Looking for a node with this file path.</param>
        /// <param name="value">The matching node</param>
        /// <returns></returns>
        // TODO: unit tests
        // CONSIDER: improve performance with a dictionary mapping file paths to tree nodes
        private static bool FindTreeNodeForFileName(FilesTreeNode filesTreeNode, string filePath, out FilesTreeNode value)
        {
            value = null;

            if (filesTreeNode.Model.IsBackedByFile && 
                filesTreeNode.LocalFilePath != null && 
                filesTreeNode.LocalFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
            {
                value = filesTreeNode;
                return true;
            }

            foreach (FilesTreeNode n in filesTreeNode.Nodes)
            {
                if (FindTreeNodeForFileName(n, filePath, out value))
                    return true;
            }

            return false;
        }
    }
}