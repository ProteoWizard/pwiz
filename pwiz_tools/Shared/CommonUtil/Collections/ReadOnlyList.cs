/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Collections.Generic;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Simple implementation of IList that only needs a count and a function to retrieve a given element.
    /// </summary>
    public static class ReadOnlyList
    {
        public static IList<TItem> Create<TItem>(int count, Func<int, TItem> getter)
        {
            return new Impl<TItem>(count, getter);
        }

        public static ICollection<TItem> CreateCollection<TItem>(int count, IEnumerable<TItem> enumerable)
        {
            return new CollectionImpl<TItem>(count, enumerable);
        }

        private class Impl<TItem> : AbstractReadOnlyList<TItem>
        {
            private readonly int _count;
            private readonly Func<int, TItem> _getter;
            public Impl(int count, Func<int, TItem> getter)
            {
                _count = count;
                _getter = getter;
            }

            public override int Count
            {
                get { return _count; }
            }

            public override TItem this[int index]
            {
                get { return _getter(index); }
            }
        }

        private class CollectionImpl<TItem> : AbstractReadOnlyCollection<TItem>
        {
            private readonly IEnumerable<TItem> _enumerable;
            public CollectionImpl(int count, IEnumerable<TItem> enumerable)
            {
                _enumerable = enumerable;
                Count = count;
            }

            public override int Count { get; }

            public override IEnumerator<TItem> GetEnumerator()
            {
                return _enumerable.GetEnumerator();
            }
        }
    }
}
