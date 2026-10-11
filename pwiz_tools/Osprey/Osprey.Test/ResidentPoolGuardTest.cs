/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for what is left of the resident first-pass pool ratchet after
    /// <c>OSPREY_FDR_PROJECTION</c> was removed (2026-10-10): the token list
    /// (<see cref="ResidentPaths.KNOWN_UNFIXED"/>) pinned EMPTY, the lean-load predicate
    /// (<see cref="PerFileScoringTask.CanUseLeanProjection"/>) as a truth table, and the
    /// guards past the compaction line. The first-pass guard itself went with the last path it
    /// could admit: with no resident first-pass path left, there is nothing for it to refuse
    /// or name.
    /// </summary>
    [TestClass]
    public class ResidentPoolGuardTest
    {
        [TestMethod]
        public void TestResidentPathRatchet()
        {
            // The high-water mark itself. This list may SHRINK as paths are streamed; it must
            // never GROW. Asserting the WHOLE set rather than membership is the point: an
            // addition then shows up in review as the ratchet running backwards, instead of
            // as an environment variable somebody set months ago and nobody re-examined.
            // 'mdiag-full-resume' is GONE (#4505), 'resume-survivor-handoff' is GONE (#4536,
            // the rehydrate got its own survivor loader), and 'hpc-merge' is GONE (#4486, the
            // reconciled-input merge streams its load) - the ratchet shrinking three times.
            // 'stage7-stream-off' is GONE too (2026-09-10) - the FOURTH shrink, with
            // OSPREY_STAGE7_STREAM, after its A/B was banked at 1e-9 against the golden.
            // 'fdrbench-pass1' is GONE (#4507) - the FIFTH shrink. The pass-1 FDRBench emitter
            // streams off the per-file sidecars and the experiment map.
            // 'non-percolator-fdr' is GONE (#4543) - the SIXTH shrink. Every FDR method left is a
            // classifier inside the Percolator framework and streams.
            // 'compacted-entries-buffer' is GONE (2026-10-10) - the SEVENTH shrink, with
            // OSPREY_STAGE6_STREAM_SURVIVORS.
            // 'projection-off' is GONE (2026-10-10) - the EIGHTH shrink, and the list is now
            // EMPTY. It named the legacy resident first pass (OSPREY_FDR_PROJECTION=0), the A/B
            // oracle for the projection path, and went with that switch. No final A/B was
            // possible: the arm aborted in Stage 7 on every dataset, the same failure as the
            // Stage 6 oracle; the committed golden is the oracle.
            Assert.AreEqual(0, ResidentPaths.KNOWN_UNFIXED.Count,
                "a token was added back to KNOWN_UNFIXED - the ratchet is running backwards");

            // The list parsing the startup warning relies on still works on its own terms:
            // separators are interchangeable, whitespace is tolerated, matching ignores case.
            Assert.IsTrue(OspreyEnvironment.NamesResidentPath(" alpha ; BETA ", "beta"));
            Assert.IsTrue(OspreyEnvironment.NamesResidentPath("alpha,beta", "alpha"));
            Assert.IsFalse(OspreyEnvironment.NamesResidentPath("alpha,beta", "gamma"));
            Assert.IsFalse(OspreyEnvironment.NamesResidentPath(null, "alpha"));

            AssertLeanProjectionTruthTable();

            // The ALL-RUNS reconciliation bundle, refused outright. Reaches PAST the compaction
            // line, but takes NO token: the bounded alternative
            // (the per-run survivor loader off the analysis-wide retained base_id summary)
            // exists on every route that gets here, so residency is a defect to fix and not a
            // path to name.
            //
            // Unit-tested rather than gate-tested BY NECESSITY, and that is the point. No
            // regression leg reaches this guard - verified, the message appears on zero legs -
            // because the routing fix left it with no caller. A guard nothing exercises is a
            // guard that can rot unnoticed, so its message is pinned here: it must name the
            // shape, the measured cost, and the bounded alternative, or the operator who trips
            // it in a year gets a refusal with no way forward.
            AssertAllRunsBundleGuard();

            // The Stage-7 join ADMISSION, which is what decides whether that guard has a
            // subject at all. It used to be config.ExpectReconciledInput - one CLI flag - and
            // is now the disk question that flag stood in for, so the rules it must not lose
            // are pinned here rather than left to the end-to-end gate.
            AssertStage7StreamAdmission();
        }

        /// <summary>
        /// The lean counts-only load adds an EMPTY entry list per file, so the two consumers that
        /// need real stubs must never take it: a reconciled bundle (the overlay reads them) and
        /// the reconciled-input merge, whose Stage 7 would otherwise write a near-empty
        /// <c>.blib</c> with no error. The case that matters for the merge is inputs MISSING a
        /// <c>.reconciliation.json</c> (one partially copied file is enough to make
        /// <c>AllHaveReconSidecars</c> false). Every other config - the default run, the tree
        /// classifier, FDRBench pass 1 and <c>both</c>, <c>--model-diagnostics</c> - goes lean:
        /// none of them reads a resident first-pass pool any more.
        /// </summary>
        private static void AssertLeanProjectionTruthTable()
        {
            var hpc = TaskConfigs.ForTask(SecondPassFdrTask.TASK_NAME);
            Assert.IsFalse(PerFileScoringTask.CanUseLeanProjection(hpc, hasReconSidecars: false),
                "the reconciled-input merge must never take the lean counts-only load");
            Assert.IsFalse(PerFileScoringTask.CanUseLeanProjection(hpc, hasReconSidecars: true));

            var lean = new OspreyConfig();
            Assert.IsFalse(PerFileScoringTask.CanUseLeanProjection(lean, hasReconSidecars: true));
            Assert.IsTrue(PerFileScoringTask.CanUseLeanProjection(lean, hasReconSidecars: false));

            var fdrbench1 = new OspreyConfig { OutputFdrBench = "bench.tsv", FdrBenchPass = OspreyConfig.FDRBENCH_PASS_1 };
            var fdrbenchBoth = new OspreyConfig
            {
                OutputFdrBench = "bench.tsv",
                FdrBenchPass = OspreyConfig.FDRBENCH_PASS_1 | OspreyConfig.FDRBENCH_PASS_2
            };
            foreach (var config in new[]
                     {
                         fdrbench1, fdrbenchBoth,
                         new OspreyConfig { FdrClassifier = FdrClassifier.Gbdt },
                         new OspreyConfig { ModelDiagnostics = true }
                     })
            {
                Assert.IsTrue(PerFileScoringTask.CanUseLeanProjection(config, hasReconSidecars: false));
            }
        }

        /// <summary>
        /// The all-runs reconciled-parquet admission: ALL, never any, and never vacuously
        /// true.
        ///
        /// <para>The fold rebuilds each run from its own reconciled parquet, so one run without
        /// a readable one is a run it cannot produce - and "some run has one" would fail at that
        /// run, hours in. An empty or absent input list is refused for the opposite reason:
        /// there is no pool to bound, so vacuous truth would admit a fold over nothing on a
        /// configuration no other term in the predicate examines.</para>
        ///
        /// <para>Only the negative half is asserted here. The positive one needs real Stage 6
        /// artifacts, which is <c>TestIsCurrentReconciledSurvivorSubset</c>'s job per file and
        /// the regression gate's per cohort; what CANNOT be seen there is a predicate that says
        /// yes when it has been handed nothing.</para>
        /// </summary>
        private static void AssertStage7StreamAdmission()
        {
            Assert.IsFalse(ScoringTaskShared.AllReconciledParquetsCurrent(new OspreyConfig()));
            Assert.IsFalse(ScoringTaskShared.AllReconciledParquetsCurrent(
                new OspreyConfig { InputFiles = new List<string>() }));
            // Paths under a directory that does not exist: every run is missing its parquet,
            // which is the cold cohort's state before Stage 6 has written any.
            string absent = Path.Combine(Path.GetTempPath(),
                "osprey_no_such_dir_" + Guid.NewGuid().ToString("N"));
            Assert.IsFalse(ScoringTaskShared.AllReconciledParquetsCurrent(
                new OspreyConfig
                {
                    InputFiles = new List<string>
                    {
                        Path.Combine(absent, "a.mzML"), Path.Combine(absent, "b.mzML")
                    }
                }));
        }

        /// <summary>
        /// The all-runs reconciliation bundle guard: refuses the run that HAD the bounded
        /// alternative and declined it, and stays out of the way of the run that never had one.
        ///
        /// <para>That distinction is the test. The guard first refused unconditionally, which
        /// reads as caution and is not: the arm it guards is also reached when this analysis has
        /// no retained base_id summary at all - no <c>-o</c> blib, or one written by a build
        /// with another <c>FormatVersion</c>. Under a resident token master completes those runs
        /// through the overlay, which needs no summary; on the default load the streamed bundle
        /// fails one call later with its own remedy. Either way an unconditional refusal here
        /// adds nothing but a wrong instruction - use a loader that is built from the very file
        /// whose absence sent the run down this arm. So both halves are pinned, and the null
        /// half is the one that would otherwise regress silently.</para>
        ///
        /// <para>The refusing half also asserts a supplied token is named back, so nobody burns
        /// an afternoon hunting the environment variable that would let it through. The wording
        /// is user text and is not asserted.</para>
        /// </summary>
        private static void AssertAllRunsBundleGuard()
        {
            // No output blib: nothing names the summary, so the per-run loader cannot exist and
            // there is nothing to refuse - the load that follows decides the outcome.
            Assert.IsNull(
                ScoringTaskShared.AllRunsBundleGuardError(new OspreyConfig(), null),
                "a run with no bounded alternative must not be refused");

            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_bundle_guard_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var config = new OspreyConfig
                {
                    InputFiles = new List<string> { Path.Combine(dir, "a.mzML") },
                    OutputBlib = Path.Combine(dir, "out.blib")
                };

                // Named but not written: the summary is what the loader is built from, so a
                // path with no file behind it is still "no bounded alternative".
                Assert.IsNull(ScoringTaskShared.AllRunsBundleGuardError(config, null),
                    "a missing retained base_id summary is disk state, not a declined route");

                // Written and current: the bounded route existed and this arm was reached
                // anyway, which is the --task ModelDiagnostics defect shape.
                RetainedBaseIdSidecar.Write(
                    RetainedBaseIdSidecar.PathFor(config.OutputBlib, config.InputFiles[0]),
                    new[] { 1u, 2u, 3u }, TestStamps.Any);
                string err = ScoringTaskShared.AllRunsBundleGuardError(config, null);
                Assert.IsNotNull(err, "the all-runs bundle must never be admitted silently");

                // A supplied token changes the wording but not the answer. Naming the value back
                // is what stops a stale or misspelled token reading exactly like an unset one.
                string named = ScoringTaskShared.AllRunsBundleGuardError(
                    config, "projection-off");
                Assert.IsNotNull(named, "no token admits the all-runs bundle");
                StringAssert.Contains(named, "projection-off");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// The predicate the pre-compaction-pool decision now defers to (#4486).
        /// <c>PreCompactionPoolReason</c> used to ask <c>!NoJoin</c> as a stand-in for "will
        /// first-pass Percolator train in this process", which agreed with the real rule for
        /// every task except <c>--task SecondPassFDR</c> - and that one disagreement forced an
        /// O(files) resident pool for a consumer that does not exist.
        ///
        /// <para>Pinned as a TRUTH TABLE over every selection, built the way the CLI builds
        /// them, not as a single case, because the defect was a predicate that was right four
        /// times out of five. The flag-only configs that used to stand for the tasks here are
        /// gone: membership is the selection's, and a config with a flag but no selection is
        /// one the CLI cannot produce.</para>
        /// </summary>
        [TestMethod]
        public void TestFirstPassMembershipAcrossTasks()
        {
            // Straight-through (-i, no --task): FirstPassFDR runs.
            Assert.IsTrue(ScoringTaskShared.Includes<FirstPassFdrTask>(TaskConfigs.StraightThrough()));

            // The two per-file workers: excluded, they stop before the join.
            Assert.IsFalse(ScoringTaskShared.Includes<FirstPassFdrTask>(TaskConfigs.ForTask(PerFileScoringTask.TASK_NAME)));
            Assert.IsFalse(ScoringTaskShared.Includes<FirstPassFdrTask>(TaskConfigs.ForTask(PerFileRescoreTask.TASK_NAME)));

            // --task FirstPassFDR: it IS the first-pass node.
            Assert.IsTrue(ScoringTaskShared.Includes<FirstPassFdrTask>(TaskConfigs.ForTask(FirstPassFdrTask.TASK_NAME)));

            // --task SecondPassFDR: the old !NoJoin proxy said "runs" - the selection says the
            // join runs alone. This single row is the whole change.
            var secondPass = TaskConfigs.ForTask(SecondPassFdrTask.TASK_NAME);
            Assert.IsFalse(ScoringTaskShared.Includes<FirstPassFdrTask>(secondPass),
                OspreyCommandArgs.ARG_TASK + SecondPassFdrTask.TASK_NAME + " must not be treated as running first-pass Percolator");

            // This row is what lets the merge take the file-count-bounded STREAMING hydrate. It
            // does NOT license the LEAN counts-only projection for this config -
            // CanUseLeanProjection is asserted FALSE for it in TestResidentPathRatchet, because
            // that path hands Stage 7 empty per-file lists and would write a near-empty .blib
            // with no error. Streaming hydrate and lean projection are different routes.
        }
    }
}
