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
    /// <summary>
    /// Monitors directory availability on a background thread, checking periodically or on demand.
    ///
    /// DESIGN: Single-threaded monitor. Only one background thread runs at a time, captured in _workerThread.
    /// The Start() method ensures any existing thread is stopped before starting a new one.
    /// This class is NOT designed to support multiple concurrent monitor threads.
    /// </summary>
    internal class FileSystemHealthMonitor : IDisposable
    {
        private readonly TimeSpan _checkInterval;
        private AutoResetEvent _manualTriggerEvent = new AutoResetEvent(false);
        private readonly object _lock = new object();

        // Single background worker thread - only one runs at a time
        private Thread _workerThread;
        private CancellationTokenSource _cancellationTokenSource;
        private Dictionary<string, PathAvailability> _paths;

        internal event Action<string, PathAvailability> PathAvailabilityChanged;

        public FileSystemHealthMonitor(TimeSpan checkInterval)
        {
            _checkInterval = checkInterval;
            _paths = new Dictionary<string, PathAvailability>();
        }

        public void Start()
        {
            // If already started, stop the existing thread first to prevent multiple threads.
            // This class is designed for a single background thread at a time.
            if (_workerThread != null)
            {
                Stop();
            }

            // Reset stopping/stopped flags so AddPath() can be called again
            _isStopped = _isStopping = false;

            _cancellationTokenSource = new CancellationTokenSource();

            // Start the single background worker thread
            // ActionUtil.RunAsync() creates and starts the thread
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

        /// <summary>
        /// Background monitoring loop. This runs on the single worker thread created by Start().
        /// Periodically checks path availability and fires events when state changes.
        /// </summary>
        private void MonitorLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Check all monitored paths
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

                        // Ignore initial state transitions from unknown
                        if (oldState != PathAvailability.unknown)
                        {
                            PathAvailabilityChanged?.Invoke(path, newState);
                        }
                    }
                }

                // Wait for next check cycle
                // AutoResetEvent automatically resets when WaitAny returns due to the trigger
                // Exits when: (1) manual trigger via Trigger(), (2) timeout after _checkInterval, or (3) cancellation
                WaitHandle.WaitAny(new[] { _manualTriggerEvent, cancellationToken.WaitHandle }, _checkInterval);
            }
        }

        public void Trigger()
        {
            // Manually trigger an immediate check
            // AutoResetEvent will automatically reset when the worker thread wakes
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

            // Signal the manual trigger event so WaitAny() returns immediately
            // This allows the worker thread to check the cancellation token and exit
            _manualTriggerEvent?.Set();

            // Wait for the single worker thread to exit completely
            // After Join() returns, we know the thread is gone and won't access _paths or _manualTriggerEvent
            // IMPORTANT: Join BEFORE disposing CancellationTokenSource - the worker thread accesses
            // cancellationToken.WaitHandle in MonitorLoop, which throws ObjectDisposedException if disposed early
            _workerThread?.Join();

            // Dispose the cancellation token source - safe now that worker thread has exited
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            // Clear paths - safe because Join() guarantees the worker thread has exited
            lock (_lock)    // For ReSharper
            {
                _paths?.Clear();
            }

            // Dispose the event - safe because the worker thread has exited
            // Use Interlocked.Exchange to prevent double-disposal if Stop() is called multiple times
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
