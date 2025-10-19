/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.Collections
{
    public static class ImmutableListFactory
    {
        /// <summary>
        /// Returns an ImmutableList with the items in the enumerable.
        /// If the enumerable is already an instance of <see cref="ImmutableList{T}"/>
        /// then returns that object.
        /// If the type of the enumerable is <see cref="int"/> then calls
        /// <see cref="IntegerList.FromIntegers"/> to potentially store the
        /// values as bytes or shorts.
        /// </summary>
        public static ImmutableList<T> ToImmutable<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable is ImmutableList<T> immutableList)
            {
                return immutableList;
            }

            if (typeof(T) == typeof(int))
            {
                return (ImmutableList<T>) (object) IntegerList.FromIntegers((IEnumerable<int>)enumerable);
            }
            return ImmutableList.ValueOf(enumerable);
        }

        /// <summary>
        /// Returns a <see cref="ConstantList{T}"/> if items in the list are identical.
        /// Otherwise, returns the original list.
        /// </summary>
        public static ImmutableList<T> MaybeConstant<T>(this ImmutableList<T> list)
        {
            if (list == null)
            {
                return null;
            }
            if (list is ConstantList<T>)
            {
                return list;
            }
            if (list.Count <= 1)
            {
                return list;
            }

            var first = list[0];
            if (list.Skip(1).All(item => Equals(first, item)))
            {
                return new ConstantList<T>(list.Count, first);
            }
            return list;
        }

        /// <summary>
        /// Returns a <see cref="NullableList{T}"/>
        /// </summary>
        public static ImmutableList<T?> Nullables<T>(this IEnumerable<T?> list) where T:struct
        {
            return list as NullableList<T> ?? new NullableList<T>(list);
        }

        private static readonly ImmutableList<bool> _booleanLevels = new[] { false, true }.ToImmutable();
        public static ImmutableList<bool> Booleans(this IEnumerable<bool> booleans)
        {
            var indexes = IntegerList.FromIntegers(booleans.Select(b => b ? 1 : 0));
            return new Factor<bool>(_booleanLevels, indexes).MaybeConstant();
        }

        public static Factor<T> ToFactor<T>(this IEnumerable<T> items)
        {
            return Factor<T>.FromItems(items);
        }
    }
}
