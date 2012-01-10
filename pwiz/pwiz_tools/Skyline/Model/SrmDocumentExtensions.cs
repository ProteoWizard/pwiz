/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

namespace pwiz.Skyline.Model
{
    public static class SrmDocumentExtensions
    {
        /// <summary>
        /// Removes all nodes that are not listed in a set to preserve, or which
        /// contain a node that is listed in the set to preserve.  Preserved nodes
        /// which contain no other preserved nodes preserve all their children.
        /// </summary>
        /// <param name="document">The document to be modified</param>
        /// <param name="preserveNodes">Nodes to preserve</param>
        /// <returns>A new copy of the document with preserved children, or an empty
        /// document, if nothing was preserved</returns>
        public static SrmDocument RemoveAllBut(this SrmDocument document, IEnumerable<DocNode> preserveNodes)
        {
            var preserveIndexes = new HashSet<int>();
            foreach (var node in preserveNodes)
                preserveIndexes.Add(node.Id.GlobalIndex);

            return (SrmDocument)(RemoveAllBut(document, preserveIndexes) ??
                                 // If nothing was preserved, return an empty document
                                 document.ChangeChildrenChecked(new DocNode[0]));
        }

        /// <summary>
        /// Removes all nodes that are not listed in a set to preserve, or which
        /// contain a node that is listed in the set to preserve.  Preserved nodes
        /// which contain no other preserved nodes preserve all their children.
        /// </summary>
        /// <param name="node">The node to be modified</param>
        /// <param name="preserveIndexes">The GlobalIndex values of the nodes to preserve</param>
        /// <returns>A new instance of this node with nodes removed</returns>
        public static DocNode RemoveAllBut(this DocNode node, ICollection<int> preserveIndexes)
        {
            bool preserve = preserveIndexes.Contains(node.Id.GlobalIndex);

            // Recursion stopping condition: non-parent nodes are included only based
            // on whether they are in the set to preserve
            var nodeParent = node as DocNodeParent;
            if (nodeParent == null)
                return (preserve ? node : null);

            // Rebuild the list of children based on the preserveIndexes
            var listNewChildren = new List<DocNode>();
            foreach (var nodeChild in nodeParent.Children)
            {
                // Recurse into children and add any that include themselves
                var nodeNew = nodeChild.RemoveAllBut(preserveIndexes);
                if (nodeNew != null)
                    listNewChildren.Add(nodeNew);
            }
            // If some of the children were included by virtue of their being in the
            // set to preserve, then reset the children of this parent to only those.
            if (listNewChildren.Count > 0)
            {
                return nodeParent.ChangeChildrenChecked(listNewChildren);
            }
                // Otherwise, if this node itself is to be preserved, then include all of
                // its children
            else if (preserve)
                return node;
            // Skip this node and allow the parent to decide
            return null;
        }
    }
}