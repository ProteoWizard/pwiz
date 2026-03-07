/*
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

using System;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Controls.FilesTree
{
    public class BackgroundActionService : IDisposable
    {
        private QueueWorker<Action> _workQueue;
        private volatile bool _isShutdown;
        private int _pendingActionCount;

        // CONSIDER: Should this take a CancellationToken? Would be nice to centralize cancellation checks before executing actions queued
        //           for processing on a background / UI thread.
        public BackgroundActionService(Control synchronizingObject)
        {
            Assume.IsNotNull(synchronizingObject);

            SynchronizingObject = synchronizingObject;

            var threadCount = ParallelEx.GetThreadCount(2); // to process tasks synchronously, set to zero
            _workQueue = new QueueWorker<Action>(null, ProcessTask);
            _workQueue.RunAsync(threadCount, @"BackgroundActionService: system for background processing of FilesTree related work");
        }

        private Control SynchronizingObject { get; }
        internal bool IsComplete => _pendingActionCount == 0;

        /// <summary>
        /// Queue a task to be processed on a background thread. Callers are responsible
        /// for cancelling tasks if needed - for example, by checking a CancellationToken.
        /// </summary>
        /// <param name="action">Action to queue for async processing</param>
        internal void AddTask(Action action)
        {
            if (_isShutdown)
                return;

            // Capture reference to avoid race with Dispose() setting _workQueue to null
            var queue = _workQueue;
            if (queue == null)
                return; // Service has been disposed

            Interlocked.Increment(ref _pendingActionCount);
            try
            {
                queue.Add(action);
            }
            catch (ObjectDisposedException)
            {
                // Race condition: queue was disposed between our null check and Add call
                _pendingActionCount = 0;
            }
        }

        /// <summary>
        /// Internal method for processing a task on a background thread.
        /// </summary>
        /// <param name="action">Action to process asynchronously</param>
        /// <param name="threadIndex">Identifier for the thread processing the action</param>
        private void ProcessTask(Action action, int threadIndex)
        {
            try
            {
                action();
            }
            finally
            {
                var result = Interlocked.Decrement(ref _pendingActionCount);
                Assume.IsTrue(result >= 0);
            }
        }

        /// <summary>
        /// Run an action on the UI thread. Callers are responsible for cancelling tasks
        /// as needed - for example, by checking a CancellationToken. 
        /// </summary>
        /// <param name="action">Action to queue for processing on the UI thread.</param>
        internal void RunUI(Action action)
        {
            Assume.IsNotNull(SynchronizingObject);

            if (_isShutdown)
                return;

            if (!SynchronizingObject.IsDisposed && SynchronizingObject.IsHandleCreated)
            {
                Interlocked.Increment(ref _pendingActionCount);
                if (!CommonActionUtil.SafeBeginInvoke(SynchronizingObject, () =>
                    {
                        try
                        {
                            action();
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _pendingActionCount);
                            Assume.IsTrue(_pendingActionCount >= 0);
                        }
                    }))
                {
                    Interlocked.Decrement(ref _pendingActionCount);
                }
            }
        }

        /// <summary>
        /// Stop this service, clearing pending tasks and preventing new tasks from being added.
        /// This action is irreversible and should be used only when the owner is sure it no longer
        /// needs the service - for example, when a Control is disposed.
        /// </summary>
        public void Shutdown()
        {
            Assume.IsNotNull(_workQueue);

            _isShutdown = true;
            _workQueue?.Clear();
            _workQueue?.DoneAdding(true);
        }

        public void Dispose()
        {
            if (_workQueue != null)
            {
                Shutdown();

                _workQueue.Dispose();
                _workQueue = null;

                _pendingActionCount = 0;
            }
        }
    }
}