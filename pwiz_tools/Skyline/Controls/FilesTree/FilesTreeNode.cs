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

using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
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
        // ReSharper disable once LocalizableElement
        private static string FILE_PATH_NOT_SET = "# local path #";

        private FileNode _model;

        internal static FilesTreeNode CreateNode(FileNode model)
        {
            return new FilesTreeNode(model, model.Name);
        }

        internal FilesTreeNode(FileNode model, string label) : base(label)
        {
            Model = model;

            FileState = FileState.not_initialized;
            LocalFilePath = FILE_PATH_NOT_SET;
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

        // Initialize the path to a local file. This tries exactly once to find a file locally
        // matching the name of a file from the model.
        //
        // Note, this accesses the file system (possibly more than once) so should
        // not be executed on the UI thread.
        public void InitLocalFile()
        {
            if (Model.IsBackedByFile && ReferenceEquals(LocalFilePath, FILE_PATH_NOT_SET))
            {
                LocalFilePath = LookForFileInPotentialLocations(FilePath, FileName, DocumentPath);

                FileState = LocalFilePath != null ? FileState.available : FileState.missing;
            }
        }

        // Convenience method to avoid a repetitive cast
        public FilesTree FilesTree => (FilesTree)TreeView;

        private bool IsCurrentDropTarget()
        {
            return FilesTree.IsDragAndDrop && IsDraggable() && ReferenceEquals(FilesTree.SelectedNode, this);
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

            if (typeof(RootFileNode) == Model.GetType())
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
                    lineStart = new Point(BoundsMS.X, BoundsMS.Y + BoundsMS.Height);
                    lineEnd = new Point(BoundsMS.X + BoundsMS.Width, BoundsMS.Y + BoundsMS.Height);
                }
                // Otherwise, the insertion point is *above* the current node
                else
                {
                    lineStart = new Point(BoundsMS.X, BoundsMS.Y);
                    lineEnd = new Point(BoundsMS.X + BoundsMS.Width, BoundsMS.Y);
                }

                // TODO: does Pen need to be wrapped in using (pen) {...}
                var pen = new Pen(SystemColors.Highlight) {
                    Width = 2,
                    DashStyle = DashStyle.Dash
                };

                g.DrawLine(pen, lineStart, lineEnd);
                return;
            }

            base.DrawFocus(g);
        }

        // CONSIDER: customize behavior in subclasses. Putting everything in FilesTreeNode won't work long-term.
        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();

            // draw into table and return calculated dimensions
            var customTable = new TableDesc();
            customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_RenderTip_Name, Name, rt);

            if (Model.GetType() == typeof(ReplicatesFolder))
            {
                customTable.AddDetailRow(FilesTreeResources.FilesTreeNode_TreeNode_RenderTip_ReplicateCount, Nodes.Count.ToString(), rt);

                var sampleFileCount = 0;
                for (var i = 0; i < Nodes.Count; i++)
                {
                    sampleFileCount += Nodes[i].Nodes.Count;
                }

                customTable.AddDetailRow(FilesTreeResources.FilesTreeNode_TreeNode_RenderTip_ReplicateSampleFileCount, sampleFileCount.ToString(), rt);
            }

            if (Model.IsBackedByFile)
            {
                customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_RenderTip_FileName, FileName, rt);
                customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_RenderTip_FilePath, FilePath, rt);

                // CONSIDER: use red font if file missing
                var text = FileState == FileState.missing ? FilesTreeResources.FilesTree_TreeNode_RenderTip_FileMissing : LocalFilePath;
                customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_RenderTip_LocalFilePath, text, rt);
            }

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);
            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }

        public bool SupportsRename()
        {
            return Model.GetType() == typeof(Replicate);
        }

        public bool SupportsOpenContainingFolder()
        {
            return Model.IsBackedByFile;
        }

        public bool SupportsRemoveItem()
        {
            return Model.GetType() == typeof(Replicate) || 
                   Model.GetType() == typeof(SpectralLibrary);
        }

        public bool SupportsRemoveAllItems()
        {
            return Model.GetType() == typeof(ReplicatesFolder) || 
                   Model.GetType() == typeof(SpectralLibrariesFolder);
        }

        public bool IsDraggable()
        {
            return Model.GetType() == typeof(Replicate) ||
                   Model.GetType() == typeof(SpectralLibrary);
        }

        public bool IsDroppable()
        {
            return Model.GetType() == typeof(Replicate) ||
                   Model.GetType() == typeof(ReplicatesFolder) ||
                   Model.GetType() == typeof(SpectralLibrary) ||
                   Model.GetType() == typeof(SpectralLibrariesFolder);
        }

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

            foreach (FilesTreeNode child in node.Nodes)
            {
                if (IsAnyNodeMissingLocalFile(child))
                    return true;
            }

            return false;
        }

        // Update tree node images based on whether the local file is available.
        // Stop before updating the root node representing the .sky file.
        // Does minimal traversal of the tree only walking up to root 
        // from the given node.
        internal static void UpdateFileImages(FilesTreeNode filesTreeNode)
        {
            if (filesTreeNode == null)
                return;

            do
            {
                filesTreeNode.OnModelChanged();
                filesTreeNode = (FilesTreeNode)filesTreeNode.Parent;
            }
            while (filesTreeNode.Parent != null);
        }
    }
}