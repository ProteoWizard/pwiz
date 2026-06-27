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
        private static OspreyConfig WithInputScores(Action<OspreyConfig> set)
        {
            var config = new OspreyConfig { InputScores = new List<string> { @"a.scores.parquet" } };
            set(config);
            return config;
        }

        [TestMethod]
        public void TestIsIncludedMembershipTable()
        {
            // Expected membership per mode, in CanonicalPipeline order
            // [PerFileScoring, FirstJoin, PerFileRescore, MergeNode].
            var cases = new (string Name, OspreyConfig Config, bool[] Expected)[]
            {
                (@"straight-through",  new OspreyConfig(),
                    new[] { true,  true,  true,  true  }),
                (@"PerFileScoring",    new OspreyConfig { NoJoin = true },
                    new[] { true,  false, false, false }),
                (@"FirstPassFDR",      WithInputScores(c => c.StopAfterStage5 = true),
                    new[] { false, true,  false, false }),
                (@"PerFileRescoring",  WithInputScores(c => c.NoJoin = true),
                    new[] { false, false, true,  false }),
                (@"SecondPassFDR",     WithInputScores(c => c.ExpectReconciledInput = true),
                    new[] { false, false, false, true  }),
                // --input-scores with no --task: the single-node full pipeline.
                // PerFileScoring lazy-rehydrates the supplied scores rather than
                // computing them, so it is excluded; FirstJoin..MergeNode compute
                // Stages 5-8.
                (@"input-scores-full", WithInputScores(_ => { }),
                    new[] { false, true,  true,  true  }),
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
