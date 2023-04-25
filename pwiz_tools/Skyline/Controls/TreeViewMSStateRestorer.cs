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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Controls;

namespace pwiz.Skyline.Controls
{
    public class TreeViewMSStateRestorer : TreeViewStateRestorerBase<TreeViewMS>
    {
        
        public TreeViewMSStateRestorer(TreeViewMS tree) : base(tree)
        {
        }


        /// <summary>
        /// <para>The selection string stores which nodes are selected in the graph. The first element
        /// is a single integer representing which node "the" selected node of the underlying TreeView.
        /// The remaining comma-separated elements in the string represent the indices of nodes
        /// that are selected according to the visual order of the nodes</para> 
        ///
        /// <para>These selections can be a single element (e.g. 1), a range (e.g. 1-7) or a disjoint selection
        /// consisting of multiple single elements and/or ranges (e.g. 1,3-6,8)</para>
        /// </summary>
        protected override string GenerateSelectionString()
        {
            StringBuilder selectedRanges = new StringBuilder();

            int index = 0;
            int rangeStart = -1;
            int prevSelection = -1;
            int selectedIndex = -1;

            foreach (TreeNodeMS node in VisibleNodes)
            {
                if (node.IsInSelection)
                {
                    if (rangeStart == -1)
                    {
                        rangeStart = index;
                    }
                    else if (index != prevSelection + 1)
                    {
                        AppendRange(selectedRanges, rangeStart, prevSelection);
                        rangeStart = index;
                    }
                    prevSelection = index;
                }

                // insert the TreeView selected node at the front of the string
                if (node.IsSelected)
                    selectedIndex = index;
                index++;
            }

            // complete any selection(s) that occur at the end of the tree
            if (rangeStart != -1)
            {
                AppendRange(selectedRanges, rangeStart, prevSelection);
            }

            return selectedIndex + @"," + selectedRanges;
        }

        private static void AppendRange(StringBuilder selectedRanges, int rangeStart, int prevSelection)
        {
            if (selectedRanges.Length > 0)
                selectedRanges.Append(',');

            if (rangeStart == prevSelection)
                selectedRanges.Append(rangeStart);
            else
                selectedRanges.AppendFormat(@"{0}-{1}", rangeStart, prevSelection);
        }


        /// <summary>
        /// Restores the expansion and selection of the tree, and sets the top node for scrolling
        /// to be updated after all resizing has occured
        /// </summary>
        public override void RestoreExpansionAndSelection(string persistentString)
        {
            if (!string.IsNullOrEmpty(persistentString))
            {
                string[] stateStrings = persistentString.Split('|');

                // check that the .view file will have the necessary information to rebuild the tree
                if (stateStrings.Length > 2)
                {

                    try
                    {
                        _tree.BeginUpdate();

                        _tree.AutoExpandSingleNodes = false;

                        ExpandTreeFromString(stateStrings[0]);

                        _tree.AutoExpandSingleNodes = true;

                        SelectTreeFromString(stateStrings[1]);
                        NextTopNode = GetTopNodeFromString(stateStrings[2]);

                        _tree.RestoredFromPersistentString = true;
                    }
                    catch (FormatException)
                    {
                        // Ignore and give up
                    }
                    finally
                    {
                        _tree.EndUpdate();
                        _tree.AutoExpandSingleNodes = true;
                    }
                }
            }
        }


        /// <summary>
        /// Reselects tree nodes from the persistent string data
        /// </summary>
        protected override void SelectTreeFromString(string persistentString)
        {
            IList<TreeNode> visualOrder = VisibleNodes.ToArray();
            int nodeCount = visualOrder.Count;
            string[] selections = persistentString.Split(',');

            // select first element separately, returning if it is not a valid node
            int selectedIndex = int.Parse(selections[0]);
            if (selectedIndex < 0 || selectedIndex >= nodeCount)
                return;
            _tree.SelectedNode = visualOrder[selectedIndex];

            for (int i = 1; i < selections.Length; i++)
            {
                string selection = selections[i];
                if (selection.Contains(@"-")) // when true, the string represents a range and not a single element
                {
                    string[] range = selection.Split('-');
                    int start = Math.Min(nodeCount - 1, Math.Max(0, int.Parse(range[0])));
                    int end = Math.Min(nodeCount - 1, Math.Max(0, int.Parse(range[1])));
                    for (int j = start; j <= end; j++)
                    {
                        _tree.SelectNode((TreeNodeMS)visualOrder[j], true);
                    }
                }
                else // the string represents a single element
                {
                    int index = int.Parse(selection);
                    if (0 > index || index >= nodeCount)
                        return;
                    _tree.SelectNode((TreeNodeMS)visualOrder[index], true);
                }
            }
        }
    }
}
