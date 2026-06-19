/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Base class for tasks that drive the OspreySharp scoring engine
    /// (PerFileScoringTask and PerFileRescoreTask). Owns the shared
    /// per-window scoring + per-candidate feature computation +
    /// library prep + dedup helpers; subclasses orchestrate the
    /// per-file or per-pass flow that uses them.
    ///
    /// Methods are protected so subclasses can call them as bare
    /// names; static helpers and constants are protected static so
    /// subclasses (or static contexts inside them) can reach them
    /// without back-references.
    ///
    /// Phase A scope: a mechanical lift of the methods that used to
    /// live on AnalysisPipeline. The shared scoring engine now lives
    /// here; AnalysisPipeline becomes the thin task-pipeline driver
    /// (Run + log sinks).
    /// </summary>
    public abstract class AbstractScoringTask : OspreyTask
    {
        // Internal so FirstJoinTask (which now owns RunPercolatorFdr +
        // RunPercolatorStreaming + BuildBasicFeatures) can reuse the
        // same feature width without redeclaring it. Derives from the
        // single source of truth in OspreySharp.Scoring so the two cannot drift.
        internal const int NUM_PIN_FEATURES = OspreyFeatureCalculators.FeatureCount;


        // EntryId encodes target/decoy in the high bit; base_id is the
        // lower 31 bits, shared by a target and its paired decoy.
        // Internal so the Tasks/ subfolder partials (e.g. FirstJoinTask)
        // can use the same constant.
        internal const uint BASE_ID_MASK = 0x7FFFFFFFu;


        // Serializes mzML reads across concurrent ProcessFile() calls.
        // The producer inside MzmlReader.LoadAllSpectra is a sequential
        // XmlReader over a FileStream, so 3 files parsing in parallel
        // means 3 sequential disk scans fighting for the same head/cache.
        // Gating the parse step funnels the disk-bound work into one
        // stream at a time while leaving the subsequent main-search
        // phase free to run in parallel across files.
        // Internal so LoadSpectra (now on PerFileScoringTask) can take
        // the same gate without redeclaring a parallel SemaphoreSlim.
        internal static readonly SemaphoreSlim s_mzmlReadGate = new SemaphoreSlim(1, 1);


        /// <summary>
        /// Extract unique isolation windows from the first cycle of MS2 spectra.
        /// </summary>
        protected List<IsolationWindow> ExtractIsolationWindows(List<Spectrum> spectra)
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
        /// Run coelution scoring for all library entries across all isolation windows.
        /// For each window, finds candidate entries whose precursor falls in the window,
        /// extracts fragment XICs, detects CWT peaks, and scores at each peak.
        /// </summary>
        protected List<FdrEntry> RunCoelutionScoring(
            List<LibraryEntry> fullLibrary,
            List<Spectrum> spectra,
            List<MS1Spectrum> ms1Spectra,
            List<IsolationWindow> isolationWindows,
            RTCalibration rtCalibration,
            MzCalibrationResult ms2Calibration,
            MzCalibrationResult ms1Calibration,
            ScoringContext context,
            PipelineContext ctx)
        {
            return new ScoringPipeline(ctx.LogInfo, ctx.Diagnostics as IScoringDiagnostics)
                .RunCoelutionScoring(
                    fullLibrary, spectra, ms1Spectra, isolationWindows,
                    rtCalibration, ms2Calibration, ms1Calibration, context);
        }


        /// <summary>
        /// Find the MS1 spectrum with retention time closest to the given RT.
        /// Assumes MS1 spectra are sorted by RT. Thin forwarder to the single
        /// implementation in Core (<see cref="MS1Spectrum.FindNearest"/>) so the
        /// harness (here + <c>Calibrator</c>) and the MS1 feature calculators share
        /// one binary search / tie-break and cannot drift.
        /// </summary>
        internal static MS1Spectrum FindNearestMs1(List<MS1Spectrum> ms1Spectra, double rt)
        {
            return MS1Spectrum.FindNearest(ms1Spectra, rt);
        }


        /// <summary>
        /// Within each isolation window, drop scored entries whose top-6
        /// fragment lists overlap >= 50% with another same-class entry
        /// eluting within +/-5 spectra. Of each colliding pair, the
        /// entry with the higher coelution_sum survives. Mirrors
        /// osprey/crates/osprey/src/pipeline.rs::deduplicate_double_counting
        /// so the same precursor cannot be counted twice from a shared
        /// chromatographic feature.
        /// </summary>
        protected List<FdrEntry> DeduplicateDoubleCounting(
            List<FdrEntry> entries,
            List<LibraryEntry> library,
            IList<Spectrum> spectra,
            MzCalibrationResult ms2Cal,
            List<IsolationWindow> isolationWindows,
            OspreyConfig config,
            PipelineContext ctx)
        {
            return new ScoringPipeline(ctx.LogInfo, ctx.Diagnostics as IScoringDiagnostics)
                .DeduplicateDoubleCounting(
                    entries, library, spectra, ms2Cal, isolationWindows, config);
        }


        protected List<FdrEntry> DeduplicatePairs(List<FdrEntry> entries, PipelineContext ctx)
        {
            return new ScoringPipeline(ctx.LogInfo, ctx.Diagnostics as IScoringDiagnostics)
                .DeduplicatePairs(entries);
        }
    }
}
