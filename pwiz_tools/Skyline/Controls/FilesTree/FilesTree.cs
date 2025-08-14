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
        private bool _inhibitOnAfterSelect;
        private bool _monitoringFileSystem;
        private string _monitoredFilePath;
        private FileSystemWatcher _fsWatcher;
        private QueueWorker<Action> _fsWorkQueue = new QueueWorker<Action>(null, (a, i) => a());
        private FilesTreeNode _triggerLabelEditForNode;
        private TextBox _editTextBox;
        private string _editedLabel;

        public FilesTree()
        {
            _monitoringFileSystem = false;
            _monitoredFilePath = null;
            _fsWatcher = new FileSystemWatcher();

            // Set to zero to handle work items synchronously
            var threadCount = ParallelEx.GetThreadCount();
            _fsWorkQueue.RunAsync(threadCount, @"FilesTree file system work queue");

            ImageList = new ImageList
            {
                TransparentColor = Color.Magenta,
                ColorDepth = ColorDepth.Depth32Bit
            };

            ImageList.Images.Add(Resources.Blank);              // 1bpp
            ImageList.Images.Add(Resources.Folder);             // 32bpp
            ImageList.Images.Add(Resources.File);               // 8bbb
            ImageList.Images.Add(Resources.MissingFile);        // 32bpp
            ImageList.Images.Add(Resources.Replicate);          // 24bpp
            ImageList.Images.Add(Resources.DataProcessing);     // 8bpp
            ImageList.Images.Add(Resources.PeptideLib);         // 4bpp
            ImageList.Images.Add(Resources.Skyline_Release);    // 24bpp
            ImageList.Images.Add(Resources.AuditLog);           // 32bpp
            ImageList.Images.Add(Resources.CacheFile);          // 32bpp
            ImageList.Images.Add(Resources.ViewFile);           // 32bpp
        }

        public FilesTreeNode CurrentlySelectedFTN => SelectedNode as FilesTreeNode;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SrmDocument Document { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FilesTreeNode Root => (FilesTreeNode)Nodes[0];

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDuringDragAndDrop { get; internal set; }

        #region Test helpers

        public bool IsMonitoringFileSystem()
        {
            return _monitoringFileSystem;
        }

        public string PathMonitoredForFileSystemChanges()
        {
            return _fsWatcher?.Path;
        }
        #endregion

        public void ScrollToTop()
        {
            Nodes[0]?.EnsureVisible();
        }

        public void CollapseNodesInFolder<T>() where T : FileNode
        {
            var root = Folder<T>();
            foreach (TreeNode node in root.Nodes)
            {
                node.Collapse(true);
            }
        }

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

            OnTextZoomChanged(); // Required to respect non-default fonts when initialized
            OnDocumentChanged(this, new DocumentChangedEventArgs(null));
        }

        public void OnDocumentSaved(object sender, DocumentSavedEventArgs args)
        {
            if (args == null || 
                DocumentContainer.DocumentUI == null)
                return;

            var newDocumentFilePath = args.DocumentFilePath;

            if (IsMonitoringFileSystem() && args.IsSaveAs && !string.Equals(_monitoredFilePath, newDocumentFilePath, StringComparison.OrdinalIgnoreCase))
            {
                _fsWatcher.Path = Path.GetDirectoryName(newDocumentFilePath);
                _monitoredFilePath = newDocumentFilePath;
                _monitoringFileSystem = true;
            }

            UpdateTree(Document, newDocumentFilePath);
        }

        public void OnDocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            Document = DocumentContainer.DocumentUI;

            if (args == null || 
                Document == null || 
                ReferenceEquals(Document.Settings, args.DocumentPrevious?.Settings))
                return;

            UpdateTree(Document, DocumentContainer.DocumentFilePath);
        }

        internal void UpdateTree(SrmDocument document, string documentFilePath) 
        {
            try
            {
                BeginUpdateMS();

                var files = SkylineFile.Create(document, documentFilePath);

                MergeNodes(new SingletonList<FileNode>(files), Nodes, FilesTreeNode.CreateNode);

                var expandedNodes = IsAnyNodeExpanded(Root);
                if (!expandedNodes)
                {
                    Root.Expand();
                    foreach (TreeNode child in Root.Nodes)
                    {
                        child.Expand();
                    }
                }

                if (FileNode.IsDocumentSavedToDisk(documentFilePath) && !IsMonitoringFileSystem())
                {
                    _fsWatcher.Path = Path.GetDirectoryName(documentFilePath);
                    _fsWatcher.SynchronizingObject = this;
                    _fsWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
                    _fsWatcher.IncludeSubdirectories = true;
                    _fsWatcher.EnableRaisingEvents = true;
                    _fsWatcher.Renamed += FilesTree_ProjectDirectory_OnRenamed;
                    _fsWatcher.Deleted += FilesTree_ProjectDirectory_OnDeleted;
                    _fsWatcher.Created += FilesTree_ProjectDirectory_OnCreated;

                    _monitoredFilePath = documentFilePath;
                    _monitoringFileSystem = true;
                }
            }
            finally
            {
                EndUpdateMS();
            }
        }

        // CONSIDER: refactor for more code reuse with SrmTreeNode
        internal void MergeNodes(IEnumerable<FileNode> docFiles, 
                                 TreeNodeCollection treeNodes, 
                                 Func<FileNode, FilesTreeNode> createTreeNodeFunc, 
                                 bool changeAll = false)
        {
            if (docFiles == null)
                return;

            // need to look items up by index, so convert to list
            var docFilesList = docFiles.ToList();
            var localFileInitList = new List<FilesTreeNode>();

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

                    if (!nodeTree.Model.ModelEquals(nodeDoc))
                    {
                        if(nodeTree.Model.IdentityPath.Equals(nodeDoc.IdentityPath))
                        {
                            nodeTree.Model = nodeDoc;

                            // queue work to initialize model's file
                            localFileInitList.Add(nodeTree);
                        }
                        else
                        {
                            // If no usable equality, and not in the map of nodes already
                            // removed, then this loop cannot continue.
                            if(!remaining.TryGetValue(nodeDoc.IdentityPath, out nodeTree))
                                break;

                            // Found node with the same ID, so replace its doc node, if not
                            // reference equal to the one looked up.
                            if (!nodeTree.Model.ModelEquals(nodeDoc)) 
                            {
                                nodeTree.Model = nodeDoc;

                                // queue work to initialize model's file
                                localFileInitList.Add(nodeTree);
                            }
                            treeNodes.Insert(i, nodeTree);
                        }
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
            // Enumerate remaining DocNodes adding to the tree either corresponding
            // TreeNodes from the map, or creating new TreeNodes as necessary.
            for (; i < docFilesList.Count; i++)
            {
                nodeDoc = docFilesList[i];
                if (!remaining.TryGetValue(nodeDoc.IdentityPath, out var nodeTree))
                {
                    nodeTree = createTreeNodeFunc(nodeDoc);
                    nodesToInsert.Add(nodeTree);

                    localFileInitList.Add(nodeTree);
                }
                else
                {
                    nodesToInsert.Add(nodeTree);
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

            // If necessary, update the model for these new tree nodes. This needs to be done after 
            // the nodes have been added to the tree, because otherwise the icons may not update correctly.
            for (var insertNodeIndex = 0; insertNodeIndex < nodesToInsert.Count; insertNodeIndex++)
            {
                var nodeTree = (FilesTreeNode)treeNodes[insertNodeIndex + firstInsertPosition];
                var docNode = docFilesList[insertNodeIndex + firstInsertPosition];

                if (!nodeTree.Model.ModelEquals(nodeDoc))
                {
                    nodeTree.Model = docNode;

                    // queue work to initialize model's file
                    localFileInitList.Add(nodeTree);
                }
            }

            // queue tasks to initialize the local file for a node
            localFileInitList.ForEach(QueueInitLocalFile);

            // Finished merging nodes at this level. Next, recursively merge nodes with child files
            for (i = 0; i < treeNodes.Count; i++)
            {
                var treeNode = (FilesTreeNode)treeNodes[i];
                var model = treeNode?.Model;

                // Look for TreeNodes whose model represent a file group. If any are found, rely on their
                // models being up-to-date (since it was just merged with the document) and recursively
                // create / update / delete TreeNodes associated with its children
                if (model != null)
                    MergeNodes(model.Files, treeNode.Nodes, createTreeNodeFunc, changeAll);
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

        // Initialize this model's local file path in the background
        // to avoid blocking the UI thread. Finding the file could be
        // slow if the file is on a network drive / etc.
        private void QueueInitLocalFile(FilesTreeNode node)
        {
            // Short circuit if the file for this node shouldn't be initialized
            if (!node.Model.ShouldInitializeLocalFile())
                return;

            _fsWorkQueue.Add(() =>
            {
                node.InitLocalFile();

                // after initializing, update the FilesTreeNode on the UI thread
                RunUI(node.TreeView, node.UpdateState);
            });
        }

        // FileSystemWatcher notifications may reference files we should ignore.
        // For example, .tmp / .bak files created when saving .sky or .sky.view
        // files. So ignore a few file extensions.
        private static bool IgnoreFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return extension.Equals(@".tmp") || extension.Equals(@".bak");
        }

        public void FileDeleted(string fileName)
        {
            if (IgnoreFileName(fileName))
                return;

            if (FindTreeNodeForFileName(Root, fileName, out var missingFileTreeNode))
            {
                missingFileTreeNode.FileState = FileState.missing;

                RunUI(this, () => FilesTreeNode.UpdateFileImages(missingFileTreeNode));
            }
        }

        public void FileCreated(string fileName)
        {
            if (IgnoreFileName(fileName))
                return;

            // Look for a tree node associated with the new file name. If Files Tree isn't aware
            // of a file with that name, ignore the event.
            if (FindTreeNodeForFileName(Root, fileName, out var availableFileTreeNode))
            {
                availableFileTreeNode.FileState = FileState.available;

                RunUI(this, () => FilesTreeNode.UpdateFileImages(availableFileTreeNode));
            }
        }

        public void FileRenamed(string oldFileName, string newFileName)
        {
            // Look for a tree node with the file's previous name. If a node with that name is found
            // treat the file as missing.
            if (!IgnoreFileName(oldFileName) && FindTreeNodeForFileName(Root, oldFileName, out var missingFileTreeNode))
            {
                missingFileTreeNode.FileState = FileState.missing;

                RunUI(this, () => FilesTreeNode.UpdateFileImages(missingFileTreeNode));
            }
            // Now, look for a tree node with the new file name. If found, a file was restored with
            // a name Files Tree is aware of, so mark the file as available.
            else if (!IgnoreFileName(newFileName) && FindTreeNodeForFileName(Root, newFileName, out var availableFileTreeNode))
            {
                availableFileTreeNode.FileState = FileState.available;

                RunUI(this, () => FilesTreeNode.UpdateFileImages(availableFileTreeNode));
            }
        }

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
            _monitoringFileSystem = false;

            if (disposing)
            {
                _fsWatcher?.Dispose();
                _fsWatcher = null;
                _fsWorkQueue?.Dispose();
                _fsWorkQueue = null;
                _editTextBox?.Dispose();
                _editTextBox = null;
            }

            if (_fsWorkQueue != null)
            {
            }

            if (_editTextBox != null)
            {
                _editTextBox.KeyDown -= LabelEditTextBox_KeyDown;
                _editTextBox.LostFocus -= LabelEditTextBox_LostFocus;
            }

            base.Dispose(disposing);
        }

        private void FilesTree_ProjectDirectory_OnDeleted(object sender, FileSystemEventArgs e)
        {
            _fsWorkQueue.Add(() => {
                FileDeleted(e.Name);
            });
        }

        private void FilesTree_ProjectDirectory_OnCreated(object sender, FileSystemEventArgs e)
        {
            _fsWorkQueue.Add(() => {
                FileCreated(e.Name);
            });
        }

        private void FilesTree_ProjectDirectory_OnRenamed(object sender, RenamedEventArgs e)
        {
            _fsWorkQueue.Add(() => {
                FileRenamed(e.OldName, e.Name);
            });
        }

        private static bool IsAnyNodeExpanded(TreeNode node)
        {
            if (node.IsExpanded)
                return true;

            foreach (TreeNode child in node.Nodes)
            {
                if (IsAnyNodeExpanded(child))
                    return true;
            }

            return false;
        }

        // TODO: unit tests
        private static bool FindTreeNodeForFileName(TreeNode treeNode, string fileName, out FilesTreeNode value)
        {
            value = null;
            if (!(treeNode is FilesTreeNode filesTreeNode))
                return false;

            if (filesTreeNode.Model.IsBackedByFile && 
                filesTreeNode.FileName != null && 
                filesTreeNode.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                value = filesTreeNode;
                return true;
            }

            foreach (TreeNode n in filesTreeNode.Nodes)
            {
                if (FindTreeNodeForFileName(n, fileName, out value))
                    return true;
            }

            return false;
        }

        internal static void RunUI(Control control, Action action)
        {
            CommonActionUtil.SafeBeginInvoke(control, action);
        }
    }
}