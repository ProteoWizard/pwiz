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
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for what is left of the resident first-pass pool ratchet after
    /// <c>OSPREY_FDR_PROJECTION</c> was removed (2026-10-10): the token list
    /// (<see cref="ResidentPaths.KNOWN_UNFIXED"/>) pinned EMPTY, and the guards past the
    /// compaction line. The first-pass guard itself went with the last path it
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

            // The Stage-7 join ADMISSION, which is what decides whether a run folds per run at
            // all. It used to be config.ExpectReconciledInput - one CLI flag - and
            // is now the disk question that flag stood in for, so the rules it must not lose
            // are pinned here rather than left to the end-to-end gate.
            AssertStage7StreamAdmission();
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
        /// Whether first-pass Percolator trains in this process: the predicate the
        /// <c>--input-scores</c> load asks before it adopts a reconciliation bundle (#4486). It
        /// used to be asked as <c>!NoJoin</c>, which agreed with the real rule for every task
        /// except <c>--task SecondPassFDR</c> - and that one disagreement forced an O(files)
        /// resident pool for a consumer that does not exist.
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
        }
    }
}
