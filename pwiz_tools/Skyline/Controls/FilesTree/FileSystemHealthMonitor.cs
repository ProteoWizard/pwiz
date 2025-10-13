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

namespace pwiz.Skyline.Controls.FilesTree
{
    internal class FileSystemHealthMonitor : IDisposable
    {
        private readonly TimeSpan _checkInterval;
        private readonly ManualResetEvent _manualTriggerEvent = new ManualResetEvent(false);
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
            _cancellationTokenSource = new CancellationTokenSource();

            _workerThread = new Thread(() => MonitorLoop(_cancellationTokenSource.Token))
            {
                Name = @"FilesTree => FileSystemAvailabilityMonitor",
                IsBackground = true
            };

            _workerThread.Start();
        }

        public void AddPath(string newPath)
        {
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

                _manualTriggerEvent.WaitOne(_checkInterval);
                _manualTriggerEvent.Reset();
            }
        }

        public void Trigger()
        {
            _manualTriggerEvent.Set();
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _workerThread?.Join();

            lock (_lock)
            {
                _paths?.Clear();
            }

            _manualTriggerEvent?.Dispose();
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