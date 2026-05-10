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

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// One step in the OspreySharp analysis pipeline. Each task owns a
    /// concrete piece of work — load spectra, calibrate, score,
    /// reconcile, rescore, write blib — that the <see cref="Pipeline"/>
    /// driver invokes in sequence.
    ///
    /// Phase A scope: Run() does the work, against state held on the
    /// shared <see cref="PipelineContext"/>. Inputs / Outputs / validity-
    /// key surfaces (Phase B resume semantics) are deliberately not
    /// declared yet — adding them before any concrete extractions
    /// would commit to an API shape we have not yet validated against
    /// the real per-stage data flow. They can be introduced in a
    /// follow-up phase without breaking Phase A subclasses.
    ///
    /// Task boundaries are aligned with the HPC fan-out / join
    /// transitions documented in
    /// <c>pwiz_tools/OspreySharp/Osprey-workflow.html</c>:
    /// <list type="bullet">
    ///   <item>per-file scoring (Stages 1-4) → first join</item>
    ///   <item>first-pass FDR + reconciliation planning (Stage 5)</item>
    ///   <item>per-file rescore + gap-fill (Stage 6) → second join</item>
    ///   <item>merge-node second-pass FDR + protein FDR + blib (Stage 7)</item>
    /// </list>
    /// </summary>
    public abstract class OspreyTask
    {
        /// <summary>
        /// Short identifier used in pipeline log lines. Conventionally
        /// PascalCase and matches the class name minus the <c>Task</c>
        /// suffix (e.g. "PerFileScoring").
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Execute this task against the shared pipeline context. May
        /// read upstream-task outputs from <paramref name="ctx"/> and
        /// must persist any outputs downstream tasks depend on back to
        /// <paramref name="ctx"/>.
        ///
        /// Returns <c>true</c> on success (pipeline continues to the
        /// next task) or <c>false</c> to short-circuit the pipeline
        /// driver. A task returning <c>false</c> is responsible for
        /// having already logged the reason; if the failure should
        /// produce a non-zero process exit code, the task must set
        /// <see cref="PipelineContext.ExitCode"/> before returning.
        /// Mirrors the boolean-return early-exit pattern in
        /// <c>pwiz_tools/Skyline/CommandLine.cs</c>.
        /// </summary>
        public abstract bool Run(PipelineContext ctx);
    }
}
