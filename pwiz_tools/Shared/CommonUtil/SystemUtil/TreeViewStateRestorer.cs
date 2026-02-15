/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace pwiz.Common.SystemUtil
{
    public class TreeViewStateRestorer
    {
        public TreeViewStateRestorer(TreeView tree)
        {
            Tree = tree;
        }

        protected TreeView Tree
        {
            get;
        }

        /// <summary>
        /// Generates a persistent string storing information about the expansion and selection
        /// of nodes as well as the vertical scrolling of the form, separated by pipes
        /// </summary>
        public string GetPersistentString()
        {
            StringBuilder result = new StringBuilder();
            result.Append(GenerateExpansionString(Tree.Nodes)).Append('|');
            
            result.Append(GenerateSelectionString()).Append('|');

            result.Append(GenerateScrollString());
            return result.ToString();
        }

        /// <summary>
        /// The expansion string stores the indices of expanded nodes when called in the format
        /// a(b(c)), where a is the top level node as an integer, b is a child of a, and c
        /// is a child of b, etc. Multiple nodes and their children are stored as a comma-separated
        /// string, e.g. 0(1(0,1),2(0)),3
        /// </summary>
        private static string GenerateExpansionString(IEnumerable nodes)
        {
            int index = 0;
            StringBuilder result = new StringBuilder();
            foreach (TreeNode parent in nodes)
            {
                if (parent.IsExpanded)
                {
                    if (result.Length > 0)
                        result.Append(',');
                    result.Append(index);
                    string children = GenerateExpansionString(parent.Nodes);
                    if (children.Length != 0)
                    {
                        result.Append('(').Append(children).Append(')');
                    }
                }
                index++;
            }
            return result.ToString();
        }

        /// <summary>
        /// Gets the index of the selected node in a single-select TreeView according to the
        /// visual order of the nodes in the tree
        /// </summary>
        protected virtual string GenerateSelectionString()
        {
            int index = 0;
            foreach (TreeNode node in VisibleNodes)
            {
                if (node.IsSelected)
                    return index.ToString();
                index++;
            }
            return @"0";
        }


        /// <summary>
        /// The scroll string stores the numerical index of the first visible node in the form.
        /// The index corresponds to the location in the visual order of nodes in the form
        /// </summary>
        /// <returns></returns>
        private int GenerateScrollString()
        {
            int index = 0;
            foreach (TreeNode node in VisibleNodes)
            {
                if (node.IsVisible)
                    return index;
                index++;
            }
            return 0;
        }

        /// <summary>
        /// Restores the expansion and selection of the tree, and sets the top node for scrolling
        /// to be updated after all resizing has occurred
        /// </summary>
        public virtual void RestoreExpansionAndSelection(string persistentString)
        {
            if (!string.IsNullOrEmpty(persistentString))
            {
                string[] stateStrings = persistentString.Split('|');

                // check that the .view file will have the necessary information to rebuild the tree
                if (stateStrings.Length > 2)
                {
                    try
                    {
                        Tree.BeginUpdate();

                        ExpandTreeFromString(stateStrings[0]);

                        SelectTreeFromString(stateStrings[1]);
                        NextTopNode = GetTopNodeFromString(stateStrings[2]);
                    }
                    catch (FormatException)
                    {
                        // Ignore and give up
                    }
                    finally
                    {
                        Tree.EndUpdate();
                    }
                }
            }
        }

        /// <summary>
        /// Expands the tree from the persistent string data
        /// </summary>
        protected void ExpandTreeFromString(string persistentString)
        {
            IEnumerator<char> dataEnumerator = persistentString.GetEnumerator();
            TraverseTreeFromString(Tree.Nodes, dataEnumerator, node => node.Expand());
        }

        /// <summary>
        /// Traverses the tree structure described by the TreeState string, applying an action to each node.
        /// Used for both expanding nodes (action = Expand) and validating compatibility (action = no-op).
        /// </summary>
        /// <param name="nodes">The TreeNodeCollection to traverse</param>
        /// <param name="data">Enumerator for the TreeState string</param>
        /// <param name="action">Action to apply to each node (e.g., node => node.Expand() or node => { })</param>
        /// <returns>True if traversal completed successfully, false if invalid node index encountered</returns>
        private static bool TraverseTreeFromString(TreeNodeCollection nodes, IEnumerator<char> data, Action<TreeNode> action)
        {
            bool finishedEnumerating = !data.MoveNext();
            int currentNode = 0;
            while (!finishedEnumerating)
            {
                char value = data.Current;
                switch (value)
                {
                    case ',':
                        finishedEnumerating = !data.MoveNext();
                        break;
                    case '(':
                        // Before descending, check if current node is valid
                        if (currentNode >= nodes.Count)
                            return false;
                        finishedEnumerating = TraverseTreeFromString(nodes[currentNode].Nodes, data, action);
                        break;
                    case ')':
                        return !data.MoveNext();
                    default: // value must be an integer
                        StringBuilder dataIndex = new StringBuilder();
                        dataIndex.Append(value);
                        finishedEnumerating = !data.MoveNext();

                        // enumerate until the next element is not an integer
                        while (!finishedEnumerating && data.Current != ',' && data.Current != '(' && data.Current != ')')
                        {
                            dataIndex.Append(data.Current);
                            finishedEnumerating = !data.MoveNext();
                        }

                        currentNode = int.Parse(dataIndex.ToString());

                        // if invalid node in tree, return false
                        if (currentNode >= nodes.Count)
                            return false;
                        
                        // Apply the action to the node (e.g., expand it)
                        action(nodes[currentNode]);
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// Re-selects tree nodes from the persistent string data
        /// </summary>
        protected virtual void SelectTreeFromString(string persistentString)
        {
            IList<TreeNode> visualOrder = VisibleNodes.ToArray();
            int nodeCount = visualOrder.Count;
            string[] selections = persistentString.Split(',');

            // select first element separately, returning if it is not a valid node
            int selectedIndex = int.Parse(selections[0]);
            if (selectedIndex < 0 || selectedIndex >= nodeCount)
                return;
            Tree.SelectedNode = visualOrder[selectedIndex];
        }

        /// <summary>
        /// Sets the top node (for scrolling) for update when the tree has finished resizing
        /// </summary>
        protected TreeNode GetTopNodeFromString(string persistentString)
        {
            IList<TreeNode> nodes = VisibleNodes.ToArray();
            int index = int.Parse(persistentString);
            if (0 > index || index >= nodes.Count)
                return null;
            return nodes[index];
        }

        public TreeNode NextTopNode { get; protected set; }

        /// <summary>
        /// Updates the top node in order to establish the correct scrolling of the tree. This should
        /// not be called until all resizing of the tree has occurred
        /// </summary>
        public void UpdateTopNode()
        {
            Tree.TopNode = NextTopNode ?? Tree.TopNode;
        }

        /// <summary>
        /// Generates the visual order of nodes as they appear in the tree
        /// </summary>
        protected IEnumerable<TreeNode> VisibleNodes
        {
            get
            {
                for (TreeNode node = Tree.Nodes.Count > 0 ? Tree.Nodes[0] : null; node != null; node = node.NextVisibleNode)
                    yield return node;
            }
        }

        /// <summary>
        /// Generates a TreeState string for a given path by finding the actual nodes in the tree
        /// and generating TreeState from their indices. This is used for InitialPath where we can't
        /// assume nodes are at index 0.
        /// </summary>
        /// <param name="pathSegments">Array of path segment names to find in the tree</param>
        /// <param name="startNode">The TreeNode to start searching from (typically root)</param>
        /// <returns>TreeState string in format "expansion|selection|scroll", or null if path not found</returns>
        public string GenerateTreeStateForPath(string[] pathSegments, TreeNode startNode)
        {
            if (pathSegments == null || pathSegments.Length == 0 || startNode == null)
                return null;

            // Find the path by traversing the tree
            var pathNodes = new List<TreeNode> { startNode };
            TreeNode currentNode = startNode;

            foreach (var segment in pathSegments)
            {
                if (string.IsNullOrEmpty(segment))
                    continue;

                // Find the child node with matching name
                TreeNode foundNode = null;
                foreach (TreeNode child in currentNode.Nodes)
                {
                    if (child.Text.Equals(segment, StringComparison.Ordinal))
                    {
                        foundNode = child;
                        break;
                    }
                }

                if (foundNode == null)
                    return null; // Path segment not found

                pathNodes.Add(foundNode);
                currentNode = foundNode;
            }

            // Generate expansion string from the actual node indices
            // We expand all nodes in the path except the last one (which is selected)
            var expansion = new StringBuilder();
            
            // Build expansion string by finding indices at each level
            // For path "A/B/C", we expand A and B, then select C
            // Format: A_index(B_index(C_index))
            for (int i = 0; i < pathNodes.Count; i++)
            {
                TreeNode node = pathNodes[i];
                TreeNodeCollection parentCollection = i == 0 ? Tree.Nodes : pathNodes[i - 1].Nodes;

                // Find the index of this node in its parent's collection
                int nodeIndex = parentCollection.IndexOf(node);
                if (nodeIndex < 0)
                    return null; // Should not happen

                if (i > 0)
                    expansion.Append('(');
                expansion.Append(nodeIndex);
            }

            // Close all the parentheses (one for each level after root)
            for (int i = 1; i < pathNodes.Count; i++)
            {
                expansion.Append(')');
            }

            // Expand all nodes in the path (except the last, which will be selected)
            // This ensures they're visible for calculating the selection index
            for (int i = 0; i < pathNodes.Count - 1; i++)
            {
                if (!pathNodes[i].IsExpanded && pathNodes[i].Nodes.Count > 0)
                {
                    pathNodes[i].Expand();
                }
            }

            // Calculate selection index by counting visible nodes up to the selected node
            int index = 0;
            int selectionIndex = 0;
            foreach (TreeNode visibleNode in VisibleNodes)
            {
                if (visibleNode == pathNodes[pathNodes.Count - 1])
                {
                    selectionIndex = index;
                    break;
                }
                index++;
            }
            
            int scrollPosition = Math.Max(0, selectionIndex - 1);

            return $@"{expansion}|{selectionIndex}|{scrollPosition}";
        }

        /// <summary>
        /// Validates if a TreeState string is compatible with the current TreeView structure.
        /// Checks that all node indices referenced in the TreeState are within bounds of the actual tree.
        /// </summary>
        /// <param name="treeState">The TreeState string to validate (format: "expansion|selection|scroll")</param>
        /// <returns>True if TreeState is compatible with the current tree, false if it should be discarded</returns>
        public bool IsTreeStateCompatible(string treeState)
        {
            if (string.IsNullOrEmpty(treeState))
                return true; // Empty state is always compatible
            
            string[] parts = treeState.Split('|');
            if (parts.Length < 3)
                return true; // Invalid format - consider compatible (let RestoreExpansionAndSelection handle it)
            
            string expansionString = parts[0];
            string selectionString = parts[1];
            string scrollString = parts[2];
            
            // Validate expansion string by traversing the tree structure
            // Use a no-op action to just check bounds without modifying the tree
            IEnumerator<char> expansionEnumerator = expansionString.GetEnumerator();
            bool expansionValid = TraverseTreeFromString(Tree.Nodes, expansionEnumerator, node => { });
            
            if (!expansionValid)
                return false;
            
            // Validate selection index - check if it's within bounds of visible nodes
            // We need to expand the tree according to the expansion string first to get accurate visible node count
            // But we can't modify the tree during validation, so we'll do a simplified check
            // The actual validation happens in SelectTreeFromString which returns early if out of bounds
            if (!string.IsNullOrEmpty(selectionString))
            {
                if (!int.TryParse(selectionString, out int selectionIndex) || selectionIndex < 0)
                    return false;
                // We can't accurately count visible nodes without expanding, but negative indices are definitely invalid
            }
            
            // Validate scroll index similarly
            if (!string.IsNullOrEmpty(scrollString))
            {
                if (!int.TryParse(scrollString, out int scrollIndex) || scrollIndex < 0)
                    return false;
            }
            
            return true;
        }
    }
}
