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

using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
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

            if (DocumentContainer.DocumentFilePath == null)
            {
                Root = new SkylineRootTreeNode(ControlsResources.FilesTree_TreeNodeLabel_NewDocument, null);
            }
            else
            {
                Root = new SkylineRootTreeNode(Path.GetFileName(DocumentContainer.DocumentFilePath), DocumentContainer.DocumentFilePath);
            }

            Nodes.Add(Root);

            // SrmDocument => <measured_results> => <replicate>^*
            var replicatesRoot = new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_Replicates)
            {
                ImageIndex = (int)ImageId.folder
            };
            Root.Nodes.Add(replicatesRoot);

            if (Document.Settings.MeasuredResults != null)
            {
                var chromatograms = Document.Settings.MeasuredResults.Chromatograms;
                if (chromatograms.Count > 0)
                {
                    foreach (var chromatogramSet in chromatograms)
                    {
                        // Ex: "D_102_REP1" =>
                        var replicate = new TreeNodeMS(chromatogramSet.Name)
                        {
                            ImageIndex = (int)ImageId.folder
                        };
                        replicatesRoot.Nodes.Add(replicate);

                        foreach (var fileInfo in chromatogramSet.MSDataFileInfos)
                        {
                            // Ex: "D_102_REP1.raw"
                            var replicateFile = new ReplicateTreeNode(chromatogramSet, fileInfo);
                            replicate.Nodes.Add(replicateFile);
                        }
                    }
                }
            }
            else
            {
                replicatesRoot.Nodes.Add(new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_None));
            }

            // SrmDocument => <peptide_libraries> => <*_library>^*
            var peptideLibrariesRoot = new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_Libraries)
            {
                ImageIndex = (int)ImageId.folder
            };
            Root.Nodes.Add(peptideLibrariesRoot);

            var peptideLibrarySet = Document.Settings.PeptideSettings.Libraries.LibrarySpecs;
            if (peptideLibrarySet.Count > 0)
            {
                foreach (var peptideLibrary in peptideLibrarySet)
                {
                    // Ex: "Rat (NIST) (Rat_plasma2) (Rat_plasma)"
                    //      * In tooltip: "rat_consensus_final_true_lib.blib"
                    var peptideLibraryNode = new PeptideLibraryTreeNode(peptideLibrary);

                    // CONSIDER: if available, show the cache file (*.slc) under the .blib file
                    peptideLibrariesRoot.Nodes.Add(peptideLibraryNode);
                }
            }
            else
            {
                peptideLibrariesRoot.Nodes.Add(new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_None));
            }

            // SrmDocument => ...
            // Skyline path: document.Settings.PeptideSettings.BackgroundProteome
            var backgroundProteomeNode = new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_BackgroundProteome)
            {
                ImageIndex = (int)ImageId.folder
            };

            var backgroundProteome = document.Settings.PeptideSettings.BackgroundProteome;
            if (backgroundProteome != null)
            {
                backgroundProteomeNode.Nodes.Add(new BackgroundProteomeTreeNode(backgroundProteome));
            }
            else
            {
                backgroundProteomeNode.Nodes.Add(new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_None));
            }

            Root.Nodes.Add(backgroundProteomeNode);

            Root.Nodes.Add(new AuditLogTreeNode());

            // Expand root's immediate children so FilesTree shows a few nodes but isn't overwhelming
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

    public interface IFilePathProvider
    {
        string FilePath { get; }
    }

    public class SkylineRootTreeNode : TreeNodeMS, ITipProvider, IFilePathProvider
    {
        public SkylineRootTreeNode(string text, string filePath) : base(text)
        {
            FilePath = filePath;

            ImageIndex = (int)FilesTree.ImageId.skyline;
        }

        public string FilePath { get; }

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

    public class ReplicateTreeNode : TreeNodeMS, ITipProvider, IFilePathProvider
    {
        private readonly ChromFileInfo _chromFileInfo;
        private readonly ChromatogramSet _chromatogramSet;

        public ReplicateTreeNode(ChromatogramSet chromatogramSet, ChromFileInfo chromFileInfo)
        {
            _chromatogramSet = chromatogramSet;
            _chromFileInfo = chromFileInfo;

            Text = Path.GetFileName(_chromFileInfo.FilePath.GetFilePath());

            ImageIndex = (int)FilesTree.ImageId.replicate;
        }

        public string FilePath => _chromFileInfo.FilePath.GetFilePath();

        public string ChromatogramName => _chromatogramSet.Name;

        public bool HasTip => true;

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_SampleName, Text, rt);
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Path, _chromFileInfo.FilePath.GetFilePath(), rt);

            if (_chromFileInfo.RunStartTime != null)
            {
                customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_AcquiredTime, _chromFileInfo.RunStartTime.ToString(), rt); // TODO: i18n date / time?
            }

            if (_chromFileInfo.InstrumentInfoList != null && _chromFileInfo.InstrumentInfoList.Count > 0)
            {
                customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_InstrumentModel, _chromFileInfo.InstrumentInfoList[0].Model, rt);
            }

            // CONSIDER include annotations?

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }
    }

    public class PeptideLibraryTreeNode : TreeNodeMS, ITipProvider, IFilePathProvider
    {
        private readonly LibrarySpec _librarySpec;

        public PeptideLibraryTreeNode(LibrarySpec librarySpec) : base(librarySpec.Name)
        {
            _librarySpec = librarySpec;

            ImageIndex = (int)FilesTree.ImageId.peptide;
        }

        public string FilePath => _librarySpec.FilePath;

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

    public class BackgroundProteomeTreeNode : TreeNodeMS, ITipProvider
    {
        private readonly BackgroundProteome _backgroundProteome;

        public BackgroundProteomeTreeNode(BackgroundProteome backgroundProteome) : base(backgroundProteome.Name)
        {
            _backgroundProteome = backgroundProteome;
        }

        public bool HasTip => true;

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Name, Path.GetFileName(_backgroundProteome.DatabasePath), rt);
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Path, _backgroundProteome.DatabasePath, rt);

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }

    }

    public class AuditLogTreeNode : TreeNodeMS
    {
        public AuditLogTreeNode() : base(ControlsResources.FilesTree_TreeNodeLabel_AuditLog)
        {
            ImageIndex = (int)FilesTree.ImageId.file;
        }
    }
}