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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

// TODO: initialize FileSystemWatcher when a new SkylineDocument is saved
// ReSharper disable WrongIndentSize
namespace pwiz.Skyline.Controls.FilesTree
{
    public class FilesTree : TreeViewMS
    {
        private FileSystemWatcher _fileSystemWatcher;

        public FilesTree()
        {
            _fileSystemWatcher = new FileSystemWatcher();

            ImageList = new ImageList
            {
                TransparentColor = Color.Magenta,
                ColorDepth = ColorDepth.Depth24Bit
            };

            ImageList.Images.Add(Resources.Blank);
            ImageList.Images.Add(Resources.Folder);
            ImageList.Images.Add(Resources.File);
            ImageList.Images.Add(Resources.MissingFile);
            ImageList.Images.Add(Resources.Replicate);
            ImageList.Images.Add(Resources.DataProcessing);
            ImageList.Images.Add(Resources.Peptide);
            ImageList.Images.Add(Resources.Skyline);
            ImageList.Images.Add(Resources.AuditLog);
            ImageList.Images.Add(Resources.CacheFile);
            ImageList.Images.Add(Resources.ViewFile);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SrmDocument Document { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FilesTreeNode Root { get; private set; }

        #region Testing helpers

        // Used for testing
        public string RootNodeText()
        {
            return Root.Text;
        }

        // Used for testing
        public bool IsMonitoringFileSystem()
        {
            return _fileSystemWatcher?.Path != string.Empty;
        }

        // Used for testing
        public string PathMonitoredForFileSystemChanges()
        {
            return _fileSystemWatcher?.Path;
        }

        // Used for testing
        public void ScrollToFolder<T>() where T : FileNode
        {
            Folder<T>().EnsureVisible();
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

        public FilesTreeNode Folder<T>() where T : FileNode
        {
            foreach (TreeNode treeNode in Root.Nodes)
            {
                var filesTreeNode = (FilesTreeNode)treeNode;
                if (filesTreeNode.Model.GetType() == typeof(T))
                    return filesTreeNode;
            }
            
            return null;
        }

        public void InitializeTree(IDocumentUIContainer documentUIContainer)
        {
            DocumentContainer = documentUIContainer;

            DoubleBuffered = true;

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            OnDocumentChanged(this, new DocumentChangedEventArgs(null));
        }

        public void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            var document = DocumentContainer.DocumentUI;

            if (document == null || e == null || ReferenceEquals(document.Settings, e.DocumentPrevious?.Settings))
                return;

            Document = document;

            try
            {
                BeginUpdateMS();

                var files = new RootFileNode(document, DocumentContainer.DocumentFilePath);

                MergeNodes(new SingletonList<FileNode>(files), Nodes, FilesTreeNode.CreateNode);

                Root = (FilesTreeNode)Nodes[0]; // Set root node

                var expandedNodes = IsAnyNodeExpanded(Root);
                if (!expandedNodes)
                {
                    Root.Expand();
                    foreach (TreeNode child in Root.Nodes)
                    {
                        child.Expand();
                    }
                }

                CommonActionUtil.SafeBeginInvoke(this, () => { 

                    // Start the file system monitor if (1) not already running and (2) a Skyline document
                    // is open and saved to disk.
                    if (!IsMonitoringFileSystem() && DocumentContainer.DocumentFilePath != null)
                    {
                        _fileSystemWatcher.Path = Path.GetDirectoryName(DocumentContainer.DocumentFilePath);
                        _fileSystemWatcher.SynchronizingObject = this;
                        _fileSystemWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
                        _fileSystemWatcher.IncludeSubdirectories = true;
                        _fileSystemWatcher.EnableRaisingEvents = true;
                        _fileSystemWatcher.Renamed += FilesTree_ProjectDirectory_OnRenamed;
                        _fileSystemWatcher.Deleted += FilesTree_ProjectDirectory_OnDeleted;
                        _fileSystemWatcher.Created += FilesTree_ProjectDirectory_OnCreated;
                    }
                });
            }
            finally
            {
                EndUpdateMS();
            }
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

        // CONSIDER: skip running MergeNodes on child nodes if the parent's model didn't change
        // CONSIDER: does FilesTree need to support selection like in SequenceTree / SrmTreeNode?
        // CONSIDER: refactor for more code reuse with SrmTreeNode
        internal static void MergeNodes(IEnumerable<FileNode> docFiles, TreeNodeCollection treeNodes, Func<FileNode, FilesTreeNode> createTreeNodeFunc)
        {
            if (docFiles == null)
                return;

            // need to look items up by index, so convert to list
            var docFilesList = docFiles.ToList();

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
                    var nodeTree = treeNodes[i] as FilesTreeNode;
                    if (nodeTree == null)
                        break;
                    if (!ReferenceEquals(nodeTree.Model.Immutable, nodeDoc.Immutable))
                    {
                        if(nodeTree.Model.IdentityPath.Equals(nodeDoc.IdentityPath))
                        {
                            nodeTree.Model = nodeDoc;
                        }
                        else
                        {
                            // If no usable equality, and not in the map of nodes already
                            // removed, then this loop cannot continue.
                            
                            if(!remaining.TryGetValue(nodeDoc.IdentityPath, out nodeTree))
                                break;

                            // Found node with the same ID, so replace its doc node, if not
                            // reference equal to the one looked up.
                            if (!ReferenceEquals(nodeTree.Model.Immutable, nodeDoc.Immutable))
                            {
                                nodeTree.Model = nodeDoc;
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
                for (int iRemove = i; iRemove < treeNodes.Count; iRemove++)
                {
                    FilesTreeNode nodeTree = treeNodes[iRemove] as FilesTreeNode;
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
                FilesTreeNode nodeTree;
                if (!remaining.TryGetValue(nodeDoc.IdentityPath, out nodeTree))
                {
                    nodeTree = createTreeNodeFunc(nodeDoc);

                    nodesToInsert.Add(nodeTree);
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

                if (!ReferenceEquals(docNode.Immutable, nodeTree.Model.Immutable))
                {
                    nodeTree.Model = docNode;
                }
            }

            // finished merging latest data model for these nodes. Now, recursively update any nodes whose model has children
            for (i = 0; i < treeNodes.Count; i++)
            {
                var treeNode = treeNodes[i] as FilesTreeNode;
                var model = treeNode?.Model;

                // Look for TreeNodes whose model represent a file group. If any are found, rely on their
                // models being up-to-date (since it was just merged with the document) and recursively
                // create / update / delete TreeNodes associated with its children
                if (model != null && model.HasFiles())
                    MergeNodes(model.Files, treeNode.Nodes, createTreeNodeFunc);
            }
        }

        public void FileDeleted(string fileName)
        {
            // file system notifications might reference files FilesTree should
            // ignore - for example: *.tmp files created when saving the .sky or
            // .sky.view files so ignore updates not related to a node in the tree
            if (FindTreeNodeForFileName(Root, fileName, out var missingFileTreeNode))
            {
                missingFileTreeNode.Model.FileState = FileState.missing;

                ExecuteOnUIThread(() => FilesTreeNode.UpdateFileImages(missingFileTreeNode));
            }
        }

        public void FileCreated(string fileName)
        {
            // Look for a tree node associated with the new file name. If Files Tree isn't aware
            // of a file with that name, ignore the event.
            if (FindTreeNodeForFileName(Root, fileName, out var availableFileTreeNode))
            {
                availableFileTreeNode.Model.FileState = FileState.available;

                ExecuteOnUIThread(() => FilesTreeNode.UpdateFileImages(availableFileTreeNode));
            }
        }

        public void FileRenamed(string oldFileName, string newFileName)
        {
            // Look for a tree node with the file's previous name. If a node with that name is found
            // treat the file as missing. ;
            if (FindTreeNodeForFileName(Root, oldFileName, out var missingFileTreeNode))
            {
                missingFileTreeNode.Model.FileState = FileState.missing;

                ExecuteOnUIThread(() => FilesTreeNode.UpdateFileImages(missingFileTreeNode));
            }
            // Now, look for a tree node with the new file name. If found, a file was restored with
            // a name Files Tree is aware of, so mark the file as available.
            else if (FindTreeNodeForFileName(Root, newFileName, out var availableFileTreeNode))
            {
                availableFileTreeNode.Model.FileState = FileState.available;

                ExecuteOnUIThread(() => FilesTreeNode.UpdateFileImages(availableFileTreeNode));
            }
        }

        protected override bool IsParentNode(TreeNode node)
        {
            return node.Parent == null;
        }

        protected override int EnsureChildren(TreeNode node)
        {
            return node != null ? node.Nodes.Count : 0;
        }

        protected override void Dispose(bool disposing)
        {
            _fileSystemWatcher.Dispose();
            _fileSystemWatcher = null;

            base.Dispose(disposing);
        }

        private void FilesTree_ProjectDirectory_OnDeleted(object sender, FileSystemEventArgs e)
        {
            ExecuteOnBackgroundThread(
                () => FileDeleted(e.Name),
                @"FilesTree - FileSystemWatcher - handle file deleted");
        }

        private void FilesTree_ProjectDirectory_OnCreated(object sender, FileSystemEventArgs e)
        {
            ExecuteOnBackgroundThread(
                () => FileCreated(e.Name),
                @"FilesTree - FileSystemWatcher - handle file created");
        }

        private void FilesTree_ProjectDirectory_OnRenamed(object sender, RenamedEventArgs e)
        {
            ExecuteOnBackgroundThread(
                () => FileRenamed(e.OldName, e.Name),
                @"FilesTree - FileSystemWatcher - handle file rename");
        }

        private void ExecuteOnUIThread(Action action)
        {
            Assume.IsTrue(InvokeRequired);

            CommonActionUtil.SafeBeginInvoke(this, action);
        }

        private void ExecuteOnBackgroundThread(Action action, string threadName)
        {
            Assume.IsFalse(InvokeRequired);

            ActionUtil.RunAsync(action, threadName);
        }

        // TODO: unit tests
        private static bool FindTreeNodeForFileName(TreeNode treeNode, string fileName, out FilesTreeNode value)
        {
            value = null;
            if (!(treeNode is FilesTreeNode filesTreeNode))
                return false;

            if (filesTreeNode.Model.IsBackedByFile && filesTreeNode.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
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
    }

    public class FilesTreeNode : TreeNodeMS, ITipProvider
    {
        private FileNode _model;

        internal static FilesTreeNode CreateNode(FileNode model)
        {
            return new FilesTreeNode(model, model.Name);
        }

        internal FilesTreeNode(FileNode model, string label) : base(label)
        {
            Model = model;
        }

        public FileNode Model
        {
            get => _model;
            set
            {
                _model = value;

                OnModelChanged();

                ActionUtil.RunAsync(() =>
                    {
                        // initialize this model's local file path in the background
                        // to avoid blocking the UI thread. Finding the file could be
                        // slow if the file is on a network drive / etc.
                        _model.InitLocalFile();

                        // once initialized, return to the UI thread to update this tree node's UI
                        CommonActionUtil.SafeBeginInvoke(TreeView, UpdateState);
                    },
                    // ReSharper disable once LocalizableElement
                    $"FilesTree - initialize local file {_model.FileName}");
            }
        }

        public string FileName => Model.FileName;
        public string FilePath => Model.FilePath;
        public ImageId ImageAvailable => Model.ImageAvailable;
        public ImageId ImageMissing => Model.ImageMissing;
        public string LocalFilePath => Model.LocalFilePath;

        public void UpdateState()
        {
            OnModelChanged();
        }

        public virtual void OnModelChanged()
        {
            Name = Model.Name;
            Text = Model.Name;

            if (typeof(RootFileNode) == Model.GetType())
                ImageIndex = Model.FileState == FileState.available ? (int)ImageAvailable : (int)ImageMissing;
            else
                ImageIndex = IsAnyChildFileMissing(this) ? (int)ImageMissing : (int)ImageAvailable;
        }

        internal static bool IsAnyChildFileMissing(FilesTreeNode node)
        {
            if (node.Model.FileState == FileState.missing)
                return true;
        
            foreach (FilesTreeNode child in node.Nodes)
            {
                if (IsAnyChildFileMissing(child))
                    return true;
            }
        
            return false;
        }

        public bool HasTip => true;

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_RenderTip_Name, Name, rt);

            if (Model.IsBackedByFile)
            {
                if (Model.FileState == FileState.missing)
                {
                    var font = rt.FontBold;
                    customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_RenderTip_FileMissing, string.Empty, rt);
                    font = rt.FontNormal;
                }

                customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_RenderTip_FileName, FileName, rt);
                customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_RenderTip_FilePath, FilePath, rt);

                if (Model.IsBackedByFile)
                    customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_RenderTip_LocalFilePath, LocalFilePath, rt);
            }

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }

        // Update tree node images based on whether the local file is available.
        // Stop before updating the root node representing the .sky file.
        // Does minimal traversal of the tree only walking up to root 
        // from the given node.
        internal static void UpdateFileImages(FilesTreeNode filesTreeNode)
        {
            do
            {
                filesTreeNode.OnModelChanged();
                filesTreeNode = (FilesTreeNode)filesTreeNode.Parent;
            }
            while (filesTreeNode.Parent != null);
        }

        public bool SupportsRename()
        {
            return Model.GetType() == typeof(Replicate);
        }

        public bool SupportsOpenContainingFolder()
        {
            return Model.IsBackedByFile;
        }
    }
}