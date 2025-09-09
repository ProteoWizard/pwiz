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
        private static readonly IList<string> FILE_EXTENSION_IGNORE_LIST = new List<string> { @".tmp", @".bak" };

        private bool _inhibitOnAfterSelect;
        private bool _monitoringFileSystem;
        private string _monitoredFilePath;
        private int _pendingActionCount;
        private FileSystemWatcher _fsWatcher;
        private QueueWorker<Action> _fsWorkQueue;
        private FilesTreeNode _triggerLabelEditForNode;
        private TextBox _editTextBox;
        private string _editedLabel;
        private readonly MemoryDocumentContainer _modelDocumentContainer;

        /// <summary>
        /// Used to cancel any pending async work when the document changes including any
        /// lingering async tasks waiting in _fsWorkQueue or the UI event loop. This
        /// token source is re-instantiated whenever Skyline loads a new document. For example,
        /// creating a new document (File => New) or opening a different existing document (File => Open).
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        public FilesTree()
        {
            _fsWorkQueue = new QueueWorker<Action>(null, ProcessTask);
            _modelDocumentContainer = new MemoryDocumentContainer();
            _cancellationTokenSource = new CancellationTokenSource();

            _monitoringFileSystem = false;
            _monitoredFilePath = null;
            _fsWatcher = new FileSystemWatcher();

            // Set to zero to handle work items synchronously
            var threadCount = ParallelEx.GetThreadCount();
            _fsWorkQueue.RunAsync(threadCount, @"FilesTree: queue for monitoring the file system");

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

            OnTextZoomChanged(); // Required to respect non-default fonts when initialized
            OnDocumentChanged(this, new DocumentChangedEventArgs(null));
        }

        public void OnDocumentSaved(object sender, DocumentSavedEventArgs args)
        {
            Assume.IsNotNull(DocumentContainer);

            if (args == null || DocumentContainer.Document == null)
                return;

            // CONSIDER: use DocumentContainer.DocumentFilePath?
            var newDocumentFilePath = args.DocumentFilePath;

            if (IsMonitoringFileSystem() && args.IsSaveAs && !string.Equals(_monitoredFilePath, newDocumentFilePath, StringComparison.OrdinalIgnoreCase))
            {
                _fsWatcher.Path = Path.GetDirectoryName(newDocumentFilePath);
                _monitoredFilePath = newDocumentFilePath;
                _monitoringFileSystem = true;
            }

            UpdateTree(DocumentContainer.Document, newDocumentFilePath, true);
        }

        public void OnDocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            Assume.IsNotNull(DocumentContainer);

            if (args == null || DocumentContainer.Document == null)
                return;

            // Check SrmSettings. If it didn't change, short-circuit since there's nothing to do
            if (ReferenceEquals(DocumentContainer.Document.Settings, args.DocumentPrevious?.Settings))
                return;

            // Handle wholesale replacement of the previous document with a new one. Example scenario:
            // document foo.sky is open and user either creates a new (empty) document or opens a different document (bar.sky).
            var changeAll = args.DocumentPrevious != null && !ReferenceEquals(args.DocumentPrevious.Id, DocumentContainer.Document.Id);

            UpdateTree(DocumentContainer.Document, DocumentContainer.DocumentFilePath, false, changeAll);
        }

        // CONSIDER: re-work the parameter list, perhaps relying on DocumentContainer and not passing a series of bool flags
        internal void UpdateTree(SrmDocument document, string documentFilePath, bool documentPathChanged = false, bool changeAll = false) 
        {
            try
            {
                BeginUpdateMS();

                // Do this work inside the Begin/EndUpdate calls to avoid flickering the tree
                if (changeAll)
                {
                    Nodes.Clear();                     // Remove existing nodes from FilesTree
                    _fsWorkQueue.Clear();              // Clear pending tasks from the QueueWorker
                    _cancellationTokenSource.Cancel(); // Cancel any in-flight tasks from QueWorker and the UI event loop (control.BeginInvoke).

                    _cancellationTokenSource = new CancellationTokenSource(); // Create a token source for the new document
                }

                // Output useful for debugging async file monitoring issues
                // {
                //     var versionMsg = originalDocument != null ? @$"{originalDocument.RevisionIndex}" : @"null";
                //     var nameMsg = documentFilePath != null ? $@"{Path.GetFileName(documentFilePath)}" : @"<unsaved>";
                //     Console.WriteLine($@"===== Updating document {nameMsg} from {versionMsg} to {document.RevisionIndex}. DocumentPathChanged {documentPathChanged}.");
                // }

                var originalDocument = _modelDocumentContainer.Document;
                _modelDocumentContainer.DocumentFilePath = documentFilePath;
                _modelDocumentContainer.SetDocument(document, originalDocument);

                var files = SkylineFile.Create(document, documentFilePath);

                MergeNodes(new SingletonList<FileNode>(files), Nodes, FilesTreeNode.CreateNode, _cancellationTokenSource.Token, documentPathChanged);

                Root.Expand(); // Root is always expanded

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

                // CONSIDER: simplify so this is less error-prone
                RunUI(this, _cancellationTokenSource.Token, () => 
                {
                    Root.RefreshState();
                });
            }
            finally
            {
                EndUpdateMS();
            }
        }

        // CONSIDER: refactor for more code reuse with SrmTreeNode
        internal void MergeNodes(IList<FileNode> docFiles, TreeNodeCollection treeNodes, Func<FileNode, FilesTreeNode> createTreeNodeFunc, CancellationToken cancellationToken, bool documentPathChanged)
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

                    // If IDs, match replace the model.
                    if (nodeTree.Model.IdentityPath.Equals(nodeDoc.IdentityPath))
                    {
                        nodeTree.Model = nodeDoc;
                        localFileInitList.Add(nodeTree);
                    }
                    else
                    {
                        // If no usable equality, and not in the map of nodes already
                        // removed, then this loop cannot continue.
                        if (!remaining.TryGetValue(nodeDoc.IdentityPath, out nodeTree))
                            break;

                        // Found a match in the map of removed nodes so update its model and re-insert it
                        nodeTree.Model = nodeDoc;
                        localFileInitList.Add(nodeTree);

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
                else {
                    nodeTree = createTreeNodeFunc(nodeDoc);

                    nodesToInsert.Add(nodeTree);
                    localFileInitList.Add(nodeTree);
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

            // Queue tasks to initialize the local file for a FilesTreeNode that (1) was created or (2) had its model
            // replaced during this round of merging FilesTree with an SrmDocument.
            //
            // This uses a work queue to avoid blocking the UI thread since finding files could be slow, especially
            // if the file is on a network drive.
            foreach (var node in localFileInitList.Where(node => node.Model.ShouldInitializeLocalFile()))
            {
                AddTask(() =>
                {
                    if(cancellationToken.IsCancellationRequested)
                        return;

                    node.InitializeLocalFile();

                    // Return to the UI thread and update the FilesTreeNode with any additional
                    // info about the state of the local file
                    RunUI(node.TreeView, cancellationToken, () =>
                    {
                        node.UpdateState();
                    });
                });
            }

            // Recursively merge any nested files
            for (i = 0; i < treeNodes.Count; i++)
            {
                var treeNode = (FilesTreeNode)treeNodes[i];
                var model = treeNode?.Model;

                if (model != null && model.Files.Count > 0)
                {
                    MergeNodes(model.Files, treeNode.Nodes, createTreeNodeFunc, cancellationToken, documentPathChanged);
                }
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

        #region FileSystemWatcher Event Handlers

        public void FileDeleted(string fileName)
        {
            if (IgnoreFileName(fileName))
                return;

            if (FindTreeNodeForFileName(Root, fileName, out var missingFileTreeNode))
            {
                missingFileTreeNode.FileState = FileState.missing;

                RunUI(this, _cancellationTokenSource.Token, () =>
                {
                    FilesTreeNode.UpdateFileImages(missingFileTreeNode);
                });
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

                RunUI(this, _cancellationTokenSource.Token, () =>
                {
                    FilesTreeNode.UpdateFileImages(availableFileTreeNode);

                });
            }
        }

        public void FileRenamed(string oldName, string newName)
        {
            // Ignore file names with .bak / .tmp extensions as they are temporary files created during the save process.
            // N.B. it's important to only ignore rename events where the new file name's extension is on the ignore list.
            // Otherwise, files that actually exist will be marked as missing in Files Tree without a way to force those
            // nodes to reset their FileState from disk leading to much confusion.
            if (IgnoreFileName(newName))
                return;

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

            RunUI(this, _cancellationTokenSource.Token, () =>
            {
                FilesTreeNode.UpdateFileImages(treeNodeToUpdate);
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
            _monitoringFileSystem = false;

            // CONSIDER: should this happen last?
            if (disposing)
            {
                _fsWatcher?.Dispose();
                _fsWatcher = null;
                _fsWorkQueue?.Dispose();
                _fsWorkQueue = null;
                _editTextBox?.Dispose();
                _editTextBox = null;
            }

            if (_editTextBox != null)
            {
                _editTextBox.KeyDown -= LabelEditTextBox_KeyDown;
                _editTextBox.LostFocus -= LabelEditTextBox_LostFocus;
            }

            base.Dispose(disposing);
        }

        // TODO: reconsider whether to propagate the cancellation token to the QueueWorker, especially whether to
        //       wrap it in a container with a specific CancellationToken taken from FilesTree's CancellationTokenSource.
        private void FilesTree_ProjectDirectory_OnDeleted(object sender, FileSystemEventArgs e)
        {
            AddTask(() =>
            {
                FileDeleted(e.Name);
            });
        }

        private void FilesTree_ProjectDirectory_OnCreated(object sender, FileSystemEventArgs e)
        {
            AddTask(() =>
            {
                FileCreated(e.Name);
            });
        }

        private void FilesTree_ProjectDirectory_OnRenamed(object sender, RenamedEventArgs e)
        {
            AddTask(() =>
            {
                FileRenamed(e.OldName, e.Name);
            });
        }

        // FileSystemWatcher notifications may reference files we should ignore.
        // For example, .tmp / .bak files created when saving .sky or .sky.view
        // files.
        public static bool IgnoreFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return true;

            var extension = Path.GetExtension(fileName);

            return FILE_EXTENSION_IGNORE_LIST.Contains(extension);
        }

        // TODO: unit tests
        // CONSIDER: improve performance by caching a map of file names to FilesTreeNodes, especially to improve
        //           performance of FileRenamed which may need to find two nodes.
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

        private void RunUI(Control control, CancellationToken cancellationToken, Action action)
        {
            Interlocked.Increment(ref _pendingActionCount);

            CommonActionUtil.SafeBeginInvoke(control, () =>
            {
                try
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        action();
                    }
                }
                finally
                {
                    var result = Interlocked.Decrement(ref _pendingActionCount);
                    Assume.IsTrue(result >= 0);
                }
            });
        }

        public bool IsComplete()
        {
            return _pendingActionCount == 0;
        }

        private void AddTask(Action action)
        {
            Interlocked.Increment(ref _pendingActionCount);
            _fsWorkQueue.Add(action);
        }

        private void ProcessTask(Action action, int threadIndex)
        {
            try
            {
                action();
            }
            finally
            {
                var result = Interlocked.Decrement(ref _pendingActionCount);
                Assume.IsTrue(result >= 0);
            }
        }
    }
}