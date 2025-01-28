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
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls
{
    public class FilesTree : TreeViewMS
    {
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
            Nodes[0].EnsureVisible();
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
            if (document == null)
                return;

            Document = document;

            // Short circuit updates if nothing relevant changed. For now, this checks document.Children -
            // which is the wrong part of the document to check. SrmSettings should be used instead but
            // that breaks view state restoration (e.g. tree node expansion and selection) because
            // SrmSettings changes multiple times during startup - while state is only restored once.
            // So this needs a more nuanced fix.
            //
            // if (e.DocumentPrevious != null && ReferenceEquals(Document.Settings, e.DocumentPrevious.Settings))
            //
            if (e.DocumentPrevious != null && ReferenceEquals(document.Children, e.DocumentPrevious.Children))
            {
                return;
            }

            // Rebuild FilesTree if the document changed. This is a temporary, aggressive way of configuring 
            // the tree when something changes - ex: opening an existing .sky file or creating a new one.
            BeginUpdateMS();
            Nodes.Clear();
            EndUpdateMS();

            var skylineFileModel = DocumentContainer.DocumentFilePath == null ? 
                new SkylineFileModel(ControlsResources.FilesTree_TreeNodeLabel_NewDocument, null) : 
                new SkylineFileModel(Path.GetFileName(DocumentContainer.DocumentFilePath), DocumentContainer.DocumentFilePath);

            Root = new SkylineRootTreeNode(skylineFileModel);

            var chromatogramRoot = new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_Replicates)
            {
                ImageIndex = (int)ImageId.folder
            };

            var peptideLibrariesRoot = new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_Libraries)
            {
                ImageIndex = (int)ImageId.folder
            };

            var backgroundProteomeRoot = new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_BackgroundProteome)
            {
                ImageIndex = (int)ImageId.folder
            };

            var projectFilesRoot = new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_ProjectFiles)
            {
                ImageIndex = (int)ImageId.folder
            };

            var files = Document.Settings.Files;

            foreach (var file in files)
            {
                switch (file.Type)
                {
                    case FileType.chromatogram:
                    {
                        if (!(file is IFileGroupModel group))
                            break;

                        var replicate = new TreeNodeMS(group.Name)
                        {
                            ImageIndex = (int)ImageId.folder
                        };

                        foreach (var fileModel in group.Files)
                        {
                            var replicateFile = new ReplicateTreeNode(group, fileModel);
                            replicate.Nodes.Add(replicateFile);
                        }

                        chromatogramRoot.Nodes.Add(replicate);
                        break;
                    }
                    case FileType.peptide_library:
                    {
                        if (!(file is IFileModel model))
                            break;

                        var peptideLibrary = new PeptideLibraryTreeNode(model);
                        peptideLibrariesRoot.Nodes.Add(peptideLibrary);

                        break;
                    }
                    case FileType.background_proteome:
                    {
                        if (!(file is IFileModel model))
                            break;

                        var backgroundProteome = new BackgroundProteomeTreeNode(model);
                        backgroundProteomeRoot.Nodes.Add(backgroundProteome);
                        break;
                    }
                }
            }

            projectFilesRoot.Nodes.Add(new AuditLogTreeNode(new AuditLogFileModel(ControlsResources.FilesTree_TreeNodeLabel_AuditLog, String.Empty)));

            Nodes.Add(Root);

            Root.Nodes.Add(chromatogramRoot);

            if (backgroundProteomeRoot.Nodes.Count > 0)
            {
                Root.Nodes.Add(backgroundProteomeRoot);
            }

            if (peptideLibrariesRoot.Nodes.Count > 0)
            {
                Root.Nodes.Add(peptideLibrariesRoot);
            }

            Root.Nodes.Add(projectFilesRoot);

            // Expand root node so some nodes visible in FilesTree
            Root.Expand();
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

        public FileType Type { get => FileType.audit_log; }
        public string Name { get; }
        public string FilePath { get; }
    }

    public abstract class FilesTreeNode : TreeNodeMS
    {
        public FilesTreeNode(IFileBase model)
        {
            Model = model;
            Tag = model;

            Text = Model.Name;
        }

        public IFileBase Model { get; private set; }
    }

    public class SkylineRootTreeNode : FilesTreeNode, ITipProvider
    {
        public SkylineRootTreeNode(SkylineFileModel model) : base (model)
        {
            ImageIndex = (int)FilesTree.ImageId.skyline;
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

    public class ReplicateTreeNode : FilesTreeNode, ITipProvider
    {
        private readonly string _chromatogramName;

        public ReplicateTreeNode(IFileGroupModel group, IFileModel model) : base(model)
        {
            _chromatogramName = group.Name;
    
            ImageIndex = (int)FilesTree.ImageId.replicate;
        }

        public string ChromatogramName => _chromatogramName;

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
        public PeptideLibraryTreeNode(IFileModel model) : base(model)
        {
            ImageIndex = (int)FilesTree.ImageId.peptide;
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
        public BackgroundProteomeTreeNode(IFileModel model) : base(model) { }
    
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

    public class AuditLogTreeNode : FilesTreeNode
    {
        public AuditLogTreeNode(IFileModel model) : base(model)
        {
            ImageIndex = (int)FilesTree.ImageId.file;
        }
    }
}