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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.FilesTree
{
    internal class FileSystemHealthMonitor : IDisposable
    {
        private readonly TimeSpan _checkInterval;
        private ManualResetEvent _manualTriggerEvent = new ManualResetEvent(false);
        private readonly object _lock = new object();

        private Thread _workerThread;
        private CancellationTokenSource _cancellationTokenSource;
        private Dictionary<string, PathAvailability> _paths;

        private bool _isWorking;

        internal event Action<string, PathAvailability> PathAvailabilityChanged;

        public FileSystemHealthMonitor(TimeSpan checkInterval)
        {
            _checkInterval = checkInterval;
            _paths = new Dictionary<string, PathAvailability>();
        }

        public void Start()
        {
            // If already started, stop the existing thread first to prevent multiple threads
            // from accessing the same _paths and _manualTriggerEvent
            if (_workerThread != null)
            {
                Stop();
            }

            // Reset stopping/stopped flags so AddPath() can be called again
            _isStopped = _isStopping = false;

            _cancellationTokenSource = new CancellationTokenSource();

            // ActionUtil.RunAsync() already starts the thread, so don't call Start() again
            _workerThread = ActionUtil.RunAsync(() => MonitorLoop(_cancellationTokenSource.Token),
                @"FilesTree => FileSystemAvailabilityMonitor");
        }

        public void AddPath(string newPath)
        {
            // Enforce usage sequence: Start() -> AddPath() -> Stop()
            // AddPath() should not be called after Stop() has been called
            Assume.IsFalse(_isStopping || IsStopped(),
                @"AddPath() called after Stop(). Expected sequence: Start() -> AddPath() -> Stop()");

            lock (_lock)
            {
                if (!_paths.ContainsKey(newPath))
                {
                    _paths[newPath] = PathAvailability.unknown;
                }
            }
        }

        private void MonitorLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_isWorking)
                {
                    SpinWait.SpinUntil(() => !cancellationToken.IsCancellationRequested, 200);
                    continue;
                }
                
                _isWorking = true;
                
                Dictionary<string, PathAvailability> snapshot;
                lock (_lock)
                {
                    snapshot = new Dictionary<string, PathAvailability>(_paths);
                }
                
                foreach (var kvPair in snapshot)
                {
                    var path = kvPair.Key;
                    var oldState = kvPair.Value;
                    var newState = CheckPathAvailability(path);
                
                    if (newState != oldState)
                    {
                        lock (_lock)
                        {
                            _paths[path] = newState;
                        }

                        // Ignore these events because they only happen at startup
                        if (oldState != PathAvailability.unknown)
                        {
                            PathAvailabilityChanged?.Invoke(path, newState);
                        }
                    }
                }
                
                _isWorking = false;

                SpinWait.SpinUntil(() => !cancellationToken.IsCancellationRequested, _checkInterval.Milliseconds);

                // Check cancellation before accessing the event
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Capture reference to avoid race with disposal
                var manualEvent = _manualTriggerEvent;
                if (manualEvent == null)
                    break; // Event was disposed, exit the loop

                manualEvent.WaitOne(_checkInterval);
                
                // Check cancellation again after WaitOne returns
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Reset the event (may throw ObjectDisposedException if disposed during WaitOne, but that's fine)
                manualEvent.Reset();
            }
        }

        public void Trigger()
        {
            // No lock needed - if null, monitor is stopped anyway
            _manualTriggerEvent?.Set();
        }

        private bool _isStopped;
        private bool _isStopping;

        public void Stop()
        {
            // Mark as stopping immediately to prevent AddPath() calls after Stop() starts
            // This enforces the usage sequence: Start() -> AddPath() -> Stop()
            _isStopping = true;

            _cancellationTokenSource?.Cancel();
            
            // Signal the manual trigger event so WaitOne() returns immediately
            // This allows the worker thread to check the cancellation token and exit
            _manualTriggerEvent?.Set();
            
            // Wait for the worker thread to exit completely
            // After Join() returns, we know the thread is gone and won't access _paths or _manualTriggerEvent
            _workerThread?.Join();

            // No lock needed here - Join() guarantees the worker thread is gone, and AddPath() 
            // will throw if called after Stop() starts (enforcing Start() -> AddPath() -> Stop() sequence)
            lock (_lock)    // For ReSharper
            {
                _paths?.Clear();
            }

            // Now that the thread has exited, it's safe to dispose the event
            // No thread protection needed here - Join() guarantees the worker thread is gone
            // Use Interlocked.Exchange only to prevent double-disposal if Stop() is called multiple times
            var manualTriggerEvent = Interlocked.Exchange(ref _manualTriggerEvent, null);
            manualTriggerEvent?.Dispose();

            _isStopped = true;
            _isStopping = false;
        }

        public bool IsStopped()
        {
            return _isStopped && (_workerThread == null || !_workerThread.IsAlive);
        }

        public void Dispose()
        {
            Stop();
        }

        // CONSIDER: Directory.Exists(path) is good for testing whether a directory is accessible but does not 
        //           catch whether a path exists but is inaccessible (for example, due to ACLs). A check that 
        //           does check for authorization is possible but much more expensive, especially on network
        //           drives. So just use Exists(...) for now.
        private static PathAvailability CheckPathAvailability(string path)
        {
            try
            {
                return Directory.Exists(path) ? PathAvailability.available : PathAvailability.unavailable;
            }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            return PathAvailability.unavailable;
        }
    }
}