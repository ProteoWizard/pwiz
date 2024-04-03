/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using JetBrains.Annotations;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Collection of objects which need to eventually be disposed.
    /// This object's Dispose method calls Dispose on each of the items in the list.
    /// </summary>
    public class DisposableCollection<T> : IEnumerable<T>, IDisposable where T : IDisposable
    {
        private List<T> _list = new List<T>();
        [MustDisposeResource]
        public DisposableCollection()
        {
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public void Dispose()
        {
            foreach (var item in _list)
            {
                item?.Dispose();
            }
            _list.Clear();
        }

        public void Add([HandlesResourceDisposal] T item)
        {
            _list.Add(item);
        }
    }
}
