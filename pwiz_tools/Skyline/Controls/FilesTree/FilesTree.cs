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
using System.Collections.Concurrent;
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
        private bool _inhibitOnAfterSelect;
        private FilesTreeNode _triggerLabelEditForNode;
        private TextBox _editTextBox;
        private string _editedLabel;

        // TODO: re-do the contract between FileSystemService and FilesTree. FSS should always exist so FilesTree doesn't need to
        //       guard calls with null checks. Perhaps FSS should use a special implementation for an un-saved document.
        /// <summary>
        /// Used to cancel any pending async work when the document changes including any
        /// lingering async tasks waiting in _fsWorkQueue or the UI event loop. This
        /// token source is re-instantiated whenever Skyline loads a new document. For example,
        /// creating a new document (File => New) or opening a different existing document (File => Open).
        /// </summary>
        private FileSystemService _fileSystemService;
        private CancellationTokenSource _cancellationTokenSource;
        private BackgroundActionService _backgroundActionService;

        public FilesTree()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _backgroundActionService = BackgroundActionService.Create(this);

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

        public bool IsFileAvailable(string filePath)
        {
            if (_fileSystemService == null)
            {
                return true;
            }
            else
            {
                return _fileSystemService.IsMonitoring && _fileSystemService.IsFileAvailable(filePath);
            }
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
            return _fileSystemService is { IsMonitoring: true };
        }

        public string PathMonitoredForFileSystemChanges()
        {
            return _fileSystemService?.DirectoryPath;
        }

        public bool IsComplete()
        {
            return _backgroundActionService.IsComplete;
        }

        #endregion

        public void ScrollToTop()
        {
            Nodes[0]?.EnsureVisible();
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

            if (args.IsSaveAs && _fileSystemService != null && !_fileSystemService.IsMonitoringDirectory(DocumentContainer.DocumentFilePath))
            {
                // Stop watching the previous directory and cancel pending async tasks
                _cancellationTokenSource.Cancel();

                _fileSystemService.StopWatching();
                _backgroundActionService.CancelAll();
                
                // Create a token source for the new document
                _cancellationTokenSource = new CancellationTokenSource();
                _backgroundActionService = BackgroundActionService.Create(this);
            }

            UpdateTree(DocumentContainer.Document, DocumentContainer.DocumentFilePath, true);
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

        internal void UpdateTree(SrmDocument document, string documentFilePath, bool documentPathChanged = false, bool changeAll = false) 
        {
            // Logging used to debug issues monitoring the local file system
            // {
            //     var versionMsg = document != null ? @$"{document.RevisionIndex}" : @"null";
            //     var nameMsg = documentFilePath != null ? $@"{Path.GetFileName(documentFilePath)}" : @"<unsaved>";
            //     Console.WriteLine($@"===== Updating document {nameMsg} from {versionMsg} to {document.RevisionIndex}. DocumentPathChanged {documentPathChanged}.");
            // }

            try
            {
                BeginUpdateMS();

                // Remove existing nodes from FilesTree if the document has changed completely
                if (changeAll)
                {
                    Nodes.Clear();
                }

                if (FileNode.IsDocumentSaved(documentFilePath) && (_fileSystemService == null || !_fileSystemService.IsMonitoring))
                {
                    // Start monitoring the directory containing the document for changes to files of interest
                    _fileSystemService = FileSystemService.Create(this, _cancellationTokenSource.Token, _backgroundActionService);
                    _fileSystemService.FileCreatedAction = FileCreated;
                    _fileSystemService.FileRenamedAction = FileRenamed;
                    _fileSystemService.FileDeletedAction = FileDeleted;

                    _fileSystemService.StartWatching(documentFilePath);
                }

                var files = SkylineFile.Create(document, documentFilePath);

                MergeNodes(new SingletonList<FileNode>(files), Nodes, FilesTreeNode.CreateNode, _cancellationTokenSource.Token);

                Root.Expand(); // Always expand the root node

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
                if (!node.Model.ShouldInitializeLocalFile())
                    return;

                _fileSystemService.LoadFile(node.LocalFilePath, node.FilePath, node.FileName, node.Model.DocumentPath, (localFilePath, isAvailable, token) =>
                {
                    node.UpdateState(localFilePath, isAvailable);
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
                _backgroundActionService.CancelAll();
                _backgroundActionService.Dispose();
                _backgroundActionService = null;
            }

            if (_fileSystemService != null)
            {
                _fileSystemService.StopWatching();
                _fileSystemService = null;
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

    // TODO: add a new background task that refreshes the state of all items in the cache periodically. For example, when Skyline gains focus
    //       after being minimized or after the user switches from a different application back to Skyline. 
    // TODO: merge RunUI and Add/ProcessTask into a new service that FilesTree can use for background processing
    // TODO: provide an implementation of FileSystemService that runs when the document is unsaved (so monitoring is not running)
    // CONSIDER: does FileSystemService work properly when a (1) a new document is created or (2) an existing document is opened and internal state is reset for the new Skyline document?
    public class FileSystemService
    {
        private static readonly IList<string> FILE_EXTENSION_IGNORE_LIST = new List<string> { @".tmp", @".bak" };

        internal static FileSystemService Create(Control synchronizingObject, CancellationToken cancellationToken, BackgroundActionService backgroundActionService)
        {
            return new FileSystemService(synchronizingObject, cancellationToken, backgroundActionService);
        }

        private FileSystemWatcher _watcher;

        private FileSystemService(Control synchronizingObject, CancellationToken cancellationToken, BackgroundActionService backgroundActionService)
        {
            SynchronizingObject = synchronizingObject;
            Cache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            CancellationToken = cancellationToken;
            BackgroundActionService = backgroundActionService;

        }

        public Action<string, CancellationToken> FileDeletedAction { get; set; }
        public Action<string, CancellationToken> FileCreatedAction { get; set; }
        public Action<string, string, CancellationToken> FileRenamedAction { get; set; }

        internal Control SynchronizingObject { get; }
        internal string DirectoryPath => _watcher?.Path;
        internal CancellationToken CancellationToken { get; }
        private BackgroundActionService BackgroundActionService { get; }
        private ConcurrentDictionary<string, bool> Cache { get; }

        internal void StartWatching(string directoryPath)
        {
            Assume.IsTrue(!IsMonitoring);

            _watcher = new FileSystemWatcher();

            _watcher.Path = Path.GetDirectoryName(directoryPath);
            _watcher.SynchronizingObject = SynchronizingObject;
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;
            _watcher.Renamed += FileSystemWatcher_OnRenamed;
            _watcher.Deleted += FileSystemWatcher_OnDeleted;
            _watcher.Created += FileSystemWatcher_OnCreated;

            IsMonitoring = true;
        }

        public void LoadFile(string localFilePath, string filePath, string fileName, string documentPath, Action<string, bool, CancellationToken> callback)
        {
            var cancellationToken = CancellationToken;

            // See if the file is already loaded into the cache - if so, queue work to update to invoke the callback with the cached file state.
            if (localFilePath != null && Cache.TryGetValue(localFilePath, out var isAvailable))
            {
                Assume.IsFalse(SynchronizingObject.InvokeRequired);

                BackgroundActionService.RunUI(() =>
                {
                    callback(localFilePath, isAvailable, cancellationToken);
                });
            }
            // Not in the cache, so (1) determine the path to the local file and (2) if found, queue work to invoke the callback with info about the local file path.
            else
            {
                BackgroundActionService.AddTask(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    localFilePath = LocateFile(filePath, fileName, documentPath);

                    if (localFilePath != null)
                    {
                        Cache[localFilePath] = true;

                        BackgroundActionService.RunUI(() =>
                        {
                            callback(localFilePath, true, cancellationToken);
                        });
                    }
                });
            }
        }

        public bool IsMonitoring { get; private set; }

        public bool IsMonitoringDirectory(string directoryPath)
        {
            return IsMonitoring && DirectoryPath.Equals(directoryPath, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsFileAvailable(string fullPath)
        {
            return Cache.ContainsKey(fullPath);
        }

        private void FileSystemWatcher_OnDeleted(object sender, FileSystemEventArgs e)
        {
            var cancellationToken = CancellationToken;

            if (IgnoreFileName(e.FullPath) || cancellationToken.IsCancellationRequested)
                return;

            Cache[e.FullPath] = false;

            BackgroundActionService.AddTask(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    FileDeletedAction(e.FullPath, cancellationToken);
                }
            });
        }

        private void FileSystemWatcher_OnCreated(object sender, FileSystemEventArgs e)
        {
            var cancellationToken = CancellationToken;

            if (IgnoreFileName(e.FullPath) || cancellationToken.IsCancellationRequested)
                return;

            Cache[e.FullPath] = true;

            BackgroundActionService.AddTask(() => 
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    FileCreatedAction(e.FullPath, cancellationToken);
                }
            });
        }

        private void FileSystemWatcher_OnRenamed(object sender, RenamedEventArgs e)
        {
            var cancellationToken = CancellationToken;

            // Ignore file names with .bak / .tmp extensions as they are temporary files created during the save process.
            //
            // N.B. it's important to only ignore rename events where the new file name's extension is on the ignore list.
            // Otherwise, files that actually exist will be marked as missing in Files Tree without a way to force those
            // nodes to reset their FileState from disk leading to much confusion.
            if (IgnoreFileName(e.FullPath) || cancellationToken.IsCancellationRequested)
                return;

            if (Cache.ContainsKey(e.OldFullPath))
            {
                Cache[e.OldFullPath] = false;
            }
            else if (Cache.ContainsKey(e.FullPath)) 
            {
                Cache[e.FullPath] = true;
            }

            BackgroundActionService.AddTask(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    FileRenamedAction(e.OldFullPath, e.FullPath, cancellationToken);
                }
            });
        }

        internal void StopWatching()
        {
            // Require the caller to cancel in-flight work before calling StopWatching
            Assume.IsTrue(CancellationToken.IsCancellationRequested);

            Cache?.Clear();

            if (_watcher != null)
            {
                _watcher.Renamed -= FileSystemWatcher_OnRenamed;
                _watcher.Deleted -= FileSystemWatcher_OnDeleted;
                _watcher.Created -= FileSystemWatcher_OnCreated;
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            FileCreatedAction = null;
            FileDeletedAction = null;
            FileRenamedAction = null;

            IsMonitoring = false;
        }

        /// Find Skyline files on the local file system.
        ///
        /// SkylineFiles uses this approach to locate file paths found in SrmSettings. It starts with
        /// the given path but those paths may be set on others machines. If not available locally, use
        /// <see cref="PathEx.FindExistingRelativeFile"/> to search for the file locally.
        ///
        // Looks for a file on the file system. Can make one or more calls to File.Exists or Directory.Exists while 
        // checking places Skyline might have stored the file. Should be called from a worker thread to avoid blocking the UI.
        // TODO: what other ways does Skyline use to find files of various types? For example, Chromatogram.GetExistingDataFilePath or other possible locations for spectral libraries
        public static string LocateFile(string filePath, string fileName, string documentPath)
        {
            string localPath;

            if (File.Exists(filePath) || Directory.Exists(filePath))
                localPath = filePath;
            else localPath = PathEx.FindExistingRelativeFile(documentPath, fileName);

            return localPath;
        }

        // FileSystemWatcher events may reference files we should ignore. For example, .tmp or .bak files
        // created when saving a Skyline document or view file. So, check paths against an ignore list.
        public static bool IgnoreFileName(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return true;

            var extension = Path.GetExtension(filePath);

            return FILE_EXTENSION_IGNORE_LIST.Contains(extension);
        }
    }
}