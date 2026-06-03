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
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Tasks;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Oracle for the Phase B5 driver-loop flip: proves the per-task
    /// <see cref="OspreyTask.IsIncluded"/> membership predicate reproduces the
    /// pipeline run-set that the legacy <c>DeriveStartAtTask</c> /
    /// <c>DeriveStopAfterTask</c> range gating produces, across every HPC mode.
    /// IsIncluded is not yet wired into the driver (B4); this test pins the
    /// equivalence so B5 can replace the range walk with
    /// <c>CanonicalPipeline().Where(t =&gt; t.IsIncluded(ctx))</c> without
    /// changing which tasks run.
    ///
    /// One cell diverges by design: under the --input-scores full-pipeline mode
    /// (--join-at-pass=1 --input-scores, no other join modifier) the old range
    /// starts at PerFileScoring and so includes it, but PerFileScoring should
    /// NOT compute there -- it must lazy-rehydrate the supplied scores. The old
    /// range only "worked" because the pre-split monolithic Run dispatched to
    /// the join-only load internally; after the Run/Rehydrate split the range
    /// made the driver re-score from absent mzMLs (the mode-6 regression).
    /// IsIncluded corrects this, so the test asserts the divergence explicitly.
    /// </summary>
    [TestClass]
    public class PipelineMembershipTest
    {
        private static OspreyConfig Inputs()
        {
            return new OspreyConfig { InputScores = new List<string> { @"a.scores.parquet" } };
        }

        /// <summary>The six distinct (StartAt, StopAfter) pipeline modes.</summary>
        private static IEnumerable<(string Name, OspreyConfig Config)> Modes()
        {
            yield return (@"straight-through", new OspreyConfig());
            yield return (@"--no-join", new OspreyConfig { NoJoin = true });

            var joinOnly = Inputs(); joinOnly.StopAfterStage5 = true;
            yield return (@"--join-only", joinOnly);

            var rescore = Inputs(); rescore.NoJoin = true;
            yield return (@"rescore-worker", rescore);

            var merge = Inputs(); merge.ExpectReconciledInput = true;
            yield return (@"merge", merge);

            yield return (@"input-scores-full", Inputs());
        }

        private static int IndexOfType(OspreyTask[] tasks, Type type)
        {
            for (int i = 0; i < tasks.Length; i++)
                if (tasks[i].GetType() == type) return i;
            return -1;
        }

        [TestMethod]
        public void TestIsIncludedMatchesDeriveRange()
        {
            foreach (var mode in Modes())
            {
                var tasks = AnalysisPipeline.CanonicalPipeline();
                var ctx = new PipelineContext(mode.Config, tasks, null, null, null);

                int startIdx = IndexOfType(tasks, AnalysisPipeline.DeriveStartAtTask(mode.Config));
                int stopIdx = IndexOfType(tasks, AnalysisPipeline.DeriveStopAfterTask(mode.Config));
                Assert.IsTrue(startIdx >= 0 && stopIdx >= 0,
                    string.Format(@"{0}: DeriveStartAt/StopAfter not in CanonicalPipeline", mode.Name));

                for (int i = 0; i < tasks.Length; i++)
                {
                    var task = tasks[i];
                    bool inRange = startIdx <= i && i <= stopIdx;
                    bool included = task.IsIncluded(ctx);

                    bool knownDivergence = mode.Name == @"input-scores-full"
                                           && task is PerFileScoringTask;
                    if (knownDivergence)
                    {
                        Assert.IsFalse(included, string.Format(
                            @"{0}/{1}: PerFileScoring must be excluded (lazy-rehydrate the supplied scores)",
                            mode.Name, task.Name));
                        Assert.IsTrue(inRange, string.Format(
                            @"{0}/{1}: expected the legacy range to (wrongly) include it -- documents the corrected divergence",
                            mode.Name, task.Name));
                    }
                    else
                    {
                        Assert.AreEqual(inRange, included, string.Format(
                            @"{0}/{1}: IsIncluded ({2}) must match the DeriveStartAt..StopAfter range membership ({3})",
                            mode.Name, task.Name, included, inRange));
                    }
                }
            }
        }
    }
}
