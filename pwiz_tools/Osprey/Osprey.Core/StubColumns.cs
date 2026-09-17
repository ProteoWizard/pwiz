/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Which OPTIONAL scalar columns a <c>ParquetScoreCache.ReadFdrStubScalars</c> walk should
    /// decode. (Named here in Osprey.Core rather than beside that reader because the streaming
    /// score path in Osprey.FDR carries this choice through its row-source delegate, and
    /// Osprey.FDR deliberately does not reference Osprey.IO.) The five identity/score columns the first pass always needs - entry_id,
    /// charge, is_decoy, coelution_sum, modified_sequence - are <see cref="Core"/> and are never
    /// optional; everything here is a column some callers consume and others only pay for.
    ///
    /// <para>This exists because the columns are not free. The parquet is Zstd-compressed, so an
    /// extra column per row group is a decompress, a page decode and a fresh large-object array,
    /// not 8 bytes per row of sequential IO. On the 446-run CHS cohort one column measured ~7% of
    /// every pass that walks these scalars, and the streaming first pass walks them three times
    /// per file while only one of those walks writes the sidecar.</para>
    /// </summary>
    [Flags]
    public enum StubColumns
    {
        /// <summary>The five columns every caller of the scalar walk needs.</summary>
        Core = 0,

        /// <summary>
        /// <c>apex_rt</c>, the detection apex retention time. Wanted by the pass that writes the
        /// per-file FDR sidecar (format v7) and by
        /// <c>ParquetScoreCache.ReadApexRtsByParquetIndex</c>; nothing else reads it.
        /// </summary>
        ApexRt = 1,
    }
}
