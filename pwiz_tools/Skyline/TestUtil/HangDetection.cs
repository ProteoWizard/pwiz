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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace pwiz.SkylineTestUtil
{
    public static class HangDetection
    {
        /// <summary>
        /// If action takes more than 30 minutes to complete, interrupt this thread and write threads to console.
        /// </summary>
        public static void InterruptWhenHung(Action action)
        {
            InterruptAfter(action, TimeSpan.FromSeconds(1), 1800);
        }
        
        /// <summary>
        /// If action takes longer than <paramref name="cycleCount"/> times <paramref name="cycleDuration"/> then
        /// interrupt this thread and dump callstacks to the console.
        /// </summary>
        public static void InterruptAfter(Action action, TimeSpan cycleDuration, int cycleCount)
        {
            bool[] completed = new bool[1];
            var currentThread = Thread.CurrentThread;
            try
            {
                ActionUtil.RunAsync(() =>
                    InterruptThreadAfter(completed, currentThread, cycleDuration, cycleCount), nameof(HangDetection));
                try
                {
                    action();
                }
                finally
                {
                    lock (completed)
                    {
                        completed[0] = true;
                        Monitor.Pulse(completed);
                    }
                }
            }
            catch (ThreadInterruptedException interruptedException)
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
                throw new AssertFailedException(string.Format("Timeout waiting {0} for action to complete", TimeSpan.FromTicks(cycleDuration.Ticks * cycleCount)), interruptedException);
            }
        }

        private static void InterruptThreadAfter(bool[] completed, Thread thread, TimeSpan cycleDuration, int cycleCount)
        {
            lock (completed)
            {
                for (int i = 0; i < cycleCount; i++)
                {
                    if (completed[0])
                    {
                        return;
                    }

                    Monitor.Wait(completed, cycleDuration);
                }

                if (!completed[0])
                {
                    thread.Interrupt();
                }
            }
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
