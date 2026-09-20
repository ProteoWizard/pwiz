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
using System.Linq;
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
        [TestMethod]
        public void TestIsIncludedMembershipTable()
        {
            // Expected membership per mode, in canonical pipeline order
            // [PerFileScoring, FirstPassFDR, PerFileRescore, SecondPassFDR]. Each config
            // comes from TaskConfigs.ForTask over the SAME task list the pipeline is built
            // from, the way a run does it - the rescore worker asks whether it IS the
            // selection by reference, so a namesake from a second list would not count.
            // The rows used to carry an input KIND too (a parquet list standing for
            // --input-scores), and every predicate read both; the kind is gone and the
            // expected memberships are unchanged, which is the claim worth pinning.
            var cases = new (string Name, bool[] Expected)[]
            {
                (null,                           new[] { true,  true,  true,  true  }),
                (PerFileScoringTask.TASK_NAME,   new[] { true,  false, false, false }),
                (FirstPassFdrTask.TASK_NAME,     new[] { false, true,  false, false }),
                (PerFileRescoreTask.TASK_NAME,   new[] { false, false, true,  false }),
                (SecondPassFdrTask.TASK_NAME,    new[] { false, false, false, true  }),
                // --task ModelDiagnostics is a RENDER over retained products, and it reaches
                // AnalysisPipeline with all three membership flags FALSE - its ApplySelection
                // sets none of them. So it is in every task, exactly like the
                // straight-through run, and suppresses artifact writes rather than
                // membership. The row here used to read {true,true,false,false}, which was
                // the shape of a config the CLI cannot build; ProgramTests.cs pins the real
                // flags and agrees with this.
                (ModelDiagnosticsTask.TASK_NAME, new[] { true,  true,  true,  true  }),
            };

            // Every task that walks the canonical pipeline has a row, so a task added later
            // cannot leave its membership unpinned; the standalone task has none to pin.
            foreach (var task in OspreyTasks.CreateAll().Where(t => t.RunsCanonicalPipeline))
                Assert.IsTrue(cases.Any(c => c.Name == task.Name), task.Name + @" has no membership row");

            foreach (var c in cases)
            {
                string caseName = c.Name ?? @"straight-through";
                var allTasks = OspreyTasks.CreateAll();
                var config = c.Name == null ? new OspreyConfig() : TaskConfigs.ForTask(allTasks, c.Name);
                var tasks = OspreyTasks.PipelineFor(allTasks, config.SelectedTask);
                var ctx = new PipelineContext(config, tasks, null, null, null);
                Assert.AreEqual(tasks.Length, c.Expected.Length,
                    string.Format(@"{0}: expected-row length must match task count", caseName));

                for (int i = 0; i < tasks.Length; i++)
                {
                    Assert.AreEqual(c.Expected[i], tasks[i].IsIncluded(ctx), string.Format(
                        @"{0}/{1}: IsIncluded must be {2}", caseName, tasks[i].Name, c.Expected[i]));
                }
            }
        }

        /// <summary>
        /// The pipeline a selection builds: SpectraCache alone runs as a one-task pipeline of
        /// its own and is included there; every other selection, and no selection, walks the
        /// four canonical stages in execution order; a task that declares neither is refused.
        /// Pinned because the shape used to be two hand-written lists chosen by an enum test,
        /// and is now derived from the tasks' own <c>RunsStandalone</c> /
        /// <c>RunsCanonicalPipeline</c> facts.
        /// </summary>
        [TestMethod]
        public void TestPipelineForSelection()
        {
            var canonical = new[]
            {
                PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME,
                PerFileRescoreTask.TASK_NAME, SecondPassFdrTask.TASK_NAME
            };
            var allTasks = OspreyTasks.CreateAll();
            CollectionAssert.AreEqual(canonical,
                OspreyTasks.PipelineFor(allTasks, null).Select(t => t.Name).ToArray());
            foreach (var task in allTasks)
            {
                var config = TaskConfigs.ForTask(allTasks, task.Name);
                var pipeline = OspreyTasks.PipelineFor(allTasks, config.SelectedTask);
                if (task.RunsStandalone)
                {
                    Assert.AreEqual(1, pipeline.Length, task.Name);
                    Assert.AreSame(task, pipeline[0], task.Name);
                    Assert.IsTrue(task.IsIncluded(new PipelineContext(config, pipeline, null, null, null)),
                        string.Format(@"{0} must be included in its own pipeline", task.Name));
                }
                else
                {
                    Assert.IsTrue(task.RunsCanonicalPipeline, task.Name + @" must declare a pipeline");
                    CollectionAssert.AreEqual(canonical, pipeline.Select(t => t.Name).ToArray(), task.Name);
                }
                // The instances are shared, not namesakes: the canonical pipeline's members
                // are the list's members.
                foreach (var member in pipeline)
                    Assert.IsTrue(allTasks.Contains(member), member.Name);
            }
            Assert.AreEqual(SpectraCacheTask.TASK_NAME,
                allTasks.Single(t => t.RunsStandalone).Name, @"SpectraCache is the only standalone task");
            // A selection from a DIFFERENT list is refused rather than silently building a
            // pipeline whose reference checks all fail.
            var stranger = OspreyTasks.FindByName(OspreyTasks.CreateAll(), SpectraCacheTask.TASK_NAME);
            Assert.ThrowsException<ArgumentException>(() => OspreyTasks.PipelineFor(allTasks, stranger));
            // A listed task that declares no pipeline - the fail-closed defaults - is refused
            // too, rather than running the whole analysis with itself never called.
            var unlisted = new UnlistedTask();
            var withUnlisted = allTasks.Concat(new[] { unlisted }).ToArray();
            var selectsUnlisted = new OspreyConfig();
            selectsUnlisted.SelectTask(unlisted);
            Assert.ThrowsException<InvalidOperationException>(
                () => OspreyTasks.PipelineFor(withUnlisted, selectsUnlisted.SelectedTask));
        }

        /// <summary>
        /// The per-task facts every former enum switch now asks of the task, as one truth
        /// table. Each column was a separate <c>switch (config.SelectedTask)</c> in a
        /// separate consumer; a task added later answers false to all of them until its
        /// author says otherwise (the base-class defaults), which is what the last row pins.
        /// </summary>
        [TestMethod]
        public void TestSelectedTaskFacts()
        {
            // Columns: HydratesPerRun, StartsAfterPerFileScoring, ReadsReconciledScores,
            // RunsStage7Join, IsPerFileWorker, InCanonicalPipeline, RunsCanonicalPipeline,
            // RunsStandalone.
            var expected = new (string Name, bool HydratesPerRun, bool StartsAfter, bool ReadsReconciled,
                bool Stage7Join, bool PerFileWorker, bool InCanonical, bool RunsCanonical, bool Standalone)[]
            {
                (SpectraCacheTask.TASK_NAME,     false, false, false, false, true,  false, false, true),
                (PerFileScoringTask.TASK_NAME,   false, false, false, false, true,  true,  true,  false),
                (FirstPassFdrTask.TASK_NAME,     false, true,  false, false, false, true,  true,  false),
                (PerFileRescoreTask.TASK_NAME,   true,  true,  false, false, true,  true,  true,  false),
                (SecondPassFdrTask.TASK_NAME,    false, true,  true,  true,  false, true,  true,  false),
                (ModelDiagnosticsTask.TASK_NAME, true,  false, false, true,  false, false, true,  false),
            };
            var allTasks = OspreyTasks.CreateAll();
            Assert.AreEqual(expected.Length, allTasks.Length, @"every task has a row");
            foreach (var e in expected)
            {
                var task = OspreyTasks.FindByName(allTasks, e.Name);
                Assert.IsNotNull(task, e.Name);
                Assert.AreEqual(e.HydratesPerRun, task.HydratesPerRun, e.Name + @": HydratesPerRun");
                Assert.AreEqual(e.StartsAfter, task.StartsAfterPerFileScoring, e.Name + @": StartsAfterPerFileScoring");
                Assert.AreEqual(e.ReadsReconciled, task.ReadsReconciledScores, e.Name + @": ReadsReconciledScores");
                Assert.AreEqual(e.Stage7Join, task.RunsStage7Join, e.Name + @": RunsStage7Join");
                Assert.AreEqual(e.PerFileWorker, task.IsPerFileWorker, e.Name + @": IsPerFileWorker");
                Assert.AreEqual(e.InCanonical, task.InCanonicalPipeline, e.Name + @": InCanonicalPipeline");
                Assert.AreEqual(e.RunsCanonical, task.RunsCanonicalPipeline, e.Name + @": RunsCanonicalPipeline");
                Assert.AreEqual(e.Standalone, task.RunsStandalone, e.Name + @": RunsStandalone");
            }
            // The facts are not independent, whatever row an author writes: a stage of the
            // canonical pipeline runs it when selected; a task runs exactly one pipeline
            // shape; a per-file worker never runs the join; and a task reading the reconciled
            // parquet starts after the per-file scoring that wrote its precursor.
            foreach (var task in allTasks)
            {
                if (task.InCanonicalPipeline)
                    Assert.IsTrue(task.RunsCanonicalPipeline, task.Name + @": a canonical stage runs the canonical pipeline");
                Assert.IsFalse(task.RunsStandalone && task.RunsCanonicalPipeline, task.Name + @": one pipeline shape");
                Assert.IsFalse(task.IsPerFileWorker && task.RunsStage7Join, task.Name + @": a per-file worker runs no join");
                if (task.ReadsReconciledScores)
                    Assert.IsTrue(task.StartsAfterPerFileScoring, task.Name + @": reconciled rows exist only after Stage 4");
            }
            // The list is complete: every concrete OspreyTask in the task library is in it,
            // so a class committed without its CreateAll() line cannot pass as "unknown task".
            var concreteTaskTypes = typeof(OspreyTask).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(OspreyTask).IsAssignableFrom(t))
                .OrderBy(t => t.FullName)
                .ToArray();
            CollectionAssert.AreEqual(concreteTaskTypes,
                allTasks.Select(t => t.GetType()).OrderBy(t => t.FullName).ToArray(),
                @"every OspreyTask subclass in Osprey.Tasks must be listed in OspreyTasks.CreateAll() exactly once");
            // The defaults fail closed: a task that overrides nothing is admitted to nothing.
            var unknown = new UnlistedTask();
            Assert.IsFalse(unknown.HydratesPerRun || unknown.StartsAfterPerFileScoring ||
                           unknown.ReadsReconciledScores || unknown.RunsStage7Join ||
                           unknown.IsPerFileWorker || unknown.InCanonicalPipeline ||
                           unknown.RunsCanonicalPipeline || unknown.RunsStandalone);
            // And the predicates that consult them read the selection, with the straight-through
            // run (no selection) answering as the full pipeline: from the spectra, running the join.
            var full = new OspreyConfig();
            Assert.IsFalse(ScoringTaskShared.StartsAfterPerFileScoring(full));
            Assert.IsFalse(ScoringTaskShared.ReadsReconciledScores(full));
            Assert.IsTrue(ScoringTaskShared.RunsStage7Join(full));
            var selected = new OspreyConfig();
            selected.SelectTask(unknown);
            Assert.IsFalse(ScoringTaskShared.RunsStage7Join(selected), @"an unlisted task is refused the join");
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
            var admitted = new[] { SecondPassFdrTask.TASK_NAME, ModelDiagnosticsTask.TASK_NAME };
            foreach (var task in OspreyTasks.CreateAll())
            {
                bool expected = admitted.Contains(task.Name);
                var config = TaskConfigs.ForTask(task.Name);
                Assert.AreEqual(expected, ScoringTaskShared.RunsStage7Join(config),
                    string.Format(@"--task {0}: RunsStage7Join must be {1}", task.Name, expected));
                // A task that does not run the join must be refused BEFORE any disk term,
                // which is what makes the refusal free and unconditional.
                if (!expected)
                {
                    Assert.IsFalse(
                        ScoringTaskShared.Stage7StreamAdmittedBeforeRescore(config),
                        string.Format(@"--task {0} must not be admitted to the streamed join", task.Name));
                }
            }
            // The straight-through pipeline runs every stage, so it is admitted.
            Assert.IsTrue(ScoringTaskShared.RunsStage7Join(new OspreyConfig()));
        }

        /// <summary>
        /// A task that overrides none of the selection facts, standing in for one added
        /// later whose author has not yet decided what is true of it.
        /// </summary>
        private sealed class UnlistedTask : OspreyTask
        {
            public override string Name => @"Unlisted";
            public override bool Run(PipelineContext ctx) => true;
            public override bool Rehydrate(PipelineContext ctx) => true;
        }
    }
}
