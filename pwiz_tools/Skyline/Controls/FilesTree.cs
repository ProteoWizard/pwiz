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
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using static pwiz.Skyline.Model.Lib.EncyclopeDiaLibrary;

namespace pwiz.Skyline.Controls
{
    public class FilesTree : TreeViewMS
    {
        private readonly TreeNodeMS _chromatogramRoot;
        private readonly TreeNodeMS _peptideLibrariesRoot;
        private readonly TreeNodeMS _backgroundProteomeRoot;

        public event EventHandler NewDocument;

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
            ProjectFilesRoot = new FilesTreeFolderNode(ControlsResources.FilesTree_TreeNodeLabel_ProjectFiles);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SrmDocument Document { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkylineRootTreeNode Root { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TreeNodeMS ProjectFilesRoot { get; private set; }

        public string RootNodeText()
        {
            return Root.Text;
        }

        public void ScrollToTop()
        {
            Nodes[0]?.EnsureVisible();
        }

        public void InitializeTree(IDocumentUIContainer documentUIContainer)
        {
            DocumentContainer = documentUIContainer;

            LabelEdit = false;
            DoubleBuffered = true;

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            OnDocumentChanged(this, new DocumentChangedEventArgs(null));
        }

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

                NewDocument?.Invoke(this, EventArgs.Empty);
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

            ProjectFilesRoot.ShowOrHide(Root);
        }

        internal delegate FilesTreeNode CreateFilesTreeNode(IFileBase model);

        // TODO (ekoneil): uses file names as keys and assumes file names are unique. Probably not a safe assumption.
        //                 Instead, could use IIdentityContainer.Id if supported throughout the data model.
        //                 Currently (1) protein libraries and (2) background proteome do not support identity.
        //                 No issue with this - ok to add Id().
        // TODO (ekoneil): should folders appear in the tree when empty? Example: when there's no Background Proteome, should
        //                 its folder be removed?
        // TODO (ekoneil): position of new nodes is not maintained when inserted into the tree. Should it be?
        internal static void MergeNodes(IEnumerable<IFileBase> docFiles, TreeNodeCollection treeNodes)
        {
            var visitedKeys = new List<string>();

            foreach (var model in docFiles)
            {
                // file missing from tree, so create node and add it 
                if (!treeNodes.ContainsKey(model.Name))
                {
                    var createNodeDelegate = TREE_NODE_DELEGATES[model.Type];

                    var node = createNodeDelegate(model);
                    treeNodes.Add(node);
                }
                // file already in tree, so update its model
                else
                {
                    var matchingTreeNode = treeNodes.Find(model.Name, false)[0] as FilesTreeNode;

                    if (matchingTreeNode != null)
                        matchingTreeNode.Model = model;
                }

                visitedKeys.Add(model.Name);
            }

            // look through tree for files now missing from the data model and remove corresponding TreeNode
            for (var i = 0; i < treeNodes.Count; i++)
            {
                var treeNode = treeNodes[i] as FilesTreeNode;
                var nameFromTreeNode = treeNode?.Model.Name;

                if (!visitedKeys.Contains(nameFromTreeNode))
                    treeNodes.RemoveAt(i);
            }

            // finished merging latest data model at this level. Recursively update if any model has children
            for(var i = 0; i < treeNodes.Count; i++)
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
            Model = model;

            // Configure inherited TreeNode properties
            Tag = model;
            Name = Model.Name;
            Text = Model.Name;
            ImageIndex = (int)imageId;
        }

        // TODO: do FileTree nodes need to update view state? If so, start firing a ModelChange event.
        public IFileBase Model { get; set; }
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
                return null; // this shouldn't happen

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

    public class SkylineFileModel : IFileModel
    {
        public SkylineFileModel(string name, string filePath)
        {
            Name = name;
            FilePath = filePath;
        }

        public FileType Type { get => FileType.sky; }
        public string Name { get; }
        public string FilePath { get; }
    }

    public class AuditLogFileModel : IFileModel
    {
        public AuditLogFileModel(string name, string filePath)
        {
            Name = name;
            FilePath = filePath;
        }

        public FileType Type { get => FileType.sky_audit_log; }
        public string Name { get; }
        public string FilePath { get; }
    }

    public class ViewFileModel : IFileModel
    {
        public ViewFileModel(string name, string filePath)
        {
            Name = name;
            FilePath = filePath;
        }

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