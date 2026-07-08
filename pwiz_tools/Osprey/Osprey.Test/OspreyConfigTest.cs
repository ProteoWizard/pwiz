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

// Tests for Osprey.Core.OspreyConfig.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    [TestClass]
    public class OspreyConfigTest
    {
        private const double TOLERANCE = 1e-9;

        /// <summary>
        /// Guards the "always-on protein-FDR threshold" semantics that decoupled
        /// the second Percolator pass + protein-FDR machinery + compaction
        /// protein-rescue from the presence of <c>--protein-fdr</c>. To match Rust
        /// osprey (where <c>config.protein_fdr</c> is a plain f64, default 0.01, and
        /// that machinery runs unconditionally), <see cref="OspreyConfig.ProteinFdr"/>
        /// is now only the threshold: when unset, <see cref="OspreyConfig.EffectiveProteinFdr"/>
        /// falls back to <see cref="OspreyConfig.DefaultProteinFdr"/> (0.01) rather
        /// than gating the machinery off.
        ///
        /// This test FAILS if a future revert reintroduces a null-gated threshold
        /// (e.g. drops the <c>?? DefaultProteinFdr</c> fallback, changes the 0.01
        /// default, or removes the constant), which is exactly how the second pass
        /// would silently stop running on analyses without <c>--protein-fdr</c>.
        /// </summary>
        [TestMethod]
        public void TestEffectiveProteinFdr()
        {
            // (c) The compile-time default is 0.01, matching Rust config.protein_fdr.
            Assert.AreEqual(0.01, OspreyConfig.DefaultProteinFdr, TOLERANCE);

            // (a) No --protein-fdr supplied: ProteinFdr is null, but the effective
            //     threshold still resolves to the default so the protein-FDR
            //     machinery (second pass, picked-protein FDR, compaction rescue)
            //     runs unconditionally at 0.01.
            var config = new OspreyConfig();
            Assert.IsNull(config.ProteinFdr);
            Assert.AreEqual(OspreyConfig.DefaultProteinFdr, config.EffectiveProteinFdr, TOLERANCE);
            Assert.AreEqual(0.01, config.EffectiveProteinFdr, TOLERANCE);

            // (b) --protein-fdr 0.05 sets only the threshold; the explicit value wins.
            config.ProteinFdr = 0.05;
            Assert.AreEqual(0.05, config.EffectiveProteinFdr, TOLERANCE);
        }

        // NOTE: A direct compaction protein-rescue test (a non-decoy entry whose
        // RunPeptideQvalue is above config.RunFdr but whose RunProteinQvalue <= 0.01
        // still survives first-pass compaction) is intentionally NOT added here.
        // The predicate lives in FirstJoinTask.CompactFirstPass, a private instance
        // method that consumes a PipelineContext (DI container + logging) and either
        // a full RescoreInputs bundle or a constructed per-file entry list; there is
        // no pure/public seam exposing the
        // "RunPeptideQvalue <= RunFdr || RunProteinQvalue <= EffectiveProteinFdr"
        // gate without that heavy setup. TestEffectiveProteinFdr above guards the
        // exact value (0.01) that CompactFirstPass reads as its always-on
        // proteinGate, so the always-on threshold semantics are covered at the
        // config level.
    }
}
