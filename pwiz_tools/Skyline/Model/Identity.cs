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
using System.Globalization;
using System.Linq;
using System.Threading;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Base class for an invariant identity object which uniquely identifies
    /// each <see cref="DocNode"/> instance.  <see cref="DocNode"/> derived
    /// classes may store information that never changes in instances derived
    /// from this base class.
    /// 
    /// Because a <see cref="DocNode"/>'s Identity instance will never change,
    /// even as new instances of the <see cref="DocNode"/> are created to
    /// refer to it, Identity derrived instances are allowed to refer back
    /// up the document tree.  But only if the identity objects are truly
    /// inherently linked in a way that will also never change.
    /// 
    /// Backward references within a <see cref="DocNode"/> itself are expressly
    /// forbidden, as such references would require all child nodes to change
    /// every time any other child node changed.
    /// </summary>
    public abstract class Identity
    {
        private static int _globalIndexCounter;

        protected Identity()
        {
            GlobalIndex = Interlocked.Increment(ref _globalIndexCounter);
        }

        /// <summary>
        /// Creates a copy of this with a new global index
        /// </summary>
        public Identity Copy()
        {
            var copy = (Identity) MemberwiseClone();
            copy.GlobalIndex = Interlocked.Increment(ref _globalIndexCounter);
            return copy;
        }

        /// <summary>
        /// Use this value as a key in hashing containers, when reference equality
        /// mapping is desitred.
        /// <para>
        /// Identity subclasses frequently need to implement <see cref="Equals(object)"/>
        /// and <see cref="GetHashCode"/> for "content equality", similar to a <see cref="string"/>
        /// object.
        /// </para><para>
        /// For "reference equality", you should use <see cref="object.ReferenceEquals"/>.
        /// For "reference equality" in a <see cref="IDictionary{TKey,TValue}"/>, the
        /// <see cref="GlobalIndex"/> value is provided.
        /// </para><para>
        /// It may be confusing at first to notice that <see cref="GlobalIndex"/>
        /// is not included in the "content equality" overrides, but that is because
        /// it is strictly an "reference equality" value.  If C# supplied access
        /// to its own unique reference ID, or an in memory address, either
        /// could be used in place of <see cref="GlobalIndex"/>.</para>
        /// </summary>
        public int GlobalIndex { get; private set; }

        #region object overrides

        
        /// <summary>
        /// Content is always equal for this base class.  Use <see cref="object.ReferenceEquals"/>
        /// to distinguish <see cref="Identity"/> objects.  Many derived classes have
        /// much more interesting overrides of "content equality" functions.
        /// </summary>
        private bool Equals(Identity obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            // They at least have to be the same type, though.
            return obj.GetType() == GetType();
        }

        /// <summary>
        /// Content is always equal for this base class.  Use <see cref="object.ReferenceEquals"/>
        /// to distinguish <see cref="Identity"/> objects.  Many derived classes have
        /// much more interesting overrides of "content equality" functions.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as Identity);
        }

        /// <summary>
        /// Content is always equal for this base class.  Use <see cref="GlobalIndex"/> to
        /// distinguish <see cref="Identity"/> objects in a <see cref="IDictionary{TKey,TValue}"/>.
        /// Many derived classes have much more interesting overrides of
        /// "content equality" functions.
        /// </summary>
        public override int GetHashCode()
        {
            return 0;
        }

        public override string ToString()
        {
            return GlobalIndex.ToString(CultureInfo.InvariantCulture);
        }

        #endregion
    }

    /// <summary>
    /// The <see cref="DocNode"/> with the <see cref="Identity"/> specified was not found
    /// in the document or node searched.
    /// </summary>
    public class IdentityNotFoundException : Exception
    {
        // CONSIDER: Put useful info in Identity.ToString(), and have a better message.
        public IdentityNotFoundException(Identity id)
            : base(Resources.IdentityNotFoundException_IdentityNotFoundException_Failed_to_find_document_node)
        {
            Id = id;
        }

        /// <summary>
        /// The missing <see cref="Identity"/>
        /// </summary>
        public Identity Id { get; private set; }
    }

    /// <summary>
    /// Used to locate nodes in the <see cref="DocNode"/> tree on which to
    /// perform actions.  Currently implemented with a <see cref="Stack{T}"/>
    /// and it is stateful.  It can only be used once for a single tree
    /// traversal call.  Create a copy before making the call, if you
    /// want to keep the path state around.
    /// </summary>
    [Serializable]  // To keep Visual Studio from complaining
    public sealed class IdentityPath
    {
        public static readonly IdentityPath ROOT = new IdentityPath();

        private readonly Identity[] _identities;

        /// <summary>
        /// Creates the root path.
        /// </summary>
        private IdentityPath()
        {
            _identities = new Identity[0];
        }

        /// <summary>
        /// Constructs the path from an ordered array of <see cref="Identity"/>
        /// objects, with root first and child last.
        /// </summary>
        /// <param name="identities">Array of the identities in the path</param>
        public IdentityPath(params Identity[] identities)
        {
            _identities = identities;
        }

        /// <summary>
        /// Constructs the path from an ordered enumeration of <see cref="Identity"/>
        /// objects, with root first and child last.
        /// </summary>
        /// <param name="identities">Enumeration of the identities in the path</param>
        public IdentityPath(IEnumerable<Identity> identities)
            : this(identities.ToArray())
        {
        }

        /// <summary>
        /// Constructs the path from an existing instance with one added final
        /// <see cref="Identity"/>.
        /// </summary>
        /// <param name="parent">Original path</param>
        /// <param name="child">An <see cref="Identity"/> to add to the end</param>
        public IdentityPath(IdentityPath parent, Identity child)
        {
            List<Identity> path = new List<Identity>(parent._identities) {child};
            _identities = path.ToArray();
        }

        /// <summary>
        /// Constructs the path from an existing relative instance with one added 
        /// <see cref="Identity"/>.
        /// </summary>
        /// <param name="parent">An <see cref="Identity"/> to add to the beginning</param>
        /// <param name="child">An existing partial path</param>
        public IdentityPath(Identity parent, IdentityPath child)
        {
            List<Identity> path = new List<Identity> {parent};
            path.AddRange(child._identities);
            _identities = path.ToArray();
        }

        /// <summary>
        /// The path to the parent node of the node specified by this path
        /// </summary>
        public IdentityPath Parent
        {
            get
            {
                return GetPathTo(Depth - 1);
            }
        }

        /// <summary>
        /// The <see cref="Identity"/> of the final node in this path
        /// </summary>
        public Identity Child { get { return _identities[Depth]; } }

        /// <summary>
        /// Index to the final <see cref="Identity"/> in the path
        /// </summary>
        public int Depth { get { return _identities.Length - 1; } }

        /// <summary>
        /// Number of <see cref="Identity"/> objects in the path
        /// </summary>
        public int Length { get { return _identities.Length; } }

        /// <summary>
        /// Returns true if this path is the root path
        /// </summary>
        public bool IsRoot { get { return Length == 0; } }

        /// <summary>
        /// Access to identities in the path in order with the root at
        /// 0 and the final node at <see cref="Length"/> - 1
        /// </summary>
        /// <param name="depth">Index to an <see cref="Identity"/> in the path</param>
        /// <returns>A single <see cref="Identity"/> in the path</returns>
        public Identity GetIdentity(int depth)
        {
            return _identities[depth];
        }

        public IdentityPath GetPathTo(int depth)
        {
            if (-1 > depth || depth > Depth)
                throw new IndexOutOfRangeException(string.Format(Resources.IdentityPath_GetPathTo_Index__0__out_of_range_1_to__1__, depth, Depth));
            return new IdentityPath(_identities.Take(depth + 1));
        }

        public static IdentityPath ToIdentityPath(IList<int> idPath, SrmDocument document)
        {
            IdentityPath identityPath = ROOT;
            DocNode next = document;
            foreach (int globalIndex in idPath)
            {
                DocNodeParent parent = next as DocNodeParent;
                if (null == parent)
                {
                    return null;
                }
                next = null;
                foreach (var child in parent.Children)
                {
                    if (child.Id.GlobalIndex == globalIndex)
                    {
                        next = child;
                        break;
                    }
                }
                if (null == next)
                {
                    return null;
                }
                identityPath = new IdentityPath(identityPath, next.Id);
            }
            return identityPath;
        }

        public IEnumerable<int> ToGlobalIndexList()
        {
            if (Depth < 0)
            {
                return new int[0];
            }
            return Parent.ToGlobalIndexList().Concat(new[] { Child.GlobalIndex });
        }

        #region object overrides

        /// <summary>
        /// Returns an <see cref="IEnumerable{T}"/> of the <see cref="Identity.GlobalIndex"/>
        /// values for the <see cref="Identity"/> objects in the path.  The main purpose
        /// of a <see cref="IdentityPath"/> is for looking up <see cref="Identity"/> matches
        /// by <see cref="object.ReferenceEquals"/>.  There for its own equality members
        /// are based on reference equality of the contained <see cref="Identity"/> values.
        /// <para>
        /// Since by default <see cref="Identity"/> objects are all content equal, this is
        /// achieved by using the <see cref="Identity.GlobalIndex"/> instead.
        /// </para>
        /// </summary>
        private IEnumerable<int> GlobalIndexes
        {
            get
            {
                foreach (Identity id in _identities)
                    yield return id.GlobalIndex;
            }
        }

        public bool Equals(IdentityPath obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return GlobalIndexes.SequenceEqual(obj.GlobalIndexes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (IdentityPath)) return false;
            return Equals((IdentityPath) obj);
        }

        public override int GetHashCode()
        {
            return GlobalIndexes.ToArray().GetHashCodeDeep();                
        }

        public override string ToString()
        {
            return "/" + _identities.ToString("/"); // Not L10N
        }

        #endregion
    }

    /// <summary>
    /// Supports recursive descent traversal of a <see cref="DocNode"/> tree.
    /// </summary>
    public sealed class IdentityPathTraversal
    {
        private readonly IdentityPath _path;
        private int _index;

        /// <summary>
        /// Constructs a traversal for a specific path
        /// </summary>
        /// <param name="path">The path to traverse</param>
        public IdentityPathTraversal(IdentityPath path)
        {
            _path = path;
        }

        /// <summary>
        /// True if the final node has not been reached
        /// </summary>
        public bool HasNext { get { return _index < _path.Length; } }

        /// <summary>
        /// Gets the next <see cref="Identity"/> in the traveral
        /// </summary>
        public Identity Next() { return _path.GetIdentity(_index++); }

        /// <summary>
        /// The number of path levels remaining untraversed
        /// </summary>
        public int Remaining { get { return _path.Length - _index; } }

        /// <summary>
        /// Recursive descent function provided by the caller of <see cref="IdentityPathTraversal.Traverse{T}"/>
        /// </summary>
        /// <typeparam name="TTag">Type of the tag object used at the final node to perform the action</typeparam>
        /// <param name="parent">The parent node to use in the next recursive call</param>
        /// <param name="traversal">The active traversal object</param>
        /// <param name="tag">The tag instance that will be used to perform the action</param>
        /// <returns>An altered copy of the parent node</returns>
        public delegate DocNodeParent Recurse<in TTag>(DocNodeParent parent, IdentityPathTraversal traversal, TTag tag);

        /// <summary>
        /// Action function provided by the caller of <see cref="IdentityPathTraversal.Traverse{T}"/>
        /// </summary>
        /// <typeparam name="TTag">Type of the tag object used at the final node to perform the action</typeparam>
        /// <param name="tag">The tag instance that will be used to perform the action</param>
        /// <returns>An altered copy of the node on which the action was performed</returns>
        public delegate DocNodeParent Act<in TTag>(TTag tag);

        /// <summary>
        /// Traverses the <see cref="DocNode"/> tree, performing an action on the
        /// final node specified in the path.
        /// </summary>
        /// <typeparam name="TTag">Type of the tag object used at the final node to perform the action</typeparam>
        /// <param name="parent">The parent node to use in the next recursive call</param>
        /// <param name="tag">The tag instance that will be used to perform the action</param>
        /// <returns>An alterer parent node</returns>
        /// <param name="recurse">Recursive descent function</param>
        /// <param name="act">Action function</param>
        /// <returns>An altered copy of the <see cref="parent"/> node</returns>
        public DocNodeParent Traverse<TTag>(DocNodeParent parent, TTag tag, Recurse<TTag> recurse, Act<TTag> act)
        {
            if (HasNext)
            {
                Identity nextId = Next();

                // Look for the right child
                foreach (DocNode nodeNext in parent.Children)
                {
                    if (ReferenceEquals(nextId, nodeNext.Id))
                    {
                        DocNodeParent subParent = nodeNext as DocNodeParent;
                        if (subParent == null)
                            throw new InvalidOperationException(Resources.IdentityPathTraversal_Traverse_Invalid_attempt_to_perform_parent_operation_on_leaf_node);

                        // Make recursive call into the specified child
                        return parent.ReplaceChild(recurse(subParent, this, tag));
                    }
                }
                throw new IdentityNotFoundException(nextId);
            }

            return act(tag);
        }
    }
}