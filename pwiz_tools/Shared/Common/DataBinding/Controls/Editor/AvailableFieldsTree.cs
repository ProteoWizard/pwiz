/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    /// <summary>
    /// Displays a tree view of available properties that can be added to a GridView
    /// </summary>
    public class AvailableFieldsTree : TreeView
    {
        private ColumnDescriptor _rootColumn;
        private bool _showAdvancedFields;
        private PropertyPath _sublistId = PropertyPath.Root;
        private ICollection<PropertyPath> _checkedColumns = new PropertyPath[0];
        private static readonly Image[] ImagelistImages =
        {
            Resources.DataColumn,
            Resources.TextColumn,
            Resources.NumberColumn,
            Resources.BoolColumn,
            Resources.LinkColumn,
            Resources.SublistColumn,
            Resources.PivotColumn,
        };
        private enum ImageIndexes
        {
            Unknown,
            Text,
            Number,
            Boolean,
            Link,
            Sublist,
            Pivot,
        }

        public AvailableFieldsTree()
        {
            ImageList = new ImageList
                {
                    TransparentColor = Color.Magenta,
                };
            ImageList.Images.AddRange(ImagelistImages);
            DrawMode = TreeViewDrawMode.OwnerDrawText;
        }
        [Browsable(false)]
        public ColumnDescriptor RootColumn
        {
            get
            {
                return _rootColumn;
            }
            set
            {
                if (Equals(_rootColumn, value))
                {
                    return;
                }
                _rootColumn = value;
                Nodes.Clear();
                if (_rootColumn.DataSchema.IsRootTypeSelectable(_rootColumn.PropertyType))
                {
                    var rootNode = new TreeNode();
                    SetColumnDescriptor(rootNode, _rootColumn);
                    Nodes.Add(rootNode);
                    EnsureChildren(rootNode);
                    rootNode.Expand();
                }
                else
                {
                    Nodes.AddRange(MakeChildNodes(_rootColumn).ToArray());
                    bool allChildrenAreCollections = true;
                    foreach (TreeNode node in Nodes)
                    {
                        allChildrenAreCollections = allChildrenAreCollections &&
                                                    null != GetTreeColumn(node).CollectionInfo;
                        EnsureChildren(node);
                    }
                    // If all of the nodes at the root are collections, then expand the first node
                    // so that the user has an easier time finding "Normal" columns (this makes it
                    // less confusing in Skyline when the user is presented only with "Proteins" and "Replicate")
                    if (Nodes.Count > 0 && allChildrenAreCollections)
                    {
                        EnsureChildren(Nodes[0]);
                        Nodes[0].Expand();
                    }
                }
            }
        }

        public bool ShowAdvancedFields
        {
            get
            {
                return _showAdvancedFields;
            }
            set
            {
                if (ShowAdvancedFields == value)
                {
                    return;
                }
                _showAdvancedFields = value;
                Nodes.Clear();
                var rootColumnOld = _rootColumn;
                _rootColumn = null;
                RootColumn = rootColumnOld;
            }
        }
        /// <summary>
        /// Returns the column associated with the node in the tree.
        /// This is the column which is the parent of the child nodes in
        /// the tree, and which gives the caption to the TreeNode.
        /// </summary>
        public ColumnDescriptor GetTreeColumn(TreeNode treeNode)
        {
            return ((NodeData)treeNode.Tag).TreeColumn;
        }

        /// <summary>
        /// Returns the column which should be added to the View corresponding.
        /// This is the same as the column returned by GetTreeColumn
        /// except in the case of Dictionaries.
        /// </summary>
        public ColumnDescriptor GetValueColumn(TreeNode treeNode)
        {
            return ((NodeData) treeNode.Tag).ValueColumn;
        }
        private void SetColumnDescriptor(TreeNode node, ColumnDescriptor columnDescriptor)
        {
            var nodeData = new NodeData(columnDescriptor);
            node.Tag = nodeData;
            node.Text = columnDescriptor.GetColumnCaption(ColumnCaptionType.localized);
            UpdateNode(node);
            node.Nodes.Clear();
            node.Nodes.Add(new TreeNode {Tag = NodeData.UninitializedTag});
        }

        protected override void OnBeforeExpand(TreeViewCancelEventArgs e)
        {
            base.OnBeforeExpand(e);
            if (e.Cancel)
            {
                return;
            }
            foreach (TreeNode child in e.Node.Nodes)
            {
                EnsureChildren(child);
            }
        }

        protected override void OnAfterExpand(TreeViewEventArgs e)
        {
            base.OnAfterExpand(e);
            UpdateNode(e.Node);
        }

        protected override void OnAfterCollapse(TreeViewEventArgs e)
        {
            base.OnAfterCollapse(e);
            UpdateNode(e.Node);
        }

        protected void EnsureChildren(TreeNode treeNode)
        {
            if (treeNode.Nodes.Count != 1 || treeNode.Nodes[0].Tag != NodeData.UninitializedTag)
            {
                return;
            }
            treeNode.Nodes.Clear();
            treeNode.Nodes.AddRange(MakeChildNodes(GetTreeColumn(treeNode)).ToArray());
        }

        protected IEnumerable<TreeNode> MakeChildNodes(ColumnDescriptor parentColumnDescriptor)
        {
            var result = new List<TreeNode>();
            foreach (var columnDescriptor in ListChildren(parentColumnDescriptor))
            {
                var isAdvanced = IsAdvanced(columnDescriptor, parentColumnDescriptor);
                var child = new TreeNode();
                if (isAdvanced)
                {
                    child.ForeColor = Color.Gray;
                }
                child.SelectedImageIndex = child.ImageIndex = (int) GetImageIndex(columnDescriptor);
                SetColumnDescriptor(child, columnDescriptor);
                result.Add(child);
            }
            return result;
        }

        private bool IsAdvanced(ColumnDescriptor columnDescriptor, ColumnDescriptor parent)
        {
            while (null != columnDescriptor && columnDescriptor.PropertyPath.StartsWith(parent.PropertyPath))
            {
                if (columnDescriptor.IsAdvanced)
                {
                    return true;
                }
                columnDescriptor = columnDescriptor.Parent;
            }
            return false;
        }

        private ImageIndexes GetImageIndex(ColumnDescriptor columnDescriptor)
        {
            if (null == columnDescriptor)
            {
                return 0;
            }
            if (null != columnDescriptor.CollectionInfo)
            {
                if (null != SublistId && SublistId.StartsWith(columnDescriptor.PropertyPath))
                {
                    return ImageIndexes.Sublist;
                }
                return ImageIndexes.Pivot;
            }
            var propertyType = columnDescriptor.PropertyType;
            if (null == propertyType)
            {
                return ImageIndexes.Unknown;
            }
            if (typeof (ILinkValue).IsAssignableFrom(propertyType))
            {
                return ImageIndexes.Link;
            }
            propertyType = columnDescriptor.DataSchema.GetWrappedValueType(propertyType);
            if (typeof (string) == propertyType)
            {
                return ImageIndexes.Text;
            }
            if (typeof (bool) == propertyType)
            {
                return ImageIndexes.Boolean;
            }
            if (propertyType.IsPrimitive)
            {
                return ImageIndexes.Number;
            }
            return ImageIndexes.Unknown;
        }

        private void UpdateNode(TreeNode node)
        {
            var columnDescriptor = GetTreeColumn(node);
            if (null == columnDescriptor)
            {
                return;
            }
            node.SelectedImageIndex = node.ImageIndex = (int) GetImageIndex(columnDescriptor);
            // Avoid firing Checked related events unless the actual checked state is changing.
            if (node.Checked != CheckedColumns.Contains(GetValueColumn(node).PropertyPath))
            {
                node.Checked = !node.Checked;
            }
            if (!node.Checked && CheckedColumns.Any(idPath => idPath.StartsWith(columnDescriptor.PropertyPath)))
            {
                node.BackColor = SystemColors.ControlLight;
            }
            else
            {
                node.BackColor = SystemColors.Window;
            }
        }

        protected override void OnDrawNode(DrawTreeNodeEventArgs e)
        {
            Font font = e.Node.NodeFont ?? e.Node.TreeView.Font;
            Color fore = e.Node.ForeColor;
            if (fore == Color.Empty) fore = e.Node.TreeView.ForeColor;
            if (e.Node == e.Node.TreeView.SelectedNode)
            {
                fore = SystemColors.HighlightText;
                e.Graphics.FillRectangle(SystemBrushes.Highlight, e.Bounds);
                ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, fore, SystemColors.Highlight);
                TextRenderer.DrawText(e.Graphics, e.Node.Text, font, e.Bounds, fore, TextFormatFlags.GlyphOverhangPadding);
            }
            else
            {
                e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Node.Text, font, e.Bounds, fore, TextFormatFlags.GlyphOverhangPadding);
            }
        }

        public IEnumerable<ColumnDescriptor> ListChildren(ColumnDescriptor parent)
        {
            var allChildren = ListAllChildren(parent);
            if (ShowAdvancedFields)
            {
                return allChildren;
            }
            return allChildren.Where(child => !IsAdvanced(child, parent));
        }

        private IList<ColumnDescriptor> ListAllChildren(ColumnDescriptor parent)
        {
            var result = new List<ColumnDescriptor>();
            if (parent.CollectionInfo != null && parent.CollectionInfo.IsDictionary)
            {
                if (ShowAdvancedFields)
                {
                    result.Add(parent.ResolveChild("Key")); // Not L10N
                }
                result.AddRange(ListAllChildren(parent.ResolveChild("Value"))); // Not L10N
                return result;
            }
            foreach (var child in parent.GetChildColumns())
            {
                var collectionColumn = child.GetCollectionColumn();
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (null != collectionColumn)
                {
                    result.Add(collectionColumn);
                }
                else
                {
                    result.Add(child);
                }
            }
            return result;
        }
        [Browsable(false)]
        public IEnumerable<PropertyPath> CheckedColumns
        {
            get
            {
                return _checkedColumns.ToArray();
            }
            set
            {
                var oldValue = _checkedColumns;
                var newValue = new HashSet<PropertyPath>(value);
                if (newValue.SetEquals(oldValue))
                {
                    return;
                }
                _checkedColumns = newValue;
                UpdateNodes(Nodes);
            }
        }

        public PropertyPath SublistId 
        { 
            get { return _sublistId; }
            set
            {
                if (Equals(SublistId, value))
                {
                    return;
                }
                _sublistId = value;
                UpdateNodes(Nodes);
            }
        }

        private void UpdateNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                UpdateNode(node);
                UpdateNodes(node.Nodes);
            }
        }
        public void SelectColumn(PropertyPath idPath)
        {
            var node = FindTreeNode(idPath, true);
            if (node == null)
            {
                return;
            }
            SelectedNode = node;
        }

        private TreeNode FindTreeNode(TreeNodeCollection nodes, PropertyPath idPath)
        {
            foreach (TreeNode node in nodes)
            {
                var columnDescriptor = GetTreeColumn(node);
                if (columnDescriptor != null)
                {
                    if (idPath.StartsWith(columnDescriptor.PropertyPath))
                    {
                        return node;
                    }
                }
            }
            return null;
        }
        public TreeNode FindTreeNode(PropertyPath idPath, bool create)
        {
            if (idPath == null)
            {
                return null;
            }
            TreeNodeCollection nodes = Nodes;
            while(true)
            {
                TreeNode node = FindTreeNode(nodes, idPath);
                if (node == null)
                {
                    return null;
                }
                if (GetTreeColumn(node).PropertyPath.Equals(idPath) || GetValueColumn(node).PropertyPath.Equals(idPath))
                {
                    return node;
                }
                if (create)
                {
                    EnsureChildren(node);
                }
                nodes = node.Nodes;
            }
        }

        class NodeData
        {
            public static readonly NodeData UninitializedTag = new NodeData();
            private NodeData()
            {
            }
            public NodeData(ColumnDescriptor treeColumn)
            {
                TreeColumn = treeColumn;
                if (treeColumn.CollectionInfo != null && treeColumn.CollectionInfo.IsDictionary)
                {
                    ValueColumn = treeColumn.ResolveChild("Value"); // Not L10N
                }
                ValueColumn = ValueColumn ?? TreeColumn;
            }
            public ColumnDescriptor TreeColumn { get; private set;}
            public ColumnDescriptor ValueColumn { get; private set; }
        }
    }
}
