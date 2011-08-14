/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
/*
 * The base implementation of the red-black tree was taken from:
 * http://www.codeproject.com/KB/recipes/redblackcs.aspx
 * It is covered by the "Code Project Open License":
 * http://www.codeproject.com/info/cpol10.aspx
 * 
 * The code was modified to enable finding nodes by index,
 * and changed so that leaves in the tree are represented by nulls
 * instead of empty nodes.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Common.Collections
{
    ///<summary>
    ///A red-black tree must satisfy these properties:
    ///
    ///1. The root is black. 
    ///2. All leaves are black. 
    ///3. Red nodes can only have black children (null's count as black)
    ///4. All paths from a node to its leaves contain the same number of black nodes.
    ///</summary>
	public class RedBlackTree<TKey,TValue> : IEnumerable<RedBlackTree<TKey,TValue>.Node>
        where TKey:IComparable
	{
        // the tree
		private Node root;

		public RedBlackTree() 
        {
		    root = null;
        }
        public RedBlackTree(RedBlackTree<TKey,TValue> other)
        {
            if (other.root != null)
            {
                root = new Node(other.root) {Tree = this};
            }
        }

        public bool Validate()
        {
            if (root == null)
            {
                return true;
            }
            if (root.Parent != null)
            {
                throw new InvalidDataException("Root cannot have a parent");
            }
            root.CheckValidAndCountBlackNodesToLeaves();
            return true;
        }

	    public Node First
	    {
	        get
	        {
                if (root == null)
	            {
	                return null;
	            }
	            var node = root;
                while (node.Left != null)
                {
                    node = node.Left;
                }
	            return node;
	        }
	    }

	    public Node Last
	    {
	        get
	        {
	            if (null == root)
	            {
	                return null;
	            }
	            var node = root;
                while (node.Right != null)
                {
                    node = node.Right;
                }
	            return node;
	        }
	    }

	    public Node this[int index]
	    {
	        get
	        {
	            int currentIndex = 0;
	            var node = root;
                while (node != null)
                {
                    if (node.Left != null && currentIndex + node.Left.Weight > index)
                    {
                        node = node.Left;
                        continue;
                    }
                    currentIndex += (node.Left == null ? 0 : node.Left.Weight);
                    if (currentIndex == index)
                    {
                        return node;
                    }
                    currentIndex++;
                    node = node.Right;
                }
                throw new ArgumentException();
	        }
            set
            {
                throw new NotSupportedException();
            }
	    }

        ///<summary>
		/// Add
		/// args: ByVal key As IComparable, ByVal data As Object
		/// key is object that implements IComparable interface
		/// performance tip: change to use use int type (such as the hashcode)
		///</summary>
		public Node Add(TKey key, TValue data)
		{
			if(key == null)
				throw(new ArgumentException("RedBlackNode key must not be null"));
			
			// traverse tree - find where node belongs
			int result			=	0;
			// create new node
			Node node	=	new Node(this);
			Node temp	=	root;				// grab the rbTree node of the tree

			while(temp != null)
			{	// find Parent
				node.Parent	= temp;
				result		=  key.CompareTo(temp.Key);
                if (result == 0)
                    throw (new InvalidOperationException("A Node with the same key already exists"));
				if(result > 0)
					temp = temp.Right;
				else
					temp = temp.Left;
			}
			
            // setup node
			node.Key			=	key;
			node.Value			=	data;
            node.Left = null;
			node.Right          =   null;

			// insert node into tree starting at parent's location
			if(node.Parent != null)	
			{
				result	=  node.Key.CompareTo(node.Parent.Key);
				if(result > 0)
					node.Parent.Right = node;
				else
					node.Parent.Left = node;
			}
			else
				root = node;					// first node added

            RestoreAfterInsert(node);           // restore red-black properities
            return node;
		}
        ///<summary>
        /// RestoreAfterInsert
        /// Additions to red-black trees usually destroy the red-black 
        /// properties. Examine the tree and restore. Rotations are normally 
        /// required to restore it
        ///</summary>
		private void RestoreAfterInsert(Node x)
		{   
            // x and y are used as variable names for brevity, in a more formal
            // implementation, you should probably change the names

			Node y;

			// maintain red-black tree properties after adding x
			while(x != root && x.Parent.IsRed)
			{
				// Parent node is .Colored red; 
				if(x.Parent == x.Parent.Parent.Left)	// determine traversal path			
				{										// is it on the Left or Right subtree?
					y = x.Parent.Parent.Right;			// get uncle
					if(y != null && y.IsRed)
					{	// uncle is red; change x's Parent and uncle to black
						x.Parent.IsBlack = true;
						y.IsBlack = true;
						// grandparent must be red. Why? Every red node that is not 
						// a leaf has only black children 
						x.Parent.Parent.IsRed = true;	
						x = x.Parent.Parent;	// continue loop with grandparent
					}	
					else
					{
						// uncle is black; determine if x is greater than Parent
						if(x == x.Parent.Right) 
						{	// yes, x is greater than Parent; rotate Left
							// make x a Left child
							x = x.Parent;
							RotateLeft(x);
						}
						// no, x is less than Parent
						x.Parent.IsRed =false;	// make Parent black
						x.Parent.Parent.IsRed = true;		// make grandparent red
						RotateRight(x.Parent.Parent);					// rotate right
					}
				}
				else
				{	// x's Parent is on the Right subtree
					// this code is the same as above with "Left" and "Right" swapped
					y = x.Parent.Parent.Left;
					if(y != null && y.IsRed)
					{
						x.Parent.IsBlack = true;
						y.IsBlack = true;
						x.Parent.Parent.IsRed = true;
						x						= x.Parent.Parent;
					}
					else
					{
						if(x == x.Parent.Left)
						{
							x = x.Parent;
							RotateRight(x);
						}
						x.Parent.IsBlack = true;
						x.Parent.Parent.IsRed = true;
						RotateLeft(x.Parent.Parent);
					}
				}																													
			}
			root.IsBlack = true;		// rbTree should always be black
		}
		
		///<summary>
		/// RotateLeft
		/// Rebalance the tree by rotating the nodes to the left
		///</summary>
		private void RotateLeft(Node x)
		{
			// pushing node x down and to the Left to balance the tree. x's Right child (y)
			// replaces x (since y > x), and y's Left child becomes x's Right child 
			// (since it's < y but > x).
            
			Node y = x.Right;			// get x's Right node, this becomes y

			// set x's Right link
            x.Right = y.Left;					// y's Left child's becomes x's Right child
            // modify parents
            if (y.Left != null)
                y.Left.Parent = x;				// sets y's Left Parent to x
            y.Parent = x.Parent;			// set y's Parent to x's Parent

			if(x.Parent != null)		
			{	// determine which side of it's Parent x was on
				if(x == x.Parent.Left)			
					x.Parent.Left = y;			// set Left Parent to y
				else
					x.Parent.Right = y;			// set Right Parent to y
			} 
			else 
				root = y;						// at rbTree, set it to y

			// link x and y 
			y.Left = x;							// put x on y's Left 
            x.Parent = y;		
		}
		///<summary>
		/// RotateRight
		/// Rebalance the tree by rotating the nodes to the right
		///</summary>
		private void RotateRight(Node x)
		{
			// pushing node x down and to the Right to balance the tree. x's Left child (y)
			// replaces x (since x < y), and y's Right child becomes x's Left child 
			// (since it's < x but > y).
            
			Node y = x.Left;			// get x's Left node, this becomes y

			// set x's Right link
			x.Left = y.Right;					// y's Right child becomes x's Left child

			// modify parents
			if(y.Right != null) 
				y.Right.Parent = x;				// sets y's Right Parent to x

            y.Parent = x.Parent;			// set y's Parent to x's Parent

			if(x.Parent != null)				// null=rbTree, could also have used rbTree
			{	// determine which side of it's Parent x was on
				if(x == x.Parent.Right)			
					x.Parent.Right = y;			// set Right Parent to y
				else
					x.Parent.Left = y;			// set Left Parent to y
			} 
			else 
				root = y;						// at rbTree, set it to y

			// link x and y 
			y.Right = x;						// put x on y's Right
			// set y as x's Parent
			x.Parent = y;		
		}		
        IEnumerator IEnumerable.GetEnumerator()
		{
		    return GetEnumerator();
		}
        IEnumerator<Node> IEnumerable<Node>.GetEnumerator()
        {
            return GetEnumerator();
        }
        ///<summary>
		/// GetEnumerator
		/// return an enumerator that returns the tree nodes in order
		///<summary>
		public RedBlackEnumerator GetEnumerator()
		{
            // elements is simply a generic name to refer to the 
            // data objects the nodes contain
			return new RedBlackEnumerator(this);
		}
		///<summary>
		/// IsEmpty
		/// Is the tree empty?
		///<summary>
		public bool IsEmpty()
		{
			return root == null;
		}
		///<summary>
		/// Remove
		/// removes the key and data object (delete)
		///<summary>
		public void RemoveKey(TKey key)
		{
            if(key == null)
                throw(new ArgumentException("RedBlackNode key is null"));
		
			// find node
			int	result;
			Node node = root;
			while(node != null)
			{
				result = key.CompareTo(node.Key);
				if(result == 0)
					break;
				if(result < 0)
					node = node.Left;
				else
					node = node.Right;
			}

			if(node == null)
				return;				// key not found

			Delete(node);
		}
        public void RemoveAt(int index)
        {
            Delete(this[index]);
        }
		///<summary>
		/// Delete
		/// Delete a node from the tree and restore red black properties
		///</summary>
		private void Delete(Node nodeToBeDeleted)
		{
			// A node to be deleted will be: 
			//		1. a leaf with no children
			//		2. have one child
			//		3. have two children
			// If the deleted node is red, the red black properties still hold.
			// If the deleted node is black, the tree needs rebalancing

			Node nodeWithAtMostOneChild;					// work node 

			// find the replacement node (the successor to x) - the node one with 
			// at *most* one child. 
			if(nodeToBeDeleted.Left == null || nodeToBeDeleted.Right == null) 
				nodeWithAtMostOneChild = nodeToBeDeleted;						// node has sentinel as a child
			else 
			{
				// z has two children, find replacement node which will 
				// be the leftmost node greater than z
				nodeWithAtMostOneChild = nodeToBeDeleted.Right;				        // traverse right subtree	
				while(nodeWithAtMostOneChild.Left != null)		// to find next node in sequence
					nodeWithAtMostOneChild = nodeWithAtMostOneChild.Left;
			}

            Swap(nodeWithAtMostOneChild, nodeToBeDeleted);
            var parentOfDeletedNode = nodeToBeDeleted.Parent;
            // x (y's only child) is the node that will be linked to y's old parent. 
            Node newChild = nodeToBeDeleted.Left ?? nodeToBeDeleted.Right;
            if (parentOfDeletedNode == null)
            {
                root = newChild; // make x the root node
                if (root != null)
                {
                    root.Parent = null;
                    root.IsBlack = true;
                }
                return;
            }
            bool wasLeftChild;
            if (nodeToBeDeleted == nodeToBeDeleted.Parent.Left)
            {
                nodeToBeDeleted.Parent.Left = newChild;
                wasLeftChild = true;
            }
            else
            {
                nodeToBeDeleted.Parent.Right = newChild;
                wasLeftChild = false;
            }
            if (newChild != null)
			{
			    // replace x's parent with y's parent and
			    // link x to proper subtree in parent
			    // this removes y from the chain
                newChild.Parent = nodeToBeDeleted.Parent;
                if (wasLeftChild)
                {
                    newChild.Parent.Left = newChild;
                }
                else
                {
                    newChild.Parent.Right = newChild;
                }
			}
		    nodeToBeDeleted.Tree = null;
            if (nodeToBeDeleted.IsRed)
            {
                return;
            }
            if (newChild != null)
            {
                newChild.IsBlack = true;
                return;
            }

            while (parentOfDeletedNode != null)
            {
                var child = wasLeftChild ? parentOfDeletedNode.Left : parentOfDeletedNode.Right;
                if (child != null && child.IsRed)
                {
                    child.IsBlack = true;
                    return;
                }
                Node sibling = wasLeftChild ? parentOfDeletedNode.Right : parentOfDeletedNode.Left;
                if (sibling.IsRed)
                {
                    sibling.IsBlack = true;
                    parentOfDeletedNode.IsRed = true;
                    if (wasLeftChild)
                    {
                        RotateLeft(parentOfDeletedNode);
                        sibling = parentOfDeletedNode.Right;
                    }
                    else
                    {
                        RotateRight(parentOfDeletedNode);
                        sibling = parentOfDeletedNode.Left;
                    }
                }
                if ((sibling.Left == null || sibling.Left.IsBlack) && (sibling.Right == null || sibling.Right.IsBlack))
                {
                    sibling.IsRed = true;
                    if (parentOfDeletedNode.Parent == null)
                    {
                        parentOfDeletedNode.IsBlack = true;
                        return;
                    }
                    wasLeftChild = parentOfDeletedNode == parentOfDeletedNode.Parent.Left;
                    parentOfDeletedNode = parentOfDeletedNode.Parent;
                }
                else
                {
                    var nephew = wasLeftChild ? sibling.Right : sibling.Left;
                    if (nephew == null || nephew.IsBlack)
                    {
                        var otherNephew = wasLeftChild ? sibling.Left : sibling.Right;
                        if (otherNephew != null)
                        {
                            otherNephew.IsBlack = true;
                        }
                        sibling.IsRed = true;
                        if (wasLeftChild)
                        {
                            RotateRight(sibling);
                            sibling = parentOfDeletedNode.Right;
                        }
                        else
                        {
                            RotateLeft(sibling);
                            sibling = parentOfDeletedNode.Left;
                        }
                    }
                    sibling.IsRed = parentOfDeletedNode.IsRed;
                    parentOfDeletedNode.IsBlack = true;
                    if (wasLeftChild)
                    {
                        if (sibling.Right != null)
                        {
                            sibling.Right.IsBlack = true;
                        }
                        RotateLeft(parentOfDeletedNode);
                    }
                    else
                    {
                        if (sibling.Left != null)
                        {
                            sibling.Left.IsBlack = true;
                        }
                        RotateRight(parentOfDeletedNode);
                    }
                    
                    root.IsBlack = true;
                    return;
                }
            }
		}

        private void Swap(Node node1, Node node2)
        {
            if (node1 == node2)
            {
                return;
            }
            
            var parent1 = node1.Parent;
            var left1 = node1.Left;
            var right1 = node1.Right;
            var wasLeftChild1 = parent1 != null && node1 == node1.Parent.Left;

            var parent2 = node2.Parent;
            var left2 = node2.Left;
            var right2 = node2.Right;
            var wasLeftChild2 = parent2 != null && node2 == node2.Parent.Left;

            node1.Left = node1.Right = node1.Parent = null;
            node2.Left = node2.Right = node2.Parent = null;

            if (parent1 == null)
            {
                Debug.Assert(root == node1);
                node2.Parent = null;
                root = node2;
            }
            else
            {
                if (parent1 == node2)
                {
                    node2.Parent = node1;
                }
                else
                {
                    node2.Parent = parent1;
                }
                if (wasLeftChild1)
                {
                    node2.Parent.Left = node2;
                }
                else
                {
                    node2.Parent.Right = node2;
                }
            }
            if (left1 != node2)
            {
                if (left1 != null)
                {
                    left1.Parent = node2;
                }
                node2.Left = left1;
            }
            if (right1 != node2)
            {
                if (right1 != null)
                {
                    right1.Parent = node2;
                }
                node2.Right = right1;
            }

            if (parent2 == null)
            {
                Debug.Assert(root == node2);
                root = node1;
            }
            else
            {
                if (parent2 == node1)
                {
                    node1.Parent = node2;
                }
                else
                {
                    node1.Parent = parent2;
                }
                if (wasLeftChild2)
                {
                    node1.Parent.Left = node1;
                }
                else
                {
                    node1.Parent.Right = node1;
                }
            }
            if (left2 != node1)
            {
                if (left2 != null)
                {
                    left2.Parent = node1;
                }
                node1.Left = left2;
            }
            if (right2 != node1)
            {
                if (right2 != null)
                {
                    right2.Parent = node1;
                }
                node1.Right = right2;
            }
            var tmpIsRed = node1.IsRed;
            node1.IsRed = node2.IsRed;
            node2.IsRed = tmpIsRed;
        }


        ///<summary>
        /// RestoreAfterDelete
        /// Deletions from red-black trees may destroy the red-black 
        /// properties. Examine the tree and restore. Rotations are normally 
        /// required to restore it
        ///</summary>
		private void RestoreAfterDelete(Node parent, bool wasLeftChild)
		{
			// maintain Red-Black tree balance after deleting node 			

			Node y;

			while(parent != null) 
			{
			    var child = wasLeftChild ? parent.Left : parent.Right;
                if (child != null && child.IsRed)
                {
                    child.IsBlack = true;
                    return;
                }
				if(wasLeftChild)			// determine sub tree from parent
				{
					y = parent.Right;			// y is x's sibling 
					if(IsRed(y)) 
					{	// x is black, y is red - make both black and rotate
						y.IsBlack = true;
						parent.IsRed = true;
						RotateLeft(parent);
						y = parent.Right;
					}
					if(IsBlack(y.Left) && IsBlack(y.Right)) 
					{	// children are both black
						y.IsRed = true;		// change parent to red
						if (parent.Parent != null)
						{
						    wasLeftChild = parent == parent.Parent.Left;
						    parent = parent.Parent;
						}
                        else
						{
						    return;
						}
					} 
					else 
					{
						if(IsBlack(y.Right)) 
						{
                            y.Left.IsBlack = true;
							y.IsRed = true;
							RotateRight(y);
							y = parent.Right;
						}
						y.IsBlack = parent.IsBlack;
						parent.IsBlack = true;
                        if (y.Right != null)
                        {
                            y.Right.IsBlack = true;
                        }
						RotateLeft(parent);
						root.IsBlack = true;
					    return;
					}
				} 
				else 
				{	// right subtree - same as code above with right and left swapped
					y = parent.Left;
					if(y.IsRed) 
					{
						y.IsBlack = true;
						parent.IsRed = true;
						RotateRight (parent);
						y = parent.Left;
					}
					if(IsBlack(y.Right) && 
						IsBlack(y.Left)) 
					{
						y.IsRed = true;
					    if (parent.Parent != null)
					    {
					        wasLeftChild = parent == parent.Parent.Left;
					        parent = parent.Parent;
					    }
                        else
					    {
					        return;
					    }
					} 
					else 
					{
						if(y.Left.IsBlack) 
						{
							y.Right.IsBlack = true;
							y.IsRed = true;
							RotateLeft(y);
							y = parent.Left;
						}
						y.IsRed = parent.IsRed;
						parent.IsBlack = true;
						y.Left.IsBlack = true;
						RotateRight(parent);
						root.IsBlack = true;
					    return;
					}
				}
			}
		}
        private static bool IsBlack(Node node)
        {
            return node == null || node.IsBlack;
        }
        private static bool IsRed(Node node)
        {
            return node != null && node.IsRed;
        }
		
		///<summary>
		/// Clear
		/// Empties or clears the tree
		///<summary>
		public void Clear ()
		{
			root      = null;
		}
        public Node Find(IComparable key)
        {
            var node = root;
            while (node != null)
            {
                int cmp = key.CompareTo(node.Key);
                if (cmp == 0)
                {
                    return node;
                }
                if (cmp > 0)
                {
                    if (node.Right == null)
                    {
                        return null;
                    }
                    node = node.Right;
                }
                else
                {
                    if (node.Left == null)
                    {
                        return null;
                    }
                    node = node.Left;
                }
            }
            return null;
        }
        /// <summary>
        /// Return the node for greatest key in this tree less than or equal to key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Node Floor(IComparable key)
        {
            Node node = root;
            while (node != null)
            {
                int cmp = key.CompareTo(node.Key);
                if (cmp > 0)
                {
                    if (node.Right != null)
                    {
                        node = node.Right;
                    }
                    else
                    {
                        return node;
                    }
                } 
                else if (cmp < 0)
                {
                    if (node.Left != null)
                    {
                        node = node.Left;
                    }
                    else
                    {   
                        while (node.Parent != null && node == node.Parent.Left)
                        {
                            node = node.Parent;
                        }
                        return node;
                    }
                }
                else
                {
                    return node;
                }
            }
            return null;
        }
        public Node Lower(IComparable key)
        {
            Node node = root;
            while (node != null)
            {
                int cmp = key.CompareTo(node.Key);
                if (cmp > 0)
                {
                    if (node.Right != null)
                    {
                        node = node.Right;
                    }
                    else
                    {
                        return node;
                    }
                }
                else
                {
                    if (node.Left != null)
                    {
                        node = node.Left;
                    }
                    else
                    {
                        while (node.Parent != null && node == node.Parent.Left)
                        {
                            node = node.Parent;
                        }
                        return node.Parent;
                    }
                }
            }
            return null;
        }

	    public void CopyTo(Node[] array, int arrayIndex)
	    {
	        foreach (var node in this)
	        {
	            array[arrayIndex++] = node;
	        }
	    }

	    public int Count
	    {
            get { return root == null ? 0 : root.Weight; }
	    }

	    public IEnumerable<TKey> Keys
	    {
	        get {
	            return this.Select(node => node.Key);
	        }
	    }
	    public IEnumerable<TValue> Values
	    {
	        get {
	            return this.Select(node => node.Value);
	        }
	    }

        private Node MakeSubTree(int depth, IList<KeyValuePair<TKey, TValue>> entries, int start, int end)
        {
            if (end <= start)
            {
                return null;
            }
            int mid = (start + end)/2;
            Node root = new Node(this) { Key = entries[mid].Key, Value = entries[mid].Value, Weight = 1, IsBlack = true};
            if ((1 << depth) <= entries.Count && (1 << (depth + 1)) > entries.Count)
            {
                root.IsRed = true;
            }
            Node left = MakeSubTree(depth + 1, entries, start, mid);
            if (left != null)
            {
                root.Left = left;
                left.Parent = root;
            }
            Node right = MakeSubTree(depth + 1, entries, mid + 1, end);
            if (right != null)
            {
                root.Right = right;
                right.Parent = root;
            }
            return root;
        }
        /// <summary>
        /// Initializes a RedBlackTree from a list that is already sorted.
        /// The tree is balanced as best as possible, and only nodes in the lowest row are red.
        /// </summary>
        public static RedBlackTree<TKey,TValue> FromSorted(IList<KeyValuePair<TKey, TValue>> entries)
        {
            var tree = new RedBlackTree<TKey,TValue>();
            tree.root = tree.MakeSubTree(0, entries, 0, entries.Count);
            if (tree.root != null)
            {
                tree.root.IsBlack = true;
            }
            return tree;
        }
        public class RedBlackEnumerator : IEnumerator<Node>
        {
            public RedBlackEnumerator(RedBlackTree<TKey, TValue> tree)
            {
                Tree = tree;
                Forward = true;
            }

            public RedBlackTree<TKey, TValue> Tree
            {
                get;
                private set;
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public Node Current
            {
                get;
                set;
            }

            public bool Forward
            {
                get;
                set;
            }

            public void Reset()
            {
                Current = Start;
            }
            public Node Start
            {
                get;
                set;
            }
            public bool MoveNext()
            {
                if (Current == null)
                {
                    Current = Forward ? Tree.First : Tree.Last;
                }
                else
                {
                    Current = Forward ? Current.Next : Current.Previous;
                }
                return Current != Start;
            }
            public void Dispose()
            {
            }
        }
        public class Node
        {
            internal Node(Node other)
            {
                Key = other.Key;
                IsRed = other.IsRed;
                Value = other.Value;
                Weight = 1;
                if (other.Left != null)
                {
                    Left = new Node(other.Left);
                    Left.Parent = this;
                }
                if (other.Right != null)
                {
                    Right = new Node(other.Right);
                    Right.Parent = this;
                }
            }
            // left node 
            private Node rbnLeft;
            // right node 
            private Node rbnRight;

            public RedBlackTree<TKey, TValue> Tree { get; internal set; }

            ///<summary>
            ///Key
            ///</summary>
            public TKey Key
            {
                get;
                internal set;
            }
            ///<summary>
            ///Data
            ///</summary>
            public TValue Value { get; set; }

            ///<summary>
            ///Color
            ///</summary>
            internal bool IsRed
            {
                get;
                set;
            }
            internal bool IsBlack
            {
                get { return !IsRed; }
                set { IsRed = !value; }
            }
            public int Weight
            {
                get;
                internal set;
            }
            public int Index
            {
                get
                {
                    int index = Left == null ? 0 : Left.Weight;
                    for (var node = this; ; node = node.Parent)
                    {
                        if (node.Parent == null)
                        {
                            return index;
                        }
                        if (node == node.Parent.Right)
                        {
                            return index + node.Parent.Index + 1;
                        }
                    }
                }
            }
            private void UpdateWeight()
            {
                int newWeight = 1;
                if (Left != null)
                {
                    newWeight += Left.Weight;
                }
                if (Right != null)
                {
                    newWeight += Right.Weight;
                }
                if (newWeight == Weight)
                {
                    return;
                }
                Weight = newWeight;
                if (Parent != null)
                {
                    Parent.UpdateWeight();
                }
            }
            ///<summary>
            ///Left
            ///</summary>
            public Node Left
            {
                get
                {
                    return rbnLeft;
                }

                internal set
                {
                    rbnLeft = value;
                    UpdateWeight();
                }
            }
            ///<summary>
            /// Right
            ///</summary>
            public Node Right
            {
                get
                {
                    return rbnRight;
                }

                internal set
                {
                    rbnRight = value;
                    UpdateWeight();
                }
            }
            public Node Parent { get; set; }

            internal Node(RedBlackTree<TKey,TValue> tree)
            {
                Tree = tree;
                IsRed = true;
            }
            public Node Next
            {
                get
                {
                    Node node;
                    if (Right != null)
                    {
                        for (node = Right; node.Left != null; node = node.Left)
                        {

                        }
                        return node;
                    }
                    for (node = this; node.Parent != null; node = node.Parent)
                    {
                        if (node.Parent.Left == node)
                        {
                            return node.Parent;
                        }
                    }
                    return null;
                }
            }
            public Node Previous
            {
                get
                {
                    Node node;
                    if (Left != null)
                    {
                        for (node = Left; node.Right != null; node = node.Right)
                        {
                        }
                        return node;
                    }
                    for (node = this; node.Parent != null; node = node.Parent)
                    {
                        if (node == node.Parent.Right)
                        {
                            return node.Parent;
                        }
                    }
                    return null;
                }
            }
            ///1. The root is black. 
            ///2. All leaves are black. 
            ///3. Red nodes can only have black children (null's count as black)
            ///4. All paths from a node to its leaves contain the same number of black nodes.
            public int CheckValidAndCountBlackNodesToLeaves()
            {
                if (Tree == null)
                {
                    throw new InvalidDataException("Tree cannot be null");
                }
                if (IsRed)
                {
                    if (Parent == null)
                    {
                        throw new InvalidDataException("Root must be black");
                    }
                    if (Parent.IsRed)
                    {
                        throw new InvalidDataException("Red node cannot have red parent");
                    }
                }
                int weight = 1;
                int leftResult;
                if (Left != null)
                {
                    if (Left.Parent != this)
                    {
                        throw new InvalidDataException("Child has incorrect parent");
                    }
                    if (!ReferenceEquals(Tree, Left.Tree))
                    {
                        throw new InvalidDataException("Child is in wrong tree");
                    }
                    leftResult = Left.CheckValidAndCountBlackNodesToLeaves();
                    weight += Left.Weight;
                }
                else
                {
                    leftResult = 0;
                }

                int rightResult;
                if (Right != null)
                {
                    if (Right.Parent != this)
                    {
                        throw new InvalidDataException("Child has incorrect parent");
                    }
                    if (!ReferenceEquals(Tree, Right.Tree))
                    {
                        throw new InvalidDataException("Child is in wrong tree");
                    }
                    rightResult = Right.CheckValidAndCountBlackNodesToLeaves();
                    weight += Right.Weight;
                }
                else
                {
                    rightResult = 0;
                }

                if (leftResult != rightResult)
                {
                    throw new InvalidDataException("Left and right paths to leaves must contain same number of blacks");
                }
                if (weight != Weight)
                {
                    throw new InvalidDataException("Incorrect weight");
                }
                return IsRed ? leftResult : leftResult + 1;
            }
            public override string ToString()
            {
                return Value == null ? "" : Value.ToString();
            }
        }
    }


}
