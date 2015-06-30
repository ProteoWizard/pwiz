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
    /// <summmary>
    /// STL-like set container (unique sorted sequence);
    /// supports common set operations as member functions
    /// </summmary>
    /// <remarks>
    /// Author: Matt Chambers
    /// </remarks>
    /// <exception>
    /// ?
    /// </exception>
    public class Set<T> : RBTree<T>, IComparable< Set<T> >, IEnumerable<T>
    {
        /// <summary>
        /// Constructs an empty set using IComparable&lt;T&gt;
        /// </summary>
        public Set()
            : base()
        {
        }

        /// <summary>
        /// Constructs an empty set using the specified comparer
        /// </summary>
        public Set( IComparer<T> comparer )
            : base( comparer )
        {
        }

        /// <summary>
        /// Constructs an empty set using the specified comparison
        /// </summary>
        public Set (Comparison<T> comparison)
            : base(comparison)
        {
        }

        /// <summary>
        /// Copies the other set using IComparable&lt;T&gt;
        /// </summary>
        public Set( Set<T> otherSet )
            : base( otherSet )
        {
            if( Keys.Count != Count )
                throw new InvalidOperationException( "Map stored count does not match node count" );
        }

        /// <summary>
        /// Copies the other set using the specified comparer
        /// </summary>
        public Set( Set<T> otherSet, IComparer<T> comparer )
            : base( otherSet, comparer )
        {
            if( Keys.Count != Count )
                throw new InvalidOperationException( "Map stored count does not match node count" );
        }

        /// <summary>
        /// Copies the other collection using 
        /// </summary>
        public Set( IEnumerable<T> otherCollection )
            : base()
        {
            foreach( T item in otherCollection )
                Add( item );
        }

        /// <summary>
        /// Retrieves the list of elements as a flat list
        /// </summary>
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
        /// Generates a new set that is the union of set1 and set2
        /// </summary>
        public static Set<T> operator |( Set<T> set1, Set<T> set2 )
        {
            // Copy set1, then add items from set2 not already in set 1.
            Set<T> unionSet = new Set<T>( set1 );
            return Generate(set2, unionSet, unionSet, false);
        }

        /// <summary>
        /// Generates a new set that is the union of set1 and set2
        /// </summary>
        public Set<T> UnionCopy( Set<T> otherSet )
        {
            return this | otherSet;
        }

        /// <summary>
        /// Inserts the other set's elements into this set
        /// </summary>
        public Set<T> Union( Set<T> otherSet )
        {
            return Generate( otherSet, this, this, false );
        }

        /// <summary>
        /// Generates a new set that is the intersection of set1 and set2
        /// </summary>
        public static Set<T> operator &( Set<T> set1, Set<T> set2 ) 
        {
            // Find smaller of the two sets, iterate over it to compare to other set.
            return Generate(
                set1.Count > set2.Count ? set2 : set1,
                set1.Count > set2.Count ? set1 : set2,
                null,
                true);
        }

        /// <summary>
        /// Generates a new set that is the intersection of this and the other set
        /// </summary>
        public Set<T> Intersection( Set<T> otherSet ) 
        {
            return this & otherSet;
        }

        /// <summary>
        /// Generates a new set that contains elements in set1 or set2, but not in both (XOR)
        /// </summary>
        public static Set<T> operator ^( Set<T> set1, Set<T> set2 ) 
        {
            // Find items in set1 that aren't in set2. Then find
            // items in set2 that aren't in set1. Return combination
            // of those two subresults.
            return Generate(set2, set1, Generate(set1, set2, null, false), false);
        }

        /// <summary>
        /// Generates a new set that contains elements in set1 or set2, but not in both (XOR)
        /// </summary>
        public Set<T> ExclusiveOr( Set<T> otherSet ) 
        {
            return this ^ otherSet;
        }

        /// <summary>
        /// Subtracts elements from the other set from this set
        /// </summary>
        public Set<T> Subtract( Set<T> otherSet )
        {
            foreach( T item in otherSet )
                Remove( item );
            return this;
        }

        /// <summary>
        /// Generates a new set that is the difference of set1 and set2
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
        /// Generates a new set that is the difference of set1 and set2
        /// </summary>
        public Set<T> Difference( Set<T> otherSet ) 
        {
            return this - otherSet;
        }

        /// <summary>
        /// Pairwise comparison of the set with another set
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
                compare = Comparer.Compare(lhsItr.Current, rhsItr.Current);
            }
            return compare;
        }
    }
}
