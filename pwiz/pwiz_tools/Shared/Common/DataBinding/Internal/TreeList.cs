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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Internal
{
    /// <summary>
    /// A wrapper around a RedBlackTree that implements IList.
    /// Nodes in the RedBlackTree are keyed with <see cref="LongDecimal"/>
    /// </summary>
    internal class TreeList<TItem> : IList<TItem> where TItem : class
    {
        public TreeList(RedBlackTree<LongDecimal,TItem> tree)
        {
            Tree = tree;
        }
        public TreeList() : this(new RedBlackTree<LongDecimal, TItem>())
        {
        }
        public TreeList(IEnumerable<TItem> items) : this()
        {
            Reset(items);
        }

        public void Add(TItem item)
        {
            Insert(Count, item);
        }

        public int Count { get { return Tree.Count; } }

        public virtual void Insert(int index, TItem item)
        {
            LongDecimal newRowId;
            if (index == 0)
            {
                if (Tree.Count == 0)
                {
                    newRowId = new LongDecimal(1);
                }
                else
                {
                    newRowId = Tree[0].Key.GetPredecessor();
                }
            }
            else if (index == Tree.Count)
            {
                newRowId = Tree[Tree.Count - 1].Key.GetSuccessor();
            }
            else
            {
                newRowId =
                    Tree[index - 1].Key.MidPoint(Tree[index].Key);
            }
            Tree.Add(newRowId, item);
        }

        public RedBlackTree<LongDecimal, TItem> Tree { get; protected set; }

        public virtual void Reset(IEnumerable<TItem> values)
        {
            Tree.FillFromSorted(values.Select((item,index)
                =>new KeyValuePair<LongDecimal,TItem>(new LongDecimal(index), item)).ToArray());
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            return Tree.Values.GetEnumerator();
        }

        public virtual void Clear()
        {
            Tree.Clear();
        }

        public bool Contains(TItem item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(TItem[] array, int arrayIndex)
        {
            foreach (var item in this)
            {
                array.SetValue(item, arrayIndex++);
            }
        }

        public bool Remove(TItem item)
        {
            int index = IndexOf(item);
            if (index < 0)
            {
                return false;
            }
            RemoveAt(index);
            return true;
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public virtual int IndexOf(TItem value)
        {
            int index = 0;
            foreach (var item in this)
            {
                if (Equals(item, value))
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        public virtual void RemoveAt(int index)
        {
            Tree.RemoveAt(index);
        }

        public virtual TItem this[int index]
        {
            get
            {
                return Tree[index].Value;
            }
            set
            {
                Tree[index].Value = value;
            }
        }
    }
}
