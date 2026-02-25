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
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Files;

// ReSharper disable WrongIndentSize
namespace pwiz.Skyline.Controls.FilesTree
{
    public enum FileState { available, missing, not_initialized, in_memory }

    // CONSIDER: customize behavior in subclasses. Overloading FilesTreeNode won't scale long-term.
    public class FilesTreeNode : TreeNodeMS, ITipProviderWithText
    {
        private FileModel _model;

        internal static FilesTreeNode CreateNode(FileModel model)
        {
            Assume.IsNotNull(model);

            return new FilesTreeNode(model);
        }

        private FilesTreeNode(FileModel model)
        {
            FileState = FileState.not_initialized;
            Model = model;
        }

        public FileModel Model
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
            if (!Equals(Name, Model.Name))
                Name = Model.Name;
            if (!Equals(Text, Model.DisplayText))
                Text = Model.DisplayText;

            // Special handling for .sky file - do not change icon if child files are missing
            int imageIndexNew;
            if (Model is SkylineFile)
            {
                imageIndexNew = FileState == FileState.missing ? (int)ImageMissing : (int)ImageAvailable;
            }
            else
            {
                // CONSIDER: use a separate state flag to track whether 1+ child nodes are missing files instead of inferring child state from the parent's ImageIndex
                var anyChildMissingFile =
                    Nodes.Cast<FilesTreeNode>().Any(node => node.FileState == FileState.missing) ||
                    Nodes.Cast<FilesTreeNode>().Any(node => node.ImageIndex == (int)ImageId.file_missing || node.ImageIndex == (int)ImageId.folder_missing || node.ImageIndex == (int)ImageId.replicate_missing);

                if (anyChildMissingFile || FileState == FileState.missing)
                    imageIndexNew = (int)ImageMissing;
                // CONSIDER: use a different icon for in-memory files, maybe grey out the available icon? 
                //           for now, use the available icon.
                else if (FileState == FileState.in_memory)
                    imageIndexNew = (int)ImageAvailable;
                else
                    imageIndexNew = (int)ImageAvailable;
            }
            if (ImageIndex != imageIndexNew)
                ImageIndex = imageIndexNew;
        }

        /// <summary>
        /// See if a file is available locally.
        /// </summary>
        /// <returns>true if a local file is available (on disk or in memory). false otherwise.</returns>
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

        private static bool? _showDebugTipText;

        /// <summary>
        /// Testing hook to force showing debug tip text even when debugger is not attached.
        /// </summary>
        public static bool ShowDebugTipText
        {
            get => _showDebugTipText ?? Debugger.IsAttached;
            set => _showDebugTipText = value;
        }

        public string TipText
        {
            get
            {
                using var rt = new RenderTools();
                return GetTipTable(rt).ToString();
            }
        }

        private TableDesc GetTipTable(RenderTools rt)
        {
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

                // If saved file path and local file path are the same: only show "File Path: c:\foo\bar"
                // Use red font if local file path is unavailable
                if (string.Compare(FilePath, LocalFilePath, StringComparison.Ordinal) == 0)
                {
                    if (FileState == FileState.available || FileState == FileState.in_memory)
                        customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_FilePath, FilePath, rt);
                    else
                        TooltipNewRowWithRedValue(FilesTreeResources.FilesTree_TreeNode_Tooltip_FilePath, FilePath, customTable, rt);
                }
                // Otherwise, show both "Saved File Path: c:\abc\def" and "Local File Path: c:\foo\bar"
                // Use red font if local file path is unavailable
                else
                {
                    customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_SavedFilePath, FilePath, rt);

                    if (FileState == FileState.available || FileState == FileState.in_memory)
                        customTable.AddDetailRow(FilesTreeResources.FilesTree_TreeNode_Tooltip_LocalFilePath, LocalFilePath, rt);
                    else
                        TooltipNewRowWithRedValue(FilesTreeResources.FilesTree_TreeNode_Tooltip_LocalFilePath, FilesTreeResources.FilesTree_TreeNode_Tooltip_FileMissing, customTable, rt);
                }
            }

            // When debugging, add more info to each node's tooltip. 
            if (ShowDebugTipText)
            {
                TooltipNewRowWithText(@"     ", customTable, rt);
                TooltipNewRowWithText(@"====================", customTable, rt);
                customTable.AddDetailRow(@"Debug Info", @"(only visible when debugger attached)", rt);
                TooltipNewRowWithText(@"     ", customTable, rt);   

                customTable.AddDetailRow(@"FileName", FileName, rt);
                customTable.AddDetailRow(@"FilePath", FilePath, rt);

                TooltipNewRowWithText(@"     ", customTable, rt);
                customTable.AddDetailRow(@"Expect to find a local file?", $@"{(Model.IsBackedByFile ? @"Yes" : @"No")}", rt);
                if (FileState == FileState.available || FileState == FileState.in_memory)
                {
                    var value = FileState == FileState.available ? @"Yes, in local storage" : @"Yes, in memory";
                    customTable.AddDetailRow(@"Found local file?", value, rt);
                }
                else
                {
                    customTable.AddDetailRow(@"Found local file?", @"No", rt);
                }
                customTable.AddDetailRow(@"FileState", FileState.ToString(), rt);
                TooltipNewRowWithText(@"     ", customTable, rt);

                if (Model.IsBackedByFile) 
                {
                    if (FileState == FileState.available || FileState == FileState.in_memory)
                    {
                        customTable.AddDetailRow(@"LocalFilePath", LocalFilePath, rt);
                    }
                    else
                    {
                        TooltipNewRowWithRedValue(@"LocalFilePath", LocalFilePath, customTable, rt);
                    }
                }

                // Add more info to the .sky file
                if (Model is SkylineFile)
                {
                    TooltipNewRowWithText(@"     ", customTable, rt);

                    var monitoredDirectoryPaths = FilesTree.MonitoredDirectories();
                    TooltipNewRowWithText($@"Monitoring {monitoredDirectoryPaths.Count} directories:", customTable, rt);
                    for (var i = 0; i < monitoredDirectoryPaths.Count; i++) {
                        customTable.AddDetailRow($@"  ({i}) Directory: ", monitoredDirectoryPaths[i], rt);
                    }
                }

                if (Model is SkylineAuditLog || Model is SkylineFile)
                {
                    TooltipNewRowWithText(@"     ", customTable, rt);

                    var auditLogEnabledByTestFramework = Program.FunctionalTest && !AuditLogList.IgnoreTestChecks;
                    customTable.AddDetailRow(@"Audit logging enabled by the test framework?", $@"{(auditLogEnabledByTestFramework ? @"Yes" : @"No")}", rt);
                }

                // CONSIDER: add SrmDocument.RevisionIndex to FileModel
                // customTable.AddDetailRow(@"Document revision", $@"{Model.DocumentRevisionIndex}", rt);
            }

            return customTable;
        }

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            using var rt = new RenderTools();
            var customTable = GetTipTable(rt);
            var size = customTable.CalcDimensions(g);
            if (draw)
                customTable.Draw(g);

            return new Size((int)size.Width + 4, (int)size.Height + 4);
        }

        public bool SupportsRename()
        {
            return Model is IFileRenameable;
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

        /// <summary>
        /// Draw bounds for this tree node - <see cref="TreeNode.Bounds"/> in green and <see cref="TreeNodeMS.BoundsMS"/> in red.
        /// This is useful for debugging click target issues. A typical problem is <see cref="TreeViewMS"/> thinking a node is wider
        /// than <see cref="TreeView"/> when picking up nodes during drag-and-drop or a long-click to edit a node's label.
        /// Increasing the Skyline's font size above "default" increases the discrepancy of the widths of between those
        /// bounding boxes.
        /// </summary>
        /// <param name="g"><see cref="Graphics"/> instance used to render the tree node</param>
        /// <param name="rightEdge"></param>
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

        private static void TooltipNewRowWithText(string text, TableDesc table, RenderTools rt)
        {
            var cell = new CellDesc(text, rt)
            {
                Font = rt.FontBold
            };
            table.Add(new RowDesc { cell });
        }

        private static void TooltipNewRowWithRedValue(string label, string value, TableDesc table, RenderTools rt)
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
    }
}
