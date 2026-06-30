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

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// The Stage 5 Percolator diagnostic-dump gates, carried into the otherwise
    /// pure <see cref="PercolatorFdr.RunPercolator"/> on
    /// <see cref="PercolatorConfig.Diagnostics"/>. Each <c>Dump*</c> flag enables
    /// writing one byte-stable cross-impl bisection dump; the paired <c>*Only</c>
    /// flag asks <see cref="PercolatorFdr.RunPercolator"/> to stop after that dump
    /// by returning <see cref="PercolatorResults.DiagnosticAbort"/> -- the FDR
    /// engine never reads an env var and never exits the process. The Tasks-layer
    /// caller populates this from the run's <c>IOspreyDiagnostics</c> gate flags
    /// (the OSPREY_DUMP_STANDARDIZER / OSPREY_DUMP_PERC_INPUT / OSPREY_DUMP_SUBSAMPLE
    /// / OSPREY_DUMP_SVM_WEIGHTS family) and owns the early-exit decision.
    ///
    /// This lives in Osprey.FDR -- a small plain DTO rather than the
    /// <c>IOspreyDiagnostics</c> seam itself -- because the FDR project cannot
    /// reference the Diagnostics assembly that defines that interface (it would be
    /// a reference cycle: IOspreyDiagnostics names FDR domain types). Null on
    /// <see cref="PercolatorConfig.Diagnostics"/> means diagnostics off, the
    /// common case, so the dump call sites short-circuit on a single null check.
    /// </summary>
    public sealed class PercolatorDiagnosticsConfig
    {
        /// <summary>OSPREY_DUMP_STANDARDIZER: dump the fitted feature standardizer.</summary>
        public bool DumpStandardizer { get; set; }

        /// <summary>OSPREY_STANDARDIZER_ONLY: abort after the standardizer dump.</summary>
        public bool StandardizerOnly { get; set; }

        /// <summary>OSPREY_DUMP_PERC_INPUT: dump the raw per-entry feature vectors.</summary>
        public bool DumpPercInput { get; set; }

        /// <summary>OSPREY_PERC_INPUT_ONLY: abort after the Percolator-input dump.</summary>
        public bool PercInputOnly { get; set; }

        /// <summary>OSPREY_DUMP_SUBSAMPLE: dump subsample membership + fold assignment.</summary>
        public bool DumpSubsample { get; set; }

        /// <summary>OSPREY_SUBSAMPLE_ONLY: abort after the subsample dump.</summary>
        public bool SubsampleOnly { get; set; }

        /// <summary>OSPREY_DUMP_SVM_WEIGHTS: dump per-fold SVM weights, bias, iterations.</summary>
        public bool DumpSvmWeights { get; set; }

        /// <summary>OSPREY_SVM_WEIGHTS_ONLY: abort after the SVM-weights dump.</summary>
        public bool SvmWeightsOnly { get; set; }
    }
}
