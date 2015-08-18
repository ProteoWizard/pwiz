/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Finds <see cref="DocNode"/> elements with a certain quality, excluding
    /// child nodes with the same quality.
    /// </summary>
    public abstract class AbstractDocNodeFinder : AbstractFinder
    {
        public override FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            if (bookmarkEnumerator.CurrentChromInfo == null)
            {
                var nodePep = bookmarkEnumerator.CurrentDocNode as PeptideDocNode;
                if (nodePep != null)
                {
                    return IsMatch(nodePep) ? new FindMatch(DisplayName) : null;
                }
                var nodeGroup = bookmarkEnumerator.CurrentDocNode as TransitionGroupDocNode;
                if (nodeGroup != null)
                {
                    var nodeParent = bookmarkEnumerator.Document.FindNode(bookmarkEnumerator.IdentityPath.Parent)
                                     as PeptideDocNode;
                    return !IsMatch(nodeParent) && IsMatch(nodeGroup) ? new FindMatch(DisplayName) : null;
                }
                var nodeTran = bookmarkEnumerator.CurrentDocNode as TransitionDocNode;
                if (nodeTran != null)
                {
                    var nodeGrandParent = bookmarkEnumerator.Document.FindNode(bookmarkEnumerator.IdentityPath.Parent.Parent)
                                     as PeptideDocNode;
                    var nodeParent = bookmarkEnumerator.Document.FindNode(bookmarkEnumerator.IdentityPath.Parent)
                                     as TransitionGroupDocNode;
                    return !IsMatch(nodeGrandParent) && !IsMatch(nodeParent) && IsMatch(nodeParent, nodeTran) ? new FindMatch(DisplayName) : null;
                }
            }
            return null;            
        }

        protected virtual bool IsMatch(PeptideGroupDocNode nodePepGroup)
        {
            return false;
        }

        protected virtual bool IsMatch(PeptideDocNode nodePep)
        {
            return false;
        }

        protected virtual bool IsMatch(TransitionGroupDocNode nodeGroup)
        {
            return false;
        }

        protected virtual bool IsMatch(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            return false;
        }
    }
}