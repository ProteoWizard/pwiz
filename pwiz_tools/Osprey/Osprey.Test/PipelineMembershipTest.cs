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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Pins the per-task <see cref="OspreyTask.IsIncluded"/> membership
    /// predicate -- the driver-owned dataflow's source of truth for which
    /// tasks run in each HPC mode -- against an explicit expected truth table.
    ///
    /// The table is the run-set the legacy
    /// <c>DeriveStartAtTask</c>/<c>DeriveStopAfterTask</c> range produced and
    /// that this test proved IsIncluded reproduced before the range gating was
    /// flipped (B4 oracle) and then removed (B6). It is kept as a permanent
    /// regression guard so a future edit to an IsIncluded override that breaks
    /// the membership of any mode fails here rather than silently mis-routing
    /// the pipeline.
    /// </summary>
    [TestClass]
    public class PipelineMembershipTest
    {
        /// <summary>
        /// One task's config, built the way <c>Program.Main</c> builds it: the task, and the
        /// three membership flags DERIVED from it. Nothing else - which is the change these
        /// rows record. Each row used to carry an input KIND too (a parquet list standing for
        /// <c>--input-scores</c>), and every predicate read both; the kind is gone and the
        /// expected memberships below are unchanged, which is the claim worth pinning.
        /// </summary>
        private static OspreyConfig ForTask(HpcTask task)
        {
            return new OspreyConfig
            {
                SelectedTask = task,
                NoJoin = task == HpcTask.PerFileScoring || task == HpcTask.PerFileRescore,
                StopAfterStage5 = task == HpcTask.FirstPassFdr || task == HpcTask.ModelDiagnostics,
                ExpectReconciledInput = task == HpcTask.SecondPassFdr,
            };
        }

        [TestMethod]
        public void TestIsIncludedMembershipTable()
        {
            // Expected membership per mode, in CanonicalPipeline order
            // [PerFileScoring, FirstPassFDR, PerFileRescore, SecondPassFDR].
            var cases = new (string Name, OspreyConfig Config, bool[] Expected)[]
            {
                (@"straight-through",  new OspreyConfig(),
                    new[] { true,  true,  true,  true  }),
                (@"PerFileScoring",    ForTask(HpcTask.PerFileScoring),
                    new[] { true,  false, false, false }),
                (@"FirstPassFDR",      ForTask(HpcTask.FirstPassFdr),
                    new[] { false, true,  false, false }),
                (@"PerFileRescoring",  ForTask(HpcTask.PerFileRescore),
                    new[] { false, false, true,  false }),
                (@"SecondPassFDR",     ForTask(HpcTask.SecondPassFdr),
                    new[] { false, false, false, true  }),
                // --task ModelDiagnostics is a RENDER over retained products, not a stage.
                // It needs the per-file load (so PerFileScoring is in) and first-pass state
                // (so FirstPassFDR is), and nothing after: a diagnostics fold publishes
                // neither CompactedEntries nor a second pass, and the two tasks that demand
                // them used to join anyway and fail the run AFTER writing the report it was
                // asked for. The row that stood here was `input-scores-full` - the
                // single-node full pipeline started from parquets - and it retired with the
                // flag; this is the mode that was actually at risk.
                (@"ModelDiagnostics",  ForTask(HpcTask.ModelDiagnostics),
                    new[] { true,  true,  false, false }),
            };

            foreach (var c in cases)
            {
                var tasks = AnalysisPipeline.CanonicalPipeline();
                var ctx = new PipelineContext(c.Config, tasks, null, null, null);
                Assert.AreEqual(tasks.Length, c.Expected.Length,
                    string.Format(@"{0}: expected-row length must match task count", c.Name));

                for (int i = 0; i < tasks.Length; i++)
                {
                    Assert.AreEqual(c.Expected[i], tasks[i].IsIncluded(ctx), string.Format(
                        @"{0}/{1}: IsIncluded must be {2}", c.Name, tasks[i].Name, c.Expected[i]));
                }
            }
        }
    }
}
