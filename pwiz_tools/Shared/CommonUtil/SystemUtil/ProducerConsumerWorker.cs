/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Threading;

namespace pwiz.Common.SystemUtil
{
    public class QueueWorker<TItem> : ProducerConsumerWorker<TItem, ConcurrentQueue<TItem>> where TItem : class
    {
        public QueueWorker(Func<int, TItem> produce = null, Action<TItem, int> consume = null) : base(produce, consume) { }
    }

    public class StackWorker<TItem> : ProducerConsumerWorker<TItem, ConcurrentStack<TItem>> where TItem : class
    {
        public StackWorker(Func<int, TItem> produce = null, Action<TItem, int> consume = null) : base(produce, consume) { }
    }

    // This is possible but not used as of now
    /*public class BagWorker<TItem> : ProducerConsumerWorker<TItem, ConcurrentBag<TItem>> where TItem : class
    {
        public BagWorker(Func<int, TItem> produce = null, Action<TItem, int> consume = null) : base(produce, consume) { }
    }*/

    public class ProducerConsumerWorker<TItem, TProducerConsumerCollection> : IDisposable where TItem : class where TProducerConsumerCollection : IProducerConsumerCollection<TItem>, new()
    {
        private readonly Func<int, TItem> _produce; 
        private readonly Action<TItem, int> _consume;
        private Thread[] _produceThreads;
        private Thread[] _consumeThreads;
        private BlockingCollection<TItem> _queue;
        private readonly object _queueLock = new object();
        private CountdownEvent _threadExit;
        private int _itemsWaiting;
        private Exception _exception;

        /// <summary>
        /// Construct a QueueWorker with optional "produce" and "consume" callback functions.
        /// </summary>
        /// <param name="produce">Function to produce a work item.</param>
        /// <param name="consume">Action to consume a work item.</param>
        public ProducerConsumerWorker(Func<int, TItem> produce = null, Action<TItem, int> consume = null)
        {
            _produce = produce;
            _consume = consume;
        }

        public Exception Exception
        {
            get { return _exception; }
        }

        /// <summary>
        /// Run this worker asynchronously, starting threads to consume work items.
        /// </summary>
        /// <param name="consumeThreads">How many threads to use to consume work items (0 to consume items synchronously).</param>
        /// <param name="consumeName">Name prefix for consumption threads (null for synchronous consumption).</param>
        /// <param name="maxQueueSize">Maximum number of work items to be queued at any time.</param>
        public void RunAsync(int consumeThreads, string consumeName, int maxQueueSize = int.MaxValue)
        {
            RunAsync(consumeThreads, consumeName, 0, null, maxQueueSize);
        }

        /// <summary>
        /// Run this worker asynchronously, potentially starting threads to produce or consume
        /// work items (or both).
        /// </summary>
        /// <param name="consumeThreads">How many threads to use to consume work items (0 to consume items synchronously).</param>
        /// <param name="consumeName">Name prefix for consumption threads (null for synchronous consumption).</param>
        /// <param name="produceThreads">How many threads to use to produce work items (0 to produce items synchronously).</param>
        /// <param name="produceName">Name prefix for production threads (null for synchronous production).</param>
        /// <param name="maxQueueSize">Maximum number of work items to be queued at any time.</param>
        public void RunAsync(int consumeThreads, string consumeName, int produceThreads, string produceName, int maxQueueSize = int.MaxValue)
        {
            // Create a queue and a number of threads to work on queued items.
            // First thread to call RunAsync wins.
            lock (_queueLock)
            {
                if (_queue != null)
                {
                    if (((consumeThreads == 0 && _consumeThreads == null) || (consumeThreads > 0 && _consumeThreads != null && consumeThreads == _consumeThreads.Length)) &&
                        ((produceThreads == 0 && _produceThreads == null) || (produceThreads > 0 && _produceThreads != null && produceThreads == _produceThreads.Length)) &&
                        _queue.BoundedCapacity == maxQueueSize)
                        return;
                    Abort();
                }
                _queue = new BlockingCollection<TItem>(new TProducerConsumerCollection(), maxQueueSize);
            }

            _threadExit = new CountdownEvent(consumeThreads);

            _produceThreads = null;
            if (produceThreads > 0)
            {
                _produceThreads = new Thread[produceThreads];
                for (int i = 0; i < produceThreads; i++)
                {
                    _produceThreads[i] = new Thread(Produce)
                    {
                        Name = produceThreads <= 1 ? produceName : produceName + @" (" + (i + 1) + @")"
                    };
                    _produceThreads[i].Start(i);
                }
            }

            _consumeThreads = null;
            if (consumeThreads > 0)
            {
                _consumeThreads = new Thread[consumeThreads];
                for (int i = 0; i < consumeThreads; i++)
                {
                    _consumeThreads[i] = new Thread(Consume)
                    {
                        Name = consumeThreads <= 1 ? consumeName : consumeName + @" (" + (i + 1) + @")"
                    };
                    _consumeThreads[i].Start(i);
                }
            }
        }

        public bool IsRunningAsync { get { return _queue != null; } }

        /// <summary>
        /// Private code for production threads.
        /// </summary>
        private void Produce(object threadIndex)
        {
            LocalizationHelper.InitThread();

            try
            {
                // Add work items until there aren't any more.
                while (Exception == null)
                {
                    var item = _produce((int) threadIndex);
                    if (item == null)
                        break;
                    _queue.Add(item);
                }
            }
            catch (Exception ex)
            {
                SetException(ex);
            }

            DoneAdding();
        }

        /// <summary>
        /// Private code for consumption threads.
        /// </summary>
        /// <param name="threadIndex"></param>
        private void Consume(object threadIndex)
        {
            LocalizationHelper.InitThread();

            try
            {
                // Take queued items and process them, until the QueueWorker is stopped.
                while (Exception == null)
                {
                    //CONSIDER: observed a hang here with two file loader threads trying to take from 
                    //an empty queue. Maybe should use TryDequeue instead.
                    var item = _queue?.Take();
                    if (item == null)
                        break;
                    _consume(item, (int) threadIndex);
                    Interlocked.Decrement(ref _itemsWaiting);
                    lock (this)
                    {
                        Monitor.PulseAll(this);
                    }
                }
            }
            catch (Exception ex)
            {
                SetException(ex);
            }
            try
            {
                _threadExit?.Signal();
            }
            catch
            {
                // ignored
                // InvalidOperationException occurs sometimes during shutdown
            }
        }

        private void SetException(Exception ex)
        {
            // The first exception in wins
            if (Interlocked.CompareExchange(ref _exception, ex, null) == null)
                Abort();
        }

        private void Abort(bool wait = false)
        {
            if (_consumeThreads == null)
                return;

            Clear();
            DoneAdding(wait);
        }

        public void Clear()
        {
            // Clear work queue.
            while (_queue != null && _queue.TryTake(out _))
            {
            }

            _itemsWaiting = 0;
        }

        /// <summary>
        /// Add an item to the work queue.
        /// </summary>
        public void Add(TItem item)
        {
            if (item == null)
                return;

            if (_consumeThreads == null)
                _consume(item, 0);
            else
            {
                Interlocked.Increment(ref _itemsWaiting);
                _queue.Add(item);
            }
        }

        /// <summary>
        /// Add a collection of items to the work queue, and optionally wait
        /// for them to be processed.  No subsequent calls to Add are
        /// expected.
        /// </summary>
        public void Add(ICollection<TItem> items, bool wait = false, bool done = true)
        {
            foreach (var item in items)
            {
                Add(item);
            }

            if(done)
                DoneAdding(wait);
        }

        /// <summary>
        /// Return an item from the work queue.
        /// </summary>
        public TItem Take()
        {
            var item = (_produceThreads == null) ? _produce(0) : _queue.Take();
            return item;
        }

        /// <summary>
        /// Wait for the work queue to empty.
        /// </summary>
        public void Wait()
        {
            if (_consumeThreads == null)
                return;
            lock (this)
            {
                while (_itemsWaiting != 0)
                    Monitor.Wait(this);
            }
        }

        /// <summary>
        /// Stop the threads after all items have been queued and processed.
        /// </summary>
        public void DoneAdding(bool wait = false)
        {
            if (_queue == null)
                return;
            if (_consumeThreads == null)
            {
                _queue.Add(null);
                return;
            }
            for (int i = 0; i < _consumeThreads.Length; i++)
                _queue.Add(null);
            if (wait)
                _threadExit.Wait();
        }

        public void Dispose()
        {
            Abort(true);
            SafeDispose(ref _queue);
            SafeDispose(ref _threadExit);
        }

        private void SafeDispose<T>(ref T disposable) where T : IDisposable
        {
            if (disposable != null)
            {
                var d = disposable;
                disposable = default;
                d.Dispose();
            }
        }
    }
}
