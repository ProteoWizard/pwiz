//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is "A Better MRU List - MruDictionary"
// Implemented according to http://www.informit.com/guides/content.aspx?g=dotnet&seqNum=626
//
// The Initial Developer of the Original Code is Jim Mischel.
//
// Copyright 2007 Jim Mischel
// Copyright 2011 Vanderbilt University
//
// Contributor(s): Matt Chambers
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace IDPicker
{
    /// <summary>
    /// A most recently used dictionary. It can efficiently check for an item's existence and drop the
    /// least recently used element when a new element is added after MaximumCapacity is reached.
    /// </summary>
    public class MruDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        /// <summary>
        /// Constructs an MruDictionary with the specified maximum capacity.
        /// When Count == MaximumCapacity, adding new elements removes the least recently used element.
        /// </summary>
        public MruDictionary (int maxCapacity)
        {
            MaximumCapacity = maxCapacity;
            items = new LinkedList<KeyValuePair<TKey, TValue>>();
            itemIndex = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(maxCapacity);
        }

        /// <summary>
        /// Gets the maximum capacity for the dictionary.
        /// When Count == MaximumCapacity, adding new elements removes the least recently used element.
        /// </summary>
        public int MaximumCapacity { get; private set; }

        /// <summary>
        /// Sets the maximum capacity for the dictionary.
        /// Least recently used elements are removed until Count &lt;= MaximumCapacity.
        /// </summary>
        /// <param name="maxCapacity"></param>
        public void SetMaximumCapacity (int maxCapacity)
        {
            MaximumCapacity = maxCapacity;
            trim();
        }

        /// <summary>
        /// Adds the specified key and value to the dictionary.
        /// If Count == MaximumCapacity, adding new elements removes the least recently used element.
        /// </summary>
        public void Add (TKey key, TValue value) { Add(new KeyValuePair<TKey, TValue>(key, value)); }

        /// <summary>
        /// Adds the specified key and value to the dictionary.
        /// If Count == MaximumCapacity, adding new elements removes the least recently used element.
        /// </summary>
        public void Add (KeyValuePair<TKey, TValue> item)
        {
            if (itemIndex.ContainsKey(item.Key))
                throw new ArgumentException("An item with the same key already exists.");

            var newNode = new LinkedListNode<KeyValuePair<TKey, TValue>>(item);
            items.AddFirst(newNode);
            itemIndex.Add(item.Key, newNode);

            trim();
        }

        /// <summary>
        /// Attempts to find the element with the specified key in the dictionary.
        /// If found, value will be set to the key's associated value.
        /// </summary>
        /// <returns>True iff an element with the specified key was found.</returns>
        public bool TryGetValue (TKey key, out TValue value)
        {
            LinkedListNode<KeyValuePair<TKey, TValue>> node;
            if (itemIndex.TryGetValue(key, out node))
            {
                value = node.Value.Value;
                // move this node to the front of the list
                items.Remove(node);
                items.AddFirst(node);
                return true;
            }
            value = default(TValue);
            return false;
        }

        /// <summary>Removes the element with the specified key from the dictionary.</summary>
        /// <returns>True iff an element with the specified key was removed.</returns>
        public bool Remove (KeyValuePair<TKey, TValue> item) { return Remove(item.Key); }

        /// <summary>Removes the element with the specified key from the dictionary.</summary>
        /// <returns>True iff an element with the specified key was removed.</returns>
        public bool Remove (TKey key)
        {
            if (itemIndex.ContainsKey(key))
            {
                items.Remove(itemIndex[key].Value);
                itemIndex.Remove(key);
                return true;
            }
            return false;
        }

        /// <summary>Clears the dictionary of all elements.</summary>
        public void Clear ()
        {
            items.Clear();
            itemIndex.Clear();
        }

        /// <summary>Returns true iff the dictionary contains an element with the specified key.</summary>
        public bool ContainsKey (TKey key) { return itemIndex.ContainsKey(key); }

        /// <summary>Returns the keys currently in the dictionary.</summary>
        public ICollection<TKey> Keys { get { return itemIndex.Keys; } }

        /// <summary>Returns the values currently in the dictionary.</summary>
        public ICollection<TValue> Values { get { return itemIndex.Values.Select(o => o.Value.Value).ToArray(); } }

        /// <summary>Gets or sets the value associated with the specified key.</summary>
        public TValue this[TKey key] { get { return itemIndex[key].Value.Value; } set { Add(key, value); } }

        /// <summary>Returns true iff the dictionary contains an element with the specified key.</summary>
        public bool Contains (KeyValuePair<TKey, TValue> item) { return itemIndex.ContainsKey(item.Key); }

        /// <summary>Not implemented.</summary>
        /// <exception cref="NotImplementedException"/>
        public void CopyTo (KeyValuePair<TKey, TValue>[] array, int arrayIndex) { throw new NotImplementedException(); }

        /// <summary>Gets the number of elements currently in the dictionary.</summary>
        public int Count { get { return items.Count; } }

        /// <summary>Returns false.</summary>
        public bool IsReadOnly { get { return false; } }

        /// <summary>Gets an enumerator for the key-value-pairs in the dictionary.</summary>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator () { return items.GetEnumerator(); }

        /// <summary>Gets an enumerator for the key-value-pairs in the dictionary.</summary>
        IEnumerator IEnumerable.GetEnumerator () { return items.GetEnumerator(); }

        void trim ()
        {
            while (itemIndex.Count > MaximumCapacity)
            {
                LinkedListNode<KeyValuePair<TKey, TValue>> node = items.Last;
                items.RemoveLast();
                itemIndex.Remove(node.Value.Key);
            }
        }

        // The linked list of items in MRU order
        private LinkedList<KeyValuePair<TKey, TValue>> items;

        // The dictionary of keys and nodes
        private Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> itemIndex;
    }

}