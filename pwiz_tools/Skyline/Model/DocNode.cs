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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Base class for a node in the Skyline document heirarchy.  All DocNode
    /// derrived classes must be immutable, and may not contain direct parent
    /// node references.  In an immutable document architecture parent references
    /// force new copies of all children every time a new copy of the parent
    /// is created.
    /// 
    /// If access to the parent node is required, instead use references to
    /// the <see cref="Id"/> properties of the parents to aid in node lookup
    /// in the document tree.
    /// </summary>
    public abstract class DocNode : Immutable
    {
        protected DocNode(Identity id)
            : this(id, Annotations.Empty)
        {
        }

        protected DocNode(Identity id, Annotations annotations)
        {
            Id = id;
            Annotations = annotations;
        }

        /// <summary>
        /// The core <see cref="Identity"/> for this node, which will not change
        /// as properties on the node itself are changed by cloning it, and setting
        /// the new values on the clone.
        /// </summary>
        public Identity Id { get; private set; }

        public Annotations Annotations { get; private set; }

        public abstract AnnotationDef.AnnotationTarget AnnotationTarget { get; }
        /// <summary>
        /// User supplied comment of this node.  All <see cref="DocNode"/> objects
        /// in the document support user notes.
        /// </summary>
        public string Note { get { return Annotations.Note;} }

        public string NoteMark { get { return Annotations.IsEmpty ? "" : "*"; } }

        /// <summary>
        /// Returns a clone of this with a different property value.
        /// </summary>
        /// <returns>New instance</returns>
        public DocNode ChangeAnnotations(Annotations annotations)
        {
            return ChangeProp(ImClone(this), im => im.Annotations = annotations);            
        }

        /// <summary>
        /// Convenience function for checking <see cref="Identity"/> equality of
        /// this with another <see cref="DocNode"/>.  The <see cref="object.ReferenceEquals"/>
        /// function is used, because not all <see cref="Identity"/> objects support
        /// content equality.  It is also valid to check equality of the
        /// <see cref="Identity.GlobalIndex"/> values for two identies, and this is
        /// the perfered method of using a <see cref="Identity"/> as a key in a map.
        /// </summary>
        /// <param name="node">Other node to compare for <see cref="Identity"/> equality</param>
        /// <returns>True if the <see cref="Identity"/> references for both nodes are reference equal</returns>
        public bool EqualsId(DocNode node)
        {
            return ReferenceEquals(Id, node.Id);
        }

        #region object overrides

        public bool Equals(DocNode obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.Id, Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (DocNode)) return false;
            return Equals((DocNode) obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        #endregion
    }

    /// <summary>
    /// Base clase for a node with associated child nodes in the Skyline
    /// document hierarchy.  Since the Skyline document is immutable, access
    /// to the child list is made at consturctor time, or through functions
    /// which return a copy of the parent node itself with a modified copy
    /// of the list.
    /// </summary>
    public abstract class DocNodeParent : DocNode
    {
        private ReadOnlyCollection<DocNode> _children;
        private IList<int> _nodeCountStack;

        /// <summary>
        /// Constructor for all cases other than serialization support, resulting
        /// in a complete instance of a node.
        /// </summary>
        /// <param name="id">The <see cref="Identity"/> object for this node</param>
        /// <param name="children">Children of this node</param>
        protected DocNodeParent(Identity id, IList<DocNode> children)
            : this(id, Annotations.Empty, children, true)
        {
        }

        /// <summary>
        /// Constructor for all cases other than serialization support, resulting
        /// in a complete instance of a node.
        /// </summary>
        /// <param name="id">The <see cref="Identity"/> object for this node</param>
        /// <param name="annotations">Annotations for this node</param>
        /// <param name="children">Children of this node</param>
        /// <param name="autoManageChildren">Whether children should be added and removed when the settings change</param>
        protected DocNodeParent(Identity id, Annotations annotations, IList<DocNode> children, bool autoManageChildren) : base(id, annotations)
        {
            Children = children;
            _nodeCountStack = GetCounts(children);
            AutoManageChildren = autoManageChildren;
        }

        /// <summary>
        /// For use in deserializing the document root node. Use of this constructor
        /// must be followed by a call to <see cref="SetChildren"/> before this
        /// node is usable.  After which the node is immutably locked, as
        /// <see cref="SetChildren"/> may only be called once.
        /// 
        /// The only other option would be to make the <see cref="SrmDocument"/>
        /// subclass <see cref="DocNode"/>, and contain a root <see cref="DocNodeParent"/>.
        /// This has its own awkward compromises.
        /// </summary>
        /// <param name="id">The <see cref="Identity"/> object for this node</param>
        protected DocNodeParent(Identity id)
            : base(id)
        {
            // This is not strictly necessary, but makes the instance
            // closer to more viable.  Only access of the children is not
            // allowed until after the call to SetChildren.
            _nodeCountStack = new int[0];
        }

        /// <summary>
        /// Read-only list access to the child nodes.
        /// </summary>
        public IList<DocNode> Children
        {
            get { return _children; }
            private set { _children = MakeReadOnly(value); }
        }

        /// <summary>
        /// Depth of the tree below this node
        /// </summary>
        public int Depth { get { return _nodeCountStack.Count; } }

        /// <summary>
        /// Gets a node count at a specific depth.  The depth 0 gets a count
        /// of direct children, 1 a count of all grandchildren, etc.
        /// 
        /// Subclasses should provide enums for accessing this function.
        /// </summary>
        /// <param name="depth">The depth below the current node to count</param>
        /// <returns>A count of nodes at the specified depth</returns>
        public int GetCount(int depth)
        {
            return depth < Depth ? _nodeCountStack[depth] : 0;
        }

        /// <summary>
        /// True if children of this node should be automatically updated with
        /// changes to the document settings.
        /// </summary>
        public bool AutoManageChildren { get; private set; }

        public DocNodeParent ChangeAutoManageChildren(bool autoManageChildren)
        {
            return ChangeProp(ImClone(this), im => im.AutoManageChildren = autoManageChildren);
        }

        /// <summary>
        /// Adds all children to a map by their <see cref="Identity"/> itself,
        /// and not the <see cref="Identity.GlobalIndex"/>.  Callers should be
        /// sure that the <see cref="Identity"/> subclass provides a useful
        /// implementation of <see cref="object.GetHashCode"/>, otherwise this
        /// will result in a map with one value, since by default all <see cref="Identity"/>
        /// objects are considered content equal.
        /// 
        /// This method is used when picking children where distinct new
        /// <see cref="Identity"/> objects are created, but should not replace
        /// existing objects with the same identity.
        /// </summary>
        /// <returns>A map of children by their <see cref="Identity"/> values</returns>
        public Dictionary<Identity, DocNode> CreateIdContentToChildMap()
        {
            var map = new Dictionary<Identity, DocNode>();
            foreach (DocNode child in Children)
                map.Add(child.Id, child);
            return map;
        }
        
        /// <summary>
        /// Find a child of the current node by <see cref="Identity"/>.
        /// </summary>
        /// <param name="id">The <see cref="Identity"/> of the child to find</param>
        /// <returns>The child <see cref="DocNode"/>, or null if not found</returns>
        public DocNode FindNode(Identity id)
        {
            foreach (DocNode node in Children)
            {
                if (ReferenceEquals(node.Id, id))
                    return node;
            }
            return null;
        }

        /// <summary>
        /// Find a descendant of the current node by <see cref="IdentityPath"/>.
        /// </summary>
        /// <param name="path">Path to the descendant</param>
        /// <returns>The descendant <see cref="DocNode"/>, or null if not found</returns>
        public DocNode FindNode(IdentityPath path)
        {
            return FindNode(new IdentityPathTraversal(path));
        }

        private DocNode FindNode(IdentityPathTraversal traversal)
        {
            if (traversal.HasNext)
            {
                DocNode nodeNext = FindNode(traversal.Next);
                if (!traversal.HasNext)
                    return nodeNext;
                else if (nodeNext is DocNodeParent)
                    return ((DocNodeParent) nodeNext).FindNode(traversal);
                return null;
            }
            return this;
        }

        /// <summary>
        /// Find the index to a specific child
        /// </summary>
        /// <param name="id">The <see cref="Identity"/> of the desired child</param>
        /// <returns>The index of the child, or -1 if not found</returns>
        public int FindNodeIndex(Identity id)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (ReferenceEquals(id, Children[i].Id))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Find the index to a specific descendant.  The index may be used with
        /// <see cref="GetPathTo(int,int)"/>, where depth is the <see cref="IdentityPath.Depth"/>
        /// of the path used to retrieve the index.
        /// </summary>
        /// <param name="path">An <see cref="IdentityPath"/> to the desired descendant</param>
        /// <returns>The index of the descendant, or -1 if not found</returns>
        public int FindNodeIndex(IdentityPath path)
        {
            return FindNodeIndex(new IdentityPathTraversal(path));
        }

        private int FindNodeIndex(IdentityPathTraversal traversal)
        {
            if (traversal.HasNext)
            {
                int index = FindNodeIndex(traversal.Next);
                if (!traversal.HasNext)
                    return index;
                else if (Children[index] is DocNodeParent)
                {
                    int countPreceding = 0;
                    for (int i = 0; i < index; i++)
                        countPreceding += ((DocNodeParent) Children[i]).GetCount(traversal.Remaining - 1);
                    return countPreceding + ((DocNodeParent) Children[index]).FindNodeIndex(traversal);
                }
                return -1;
            }
            return 0;            
        }

        /// <summary>
        /// Gets a <see cref="IdentityPath"/> to a child by index.
        /// </summary>
        /// <param name="index">Index to the child</param>
        /// <returns>A new <see cref="IdentityPath"/> to the child</returns>
        public IdentityPath GetPathTo(int index)
        {
            return new IdentityPath(Children[index].Id);
        }

        /// <summary>
        /// Gets a <see cref="IdentityPath"/> to a descendant by depth in the
        /// tree, and index at that depth.
        /// </summary>
        /// <param name="depth">Depth of nodes being indexed</param>
        /// <param name="index">Index to the node at the specified depth</param>
        /// <returns>The specified node</returns>
        /// <exception cref="IndexOutOfRangeException"/>
        public IdentityPath GetPathTo(int depth, int index)
        {
            return new IdentityPath(GetPathTo(depth, index, new List<Identity>()));
        }

        private List<Identity> GetPathTo(int depth, int index, List<Identity> list)
        {
            if (depth == 0)
            {
                list.Add(Children[index].Id);
                return list;
            }

            // Make sure the index does not exceed the expected count at the desired level
            int expected = GetCount(depth);
            if (expected <= index)
                throw new IndexOutOfRangeException(string.Format("Index {0} exceeds length {1}", index, expected));

            // Descend into the child containing the desired node.
            depth--;
            int count = 0;
            foreach (DocNodeParent child in Children)
            {
                int countNext = child.GetCount(depth);
                if (index < count + countNext)
                {
                    list.Add(child.Id);
                    return child.GetPathTo(depth, index - count, list);
                }
                count += countNext;
            }

            // This means the node count stack at this level is inconsistent with
            // its children.
            throw new IndexOutOfRangeException(string.Format("Node reported {0} descendants at depth {1}, but found only {2}.",
                expected, depth, count));
        }

        /// <summary>
        /// Creates a clone of the current node with a new child added
        /// at the end of its child list.
        /// </summary>
        /// <param name="childAdd">New child to add</param>
        /// <returns>A new parent node with the child at the end of its child list</returns>
        public DocNodeParent Add(DocNode childAdd)
        {
            return AddAll(new[] {childAdd});
        }

        public DocNodeParent Add(IdentityPath path, DocNode childAdd)
        {
            return AddAll(path, new[] {childAdd});
        }

        /// <summary>
        /// Creates a clone of the current node with a list of new children added
        /// at the end of its child list.
        /// </summary>
        /// <param name="childrenAdd">New children to add</param>
        /// <returns>A new parent node with the children at the end of its child list</returns>
        public DocNodeParent AddAll(IEnumerable<DocNode> childrenAdd)
        {
            Debug.Assert(Children != null); // SetChildren required

            List<DocNode> childrenNew = new List<DocNode>(Children);
            List<int> nodeCountStack = new List<int>(_nodeCountStack);

            foreach(DocNode childAdd in childrenAdd)
            {
                childrenNew.Add(childAdd);
                AddCounts(childAdd, nodeCountStack);
            }

            return ChangeChildren(childrenNew, nodeCountStack);
        }

        public DocNodeParent AddAll(IdentityPath path, IEnumerable<DocNode> childrenAdd)
        {
            return AddAll(this, new IdentityPathTraversal(path), childrenAdd);
        }

        private static DocNodeParent AddAll(DocNodeParent parent, IdentityPathTraversal traversal, IEnumerable<DocNode> childrenAdd)
        {
            return traversal.Traverse(parent, childrenAdd, AddAll, parent.AddAll);
        }

        /// <summary>
        /// Creates a clone of the current node with a new child inserted
        /// before a node with a specified <see cref="Identity"/>.
        /// </summary>
        /// <param name="idBefore"><see cref="Identity"/> of the child before which to insert</param>
        /// <param name="childAdd">New child to add</param>
        /// <param name="after">True if new children inserted after the node specified with the path</param>
        /// <returns>A new parent node with the child at the end of its child list</returns>
        /// <exception cref="IdentityNotFoundException"/>
        public DocNodeParent Insert(Identity idBefore, DocNode childAdd, bool after)
        {
            return InsertAll(idBefore, new[] { childAdd }, after);
        }

        public DocNodeParent Insert(Identity idBefore, DocNode childAdd)
        {
            return Insert(idBefore, childAdd, false);
        }

        /// <summary>
        /// Creates a clone of the current node with a new child inserted
        /// before a node with a specified with an <see cref="IdentityPath"/>.
        /// 
        /// Unlike many of the other functions that modify a node's children,
        /// the specified <see cref="IdentityPath"/> points to a child of the
        /// node containing the child list to be modified.
        /// </summary>
        /// <param name="path">Path to the child before which to insert</param>
        /// <param name="childAdd">Child to insert</param>
        /// <param name="after">True if new children inserted after the node specified with the path</param>
        /// <returns>A new parent node with cloned and modified path to the node with the modified child list</returns>
        /// <exception cref="IdentityNotFoundException"/>
        public DocNodeParent Insert(IdentityPath path, DocNode childAdd, bool after)
        {
            return InsertAll(path, new[] { childAdd }, after);
        }

        public DocNodeParent Insert(IdentityPath path, DocNode childAdd)
        {
            return Insert(path, childAdd, false);
        }

        /// <summary>
        /// Creates a clone of the current node with a list of new children added
        /// before the child supplied.
        /// </summary>
        /// <param name="idBefore"><see cref="Identity"/> of the child before which to insert</param>
        /// <param name="childrenAdd">New children to insert</param>
        /// <param name="after">True if new children inserted after the node specified with the path</param>
        /// <returns>A new parent node with the children inserted into its list</returns>
        /// <exception cref="IdentityNotFoundException"/>
        public DocNodeParent InsertAll(Identity idBefore, IEnumerable<DocNode> childrenAdd, bool after)
        {
            Debug.Assert(Children != null); // SetChildren required

            List<DocNode> childrenNew = new List<DocNode>(Children);
            List<int> nodeCountStack = new List<int>(_nodeCountStack);

            // Look for the node to insert before.
            int i = 0;
            for (; i < childrenNew.Count; i++)
            {
                if (ReferenceEquals(childrenNew[i].Id, idBefore))
                {
                    if (after)
                        i++;

                    // Insert the nodes.
                    foreach (DocNode childAdd in childrenAdd)
                    {
                        if (i < childrenNew.Count)
                            childrenNew.Insert(i, childAdd);
                        else
                            childrenNew.Add(childAdd);
                        i++;
                        AddCounts(childAdd, nodeCountStack);
                    }
                    break;
                }
            }
            // No children added means identity not found.
            if (Children.Count == childrenNew.Count)
                throw new IdentityNotFoundException(idBefore);

            return ChangeChildren(childrenNew, nodeCountStack);
        }

        public DocNodeParent InsertAll(Identity idBefore, IEnumerable<DocNode> childrenAdd)
        {
            return InsertAll(idBefore, childrenAdd, false);
        }

        /// <summary>
        /// Creates a clone of the current node with a list of new children inserted
        /// before a node with a specified with an <see cref="IdentityPath"/>.
        /// 
        /// Unlike many of the other functions that modify a node's children,
        /// the specified <see cref="IdentityPath"/> points to a child of the
        /// node containing the child list to be modified.
        /// </summary>
        /// <param name="path">Path to the child before which to insert</param>
        /// <param name="childrenAdd">Children to insert</param>
        /// <param name="after">True if new children inserted after the node specified with the path</param>
        /// <returns>A new parent node with cloned and modified path to the node with the modified child list</returns>
        /// <exception cref="IdentityNotFoundException"/>
        public DocNodeParent InsertAll(IdentityPath path, IEnumerable<DocNode> childrenAdd, bool after)
        {
            return InsertAll(this, new IdentityPathTraversal(path.Parent), new InsertTag(path.Child, childrenAdd, after));
        }

        public DocNodeParent InsertAll(IdentityPath path, IEnumerable<DocNode> childrenAdd)
        {
            return InsertAll(path, childrenAdd, false);
        }

        private DocNodeParent InsertAll(InsertTag tag)
        {
            return InsertAll(tag.Id, tag.Children, tag.After);
        }

        private static DocNodeParent InsertAll(DocNodeParent parent, IdentityPathTraversal traversal, InsertTag tag)
        {
            return traversal.Traverse(parent, tag, InsertAll, parent.InsertAll);
        }

        private sealed class InsertTag
        {
            public InsertTag(Identity id, IEnumerable<DocNode> children, bool after)
            {
                Id = id;
                Children = children;
                After = after;
            }

            public Identity Id { get; private set; }
            public IEnumerable<DocNode> Children { get; private set; }
            public bool After { get; private set; }
        }

        public DocNodeParent PickChildren(IPickedList picked)
        {
            // Make sure already chosen nodes are not recreated.  They
            // may also contain specifically chosen children.
            Dictionary<Identity, DocNode> dict = new Dictionary<Identity, DocNode>();
            foreach (DocNode child in Children)
                dict.Add(child.Id, child);

            List<DocNode> childrenNew = new List<DocNode>();
            foreach (Identity childId in picked.Chosen)
            {
                // If the dictionary of previously existing nodes already
                // contains this one, then just add it back.  Otherwise,
                // create a new node.
                DocNode child;
                if (!dict.TryGetValue(childId, out child))
                    child = picked.CreateChildNode(childId);
                childrenNew.Add(child);
            }
            return ChangeChildren(childrenNew).ChangeAutoManageChildren(picked.AutoManageChildren);
        }

        public DocNodeParent PickChildren(IdentityPath path, IPickedList picked)
        {
            return PickChildren(this, new IdentityPathTraversal(path), picked);
        }

        private static DocNodeParent PickChildren(DocNodeParent parent, IdentityPathTraversal traversal, IPickedList picked)
        {
            return traversal.Traverse(parent, picked, PickChildren, parent.PickChildren);
        }

        /// <summary>
        /// Creates a clone of the current node with a modified copy of the
        /// child list, replacing a child with the <see cref="DocNode.Id"/>
        /// matching that of a specified replacement child node.  If no
        /// match is found, null is returned.
        /// </summary>
        /// <param name="childReplace">A child node with an Id that is reference equal to the node to be replaced</param>
        /// <returns>A new parent node</returns>
        /// <exception cref="IdentityNotFoundException"/>
        public DocNodeParent ReplaceChild(DocNode childReplace)
        {
            Debug.Assert(Children != null); // SetChildren required

            DocNode[] childrenNew = new DocNode[Children.Count];
            List<int> nodeCountStack = new List<int>(_nodeCountStack);
            int index = -1;
            for (int i = 0; i < Children.Count; i++)
            {
                if (!childReplace.EqualsId(Children[i]))
                    childrenNew[i] = Children[i];
                else
                {
                    index = i;
                    RemoveCounts(Children[i], nodeCountStack);
                    childrenNew[i] = childReplace;
                    AddCounts(childReplace, nodeCountStack);
                }
            }
            // If nothing was replaced throw an exception to let the caller know.
            if (index == -1)
                throw new IdentityNotFoundException(childReplace.Id);

            return ChangeChildren(childrenNew, nodeCountStack);
        }

        public DocNodeParent ReplaceChild(IdentityPath path, DocNode childReplace)
        {
            return ReplaceChild(this, new IdentityPathTraversal(path), childReplace);
        }

        private static DocNodeParent ReplaceChild(DocNodeParent parent, IdentityPathTraversal traversal, DocNode childReplace)
        {
            return traversal.Traverse(parent, childReplace, ReplaceChild, parent.ReplaceChild);
        }

        /// <summary>
        /// Creates a clone of the current node with a modified copy of the
        /// child list, removing a specific child.
        /// </summary>
        /// <param name="childRemove">A node with the <see cref="Identity"/> to be removed</param>
        /// <returns>A new parent node</returns>
        /// <exception cref="IdentityNotFoundException"/>
        public DocNodeParent RemoveChild(DocNode childRemove)
        {
            Debug.Assert(Children != null); // Set children required

            List<DocNode> childrenNew = new List<DocNode>();
            List<int> nodeCountStack = new List<int>(_nodeCountStack);
            foreach (DocNode child in Children)
            {
                if (!childRemove.EqualsId(child))
                    childrenNew.Add(child);
                else
                    RemoveCounts(child, nodeCountStack);
            }
            // If nothing was removed, throw an exception to let the caller know.
            if (childrenNew.Count == Children.Count)
                throw new IdentityNotFoundException(childRemove.Id);

            return ChangeChildren(childrenNew.ToArray(), nodeCountStack);
        }

        public DocNodeParent RemoveChild(IdentityPath path, DocNode childRemove)
        {
            return RemoveChild(this, new IdentityPathTraversal(path), childRemove);
        }

        public static DocNodeParent RemoveChild(DocNodeParent parent, IdentityPathTraversal traversal, DocNode childRemove)
        {
            return traversal.Traverse(parent, childRemove, RemoveChild, parent.RemoveChild);
        }

        /// <summary>
        /// Creates a clone of the current node with a list of descendents removed
        /// from the tree.
        /// </summary>
        /// <param name="descendentsRemoveIds">Collection of the <see cref="Identity.GlobalIndex"/> values for the descendents to remove</param>
        /// <returns>A node with the desendents removed</returns>
        public DocNodeParent RemoveAll(ICollection<int> descendentsRemoveIds)
        {
            Debug.Assert(Children != null); // SetChildren required

            List<DocNode> childrenNew = new List<DocNode>();
            List<int> nodeCountStack = new List<int> { 0 };

            foreach (DocNode child in Children)
            {
                // Skip any child with a GlobalIndex found in the set to be removed.
                if (descendentsRemoveIds.Contains(child.Id.GlobalIndex))
                    continue;

                var childNew = child;
                if (child is DocNodeParent)
                    childNew = ((DocNodeParent) child).RemoveAll(descendentsRemoveIds);
                childrenNew.Add(childNew);
                AddCounts(childNew, nodeCountStack);
            }

            return ChangeChildren(childrenNew, nodeCountStack);
        }

        public DocNodeParent RemoveAll(IdentityPath path, ICollection<DocNode> descendentsRemove)
        {
            return RemoveAll(this, new IdentityPathTraversal(path), descendentsRemove);
        }

        private static DocNodeParent RemoveAll(DocNodeParent parent, IdentityPathTraversal traversal, ICollection<DocNode> descendentsRemove)
        {
            return traversal.Traverse(parent, descendentsRemove, AddAll, parent.AddAll);
        }

        public DocNodeParent ChangeAll(Func<DocNode, DocNode> change, int depth)
        {
            var listChildrenNew = new List<DocNode>();
            foreach (var child in Children)
            {
                var childNew = change(child);
                if (depth > 0)
                    childNew = ((DocNodeParent) childNew).ChangeAll(change, depth - 1);
                listChildrenNew.Add(childNew);
            }
            return ChangeChildrenChecked(listChildrenNew);
        }

        /// <summary>
        /// Creates a clone of the current node with a new child list.
        /// </summary>
        /// <param name="children">New list of children</param>
        /// <returns>A new parent node</returns>
        public DocNodeParent ChangeChildren(IList<DocNode> children)
        {
            return ChangeChildren(children, GetCounts(children));
        }

        public DocNodeParent ChangeChildrenChecked(IList<DocNode> children)
        {
            return ChangeChildrenChecked(children, false);
        }

        public DocNodeParent ChangeChildrenChecked(IList<DocNode> children, bool changeAutoManage)
        {
            if (ArrayUtil.ReferencesEqual(children, Children))
                return this;

            var nodeChanged = ChangeChildren(children);
            // If the children changed, and this should impact the AutoManageChildren
            // property, then turn it off.
            if (changeAutoManage && nodeChanged.AutoManageChildren)
                nodeChanged = nodeChanged.ChangeAutoManageChildren(false);
            return nodeChanged;
        }

        /// <summary>
        /// Core utility method for cloning the node with a new child list
        /// which all of the child list modifying methods call to complete
        /// their work.
        /// </summary>
        /// <param name="children">An altered list of children</param>
        /// <param name="counts">An altered child list stack that correctly counts the new children</param>
        /// <returns>A new parent node</returns>
        private DocNodeParent ChangeChildren(IList<DocNode> children, IList<int> counts)
        {
            DocNodeParent clone = ChangeProp(ImClone(this), im => im.Children = children);
            clone._nodeCountStack = counts;
            var childrenNew = OnChangingChildren(clone);
            if (!ArrayUtil.ReferencesEqual(childrenNew, clone.Children))
                clone.Children = childrenNew;
            return clone;
        }

        /// <summary>
        /// Override to change properties to update properties dependent on the
        /// children of this node, when a clone is being created with a new set of children.
        /// </summary>
        /// <param name="clone">A copy of this instance created with <see cref="object.MemberwiseClone"/>
        /// with its new children assigned</param>
        protected virtual IList<DocNode> OnChangingChildren(DocNodeParent clone)
        {            
            // Default does nothing.
            return clone.Children;
        }

        /// <summary>
        /// For use with serialization, to complete the node before returning
        /// from a deserialization function, which created the node with a
        /// no-args constructor.
        /// 
        /// This method must not break node immutability.
        /// </summary>
        /// <param name="children">Deserialized list of children for this node</param>
        protected void SetChildren(IList<DocNode> children)
        {
            Debug.Assert(Children == null); // Children must not have been

            Children = children;
            _nodeCountStack = GetCounts(children);
        }

        /// <summary>
        /// Performs a full accounting of all children an their contents, and returns
        /// an accurate child count stack.  The top of this stack, at index 0
        /// represents the direct children of this node, and the bottom the most
        /// distant leaf nodes.
        /// 
        /// This function depends on the child nodes having a correct accounting
        /// of any children they may have.  It does not perform a deep traversal.
        /// </summary>
        /// <param name="children">The list of children to count</param>
        /// <returns>A list of integers representing a stack of child counts</returns>
        private static IList<int> GetCounts(IEnumerable<DocNode> children)
        {
            List<int> counts = new List<int> { 0 }; // level-0 is direct children
            foreach (DocNode child in children)
                AddCounts(child, counts);
            return counts.ToArray();
        }

        private static void AddCounts(DocNode node, IList<int> stack)
        {
            AdjustCounts(node, 1, stack);
        }

        private static void RemoveCounts(DocNode node, IList<int> stack)
        {
            AdjustCounts(node, -1, stack);
        }

        private static void AdjustCounts(DocNode node, int sign, IList<int> stack)
        {
            stack[0] += sign;

            DocNodeParent child = node as DocNodeParent;
            if (child != null)
            {
                int i = 0;
                while (i < child.Depth)
                {
                    int count = child.GetCount(i) * sign;
                    i++;    // level + 1 in the parent node
                    if (i < stack.Count)
                        stack[i] += count;
                    else
                        stack.Add(count);
                }
            }
        }

        #region object overrides

        public bool Equals(DocNodeParent obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && ArrayUtil.EqualsDeep(obj.Children, Children) 
                && Equals(AutoManageChildren, obj.AutoManageChildren);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as DocNodeParent);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode()*397) ^ Children.GetHashCodeDeep();
            }
        }

        #endregion
    }

    /// <summary>
    /// Supports picking a set of children for a <see cref="DocNodeParent"/>.
    /// </summary>
    public interface IPickedList
    {
        /// <summary>
        /// List of <see cref="Identity"/> objects for the chosen children.
        /// </summary>
        IEnumerable<Identity> Chosen { get; }

        bool AutoManageChildren { get; }

        /// <summary>
        /// Creation function used to materialise children, which are not already present,
        /// for the specified <see cref="Identity"/> objects.
        /// </summary>
        /// <param name="childId">An <see cref="Identity"/> for which a node is needed</param>
        /// <returns>The newly created node to add to the child list</returns>
        DocNode CreateChildNode(Identity childId);
    }
}
