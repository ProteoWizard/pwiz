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
using System.Buffers.Binary;
using System.Collections.Generic;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// A peak candidate identified by continuous wavelet transform.
    /// Maps to osprey-core/src/types.rs CwtCandidate.
    /// </summary>
    public struct CwtCandidate
    {
        public double ApexRt { get; set; }
        public double StartRt { get; set; }
        public double EndRt { get; set; }
        public double Area { get; set; }
        public double Snr { get; set; }
        public double CoelutionScore { get; set; }
    }

    /// <summary>
    /// Binary encoding for the per-entry CWT candidate list stored in the
    /// <c>cwt_candidates</c> column of <c>{stem}.scores.parquet</c>. Mirrors
    /// the Rust layout in <c>osprey/crates/osprey/src/pipeline.rs</c>:
    /// 4 little-endian bytes for the candidate count, then for each
    /// candidate 6 little-endian f64 fields in the order
    /// <c>apex_rt, start_rt, end_rt, area, snr, coelution_score</c>
    /// (48 bytes per candidate). Cross-impl byte parity required.
    /// </summary>
    public static class CwtCandidateCodec
    {
        public const int BYTES_PER_CANDIDATE = 48;
        public const int COUNT_PREFIX_BYTES = 4;

        /// <summary>Encode a list of CWT candidates to the Rust binary
        /// layout. Returns null when <paramref name="candidates"/> is null
        /// (matches Rust's missing-column semantics in the parquet write path).
        /// Writes directly into a single preallocated buffer via
        /// <see cref="BinaryPrimitives"/> -- no per-field allocations and
        /// little-endian is enforced explicitly (independent of host
        /// endianness).</summary>
        public static byte[] Encode(IReadOnlyList<CwtCandidate> candidates)
        {
            if (candidates == null)
                return null;
            int n = candidates.Count;
            var buf = new byte[COUNT_PREFIX_BYTES + n * BYTES_PER_CANDIDATE];
            BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)n);
            int offset = COUNT_PREFIX_BYTES;
            for (int i = 0; i < n; i++)
            {
                var c = candidates[i];
                WriteF64(buf, ref offset, c.ApexRt);
                WriteF64(buf, ref offset, c.StartRt);
                WriteF64(buf, ref offset, c.EndRt);
                WriteF64(buf, ref offset, c.Area);
                WriteF64(buf, ref offset, c.Snr);
                WriteF64(buf, ref offset, c.CoelutionScore);
            }
            return buf;
        }

        /// <summary>Decode a CWT candidate list from the Rust binary layout.
        /// Returns an empty list for null/short input, mirroring the Rust
        /// loader's tolerance for cells that were written before this column
        /// was populated. Reads via <see cref="BinaryPrimitives"/> so the
        /// little-endian byte order matches the writer regardless of host
        /// endianness.</summary>
        public static List<CwtCandidate> Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < COUNT_PREFIX_BYTES)
                return new List<CwtCandidate>();
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
            int available = (bytes.Length - COUNT_PREFIX_BYTES) / BYTES_PER_CANDIDATE;
            int n = (int)Math.Min(count, (uint)available);
            var result = new List<CwtCandidate>(n);
            int offset = COUNT_PREFIX_BYTES;
            for (int i = 0; i < n; i++)
            {
                result.Add(new CwtCandidate
                {
                    ApexRt = ReadF64(bytes, offset),
                    StartRt = ReadF64(bytes, offset + 8),
                    EndRt = ReadF64(bytes, offset + 16),
                    Area = ReadF64(bytes, offset + 24),
                    Snr = ReadF64(bytes, offset + 32),
                    CoelutionScore = ReadF64(bytes, offset + 40),
                });
                offset += BYTES_PER_CANDIDATE;
            }
            return result;
        }

        private static void WriteF64(byte[] buf, ref int offset, double v)
        {
            BinaryPrimitives.WriteInt64LittleEndian(
                new Span<byte>(buf, offset, 8),
                BitConverter.DoubleToInt64Bits(v));
            offset += 8;
        }

        private static double ReadF64(byte[] buf, int offset)
        {
            return BitConverter.Int64BitsToDouble(
                BinaryPrimitives.ReadInt64LittleEndian(new Span<byte>(buf, offset, 8)));
        }
    }
}
