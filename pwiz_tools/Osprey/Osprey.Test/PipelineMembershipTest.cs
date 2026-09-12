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
                // EXACTLY Program.cs's assignment, which is the only one in the tree:
                // `config.StopAfterStage5 = selectedTask == HpcTask.FirstPassFdr;`. This
                // helper also named ModelDiagnostics, building a config the CLI cannot
                // produce - so the row below asserted a membership no real run has, while
                // ProgramTests pinned the real flags and stated the opposite design. Two
                // tests in one assembly asserting incompatible things is worse than either
                // being wrong alone, because whichever you read first looks corroborated.
                StopAfterStage5 = task == HpcTask.FirstPassFdr,
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
                // --task ModelDiagnostics is a RENDER over retained products, and it reaches
                // AnalysisPipeline with all three membership flags FALSE - it sets none of
                // them (see ForTask, and Program.cs's single StopAfterStage5 assignment). So
                // it is in every task, exactly like the straight-through run, and suppresses
                // artifact writes rather than membership. The row here used to read
                // {true,true,false,false}, which was the shape of a config the CLI cannot
                // build; ProgramTests.cs pins the real flags and now agrees with this.
                (@"ModelDiagnostics",  ForTask(HpcTask.ModelDiagnostics),
                    new[] { true,  true,  true,  true  }),
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

        /// <summary>
        /// Only a process that RUNS Stage 7's join may be admitted to the streamed one.
        ///
        /// <para>The case that matters is <c>--task FirstPassFDR</c>, and it is not
        /// hypothetical: re-run over a directory a previous analysis COMPLETED, every
        /// disk-side term of <c>CanStreamStage7Join</c> is satisfied by that previous run's
        /// own output. <c>PerFileScoringTask</c> would then take its per-run-join branch,
        /// publish one EMPTY list per run for a fold that never comes, and FirstPassFDR would
        /// compute its pass over nothing - rewriting both boundary sidecars and the retained
        /// base_id summary as empty, exit 0.</para>
        ///
        /// <para>Asserted with the switch passed as TRUE and against the two-argument form, so
        /// this pins the membership term alone and cannot pass merely because the environment
        /// happens to have streaming off.</para>
        /// </summary>
        [TestMethod]
        public void TestOnlyStage7JoinTasksAdmitTheStreamedJoin()
        {
            var admitted = new[] { HpcTask.SecondPassFdr, HpcTask.ModelDiagnostics };
            foreach (HpcTask task in Enum.GetValues(typeof(HpcTask)))
            {
                bool expected = admitted.Contains(task);
                Assert.AreEqual(expected, ScoringTaskShared.RunsStage7Join(ForTask(task)),
                    string.Format(@"--task {0}: RunsStage7Join must be {1}", task, expected));
                // A task that does not run the join must be refused BEFORE any disk term,
                // which is what makes the refusal free and unconditional.
                if (!expected)
                {
                    Assert.IsFalse(
                        ScoringTaskShared.Stage7StreamAdmittedBeforeRescore(ForTask(task), true),
                        string.Format(@"--task {0} must not be admitted to the streamed join", task));
                }
            }
            // The straight-through pipeline runs every stage, so it is admitted.
            Assert.IsTrue(ScoringTaskShared.RunsStage7Join(new OspreyConfig()));
        }
    }
}
