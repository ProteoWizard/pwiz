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
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

namespace System.Collections.Generic
{
    /// <summary>
    /// A KeyValuePair class with both key and value being mutable.
    /// </summary>
    public class MutableKeyValuePair<TKey, TValue>
        where TValue : new()
    {
        /// <summary>
        /// Construct a mutable KeyValuePair with the given key and value.
        /// </summary>
        public MutableKeyValuePair( TKey key, TValue value )
        {
            Key = key;
            Value = value;
        }

        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        public TKey Key { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public TValue Value { get; set; }
    }

    /// <summary>
    /// An unique associative ordered table, like SortedDictionary but the index operator will insert and return a default-constructed element when a non-existent key is requested.
    /// </summary>
    public class Map<TKey, TValue> : RBTree<MutableKeyValuePair<TKey, TValue>>, IComparable<Map<TKey, TValue>>
        where TValue : new()
    {
        /// <summary>
        /// A typedef for MutableKeyValuePair.
        /// </summary>
        public class MapPair : MutableKeyValuePair<TKey, TValue>
        {
            /// <summary>
            /// Constructs a MapPair from the given key and value.
            /// </summary>
            public MapPair (TKey key, TValue value) : base(key, value) { }
        }

        /// <summary>
        /// Constructs an empty map using the default key comparer.
        /// </summary>
        public Map()
            : base(new MapComparer(Comparer<TKey>.Default))
        {
            KeyComparer = Comparer<TKey>.Default;
        }

        /// <summary>
        /// Returns the key comparer used by the map.
        /// </summary>
        public IComparer<TKey> KeyComparer { get; protected set; }

        /// <summary>
        /// Constructs an empty map using the given key comparer.
        /// </summary>
        public Map( IComparer<TKey> comparer )
            : base( new MapComparer(comparer) )
        {
            KeyComparer = comparer;
        }

        /// <summary>
        /// Constructs a map by copying another map of the same type and key comparer.
        /// </summary>
        public Map( Map<TKey, TValue> otherMap )
            : base( otherMap )
        {
            if( Keys.Count != Count ) throw new InvalidOperationException( "Map stored count does not match node count" );
        }

        /// <summary>
        /// Constructs a map by copying another map of the same type but using a different key comparer.
        /// </summary>
        public Map( Map<TKey, TValue> otherMap, IComparer<TKey> comparer )
            : base( otherMap, new MapComparer( comparer ) )
        {
            if( Keys.Count != Count ) throw new InvalidOperationException( "Map stored count does not match node count" );
        }

        /// <summary>
        /// If the key exists in the map, returns the element corresponding to that key. If the element doesn't exist, a default-constructed element will be inserted and returned instead.
        /// </summary>
        public TValue this[TKey key]
        {
            get
            {
                return Insert(key, new TValue()).Element.Value;
            }

            set
            {
                Insert(key, value).Element.Value = value;
            }
        }

        /// <summary>
        /// Inserts an element into the map or returns an existing element for the current key.
        /// </summary>
        /// <returns>A pair whose first element is a reference to either the inserted key-value pair or the existing one, and the second element is a boolean storing whether the insert actually happened.</returns>
        public virtual InsertResult Insert( TKey key, TValue value )
        {
            return Insert(new MapPair(key, value));
        }

        /// <summary>
        /// Adds a key-value pair to the map or does nothing if the key already exists.
        /// </summary>
        public virtual void Add( TKey key, TValue value )
        {
            Insert( key, value );
        }

        /// <summary>
        /// Returns the current sorted list of keys in the map.
        /// </summary>
        public virtual IList<TKey> Keys
        {
            get
            {
                var keyList = new List<TKey>();
                foreach( MapPair itr in this )
                    keyList.Add( itr.Key );
                return keyList;
            }
        }

        /// <summary>
        /// Returns the current list of values in the map.
        /// </summary>
        public virtual IList<TValue> Values
        {
            get
            {
                var valueList = new List<TValue>();
                foreach( var itr in this )
                    valueList.Add( itr.Value );
                return valueList;
            }
        }

        /// <summary>
        /// Returns true iff the map contains the given key.
        /// </summary>
        public virtual bool Contains( TKey key )
        {
            return Contains( new MapPair( key, new TValue() ) );
        }

        /// <summary>
        /// Attempts to the remove the given key from the map.
        /// </summary>
        /// <returns>True if the key existed and was removed, or false if the key is not in the map.</returns>
        public virtual bool Remove( TKey key )
        {
            return base.Remove( new MapPair( key, new TValue() ) );
        }

        /// <summary>
        /// Returns an enumerator for the given key if it is in the map, or an invalid enumerator if it's not.
        /// </summary>
        public virtual Enumerator Find( TKey key )
        {
            return base.Find( new MapPair( key, new TValue() ) );
        }

        /// <summary>
        /// Returns an enumerator to the first element with a key greater than or equal to the given key (i.e. the first position where the key could be inserted without violating ordering).
        /// </summary>
        public virtual Enumerator LowerBound( TKey key )
        {
            return base.LowerBound( new MapPair( key, new TValue() ) );
        }

        /// <summary>
        /// Returns an enumerator to the first element with a key greater than the given key, or an invalid enumerator if the key is greater than all other keys in the map (i.e. the last position where the key could be inserted without violating ordering).
        /// </summary>
        public virtual Enumerator UpperBound( TKey key )
        {
            return base.UpperBound( new MapPair( key, new TValue() ) );
        }

        /// <summary>
        /// Compares the map to another map of the same type, first by the number of elements and then by the actual elements themselves (if all elements are equal, returns 0).
        /// </summary>
        public virtual int CompareTo( Map<TKey, TValue> other )
        {
            if( Count != other.Count )
                return Count.CompareTo( other.Count );

            int compare = 0;
            Enumerator lhsItr = GetEnumerator(); lhsItr.MoveNext();
            Enumerator rhsItr = other.GetEnumerator(); rhsItr.MoveNext();
            for( int i = 0; i < Count && compare == 0; ++i, lhsItr.MoveNext(), rhsItr.MoveNext() )
            {
                compare = Comparer.Compare(lhsItr.Current, rhsItr.Current);
            }
            return compare;
        }

        /// <summary>
        /// A simple implementation of the IComparer interface for this map type.
        /// </summary>
        public class MapComparer : IComparer<MutableKeyValuePair<TKey, TValue>>
        {
            readonly IComparer<TKey> _comparer;

            /// <summary>
            /// Constructs a MapComparer from a comparer object based only on the key.
            /// </summary>
            public MapComparer( IComparer<TKey> comparer )
            {
                this._comparer = comparer;
            }

            /// <summary>
            /// Compares two elements of the map.
            /// </summary>
            public int Compare( MutableKeyValuePair<TKey, TValue> x, MutableKeyValuePair<TKey, TValue> y )
            {
                return _comparer.Compare( x.Key, y.Key );
            }
        }
    }

    /// <summary>
    /// A subclass of Map that provides the same interface but with an immutable indexing operator (i.e. someMap[someNewKey] will throw a KeyNotFoundException)
    /// </summary>
    public class ImmutableMap<TKey, TValue> : IComparable<ImmutableMap<TKey, TValue>>
        where TKey : IComparable<TKey>
        where TValue : new()
    {
        /// <summary>
        /// The mutable map being wrapped.
        /// </summary>
        protected Map<TKey, TValue> base_;

        /// <summary>
        /// Constructs an ImmutableMap wrapper of a mutable Map.
        /// </summary>
        public ImmutableMap (Map<TKey, TValue> baseMap)
        {
            base_ = baseMap;
        }

        /// <summary>
        /// If the key exists in the map, returns the value associated with it. Otherwise throws <exception cref="KeyNotFoundException"></exception>.
        /// </summary>
        public TValue this[TKey key]
        {
            get
            {
                if (!base_.Contains(key))
                    throw new KeyNotFoundException();
                return base_[key];
            }
        }

        /// <summary>
        /// Returns the current sorted list of keys in the map.
        /// </summary>
        public IList<TKey> Keys { get { return base_.Keys; } }

        /// <summary>
        /// Returns the current list of values in the map.
        /// </summary>
        public IList<TValue> Values { get { return base_.Values; } }

        /// <summary>
        /// Returns true iff the map contains the given key.
        /// </summary>
        public bool Contains(TKey key) { return base_.Contains(key); }

        /// <summary>
        /// Attempts to the remove the given key from the map.
        /// </summary>
        /// <returns>True if the key existed and was removed, or false if the key is not in the map.</returns>
        public bool Remove(TKey key) { return base_.Remove(key); }

        /// <summary>
        /// Returns an enumerator to the first element with a key greater than or equal to the given key (i.e. the first position where the key could be inserted without violating ordering).
        /// </summary>
        public RBTree<MutableKeyValuePair<TKey, TValue>>.Enumerator Find(TKey key) { return base_.Find(key); }

        /// <summary>
        /// Returns an enumerator to the first element with a key greater than or equal to the given key (i.e. the first position where the key could be inserted without violating ordering).
        /// </summary>
        public RBTree<MutableKeyValuePair<TKey, TValue>>.Enumerator LowerBound(TKey key) { return base_.LowerBound(key); }

        /// <summary>
        /// Returns an enumerator to the first element with a key greater than the given key, or an invalid enumerator if the key is greater than all other keys in the map (i.e. the last position where the key could be inserted without violating ordering).
        /// </summary>
        public RBTree<MutableKeyValuePair<TKey, TValue>>.Enumerator UpperBound (TKey key) { return base_.UpperBound(key); }

        /// <summary>
        /// Compares the map to another map of the same type, first by the number of elements and then by the actual elements themselves (if all elements are equal, returns 0).
        /// </summary>
        public int CompareTo (ImmutableMap<TKey, TValue> other) { return base_.CompareTo(other.base_); }
    }
}
