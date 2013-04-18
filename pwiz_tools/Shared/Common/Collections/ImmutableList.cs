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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Constructs lists which cannot be modified.
    /// This class has static methods which return a read-only list.
    /// The read-only list overrides Equals() so that it uses content-equality.
    /// The actual implementation class is private.
    /// </summary>
    public static class ImmutableList
    {
        public static IList<T> ValueOf<T>(IEnumerable<T> values)
        {
            if (values == null)
            {
                return null;
            }
            var immutableList = values as Impl<T>;
            if (immutableList != null)
            {
                return immutableList;
            }
            return new Impl<T>(values.ToArray());
        }
        public static IList<T> Empty<T>()
        {
            return Impl<T>.EMPTY_IMPL;
        }
        public static IList<T> Singleton<T>(T value)
        {
            return new Impl<T>(new[]{value});
        }
        class Impl<T> : ReadOnlyCollection<T>
        {
            public static readonly IList<T> EMPTY_IMPL = new Impl<T>(new T[0]);
            public Impl(IList<T> list) : base(list)
            {
            }
            public override int GetHashCode()
            {
                return CollectionUtil.GetHashCodeDeep(this);
            }

            public override bool Equals(object o)
            {
                if (o == null)
                {
                    return false;
                }
                if (o == this)
                {
                    return true;
                }
                var that = o as Impl<T>;
                if (null == that)
                {
                    return false;
                }
                return this.SequenceEqual(that);
            }
        }
    }
}
