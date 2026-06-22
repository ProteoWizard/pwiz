/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.Threading;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// The shared plumbing the scoring tasks (<see cref="PerFileScoringTask"/>,
    /// <see cref="PerFileRescoreTask"/>, <see cref="FirstJoinTask"/>) once
    /// inherited from the retired <c>AbstractScoringTask</c> base: the mzML read
    /// gate, the PIN feature width + base-id mask constants, the isolation-window
    /// extractor, and the nearest-MS1 lookup. None of it needs instance state, so
    /// it lives here as <c>internal static</c> rather than in a base class --
    /// removing the inheritance edge that invited feature-envy. The actual scoring
    /// BEHAVIOR lives in <see cref="ScoringPipeline"/> (composition, debt-paydown
    /// PR 7); tasks reach it through <see cref="Pipeline"/> and call its methods
    /// directly.
    /// </summary>
    internal static class ScoringTaskShared
    {
        // Internal so the scoring tasks share the single PIN feature width without
        // redeclaring it. Derives from the single source of truth in
        // OspreySharp.Scoring so the two cannot drift.
        internal const int NUM_PIN_FEATURES = OspreyFeatureCalculators.FeatureCount;

        // EntryId encodes target/decoy in the high bit; base_id is the lower 31
        // bits, shared by a target and its paired decoy.
        internal const uint BASE_ID_MASK = 0x7FFFFFFFu;

        // Serializes mzML reads across concurrent ProcessFile() calls. The
        // producer inside MzmlReader.LoadAllSpectra is a sequential XmlReader over
        // a FileStream, so 3 files parsing in parallel means 3 sequential disk
        // scans fighting for the same head/cache. Gating the parse step funnels
        // the disk-bound work into one stream at a time while leaving the
        // subsequent main-search phase free to run in parallel across files.
        internal static readonly SemaphoreSlim s_mzmlReadGate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Build the <see cref="ScoringPipeline"/> the tasks score through, wired
        /// to the run's log sink and (optional) diagnostics seam. Cheap: the
        /// pipeline only carries those two references, so constructing one per
        /// scoring call matches the former per-call construction inside the base
        /// class forwarders exactly.
        /// </summary>
        internal static ScoringPipeline Pipeline(PipelineContext ctx)
        {
            return new ScoringPipeline(ctx.LogInfo, ctx.Diagnostics as IScoringDiagnostics);
        }

        /// <summary>
        /// Extract unique isolation windows from the first cycle of MS2 spectra.
        /// </summary>
        internal static List<IsolationWindow> ExtractIsolationWindows(List<Spectrum> spectra)
        {
            var windows = new List<IsolationWindow>();
            var seenCenters = new HashSet<int>();

            foreach (var spectrum in spectra)
            {
                int centerKey = (int)Math.Round(spectrum.IsolationWindow.Center * 10.0);
                if (seenCenters.Contains(centerKey))
                    break;
                seenCenters.Add(centerKey);
                windows.Add(spectrum.IsolationWindow);
            }

            // Sort by center m/z
            windows.Sort((a, b) => a.Center.CompareTo(b.Center));
            return windows;
        }

        /// <summary>
        /// Find the MS1 spectrum with retention time closest to the given RT.
        /// Assumes MS1 spectra are sorted by RT. Thin forwarder to the single
        /// implementation in Core (<see cref="MS1Spectrum.FindNearest"/>) so the
        /// scoring harness and the <c>Calibrator</c> share one binary search /
        /// tie-break and cannot drift.
        /// </summary>
        internal static MS1Spectrum FindNearestMs1(List<MS1Spectrum> ms1Spectra, double rt)
        {
            return MS1Spectrum.FindNearest(ms1Spectra, rt);
        }
    }
}
