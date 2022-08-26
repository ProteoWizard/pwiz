/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.ElementLocators
{
    /// <summary>
    /// Handles converting <see cref="ElementLocator"/> objects into <see cref="ElementRef"/>.
    /// Also, maintains state in order to efficiently construct ElementRef objects from DocNode's etc.
    /// It can be slow to figure out the ElementRef of a DocNode, because it involves checking for duplicates among
    /// the siblings and potentially adding an "index=" attribute.
    /// </summary>
    public class ElementRefs
    {
        private static readonly ImmutableList<ElementRef> PROTOTYPES = ImmutableList.ValueOf(new ElementRef[]
        {
            DocumentRef.PROTOTYPE, MoleculeGroupRef.PROTOTYPE, MoleculeRef.PROTOTYPE, PrecursorRef.PROTOTYPE, TransitionRef.PROTOTYPE,
            ReplicateRef.PROTOTYPE, ResultFileRef.PROTOTYPE,
            MoleculeResultRef.PROTOTYPE, PrecursorResultRef.PROTOTYPE, TransitionResultRef.PROTOTYPE
        });
        private static readonly ImmutableList<NodeRef> NODEREFPROTOTYPES
            = ImmutableList.ValueOf(PROTOTYPES.OfType<NodeRef>());

        private static readonly IDictionary<string, ElementRef> _prototypes =
            PROTOTYPES.ToDictionary(element => element.ElementType);

        private readonly IDictionary<IdentityPath, NodeRef> _nodeRefs
            = new Dictionary<IdentityPath, NodeRef>();
        private readonly IDictionary<IdentityPath, NodeRef[]> _siblings
            = new Dictionary<IdentityPath, NodeRef[]>();

        public ElementRefs(SrmDocument document)
        {
            Document = document;
        }

        public SrmDocument Document { get; private set; }

        public NodeRef GetNodeRef(IdentityPath identityPath)
        {
            if (identityPath.IsRoot)
            {
                return DocumentRef.PROTOTYPE;
            }
            lock (this)
            {
                NodeRef nodeRef;
                if (_nodeRefs.TryGetValue(identityPath, out nodeRef))
                {
                    return nodeRef;
                }
                var parentIdentityPath = identityPath.Parent;
                DocNodeParent parentNode;
                try
                {
                    parentNode = (DocNodeParent)Document.FindNode(parentIdentityPath);
                    if (parentNode == null)
                    {
                        return null;
                    }
                }
                catch (IdentityNotFoundException)
                {
                    return null;
                }
                int childIndex = parentNode.FindNodeIndex(identityPath.Child);
                if (childIndex < 0)
                {
                    return null;
                }
                NodeRef[] siblings;
                if (!_siblings.TryGetValue(parentIdentityPath, out siblings))
                {
                    var parentRef = GetNodeRef(parentIdentityPath);
                    if (parentRef == null)
                    {
                        return null;
                    }
                    var siblingRef = NODEREFPROTOTYPES[identityPath.Length].ChangeParent(parentRef);
                    siblings = siblingRef.ListChildrenOfParent(Document).Cast<NodeRef>().ToArray();
                    _siblings.Add(parentIdentityPath, siblings);
                }
                var result = siblings[childIndex];
                _nodeRefs.Add(identityPath, result);
                return result;
            }
        }
        public static ElementRef FromObjectReference(ElementLocator objectReference)
        {
            var prototype = _prototypes[objectReference.Type];
            return prototype.ChangeElementLocator(objectReference);
        }
    }
}
