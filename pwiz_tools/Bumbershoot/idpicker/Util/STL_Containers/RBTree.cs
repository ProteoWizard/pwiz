/*=====================================================================
 
    File: RBTree.cs
    
    Summary: Implementation of the RBTree class. 
    
    Author: Joannes Vermorel (joannes@vermorel.com)
    
    Last Modified: 2002-07-13


    Acknowledgement: Brian Berns (code improvements)

    Copyright © 2003 Joannes Vermorel

    This software is provided 'as-is', without any express or implied warranty. 
    In no event will the authors be held liable for any damages arising from the 
    use of this software.

    Permission is granted to anyone to use this software for any purpose, including 
    commercial applications, and to alter it and redistribute it freely, subject to the 
    following restrictions:

    1. The origin of this software must not be misrepresented; you must not claim that 
    you wrote the original software. If you use this software in a product, an acknowledgment 
    (see the following) in the product documentation is required.

    Portions Copyright © 2003 Joannes Vermorel

    2. Altered source versions must be plainly marked as such, and must not be 
    misrepresented as being the original software.

    3. This notice may not be removed or altered from any source distribution.

 =====================================================================*/

//
// $Id$
//

namespace System.Collections.Generic
{

    internal delegate void RBTreeModifiedHandler();

    /// <summary>
    /// Allows using a Comparison&lt;T&gt; object as an object that implemented IComparer&lt;T&gt;
    /// </summary>
    public class ComparisonForwarder<T> : IComparer<T>
    {
        /// <summary>
        /// Constructs a ComparisonForwarder that forwards calls to Compare() to the given comparison predicate.
        /// </summary>
        public ComparisonForwarder (Comparison<T> comparison) { Comparison = comparison; }

        /// <summary>
        /// The comparison predicate used by this instance.
        /// </summary>
        public Comparison<T> Comparison { get; private set; }

        /// <summary>
        /// Compares two T values using the comparison predicate.
        /// </summary>
        public int Compare (T x, T y) { return Comparison(x, y); }
    }

    /// <summary>
    /// RBTree is implemented using black-red binary trees. The algorithms follows the indications given in the textbook
    /// "Introduction to Algorithms" Thomas H. Cormen, Charles E. Leiserson, Ronald L. Rivest
    /// </summary>
    public class RBTree<T> : ICollection<T>, IEnumerable<T>
    {
        /// <summary>
        /// Store the number of elements in the RBTree.
        /// </summary>
        private int count;

        /// <summary>
        /// Store the root node of the RBTree.
        /// </summary>
        internal RBTreeNode<T> root;

        /// <summary>
        /// Store the IComparer that allows to compare the node keys.
        /// </summary>
        public IComparer<T> Comparer { get; protected set; }

        // <summary>
        // Store the lock for multiple-reader access and single-writer access.
        // </summary>
        //private ReaderWriterLock rwLock;

        /// <summary>
        /// Initializes a new instance of Collections.System.RBTree
        /// class that is empty. A default comparer will be used to
        /// compare the elements added to the RBTree.
        /// </summary>
        public RBTree()
        {
            Comparer = Comparer<T>.Default;
            Initialize();
        }

        /// <summary>
        /// Initializes an empty RBTree instance.
        /// </summary>
        /// <param name="comp">
        /// comp represents the IComparer elements which will be used to sort the elements in RBTree.
        /// </param>
        public RBTree(IComparer<T> comp)
        {
            Comparer = comp;
            Initialize();
        }

        /// <summary>
        /// Initializes a RBTree by copying elements from another RBTree, using the given comparer.
        /// </summary>
        public RBTree (RBTree<T> otherTree, IComparer<T> comp)
        {
            Comparer = comp;
            Initialize();
            Copy(otherTree);
        }

        /// <summary>
        /// Initializes an empty RBTree instance using the given comparison predicate.
        /// </summary>
        /// <param name="comp"></param>
        public RBTree (Comparison<T> comp)
        {
            Comparer = new ComparisonForwarder<T>(comp);
            Initialize();
        }

        /// <summary>
        /// Initializes a RBTree by copying elements from another RBTree, using the given comparison predicate.
        /// </summary>
        public RBTree (RBTree<T> otherTree, Comparison<T> comp) : this(comp)
        {
            Copy(otherTree);
        }

        /// <summary>
        /// Initializes a RBTree by copying elements from another RBTree, using the same comparer.
        /// </summary>
        public RBTree( RBTree<T> otherTree ) : this()
        {
            Copy( otherTree );
        }

        /// <summary>
        /// Perform the common initialization tasks to all the constructors.
        /// </summary>
        private void Initialize()
        {
            count = 0;
            root = null;
            //rwLock = new ReaderWriterLock();
        }

        private void Copy( RBTree<T> otherTree )
        {
            root = Copy( otherTree.root, root );
            count = otherTree.count;
        }

        // copy entire subtree, recursively
        private RBTreeNode<T> Copy( RBTreeNode<T> oldRoot, RBTreeNode<T> newFather )
        {
            RBTreeNode<T> newRoot = null;

            // copy a node, then any subtrees
            if( oldRoot != null )
            {
                newRoot = new RBTreeNode<T>( oldRoot );
                if( newFather != null )
                    newRoot.Father = newFather;

                newRoot.Left = Copy( oldRoot.Left, newRoot );
                newRoot.Right = Copy( oldRoot.Right, newRoot );
            }

            return newRoot;    // return newly constructed tree
        }

        /// <summary>
        /// Gets the number of elements stored in RBTree.
        /// </summary>
        public int Count
        {
            get 
            {
                return count;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the access to RBTree is
        /// synchronized (thread-safe).
        /// </summary>
        public bool IsSynchronized
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access
        /// to RBTree
        /// </summary>
        public object SyncRoot
        {
            get 
            {
                return this;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the RBTree has
        /// a fixed size.
        /// </summary>
        public bool IsFixedSize
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the RBTree is
        /// read-only.
        /// </summary>
        public bool IsReadOnly
        {
            get 
            {
                return false;
            }
        }

        internal RBTreeNode<T> MaxNode()
        {
            if( root == null )
                throw new InvalidOperationException( "Unable to return Max because the RBTree is empty." );

            RBTreeNode<T> node = root;
            while( node.Right != null )
                node = node.Right;
            return node;
        }

        internal RBTreeNode<T> MinNode()
        {
            if( root == null )
                throw new InvalidOperationException( "Unable to return Min because the RBTree is empty." );

            RBTreeNode<T> node = root;
            while( node.Left != null )
                node = node.Left;
            return node;
        }

        /// <summary>
        /// Gets the highest element stored in the RBTree. The operation
        /// is performed in a guaranteed logarithmic time of the size of RBTree.
        /// </summary>
        public T Max
        {
            get
            {
                //rwLock.AcquireReaderLock(Timeout.Infinite);

                try 
                {
                    return MaxNode().Key;
                }
                finally
                {
                    //rwLock.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// Gets the lowest element stored in the RBTree. The operation
        /// is performed in a guaranteed logarithmic time of the size of RBTree.
        /// </summary>
        public T Min
        {
            get 
            {
                //rwLock.AcquireReaderLock(Timeout.Infinite);

                try
                {
                    return MinNode().Key;
                }
                finally
                {
                    //rwLock.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// A pair whose first element is a reference to either a newly inserted key-value pair or an existing one, and the second element is a boolean storing whether the insert actually happened.
        /// </summary>
        public class InsertResult
        {
            internal InsertResult( T element, bool wasInserted )
            {
                m_Element = element;
                m_WasInserted = wasInserted;
            }

            /// <summary>
            /// Returns the element, either newly inserted or a previously existing element.
            /// </summary>
            public T Element
            {
                get
                {
                    return m_Element;
                }
            }
            
            /// <summary>
            /// Returns true if the element was inserted (i.e. the key was not already in the tree).
            /// </summary>
            public bool WasInserted
            {
                get
                {
                    return m_WasInserted;
                }
            }

            internal T m_Element;
            internal bool m_WasInserted;
        }

        /// <summary>
        /// Inserts an elements to the RBTree. The operation is performed
        /// in a guaranteed logarithmic time of the RBTree size.
        /// Returns a pair: first element is a reference to either the inserted key-value pair or the existing one,
        /// second element is a boolean storing whether the insert actually happened
        /// (if the keys must be unique and the key was already in the tree, the key is not actually inserted)
        /// </summary>
        public InsertResult Insert( T x )
        {
            //rwLock.AcquireReaderLock( Timeout.Infinite );

            InsertResult rv = null;

            try
            {
                //OnRBTreeModified();
                //if(comparer == null) 
                //    throw new ArgumentException("RBTree : not able to compare the elements");

                if( root == null )
                {
                    root = new RBTreeNode<T>( x, null );
                    rv = new InsertResult( root.Key, true );
                } else
                {
                    // First step : a naive insertion of the element
                    RBTreeNode<T> node1 = root, node2 = null;
                    int compare = 0;

                    while( node1 != null )
                    {
                        node2 = node1;
                        compare = Comparer.Compare( x, node1.Key );
                        if( compare < 0 )
                        {
                            node1 = node1.Left;
                        } else if( compare > 0 )
                        {
                            node1 = node1.Right;
                        } else
                        {
                            rv = new InsertResult( node1.Key, false );
                            node1 = null;
                        }
                    }

                    if( rv == null )
                    {
                        node1 = new RBTreeNode<T>( x, node2 );
                        rv = new InsertResult( node1.Key, true );

                        if( compare < 0 ) node2.Left = node1;
                        else node2.Right = node1;

                        node1.Color = true;

                        // Then : correct the structure of the tree
                        while( node1 != root && node1.Father.Color )
                        {
                            if( node1.Father == node1.Father.Father.Left )
                            {
                                node2 = node1.Father.Father.Right;
                                if( node2 != null && node2.Color )
                                {
                                    node1.Father.Color = false;
                                    node2.Color = false;
                                    node1.Father.Father.Color = true;
                                    node1 = node1.Father.Father;
                                } else
                                {
                                    if( node1 == node1.Father.Right )
                                    {
                                        node1 = node1.Father;
                                        RotateLeft( node1 );
                                    }
                                    node1.Father.Color = false;
                                    node1.Father.Father.Color = true;
                                    RotateRight( node1.Father.Father );
                                }
                            } else
                            {
                                node2 = node1.Father.Father.Left;
                                if( node2 != null && node2.Color )
                                {
                                    node1.Father.Color = false;
                                    node2.Color = false;
                                    node1.Father.Father.Color = true;
                                    node1 = node1.Father.Father;
                                } else
                                {
                                    if( node1 == node1.Father.Left )
                                    {
                                        node1 = node1.Father;
                                        RotateRight( node1 );
                                    }
                                    node1.Father.Color = false;
                                    node1.Father.Father.Color = true;
                                    RotateLeft( node1.Father.Father );
                                }
                            }
                        }
                    } else if( rv.WasInserted )
                        rv = new InsertResult( x, true );
                }


                if( rv.WasInserted )
                {
                    root.Color = false;
                    count++;
                }
            } finally
            {
                //rwLock.ReleaseReaderLock();
            }

            //if( Count != EnumeratedCount ) throw new InvalidOperationException( "EnumeratedCount does not match stored count" );
            return rv;
        }

        /// <summary>
        /// Attempts to add an element in the tree, but does nothing if the element already exists.
        /// </summary>
        public void Add( T x )
        {
            Insert( x );
        }

        /// <summary>
        /// Removes of the elements from the RBTree.
        /// </summary>
        public void Clear()
        {
            //rwLock.AcquireWriterLock( Timeout.Infinite );

            try
            {
                //OnRBTreeModified();
                root = null;
                count = 0;
            } finally
            {
                //rwLock.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Determines whether the RBTree contains a specific object.
        /// The RBTree could contain several identical object. The operation
        /// is performed in a guaranteed logarithmic time of the RBTree size.
        /// </summary>
        public bool Contains( T x )
        {
            // null is always contained in a tree
            if( x == null ) return true;

            //rwLock.AcquireReaderLock( Timeout.Infinite );

            try
            {
                return ( FindNode( x ) != null );
            } finally
            {
                //rwLock.ReleaseReaderLock();
            }
        }

        /// <summary>
        /// Copies the elements of RBTree to a one dimensional
        /// System.Array at the specified index.
        /// </summary>
        public void CopyTo( T[] array, int index )
        {
            // Check the validity of the arguments
            if( array == null ) throw new ArgumentNullException();
            if( index < 0 ) throw new ArgumentOutOfRangeException();
            if( array.Rank > 1 || ( array.Length - index ) < count )
                throw new ArgumentException();

            //rwLock.AcquireReaderLock( Timeout.Infinite );

            try
            {
                int i = 0;
                foreach (var itr in this)
                    if (i >= array.Length)
                        break;
                    else if (i >= index)
                        array[i++] = itr;
                    else
                        ++i;
            } finally
            {
                //rwLock.ReleaseReaderLock();
            }
        }

        /// <remarks>
        /// RBTreeEnumerator could be instancied only through the
        /// RBTree.GetEnumerator method. If the RBTree is modified
        /// after the instanciation of RBTreeEnumerator, then
        /// RBTreeEnumerator become invalid. Any attempt to read or
        /// iterate will throw an exception. The elements contained
        /// in the RBTree are iterated following the order provided
        /// to the RBTree (ascending order).
        /// </remarks>
        public class Enumerator : IEnumerator<T>, ICloneable
        {
            /// <summary>
            /// The current node (or null if none)
            /// </summary>
            RBTreeNode<T> current;

            /// <summary>
            /// Reference to the RBTree which has instanciated the
            /// RBTreeEnumerator.
            /// </summary>
            RBTree<T> tree;

            /// <summary>
            /// Store the state of the RBTreeEnumerator. If 
            /// <c>!started</c> then the current position is
            /// before the first element of the RBTree.
            /// </summary>
            bool started;

            /// <summary>
            /// Store the the state of the RBTreeEnumerator. If
            /// <c>!isValid</c>, any attempt to read or iterate 
            /// will throw an exception.
            /// </summary>
            bool isValid;

            /// <summary>
            /// Initializes an new instance of Collections.System.RBTreeEnumerator
            /// class. The current position is before the first element.
            /// </summary>
            /// <param name="t">The RBTree which will be enumerate.</param>
            internal Enumerator( RBTree<T> t )
            {
                tree = t;
                if( t.root == null && t.Count > 0 )
                    throw new InvalidOperationException( "The RBTree has null root but non-zero size" );
                started = false;
                isValid = true;
                current = tree.root;
                if( current != null )
                {
                    while( current.Left != null )
                        current = current.Left;
                }
            }

            internal Enumerator( RBTree<T> t, RBTreeNode<T> cur )
            {
                tree = t;
                started = ( cur == tree.root ? false : true );
                isValid = true;
                current = cur;
            }

            internal Enumerator( RBTree<T> t, RBTreeNode<T> cur, bool forceStarted )
            {
                tree = t;
                started = forceStarted;
                isValid = true;
                current = cur;
            }

            internal RBTreeNode<T> CurrentNode
            {
                get
                {
                    if( current == null ) throw new InvalidOperationException( "After last element" );
                    if( !isValid ) throw new InvalidOperationException( "The RBTree was modified after the enumerator was created" );
                    if( !started ) throw new InvalidOperationException( "Before first element" );
                    return current;
                }
            }

            /// <summary>
            /// Gets the current element in the RBTree.
            /// </summary>
            public T Current
            {
                get
                {
                    return CurrentNode.Key;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return (object) Current;
                }
            }

            /// <summary>
            /// Advances the RBTreeEnumerator to the next element of the RBTree.
            /// Returns whether the move was possible.
            /// </summary>
            public bool MoveNext()
            {
                if( !isValid ) throw new InvalidOperationException( "The RBTree was modified after the enumerator was created" );
                if( !started )
                {
                    started = true;
                    return current != null;
                }
                if( current == null )
                    return false;
                if( current.Right == null )
                {
                    RBTreeNode<T> prev;
                    do
                    {
                        prev = current;
                        current = current.Father;
                    } while( ( current != null ) && ( current.Right == prev ) );
                } else
                {
                    current = current.Right;
                    while( current.Left != null )
                        current = current.Left;
                }
                return current != null;
            }

            /// <summary>
            /// Advances the RBTreeEnumerator to the previous element of the RBTree.
            /// Returns whether the move was possible.
            /// </summary>
            public bool MovePrev()
            {
                if( !isValid ) throw new InvalidOperationException( "The RBTree was modified after the enumerator was created" );
                if( !started )
                    return false;
                if( current == null )
                {
                    current = tree.MaxNode();
                } else if( current.Left == null )
                {
                    RBTreeNode<T> prev;
                    do
                    {
                        prev = current;
                        current = current.Father;
                    } while( ( current != null ) && ( current.Left == prev ) );
                } else
                {
                    current = current.Left;
                    while( current.Right != null )
                        current = current.Right;
                }
                return current != null;
            }

            /// <summary>
            /// Sets the enumerator the its initial position which is before
            /// the first element of the RBTree.
            /// </summary>
            public void Reset()
            {
                if( !isValid ) throw new InvalidOperationException( "The RBTree was modified after the enumerator was created" );
                started = false;
                current = tree.root;
                if( current != null )
                {
                    while( current.Left != null )
                        current = current.Left;
                }
            }

            /// <summary>
            /// Returns true iff the enumerator points at a valid element. If it is false, it may mean the enumerator has not been initialized, or it can be considered pointing at the "end" of the RBTree.
            /// </summary>
            public bool IsValid
            {
                get
                {
                    if( current == null || !isValid || !started )
                        return false;
                    return true;
                }
            }

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void Dispose()
            {
            }

            /// <summary>
            /// Invalidates the RBTreeEnumerator.
            /// </summary>
            internal void Invalidate()
            {
                isValid = false;
            }

            /// <summary>
            /// Returns a copy of this enumerator which can be stored or enumerated separately.
            /// </summary>
            public object Clone()
            {
                return new Enumerator( this.tree, this.current );
            }
        }

        /// <summary>
        /// Returns an System.Collection.IEnumerator that can iterate
        /// through the RBTree.
        /// </summary>
        /// 
        public Enumerator GetEnumerator()
        {
            Enumerator tEnum;

            //rwLock.AcquireReaderLock( Timeout.Infinite );

            try
            {
                tEnum = new Enumerator( this );
                //RBTreeModified += new RBTreeModifiedHandler( tEnum.Invalidate );
            } finally
            {
                //rwLock.ReleaseReaderLock();
            }

            return tEnum;
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return (IEnumerator<T>) GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator) GetEnumerator();
        }

        /// <summary>
        /// FOR TESTING: returns the number of times an RBTreeEnumerator can be advanced, which should be equal to the other Count property.
        /// </summary>
        public int EnumeratedCount
        {
            get
            {
                int count = 0;
                Enumerator itr = GetEnumerator();
                while( itr.MoveNext() )
                    ++count;
                return count;
            }
        }

        /// <summary>
        /// Removes the first occurrence of the element in the RBTree.
        /// The operation is performed in a guaranteed logarithmic time
        /// of the RBTree size.
        /// </summary>
        public bool Remove( T x )
        {
            //rwLock.AcquireWriterLock( Timeout.Infinite );

            try
            {
                RBTreeNode<T> node = FindNode( x );
                if( node != null )
                {
                    RemoveRBTreeNode( node );
                    //if( Count != EnumeratedCount ) throw new InvalidOperationException( "EnumeratedCount does not match stored count" );
                    return true;
                } else
                    return false;
            } finally
            {
                //rwLock.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Removes the element pointed to by the given enumerator; this allows not having to redo the element search.
        /// </summary>
        public void Remove( Enumerator x )
        {
            if( !x.IsValid )
                return;

            RemoveRBTreeNode( x.CurrentNode );
        }

        /// <summary>
        /// Returns an enumerator to the first element greater than or equal to the given element
        /// (i.e. the first position where the element could be inserted without violating ordering).
        /// </summary>
        public Enumerator LowerBound( T x )
        {
            //rwLock.AcquireReaderLock( Timeout.Infinite );
            try
            {
                return new Enumerator( this, LowerBoundNode( x ), true );
            } finally
            {
                //rwLock.ReleaseReaderLock();
            }
        }

        /// <summary>
        /// Returns an enumerator to the first element greater than the given element, or an invalid enumerator if the element is greater than all others in the map
        /// (i.e. the last position where the element could be inserted without violating ordering).
        /// </summary>
        public Enumerator UpperBound( T x )
        {
            //rwLock.AcquireReaderLock( Timeout.Infinite );
            try
            {
                return new Enumerator( this, UpperBoundNode( x ), true );
            } finally
            {
                //rwLock.ReleaseReaderLock();
            }
        }

        private RBTreeNode<T> FindNode( T x )
        {
            RBTreeNode<T> node = LowerBoundNode( x );
            if( node == null || Comparer.Compare( x, node.Key ) < 0 )
                node = null;
            return node;
        }

        /// <summary>
        /// Returns an enumerator for the given element if it is in the map, or an invalid enumerator if it's not.
        /// </summary>
        public Enumerator Find( T x )
        {
            //rwLock.AcquireReaderLock( Timeout.Infinite );
            try
            {
                return new Enumerator( this, FindNode( x ), true );
            } finally
            {
                //rwLock.ReleaseReaderLock();
            }
        }

        private RBTreeNode<T> LowerBoundNode( T x )
        {
            RBTreeNode<T> node = root;
            RBTreeNode<T> lowerBoundNode = null;

            int compare;
            while( node != null )
            {
                compare = Comparer.Compare( node.Key, x );
                if( compare < 0 )
                    node = node.Right; // descend right subtree
                else
                {
                    // node not less than x, remember it
                    lowerBoundNode = node;
                    node = node.Left;    // descend left subtree
                }
            }

            return lowerBoundNode; // return best remembered candidate
        }

        private RBTreeNode<T> UpperBoundNode( T x )
        {
            RBTreeNode<T> node = root;
            RBTreeNode<T> upperBoundNode = null;

            while( node != null )
            {
                if( Comparer.Compare( x, node.Key ) < 0 )
                {
                    // node greater than x, remember it
                    upperBoundNode = node;
                    node = node.Left; // descend left subtree
                }  else
                    node = node.Right; // descend right subtree
            }

            return upperBoundNode; // return best remembered candidate
        }

        /// <summary>
        /// Invalidates the System.Collections.IEnumerator linked
        /// with the RBTree.
        /// </summary>
        private void OnRBTreeModified()
        {
            /*( RBTreeModified != null )
            {
                RBTreeModified();
                RBTreeModified = null;
            }*/
        }

        /// <summary>
        /// Removes a specific node of the RBTree.
        /// </summary>
        /// <param name="node">
        /// node must be contained by RBTree.</param>
        private void RemoveRBTreeNode( RBTreeNode<T> node )
        {
            RBTreeNode<T> nodeX, nodeY, fatherX, fatherY;

            if(node.Left == null || node.Right == null) nodeY = node;
            else nodeY = Successor(node);
            if(nodeY.Left != null) nodeX = nodeY.Left;
            else nodeX = nodeY.Right;

            fatherY = nodeY.Father;
            fatherX = fatherY;
            if(nodeX != null) nodeX.Father = nodeY.Father;

            if(fatherY == null) root = nodeX;
            else 
            {
                if(nodeY == fatherY.Left) fatherY.Left = nodeX;
                else fatherY.Right = nodeX;
            }

            if(nodeY != node) node.Key = nodeY.Key;

            // Remove Correction of the colors
            if(nodeY == null || !nodeY.Color)
            {
                while(nodeX != root && (nodeX == null || !nodeX.Color))
                {
                    if(nodeX == fatherX.Left /*&& nodeX != fatherX.Right*/)
                    {
                        fatherY = fatherX;
                        nodeY = fatherX.Right;
                        if(/*nodeY != null && */nodeY.Color)
                        {
                            nodeY.Color = false;
                            fatherX.Color = true;
                            RotateLeft(fatherX);
                            nodeY = fatherX.Right;
                        }

                        if((nodeY.Left == null || !nodeY.Left.Color) 
                            && (nodeY.Right == null || !nodeY.Right.Color)) 
                        {
                            nodeY.Color = true;
                            nodeX = fatherX;
                            fatherX = fatherX.Father;
                        }
                        else 
                        {
                            if(nodeY.Right == null || !nodeY.Right.Color)
                            {
                                nodeY.Left.Color = false;
                                nodeY.Color = true;
                                RotateRight(nodeY);
                                nodeY = fatherX.Right;
                            }

                            nodeY.Color = fatherX.Color;
                            fatherX.Color = false;
                            nodeY.Right.Color = false;
                            RotateLeft(fatherX);
                            nodeX = root;
                        }
                    } 
                    else
                    {
                        fatherY = fatherX;
                        nodeY = fatherX.Left;
                        if(/*nodeY != null &&*/ nodeY.Color)
                        {
                            nodeY.Color = false;
                            fatherX.Color = true;
                            RotateRight(fatherX);
                            nodeY = fatherX.Left;
                        }

                        if((nodeY.Right == null || !nodeY.Right.Color) 
                            && (nodeY.Left == null || !nodeY.Left.Color))
                        {
                            nodeY.Color = true;
                            nodeX = fatherX;
                            fatherX = fatherX.Father;
                        }
                        else 
                        {
                            if(nodeY.Left == null || !nodeY.Left.Color)
                            {
                                nodeY.Right.Color = false;
                                nodeY.Color = true;
                                RotateLeft(nodeY);
                                nodeY = fatherX.Left;
                            }

                            nodeY.Color = fatherX.Color;
                            fatherX.Color = false;
                            nodeY.Left.Color = false;
                            RotateRight(fatherX);
                            nodeX = root;
                        }
                    }
                } // End While

                if(nodeX != null) nodeX.Color = false;
            } // End Correction

            count--;
        }


        /// <summary>
        /// Returns the node that contains the successor of node.Key.
        /// If such node does not exist then null is returned.
        /// </summary>
        /// <param name="node">
        /// node must be contained by RBTree.</param>
        private RBTreeNode<T> Successor( RBTreeNode<T> node )
        {
            RBTreeNode<T> node1, node2;

            if( node.Right != null )
            {
                // We find the Min
                node1 = node.Right;
                while( node1.Left != null )
                    node1 = node1.Left;
                return node1;
            }

            node1 = node;
            node2 = node.Father;
            while( node2 != null && node1 == node2.Right )
            {
                node1 = node2;
                node2 = node2.Father;
            }
            return node2;
        }


        /// <summary>
        /// Performs a left tree rotation.
        /// </summary>
        /// <param name="node">
        /// node is considered as the root of the tree.</param>
        private void RotateLeft( RBTreeNode<T> node )
        {
            RBTreeNode<T> nodeX = node, nodeY = node.Right;
            nodeX.Right = nodeY.Left;

            if(nodeY.Left != null) nodeY.Left.Father = nodeX;
            nodeY.Father = nodeX.Father;

            if(nodeX.Father == null) root = nodeY;
            else 
            {
                if(nodeX == nodeX.Father.Left)
                    nodeX.Father.Left = nodeY;
                else nodeX.Father.Right = nodeY;
            }

            nodeY.Left = nodeX;
            nodeX.Father = nodeY;
        }


        /// <summary>
        /// Performs a right tree rotation.
        /// </summary>
        /// <param name="node">
        /// node is considered as the root of the tree.</param>
        private void RotateRight( RBTreeNode<T> node )
        {
            RBTreeNode<T> nodeX = node, nodeY = node.Left;
            nodeX.Left = nodeY.Right;

            if(nodeY.Right != null) nodeY.Right.Father = nodeX;
            nodeY.Father = nodeX.Father;

            if(nodeX.Father == null) root = nodeY;
            else 
            {
                if(nodeX == nodeX.Father.Right)
                    nodeX.Father.Right = nodeY;
                else nodeX.Father.Left = nodeY;
            }

            nodeY.Right = nodeX;
            nodeX.Father = nodeY;
        }


        /// <summary>
        /// Copies the element of the tree into a one dimensional
        /// System.Array starting at index.
        /// </summary>
        /// <param name="currentRBTreeNode">The root of the tree.</param>
        /// <param name="array">The System.Array where the elements will be copied.</param>
        /// <param name="index">The index where the copy will start.</param>
        /// <returns>
        /// The new index after the copy of the elements of the tree.
        /// </returns>
        private int RecCopyTo( RBTreeNode<T> currentRBTreeNode, T[] array, int index )
        {
            if(currentRBTreeNode != null) 
            {
                array.SetValue(currentRBTreeNode.Key, index);
                return RecCopyTo(currentRBTreeNode.Right, array,
                    RecCopyTo(currentRBTreeNode.Left, array, index + 1));
            }
            else return index;
        }


        /// <summary>
        /// Returns a node of the tree which contains the object
        /// as Key. If the tree does not contain such node, then
        /// null is returned.
        /// </summary>
        /// <param name="node">The root of the tree.</param>
        /// <param name="x">The researched object.</param>
        private RBTreeNode<T> RecContains( RBTreeNode<T> node, T x )
        {
            if(node == null) return null;

            int c = Comparer.Compare(x, node.Key);

            if(c == 0) return node;
            if(c < 0) return RecContains(node.Left, x);
            else return RecContains(node.Right, x);
        }


        /// <summary>
        /// For debugging only. Checks whether the RBTree is conform
        /// to the definition of the a red-black tree. If not an
        /// exception is thrown.
        /// </summary>
        /// <param name="node">The root of the tree.</param>
        private int RecConform( RBTreeNode<T> node )
        {
            if(node == null) return 1;

            if(node.Father == null) 
            {
                if(node.Color) throw new ArgumentException("RBTree : the root is not black.");
            } 
            else 
            {
                if(node.Father.Color && node.Color)
                    throw new ArgumentException("RBTree : father and son are red.");
            }

            if(node.Left != null && Comparer.Compare(node.Key, node.Left.Key) < 0) 
                throw new ArgumentException("RBTree : order not respected in tree.");
            if(node.Right != null && Comparer.Compare(node.Key, node.Right.Key) > 0)
                throw new ArgumentException("RBTree : order not respected in tree.");

            int a = RecConform(node.Left),
                b = RecConform(node.Right);

            if(a < 0 || b < 0) return -1;

            if(a != b) throw new ArgumentException("RBTree : the paths do have not the  same number of black nodes.");

            if(!node.Color) return (a+1);
            else return a;
        }

    }

    /// <remarks>
    /// RBTreeNode is simple colored binary tree node which
    /// contains a key.
    /// </remarks>
    internal class RBTreeNode<T>
    {
        /// <summary>
        /// References to the other elements of the RBTree.
        /// </summary>
        RBTreeNode<T> father, left, right;

        /// <summary>
        /// Reference to the object contained by the RBTreeNode.
        /// </summary>
        T key;

        /// <summary>
        /// The color of the node (red = true, black = false).
        /// </summary>
        bool color;

        /// <summary>
        /// Initializes an new instance of Collections.System.RBTreeNode
        /// class. All references are set to null.
        /// </summary>
        internal RBTreeNode()
        {
            key = default(T);
            father = null;
            left = null;
            right = null;
            color = true;
        }

        /// <summary>
        /// Initializes an new instance of Collections.System.RBTreeNode
        /// class and partially insert the RBTreeNode into a tree.
        /// </summary>
        /// <param name="k">Key of the RBTreeNode</param>
        /// <param name="fatherRBTreeNode">The father node of the instanciated RBTreeNode.</param>
        internal RBTreeNode(T k, RBTreeNode<T> fatherRBTreeNode)
        {
            key = k;
            father = fatherRBTreeNode;
            left = null;
            right = null;
            color = true;
        }

        internal RBTreeNode( RBTreeNode<T> otherNode )
        {
            key = otherNode.key;
            father = otherNode.father;
            left = otherNode.left;
            right = otherNode.right;
            color = otherNode.color;
        }

        /// <summary>
        /// Gets or sets the key of the RBTreeNode.
        /// </summary>
        internal T Key 
        {
            get
            {
                return key;
            }
            set 
            {
                key = value;
            }
        }

        /// <summary>
        /// Gets or sets the father of the RBTreeNode.
        /// </summary>
        internal RBTreeNode<T> Father
        {
            get 
            {
                return father;
            }
            set 
            {
                father = value;
            }
        }

        /// <summary>
        /// Gets or sets the left children of the RBTreeNode.
        /// </summary>
        internal RBTreeNode<T> Left
        {
            get
            {
                return left;
            }
            set
            {
                left = value;
            }
        }

        /// <summary>
        /// Gets or sets the right children of the RBTreeNode.
        /// </summary>
        internal RBTreeNode<T> Right
        {
            get
            {
                return right;
            }
            set 
            {
                right = value;
            }
        }

        /// <summary>
        /// Gets or sets the color of the RBTreeNode.
        /// </summary>
        internal bool Color
        {
            get 
            {
                return color;
            }
            set 
            {
                color = value;
            }
        }
    }
}
