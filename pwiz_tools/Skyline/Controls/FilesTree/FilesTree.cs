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

// ReSharper disable WrongIndentSize
namespace pwiz.Skyline.Controls.FilesTree
{
    public class FilesTree : TreeViewMS
    {
        public static string FILES_TREE_SHOWN_ONCE_TOKEN = @"FilesTreeShownOnce";

        // Fields for editing the label of a FilesTreeNode
        private bool _inhibitOnAfterSelect;
        private FilesTreeNode _triggerLabelEditForNode;
        private TextBox _editTextBox;
        private string _editedLabel;

        /// <summary>
        /// Used to cancel any pending async work when the document changes including any
        /// lingering async tasks waiting in _fsWorkQueue or the UI event loop. This
        /// token source is re-instantiated when Skyline loads a new document. For example,
        /// creating a new document (File => New) or opening a different existing document (File => Open).
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Timer for debouncing document change events. During rapid changes (like importing
        /// many files), this coalesces multiple updates into a single tree refresh after
        /// a quiet period, similar to the graph update pattern in SkylineGraphs.
        /// </summary>
        private System.Windows.Forms.Timer _timerUpdate;
        private bool _pendingChangeAll;
        private bool _skipDebounce;
        private const int UPDATE_DELAY_MS = 100;

        public FilesTree()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            BackgroundActionService = new BackgroundActionService(this);

            FileSystemService = new FileSystemService(this, BackgroundActionService);
            FileSystemService.FileDeletedAction += FileDeleted;
            FileSystemService.FileCreatedAction += FileCreated;
            FileSystemService.FileRenamedAction += FileRenamed;

            // Timer for debouncing document changes during rapid updates (e.g., importing many files)
            _timerUpdate = new System.Windows.Forms.Timer { Interval = UPDATE_DELAY_MS };
            _timerUpdate.Tick += OnUpdateTimer;

            // Icons size is 16x16
            ImageList = new ImageList
            {
                TransparentColor = Color.Magenta,
                ColorDepth = ColorDepth.Depth32Bit 
            };

            ImageList.Images.Add(Resources.Blank);              // 1bpp
            ImageList.Images.Add(Resources.Folder);             // 32bpp
            ImageList.Images.Add(Resources.FolderMissing);      // 32bpp
            ImageList.Images.Add(Resources.File);               // 8bpp
            ImageList.Images.Add(Resources.FileMissing);        // 32bpp
            ImageList.Images.Add(Resources.Replicate);          // 24bpp
            ImageList.Images.Add(Resources.ReplicateMissing);   // 24bpp // CONSIDER: improve icon?
            ImageList.Images.Add(Resources.DataProcessing);     // 8bpp
            ImageList.Images.Add(Resources.PeptideLib);         // 4bpp
            ImageList.Images.Add(Resources.Skyline_FilesTree);  // 24bpp
            ImageList.Images.Add(Resources.AuditLog);           // 32bpp
            ImageList.Images.Add(Resources.CacheFile);          // 32bpp
            ImageList.Images.Add(Resources.ViewFile);           // 32bpp
            ImageList.Images.Add(Resources.ProtDB);             // 32bpp
            ImageList.Images.Add(Resources.ImsDB);              // 32bpp
            ImageList.Images.Add(Resources.OptDB);              // 32bpp
            ImageList.Images.Add(Resources.IrtCalculator);      // 32bpp
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
        public FileSystemService FileSystemService { get; }
        public FileSystemType FileSystemType() => FileSystemService.FileSystemType;
        public bool IsMonitoringDirectory(string directoryPath) => FileSystemService.IsMonitoringDirectory(directoryPath);
        public IList<string> MonitoredDirectories() => FileSystemService.MonitoredDirectories();

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        private BackgroundActionService BackgroundActionService { get; }
        public bool IsComplete() => !_timerUpdate.Enabled && BackgroundActionService.IsComplete;

        /// <summary>
        /// Call before modifying the document from within FilesTree/FilesTreeForm to skip
        /// debouncing for that update. This ensures immediate UI feedback for user-initiated
        /// actions like drag-drop, delete, or rename.
        /// </summary>
        public void SkipNextDebounce()
        {
            _skipDebounce = true;
        }

        /// <summary>
        /// Returns true if file system watching is completely shut down.
        /// Useful for tests to wait for FileSystemWatchers to be fully disposed before cleanup.
        /// </summary>
        public bool IsFileSystemWatchingComplete() => FileSystemService.IsFileSystemWatchingComplete();

        #endregion

        /// <summary>
        /// Get the first folder associated with type <see cref="T"/>.
        /// </summary>
        /// <typeparam name="T">The model type</typeparam>
        /// <returns></returns>
        // CONSIDER: does the model need a way to distinguish "folders" from other node types? Ex: with a marker interface?
        public FilesTreeNode RootChild<T>() where T : FileModel
        {
            return Root.Model is T ? 
                Root : 
                Root.Nodes.Cast<FilesTreeNode>().FirstOrDefault(filesTreeNode => filesTreeNode.Model is T);
        }

        /// <summary>
        /// Selects a node without triggering the event <see cref="TreeView.AfterSelect"/>. Used to re-select nodes after drag-and-drop.
        /// </summary>
        /// <param name="node">Node to select.</param>
        internal void SelectNodeWithoutResettingSelection(FilesTreeNode node)
        {
            _inhibitOnAfterSelect = true;
            SelectedNode = node;
            _inhibitOnAfterSelect = false;
        }

        public void InitializeTree(IDocumentUIContainer documentUIContainer)
        {
            DocumentContainer = documentUIContainer;

            // Force handle creation to ensure tree is populated before view state restoration.
            // RestoreExpansionAndSelection is called from CreateFilesTreeForm immediately after
            // FilesTreeForm construction, and requires nodes to exist.
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

            HandleDocumentEvent(isSaveAs:args.IsSaveAs);
        }

        public void OnDocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            Assume.IsNotNull(DocumentContainer);

            if (args == null || DocumentContainer.Document == null)
                return;

            // Short-circuit if SrmSettings has no changes
            if (ReferenceEquals(DocumentContainer.Document.Settings, args.DocumentPrevious?.Settings))
                return;

            // Handle wholesale replacement of the previous document with a new one. Example scenario:
            // document foo.sky is open and user either creates a new (empty) document or opens a different document (bar.sky).
            var changeAll = args.DocumentPrevious != null && !ReferenceEquals(args.DocumentPrevious.Id, DocumentContainer.Document.Id);

            // Initial call from InitializeTree passes null for DocumentPrevious - handle immediately
            // Also skip debouncing if FilesTree itself initiated the document change (e.g., drag-drop, delete, rename)
            if (args.DocumentPrevious == null || _skipDebounce)
            {
                _skipDebounce = false;
                HandleDocumentEvent(isSaveAs: false, changeAll);
                return;
            }

            // Debounce subsequent document changes to avoid excessive updates during rapid changes
            // (e.g., importing many files). Track if any pending change requires a full refresh.
            _pendingChangeAll = _pendingChangeAll || changeAll;

            // Restart the timer - this coalesces rapid changes into a single update
            _timerUpdate.Stop();
            _timerUpdate.Start();
        }

        private void OnUpdateTimer(object sender, EventArgs e)
        {
            // Stop the timer immediately to prevent re-entry
            _timerUpdate.Stop();

            // Process the pending document change
            var changeAll = _pendingChangeAll;
            _pendingChangeAll = false;

            HandleDocumentEvent(isSaveAs: false, changeAll);
        }

        private void HandleDocumentEvent(bool isSaveAs = false, bool changeAll = false)
        {
            var document = DocumentContainer.Document;
            var documentFilePath = DocumentContainer.DocumentFilePath;

            // Stop watching if the document path changed (Save As) or if we're currently watching a different directory
            var documentDirectory = Path.GetDirectoryName(documentFilePath);
            var documentPathChanged = isSaveAs || (documentDirectory != null && !FileSystemService.IsMonitoringDirectory(documentDirectory));
            
            if (documentPathChanged && FileSystemService.IsWatching())
            {
                // Stop the out-of-date monitoring service, first triggering the CancellationToken
                _cancellationTokenSource.Cancel();
                FileSystemService.StopWatching();
            }

            BeginUpdateMS();

            var savedTopNodeId = ((FilesTreeNode)TopNode)?.Model.IdentityPath;
            var savedSelectedNodeId = ((FilesTreeNode)SelectedNode)?.Model.IdentityPath;
            try
            {
                // Create a new CancellationToken if we stopped watching or if we're not watching yet
                if (!FileSystemService.IsWatching())
                {
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = new CancellationTokenSource();
                }
                var cancellationToken = _cancellationTokenSource.Token;

                // Remove existing nodes from FilesTree if the document has changed completely
                if (changeAll)
                {
                    Nodes.Clear();
                }

                var files = SkylineFile.Create(document, documentFilePath).AsList();

                // Only start watching if there are files with actual file paths to watch.
                // Files like audit logs may have null FilePath and don't need file system watching.
                // IMPORTANT: StartWatching() must be called BEFORE MergeNodes() because MergeNodes()
                // calls LoadFile() which calls WatchDirectory(), and WatchDirectory() requires the
                // FileSystemService to have a LocalFileSystemService delegate (not NoOpService).
                var filesWithPaths = files.Count(f => !string.IsNullOrEmpty(f.FilePath));
                if (filesWithPaths > 0 && !FileSystemService.IsWatching())
                {
                    FileSystemService.StartWatching(cancellationToken);
                }
                else if (filesWithPaths == 0 && FileSystemService.IsWatching())
                {
                    // Stop watching if document has no files with paths
                    _cancellationTokenSource.Cancel();
                    FileSystemService.StopWatching();
                }

                MergeNodes(files, Nodes, cancellationToken);

                BackgroundActionService.RunUI(() => 
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        UpdateNodeStates();
                    }
                });

                // Reset selected node and top node which could have changed while merging nodes
                var foundNode = FindNodeByIdentityPath(savedSelectedNodeId);
                if (foundNode != null)
                {
                    SelectedNode = foundNode;
                }
            }
            finally
            {
                EndUpdateMS();

                // Root should always be expanded. Do this outside BeginUpdate / EndUpdate.
                if (Nodes.Count > 0 && !Nodes[0].IsExpanded)
                {
                    Root.Expand();
                }

                // Set TopNode *after* the BeginUpdateMS / EndUpdateMS block. Otherwise, TreeView will not correctly set TopNode.
                // Only try to restore TopNode if the tree has nodes (MergeNodes may result in empty tree if document is invalid)
                if (Nodes.Count > 0)
                {
                    var foundNode = FindNodeByIdentityPath(savedTopNodeId);
                    if (foundNode != null)
                    {
                        foundNode.EnsureVisible();
                        TopNode = foundNode;
                    }
                }
            }
        }

        // CONSIDER: refactor for more code reuse with SrmTreeNode
        internal void MergeNodes(IList<FileModel> docFilesList, TreeNodeCollection treeNodes, CancellationToken cancellationToken)
        {
            FileModel nodeDoc = null;

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
                    nodeTree = FilesTreeNode.CreateNode(nodeDoc);

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
                    MergeNodes(model.Files, treeNode.Nodes, cancellationToken);
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

                FileSystemService.LoadFile(node.LocalFilePath, node.FilePath, node.FileName, node.Model.DocumentPath, (localFilePath, token) =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        node.UpdateState(localFilePath);
                    }
                });
            }
        }

        internal void UpdateNodeStates()
        {
            BeginUpdateMS();
            UpdateNodeStates(Root);
            EndUpdateMS();
        }


        #region Edit tree node labels

        protected override void OnAfterSelect(TreeViewEventArgs e)
        {
            if (!_inhibitOnAfterSelect)
                base.OnAfterSelect(e);
        }

        [Browsable(false)]
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
                if (node != null && node == GetNodeAt(0, e.Y) && node.SupportsRename())
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

            // Put focus back on the tree after label edit is complete
            Focus();
        }

        #endregion

        #region Handlers for events raised while monitoring the local file system

        public void FileDeleted(string filePath, CancellationToken cancellationToken)
        {
            var matchingNodes = FindNodesByFilePath(filePath);
            if (matchingNodes.Count > 0)
            {
                foreach (var node in matchingNodes)
                {
                    node.FileState = FileState.missing;

                    BackgroundActionService.RunUI(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            FilesTreeNode.UpdateImagesForTreeNode(node);
                        }
                    });
                }
            }
        }

        public void FileCreated(string filePath, CancellationToken cancellationToken)
        {
            // Look for a tree node associated with the new file name. If Files Tree isn't aware
            // of a file with that name, ignore the event.
            var matchingNodes = FindNodesByFilePath(filePath);
            if (matchingNodes.Count > 0)
            {
                foreach (var node in matchingNodes)
                {
                    node.FileState = FileState.available;

                    BackgroundActionService.RunUI(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            FilesTreeNode.UpdateImagesForTreeNode(node);
                        }
                    });
                }
            }
        }

        public void FileRenamed(string oldFilePath, string newFilePath, CancellationToken cancellationToken)
        {
            // Look for a tree node with the file's previous name. If a node with that name is found
            // treat the file as missing.
            var matchingNodes = FindNodesByFilePath(oldFilePath);
            if (matchingNodes.Count > 0)
            {
                foreach (var node in matchingNodes)
                {
                    node.FileState = FileState.missing;

                    BackgroundActionService.RunUI(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            FilesTreeNode.UpdateImagesForTreeNode(node);
                        }
                    });
                }
            }

            // Now, look for a tree node with the new file name. If found, a file was restored with
            // a name Files Tree is aware of, so mark the file as available.
            matchingNodes = FindNodesByFilePath(newFilePath);
            if (matchingNodes.Count > 0)
            {
                foreach (var node in matchingNodes)
                {
                    node.FileState = FileState.available;

                    BackgroundActionService.RunUI(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            FilesTreeNode.UpdateImagesForTreeNode(node);
                        }
                    });
                }
            }
        }

        #endregion

        #region Enable and disable cut, copy, paste, delete when gaining or losing focus
        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            ClipboardControlGotLostFocus(true);
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            ClipboardControlGotLostFocus(false);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            ClipboardControlGotLostFocus(false);
        }

        protected void ClipboardControlGotLostFocus(bool gettingFocus)
        {
            var skylineWindow = FindParentSkylineWindow(this);
            if (skylineWindow != null)
            {
                if (gettingFocus)
                {
                    skylineWindow.ClipboardControlGotFocus(this);
                }
                else
                {
                    skylineWindow.ClipboardControlLostFocus(this);
                }
            }
        }

        #endregion   

        protected override bool IsParentNode(TreeNode node)
        {
            return node.Nodes.Count != 0;
        }

        protected override int EnsureChildren(TreeNode node)
        {
            return node != null ? node.Nodes.Count : 0;
        }

        protected override void Dispose(bool disposing)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            if (_timerUpdate != null)
            {
                _timerUpdate.Stop();
                _timerUpdate.Tick -= OnUpdateTimer;
                _timerUpdate.Dispose();
                _timerUpdate = null;
            }

            if (BackgroundActionService != null)
            {
                BackgroundActionService.Shutdown();
                BackgroundActionService.Dispose();
            }

            if (FileSystemService != null)
            {
                FileSystemService.StopWatching();
                FileSystemService.Dispose();
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

        public IList<FilesTreeNode> FindNodesByFilePath(string targetPath)
        {
            var normalizedTargetPath = FileSystemUtil.Normalize(targetPath);

            var matchingNodes = new List<FilesTreeNode>();
            Traverse(Root, filesTreeNode =>
            {
                if (filesTreeNode.Model.IsBackedByFile && filesTreeNode.LocalFilePath != null)
                {
                    var normalizedCurrentPath = FileSystemUtil.Normalize(filesTreeNode.LocalFilePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    // Check for exact match - if found, this is the target node
                    if (FileSystemUtil.PathEquals(normalizedCurrentPath, normalizedTargetPath))
                    {
                        matchingNodes.Add(filesTreeNode);
                    }
                    // Check for partial match - if found, this node was in a directory whose name changed
                    else if (FileSystemUtil.IsFileInDirectory(normalizedTargetPath, normalizedCurrentPath))
                    {
                        matchingNodes.Add(filesTreeNode);
                    }
                }

                return true; // continue traversal
            });
            
            return matchingNodes;
        }

        private FilesTreeNode FindNodeByIdentityPath(IdentityPath identityPath)
        {
            if (identityPath == null)
            {
                return null;
            }
        
            FilesTreeNode result = null;
        
            Traverse(Root, filesTreeNode =>
            {
                if (filesTreeNode.Model.IdentityPath.Equals(identityPath))
                {
                    result = filesTreeNode;
                    return false; // found a match, stop traversal
                }
        
                return true; // continue traversal
            });
        
            return result;
        }

        /// <summary>
        /// Update UI of all FilesTree nodes. Traverses bottom-up so upper nodes can process the state of lower nodes, especially
        /// so changes to a node's <see cref="FileState"/> appear in the UI. This happens on the UI thread and should not access
        /// the file system.
        /// </summary>
        /// <param name="node">Node to update</param>
        private void UpdateNodeStates(FilesTreeNode node)
        {
            Traverse(Root, filesTreeNode =>
            {
                filesTreeNode.UpdateState();
                return true; // continue traversal
            }, true);
        }

        /// <summary>
        /// Traverse the tree performing <see cref="visit"/> on each node until (1) all nodes are visited or (2) traversal was stopped early when visiting a node.
        /// </summary>
        /// <param name="currentNode">The current node. Typically starts with the tree's root.</param>
        /// <param name="visit">Function to call on each node, passing the current node as a parameter. Return false to continue traversal and true to stop traversal.</param>
        /// <param name="postOrder">Flag indicating whether to perform call <see cref="visit"/> before traversing child nodes or after. Defaults to pre-order.</param>
        /// <returns></returns>
        internal bool Traverse(FilesTreeNode currentNode, Func<FilesTreeNode, bool> visit, bool postOrder = false)
        {
            if (currentNode == null)
                return true;

            if (!postOrder && !visit(currentNode))
            {
                return false;
            }

            foreach (FilesTreeNode childNode in currentNode.Nodes)
            {
                if (!Traverse(childNode, visit, postOrder))
                {
                    return false;
                }
            }

            if (postOrder && !visit(currentNode))
            {
                return false;
            }

            return true;
        }

        // CONSIDER: does SkylineWindow already have a way to do this? If not, should it?
        private static SkylineWindow FindParentSkylineWindow(Control me)
        {
            for (var control = me; control != null; control = control.Parent)
            {
                if (control is SkylineWindow skylineWindow)
                {
                    return skylineWindow;
                }
            }
            return null;
        }
    }
}
