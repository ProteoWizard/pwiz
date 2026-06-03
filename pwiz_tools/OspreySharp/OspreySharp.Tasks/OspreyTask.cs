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

using System;
using System.Collections.Generic;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// One step in the OspreySharp analysis pipeline. Each task owns a
    /// concrete piece of work — load spectra, calibrate, score,
    /// reconcile, rescore, write blib — run in a pipeline.
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
    ///
    /// Phase B adds <see cref="Inputs"/> / <see cref="Outputs"/> /
    /// <see cref="ValidityKey"/> for the resume-on-restart capability:
    /// the pipeline driver checks each task's outputs against
    /// <c>.&lt;TaskName&gt;.osprey.task</c> sidecar validity keys and
    /// skips Run when every output exists with a matching key. The
    /// same skip-if-valid check applies to every CLI mode — cross-impl
    /// / worker-mode invocations (<c>--input-scores</c> /
    /// <c>--join-at-pass=*</c>) flow through the same driver, so
    /// validity-key match drives the decision rather than any CLI
    /// flag gate. (Cross-impl correctness on <c>--input-scores</c>
    /// parquets is enforced separately by the parquet
    /// <c>osprey.search_hash</c> footer metadata check.)
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

        /// <summary>
        /// File paths this task reads as inputs. Reported in the
        /// <c>.osprey.task</c> sidecar so a human inspecting a
        /// completed-output sidecar can see what the task consumed.
        /// Default empty; tasks override to list their input files
        /// (mzML, library, upstream sidecars, etc.).
        /// </summary>
        public virtual IEnumerable<string> Inputs(PipelineContext ctx) => Array.Empty<string>();

        /// <summary>
        /// File paths this task produces as outputs. The driver checks
        /// these for existence and matching validity-key sidecars before
        /// running; if all exist and match, the task's
        /// <see cref="Run"/> is skipped. After a successful Run, the
        /// driver writes a <c>&lt;output&gt;.&lt;TaskName&gt;.osprey.task</c>
        /// sidecar next to each output file. The per-task naming lets
        /// two tasks that produce the same output path keep distinct
        /// validity records. (Historically PerFileScoring wrote the initial
        /// <c>.scores.parquet</c> and PerFileRescore overwrote it in place;
        /// Stage 6 now writes a separate <c>.scores-reconciled.parquet</c>, so
        /// they no longer share an output -- the per-task naming remains for any
        /// future same-path producers.)
        ///
        /// A task that returns no Outputs cannot be skipped; the driver
        /// always invokes <see cref="Run"/> for it. Use that posture
        /// for tasks whose work isn't durably represented on disk
        /// (purely-in-memory transformations).
        /// </summary>
        public virtual IEnumerable<string> Outputs(PipelineContext ctx) => Array.Empty<string>();

        /// <summary>
        /// Identifier that distinguishes "outputs from a different
        /// invocation that happens to share these paths" from "outputs
        /// from this invocation that I should reuse." Written into each
        /// output's <c>.osprey.task</c> sidecar after Run; checked on
        /// the next invocation before deciding whether to skip Run.
        ///
        /// Default includes <see cref="SearchIdentity.SearchParameterHash"/>
        /// and <see cref="SearchIdentity.LibraryIdentityHash"/> — the
        /// two hashes that already participate in the parquet-metadata
        /// integrity check downstream. Tasks with extra per-task state
        /// that affects their output (e.g. <see cref="SearchIdentity.ReconciliationParameterHash"/>
        /// for the rescore task) override and append.
        /// </summary>
        public virtual string ValidityKey(PipelineContext ctx) => string.Format(
            @"search={0};library={1}",
            ctx.Config.Identity.SearchParameterHash(),
            ctx.Config.Identity.LibraryIdentityHash());
    }
}
