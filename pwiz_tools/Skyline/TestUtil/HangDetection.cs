/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
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
using Microsoft.Diagnostics.Runtime;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using pwiz.Common.SystemUtil;

namespace pwiz.SkylineTestUtil
{
    public class HangDetection : IDisposable
    {
        private readonly object _lock = new object();
        private bool _disposed;
        private TimeSpan? _waitDuration;
        private Thread _callerThread;
        private readonly Thread _watchdogThread;

        public HangDetection()
        {
            _watchdogThread = new Thread(WatchdogLoop)
            {
                IsBackground = true,
                Name = nameof(HangDetection)
            };
            _watchdogThread.Start();
        }

        /// <summary>
        /// If action takes more than 30 minutes to complete, interrupt this thread.
        /// </summary>
        public static void InterruptWhenHung(Action action)
        {
            using var hangDetection = new HangDetection();
            hangDetection.InterruptAfter(action, TimeSpan.FromMinutes(30));
        }

        /// <summary>
        /// If action takes longer than <paramref name="duration"/> then
        /// interrupt this thread and dump callstacks to the console.
        /// </summary>
        public void InterruptAfter(Action action, TimeSpan duration)
        {
            lock (_lock)
            {
                _waitDuration = duration;
                _callerThread = Thread.CurrentThread;
                Monitor.Pulse(_lock);
            }

            try
            {
                action();
            }
            catch (ThreadInterruptedException)
            {
                try
                {
                    var threadDumpLines = new List<string> { "*** Hang detected. Thread dump:" };
                    threadDumpLines.AddRange(GetAllThreadsCallstacks(Process.GetCurrentProcess().Id));
                    threadDumpLines.Add("*** End of thread dump");
                    Console.Out.WriteLine(TextUtil.LineSeparate(threadDumpLines));
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLine("Unable to get thread dump: {0}", ex);
                }

                try
                {
                    foreach (var form in FormUtil.OpenForms)
                    {
                        Console.Out.WriteLine("Open Form: {0}", AbstractFunctionalTest.GetTextForForm(form));
                    }
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLine("Unable to get open forms string: {0}", ex);
                }
                throw;
            }
            finally
            {
                lock (_lock)
                {
                    _waitDuration = null;
                    Monitor.Pulse(_lock);
                }
            }
        }

        private void WatchdogLoop()
        {
            lock (_lock)
            {
                while (!_disposed)
                {
                    if (!_waitDuration.HasValue)
                    {
                        Monitor.Wait(_lock);
                        continue;
                    }

                    var duration = _waitDuration.Value;
                    var stopWatch = Stopwatch.StartNew();
                    TimeSpan cycleDuration = TimeSpan.FromTicks(100);
                    long minCycleCount = duration.Ticks / cycleDuration.Ticks;

                    for (long cycleIndex = 0; ; cycleIndex++)
                    {
                        if (!_waitDuration.HasValue || _disposed)
                        {
                            break;
                        }

                        if (cycleIndex > minCycleCount && stopWatch.Elapsed > duration)
                        {
                            _callerThread.Interrupt();
                            break;
                        }

                        Monitor.Wait(_lock, cycleDuration);
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                Monitor.Pulse(_lock);
            }

            _watchdogThread.Join();
        }

        public static IEnumerable<string> GetAllThreadsCallstacks(int processId)
        {
            using var dataTarget = DataTarget.AttachToProcess(processId, 5000, AttachFlag.Passive);
            var runtime = dataTarget.ClrVersions[0].CreateRuntime();

            foreach (var thread in runtime.Threads)
            {
                if (!thread.IsAlive) continue;

                yield return $"Thread {thread.OSThreadId:X} (Managed ID: {thread.ManagedThreadId})";

                foreach (var frame in thread.EnumerateStackTrace())
                {
                    yield return $"  {frame.Method?.Type?.Name}.{frame.Method?.Name ?? "[Unknown]"}";
                }

                yield return string.Empty;
            }
        }
    }
}
