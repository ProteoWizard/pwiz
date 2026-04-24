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
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.FDR.Reconciliation
{
    /// <summary>
    /// Refits per-file RT calibration using consensus peptides. Ports
    /// <c>refit_calibration_with_consensus</c> in
    /// <c>osprey/crates/osprey/src/reconciliation.rs</c>.
    /// </summary>
    public static class CalibrationRefit
    {
        /// <summary>Minimum consensus points required before attempting a refit.</summary>
        public const int MIN_CONSENSUS_POINTS = 20;

        /// <summary>
        /// Builds (consensus_library_rt, measured_apex_rt) pairs for target
        /// peptides in this run that pass the experiment-level FDR at
        /// <paramref name="consensusFdr"/>, then fits a LOESS calibration.
        /// Returns null when fewer than <see cref="MIN_CONSENSUS_POINTS"/>
        /// pairs are available or the fit fails.
        /// </summary>
        public static RTCalibration Refit(
            IReadOnlyList<PeptideConsensusRT> consensus,
            IReadOnlyList<FdrEntry> entries,
            double consensusFdr)
        {
            if (consensus == null)
                throw new ArgumentNullException(nameof(consensus));
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            // Consensus lookup: target peptide sequence → consensus library RT.
            var consensusMap = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var c in consensus)
            {
                if (c.IsDecoy)
                    continue;
                consensusMap[c.ModifiedSequence] = c.ConsensusLibraryRt;
            }

            var libraryRts = new List<double>();
            var measuredRts = new List<double>();
            foreach (var entry in entries)
            {
                if (entry.IsDecoy)
                    continue;
                if (entry.EffectiveExperimentQvalue(FdrLevel.Both) > consensusFdr)
                    continue;
                if (!consensusMap.TryGetValue(entry.ModifiedSequence, out var consensusLibRt))
                    continue;
                libraryRts.Add(consensusLibRt);
                measuredRts.Add(entry.ApexRt);
            }

            if (libraryRts.Count < MIN_CONSENSUS_POINTS)
                return null;

            // Disable outlier removal (retention = 1.0) — these are
            // FDR-controlled detections, not noisy initial matches. LOESS
            // robustness iterations still downweight any stragglers.
            var config = new RTCalibratorConfig
            {
                Bandwidth = 0.3,
                OutlierRetention = 1.0,
                MinPoints = MIN_CONSENSUS_POINTS,
            };

            try
            {
                return new RTCalibrator(config).Fit(libraryRts.ToArray(), measuredRts.ToArray());
            }
            catch (ArgumentException)
            {
                // Insufficient points or mismatched arrays; treat as a
                // recoverable "fit not possible" consistent with Rust's
                // Option::None return.
                return null;
            }
        }
    }
}
