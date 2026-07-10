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

// Tests for Osprey.FDR.Reconciliation.GapFillTargetIdentifier and the
// isolation-window m/z carrier it consumes.
// Ports the isolation-filter behavior of identify_gap_fill_targets in
// osprey/crates/osprey/src/reconciliation.rs.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;

namespace pwiz.Osprey.Test
{
    [TestClass]
    public class GapFillTargetIdentifierTest
    {
        private const double TOLERANCE = 1e-9;

        // A single passing precursor P: modified sequence + charge that is present
        // in FILE_A and absent from FILE_B, so it is a gap-fill candidate for
        // FILE_B. Its library precursor m/z is PRECURSOR_MZ.
        private const string MODSEQ = @"PEPTIDEK";
        private const byte CHARGE = 2;
        private const uint TARGET_ID = 100u;
        private const string FILE_A = @"fileA";
        private const string FILE_B = @"fileB";
        private const double PRECURSOR_MZ = 500.0;

        #region Isolation-window m/z filter

        /// <summary>
        /// The core #2 regression guard: the per-file isolation-window m/z filter
        /// must actually exclude a gap-fill candidate whose library precursor m/z
        /// falls outside every window in the file it would be gap-filled into. The
        /// bug was that the pipeline fed <c>null</c> for <c>perFileIsolationMz</c>,
        /// so the filter never fired and out-of-window precursors were gap-filled
        /// anyway (wrong for GPF datasets with disjoint m/z ranges). This test
        /// exercises the parameter directly:
        ///
        ///  - a window NOT covering P's m/z produces NO gap-fill target for P;
        ///  - a window covering P's m/z DOES produce one;
        ///  - a null filter (feature disabled) also produces one.
        ///
        /// It FAILS if the filter is bypassed (window argument ignored / treated as
        /// null again): the out-of-window case would then wrongly emit a target.
        /// </summary>
        [TestMethod]
        public void TestIsolationWindowFilterExcludesOutOfWindowCandidate()
        {
            // Window that does NOT cover P's m/z (500) -> P filtered out of FILE_B.
            var outside = new Dictionary<string, IReadOnlyList<(double Lo, double Hi)>>
            {
                { FILE_B, new[] { (600.0, 610.0) } },
            };
            var noTarget = RunIdentify(PRECURSOR_MZ, outside);
            Assert.IsFalse(noTarget.ContainsKey(FILE_B),
                @"out-of-window precursor must not be gap-filled into FILE_B");
            Assert.AreEqual(0, noTarget.Count,
                @"no file should have gap-fill targets when the only candidate is out of window");

            // Window that DOES cover P's m/z -> P gap-filled into FILE_B.
            var inside = new Dictionary<string, IReadOnlyList<(double Lo, double Hi)>>
            {
                { FILE_B, new[] { (495.0, 505.0) } },
            };
            var withTarget = RunIdentify(PRECURSOR_MZ, inside);
            AssertSingleTargetForFileB(withTarget);

            // Null filter (feature disabled) -> P gap-filled into FILE_B.
            var noFilter = RunIdentify(PRECURSOR_MZ, null);
            AssertSingleTargetForFileB(noFilter);
        }

        /// <summary>
        /// Guards the interval derivation the plumbing applies to each isolation
        /// window, <c>(center, width) -&gt; (center - width/2, center + width/2)</c>,
        /// together with the filter's half-open <c>[Lo, Hi)</c> containment test
        /// (<c>precursor_mz &gt;= Lo &amp;&amp; precursor_mz &lt; Hi</c>, matching Rust
        /// reconciliation.rs:954-956). A precursor exactly at the window center or
        /// its low edge is in range; one exactly at the high edge is OUT (exclusive
        /// upper bound); one just below the low edge is out.
        ///
        /// FAILS if the filter is bypassed (every case would produce a target) or if
        /// the half-open bound is loosened to inclusive (the high-edge case would
        /// wrongly produce a target).
        /// </summary>
        [TestMethod]
        public void TestIsolationIntervalDerivationHalfOpenSemantics()
        {
            // Window expressed as (center, width); derive the [Lo, Hi) interval the
            // Stage 6 filter consumes exactly as PerFileScoringTask does.
            const double center = 500.0;
            const double width = 10.0;
            const double lo = center - width / 2.0; // 495.0
            const double hi = center + width / 2.0; // 505.0

            var window = new Dictionary<string, IReadOnlyList<(double Lo, double Hi)>>
            {
                { FILE_B, new[] { (lo, hi) } },
            };

            // At the center -> inside.
            Assert.IsTrue(RunIdentify(center, window).ContainsKey(FILE_B),
                @"precursor at window center must be in range");
            // At the low edge -> inside (inclusive lower bound).
            Assert.IsTrue(RunIdentify(lo, window).ContainsKey(FILE_B),
                @"precursor at the low edge must be in range (Lo is inclusive)");
            // At the high edge -> OUTSIDE (exclusive upper bound).
            Assert.IsFalse(RunIdentify(hi, window).ContainsKey(FILE_B),
                @"precursor at the high edge must be out of range (Hi is exclusive)");
            // Just below the low edge -> outside.
            Assert.IsFalse(RunIdentify(lo - 0.001, window).ContainsKey(FILE_B),
                @"precursor below the low edge must be out of range");
        }

        #endregion

        #region Isolation-scheme JSON carrier

        /// <summary>
        /// Guards the HPC carrier half of fix #2: the <c>[center, width]</c> window
        /// pairs on <see cref="IsolationSchemeJson.Windows"/> (a <c>double[][]</c>
        /// on <see cref="CalibrationParams"/>'s metadata) must survive the
        /// calibration.json serialize -&gt; deserialize round-trip so a merge node
        /// with no mzML can rebuild the per-file gap-fill m/z filter. Uses the same
        /// Newtonsoft serializer + <c>[JsonProperty("windows")]</c> mapping the
        /// product uses.
        ///
        /// FAILS if the <c>windows</c> field / its JSON mapping is dropped (the
        /// carrier revert): the round-tripped <c>Windows</c> would be null.
        /// </summary>
        [TestMethod]
        public void TestIsolationSchemeWindowsJsonRoundTrip()
        {
            var cal = CalibrationParams.Uncalibrated();
            cal.Metadata.IsolationScheme = new IsolationSchemeJson
            {
                NumWindows = 2,
                MzMin = 500.0,
                MzMax = 520.0,
                TypicalWidth = 10.0,
                UniformWidth = true,
                Windows = new[]
                {
                    new[] { 500.0, 10.0 },
                    new[] { 520.0, 12.0 },
                },
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(cal,
                Newtonsoft.Json.Formatting.Indented);
            Assert.IsTrue(json.Contains(@"""windows"""),
                @"serialized calibration.json must carry the isolation-window pairs");

            var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<CalibrationParams>(json);
            var windows = loaded.Metadata.IsolationScheme.Windows;
            Assert.IsNotNull(windows, @"windows must survive the round-trip");
            Assert.AreEqual(2, windows.Length);
            Assert.AreEqual(500.0, windows[0][0], TOLERANCE);
            Assert.AreEqual(10.0, windows[0][1], TOLERANCE);
            Assert.AreEqual(520.0, windows[1][0], TOLERANCE);
            Assert.AreEqual(12.0, windows[1][1], TOLERANCE);
        }

        #endregion

        #region Fixture helpers

        /// <summary>
        /// Run <see cref="GapFillTargetIdentifier.Identify"/> for the canonical
        /// scenario: passing precursor P present in FILE_A, absent from FILE_B (so P
        /// is a gap-fill candidate for FILE_B), with P's library precursor m/z set to
        /// <paramref name="precursorMz"/> and the supplied per-file isolation windows.
        /// </summary>
        private static IReadOnlyDictionary<string, IReadOnlyList<GapFillTarget>> RunIdentify(
            double precursorMz,
            IReadOnlyDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz)
        {
            var cal = IdentityCalibration();

            var consensus = new[]
            {
                new PeptideConsensusRT
                {
                    ModifiedSequence = MODSEQ, IsDecoy = false,
                    ConsensusLibraryRt = 10.0, MedianPeakWidth = 0.6,
                    NRunsDetected = 3, ApexLibraryRtMad = 0.02,
                },
            };

            // FILE_A has the passing precursor; FILE_B has none, so P is missing there.
            var perFileEntries = new List<KeyValuePair<string, IReadOnlyList<FdrEntry>>>
            {
                new KeyValuePair<string, IReadOnlyList<FdrEntry>>(
                    FILE_A, new[] { PassingTarget() }),
                new KeyValuePair<string, IReadOnlyList<FdrEntry>>(
                    FILE_B, Array.Empty<FdrEntry>()),
            };

            // Both files carry an (identity) calibration; the same populated map
            // serves as both the refined and original calibration inputs (Identify
            // prefers refined, falling back to original -- identical here).
            var cals = new Dictionary<string, RTCalibration>
            {
                { FILE_A, cal }, { FILE_B, cal },
            };

            var libLookup = new Dictionary<(string ModifiedSequence, byte Charge),
                (uint TargetEntryId, uint DecoyEntryId)>
            {
                { (MODSEQ, CHARGE), (TARGET_ID, TARGET_ID | 0x80000000u) },
            };
            var libPrecursorMz = new Dictionary<uint, double>
            {
                { TARGET_ID, precursorMz },
            };

            return GapFillTargetIdentifier.Identify(
                consensus, perFileEntries, cals, cals,
                experimentFdr: 0.01, libLookup, libPrecursorMz, perFileIsolationMz);
        }

        private static void AssertSingleTargetForFileB(
            IReadOnlyDictionary<string, IReadOnlyList<GapFillTarget>> result)
        {
            Assert.IsTrue(result.ContainsKey(FILE_B),
                @"in-window candidate must be gap-filled into FILE_B");
            Assert.AreEqual(1, result[FILE_B].Count);
            Assert.AreEqual(MODSEQ, result[FILE_B][0].ModifiedSequence);
            Assert.AreEqual(CHARGE, result[FILE_B][0].Charge);
            Assert.AreEqual(TARGET_ID, result[FILE_B][0].TargetEntryId);
        }

        private static FdrEntry PassingTarget()
        {
            // RunPrecursorQvalue 0 clears the eligibility gate (min of the four
            // q-values <= experimentFdr); all other q-values default to 1.0.
            return new FdrEntry
            {
                EntryId = TARGET_ID,
                IsDecoy = false,
                Charge = CHARGE,
                ApexRt = 10.0,
                StartRt = 9.5,
                EndRt = 10.5,
                CoelutionSum = 1.0,
                Score = 3.0,
                RunPrecursorQvalue = 0.0,
                ModifiedSequence = MODSEQ,
            };
        }

        /// <summary>
        /// Identity LOESS calibration fit on 30 equally-spaced (x, x) points so
        /// Predict(x) = x for x in [0, 29]. Mirrors ReconciliationTest's fixture.
        /// </summary>
        private static RTCalibration IdentityCalibration()
        {
            int n = 30;
            var libRts = new double[n];
            var measRts = new double[n];
            for (int i = 0; i < n; i++)
            {
                libRts[i] = i;
                measRts[i] = i;
            }
            var config = new RTCalibratorConfig
            {
                Bandwidth = 0.3,
                Degree = 1,
                MinPoints = 5,
                RobustnessIterations = 0,
                OutlierRetention = 1.0,
            };
            return new RTCalibrator(config).Fit(libRts, measRts);
        }

        #endregion
    }
}
