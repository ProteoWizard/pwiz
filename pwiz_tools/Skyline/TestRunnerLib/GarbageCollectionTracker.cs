/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
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
using System.Linq;
using System.Threading;

namespace TestRunnerLib
{
    /// <summary>
    /// Tracks objects via WeakReferences to verify they become eligible for
    /// garbage collection after test cleanup. Registration happens from
    /// Skyline code via <c>Program.GcTracker</c>; verification happens in
    /// RunTests.Run after FlushMemory.
    ///
    /// Inspired by a Java pattern of using weak-reference maps to assert
    /// that primary objects (like the main window and document) are properly
    /// released after each test, preventing silent memory leak regressions.
    /// </summary>
    public static class GarbageCollectionTracker
    {
        private static readonly object _lock = new object();
        private static readonly List<TrackedObject> _trackedObjects = new List<TrackedObject>();

        /// <summary>
        /// Register an object that should become garbage-collectible after the
        /// current test completes. Only a WeakReference is stored, so this call
        /// does not itself prevent collection.
        /// </summary>
        /// <param name="type">The type being tracked, used to build the name
        ///     for failure messages</param>
        /// <param name="target">The object to track</param>
        public static void Register(Type type, object target)
        {
            if (target == null)
                return;

            lock (_lock)
            {
                _trackedObjects.Add(new TrackedObject(type, target));
            }
        }

        /// <summary>
        /// Checks whether all tracked objects have been garbage collected.
        /// Returns null on success, or an error message listing survivors.
        /// Clears collected objects but retains survivors for retry or pinning.
        /// </summary>
        public static string CheckForLeaks()
        {
            lock (_lock)
            {
                if (_trackedObjects.Count == 0)
                    return null;

                // Remove objects that have been collected, keep survivors
                _trackedObjects.RemoveAll(t => !t.IsAlive);

                if (_trackedObjects.Count == 0)
                    return null;

                var survivorCounts = _trackedObjects
                    .GroupBy(t => t.TypeName)
                    .Select(g => g.Count() == 1 ? g.Key : string.Format(@"{0} x{1}", g.Key, g.Count()))
                    .ToList();

                return string.Format(@"Objects not garbage collected after test: {0}",
                    string.Join(@", ", survivorCounts));
            }
        }

        /// <summary>
        /// Clears all tracked objects without checking. Use when a test has
        /// already failed and leak checking would produce misleading noise.
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _pinnedSurvivors.Clear();
                _trackedObjects.Clear();
            }
        }

        // Strong references to survivors, kept alive for dotMemory inspection
        // ReSharper disable once CollectionNeverQueried.Local
        private static readonly List<object> _pinnedSurvivors = new List<object>();

        /// <summary>
        /// Promotes surviving (not yet GC'd) objects from weak to strong references
        /// so they appear in dotMemory snapshots for retention path analysis.
        /// Use instead of Clear() when memory profiling is active.
        /// </summary>
        public static void PinSurvivors()
        {
            lock (_lock)
            {
                _pinnedSurvivors.Clear();
                foreach (var t in _trackedObjects)
                {
                    var target = t.Reference.Target;
                    if (target != null)
                        _pinnedSurvivors.Add(target);
                }
                _trackedObjects.Clear();
            }
        }

        private const int GC_RETRY_COUNT = 3;
        private const int GC_RETRY_SLEEP_MS = 500;

        /// <summary>
        /// Post-test GC leak check with automatic dotMemory snapshot on leak detection.
        /// Call after FlushMemory. Returns an error message if a leak was found, null otherwise.
        ///
        /// Behavior depends on context:
        /// - DotMemoryWarmupRuns > 0: PinSurvivors mode (for explicit profiling sessions)
        /// - Prior test exception: Clear tracked objects (leak check would be misleading)
        /// - Survivors found: Retry with sleep+GC cycles to distinguish transient GC
        ///   timing from real leaks, then pin survivors and snapshot if still leaking
        /// </summary>
        public static string CheckAfterTest(string testName, int dotMemoryWarmupRuns,
            Exception exception, Action<string, object[]> log)
        {
            if (dotMemoryWarmupRuns > 0)
            {
                PinSurvivors();
                return null;
            }

            if (exception != null)
            {
                Clear();
                return null;
            }

            // Phase 1: Check for survivors without pinning
            var leakMessage = CheckForLeaks();
            if (leakMessage == null)
                return null;

            // Phase 2: Survivors found - retry with sleep+GC to rule out transient GC timing
            for (int retry = 0; retry < GC_RETRY_COUNT; retry++)
            {
                Thread.Sleep(GC_RETRY_SLEEP_MS);
                RunTests.MemoryManagement.FlushMemory();
                leakMessage = CheckForLeaks();
                if (leakMessage == null)
                    return null;
            }

            // Phase 3: Still leaking after retries - pin survivors and report
            PinSurvivors();
            if (MemoryProfiler.IsReady)
            {
                var snapshotName = testName + @"_GC_LEAK";
                log("\n# GC leak detected - taking dotMemory snapshot: {0}\n", new object[] { snapshotName });
                MemoryProfiler.Snapshot(snapshotName);
            }
            return leakMessage;
        }

        private class TrackedObject
        {
            public string TypeName { get; }
            public WeakReference Reference { get; }
            public bool IsAlive => Reference.IsAlive;

            public TrackedObject(Type type, object target)
            {
                TypeName = type.Name;
                Reference = new WeakReference(target);
            }
        }
    }
}
