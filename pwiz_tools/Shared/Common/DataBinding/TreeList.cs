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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// A wrapper around a RedBlackTree that implements IList.
    /// Nodes in the RedBlackTree are keyed with <see cref="LongDecimal"/>
    /// </summary>
    public class TreeList<T> : IList<T>
    {   
        public TreeList(RedBlackTree<LongDecimal,T> tree)
        {
            Tree = tree;
        }
        public TreeList(IEnumerable<T> items)
        {
            Tree = RedBlackTree<LongDecimal,T>.FromSorted(
                items.Select((item, index) => new KeyValuePair<LongDecimal, T>(new LongDecimal(index), item))
                .ToArray());
        }

        public IEnumerator GetEnumerator()
        {
            return Tree.Select(node => node.Value).GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return Tree.Select(node => node.Value).Cast<T>().GetEnumerator();
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            foreach (T item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index < 0)
            {
                return false;
            }
            RemoveAt(index);
            return true;
        }

        public void Add(T item)
        {
            Insert(Count, item);
        }

        public void Clear()
        {
            Tree.Clear();
        }

        public bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(Array array, int arrayIndex)
        {
            foreach (var item in this)
            {
                array.SetValue(item, arrayIndex);
                arrayIndex++;
            }
        }

        public int Count
        {
            get { return Tree.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public int IndexOf(T item)
        {
            foreach (var node in Tree)
            {
                if (Equals(item, node.Value))
                {
                    return node.Index;
                }
            }
            return -1;
        }

        public virtual void Insert(int index, T item)
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
                    newRowId = ((LongDecimal)Tree[0].Key).GetPredecessor();
                }
            }
            else if (index == Tree.Count)
            {
                newRowId = ((LongDecimal)Tree[Tree.Count - 1].Key).GetSuccessor();
            }
            else
            {
                newRowId =
                    ((LongDecimal)Tree[index - 1].Key).MidPoint(
                        (LongDecimal)Tree[index].Key);
            }
            Tree.Add(newRowId, item);
        }

        public void RemoveAt(int index)
        {
            Tree.RemoveAt(index);
        }

        public T this[int index]
        {
            get { return Tree[index].Value; }
            set { Tree[index].Value = value; }
        }
        public RedBlackTree<LongDecimal,T> Tree { get; set; }
    }
}
