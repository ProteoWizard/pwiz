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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for <see cref="MultiChargeConsensus.SelectRescoreTargets"/>, the
    /// intra-file multi-charge consensus leader selection whose tie-break must match
    /// Rust select_post_fdr_consensus (pipeline.rs:7665-7679).
    /// </summary>
    [TestClass]
    public class MultiChargeConsensusTest
    {
        private const double FDR = 0.01;

        /// <summary>
        /// #5 parity: among charge states tied on BOTH SVM score AND
        /// run_precursor_qvalue, the LAST entry (in ascending entry order) becomes the
        /// consensus leader, matching Rust max_by, which returns the last of equal
        /// maxima (pipeline.rs:7668-7674). C# previously kept the FIRST (a strict
        /// less-than q-value tie-break), so on an exact tie it chose a different leader,
        /// a different consensus window, and a different rescore-target set.
        ///
        /// FAILS on revert (restoring the strict less-than): the leader would be entry
        /// 0, so the target's Index and window would flip to entry 1 / entry 0's window.
        /// </summary>
        [TestMethod]
        public void TestConsensusLeaderTieBreakPrefersLastEntry()
        {
            // Two charge states of one peptide, tied on score AND q-value, at peaks
            // far enough apart (|10 - 20| = 10 > tol 0.5) that the loser is rescored.
            var entries = new List<FdrEntry>
            {
                new FdrEntry
                {
                    ModifiedSequence = "PEPTIDEK", Charge = 2, Score = 5.0,
                    RunPrecursorQvalue = 0.005, ApexRt = 10.0, StartRt = 9.5, EndRt = 10.5
                },
                new FdrEntry
                {
                    ModifiedSequence = "PEPTIDEK", Charge = 3, Score = 5.0,
                    RunPrecursorQvalue = 0.005, ApexRt = 20.0, StartRt = 19.5, EndRt = 20.5
                }
            };

            var targets = MultiChargeConsensus.SelectRescoreTargets(entries, FDR);

            // Leader is entry 1 (last on the full tie); entry 0 is the rescore target,
            // pinned to entry 1's consensus window.
            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(0, targets[0].Index);
            Assert.AreEqual(20.0, targets[0].Apex, 1e-9);
            Assert.AreEqual(19.5, targets[0].Start, 1e-9);
            Assert.AreEqual(20.5, targets[0].End, 1e-9);
        }

        /// <summary>
        /// Cross-impl determinism for shared peak boundaries: when two charge states
        /// of one peptide in a file are BOTH gap-filled at run q=1.0 (a tie) with
        /// DIFFERENT windows, the shared (modseq, file) boundary must resolve to the
        /// LOWEST-CHARGE window regardless of entry order. Otherwise C# (in-memory
        /// per-file entry order) and Rust (parquet row order) keep different charges,
        /// diverging the blib RetentionTimes start/end (the Astral transfer-compete
        /// 20-row divergence). Rust build_shared_boundaries_from_plan applies the
        /// identical (lower run_qvalue, then lower charge) rule.
        /// </summary>
        [TestMethod]
        public void TestSharedBoundariesTieBrokenByLowestCharge()
        {
            const string seq = "SVDEVFDEVVQIFDK";
            var passing = new HashSet<(string, byte)> { (seq, 2), (seq, 3) };

            FdrEntry Mk(byte charge, double start, double end) => new FdrEntry
            {
                ModifiedSequence = seq, Charge = charge,
                RunPrecursorQvalue = 1.0, RunPeptideQvalue = 1.0,
                ApexRt = (start + end) / 2.0, StartRt = start, EndRt = end
            };

            foreach (var swap in new[] { false, true })
            {
                var z2 = Mk(2, 22.925, 23.850); // wide window
                var z3 = Mk(3, 23.665, 23.854); // narrow window
                var list = swap
                    ? new List<FdrEntry> { z3, z2 }
                    : new List<FdrEntry> { z2, z3 };
                var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
                {
                    new KeyValuePair<string, List<FdrEntry>>("file1", list)
                };

                var shared = MergeNodeTask.BuildSharedBoundaries(perFile, passing);

                Assert.IsTrue(shared.TryGetValue((seq, "file1"), out var b),
                    "shared boundary must exist for the peptide/file");
                Assert.AreEqual(22.925, b[1], 1e-9,
                    "lowest charge (z=2) start must win regardless of order (swap=" + swap + ")");
                Assert.AreEqual(23.850, b[2], 1e-9,
                    "lowest charge (z=2) end must win regardless of order (swap=" + swap + ")");
            }
        }

        /// <summary>
        /// The score tie-break still prefers the strictly-lower q-value regardless of
        /// order: a later entry with a HIGHER q-value must NOT displace an earlier
        /// lower-q leader (guards that widening the q-value comparison to
        /// less-than-or-equal did not collapse the comparison itself).
        /// </summary>
        [TestMethod]
        public void TestConsensusLeaderPrefersLowerQvalueOnScoreTie()
        {
            var entries = new List<FdrEntry>
            {
                new FdrEntry
                {
                    ModifiedSequence = "PEPTIDEK", Charge = 2, Score = 5.0,
                    RunPrecursorQvalue = 0.001, ApexRt = 10.0, StartRt = 9.5, EndRt = 10.5
                },
                new FdrEntry
                {
                    ModifiedSequence = "PEPTIDEK", Charge = 3, Score = 5.0,
                    RunPrecursorQvalue = 0.008, ApexRt = 20.0, StartRt = 19.5, EndRt = 20.5
                }
            };

            var targets = MultiChargeConsensus.SelectRescoreTargets(entries, FDR);

            // Leader is entry 0 (lower q-value wins the score tie); entry 1 is rescored
            // onto entry 0's window.
            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(1, targets[0].Index);
            Assert.AreEqual(10.0, targets[0].Apex, 1e-9);
            Assert.AreEqual(9.5, targets[0].Start, 1e-9);
            Assert.AreEqual(10.5, targets[0].End, 1e-9);
        }
    }
}
