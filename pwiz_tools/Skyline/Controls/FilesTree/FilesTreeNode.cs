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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Files;

// ReSharper disable WrongIndentSize
namespace pwiz.Skyline.Controls.FilesTree
{
    public enum FileState { available, missing, not_initialized, in_memory }

    // CONSIDER: customize behavior in subclasses. Overloading FilesTreeNode won't scale long-term.
    public class FilesTreeNode : TreeNodeMS, ITipProvider
    {
        private FileNode _model;

        internal static FilesTreeNode CreateNode(FileNode model)
        {
            Assume.IsNotNull(model);

            return new FilesTreeNode(model);
        }

        private FilesTreeNode(FileNode model)
        {
            FileState = FileState.not_initialized;
            Model = model;
        }

        public FileNode Model
        {
            get => _model;
            internal set
            {
                _model = value;

                OnModelChanged();
            }
        }

        public string FileName => Model.FileName;
        public string FilePath => Model.FilePath;
        public string LocalFilePath { get; private set; }
        public FileState FileState { get; internal set; }
        public ImageId ImageAvailable => Model.ImageAvailable;
        public ImageId ImageMissing => Model.ImageMissing;
        
        public bool HasTip => true;

        // Convenience to avoid casts
        public FilesTree FilesTree => (FilesTree)TreeView;
        public FilesTreeNode ParentFTN => (FilesTreeNode)Parent;

        /// <summary>
        /// Update the node's state given a local file path. If the local file path is non-null,
        /// the file is available either in-memory or on the local file system.
        /// </summary>
        /// <param name="localFilePath"></param>
        public void UpdateState(string localFilePath)
        {
            LocalFilePath = localFilePath;

            if (LocalFilePath == null && Model is SkylineAuditLog)
            {
                FileState = FileState.in_memory;
            }
            else
            {
                FileState = LocalFilePath != null ? FileState.available : FileState.missing;
            }

            UpdateState();
        }

        public void UpdateState()
        {
            OnModelChanged();
        }

        public virtual void OnModelChanged()
        {
            Name = Model.Name;
            Text = Model.Name;

            // Special handling for .sky file - do not change icon if child files are missing
            if (Model is SkylineFile)
            {
                ImageIndex = FileState == FileState.missing ? (int)ImageMissing : (int)ImageAvailable;
            }
            else
            {
                // CONSIDER: use a separate state flag to track whether 1+ child nodes are missing files instead of inferring child state from the parent's ImageIndex
                var anyChildMissingFile =
                    Nodes.Cast<FilesTreeNode>().Any(node => node.FileState == FileState.missing) ||
                    Nodes.Cast<FilesTreeNode>().Any(node => node.ImageIndex == (int)ImageId.file_missing || node.ImageIndex == (int)ImageId.folder_missing || node.ImageIndex == (int)ImageId.replicate_missing);

                if (anyChildMissingFile || FileState == FileState.missing)
                    ImageIndex = (int)ImageMissing;
                // CONSIDER: use a different icon for in-memory files, maybe grey out the available icon? 
                //           for now, use the available icon.
                else if (FileState == FileState.in_memory)
                    ImageIndex = (int)ImageAvailable;
                else
                    ImageIndex = (int)ImageAvailable;
            }
        }

        public void RefreshState()
        {
            RefreshState(this);
        }

        /// <summary>
        /// Update the UI of all nodes in a FilesTree. Works bottom-up to ensure the entire tree reflects the latest
        /// state, making sure any changes to the node's <see cref="FileState"/> are shown in the UI. Does not access
        /// the file system.
        /// </summary>
        /// <param name="node"></param>
        internal static void RefreshState(FilesTreeNode node)
        {
            foreach (FilesTreeNode child in node.Nodes)
            {
                RefreshState(child);
            }

            node.UpdateState();
        }

        /// <summary>
        /// Check if a file is available locally. This check is subtle so see in-line docs.
        /// </summary>
        /// <returns>true if the file is available locally - either on disk or in-memory. false otherwise.</returns>
        public bool LocalFileIsAvailable()
        {
            if (!Model.IsBackedByFile)
                return false;

            // Model is backed by a file and the file is available locally, as of the last check
            if (LocalFilePath != null && FileState == FileState.available)
                return true;

            // Model is backed by a file and the file is available - but only in-memory
            if (FileState == FileState.in_memory)
                return true;

            return false;
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

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();
            var customTable = new TableDesc();

            customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_Name, Name, rt);

            if (Model is ReplicatesFolder replicatesFolder)
            {
                customTable.AddDetailRow(FilesTreeResources.FilesTreeNode_TreeNode_Tooltip_ReplicateCount, Nodes.Count.ToString(), rt);
                customTable.AddDetailRow(FilesTreeResources.FilesTreeNode_TreeNode_Tooltip_ReplicateSampleFileCount, replicatesFolder.SampleFileCount().ToString(), rt);
            }

            // Only show this section if the file exists (or should exist) on disk
            if (Model.IsBackedByFile && FileState != FileState.not_initialized && FileState != FileState.in_memory)
            {
                customTable.AddDetailRow(@" ", @" ", rt);
                customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_FileName, FileName, rt);
                customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_FilePath, FilePath, rt);

                customTable.AddDetailRow(@" ", @" ", rt);
                if (FileState == FileState.available || FileState == FileState.in_memory)
                {
                    customTable.AddDetailRow(@"LocalFilePath", LocalFilePath, rt);
                }
                else
                {
                    var label = @"LocalFilePath";
                    var value = LocalFilePath;

                    RenderTipAddRowWithRedValue(label, value, customTable, rt);
                }

                // TODO: Brendan wants tooltips to look like this. Restore.
                // // if saved file path and local file path are the same: show only "File Path: c:\foo\bar"
                // // otherwise, show both "Saved File Path: c:\abc\def" and "Local File Path: c:\foo\bar"
                // if (string.Compare(FilePath, LocalFilePath, StringComparison.Ordinal) == 0)
                // {
                //     customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_FilePath, FilePath, rt);
                // }
                // else
                // {
                //     customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_SavedFilePath, FilePath, rt);
                //
                //     // CONSIDER: use red font for missing files?
                //     var localFilePath = FileState == FileState.missing ? FilesTreeResources.FilesTree_TreeNode_Tooltip_FileMissing : LocalFilePath;
                //     customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_LocalFilePath, localFilePath, rt);
                // }
            }

            if (Debugger.IsAttached)
            {
                customTable.AddDetailRow(@" ", @" ", rt);
                customTable.AddDetailRow(@"Debug Info", @"(only visible when debugger attached) ", rt);
                customTable.AddDetailRow(@" ", @" ", rt);
                customTable.AddDetailRow(@"FileName", FileName, rt);
                customTable.AddDetailRow(@"FilePath", FilePath, rt);

                if (!Model.IsBackedByFile)
                {
                    customTable.AddDetailRow(@"FileState", @"Not backed by local file", rt);
                }
                else if (FileState == FileState.available || FileState == FileState.in_memory)
                {
                    customTable.AddDetailRow(@"FileState", FileState.ToString(), rt);
                    customTable.AddDetailRow(@"LocalFilePath", LocalFilePath, rt);
                }
                else
                {
                    RenderTipAddRowWithRedValue(@"FileState", FileState.ToString(), customTable, rt);
                    RenderTipAddRowWithRedValue(@"LocalFilePath", LocalFilePath, customTable, rt);
                }

                // Show extra debug info on the .sky file
                if (Model is SkylineFile)
                {
                    customTable.AddDetailRow(@" ", @" ", rt);
                    customTable.AddDetailRow(@"Monitored directory", FilesTree.PathMonitoredForFileSystemChanges(), rt);
                }

                if (Model is SkylineAuditLog || Model is SkylineFile)
                {
                    var isForceEnabled = Program.FunctionalTest && !AuditLogList.IgnoreTestChecks;
                    customTable.AddDetailRow(@"Audit Log enabled by tests?", $@"{(isForceEnabled ? @"yes" : @"no")}", rt);
                }

                // CONSIDER: add SrmDocument.RevisionIndex to FileNode
                // customTable.AddDetailRow(@"Document revision", $@"{Model.DocumentRevisionIndex}", rt);
            }

            var size = customTable.CalcDimensions(g);
            customTable.Draw(g);

            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }

        private static void RenderTipAddRowWithRedValue(string label, string value, TableDesc table, RenderTools rt)
        {
            var cellLabel = new CellDesc(label, rt)
            {
                Font = rt.FontBold
            };

            var cellValue = new CellDesc(value, rt)
            {
                Brush = rt.BrushSelected
            };

            var row = new RowDesc { cellLabel, cellValue };

            table.Add(row);
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

        protected override void DebugBorders(Graphics g, int rightEdge)
        {
            // var bounds = BoundsMS;
            //
            // var boundsTv = Bounds;
            // using var penTv = new Pen(Color.Green, 1);
            // g.DrawRectangle(penTv, boundsTv);
            //
            // using var penMs = new Pen(Color.Red, 1);
            // g.DrawRectangle(penMs, bounds);
        }

        public new string ToString()
        {
            return $@"FilesTreeNode: {Name} {Model.GetType().Name}";
        }

        /// <summary>
        /// Update images for a branch of tree starting with the node whose FileState changed and walking up
        /// to the root calling <see cref="OnModelChanged"/> on each visited node.
        /// </summary>
        /// <param name="filesTreeNode"></param>
        internal static void UpdateImagesForTreeNode(FilesTreeNode filesTreeNode)
        {
            Assume.IsNotNull(filesTreeNode);

            do
            {
                filesTreeNode.OnModelChanged();
                filesTreeNode = filesTreeNode.ParentFTN;
            } while (filesTreeNode != null && filesTreeNode.ParentFTN != null);
        }
    }
}