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
    /// OSPREY_DUMP_* / OSPREY_DIAG_* env var is set, or OSPREY_DIAGNOSTICS is set);
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
        /// Select the live diagnostics sink. Call once at pipeline entry, before
        /// constructing the <c>PipelineContext</c> that <see cref="Active"/> is
        /// injected into. When <paramref name="forceDumps"/> is <c>true</c> (the
        /// OSPREY_DIAGNOSTICS variable) the structured-dump env vars are turned on in-process
        /// first, so the sink picks them up the same way an external env var
        /// would. Otherwise the sink is created only if a diagnostic env var is
        /// already set, preserving existing env-var-driven bisection workflows.
        /// When nothing is enabled the sink stays <c>null</c>.
        /// </summary>
        public static void Initialize(bool forceDumps)
        {
            if (forceDumps)
            {
                foreach (var envVar in OspreyFileDiagnostics.FORCED_DUMP_BUNDLE)
                {
                    // Force-on: override "0" / unset / anything except already-"1".
                    if (envVar.Value != @"1")
                        envVar.SetInProcess(@"1");
                }
            }
            var sink = new OspreyFileDiagnostics();
            s_sink = sink.AnyEnabled ? sink : null;
            if (s_sink != null)
            {
                // Guarantees CloseAll runs on a normal Environment.Exit, not just a return
                // from Main - the held-open dump writers (cs_stage6_mp_inputs.tsv and
                // siblings) commit through SimpleFileSaver, so without this an early *_ONLY exit
                // (there are two dozen call sites) would leave their content sitting in an
                // uncommitted temp forever rather than at the real path a developer expects.
                // Does not run on a hard crash (AccessViolationException, a killed process) -
                // no cleanup mechanism does; that content is recoverable, if at all, only via
                // OspreyEnvironment.KeepFailedWrites naming the abandoned temp.
                // Closes the sink this call created, not whatever s_sink holds at exit: a later
                // Initialize with diagnostics off sets s_sink to null, and a handler reading the
                // field then threw NullReferenceException at process exit (every test host run).
                AppDomain.CurrentDomain.ProcessExit += (_, _) => sink.CloseAll();
            }
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
