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

using System.Collections.Generic;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Per-file scoring context carrying immutable inputs shared across the
    /// pipeline. Created once per file at the top of ProcessFile.
    /// </summary>
    public class ScoringContext
    {
        public OspreyConfig Config { get; }
        public IResolutionStrategy Resolution { get; }
        public string FileName { get; }

        /// <summary>
        /// Pool of per-spectrum XCorr scratch buffers reused across the
        /// main search. Lazily created by the pipeline right before Stage 4
        /// (<see cref="EnsureXcorrScratchPool"/>); null during calibration.
        /// Keeping a single pool per scoring run lets gen-2 hold onto the
        /// 100K-bin HRAM arrays instead of triggering LOH churn per scan.
        /// </summary>
        public XcorrScratchPool XcorrScratchPool { get; private set; }

        /// <summary>
        /// Stage 6 boundary overrides keyed by library entry id. When the
        /// dictionary contains an entry's id, the search engine skips CWT
        /// peak detection + the signal pre-filter and scores at the supplied
        /// (apex, start, end) RT triple. Null during the first-pass main
        /// search; populated only on the post-FDR re-scoring pass for
        /// multi-charge consensus, cross-run reconciliation, and gap-fill.
        /// Maps to <c>boundary_overrides</c> in
        /// <c>osprey/crates/osprey/src/pipeline.rs run_search</c>.
        /// </summary>
        public IReadOnlyDictionary<uint, (double Apex, double Start, double End)> BoundaryOverrides { get; set; }

        public ScoringContext(OspreyConfig config, string fileName)
        {
            Config = config;
            FileName = fileName;
            Resolution = ResolutionStrategy.Create(config.ResolutionMode);
        }

        public XcorrScratchPool EnsureXcorrScratchPool(int nBins)
        {
            if (XcorrScratchPool == null || XcorrScratchPool.NBins != nBins)
                XcorrScratchPool = new XcorrScratchPool(nBins);
            return XcorrScratchPool;
        }
    }
}
