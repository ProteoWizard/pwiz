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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls
{
    public class FilesTree : TreeViewMS
    {
        private readonly TreeNodeMS _chromatogramRoot;
        private readonly TreeNodeMS _peptideLibrariesRoot;
        private readonly TreeNodeMS _backgroundProteomeRoot;
        private readonly TreeNodeMS _projectFilesRoot;

        // Used while merging a file data model with a FilesTree. Each entry explains how to
        // create a specific type of TreeNode given a type of file. Entries in this dict will
        // grow as more file types are supported. 
        private static readonly Dictionary<FileType, CreateFilesTreeNode> TREE_NODE_DELEGATES =
            new Dictionary<FileType, CreateFilesTreeNode> {
                {FileType.peptide_library, PeptideLibraryTreeNode.CreateNode},
                {FileType.background_proteome, BackgroundProteomeTreeNode.CreateNode},
                {FileType.replicates, ReplicateTreeNode.CreateNode},
                {FileType.replicate_file, ReplicateSampleFileTreeNode.CreateNode}
            };

        public enum ImageId
        {
            blank,
            folder,
            file,
            replicate,
            peptide,
            skyline
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
            ImageList.Images.Add(Resources.Replicate);
            ImageList.Images.Add(Resources.Peptide);
            ImageList.Images.Add(Resources.Skyline);

            _chromatogramRoot = new FilesTreeFolderNode(ControlsResources.FilesTree_TreeNodeLabel_Replicates);
            _peptideLibrariesRoot = new FilesTreeFolderNode(ControlsResources.FilesTree_TreeNodeLabel_Libraries);
            _backgroundProteomeRoot = new FilesTreeFolderNode(ControlsResources.FilesTree_TreeNodeLabel_BackgroundProteome);
            _projectFilesRoot = new FilesTreeFolderNode(ControlsResources.FilesTree_TreeNodeLabel_ProjectFiles);
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

        public void ScrollToFileType(FileType type)
        {
            NodesForFileType(type).EnsureVisible();
        }

        public TreeNode NodesForFileType(FileType type)
        {
            return type switch
            {
                FileType.replicates => _chromatogramRoot,
                FileType.peptide_library => _peptideLibrariesRoot,
                FileType.background_proteome => _backgroundProteomeRoot,
                FileType.project_files => _projectFilesRoot,
                _ => new DummyNode()
            };
        }

        public void InitializeTree(IDocumentUIContainer documentUIContainer)
        {
            DocumentContainer = documentUIContainer;

            LabelEdit = false;
            DoubleBuffered = true;

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            OnDocumentChanged(this, new DocumentChangedEventArgs(null));
        }

        // TODO (ekoneil): should folders appear in the tree when empty? Example: when there's no Background Proteome, should
        //                 its folder be removed?
        // CONSIDER: support updating folders on document change. Ex: to show file counts in tooltips, etc.
        public void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            var document = DocumentContainer.DocumentUI;
            if (document == null || e == null)
                return;

            Document = document;

            if (!ReferenceEquals(document.Id, e.DocumentPrevious?.Id))
            {
                // Rebuild FilesTree if the document changed. This is a temporary, aggressive way of configuring 
                // the tree when something changes - ex: opening an existing .sky file or creating a new one.
                BeginUpdateMS();
                Nodes.Clear();
                EndUpdateMS();

                var skylineFileModel = DocumentContainer.DocumentFilePath == null
                    ? new SkylineFileModel(ControlsResources.FilesTree_TreeNodeLabel_NewDocument, null)
                    : new SkylineFileModel(Path.GetFileName(DocumentContainer.DocumentFilePath),
                        DocumentContainer.DocumentFilePath);

                Root = new SkylineRootTreeNode(skylineFileModel);

                Nodes.Add(Root);

                // document changed, so re-wire project-level files
                var auditLogFilePath = SrmDocument.GetAuditLogPath(DocumentContainer.DocumentFilePath);
                _projectFilesRoot.Nodes.Add(new AuditLogTreeNode(new AuditLogFileModel(ControlsResources.FilesTree_TreeNodeLabel_AuditLog, auditLogFilePath)));

                var viewFilePath = SkylineWindow.GetViewFile(DocumentContainer.DocumentFilePath);
                _projectFilesRoot.Nodes.Add(new ViewFileTreeNode(new ViewFileModel(ControlsResources.FilesTree_TreeNodeLabel_ViewFile, viewFilePath)));
            }

            var files = Document.Settings.Files;

            // Measured Results / Chromatograms
            if (files.TryGetValue(FileType.replicates, out var chromatogramFileGroups))
            {
                MergeNodes(chromatogramFileGroups, _chromatogramRoot.Nodes);
                _chromatogramRoot.ShowOrHide(Root);
            }

            // Peptide Libraries
            if (files.TryGetValue(FileType.peptide_library, out var peptideLibraryFiles))
            {
                MergeNodes(peptideLibraryFiles, _peptideLibrariesRoot.Nodes);
                _peptideLibrariesRoot.ShowOrHide(Root);
            }

            // Background Proteome
            if (files.TryGetValue(FileType.background_proteome, out var backgroundProteomeFiles))
            {
                MergeNodes(backgroundProteomeFiles, _backgroundProteomeRoot.Nodes);
                _backgroundProteomeRoot.ShowOrHide(Root);
            }

            _projectFilesRoot.ShowOrHide(Root);
        }

        private delegate FilesTreeNode CreateFilesTreeNode(IFileBase model);

        // CONSIDER: does FilesTree need to support selection like in SequenceTree / SrmTreeNode?
        // CONSIDER: refactor for more code reuse with SrmTreeNode
        internal static void MergeNodes(IEnumerable<IFileBase> docFiles, TreeNodeCollection treeNodes)
        {
            // need support for lookup by index, so convert to list for now
            var docFilesList = docFiles.ToList();

            IFileBase nodeDoc = null;

            // Keep remaining tree nodes into a map by the identity global index.
            var remaining = new Dictionary<int, FilesTreeNode>();

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
                var remove = new Dictionary<int, FilesTreeNode>();
                for (int iRemove = i; iRemove < treeNodes.Count; iRemove++)
                {
                    FilesTreeNode nodeTree = treeNodes[iRemove] as FilesTreeNode;
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
            while (i < treeNodes.Count && treeNodes[i] is FilesTreeNode);

            var firstInsertPosition = i;
            var nodesToInsert = new List<TreeNode>(docFilesList.Count - firstInsertPosition);
            // Enumerate remaining DocNodes adding to the tree either corresponding
            // TreeNodes from the map, or creating new TreeNodes as necessary.
            for (; i < docFilesList.Count; i++)
            {
                nodeDoc = docFilesList[i];
                FilesTreeNode nodeTree;
                if (!remaining.TryGetValue(nodeDoc.Id.GlobalIndex, out nodeTree))
                {
                    var createNodeDelegate = TREE_NODE_DELEGATES[nodeDoc.Type];
                    nodeTree = createNodeDelegate(nodeDoc);

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
                // Best replicate display, requires that the node have correct
                // parenting, before the text and icons can be set correctly.
                // So, force a model change to update those values.
                // Also, TransitionGroups and Transitions need to know their Peptide in order
                // to display ratios, so they must be updated when their parent changes
                if (!ReferenceEquals(docNode, nodeTree.Model))
                {
                    nodeTree.Model = docNode;
                }
            }

            // finished merging latest data model for these nodes. Now, recursively update any nodes whose model has children
            for (i = 0; i < treeNodes.Count; i++)
            {
                var treeNode = treeNodes[i] as FilesTreeNode;
                var model = treeNode?.Model;

                if (!(model is IFileGroupModel fileGroup))
                    continue;

                MergeNodes(fileGroup.Files, treeNode.Nodes);
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
    }

    public class FilesTreeFolderNode : TreeNodeMS
    {
        public FilesTreeFolderNode(string label) : base(label)
        {
            ImageIndex = (int)FilesTree.ImageId.folder;
        }
    }

    public abstract class FilesTreeNode : TreeNodeMS
    {
        public FilesTreeNode(IFileBase model, FilesTree.ImageId imageId)
        {
            // Configure inherited TreeNode properties
            Tag = model;
            Name = Model.Name;
            Text = Model.Name;
            ImageIndex = (int)imageId;
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

        protected virtual void OnModelChanged()
        {
            Name = Model.Name;
            Text = Model.Name;
        }
    }

    public class SkylineRootTreeNode : FilesTreeNode, ITipProvider
    {
        public SkylineRootTreeNode(SkylineFileModel model) : base (model, FilesTree.ImageId.skyline)
        {
        }

        public string FilePath { get => ((IFileModel)Model).FilePath; }

        public bool HasTip => true;
        
        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Name, Text, rt);
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Path, FilePath, rt);
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_ActiveDirectory, Settings.Default.ActiveDirectory, rt);

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }
    }

    public class ReplicateTreeNode : FilesTreeNode
    {
        internal static ReplicateTreeNode CreateNode(IFileBase file)
        {
            if (!(file is IFileGroupModel model))
                return null;

            return new ReplicateTreeNode(model);
        }

        public ReplicateTreeNode(IFileGroupModel model) : base(model, FilesTree.ImageId.folder)
        {
        }
    }

    public class ReplicateSampleFileTreeNode : FilesTreeNode, ITipProvider
    {
        internal static ReplicateSampleFileTreeNode CreateNode(IFileBase file)
        {
            if (!(file is IFileModel model))
                return null;

            return new ReplicateSampleFileTreeNode(model);
        }

        public ReplicateSampleFileTreeNode(IFileModel model) : base(model, FilesTree.ImageId.replicate)
        {
        }

        public string FilePath { get => ((IFileModel)Model).FilePath; }
    
        public bool HasTip => true;
    
        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();
    
            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_SampleName, Text, rt);
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Path, FilePath, rt);
    
            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }
    }

    public class PeptideLibraryTreeNode : FilesTreeNode, ITipProvider
    {
        internal static FilesTreeNode CreateNode(IFileBase file)
        {
            if (!(file is IFileModel model))
                return null;

            return new PeptideLibraryTreeNode(model);
        }

        public PeptideLibraryTreeNode(IFileModel model) : base(model, FilesTree.ImageId.peptide)
        {
        }

        public string FilePath { get => ((IFileModel)Model).FilePath; }

        public bool HasTip => true;

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Name, Path.GetFileName(FilePath), rt);
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Path, Path.GetFullPath(FilePath ?? string.Empty), rt);

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }
    }

    public class BackgroundProteomeTreeNode : FilesTreeNode, ITipProvider
    {
        internal static FilesTreeNode CreateNode(IFileBase file)
        {
            if (!(file is IFileModel model))
                return null;

            return new BackgroundProteomeTreeNode(model);
        }

        public BackgroundProteomeTreeNode(IFileModel model) : base(model, FilesTree.ImageId.file) { }
    
        public string FilePath { get => ((IFileModel)Model).FilePath; }
    
        public bool HasTip => true;
    
        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();
    
            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Name, Path.GetFileName(FilePath), rt);
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Path, FilePath, rt);
    
            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }
    }

    public class AuditLogTreeNode : FilesTreeNode, ITipProvider
    {
        public AuditLogTreeNode(AuditLogFileModel model) : base(model, FilesTree.ImageId.file)
        {
        }

        public bool HasTip => true;

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            if (!(Model is IFileModel model))
                return Size.Empty;

            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Name, model.Name, rt);
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Path, model.FilePath, rt);

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }
    }

    public class ViewFileTreeNode : FilesTreeNode, ITipProvider
    {
        public ViewFileTreeNode(ViewFileModel model) : base(model, FilesTree.ImageId.file)
        {
        }

        public bool HasTip => true;

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            if (!(Model is IFileModel model))
                return Size.Empty;

            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Name, model.Name, rt);
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Path, model.FilePath, rt);

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }
    }

    public sealed class SkylineFileModelId : Identity { };
    public sealed class AuditLogFileModelId : Identity { };
    public sealed class ViewFileModelId : Identity { };

    public class SkylineFileModel : IFileModel
    {
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
        public AuditLogFileModel(string name, string filePath)
        {
            Id = new AuditLogFileModelId();
            Name = name;
            FilePath = filePath;
        }
        public Identity Id { get; private set; }
        public FileType Type { get => FileType.sky_audit_log; }
        public string Name { get; }
        public string FilePath { get; }
    }

    public class ViewFileModel : IFileModel
    {
        public ViewFileModel(string name, string filePath)
        {
            Id = new ViewFileModelId();    
            Name = name;
            FilePath = filePath;
        }

        public Identity Id { get; private set; }
        public FileType Type { get => FileType.sky_view; }
        public string Name { get; }
        public string FilePath { get; }
    }

    public static class ExtensionMethods
    {
        public static void ShowOrHide(this TreeNode treeNode, TreeNode root)
        {
            if (treeNode.Nodes.Count > 0 && !root.Nodes.Contains(treeNode))
            {
                root.Nodes.Add(treeNode);
            }
            else if (treeNode.Nodes.Count == 0 && root.Nodes.Contains(treeNode))
            {
                root.Nodes.Remove(treeNode);
            }
        }
    }
}