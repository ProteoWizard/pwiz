/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey
{
    /// <summary>
    /// Bootstrap for the cross-implementation bisection diagnostics sink. The
    /// driver calls <see cref="Initialize"/> once at pipeline entry to select the
    /// live <see cref="OspreyFileDiagnostics"/> sink (created only when a
    /// OSPREY_DUMP_* / OSPREY_DIAG_* env var is set, or <c>-d</c> was passed);
    /// otherwise <see cref="s_sink"/> is <c>null</c> and <see cref="Active"/> is
    /// <c>null</c>, so a production run carries no diagnostic state.
    ///
    /// Tasks no longer reach a static dump surface here: they emit dumps through
    /// the injected <c>ctx.Diagnostics?.X</c> (an <see cref="IOspreyDiagnostics"/>,
    /// supplied by <see cref="Active"/>). The stateless logging / formatting /
    /// abort helpers moved to <see cref="OspreyDiagnosticsLog"/> so the task
    /// layer never references this exe-only bootstrap.
    /// </summary>
    public static class OspreyDiagnostics
    {
        // The live sink, or null when diagnostics are off (the no-op default).
        private static OspreyFileDiagnostics s_sink;

        /// <summary>
        /// OSPREY_DUMP_* env vars turned on by the <c>-d</c> master switch.
        /// Excludes the per-call firehose (OSPREY_DUMP_MP_INPUTS), the disabled
        /// predict-rt dump, the *_ONLY early-exit gates, and the OSPREY_DIAG_*
        /// per-entry selectors (which need specific IDs). Fine-grained control
        /// is still available by setting individual env vars.
        /// </summary>
        private static readonly string[] s_forcedDumpBundle =
        {
            @"OSPREY_DUMP_CAL_SAMPLE",
            @"OSPREY_DUMP_CAL_WINDOWS",
            @"OSPREY_DUMP_CAL_MATCH",
            @"OSPREY_DUMP_MS2_CAL_ERRORS",
            @"OSPREY_DUMP_LDA_SCORES",
            @"OSPREY_DUMP_LOESS_INPUT",
            @"OSPREY_DUMP_PERCOLATOR",
            @"OSPREY_DUMP_RESCORED",
            @"OSPREY_DUMP_CWT_PATH",
            @"OSPREY_DUMP_CONSENSUS",
            @"OSPREY_DUMP_MULTICHARGE",
            @"OSPREY_DUMP_REFIT",
            @"OSPREY_DUMP_RECONCILIATION",
            @"OSPREY_DUMP_CALIBRATION",
            @"OSPREY_DUMP_INV_PREDICT",
            @"OSPREY_DUMP_PROTEIN_FDR",
            @"OSPREY_DUMP_STAGE7_PROTEIN_FDR",
            @"OSPREY_DUMP_DETECTED_PEPTIDES",
            @"OSPREY_DUMP_LOESS_FIT",
        };

        /// <summary>
        /// Select the live diagnostics sink. Call once at pipeline entry, before
        /// constructing the <c>PipelineContext</c> that <see cref="Active"/> is
        /// injected into. When <paramref name="forceDumps"/> is <c>true</c> (the
        /// <c>-d</c> flag) the structured-dump env vars are turned on in-process
        /// first, so the sink picks them up the same way an external env var
        /// would. Otherwise the sink is created only if a diagnostic env var is
        /// already set, preserving existing env-var-driven bisection workflows.
        /// When nothing is enabled the sink stays <c>null</c>.
        /// </summary>
        public static void Initialize(bool forceDumps)
        {
            if (forceDumps)
            {
                foreach (string envVar in s_forcedDumpBundle)
                {
                    // Force-on: override "0" / unset / anything except already-"1".
                    if (Environment.GetEnvironmentVariable(envVar) != @"1")
                        Environment.SetEnvironmentVariable(envVar, @"1");
                }
            }
            var sink = new OspreyFileDiagnostics();
            s_sink = sink.AnyEnabled ? sink : null;
        }

        /// <summary>
        /// The active diagnostics sink as the full <see cref="IOspreyDiagnostics"/>,
        /// or <c>null</c> when diagnostics are off. The driver injects this into
        /// <c>PipelineContext</c> so tasks emit dumps through the context (via the
        /// <c>?.</c> null-conditional operator). Returns <c>null</c> before
        /// <see cref="Initialize"/> has run.
        /// </summary>
        public static IOspreyDiagnostics Active => s_sink;
    }
}
