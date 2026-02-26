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
        /// Always clears the tracker regardless of outcome.
        /// </summary>
        public static string CheckForLeaks()
        {
            lock (_lock)
            {
                if (_trackedObjects.Count == 0)
                    return null;

                var survivorCounts = _trackedObjects
                    .Where(t => t.IsAlive)
                    .GroupBy(t => t.TypeName)
                    .Select(g => g.Count() == 1 ? g.Key : string.Format(@"{0} x{1}", g.Key, g.Count()))
                    .ToList();

                _trackedObjects.Clear();

                if (survivorCounts.Count == 0)
                    return null;

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

        /// <summary>
        /// Post-test GC leak check with automatic dotMemory snapshot on leak detection.
        /// Call after FlushMemory. Returns an error message if a leak was found, null otherwise.
        ///
        /// Behavior depends on context:
        /// - DotMemoryWarmupRuns > 0: PinSurvivors mode (for explicit profiling sessions)
        /// - Prior test exception: Clear tracked objects (leak check would be misleading)
        /// - dotMemory attached: Check for leaks, pin survivors and snapshot if found
        /// - Normal: Check for leaks, report if found
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

            // Use CheckAndPinLeaks which checks + pins in one pass, keeping
            // survivors accessible for dotMemory. The pin is harmless when
            // dotMemory is not attached (just extra strong refs until next Clear).
            var leakMessage = CheckAndPinLeaks();
            if (leakMessage != null && MemoryProfiler.IsReady)
            {
                // dotMemory is attached - take a snapshot with pinned survivors
                var snapshotName = testName + @"_GC_LEAK";
                log("\n# GC leak detected - taking dotMemory snapshot: {0}\n", new object[] { snapshotName });
                MemoryProfiler.Snapshot(snapshotName);
            }
            return leakMessage;
        }

        /// <summary>
        /// Checks for leaks and pins survivors atomically. Returns an error message
        /// listing survivors if any are found, null otherwise.  Pinning keeps the
        /// leaked objects alive so dotMemory can show retention paths.
        /// </summary>
        private static string CheckAndPinLeaks()
        {
            lock (_lock)
            {
                if (_trackedObjects.Count == 0)
                    return null;

                _pinnedSurvivors.Clear();
                var survivorCounts = new List<string>();

                foreach (var group in _trackedObjects.Where(t => t.IsAlive).GroupBy(t => t.TypeName))
                {
                    // Pin survivors so dotMemory can inspect retention paths
                    foreach (var t in group)
                    {
                        var target = t.Reference.Target;
                        if (target != null)
                            _pinnedSurvivors.Add(target);
                    }
                    survivorCounts.Add(group.Count() == 1
                        ? group.Key
                        : string.Format(@"{0} x{1}", group.Key, group.Count()));
                }

                _trackedObjects.Clear();

                if (survivorCounts.Count == 0)
                    return null;

                return string.Format(@"Objects not garbage collected after test: {0}",
                    string.Join(@", ", survivorCounts));
            }
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
