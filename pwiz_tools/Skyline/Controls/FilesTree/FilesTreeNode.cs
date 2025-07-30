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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Files;

// ReSharper disable WrongIndentSize
namespace pwiz.Skyline.Controls.FilesTree
{
    public enum FileState
    {
        available,
        missing,
        not_initialized
    }

    public class FilesTreeNode : TreeNodeMS, ITipProvider
    {
        private FileNode _model;

        internal static FilesTreeNode CreateNode(FileNode model)
        {
            return new FilesTreeNode(model, model.Name);
        }

        private FilesTreeNode(FileNode model, string label) : base(label)
        {
            Model = model;
            FileState = FileState.not_initialized;
        }

        public FileNode Model
        {
            get => _model;
            set
            {
                _model = value;

                OnModelChanged();
            }
        }

        public string FileName => Model.FileName;
        public string FilePath => Model.FilePath;
        public ImageId ImageAvailable => Model.ImageAvailable;
        public ImageId ImageMissing => Model.ImageMissing;

        public FileState FileState { get; set; }
        public string LocalFilePath { get; private set; }

        public bool HasTip => true;

        internal string DocumentPath => ((FilesTree)TreeView).DocumentContainer.DocumentFilePath;

        // Convenience to avoid a cast
        public FilesTree FilesTree => (FilesTree)TreeView;

        // Try to find the file locally using a file's name / path and the path to a SrmDocument.
        // This may access the file system multiple times and should not be called on the UI thread.
        public void InitLocalFile()
        {
            // CONSIDER: assume callers already did this check, making it safe to remove?
            if (Model.IsBackedByFile)
            {
                LocalFilePath = LookForFileInPotentialLocations(FilePath, FileName, DocumentPath);

                FileState = LocalFilePath != null ? FileState.available : FileState.missing;
            }
        }

        private bool IsCurrentDropTarget()
        {
            return FilesTree.IsDuringDragAndDrop && IsDraggable() && ReferenceEquals(FilesTree.SelectedNode, this);
        }

        internal bool IsLastNodeInFolder()
        {
            return NextNode == null;
        }

        internal bool IsMouseInLowerHalf()
        {
            var mousePosition = FilesTree.PointToClient(Cursor.Position);
            var mouseInLowerHalf = mousePosition.Y > BoundsMS.Y + 0.5 * BoundsMS.Height;

            return mouseInLowerHalf;
        }

        public void UpdateState()
        {
            OnModelChanged();
        }

        public virtual void OnModelChanged()
        {
            Name = Model.Name;
            Text = Model.Name;

            if (typeof(SkylineFile) == Model.GetType())
                ImageIndex = FileState == FileState.missing ? (int)ImageMissing : (int)ImageAvailable;
            else
                ImageIndex = IsAnyNodeMissingLocalFile(this) ? (int)ImageMissing : (int)ImageAvailable;
        }

        public override Color ForeColorMS => IsCurrentDropTarget() ? Color.Black : base.ForeColorMS;

        public override Color BackColorMS => IsCurrentDropTarget() ? BackColor : base.BackColorMS;

        public override Brush SelectionBrush => IsCurrentDropTarget() ? new SolidBrush(BackColorMS) : base.SelectionBrush;

        protected override void DrawFocus(Graphics g)
        {
            if (IsCurrentDropTarget())
            {
                Point lineStart, lineEnd;

                // If dropping on the last node in this folder, move the insertion point *below* the current node
                if (IsLastNodeInFolder() && IsMouseInLowerHalf())
                {
                    const int yOffset = 1;
                    lineStart = new Point(BoundsMS.X, BoundsMS.Y + BoundsMS.Height - yOffset);
                    lineEnd = new Point(BoundsMS.X + BoundsMS.Width, BoundsMS.Y + BoundsMS.Height - yOffset);
                }
                // Otherwise, the insertion point is *above* the current node
                else
                {
                    lineStart = new Point(BoundsMS.X, BoundsMS.Y);
                    lineEnd = new Point(BoundsMS.X + BoundsMS.Width, BoundsMS.Y);
                }

                using var pen = new Pen(SystemColors.Highlight);
                pen.Width = 2;
                pen.DashStyle = DashStyle.Dash;

                g.DrawLine(pen, lineStart, lineEnd);
            }
            else
            {
                base.DrawFocus(g);
            }
        }

        // CONSIDER: customize behavior in subclasses. Putting everything in FilesTreeNode won't work long-term.
        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_Name, Name, rt);

            if (Model is ReplicatesFolder)
            {
                customTable.AddDetailRow(FilesTreeResources.FilesTreeNode_TreeNode_Tooltip_ReplicateCount, Nodes.Count.ToString(), rt);

                var sampleFileCount = 0;
                for (var i = 0; i < Nodes.Count; i++)
                {
                    sampleFileCount += Nodes[i].Nodes.Count;
                }

                customTable.AddDetailRow(FilesTreeResources.FilesTreeNode_TreeNode_Tooltip_ReplicateSampleFileCount, sampleFileCount.ToString(), rt);
            }

            if (Model.IsBackedByFile && FileState != FileState.not_initialized)
            {
                customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_FileName, FileName, rt);

                // if path == localPath: "File Path: <foo/bar>"
                // else "Saved File Path: <abc/def>, Local File Path: <foo/bar>"
                if (string.Compare(FilePath, LocalFilePath, StringComparison.Ordinal) == 0)
                {
                    customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_FilePath, FilePath, rt);
                }
                else
                {
                    customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_SavedFilePath, FilePath, rt);

                    // CONSIDER: use red font for missing files?
                    var localFilePath = FileState == FileState.missing ? FilesTreeResources.FilesTree_TreeNode_Tooltip_FileMissing : LocalFilePath;
                    customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_LocalFilePath, localFilePath, rt);
                }
            }

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }

        public bool SupportsRename()
        {
            return Model is Replicate;
        }

        public bool SupportsOpenContainingFolder()
        {
            return Model.IsBackedByFile;
        }

        public bool SupportsRemoveItem()
        {
            return Model is Replicate || Model is SpectralLibrary;
        }

        public bool SupportsRemoveAllItems()
        {
            return Model is ReplicatesFolder || Model is SpectralLibrariesFolder;
        }

        public bool IsDraggable()
        {
            return Model is Replicate || Model is SpectralLibrary;
        }

        public bool IsDroppable()
        {
            return Model is Replicate || 
                   Model is ReplicatesFolder ||
                   Model is SpectralLibrary || 
                   Model is SpectralLibrariesFolder;
        }

        public bool HasChildWithName(string name)
        {
            return Nodes.Cast<TreeNode>().Any(node => node.Text.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public FilesTreeNode NodeAt(int index)
        {
            return (FilesTreeNode)Nodes[index];
        }

        public FilesTreeNode ParentFTN => (FilesTreeNode)Parent;

        ///
        /// LOOK FOR A FILE ON DISK
        ///
        /// SkylineFiles uses this approach to locate file paths found in SrmSettings. It starts with
        /// the given path but those paths may be set on others machines. If not available locally, use
        /// <see cref="PathEx.FindExistingRelativeFile"/> to search for the file locally.
        ///
        /// TODO: is this the same way Skyline finds replicate sample files? Ex: Chromatogram.GetExistingDataFilePath
        internal static string LookForFileInPotentialLocations(string filePath, string fileName, string documentPath)
        {
            string localPath;

            if (File.Exists(filePath) || Directory.Exists(filePath))
                localPath = filePath;
            else localPath = PathEx.FindExistingRelativeFile(documentPath, fileName);

            return localPath;
        }

        internal static bool IsAnyNodeMissingLocalFile(FilesTreeNode node)
        {
            if (node.Model.IsBackedByFile && node.FileState == FileState.missing)
                return true;

            return node.Nodes.Cast<FilesTreeNode>().Any(IsAnyNodeMissingLocalFile);
        }

        // Update tree node images based on whether the local file is available.
        // Stop before updating the root node representing the .sky file.
        // Does minimal traversal of the tree only walking up to root 
        // from the given node.
        internal static void UpdateFileImages(FilesTreeNode filesTreeNode)
        {
            while (filesTreeNode != null && filesTreeNode.Parent != null)
            {
                filesTreeNode.OnModelChanged();
                filesTreeNode = (FilesTreeNode)filesTreeNode.Parent;
            }
        }

        public bool IsFileInitialized()
        {
            if (Model.IsBackedByFile)
                return FileState != FileState.not_initialized;
            // Nodes not backed by files are considered initialized by default
            else return true;
        }
    }
}