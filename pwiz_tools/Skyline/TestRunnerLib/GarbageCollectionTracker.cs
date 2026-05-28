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

        // Weak-ref shadow of every object that a per-test CheckAfterTest declared a
        // leak and pinned. FinalCheck at end of run drops the pins and re-checks
        // these weak refs after a long settle; whatever is still alive is a real
        // leak, anything that has died was a slow-drain false positive. Purely
        // additive to per-test reporting -- entries are appended at PinSurvivors
        // time, never consulted during the test run.
        private static readonly List<TrackedObject> _postReportSurvivors = new List<TrackedObject>();

        /// <summary>
        /// Name of the currently-executing test, or null between tests. Set by
        /// RunTests at the start of each test so Register can stamp each
        /// <see cref="TrackedObject"/> with the test that introduced it, enabling
        /// <see cref="FinalCheck"/> to attribute end-of-run survivors back to a
        /// specific test.
        /// </summary>
        public static string CurrentTestName { get; set; }

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
        /// Returns the number of still-tracked objects (those not yet collected and
        /// not yet pruned). Used by <see cref="CheckAfterTest"/>'s adaptive retry loop
        /// to detect whether cleanup is still making progress (count dropping between
        /// forced GCs) versus stuck (count stable across cycles). Expects to be called
        /// after <see cref="CheckForLeaks"/> in the same sequence so the underlying
        /// list reflects the post-prune alive count.
        /// </summary>
        private static int SurvivorCount()
        {
            lock (_lock)
            {
                return _trackedObjects.Count;
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

        /// <summary>
        /// Releases strong references to survivors pinned from the previous test.
        /// Call this BEFORE FlushMemory so that pinned objects from the prior test
        /// do not act as GC roots during the current test's collection cycle,
        /// preventing a cascade where one real leak causes all subsequent tests
        /// to appear to leak the same unreleased objects.
        /// </summary>
        public static void ClearPins()
        {
            lock (_lock)
            {
                _pinnedSurvivors.Clear();
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
                    {
                        _pinnedSurvivors.Add(target);
                        // Record a weak-ref shadow with test attribution so FinalCheck
                        // can later distinguish real leaks from slow-drain transients.
                        _postReportSurvivors.Add(t);
                    }
                }
                _trackedObjects.Clear();
            }
        }

        // Adaptive retry parameters. The post-test GC check sleeps GC_RETRY_SLEEP_MS
        // between forced collections and accepts the survivor count as a real leak only
        // after GC_STABLE_CYCLES_REQUIRED consecutive cycles produce no further drop in
        // the count (forced GC made no progress on freeing the survivors). This is more
        // tolerant of legitimate transient retention than a fixed-budget retry --
        // background loaders, finalizer queue drain, and deferred GC under high-RAM /
        // server-GC modes can all extend the cleanup tail past a sub-second budget --
        // without losing the regression check, since a truly stuck reference still shows
        // as an unchanged count and is reported. GC_MAX_RETRY_CYCLES caps total wall
        // time so a runaway leak path cannot stall the suite.
        private const int GC_RETRY_SLEEP_MS = 500;
        private const int GC_STABLE_CYCLES_REQUIRED = 3;
        private const int GC_MAX_RETRY_CYCLES = 20;

        /// <summary>
        /// Post-test GC leak check with automatic dotMemory snapshot on leak detection.
        /// Call after FlushMemory. Returns an error message if a leak was found, null otherwise.
        ///
        /// Behavior depends on context:
        /// - DotMemoryWarmupRuns > 0: PinSurvivors mode (for explicit profiling sessions)
        /// - Prior test exception: Clear tracked objects (leak check would be misleading)
        /// - Survivors found: Retry with sleep+GC cycles, keeping the loop running as long
        ///   as the survivor count is still dropping. Declare a leak only after the count
        ///   has been stable across GC_STABLE_CYCLES_REQUIRED consecutive cycles (i.e.
        ///   forced GC made no further progress -- a real retention) or GC_MAX_RETRY_CYCLES
        ///   total cycles have elapsed. Then pin survivors and snapshot.
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

            // Phase 2: Survivors found - keep forcing GC while cleanup is still making
            // progress (survivor count dropping). Declare a leak only when the count has
            // been stable across GC_STABLE_CYCLES_REQUIRED consecutive cycles, since a
            // genuinely stuck reference cannot be released by additional forced GC while
            // a falling count means cleanup is still draining. See the constants block
            // above for the rationale (background loaders, finalizer drain, deferred GC
            // under high-RAM/server-GC modes can extend the cleanup tail past a fixed
            // sub-second budget without indicating a real leak).
            int lastCount = SurvivorCount();
            int stableCycles = 0;
            for (int cycle = 0; cycle < GC_MAX_RETRY_CYCLES; cycle++)
            {
                Thread.Sleep(GC_RETRY_SLEEP_MS);
                RunTests.MemoryManagement.FlushMemory();
                leakMessage = CheckForLeaks();
                if (leakMessage == null)
                    return null;

                int currentCount = SurvivorCount();
                if (currentCount < lastCount)
                {
                    // Cleanup is still draining survivors -- reset stability and wait more
                    stableCycles = 0;
                }
                else if (++stableCycles >= GC_STABLE_CYCLES_REQUIRED)
                {
                    // Survivor count unchanged across enough cycles -- treat as a real leak
                    break;
                }
                lastCount = currentCount;
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

        // Settle budget for FinalCheck. Generous compared to the per-test budget
        // because (a) there is no test-progress pressure at end of run, and (b)
        // the whole point of this check is to filter out drain-tail false
        // positives that the per-test check missed -- agents (e.g. Windows
        // Server 2022) where finalizer-chain reclamation runs slower than the
        // per-test budget will be reported as leaks per-test but should clear
        // here. Sleep is split across FINAL_CHECK_GC_CYCLES forced collections
        // so each cycle gets a chance to advance the finalizer chain.
        private const int FINAL_CHECK_SETTLE_SECONDS = 60;
        private const int FINAL_CHECK_GC_CYCLES = 10;

        /// <summary>
        /// End-of-run leak verification. Purely additive to per-test
        /// <see cref="CheckAfterTest"/>: it does not affect any test's pass/fail
        /// status and only logs an informational summary. Drops the strong
        /// references that per-test PinSurvivors calls accumulated, waits
        /// FINAL_CHECK_SETTLE_SECONDS while running FINAL_CHECK_GC_CYCLES forced
        /// collections, then logs any previously-pinned object whose
        /// WeakReference is still alive -- grouped by type and the test that
        /// first registered it.
        ///
        /// Distinguishes genuine leaks (still rooted after a generous settle)
        /// from per-test false positives caused by slow finalizer drain on some
        /// agents. Call once, just before the test runner exits.
        /// </summary>
        public static void FinalCheck(Action<string, object[]> log)
        {
            List<TrackedObject> survivors;
            lock (_lock)
            {
                if (_postReportSurvivors.Count == 0)
                {
                    log(@"# GC-LEAK final check: no previously-reported survivors to verify." + Environment.NewLine,
                        new object[0]);
                    return;
                }

                // Drop the strong refs so weak refs can actually reach zero if
                // cleanup was just slow. Per-test reporting has already happened
                // so dotMemory inspection is no longer relevant.
                _pinnedSurvivors.Clear();
                survivors = new List<TrackedObject>(_postReportSurvivors);
                _postReportSurvivors.Clear();
            }

            log(@"# GC-LEAK final check: settling for {0}s before re-checking {1} previously-reported survivors..." + Environment.NewLine,
                new object[] { FINAL_CHECK_SETTLE_SECONDS, survivors.Count });

            var sleepMs = (FINAL_CHECK_SETTLE_SECONDS * 1000) / FINAL_CHECK_GC_CYCLES;
            for (var i = 0; i < FINAL_CHECK_GC_CYCLES; i++)
            {
                Thread.Sleep(sleepMs);
                RunTests.MemoryManagement.FlushMemory();
            }

            var stillAlive = survivors.Where(t => t.IsAlive).ToList();
            if (stillAlive.Count == 0)
            {
                log(@"# GC-LEAK final check: all {0} previously-reported survivors were eventually collected (per-test reports were transient drain-tail, not real leaks)." + Environment.NewLine,
                    new object[] { survivors.Count });
                return;
            }

            var grouped = stillAlive
                .GroupBy(t => string.Format(@"{0} ({1})", t.TypeName, t.TestName ?? @"unknown"))
                .Select(g => g.Count() == 1 ? g.Key : string.Format(@"{0} x{1}", g.Key, g.Count()))
                .OrderBy(s => s)
                .ToList();

            log(@"# GC-LEAK final check: {0} of {1} previously-reported survivors STILL alive after {2}s settle: {3}" + Environment.NewLine,
                new object[] { stillAlive.Count, survivors.Count, FINAL_CHECK_SETTLE_SECONDS, string.Join(@", ", grouped) });
        }

        private class TrackedObject
        {
            public string TypeName { get; }
            public string TestName { get; }
            public WeakReference Reference { get; }
            public bool IsAlive => Reference.IsAlive;

            public TrackedObject(Type type, object target)
            {
                TypeName = type.Name;
                TestName = CurrentTestName;
                Reference = new WeakReference(target);
            }
        }
    }
}
