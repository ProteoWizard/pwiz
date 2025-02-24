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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls
{
    public class FilesTree : TreeViewMS
    {
        // Used while merging a file data model with a FilesTree. Each entry explains how to
        // create a specific type of TreeNode given a type of file. Entries in this dict will
        // grow as more file types are supported. 
        private static readonly Dictionary<FileType, CreateFilesTreeNode> TREE_NODE_FACTORIES =
            new Dictionary<FileType, CreateFilesTreeNode> {
                {FileType.replicate, ReplicateTreeNode.CreateNode},
                {FileType.replicate_sample, ReplicateSampleFileTreeNode.CreateNode},
                {FileType.peptide_library, PeptideLibraryTreeNode.CreateNode},
                {FileType.background_proteome, BackgroundProteomeTreeNode.CreateNode},
                {FileType.retention_score_calculator, RetentionScoreCalculatorFileTreeNode.CreateNode},
                {FileType.ion_mobility_library, IonMobilityLibraryFileTreeNode.CreateNode}
            };

        private readonly FolderNode _replicatesFolder;
        private readonly FolderNode _peptideLibrariesFolder;
        private readonly FolderNode _backgroundProteomeFolder;
        private readonly FolderNode _irtFolder;
        private readonly FolderNode _imsdbFolder;
        private readonly FolderNode _projectFilesFolder;

        private readonly FileSystemWatcher _fileSystemWatcher;

        public enum ImageId
        {
            blank,
            folder,
            file,
            file_missing,
            replicate,
            replicate_sample_file,
            peptide,
            skyline,
            audit_log,
            cache_file,
            view_file
        }

        public FilesTree()
        {
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

            _replicatesFolder = FolderNode.Create(FolderType.replicates, ControlsResources.FilesTree_TreeNodeLabel_Replicates, 0);
            _peptideLibrariesFolder = FolderNode.Create(FolderType.peptide_libraries, ControlsResources.FilesTree_TreeNodeLabel_Libraries, 1);
            _backgroundProteomeFolder = FolderNode.Create(FolderType.background_proteome, ControlsResources.FilesTree_TreeNodeLabel_BackgroundProteome, 2);
            _irtFolder = FolderNode.Create(FolderType.retention_score_calculator, SkylineResources.SkylineWindow_FindIrtDatabase_iRT_Calculator, 3);
            _imsdbFolder = FolderNode.Create(FolderType.ion_mobility_library, SkylineResources.SkylineWindow_FindIonMobilityLibrary_Ion_Mobility_Library, 4);
            _projectFilesFolder = FolderNode.Create(FolderType.project_files, ControlsResources.FilesTree_TreeNodeLabel_ProjectFiles, 5);

            _fileSystemWatcher = new FileSystemWatcher();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SrmDocument Document { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkylineRootTreeNode Root { get; private set; }

        public string RootNodeText()
        {
            return Root.Text;
        }

        public void ScrollToTop()
        {
            Nodes[0]?.EnsureVisible();
        }

        public void ScrollToFolder(FolderType type)
        {
            Folder(type).EnsureVisible();
        }

        public void CollapseNodesInFolder(FolderType type)
        {
            var root = Folder(type);
            foreach (TreeNode node in root.Nodes)
            {
                node.Collapse(true);
            }
        }

        public TreeNode Folder(FolderType type)
        {
            return type switch
            {
                FolderType.replicates => _replicatesFolder,
                FolderType.peptide_libraries => _peptideLibrariesFolder,
                FolderType.background_proteome => _backgroundProteomeFolder,
                FolderType.retention_score_calculator => _irtFolder,
                FolderType.ion_mobility_library => _imsdbFolder,
                FolderType.project_files => _projectFilesFolder,
                _ => new DummyFilesTreeNode()
            };
        }

        public void FileDeleted(string fileName)
        {
            FileNode.FileDeleted(Root, fileName);
            UpdateNodeStates();
        }

        public void FileCreated(string fileName)
        {
            FileNode.FileCreated(Root, fileName);
            UpdateNodeStates();
        }

        public void FileRenamed(string oldFileName, string newFileName)
        {
            FileNode.FileRenamed(Root, oldFileName, newFileName);
            UpdateNodeStates();
        }


        public void InitializeTree(IDocumentUIContainer documentUIContainer)
        {
            DocumentContainer = documentUIContainer;

            LabelEdit = false;
            DoubleBuffered = true;

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            OnDocumentChanged(this, new DocumentChangedEventArgs(null));
        }

        // TODO (ekoneil): should folders appear in the tree when empty?
        //                 Example: when there's no Background Proteome, should its folder be removed?
        // CONSIDER: support updating folders on document change. Ex: to show file counts in tooltips, etc.
        public void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            var document = DocumentContainer.DocumentUI;
            if (document == null || e == null)
                return;

            Document = document;

            if (!ReferenceEquals(document.Id, e.DocumentPrevious?.Id))
            {
                // The document changed, so rebuild FilesTree starting at the root
                BeginUpdateMS();
                Nodes.Clear();
                _projectFilesFolder.Nodes.Clear();
                EndUpdateMS();

                SkylineFileModel skylineFileModel;
                if (DocumentContainer.DocumentFilePath == null)
                {
                    // TODO: initialize FileSystemWatcher when a new document is saved?
                    skylineFileModel = new SkylineFileModel(ControlsResources.FilesTree_TreeNodeLabel_NewDocument, null);
                }
                else
                {
                    skylineFileModel = new SkylineFileModel(Path.GetFileName(DocumentContainer.DocumentFilePath), DocumentContainer.DocumentFilePath);

                    _fileSystemWatcher.Path = Path.GetDirectoryName(DocumentContainer.DocumentFilePath);
                    _fileSystemWatcher.SynchronizingObject = this;
                    _fileSystemWatcher.NotifyFilter = NotifyFilters.FileName;
                    _fileSystemWatcher.IncludeSubdirectories = true;
                    _fileSystemWatcher.EnableRaisingEvents = true;
                    _fileSystemWatcher.Renamed += FilesTree_ProjectDirectory_OnRenamed;
                    _fileSystemWatcher.Deleted += FilesTree_ProjectDirectory_OnDeleted;
                    _fileSystemWatcher.Created += FilesTree_ProjectDirectory_OnCreated;
                }

                Root = new SkylineRootTreeNode(skylineFileModel);

                Nodes.Add(Root);

                // document changed, so re-wire project-level files
                var auditLogFilePath = SrmDocument.GetAuditLogPath(DocumentContainer.DocumentFilePath);
                _projectFilesFolder.Nodes.Add(new SkylineAuditLogTreeNode(new AuditLogFileModel(auditLogFilePath)));

                var viewFilePath = SkylineWindow.GetViewFile(DocumentContainer.DocumentFilePath);
                _projectFilesFolder.Nodes.Add(new SkylineViewTreeNode(new ViewFileModel(viewFilePath)));

                // CONSIDER: is this the correct way to get the path to a .skyd file?
                //           is there a back compat consideration when there were per-replicate cache files?
                var skydFilePath = ChromatogramCache.FinalPathForName(DocumentContainer.DocumentFilePath, null);
                if (File.Exists(skydFilePath))
                {
                    _projectFilesFolder.Nodes.Add(new SkylineChromatogramCacheTreeNode(new ChromatogramCacheFileModel(skydFilePath)));
                }
            }

            var docFiles = Document.Settings.Files;

            // TODO: generate whole tree from document
            // ConnectFilesOfTypeToTree(docFiles, FileType.folder_replicates, _replicatesFolder);                      // replicate => sample files (.raw, etc)

            ConnectFilesOfTypeToTree(docFiles, FileType.folder_replicates, _replicatesFolder);                      // replicate => sample files (.raw, etc)
            ConnectFilesOfTypeToTree(docFiles, FileType.folder_peptide_libraries, _peptideLibrariesFolder);         // .blib
            ConnectFilesOfTypeToTree(docFiles, FileType.folder_background_proteome, _backgroundProteomeFolder);     // .protdb
            ConnectFilesOfTypeToTree(docFiles, FileType.folder_retention_score_calculator, _irtFolder);             // .irtdb
            ConnectFilesOfTypeToTree(docFiles, FileType.folder_ion_mobility_library, _imsdbFolder);                 // .imsdb
            // TODO: support .optdb

            AddOrRemove(_projectFilesFolder);

            UpdateNodeStates();

            var expandedNodes = IsAnyNodeExpanded(Root);
            if (!expandedNodes)
            {
                Root.Expand();
                foreach (TreeNode child in Root.Nodes)
                {
                    child.Expand();

                }
            }
        }

        private void UpdateNodeStates()
        {
            BeginUpdate();
            UpdateNodeStates(Root);
            EndUpdate();
        }

        private static void UpdateNodeStates(FilesTreeNode node)
        {
            foreach (FilesTreeNode child in node.Nodes)
            {
                UpdateNodeStates(child);
            }

            node.UpdateState();
        }

        private void ConnectFilesOfTypeToTree(IFileGroupModel root, FileType type, FolderNode folder)
        {
            // CONSIDER: put TryGetValue style accessor on IFileGroupModel
            var filesForType = root.FileGroupForType(type)?.FilesAndFolders;

            MergeNodes(DocumentContainer.DocumentFilePath, filesForType, folder.Nodes); 
            AddOrRemove(folder);
        }

        private void AddOrRemove(FolderNode folder)
        {
            if (folder.Nodes.Count == 0 && Root.Nodes.Contains(folder))
                Root.Nodes.Remove(folder);
            else if (folder.Nodes.Count > 0 && !Root.Nodes.Contains(folder))
                InsertInOrder(folder);
        }

        private void InsertInOrder(FolderNode node)
        {
            var start = 0;
            var end = Root.Nodes.Count;
            while (start < end)
            {
                var middle = (start + end) / 2;
                if (Compare(Root.Nodes[middle] as FolderNode, node) <= 0)
                    start = middle + 1;
                else end = middle;

            }
            var insertAt = start;

            Root.Nodes.Insert(insertAt, node);
        }

        private static int Compare(FolderNode a, FolderNode b)
        {
            return a.CompareTo(b);
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

        public delegate FileNode CreateFilesTreeNode(string documentPath, IFileBase model);

        // CONSIDER: does FilesTree need to support selection like in SequenceTree / SrmTreeNode?
        // CONSIDER: refactor for more code reuse with SrmTreeNode
        internal static void MergeNodes(string documentPath, IEnumerable<IFileBase> docFiles, TreeNodeCollection treeNodes)
        {
            if (docFiles == null)
                return;

            // need to look items up by index, so convert to list
            var docFilesList = docFiles.ToList();

            IFileBase nodeDoc = null;

            // Keep remaining tree nodes into a map by the identity global index.
            var remaining = new Dictionary<int, FileNode>();

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
                    var nodeTree = treeNodes[i] as FileNode;
                    if (nodeTree == null)
                        break;
                    if (!ReferenceEquals(nodeTree.Model, nodeDoc))
                    {
                        if (ReferenceEquals(nodeTree.Model.Id, nodeDoc.Id))
                        {
                            nodeTree.Model = nodeDoc;
                        }
                        else
                        {
                            // If no usable equality, and not in the map of nodes already
                            // removed, then this loop cannot continue.
                            if (!remaining.TryGetValue(nodeDoc.Id.GlobalIndex, out nodeTree))
                                break;

                            // Found node with the same ID, so replace its doc node, if not
                            // reference equal to the one looked up.
                            if (!ReferenceEquals(nodeTree.Model, nodeDoc))
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
                var remove = new Dictionary<int, FileNode>();
                for (int iRemove = i; iRemove < treeNodes.Count; iRemove++)
                {
                    FileNode nodeTree = treeNodes[iRemove] as FileNode;
                    if (nodeTree == null)
                        break;
                    // Stop removing, if the next node in the document is encountered.
                    if (nodeDoc != null && ReferenceEquals(nodeTree.Model.Id, nodeDoc.Id))
                        break;

                    remove.Add(nodeTree.Model.Id.GlobalIndex, nodeTree);
                    remaining.Add(nodeTree.Model.Id.GlobalIndex, nodeTree);
                }

                // Remove the newly mapped children from the tree itself for now.
                foreach (var node in remove.Values)
                    node.Remove();
            }
            // Loop, if not all tree nodes have been removed or matched.
            while (i < treeNodes.Count && treeNodes[i] is FileNode);

            var firstInsertPosition = i;
            var nodesToInsert = new List<TreeNode>(docFilesList.Count - firstInsertPosition);
            // Enumerate remaining DocNodes adding to the tree either corresponding
            // TreeNodes from the map, or creating new TreeNodes as necessary.
            for (; i < docFilesList.Count; i++)
            {
                nodeDoc = docFilesList[i];
                FileNode nodeTree;
                if (!remaining.TryGetValue(nodeDoc.Id.GlobalIndex, out nodeTree))
                {
                    var createNodeDelegate = TREE_NODE_FACTORIES[nodeDoc.Type];
                    nodeTree = createNodeDelegate(documentPath, nodeDoc);

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
                var nodeTree = (FileNode)treeNodes[insertNodeIndex + firstInsertPosition];
                var docNode = docFilesList[insertNodeIndex + firstInsertPosition];

                if (!ReferenceEquals(docNode, nodeTree.Model))
                {
                    nodeTree.Model = docNode;
                }
            }

            // finished merging latest data model for these nodes. Now, recursively update any nodes whose model has children
            for (i = 0; i < treeNodes.Count; i++)
            {
                var treeNode = treeNodes[i] as FileNode;
                var model = treeNode?.Model;

                // TODO (ekoneil): replace .Files with .FilesAndFolders
                // Look for TreeNodes whose model represent a file group. If any are found, rely on their
                // models being up-to-date (since it was just merged with the document) and recursively
                // create / update / delete TreeNodes associated with its children
                if (model is IFileGroupModel fileGroup)
                    MergeNodes(documentPath, fileGroup.Files, treeNode.Nodes);
            }
        }

        protected override bool IsParentNode(TreeNode node)
        {
            return node is SkylineRootTreeNode;
        }

        protected override int EnsureChildren(TreeNode node)
        {
            return node != null ? node.Nodes.Count : 0;
        }

        protected override void Dispose(bool disposing)
        {
            _fileSystemWatcher?.Dispose();

            base.Dispose(disposing);
        }

        private void FilesTree_ProjectDirectory_OnDeleted(object sender, FileSystemEventArgs e)
        {
            FileDeleted(e.Name);
        }

        private void FilesTree_ProjectDirectory_OnCreated(object sender, FileSystemEventArgs e)
        {
            FileCreated(e.Name);
        }

        private void FilesTree_ProjectDirectory_OnRenamed(object sender, RenamedEventArgs e)
        {
            FileRenamed(e.OldName, e.Name);
        }
    }

    public enum FolderType
    {
        replicates,
        peptide_libraries,
        background_proteome,
        retention_score_calculator,
        ion_mobility_library,
        project_files
    }

    public abstract class FilesTreeNode : TreeNodeMS
    {
        internal FilesTreeNode(string label, FilesTree.ImageId imageAvailable, FilesTree.ImageId imageMissing) :
            base(label)
        {
            ImageAvailable = (int)imageAvailable;
            ImageMissing = (int)imageMissing;

            ImageIndex = ImageAvailable;
        }
        public int ImageAvailable { get; }

        public int ImageMissing { get; }

        public virtual void OnModelChanged()
        {
        }

        public void UpdateState()
        {
            OnModelChanged();
        }

        internal static bool IsAnyFileMissing(FilesTreeNode node)
        {
            if (node is FileNode { FileState: FileState.missing })
                return true;

            foreach (FilesTreeNode child in node.Nodes)
            {
                if (IsAnyFileMissing(child))
                    return true;
            }

            return false;
        }
    }

    internal class DummyFilesTreeNode : FilesTreeNode
    {
        public DummyFilesTreeNode() : base(string.Empty, FilesTree.ImageId.blank, FilesTree.ImageId.blank) { }
    }

    public class FolderNode : FilesTreeNode, IComparable
    {
        public static FolderNode Create(FolderType type, string label, int sortOrder, 
                                        FilesTree.ImageId imageAvailable = FilesTree.ImageId.folder, 
                                        FilesTree.ImageId imageMissing = FilesTree.ImageId.file_missing)
        {
            var folderNode = new FolderNode(label, imageAvailable, imageMissing)
            {
                Name = label,
                FolderType = type,
                SortOrder = sortOrder,
            };

            return folderNode;
        }

        internal FolderNode(string label, FilesTree.ImageId imageAvailable, FilesTree.ImageId imageMissing) :
            base(label, imageAvailable, imageMissing) { }

        public FolderType FolderType { get; private set; }

        // Keeps folder nodes in proper order. Only applies to level 1 children of FilesTree.
        public int SortOrder { get; private set; }

        public override void OnModelChanged()
        {
            var isAnyFileMissing = IsAnyFileMissing(this);
            ImageIndex = isAnyFileMissing ? ImageMissing : ImageAvailable;
        }

        public int CompareTo(object obj)
        {
            return obj switch
            {
                null => 1,
                FolderNode otherFolderNode => SortOrder.CompareTo(otherFolderNode.SortOrder),
                _ => 0
            };
        }
    }

    public enum FileState
    {
        available,
        missing,
        unknown
    }

    public abstract class FileNode : FilesTreeNode
    {
        protected FileNode(IFileBase model, FilesTree.ImageId imageAvailable, FilesTree.ImageId imageMissing = FilesTree.ImageId.file_missing) :
            base(model.Name, imageAvailable, imageMissing)
        {
            Tag = model;
            Name = Model.Name;
            FileState = FileState.unknown;
        }

        public FileState FileState { get; protected set; }

        public string FilePath { get => (Model as IFileModel)?.FilePath; }

        public abstract string LocalFilePath { get; }

        // CONSIDER: add FileName to IFileModel?
        public virtual string FileName
        {
            get
            {
                var model = Tag as IFileModel;
                return Path.GetFileName(model?.FilePath);
            }
        }

        public IFileBase Model
        {
            get => (IFileBase)Tag;
            set
            {
                Tag = value;

                OnModelChanged();
            }
        }

        internal virtual bool LocalFileExists()
        {
            return File.Exists(LocalFilePath);
        }

        internal virtual void FileAvailable()
        {
            FileState = FileState.available; 
        }

        internal virtual void FileMissing()
        {
            FileState = FileState.missing;
        }

        public override void OnModelChanged()
        {
            Name = Model.Name;
            Text = Model.Name;

            var isAnyFileMissing = IsAnyFileMissing(this);
            // CONSIDER: rely on FileState or just check the file system?
            ImageIndex = isAnyFileMissing ? ImageMissing : ImageAvailable;
        }

        public bool HasTip => true;

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Name, Name, rt);

            if (!LocalFileExists())
            {
                var font = rt.FontBold;
                customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_FileMissing, string.Empty, rt);
                font = rt.FontNormal;
            }

            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_FileName, FileName, rt);
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_FilePath, FilePath, rt);

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }

        private static TreeNode FindTreeNodeForFileName(TreeNode node, string name)
        {
            if (node is FileNode file) 
            {
                if (file.FileName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }
            }

            foreach (TreeNode n in node.Nodes)
            {
                var result = FindTreeNodeForFileName(n, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        internal static void FileDeleted(TreeNode root, string name)
        {
            if(FindTreeNodeForFileName(root, name) is FileNode treeNode)
                treeNode.FileState = FileState.missing;
        }

        internal static void FileCreated(TreeNode root, string name)
        {
            // Look for a tree node associated with the new file name. If Files Tree isn't aware
            // of a file with that name, ignore the event.
            if (FindTreeNodeForFileName(root, name) is FileNode treeNode)
                treeNode.FileState = FileState.available;
        }

        internal static void FileRenamed(TreeNode root, string oldName, string newName)
        {
            // Look for a tree node with the file's previous name. If a node with that name is found
            // treat the file as missing. 
            if (FindTreeNodeForFileName(root, oldName) is FileNode missing)
            {
                missing.FileMissing();
            }
            // Now, look for a tree node with the new file name. If found, a file was restored with
            // a name Files Tree is aware of, so mark the file as available.
            else if(FindTreeNodeForFileName(root, newName) is FileNode available)
            {
                available.FileAvailable();
            }
        }

        /// SkylineFiles uses this approach to locate file paths found in SrmSettings. It starts with
        /// the given path but those paths may be set on others machines. If not available locally, use
        /// <see cref="PathEx.FindExistingRelativeFile"/> to search for the file locally.
        ///
        /// <param name="relativeFilePath">Usually the SrmDocument path.</param>
        /// <param name="file"></param>
        ///
        /// TODO: is this the same search algorithm used for replicate sample files?
        internal static string FindExistingInPossibleLocations(string relativeFilePath, string file)
        {
            if (File.Exists(file) || Directory.Exists(file))
            {
                return file;
            }
            else return PathEx.FindExistingRelativeFile(relativeFilePath, file);
        }
    }

    public class SkylineRootTreeNode : FileNode, ITipProvider
    {
        public SkylineRootTreeNode(SkylineFileModel model) : 
            base (model, FilesTree.ImageId.skyline) { }

        public override string LocalFilePath => FilePath;

        public override void OnModelChanged()
        {
            Name = Model.Name;
            Text = Model.Name;
        }
    }

    public class ReplicateTreeNode : FileNode
    {
        internal static ReplicateTreeNode CreateNode(string documentPath, IFileBase file)
        {
            if (!(file is IFileGroupModel model))
                return null;

            return new ReplicateTreeNode(model);
        }

        public ReplicateTreeNode(IFileGroupModel model) : 
            base(model, FilesTree.ImageId.replicate) { }

        // CONSIDER: ReplicateTreeNodes are virtual files and don't represent an actual
        //           file on disk / network. So consider adding a VirtualFileNode subclass
        public override string FileName { get => Name;}

        public override string LocalFilePath { get => null; }

        internal override bool LocalFileExists() { return true; }
    }

    public class ReplicateSampleFileTreeNode : FileNode, ITipProvider
    {
        internal static ReplicateSampleFileTreeNode CreateNode(string documentPath, IFileBase file)
        {
            if (!(file is IFileModel model))
                return null;

            var localFilePath = FindExistingInPossibleLocations(documentPath, model.FilePath);
            return new ReplicateSampleFileTreeNode(model, localFilePath);
        }

        public ReplicateSampleFileTreeNode(IFileModel model, string localFilePath) : 
            base(model, FilesTree.ImageId.replicate_sample_file)
        {
            LocalFilePath = localFilePath;
            FileState = localFilePath != null ? FileState.available : FileState.missing;
        }

        public override string LocalFilePath { get; }

        internal override bool LocalFileExists()
        {
            // includes a directory check because some replicate samples live in nested directories (*.d)
            return File.Exists(LocalFilePath) || Directory.Exists(LocalFilePath); 
        }
    }

    public class PeptideLibraryTreeNode : FileNode, ITipProvider
    {
        internal static FileNode CreateNode(string documentPath, IFileBase file)
        {
            if (!(file is IFileModel model))
                return null;

            var localFilePath = FindExistingInPossibleLocations(documentPath, model.FilePath);
            return new PeptideLibraryTreeNode(model, localFilePath);
        }

        public PeptideLibraryTreeNode(IFileModel model, string localFilePath) : 
            base(model, FilesTree.ImageId.peptide)
        {
            LocalFilePath = localFilePath;
            FileState = localFilePath != null ? FileState.available : FileState.missing;
        }

        public override string LocalFilePath { get; }
    }

    public class RetentionScoreCalculatorFileTreeNode : FileNode, ITipProvider
    {
        internal static FileNode CreateNode(string documentPath, IFileBase file)
        {
            if (!(file is IFileModel model))
                return null;

            var localFilePath = FindExistingInPossibleLocations(documentPath, model.FilePath);
            return new RetentionScoreCalculatorFileTreeNode(model, localFilePath);
        }

        public RetentionScoreCalculatorFileTreeNode(IFileModel model, string localFilePath) : 
            base(model, FilesTree.ImageId.file)
        {
            LocalFilePath = localFilePath;
            FileState = localFilePath != null ? FileState.available : FileState.missing;
        }

        public override string LocalFilePath { get; }
    }

    public class IonMobilityLibraryFileTreeNode : FileNode, ITipProvider
    {
        internal static FileNode CreateNode(string documentPath, IFileBase file)
        {
            if (!(file is IFileModel model))
                return null;

            var localFilePath = FindExistingInPossibleLocations(documentPath, model.FilePath);
            return new IonMobilityLibraryFileTreeNode(model, localFilePath);
        }

        public IonMobilityLibraryFileTreeNode(IFileModel model, string localFilePath) : 
            base(model, FilesTree.ImageId.file)
        {
            LocalFilePath = localFilePath;
            FileState = localFilePath != null ? FileState.available : FileState.missing;
        }

        public override string LocalFilePath { get; }
    }

    public class BackgroundProteomeTreeNode : FileNode, ITipProvider
    {
        internal static FileNode CreateNode(string documentPath, IFileBase file)
        {
            if (!(file is IFileModel model))
                return null;

            var localFilePath = FindExistingInPossibleLocations(documentPath, model.FilePath);
            return new BackgroundProteomeTreeNode(model, localFilePath);
        }

        public BackgroundProteomeTreeNode(IFileModel model, string localFilePath) : 
            base(model, FilesTree.ImageId.file)
        {
            LocalFilePath = localFilePath;
            FileState = localFilePath != null ? FileState.available : FileState.missing;
        }

        public override string LocalFilePath { get; }
    }

    #region Project File IFileModels and TreeNodes

    public class SkylineAuditLogTreeNode : FileNode, ITipProvider
    {
        public SkylineAuditLogTreeNode(AuditLogFileModel model) : 
            base(model, FilesTree.ImageId.audit_log) { }

        public override string LocalFilePath => (Model as IFileModel)?.FilePath;
    }

    public class SkylineViewTreeNode : FileNode, ITipProvider
    {
        public SkylineViewTreeNode(ViewFileModel model) : 
            base(model, FilesTree.ImageId.view_file) { }

        public override string LocalFilePath => (Model as IFileModel)?.FilePath;
    }

    public class SkylineChromatogramCacheTreeNode : FileNode, ITipProvider
    {
        public SkylineChromatogramCacheTreeNode(ChromatogramCacheFileModel model) : 
            base(model, FilesTree.ImageId.cache_file) { }

        public override string LocalFilePath => (Model as IFileModel)?.FilePath;
    }

    public class SkylineFileModel : IFileModel
    {
        private sealed class SkylineFileModelId : Identity { }

        public SkylineFileModel(string name, string filePath)
        {
            Id = new SkylineFileModelId();
            Name = name;
            FilePath = filePath;
        }

        public Identity Id { get; private set; }
        public FileType Type { get => FileType.sky; }
        public string Name { get; }
        public string FilePath { get; }
    }

    public class AuditLogFileModel : IFileModel
    {
        private sealed class AuditLogFileModelId : Identity { }

        public AuditLogFileModel(string filePath)
        {
            Id = new AuditLogFileModelId();
            Name = ControlsResources.FilesTree_TreeNodeLabel_AuditLog;
            FilePath = filePath;
        }
        public Identity Id { get; private set; }
        public FileType Type { get => FileType.sky_audit_log; }
        public string Name { get; }
        public string FilePath { get; }
    }
    
    public class ChromatogramCacheFileModel : IFileModel
    {
        private sealed class ChromatogramCacheModelId : Identity { }

        public ChromatogramCacheFileModel(string filePath)
        {
            Id = new ChromatogramCacheModelId();
            Name = ControlsResources.FilesTree_TreeNodeLabel_ChromatogramCache;
            FilePath = filePath;
        }
        public Identity Id { get; private set; }
        public FileType Type { get => FileType.sky_chromatogram_cache; }
        public string Name { get; }
        public string FilePath { get; }
    }

    public class ViewFileModel : IFileModel
    {
        private sealed class ViewFileModelId : Identity { }

        public ViewFileModel(string filePath)
        {
            Id = new ViewFileModelId();    
            Name = ControlsResources.FilesTree_TreeNodeLabel_ViewFile;
            FilePath = filePath;
        }

        public Identity Id { get; private set; }
        public FileType Type { get => FileType.sky_view; }
        public string Name { get; }
        public string FilePath { get; }
    }

    #endregion
}