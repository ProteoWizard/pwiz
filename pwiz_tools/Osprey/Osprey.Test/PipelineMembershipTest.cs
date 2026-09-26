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
    /// Pins the one membership rule, <see cref="OspreyConfig.Includes"/> - the driver-owned
    /// dataflow's source of truth for which stages run in each HPC mode - against an explicit
    /// expected truth table, and the task set it is asked over: the two lists, what each
    /// selection runs, and the two facts a task still states about itself.
    ///
    /// The table is the run-set the legacy <c>DeriveStartAtTask</c>/<c>DeriveStopAfterTask</c>
    /// range produced, then the per-task <c>IsIncluded</c> overrides reproduced over three
    /// membership flags, and now the rule reproduces over the selection and its pipeline. It
    /// is kept as a permanent regression guard so an edit that breaks the membership of any
    /// mode fails here rather than silently mis-routing the pipeline.
    /// </summary>
    [TestClass]
    public class PipelineMembershipTest
    {
        [TestMethod]
        public void TestIncludesMembershipTable()
        {
            // Expected membership per mode, in canonical pipeline order
            // [PerFileScoring, FirstPassFDR, PerFileRescoring, SecondPassFDR, TrainingExport]. Each config
            // comes from TaskConfigs.ForTask, the way a run builds it: the selection and the
            // pipeline it runs, from one task set. The rows used to carry an input KIND too
            // (a parquet list standing for --input-scores), and every predicate read both;
            // the kind is gone and the expected memberships are unchanged, which is the claim
            // worth pinning.
            // The fifth stage is OPTIONAL: with --training-export off (every row but the
            // two that turn it on) it is excluded under every selection, which is what keeps
            // every other artifact byte-identical. --task TrainingExport implies the option.
            var cases = new (string Name, bool Export, bool[] Expected)[]
            {
                (null,                           false, new[] { true,  true,  true,  true,  false }),
                (null,                           true,  new[] { true,  true,  true,  true,  true  }),
                (PerFileScoringTask.TASK_NAME,   true,  new[] { true,  false, false, false, false }),
                (FirstPassFdrTask.TASK_NAME,     false, new[] { false, true,  false, false, false }),
                (PerFileRescoreTask.TASK_NAME,   false, new[] { false, false, true,  false, false }),
                (SecondPassFdrTask.TASK_NAME,    true,  new[] { false, false, false, true,  false }),
                (TrainingExportTask.TASK_NAME,   false, new[] { false, false, false, false, true  }),
                // --task ModelDiagnostics is a RENDER over retained products: a selector that
                // is not a stage of the pipeline it runs, so the rule includes every stage,
                // exactly like the straight-through run, and artifact WRITES are what it
                // suppresses. The row here used to read {true,true,false,false}, which was
                // the shape of a config the CLI cannot build. The training export is the one
                // stage it does not include even when asked: a render writes no artifact.
                (ModelDiagnosticsTask.TASK_NAME, true,  new[] { true,  true,  true,  true,  false }),
            };
            // Every selection that walks the canonical pipeline has a row, so a task added
            // later cannot leave its membership unpinned; the standalone task has none to pin.
            var set = OspreyTasks.Create();
            foreach (var task in set.All.Where(t => ReferenceEquals(set.PipelineFor(t), set.Pipeline)))
                Assert.IsTrue(cases.Any(c => c.Name == task.Name), task.Name + @" has no membership row");

            foreach (var c in cases)
            {
                string caseName = (c.Name ?? @"straight-through") + (c.Export ? @" --training-export" : string.Empty);
                var config = c.Name == null ? TaskConfigs.StraightThrough() : TaskConfigs.ForTask(c.Name);
                if (c.Export)
                    config.TrainingExport.Enabled = true;
                Assert.AreEqual(config.Pipeline.Count, c.Expected.Length,
                    string.Format(@"{0}: expected-row length must match stage count", caseName));

                for (int i = 0; i < config.Pipeline.Count; i++)
                {
                    Assert.AreEqual(c.Expected[i], config.Includes(config.Pipeline[i]), string.Format(
                        @"{0}/{1}: Includes must be {2}", caseName, config.Pipeline[i].Name, c.Expected[i]));
                }
            }
            // A bare config that never went through SelectTask reads as the full pipeline -
            // every stage the pipeline always runs, and the optional one only when asked for.
            var bare = new OspreyConfig();
            foreach (var stage in set.Pipeline)
                Assert.AreEqual(!(stage is TrainingExportTask), bare.Includes(stage), stage.Name);
            bare.TrainingExport.Enabled = true;
            foreach (var stage in set.Pipeline)
                Assert.IsTrue(bare.Includes(stage), stage.Name);
        }

        /// <summary>
        /// The task set: every selectable task listed once, the canonical pipeline an
        /// explicit ordered sub-list of it, the pipeline each selection runs declared by the
        /// set (SpectraCache alone; ModelDiagnostics the canonical stages; a stage its own
        /// pipeline), instances shared throughout, and a selection from another set refused.
        /// </summary>
        [TestMethod]
        public void TestTaskSetAndPipelineForSelection()
        {
            var set = OspreyTasks.Create();
            CollectionAssert.AreEqual(
                new[]
                {
                    SpectraCacheTask.TASK_NAME, PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME,
                    PerFileRescoreTask.TASK_NAME, SecondPassFdrTask.TASK_NAME, TrainingExportTask.TASK_NAME,
                    ModelDiagnosticsTask.TASK_NAME
                },
                set.All.Select(t => t.Name).ToArray(), @"the --task values, in --help order");
            CollectionAssert.AreEqual(
                new[]
                {
                    PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME,
                    PerFileRescoreTask.TASK_NAME, SecondPassFdrTask.TASK_NAME, TrainingExportTask.TASK_NAME
                },
                set.Pipeline.Select(t => t.Name).ToArray(), @"the canonical stages, in execution order");
            foreach (var stage in set.Pipeline)
                Assert.IsTrue(set.All.Contains(stage), stage.Name + @": a stage is one of the listed instances");

            Assert.AreSame(set.Pipeline, set.PipelineFor(null), @"no selection walks the canonical pipeline");
            foreach (var stage in set.Pipeline)
                Assert.AreSame(set.Pipeline, set.PipelineFor(stage), stage.Name + @": a stage runs its pipeline");
            var spectraCache = set.FindByName(SpectraCacheTask.TASK_NAME);
            CollectionAssert.AreEqual(new[] { spectraCache }, set.PipelineFor(spectraCache).ToArray(),
                @"SpectraCache runs alone");
            Assert.AreSame(set.Pipeline, set.PipelineFor(set.FindByName(ModelDiagnosticsTask.TASK_NAME)),
                @"ModelDiagnostics runs the canonical stages");

            // Case-insensitive lookup resolves to the canonical spelling.
            Assert.AreSame(set.FindByName(FirstPassFdrTask.TASK_NAME), set.FindByName(@"firstpassfdr"));
            Assert.IsNull(set.FindByName(@"Bogus"));

            // A selection from a DIFFERENT set is refused rather than silently building a
            // pipeline whose reference checks all fail - at the set, and at the driver, which
            // must be handed the very list the config's task was selected with: with any
            // other list the membership rule excludes every stage and the run "completes"
            // having done nothing.
            var stranger = OspreyTasks.Create().FindByName(SpectraCacheTask.TASK_NAME);
            Assert.ThrowsException<ArgumentException>(() => set.PipelineFor(stranger));
            var config = TaskConfigs.ForTask(set, FirstPassFdrTask.TASK_NAME);
            Assert.ThrowsException<ArgumentException>(
                () => new AnalysisPipeline().Run(config, OspreyTasks.Create().Pipeline));

            // The list is complete: every concrete OspreyTask in the task library is in it,
            // so a class committed without its place in the list cannot pass as "unknown task".
            var concreteTaskTypes = typeof(OspreyTask).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(OspreyTask).IsAssignableFrom(t))
                .OrderBy(t => t.FullName)
                .ToArray();
            CollectionAssert.AreEqual(concreteTaskTypes,
                set.All.Select(t => t.GetType()).OrderBy(t => t.FullName).ToArray(),
                @"every OspreyTask subclass in Osprey.Tasks must be listed in OspreyTasks.Create() exactly once");
        }

        /// <summary>
        /// The two facts a task still states about itself, as one truth table, plus the
        /// position and membership questions the pipeline answers about a selection: what
        /// used to be four per-task facts and an enum switch each.
        /// </summary>
        [TestMethod]
        public void TestSelectedTaskFacts()
        {
            // Columns: IsPerFileWorker, HydratesPerRun; then, of the selection: starts after
            // per-file scoring, reads the reconciled parquets, runs the Stage 7 join.
            var expected = new (string Name, bool PerFileWorker, bool HydratesPerRun,
                bool StartsAfterScoring, bool ReadsReconciled, bool RunsStage7Join)[]
            {
                (SpectraCacheTask.TASK_NAME,     true,  false, false, false, false),
                (PerFileScoringTask.TASK_NAME,   true,  false, false, false, false),
                (FirstPassFdrTask.TASK_NAME,     false, false, true,  false, false),
                (PerFileRescoreTask.TASK_NAME,   true,  true,  true,  false, false),
                (SecondPassFdrTask.TASK_NAME,    false, false, true,  true,  true),
                (TrainingExportTask.TASK_NAME,   true,  false, true,  true,  false),
                (ModelDiagnosticsTask.TASK_NAME, false, true,  false, false, true),
            };
            var set = OspreyTasks.Create();
            Assert.AreEqual(expected.Length, set.All.Count, @"every task has a row");
            foreach (var e in expected)
            {
                var task = set.FindByName(e.Name);
                Assert.IsNotNull(task, e.Name);
                Assert.AreEqual(e.PerFileWorker, task.IsPerFileWorker, e.Name + @": IsPerFileWorker");
                Assert.AreEqual(e.HydratesPerRun, task.HydratesPerRun, e.Name + @": HydratesPerRun");
                var config = TaskConfigs.ForTask(set, e.Name);
                Assert.AreEqual(e.StartsAfterScoring, ScoringTaskShared.StartsAfterPerFileScoring(config), e.Name + @": StartsAfterPerFileScoring");
                Assert.AreEqual(e.ReadsReconciled, ScoringTaskShared.ReadsReconciledScores(config), e.Name + @": ReadsReconciledScores");
                Assert.AreEqual(e.RunsStage7Join, ScoringTaskShared.RunsStage7Join(config), e.Name + @": RunsStage7Join");
                // A per-file worker never runs the join.
                Assert.IsFalse(task.IsPerFileWorker && ScoringTaskShared.RunsStage7Join(config), e.Name + @": a per-file worker runs no join");
            }
            // The straight-through run (no selection) answers as the full pipeline: from the
            // spectra, running the join - and so does a bare config that never selected.
            foreach (var full in new[] { TaskConfigs.StraightThrough(), new OspreyConfig() })
            {
                Assert.IsFalse(ScoringTaskShared.StartsAfterPerFileScoring(full));
                Assert.IsFalse(ScoringTaskShared.ReadsReconciledScores(full));
                Assert.IsTrue(ScoringTaskShared.RunsStage7Join(full));
            }
            // The defaults fail closed: a task that overrides nothing is a join that hydrates
            // nothing per run.
            var unknown = new UnlistedTask();
            Assert.IsFalse(unknown.IsPerFileWorker || unknown.HydratesPerRun);
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
            foreach (var task in OspreyTasks.Create().All)
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
            Assert.IsTrue(ScoringTaskShared.RunsStage7Join(TaskConfigs.StraightThrough()));
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
