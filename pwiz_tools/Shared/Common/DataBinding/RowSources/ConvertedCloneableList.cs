/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

namespace pwiz.Common.DataBinding.RowSources
{
    public abstract class ConvertedCloneableList<TKey, TSource, TTarget> : ICloneableList<TKey, TTarget>
    {
        protected ConvertedCloneableList(ICloneableList<TKey, TSource> sourceList)
        {
            SourceList = sourceList;
        }

        public ICloneableList<TKey, TSource> SourceList { get; private set; }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<TTarget> GetEnumerator()
        {
            return SourceList.Select(Convert).GetEnumerator();
        }

        public void Add(TTarget item)
        {
            SourceList.Add(Unconvert(item));
        }

        public void Clear()
        {
            SourceList.Clear();
        }

        public bool Contains(TTarget item)
        {
            return SourceList.IndexOfKey(GetKey(item)) >= 0;
        }

        public void CopyTo(TTarget[] array, int arrayIndex)
        {
            CopyTo((Array) array, arrayIndex);
        }

        public void CopyTo(Array array, int arrayIndex)
        {
            SourceList.Select(Convert).ToArray().CopyTo(array, arrayIndex);
        }

        public bool Remove(TTarget item)
        {
            int index = SourceList.IndexOfKey(GetKey(item));
            if (index < 0)
            {
                return false;
            }
            SourceList.RemoveAt(index);
            return true;
        }

        public int Count
        {
            get { return SourceList.Count; }
        }

        public bool IsReadOnly
        {
            get { return SourceList.IsReadOnly; }
        }

        public int IndexOf(TTarget item)
        {
            return IndexOfKey(GetKey(item));
        }

        public void Insert(int index, TTarget item)
        {
            SourceList.Insert(index, Unconvert(item));
        }

        public void RemoveAt(int index)
        {
            SourceList.RemoveAt(index);
        }

        public TTarget this[int index]
        {
            get { return Convert(SourceList[index]); }
            set { SourceList[index] = Unconvert(value); }
        }

        public int IndexOfKey(TKey key)
        {
            return SourceList.IndexOfKey(key);
        }

        public abstract TKey GetKey(TTarget value);

        public IList<TTarget> DeepClone()
        {
            return SourceList.DeepClone().Select(Convert).ToArray();
        }

        IEnumerable ICloneableList.DeepClone()
        {
            return DeepClone();
        }

        protected abstract TTarget Convert(TSource source);
        protected virtual TSource Unconvert(TTarget target)
        {
            throw new InvalidOperationException();
        }
    }
}
