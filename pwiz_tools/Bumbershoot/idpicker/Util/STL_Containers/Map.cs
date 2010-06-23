//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Collections.Generic;
using System.Text;

namespace System.Collections.Generic
{
	public class MutableKeyValuePair<TKey, TValue> : IComparable<MutableKeyValuePair<TKey, TValue>>
		where TKey : IComparable<TKey>
		where TValue : new()
	{
		private TKey m_key;
		private TValue m_value;

		public MutableKeyValuePair( TKey key, TValue value )
		{
			m_key = key;
			m_value = value;
		}

		public TKey Key
		{
			get
			{
				return m_key;
			}
		}

		public TValue Value
		{
			get
			{
				return m_value;
			}

			set
			{
				m_value = value;
			}
		}

		public int CompareTo( MutableKeyValuePair<TKey, TValue> rhs )
		{
			return m_key.CompareTo( rhs.m_key );
		}
	}

	public class Map<TKey, TValue> : RBTree<MutableKeyValuePair<TKey, TValue>>, IComparable<Map<TKey, TValue>>
		where TKey : IComparable<TKey>
		where TValue : new()
	{
		public class MapPair : MutableKeyValuePair<TKey, TValue>
		{
			public MapPair( TKey key, TValue value ) : base( key, value ) { }
		}

		public Map()
			: base()
		{
		}

		public Map( IComparer<TKey> comparer )
			: base( new MapComparer(comparer) )
		{ }

		public Map( Map<TKey, TValue> otherMap )
			: base( otherMap )
		{
			if( Keys.Count != Count ) throw new InvalidOperationException( "Map stored count does not match node count" );
		}

		public Map( Map<TKey, TValue> otherMap, IComparer<TKey> comparer )
			: base( otherMap, new MapComparer( comparer ) )
		{
			if( Keys.Count != Count ) throw new InvalidOperationException( "Map stored count does not match node count" );
		}

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

		public InsertResult Insert( TKey key, TValue value )
		{
			return Insert( new MapPair( key, value ) );
		}

		public void Add( TKey key, TValue value )
		{
			Insert( key, value );
		}

		public IList<TKey> Keys
		{
			get
			{
				List<TKey> keyList = new List<TKey>();
				foreach( MapPair itr in this )
					keyList.Add( itr.Key );
				return keyList;
			}
		}

		public IList<TValue> Values
		{
			get
			{
				List<TValue> valueList = new List<TValue>();
				foreach( MapPair itr in this )
					valueList.Add( itr.Value );
				return valueList;
			}
		}

		public bool Contains( TKey key )
		{
			return Contains( new MapPair( key, new TValue() ) );
		}

		public bool Remove( TKey key )
		{
			return base.Remove( new MapPair( key, new TValue() ) );
		}

		public Enumerator Find( TKey key )
		{
			return base.Find( new MapPair( key, new TValue() ) );
		}

		public Enumerator LowerBound( TKey key )
		{
			return base.LowerBound( new MapPair( key, new TValue() ) );
		}

		public Enumerator UpperBound( TKey key )
		{
			return base.UpperBound( new MapPair( key, new TValue() ) );
		}

		public int CompareTo( Map<TKey, TValue> other )
		{
			if( Count != other.Count )
				return Count.CompareTo( other.Count );

			int compare = 0;
			Enumerator lhsItr = GetEnumerator(); lhsItr.MoveNext();
			Enumerator rhsItr = other.GetEnumerator(); rhsItr.MoveNext();
			for( int i = 0; i < Count && compare == 0; ++i, lhsItr.MoveNext(), rhsItr.MoveNext() )
			{
				compare = lhsItr.Current.Key.CompareTo( rhsItr.Current.Key );
			}
			return compare;
		}

		public class MapComparer : IComparer<MutableKeyValuePair<TKey, TValue>>
		{
			IComparer<TKey> comparer;
			public MapComparer( IComparer<TKey> comparer )
			{
				this.comparer = comparer;
			}

			public int Compare( MutableKeyValuePair<TKey, TValue> x, MutableKeyValuePair<TKey, TValue> y )
			{
				return comparer.Compare( x.Key, y.Key );
			}
		}
	}

    public class ImmutableMap<TKey, TValue> : IComparable<ImmutableMap<TKey, TValue>>
        where TKey : IComparable<TKey>
        where TValue : new()
    {
        protected Map<TKey, TValue> base_;

        public ImmutableMap (Map<TKey, TValue> baseMap)
        {
            base_ = baseMap;
        }

        public TValue this[TKey key]
        {
            get
            {
                if (!base_.Contains(key))
                    throw new KeyNotFoundException();
                return base_[key];
            }
        }

        public IList<TKey> Keys { get { return base_.Keys; } }
        public IList<TValue> Values { get { return base_.Values; } }
        public bool Contains (TKey key) { return base_.Contains(key); }
        public bool Remove (TKey key) { return base_.Remove(key); }
        public Map<TKey, TValue>.Enumerator Find (TKey key) { return base_.Find(key); }
        public Map<TKey, TValue>.Enumerator LowerBound (TKey key) { return base_.LowerBound(key); }
        public Map<TKey, TValue>.Enumerator UpperBound (TKey key) { return base_.UpperBound(key); }
        public int CompareTo (ImmutableMap<TKey, TValue> other) { return base_.CompareTo(other.base_); }
    }
}
