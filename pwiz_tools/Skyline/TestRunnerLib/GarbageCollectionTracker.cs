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

                // Group survivors by type for a concise report
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
                _trackedObjects.Clear();
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
