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
        public SkylineRootTreeNode Root { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TreeNodeMS AuditLogTreeNode { get; private set; }

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

            // If none of the children changed, then do nothing
            if (e.DocumentPrevious != null && ReferenceEquals(document.Children, e.DocumentPrevious.Children))
            {
                return;
            }

            // If Skyline is opening a file, empty out the tree's nodes. Handles the case where Skyline is running,
            // showing one project and a new project is open - whose nodes should replace ones already in the tree
            if (e.IsOpeningFile)
            {
                BeginUpdateMS();
                Nodes.Clear();
                EndUpdateMS();
            }

            if (DocumentContainer.DocumentFilePath == null)
            {
                BeginUpdateMS();
                Nodes.Clear(); // CONSIDER: is this necessary?
                EndUpdateMS();

                Root = new SkylineRootTreeNode(ControlsResources.FilesTree_TreeNodeLabel_NewDocument, null);
            }
            else
            {
                Root = new SkylineRootTreeNode(Path.GetFileName(DocumentContainer.DocumentFilePath), DocumentContainer.DocumentFilePath);
            }

            Nodes.Add(Root);

            // SrmDocument => <measured_results> => <replicate>^*
            var replicatesRoot = new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_Replicates);
            replicatesRoot.ImageIndex = (int)ImageId.folder;
            Root.Nodes.Add(replicatesRoot);

            if (Document.Settings.MeasuredResults != null)
            {
                var chromatogramSet = Document.Settings.MeasuredResults.Chromatograms;
                if (chromatogramSet.Count > 0)
                {
                    foreach (var chromatogram in chromatogramSet)
                    {
                        // Ex: "D_102_REP1" =>
                        var replicate = new TreeNodeMS(chromatogram.Name);
                        replicate.ImageIndex = (int)ImageId.folder;
                        replicatesRoot.Nodes.Add(replicate);

                        foreach (var fileInfo in chromatogram.MSDataFileInfos)
                        {
                            // Ex: "D_102_REP1.raw"
                            var replicateFile = new ReplicateTreeNode(fileInfo);
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
            var peptideLibrariesRoot = new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_Libraries);
            peptideLibrariesRoot.ImageIndex = (int)ImageId.folder;
            Root.Nodes.Add(peptideLibrariesRoot);

            var peptideLibrarySet = Document.Settings.PeptideSettings.Libraries.LibrarySpecs;
            if (peptideLibrarySet.Count > 0)
            {
                foreach (var peptideLibrary in peptideLibrarySet)
                {
                    // Ex: "Rat (NIST) (Rat_plasma2) (Rat_plasma)"
                    //      * In tooltip: "rat_consensus_final_true_lib.blib"
                    var peptideLibraryNode = new PeptideLibraryTreeNode(peptideLibrary.Name, peptideLibrary.FilePath);

                    // CONSIDER: if available, show the cache file (*.slc) under the .blib file
                    peptideLibrariesRoot.Nodes.Add(peptideLibraryNode);
                }
            }
            else
            {
                peptideLibrariesRoot.Nodes.Add(new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_None));
            }

            // SrmDocument => ...
            var backgroundProteomeNode = new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_BackgroundProteome);
            backgroundProteomeNode.ImageIndex = (int)ImageId.folder;
            backgroundProteomeNode.Nodes.Add(new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_None));
            Root.Nodes.Add(backgroundProteomeNode);

            AuditLogTreeNode = new TreeNodeMS(ControlsResources.FilesTree_TreeNodeLabel_AuditLog)
            {
                ImageIndex = (int)ImageId.file
            };
            Root.Nodes.Add(AuditLogTreeNode);

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

        public string FilePath { get; set; }

        public bool HasTip => true;
        
        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
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

        public ReplicateTreeNode(ChromFileInfo chromFileInfo)
        {
            _chromFileInfo = chromFileInfo;

            Text = Path.GetFileName(_chromFileInfo.FilePath.GetFilePath());

            ImageIndex = (int)FilesTree.ImageId.replicate;
        }

        public string FilePath => _chromFileInfo.FilePath.GetFilePath();

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
        public PeptideLibraryTreeNode(string name, string filePath) : base(name)
        {
            FilePath = filePath;

            ImageIndex = (int)FilesTree.ImageId.peptide;
        }

        public string FilePath { get; }

        public bool HasTip => true;

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(ControlsResources.FilesTree_TreeNode_RenderTip_Path, FilePath, rt);

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }
    }
}