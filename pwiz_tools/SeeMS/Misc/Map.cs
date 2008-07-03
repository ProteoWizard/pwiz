using System;
using System.Collections.Generic;
using System.Text;

namespace Extensions
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
}
