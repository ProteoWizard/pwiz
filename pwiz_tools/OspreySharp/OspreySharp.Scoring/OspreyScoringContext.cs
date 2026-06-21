/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Shared per-candidate scoring context, mirroring Skyline's
    /// <c>PeakScoringContext</c>: it carries the machinery shared across a
    /// candidate's feature calculators plus a typed byproduct cache
    /// (<see cref="AddInfo{TInfo}"/> / <see cref="TryGetInfo{TInfo}"/>) so one
    /// producer can publish an intermediate (e.g. the reference-XIC selection) for
    /// its sibling calculators to read instead of recomputing it.
    ///
    /// The instance is reused across candidates within a file/window;
    /// <see cref="ClearByproducts"/> is called between candidates so the byproduct
    /// dictionary is not reallocated in the per-candidate hot loop. The machinery
    /// members (scorer, preprocessed-xcorr cache, calibration, ...) are added by
    /// the feature families that read them.
    /// </summary>
    public class OspreyScoringContext
    {
        private readonly Dictionary<Type, object> _byproducts = new Dictionary<Type, object>();

        public OspreyScoringContext(OspreyConfig config)
        {
            Config = config;
        }

        /// <summary>The live (post-calibration) configuration the pipeline scores with.</summary>
        public OspreyConfig Config { get; }

        /// <summary>
        /// Resolution strategy (Unit/HRAM) -- window-level. Drives the f64/f32 cache
        /// dispatch inside <see cref="IResolutionStrategy.ScoreXcorr"/>; must not be
        /// bypassed. Set once per window via <see cref="SetWindow"/>.
        /// </summary>
        public IResolutionStrategy Resolution { get; private set; }

        /// <summary>
        /// Per-window preprocessed XCorr cache (f64 doubles for Unit, f32 floats for
        /// HRAM), produced once before the candidate loop. Window-level.
        /// </summary>
        public WindowXcorrCache PreprocessedXcorr { get; private set; }

        /// <summary>
        /// Main-search spectral scorer (resolution-mode bin config) -- window-level.
        /// Not the unit-resolution calibration scorer.
        /// </summary>
        public SpectralScorer Scorer { get; private set; }

        /// <summary>
        /// Rented XCorr scratch / VisitedBins pool -- window-level. MUST be threaded
        /// into <see cref="IResolutionStrategy.ScoreXcorr"/> so HRAM scoring avoids
        /// per-call LOH allocation (perf-critical: the xcorr family is the perf gate).
        /// </summary>
        public XcorrScratchPool XcorrScratchPool { get; private set; }

        /// <summary>
        /// Set the per-window machinery the xcorr / Savitzky-Golay calculators read.
        /// Called once per window after construction, before the candidate loop.
        /// These are window-level and deliberately survive <see cref="ClearByproducts"/>
        /// (which only resets the per-candidate byproduct cache).
        /// </summary>
        public void SetWindow(IResolutionStrategy resolution, WindowXcorrCache preprocessedXcorr,
            SpectralScorer scorer, XcorrScratchPool xcorrScratchPool)
        {
            Resolution = resolution;
            PreprocessedXcorr = preprocessedXcorr;
            Scorer = scorer;
            XcorrScratchPool = xcorrScratchPool;
        }

        // NOTE: the per-window MS1 machinery (HasMs1Features / Ms1Spectra /
        // Ms1Calibration + SetMs1Machinery) was removed when MS1 production moved
        // upstream: the extractor now produces the MS1 precursor XIC + isotope
        // envelope and the ms1 features read them from the peak-data view, so the
        // context no longer carries MS1 state. The HRAM gate is the resolution
        // strategy (IResolutionStrategy.HasMs1Features), read by the extractor.

        /// <summary>
        /// Publish a byproduct keyed by its type for sibling calculators to read.
        /// Throws if one of this type was already published for the current
        /// candidate (mirrors Skyline's <c>AddInfo</c>); producers publish once,
        /// guarded by a <see cref="TryGetInfo{TInfo}"/> check.
        /// </summary>
        public void AddInfo<TInfo>(TInfo info)
        {
            _byproducts.Add(typeof(TInfo), info);
        }

        /// <summary>
        /// Get a byproduct published earlier during the current candidate's
        /// scoring. Returns false (and <paramref name="info"/> = default) when none
        /// was published, so callers can apply a family-specific default.
        /// </summary>
        public bool TryGetInfo<TInfo>(out TInfo info)
        {
            if (_byproducts.TryGetValue(typeof(TInfo), out var infoObj))
            {
                info = (TInfo) infoObj;
                return true;
            }
            info = default(TInfo);
            return false;
        }

        /// <summary>
        /// Reset the byproduct cache between candidates. The context instance is
        /// reused, so this is called once per candidate before its calculators run.
        /// </summary>
        public void ClearByproducts()
        {
            _byproducts.Clear();
        }
    }
}
