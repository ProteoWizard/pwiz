/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Threading;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Base class for transforming items in parallel and consuming them in order,
    /// while limiting the number of transformed items in memory.
    /// </summary>
    public abstract class ParallelOrderedTransformer<TSource, TResult> : IDisposable
    {
        private readonly int _maxTransformedItems;
        private readonly QueueWorker<WorkItem> _worker;
        private readonly Dictionary<int, TResult> _results = new Dictionary<int, TResult>();
        private int _nextAddIndex;
        private int _nextTakeIndex;
        private bool _doneAdding;
        private Exception _unhandledException;

        protected ParallelOrderedTransformer(int maxTransformedItems, int maxQueueSize)
        {
            _maxTransformedItems = maxTransformedItems;
            _worker = new QueueWorker<WorkItem>(null, ConsumeWorkItem);
            // ReSharper disable once VirtualMemberCallInConstructor
            _worker.RunAsync(ParallelEx.GetThreadCount(), GetThreadName(), maxQueueSize);
        }

        protected ParallelOrderedTransformer(int maxTransformedItems)
            : this(maxTransformedItems, maxTransformedItems)
        {
        }

        protected virtual string GetThreadName() => GetType().Name;

        protected abstract TResult Transform(TSource source);

        public void Add(TSource source)
        {
            int index;
            lock (_results)
            {
                if (_unhandledException != null)
                {
                    throw new AggregateException(_unhandledException);
                }
                index = _nextAddIndex++;
            }
            _worker.Add(new WorkItem(index, source));
        }

        public void AddAll(IEnumerable<TSource> sources)
        {
            CommonActionUtil.RunAsync(() =>
            {
                foreach (var source in sources)
                {
                    Add(source);
                }
                DoneAdding();
            }, CommonTextUtil.SpaceSeparate(GetThreadName(), nameof(AddAll)));
        }

        public void SetException(Exception ex)
        {
            lock (_results)
            {
                if (_unhandledException == null)
                {
                    _unhandledException = ex;
                    Monitor.PulseAll(_results);
                }
            }
        }

        private void ConsumeWorkItem(WorkItem item, int threadIndex)
        {
            try
            {
                TResult result = Transform(item.Source);
                lock (_results)
                {
                    while (item.Index >= _nextTakeIndex + _maxTransformedItems && _worker.Exception == null && _unhandledException == null)
                    {
                        Monitor.Wait(_results);
                    }

                    if (_worker.Exception != null || _unhandledException != null)
                        return;

                    _results.Add(item.Index, result);
                    Monitor.PulseAll(_results);
                }
            }
            catch (Exception ex)
            {
                SetException(ex);
                throw;
            }
        }

        public TResult TakeNext()
        {
            lock (_results)
            {
                while (!_results.ContainsKey(_nextTakeIndex))
                {
                    if (_worker.Exception != null)
                    {
                        throw new AggregateException(_worker.Exception);
                    }
                    if (_unhandledException != null)
                    {
                        throw new AggregateException(_unhandledException);
                    }
                    if (_doneAdding && _nextTakeIndex >= _nextAddIndex)
                        return default(TResult);

                    Monitor.Wait(_results);
                }

                TResult result = _results[_nextTakeIndex];
                _results.Remove(_nextTakeIndex);
                _nextTakeIndex++;
                Monitor.PulseAll(_results);
                return result;
            }
        }

        public void DoneAdding()
        {
            lock (_results)
            {
                _doneAdding = true;
                Monitor.PulseAll(_results);
            }
            _worker.DoneAdding();
        }

        public void Dispose()
        {
            _worker.Dispose();
        }

        private class WorkItem
        {
            public int Index { get; }
            public TSource Source { get; }

            public WorkItem(int index, TSource source)
            {
                Index = index;
                Source = source;
            }
        }
    }
}
