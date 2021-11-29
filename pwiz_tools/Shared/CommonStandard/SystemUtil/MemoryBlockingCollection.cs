/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Collections.Concurrent;
using System.Threading;

namespace pwiz.Common.SystemUtil
{
    public interface IMemSized
    {
        int Size { get; }
    }
    public class MemoryBlockingCollection<TMem> : IDisposable where TMem : IMemSized
    {
        private long _maximumMem;
        private BlockingCollection<TMem> _collection;
        private AutoResetEvent _memEvent;
        private long _heldMem;

        public MemoryBlockingCollection(long maximumMem, int maximumLength)
        {
            _maximumMem = maximumMem;
            _collection = new BlockingCollection<TMem>(maximumLength);
            _memEvent = new AutoResetEvent(false);
        }

        public void Dispose()
        {
            _collection.Dispose();
            _memEvent.Dispose();
        }

        public void Add(TMem item)
        {
            while (!TryAddMem(item.Size))
                _memEvent.WaitOne();
            _collection.Add(item);
        }

        private bool TryAddMem(int itemSize)
        {
            lock (this)
            {
                long newSize = _heldMem + itemSize;
                // Avoid adding more than the memory limit, unless there is no memory held yet.
                // Then take at least one element, or the program will deadlock.
                if (newSize > _maximumMem && _heldMem > 0)
                    return false;
                _heldMem = newSize;
                return true;
            }
        }

        public TMem Take()
        {
            var item = _collection.Take();
            RemoveMem(item.Size);
            return item;
        }

        public bool TryTake(out TMem item)
        {
            if (!_collection.TryTake(out item))
                return false;
            RemoveMem(item.Size);
            return true;
        }

        private void RemoveMem(int itemSize)
        {
            lock (this)
            {
                _heldMem -= itemSize;
            }

            _memEvent.Set();
        }
    }
}
