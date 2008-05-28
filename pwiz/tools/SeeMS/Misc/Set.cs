using System;
using System.Collections;
using System.Collections.Generic;

namespace Extensions
{
	/// <summmary>
	/// Fairly decent sets class implemented using hashtables.
	/// </summmary>
	/// <remarks>
	/// Authors: Richard Bothne, Jim Showalter
	/// </remarks>
	/// <remarks>
	/// All sets used with an instance of this class have to use
	/// the same hashcode provider and comparer.
	/// </remarks>
	/// <remarks>
	/// The constructors duplicate the Hashtable constructors. For
	/// documentation, see the corresponding Hashtable documentation.
	/// </remarks>
	/// <exception>
	/// Throws no exceptions, and propagates untouched all exceptions thrown by callees.
	/// </exception>
	public class Set<T> : RBTree<T>, IComparable< Set<T> >, IEnumerable<T> where T: IComparable<T>
	{
		/// <summary>
		/// Refer to Hashtable constructor documentation.
		/// </summary>
		public Set()
			: base()
		{
		}

		public Set( IComparer<T> comparer )
			: base( comparer )
		{
		}

		/// <summary>
		/// Refer to Hashtable constructor documentation.
		/// </summary>
		public Set( Set<T> otherSet )
			: base( otherSet )
		{
			if( Keys.Count != Count )
				throw new InvalidOperationException( "Map stored count does not match node count" );
		}

		public Set( Set<T> otherSet, IComparer<T> comparer )
			: base( otherSet, comparer )
		{
			if( Keys.Count != Count )
				throw new InvalidOperationException( "Map stored count does not match node count" );
		}

		/// <summary>
		/// Refer to Hashtable constructor documentation.
		/// </summary>
		public Set( IList<T> otherSet )
			: base()
		{
			foreach( T item in otherSet )
				Add( item );
		}

		public IList<T> Keys
		{
			get
			{
				List<T> keyList = new List<T>();
				foreach( T itr in this )
					keyList.Add( itr );
				return keyList;
			}
		}

		/// <summary>
		/// Helper function that does most of the work in the class.
		/// </summary>
		private static Set<T> Generate(
			Set<T> iterSet,
			Set<T> containsSet,
			Set<T> startingSet,
			bool containment)
		{
			// Returned set either starts out empty or as copy of the starting set.
			Set<T> returnSet = startingSet == null ? new Set<T>() : startingSet;

			foreach( T key in iterSet )
			{
				// (!containment && !containSet.ContainsKey) ||
				//  (containment &&  containSet.ContainsKey)
				if( !( containment ^ containsSet.Contains( key ) ) )
				{
					returnSet.Add( key );
				}
			}

			return returnSet;
		}

		/// <summary>
		/// Union of set1 and set2.
		/// </summary>
		public static Set<T> operator |( Set<T> set1, Set<T> set2 )
		{
			// Copy set1, then add items from set2 not already in set 1.
			Set<T> unionSet = new Set<T>( set1 );
			return Generate(set2, unionSet, unionSet, false);
		}

		/// <summary>
		/// Union of this set and otherSet copied to a new set.
		/// </summary>
		public Set<T> UnionCopy( Set<T> otherSet )
		{
			return this | otherSet;
		}

		/// <summary>
		/// Union of this set and otherSet.
		/// </summary>
		public Set<T> Union( Set<T> otherSet )
		{
			return Generate( otherSet, this, this, false );
		}

		/// <summary>
		/// Intersection of set1 and set2.
		/// </summary>
		public static Set<T> operator &( Set<T> set1, Set<T> set2 ) 
		{
			// Find smaller of the two sets, iterate over it
			// to compare to other set.
			return Generate(
				set1.Count > set2.Count ? set2 : set1,
				set1.Count > set2.Count ? set1 : set2,
				null,
				true);
		}

		/// <summary>
		/// Intersection of this set and otherSet.
		/// </summary>
		public Set<T> Intersection( Set<T> otherSet ) 
		{
			return this & otherSet;
		}

		/// <summary>
		/// Exclusive-OR of set1 and set2.
		/// </summary>
		public static Set<T> operator ^( Set<T> set1, Set<T> set2 ) 
		{
			// Find items in set1 that aren't in set2. Then find
			// items in set2 that aren't in set1. Return combination
			// of those two subresults.
			return Generate(set2, set1, Generate(set1, set2, null, false), false);
		}

		/// <summary>
		/// Exclusive-OR of this set and otherSet.
		/// </summary>
		public Set<T> ExclusiveOr( Set<T> otherSet ) 
		{
			return this ^ otherSet;
		}

		public Set<T> Subtract( Set<T> set2 )
		{
			foreach( T item in set2 )
				Remove( item );
			return this;
		}

		/// <summary>
		/// The set1 minus set2. This is not associative.
		/// </summary>
		public static Set<T> operator -( Set<T> set1, Set<T> set2 ) 
		{
			Set<T> diffSet = new Set<T>();
			foreach( T item in set1 )
				if( !set2.Contains( item ) )
					diffSet.Add( item );
			return diffSet;
		}

		/// <summary>
		/// This set minus otherSet. This is not associative.
		/// </summary>
		public Set<T> Difference( Set<T> otherSet ) 
		{
			return this - otherSet;
		}

		/// <summary>
		/// Piecewise comparison of the set with another set.
		/// </summary>
		public int CompareTo( Set<T> other )
		{
			if( Count != other.Count )
				return Count.CompareTo( other.Count );

			int compare = 0;
			IEnumerator<T> lhsItr = GetEnumerator(); lhsItr.MoveNext();
			IEnumerator<T> rhsItr = other.GetEnumerator(); rhsItr.MoveNext();
			for( int i = 0; i < Count && compare == 0; ++i, lhsItr.MoveNext(), rhsItr.MoveNext() )
			{
				compare = lhsItr.Current.CompareTo( rhsItr.Current );
			}
			return compare;
		}
	}
}
