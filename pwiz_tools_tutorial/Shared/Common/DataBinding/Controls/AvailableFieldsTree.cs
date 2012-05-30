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

namespace pwiz.Common.DataBinding.Controls
{
    /// <summary>
    /// Displays a tree view of available properties that can be added to a GridView
    /// </summary>
    public class AvailableFieldsTree : TreeView
    {
        private ColumnDescriptor _rootColumn;
        private bool _showAdvancedFields;
        private ICollection<IdentifierPath> _checkedColumns = new IdentifierPath[0];

        public AvailableFieldsTree()
        {
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
                if (_rootColumn.IsSelectable)
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
                    foreach (TreeNode node in Nodes)
                    {
                        EnsureChildren(node);
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
            node.Text = columnDescriptor.DisplayName;
            node.Checked = _checkedColumns.Contains(nodeData.ValueColumn.IdPath);
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
                var isAdvanced = columnDescriptor.IsAdvanced;
                if (!ShowAdvancedFields && isAdvanced)
                {
                    continue;
                }
                var child = new TreeNode();
                if (isAdvanced)
                {
                    child.ForeColor = Color.Gray;
                }
                SetColumnDescriptor(child, columnDescriptor);
                result.Add(child);
            }
            return result;
        }

        protected IList<ColumnDescriptor> ListChildren(ColumnDescriptor parent)
        {
            var result = new List<ColumnDescriptor>();
            if (parent.CollectionInfo != null && parent.CollectionInfo.IsDictionary)
            {
                result.Add(new ColumnDescriptor(parent, "Key"));
                result.AddRange(ListChildren(new ColumnDescriptor(parent, "Value")));
                return result;
            }
            foreach (var child in parent.GetChildColumns())
            {
                var collectionInfo = parent.DataSchema.GetCollectionInfo(child.PropertyType);
                if (collectionInfo != null)
                {
                    result.Add(new ColumnDescriptor(child, collectionInfo));
                }
                else
                {
                    result.Add(child);
                }
            }
            return result;
        }
        [Browsable(false)]
        public IEnumerable<IdentifierPath> CheckedColumns
        {
            get
            {
                return _checkedColumns.ToArray();
            }
            set
            {
                var oldValue = _checkedColumns;
                var newValue = new HashSet<IdentifierPath>(value);
                if (newValue.SetEquals(oldValue))
                {
                    return;
                }
                foreach (var idPath in oldValue)
                {
                    if (newValue.Contains(idPath))
                    {
                        continue;
                    }
                    var node = FindTreeNode(idPath, false);
                    if (node != null)
                    {
                        node.Checked = false;
                    }
                }
                foreach (var idPath in newValue)
                {
                    if (oldValue.Contains(idPath))
                    {
                        continue;
                    }
                    var node = FindTreeNode(idPath, false);
                    if (node != null)
                    {
                        node.Checked = true;
                    }
                }
                _checkedColumns = newValue;
            }
        }
        public void SelectColumn(IdentifierPath idPath)
        {
            var node = FindTreeNode(idPath, true);
            if (node == null)
            {
                return;
            }
            SelectedNode = node;
        }

        private TreeNode FindTreeNode(TreeNodeCollection nodes, IdentifierPath idPath)
        {
            foreach (TreeNode node in nodes)
            {
                var columnDescriptor = GetTreeColumn(node);
                if (columnDescriptor != null)
                {
                    if (idPath.StartsWith(columnDescriptor.IdPath))
                    {
                        return node;
                    }
                }
            }
            return null;
        }
        public TreeNode FindTreeNode(IdentifierPath idPath, bool create)
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
                if (GetTreeColumn(node).IdPath.Equals(idPath) || GetValueColumn(node).IdPath.Equals(idPath))
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
            public static NodeData UninitializedTag = new NodeData();
            private NodeData()
            {
            }
            public NodeData(ColumnDescriptor treeColumn)
            {
                TreeColumn = treeColumn;
                if (treeColumn.CollectionInfo != null && treeColumn.CollectionInfo.IsDictionary)
                {
                    ValueColumn = treeColumn.ResolveChild("Value");
                }
                ValueColumn = ValueColumn ?? TreeColumn;
            }
            public ColumnDescriptor TreeColumn { get; private set;}
            public ColumnDescriptor ValueColumn { get; private set; }
        }
    }
}
