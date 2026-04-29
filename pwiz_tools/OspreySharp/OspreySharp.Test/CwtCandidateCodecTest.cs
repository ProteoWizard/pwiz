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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Test
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
        /// End-to-end validation: read a real Rust-written .scores.parquet and
        /// assert <see cref="pwiz.OspreySharp.IO.ParquetScoreCache.LoadCwtCandidatesFromParquet"/>
        /// returns sensible CWT data. Skipped automatically when the
        /// reference parquet is not present (so the test stays portable),
        /// but locally validates byte-level decode compatibility against
        /// the current Rust write path.
        /// </summary>
        [TestMethod]
        public void TestLoadCwtCandidatesFromRustParquet()
        {
            const string path = @"D:\test\osprey-runs\astral\_stage6_planning\Astral\" +
                                @"Ast-2024-12-05_HeLa_3mzDIA_6mIIT_400-900_49.scores.parquet";
            if (!System.IO.File.Exists(path))
            {
                Assert.Inconclusive(@"Reference parquet not present: " + path);
                return;
            }
            var allCandidates = ParquetScoreCache.LoadCwtCandidatesFromParquet(path);
            Assert.IsNotNull(allCandidates);
            Assert.IsTrue(allCandidates.Count > 0, @"Expected at least one parquet row");
            int nonEmpty = 0;
            int totalCandidates = 0;
            int candidatesWithSensibleApex = 0;
            foreach (var lst in allCandidates)
            {
                if (lst.Count == 0)
                    continue;
                nonEmpty++;
                totalCandidates += lst.Count;
                foreach (var c in lst)
                {
                    // Astral RT range for this dataset is roughly 0..120 min.
                    // A sensible decode produces apex RTs in that range; a
                    // wrong byte order (e.g. big-endian f64 read as little)
                    // produces values around 1e308 from the bit pattern.
                    if (c.ApexRt > 0 && c.ApexRt < 200)
                        candidatesWithSensibleApex++;
                    // Boundary ordering invariant: start <= apex <= end.
                    Assert.IsTrue(c.StartRt <= c.ApexRt + 1e-6,
                        @"start_rt > apex_rt: " + c.StartRt + @" > " + c.ApexRt);
                    Assert.IsTrue(c.ApexRt <= c.EndRt + 1e-6,
                        @"apex_rt > end_rt: " + c.ApexRt + @" > " + c.EndRt);
                }
            }
            Assert.IsTrue(nonEmpty > 0, @"Expected at least one entry with CWT candidates");
            Assert.IsTrue(totalCandidates > 0, @"Expected at least one decoded candidate");
            // If decoding succeeded, the vast majority of apex values
            // should be in the dataset's measured RT range. Use 95% to
            // tolerate the rare candidate with an extreme value.
            double sensibleFraction = (double)candidatesWithSensibleApex / totalCandidates;
            Assert.IsTrue(sensibleFraction > 0.95,
                string.Format(@"Only {0}/{1} candidates have apex_rt in [0, 200] min",
                    candidatesWithSensibleApex, totalCandidates));
        }

        private static void AssertBitEqual(double expected, double actual, string label)
        {
            long expBits = System.BitConverter.DoubleToInt64Bits(expected);
            long actBits = System.BitConverter.DoubleToInt64Bits(actual);
            Assert.AreEqual(expBits, actBits, label + " bit mismatch");
        }
    }
}
