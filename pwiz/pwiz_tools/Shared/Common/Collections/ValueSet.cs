/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Immutable sorted set of elements.
    /// </summary>
    public abstract class ValueSet<TSet, TElement> : IEnumerable<TElement> where TSet : ValueSet<TSet, TElement>, new()
    {
        private TElement[] _array;
        protected ValueSet()
        {
            _array = new TElement[0];
        }

        public static readonly TSet EMPTY = new TSet();
        /// <summary>
        /// Parses a string containing a list of elements.  The string is expected to have been
        /// returned from <see cref="ToString"/>, or be of the same format.
        /// </summary>
        public static TSet Parse(string stringValue)
        {
            var result = new TSet();
            result._array = MakeElementArray(result, result.ParseElements(stringValue));
            return result;
        }
        /// <summary>
        /// Returns a ValueSet that contains the specified element.
        /// </summary>
        public static TSet Singleton(TElement value)
        {
            return new TSet {_array = new[]{value}};
        }
        /// <summary>
        /// Returns a ValueSet containing the specified elements.
        /// </summary>
        public static TSet OfValues(params TElement[] values)
        {
            return OfValues((IEnumerable<TElement>) values);
        }
        /// <summary>
        /// Returns a ValueSet containing the specified elements.
        /// </summary>
        public static TSet OfValues(IEnumerable<TElement> values)
        {
            var result = new TSet();
            result._array = MakeElementArray(result, values);
            return result;
        }

        #region Equality Members
        public override int GetHashCode()
        {
            int result = 0;
            var equalityComparer = ElementEqualityComparer;
            foreach (var element in this)
            {
                result = result*397 + equalityComparer.GetHashCode(element);
            }
            return result;
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (null == obj)
            {
                return false;
            }
            if (GetType() != obj.GetType())
            {
                return false;
            }
            return _array.SequenceEqual(((ValueSet<TSet, TElement>) obj)._array, ElementEqualityComparer);
        }
        #endregion
        /// <summary>
        /// Returns a string representation of this set.
        /// </summary>
        public override string ToString()
        {
            return string.Join(ElementSeparator, _array.Select(ElementToString));
        }
        #region IEnumerable methods
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public IEnumerator<TElement> GetEnumerator()
        {
            return _array.Cast<TElement>().GetEnumerator();
        }
        #endregion

        public int Count { get { return _array.Length; } }
        public bool IsEmpty { get { return 0 == Count; } }
        /// <summary>
        /// Returns true if this set contains the specified element.
        /// </summary>
        public bool Contains(TElement element)
        {
            return Array.BinarySearch(_array, element, ElementComparer) >= 0;
        }
        /// <summary>
        /// Returns a set which is the intersection of this set with the specified elements.
        /// </summary>
        public TSet Intersect(IEnumerable<TElement> elements)
        {
            return OfValues(elements.Where(Contains));
        }
        /// <summary>
        /// Returns a set which is the union of this set with the specified elements.
        /// </summary>
        public TSet Union(IEnumerable<TElement> elements)
        {
            return OfValues(_array.Concat(elements));
        }
        /// <summary>
        /// Returns a set which is the union of this set with the specified elements.
        /// </summary>
        public TSet Union(params TElement[] elements)
        {
            return OfValues(_array.Concat(elements));
        }
        /// <summary>
        /// Returns a set which is this with the specified elements removed
        /// </summary>
        public TSet Except(IEnumerable<TElement> elements)
        {
            return OfValues(_array.Except(elements));
        }
        /// <summary>
        /// Returns a set which is this with the specified elements removed
        /// </summary>
        public TSet Except(params TElement[] elements)
        {
            return OfValues(_array.Except(elements));
        }
        /// <summary>
        /// Returns true if this set contains only the specified element.
        /// </summary>
        public bool IsSingleton(TElement element)
        {
            return 1 == Count && ElementEqualityComparer.Equals(_array[0], element);
        }

        #region Virtual methods that affect comparing and converting of elements
        /// <summary>
        /// Returns the IEqualityComparer to be used for determining whether two elements
        /// in this set are equal.
        /// </summary>
        protected virtual IEqualityComparer<TElement> ElementEqualityComparer
        {
            get { return EqualityComparer<TElement>.Default; }
        }
        /// <summary>
        /// Returns the IComparer to be used for sorting elements in the set.
        /// Returning null is the most efficient, since Array.Sort performs best
        /// if it knows that it is using the default comparer.
        /// </summary>
        protected virtual IComparer<TElement> ElementComparer { get { return null; } }
        protected virtual string ElementToString(TElement element)
        {
            return element.ToString();
        }
        protected virtual TElement ParseElement(string stringValue)
        {
            if (typeof(TElement).IsEnum)
            {
                return (TElement) Enum.Parse(typeof (TElement), stringValue);
            }
            return (TElement) Convert.ChangeType(stringValue, typeof (TElement));
        }
        protected virtual string ElementSeparator { get { return ", "; } } // Not L10N?
        protected virtual IEnumerable<TElement> ParseElements(string stringValue)
        {
            if (string.IsNullOrEmpty(stringValue))
            {
                return new TElement[0];
            }
            return stringValue.Split(new[] {ElementSeparator}, StringSplitOptions.None).Select(ParseElement);
        }
        #endregion

        private static TElement[] MakeElementArray(TSet set, IEnumerable<TElement> values)
        {
            if (null != values)
            {
                var hashSet = new HashSet<TElement>(values, set.ElementEqualityComparer);
                if (hashSet.Count > 0)
                {
                    var array = hashSet.ToArray();
                    Array.Sort(array, set.ElementComparer);
                    return array;
                }
            }
            return new TElement[0];
        }
    }
}
