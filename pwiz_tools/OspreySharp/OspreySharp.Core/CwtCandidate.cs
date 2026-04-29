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
        /// (matches Rust's missing-column semantics in the parquet write path).</summary>
        public static byte[] Encode(IReadOnlyList<CwtCandidate> candidates)
        {
            if (candidates == null)
                return null;
            int n = candidates.Count;
            var buf = new byte[COUNT_PREFIX_BYTES + n * BYTES_PER_CANDIDATE];
            BitConverter.GetBytes((uint)n).CopyTo(buf, 0);
            int offset = COUNT_PREFIX_BYTES;
            for (int i = 0; i < n; i++)
            {
                var c = candidates[i];
                BitConverter.GetBytes(c.ApexRt).CopyTo(buf, offset); offset += 8;
                BitConverter.GetBytes(c.StartRt).CopyTo(buf, offset); offset += 8;
                BitConverter.GetBytes(c.EndRt).CopyTo(buf, offset); offset += 8;
                BitConverter.GetBytes(c.Area).CopyTo(buf, offset); offset += 8;
                BitConverter.GetBytes(c.Snr).CopyTo(buf, offset); offset += 8;
                BitConverter.GetBytes(c.CoelutionScore).CopyTo(buf, offset); offset += 8;
            }
            return buf;
        }

        /// <summary>Decode a CWT candidate list from the Rust binary layout.
        /// Returns an empty list for null/short input, mirroring the Rust
        /// loader's tolerance for cells that were written before this column
        /// was populated.</summary>
        public static List<CwtCandidate> Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < COUNT_PREFIX_BYTES)
                return new List<CwtCandidate>();
            uint count = BitConverter.ToUInt32(bytes, 0);
            int available = (bytes.Length - COUNT_PREFIX_BYTES) / BYTES_PER_CANDIDATE;
            int n = (int)Math.Min(count, (uint)available);
            var result = new List<CwtCandidate>(n);
            int offset = COUNT_PREFIX_BYTES;
            for (int i = 0; i < n; i++)
            {
                result.Add(new CwtCandidate
                {
                    ApexRt = BitConverter.ToDouble(bytes, offset),
                    StartRt = BitConverter.ToDouble(bytes, offset + 8),
                    EndRt = BitConverter.ToDouble(bytes, offset + 16),
                    Area = BitConverter.ToDouble(bytes, offset + 24),
                    Snr = BitConverter.ToDouble(bytes, offset + 32),
                    CoelutionScore = BitConverter.ToDouble(bytes, offset + 40),
                });
                offset += BYTES_PER_CANDIDATE;
            }
            return result;
        }
    }
}
