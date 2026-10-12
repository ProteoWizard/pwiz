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

using System;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Per-run, driver-owned pipeline state — distinct from the parsed
    /// <c>OspreyConfig</c> (the configuration). Holds values the pipeline
    /// computes at run time that are NOT configuration and are NOT part of
    /// any cache / identity hash. Lives on <see cref="PipelineContext.RunPlan"/>.
    ///
    /// Splitting run-time state out of <c>OspreyConfig</c> lets the config
    /// be the immutable, hash-affecting parsed inputs while this carries the
    /// mutable per-run decisions. (First field moved: file parallelism;
    /// more run-time state migrates here as the config is frozen.)
    /// </summary>
    public sealed class RunPlan
    {
        /// <summary>
        /// How many files the scoring stage runs concurrently in the current
        /// invocation (<see cref="FileStage.Scoring"/>). Computed once by
        /// <c>PerFileScoringTask</c> before the per-file <c>ProcessFile()</c> calls
        /// and read to divide the inner main-search thread budget so total thread
        /// demand stays near core count. Defaults to 1 (no scaling). Does NOT feed
        /// any <c>SearchIdentity</c> hash.
        /// </summary>
        public int ScoringFileParallelism { get; set; } = 1;

        /// <summary>
        /// How many files the re-scoring stage runs concurrently (<see cref="FileStage.Rescoring"/>),
        /// resolved by <c>PerFileRescoreTask</c> on its own rather than inherited from scoring:
        /// its per-file working set is a small fraction of the scoring stage's, so the two do
        /// not want the same count. Defaults to 1. Does NOT feed any <c>SearchIdentity</c> hash.
        /// </summary>
        public int RescoringFileParallelism { get; set; } = 1;

        /// <summary>
        /// How many files the rescore and second-pass stages work on at once in their per-file
        /// phases: <see cref="RescoringFileParallelism"/>, never below one. Those phases apply
        /// every cross-file effect in file order (<see cref="OrderedFileLanes"/>), so the count
        /// changes the wall clock and nothing else.
        /// First-pass FDR has its own count, <see cref="FirstPassFdrLanes"/>.
        /// </summary>
        public int FileLanes => Math.Max(1, RescoringFileParallelism);

        /// <summary>
        /// How many files first-pass FDR and its Stage 6 planning work on at once, chosen by
        /// <see cref="FdrLaneResolver"/> from <c>--threads</c> and free memory once the task
        /// knows its largest file's row count. One until then.
        /// </summary>
        public int FirstPassFdrLanes { get; set; } = 1;
    }
}
