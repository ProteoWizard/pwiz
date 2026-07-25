/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for <see cref="CwtCandidateCodec"/>. The encoded byte layout
    /// must be byte-identical to Rust's
    /// <c>osprey/crates/osprey/src/pipeline.rs</c> CWT-candidate write path
    /// (4-byte little-endian count followed by N x 48 bytes of little-endian
    /// f64 fields in order: apex_rt, start_rt, end_rt, area, snr,
    /// coelution_score). Stage 6 reconciliation depends on this round-trip
    /// for cross-impl byte parity of the .scores.parquet cwt_candidates
    /// column.
    /// </summary>
    [TestClass]
    public class CwtCandidateCodecTest
    {
        [TestMethod]
        public void TestEncodeNullReturnsNull()
        {
            Assert.IsNull(CwtCandidateCodec.Encode(null));
        }

        [TestMethod]
        public void TestEncodeEmptyHasOnlyCountPrefix()
        {
            var bytes = CwtCandidateCodec.Encode(new List<CwtCandidate>());
            Assert.IsNotNull(bytes);
            Assert.AreEqual(CwtCandidateCodec.COUNT_PREFIX_BYTES, bytes.Length);
            Assert.AreEqual(0u, System.BitConverter.ToUInt32(bytes, 0));
        }

        [TestMethod]
        public void TestDecodeEmptyOrShortInputReturnsEmpty()
        {
            Assert.AreEqual(0, CwtCandidateCodec.Decode(null).Count);
            Assert.AreEqual(0, CwtCandidateCodec.Decode(new byte[0]).Count);
            Assert.AreEqual(0, CwtCandidateCodec.Decode(new byte[3]).Count);
        }

        [TestMethod]
        public void TestRoundTripPreservesEveryBit()
        {
            var input = new List<CwtCandidate>
            {
                new CwtCandidate
                {
                    ApexRt = 12.345,
                    StartRt = 12.0,
                    EndRt = 12.7,
                    Area = 1.234e6,
                    Snr = 50.5,
                    CoelutionScore = 7.812345,
                },
                new CwtCandidate
                {
                    ApexRt = 0.0,
                    StartRt = -1.0,
                    EndRt = 1.0,
                    Area = 0.0,
                    Snr = 0.0,
                    CoelutionScore = 0.0,
                },
                // Tie-break boundary value to lock the byte-exact round-trip
                // through BitConverter (this is the same value that surfaced
                // as a ryu/R formatter ambiguity in the Stage 5 percolator
                // dump).
                new CwtCandidate
                {
                    ApexRt = 0.097751617431640625,
                    StartRt = 0.5,
                    EndRt = 1.5,
                    Area = 1.0,
                    Snr = 1.0,
                    CoelutionScore = -1.5,
                },
            };

            var bytes = CwtCandidateCodec.Encode(input);
            int expectedLen = CwtCandidateCodec.COUNT_PREFIX_BYTES
                              + input.Count * CwtCandidateCodec.BYTES_PER_CANDIDATE;
            Assert.AreEqual(expectedLen, bytes.Length);

            var roundTripped = CwtCandidateCodec.Decode(bytes);
            Assert.AreEqual(input.Count, roundTripped.Count);
            for (int i = 0; i < input.Count; i++)
            {
                AssertBitEqual(input[i].ApexRt, roundTripped[i].ApexRt, "ApexRt[" + i + "]");
                AssertBitEqual(input[i].StartRt, roundTripped[i].StartRt, "StartRt[" + i + "]");
                AssertBitEqual(input[i].EndRt, roundTripped[i].EndRt, "EndRt[" + i + "]");
                AssertBitEqual(input[i].Area, roundTripped[i].Area, "Area[" + i + "]");
                AssertBitEqual(input[i].Snr, roundTripped[i].Snr, "Snr[" + i + "]");
                AssertBitEqual(input[i].CoelutionScore, roundTripped[i].CoelutionScore,
                    "CoelutionScore[" + i + "]");
            }
        }

        [TestMethod]
        public void TestEncodeMatchesRustByteLayout()
        {
            // Single-candidate fixture chosen so the byte pattern is easy to
            // read: 4-byte LE count = 1, then six LE f64s. Values pinned to
            // simple constants so a Rust-side test could assert the same
            // bytes if needed.
            var input = new List<CwtCandidate>
            {
                new CwtCandidate
                {
                    ApexRt = 1.0,
                    StartRt = 2.0,
                    EndRt = 3.0,
                    Area = 4.0,
                    Snr = 5.0,
                    CoelutionScore = 6.0,
                },
            };
            var bytes = CwtCandidateCodec.Encode(input);
            Assert.AreEqual(4 + 48, bytes.Length);
            Assert.AreEqual(1u, System.BitConverter.ToUInt32(bytes, 0));
            // Each f64 emitted in little-endian; verify by re-reading.
            Assert.AreEqual(1.0, System.BitConverter.ToDouble(bytes, 4));
            Assert.AreEqual(2.0, System.BitConverter.ToDouble(bytes, 12));
            Assert.AreEqual(3.0, System.BitConverter.ToDouble(bytes, 20));
            Assert.AreEqual(4.0, System.BitConverter.ToDouble(bytes, 28));
            Assert.AreEqual(5.0, System.BitConverter.ToDouble(bytes, 36));
            Assert.AreEqual(6.0, System.BitConverter.ToDouble(bytes, 44));
        }

        [TestMethod]
        public void TestDecodeTolerantToTruncatedTail()
        {
            // The Rust loader uses `chunks_exact(48).take(count)`, which
            // silently caps at the available data. Mirror that tolerance:
            // a header claiming 5 candidates with bytes for only 2 should
            // decode to 2, not throw.
            var twoCandidates = new List<CwtCandidate>
            {
                new CwtCandidate { ApexRt = 1.0, StartRt = 2.0, EndRt = 3.0, Area = 4.0, Snr = 5.0, CoelutionScore = 6.0 },
                new CwtCandidate { ApexRt = 7.0, StartRt = 8.0, EndRt = 9.0, Area = 10.0, Snr = 11.0, CoelutionScore = 12.0 },
            };
            var bytes = CwtCandidateCodec.Encode(twoCandidates);
            // Overwrite the count prefix to claim 5 candidates.
            System.BitConverter.GetBytes(5u).CopyTo(bytes, 0);
            var decoded = CwtCandidateCodec.Decode(bytes);
            Assert.AreEqual(2, decoded.Count);
            Assert.AreEqual(7.0, decoded[1].ApexRt);
        }

        /// <summary>
        /// Round-trip CWT candidates through the real parquet writer and
        /// reader: write scored entries carrying known candidate lists,
        /// then read them back with
        /// <see cref="ParquetScoreCache.LoadCwtCandidatesFromParquet"/> and
        /// assert every field survives bit-exactly and each row keeps its
        /// own list. This gates the codec's wiring into the cwt_candidates
        /// parquet column (the encode/decode pair plus the per-row list
        /// boundaries) with no dependency on external run output, so it
        /// runs everywhere the rest of the suite does.
        /// </summary>
        [TestMethod]
        public void TestCwtCandidatesRoundTripThroughParquet()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                @"osprey_cwt_parquet_" + System.Guid.NewGuid().ToString(@"N"));
            System.IO.Directory.CreateDirectory(dir);
            try
            {
                // Ids out of order so the write's canonical (entry_id, charge,
                // scan_number) sort runs, and a deliberate mix of list lengths
                // (two, zero, one) so a per-row boundary mismap surfaces as a
                // count mismatch rather than a silent pass.
                var entries = new List<FdrEntry>
                {
                    MakeEntry(3,
                        NewCandidate(31.5, 31.0, 32.0, 1.5e6, 42.5, 8.25),
                        // Boundary value that locks the byte-exact path through
                        // BitConverter (the ryu/R formatter ambiguity from the
                        // Stage 5 percolator dump).
                        NewCandidate(0.097751617431640625, 0.5, 1.5, 1.0, 1.0, -1.5)),
                    MakeEntry(1),
                    MakeEntry(2, NewCandidate(20.25, 20.0, 20.5, 7.5e5, 13.0, 3.125)),
                };

                string path = System.IO.Path.Combine(dir, @"cwt.scores.parquet");
                ParquetScoreCache.WriteScoresParquet(path, entries, null, null, @"f.mzML");

                // Rows come back in the canonical entry_id order the write
                // imposed: 1 (none), 2 (one), 3 (two).
                var loaded = ParquetScoreCache.LoadCwtCandidatesFromParquet(path);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(entries.Count, loaded.Count, @"row count");
                Assert.AreEqual(0, loaded[0].Count, @"entry 1 candidate count");
                Assert.AreEqual(1, loaded[1].Count, @"entry 2 candidate count");
                Assert.AreEqual(2, loaded[2].Count, @"entry 3 candidate count");

                AssertCandidateBitEqual(entries[2].CwtCandidates[0], loaded[1][0], @"entry2[0]");
                AssertCandidateBitEqual(entries[0].CwtCandidates[0], loaded[2][0], @"entry3[0]");
                AssertCandidateBitEqual(entries[0].CwtCandidates[1], loaded[2][1], @"entry3[1]");
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }

        private static CwtCandidate NewCandidate(double apexRt, double startRt, double endRt,
            double area, double snr, double coelutionScore)
        {
            return new CwtCandidate
            {
                ApexRt = apexRt,
                StartRt = startRt,
                EndRt = endRt,
                Area = area,
                Snr = snr,
                CoelutionScore = coelutionScore,
            };
        }

        private static FdrEntry MakeEntry(uint entryId, params CwtCandidate[] candidates)
        {
            return new FdrEntry
            {
                EntryId = entryId,
                Charge = 2,
                ScanNumber = entryId * 10,
                ApexRt = entryId + 0.5,
                StartRt = entryId + 0.1,
                EndRt = entryId + 0.9,
                ModifiedSequence = @"PEPTIDE" + entryId,
                Features = new double[ParquetScoreCache.NUM_PIN_FEATURES],
                CwtCandidates = new List<CwtCandidate>(candidates),
            };
        }

        private static void AssertCandidateBitEqual(CwtCandidate expected, CwtCandidate actual,
            string label)
        {
            AssertBitEqual(expected.ApexRt, actual.ApexRt, label + @" ApexRt");
            AssertBitEqual(expected.StartRt, actual.StartRt, label + @" StartRt");
            AssertBitEqual(expected.EndRt, actual.EndRt, label + @" EndRt");
            AssertBitEqual(expected.Area, actual.Area, label + @" Area");
            AssertBitEqual(expected.Snr, actual.Snr, label + @" Snr");
            AssertBitEqual(expected.CoelutionScore, actual.CoelutionScore,
                label + @" CoelutionScore");
        }

        private static void AssertBitEqual(double expected, double actual, string label)
        {
            long expBits = System.BitConverter.DoubleToInt64Bits(expected);
            long actBits = System.BitConverter.DoubleToInt64Bits(actual);
            Assert.AreEqual(expBits, actBits, label + " bit mismatch");
        }
    }
}
