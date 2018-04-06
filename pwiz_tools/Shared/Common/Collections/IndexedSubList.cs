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
using System.Collections.Generic;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// A list which contains items which are drawn from another list.
    /// </summary>
    public class IndexedSubList<T> : AbstractReadOnlyList<T>
    {
        private readonly IList<int> _indexes;
        private readonly IList<T> _list;
        public IndexedSubList(IList<T> list, IList<int> indexes)
        {
            _list = list;
            _indexes = indexes;
        }

        public override int Count
        {
            get { return _indexes.Count; }
        }

        public override T this[int index]
        {
            get { return _list[_indexes[index]]; }
        }
    }
}
