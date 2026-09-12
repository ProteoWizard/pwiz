/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for the resident first-pass pool guard
    /// (<see cref="PerFileScoringTask.ResidentPoolGuardError"/>): a run that would take the
    /// O(files) resident pool must fail fast with an actionable error, UNLESS the operator
    /// named THAT path via <c>OSPREY_ALLOW_UNFIXED_RESIDENT=&lt;token&gt;</c>. Naming a
    /// different path does not help, and a path absent from
    /// <see cref="ResidentPaths.KNOWN_UNFIXED"/> is refused whatever the variable says - so no
    /// user reaches an O(files) memory path by accident, and no single value re-opens all of
    /// them the way the former blanket <c>OSPREY_ALLOW_UNBOUNDED_MEMORY=1</c> did.
    /// <c>OSPREY_FDR_PROJECTION=0</c> is included: it requests the legacy resident
    /// implementation outright, so it is the <see cref="ResidentPaths.PROJECTION_OFF"/> token
    /// rather than an automatic exemption.
    /// Also pins the trigger set that arms the guard
    /// (<see cref="PerFileScoringTask.NeedsResidentPool(OspreyConfig, bool)"/>) and the
    /// contents of the token list itself, since a wrongly-added trigger is what re-broke
    /// 82-file OSPREY_PASS2_QVALUE=transfer runs.
    /// </summary>
    [TestClass]
    public class ResidentPoolGuardTest
    {
        [TestMethod]
        public void TestResidentPoolGuardError()
        {
            // The lean streaming path (needsResidentPool == false) is never guarded, regardless
            // of the opt-in flags -- the default straight-through + resume paths land here.
            var lean = new OspreyConfig();
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(lean, needsResidentPool: false,
                allowUnfixedResident: null, useFdrProjection: true));

            // A non-Percolator FdrMethod trips the fat pool: guarded (armed), and the message is
            // actionable - it names the token the operator would set, not just a symptom.
            // This exemplar was the HPC reconciled-input merge until #4486 streamed it, and
            // --fdrbench-pass 1 until #4507 did; the properties being pinned are the guard's,
            // so any still-listed trigger exercises them.
            var simple = new OspreyConfig { FdrMethod = FdrMethod.Simple };
            string simpleErr = PerFileScoringTask.ResidentPoolGuardError(simple, needsResidentPool: true,
                allowUnfixedResident: null, useFdrProjection: true);
            Assert.IsNotNull(simpleErr);
            StringAssert.Contains(simpleErr, "OSPREY_ALLOW_UNFIXED_RESIDENT=" + ResidentPaths.NON_PERCOLATOR_FDR);

            // Naming THIS path exempts it (no error):
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(simple, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.NON_PERCOLATOR_FDR, useFdrProjection: true));
            // OSPREY_FDR_PROJECTION=0 (the A/B byte-identity oracle) is NOT an automatic
            // exemption any more - it is its own token. Unnamed it is refused like anything
            // else, which closes the last route to a resident pool nobody had to ask for.
            Assert.IsNotNull(PerFileScoringTask.ResidentPoolGuardError(simple, needsResidentPool: true,
                allowUnfixedResident: null, useFdrProjection: false));
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(simple, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.PROJECTION_OFF, useFdrProjection: false));
            // It outranks a config-driven trigger, because it selects the legacy implementation
            // for the whole run: naming the other reason is not enough.
            Assert.IsNotNull(PerFileScoringTask.ResidentPoolGuardError(simple, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.NON_PERCOLATOR_FDR, useFdrProjection: false));

            // Naming a DIFFERENT path does not: the token grants one exemption, not amnesty.
            // This is the property the former blanket boolean lacked.
            Assert.IsNotNull(PerFileScoringTask.ResidentPoolGuardError(simple, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.COMPACTED_ENTRIES_BUFFER, useFdrProjection: true));

            // Capitalization does not defeat it - the error names the exact token to set, so
            // rejecting the operator's own value for case would read as the guard ignoring them.
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(simple, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.NON_PERCOLATOR_FDR.ToUpperInvariant(),
                useFdrProjection: true));

            // --fdrbench-pass 1 no longer arms the pool at all (#4507): the pass-1 emitter
            // streams off the per-file sidecars. Pinned as a config, not just as the token's
            // absence from KNOWN_UNFIXED below, because the predicate and the list are
            // separate code and the bitmask defect this fixed lived in the predicate: `both`
            // (1 | 2) never matched the old `== 1`, so the resident path was skipped and the
            // pass-1 file silently never written. Both selections must stream now.
            var fdrbench1 = new OspreyConfig { OutputFdrBench = "bench.tsv", FdrBenchPass = OspreyConfig.FDRBENCH_PASS_1 };
            var fdrbenchBoth = new OspreyConfig
            {
                OutputFdrBench = "bench.tsv",
                FdrBenchPass = OspreyConfig.FDRBENCH_PASS_1 | OspreyConfig.FDRBENCH_PASS_2
            };

            // --task SecondPassFDR takes NO resident pool since #4486: FirstPassFdrTask is
            // excluded on that node, so nothing trains off the pre-compaction pool, and every
            // pass-2 consumer streams from disk one file at a time. The old path cost
            // 2.07 GB per file, which made the final HPC join impossible at 82 files.
            //
            // This is the production-reachable assertion, and the ONLY one this config still
            // supports. A four-token loop over ResidentPoolGuardError(hpc, true, ...) used to
            // follow and was removed as duplicative: ResidentPoolTrigger no longer reads
            // ExpectReconciledInput at all, so that loop executed byte-identically to the
            // `lean` loop below - same branch, same tokens, no new coverage. It also could not
            // deliver the ratchet it claimed: a future change re-adding a trigger under a NEW
            // token would be named by none of {null, "", "hpc-merge", "anything"} and every
            // assertion would still pass. TestFirstPassMembershipAcrossTasks pins the property
            // that actually guards this - that FirstPassFdrTask is excluded here - and the
            // retired token is pinned by KNOWN_UNFIXED not containing it.
            var hpc = new OspreyConfig { ExpectReconciledInput = true };
            AssertNeedsResidentPool(false, hpc);

            // Taking ExpectReconciledInput out of NeedsResidentPool made the LEAN counts-only
            // load newly reachable on the merge, and that path adds an EMPTY entry list per
            // file - Stage 7 would have written a near-empty .blib with no error. It is
            // suppressed by its own term, not as a side effect of the resident predicate, and
            // the case that matters is a merge whose inputs are MISSING a .reconciliation.json
            // (one partially copied file is enough to make AllHaveReconSidecars false).
            Assert.IsFalse(PerFileScoringTask.CanUseLeanProjection(hpc, hasReconSidecars: false, useFdrProjection: true),
                "the reconciled-input merge must never take the lean counts-only load");
            Assert.IsFalse(PerFileScoringTask.CanUseLeanProjection(hpc, hasReconSidecars: true, useFdrProjection: true));
            // A reconciled bundle excludes it for the other reason (the overlay reads stubs),
            // and the plain projection run is exactly what the lean path exists for.
            Assert.IsFalse(PerFileScoringTask.CanUseLeanProjection(lean, hasReconSidecars: true, useFdrProjection: true));
            Assert.IsTrue(PerFileScoringTask.CanUseLeanProjection(lean, hasReconSidecars: false, useFdrProjection: true));
            // A resident-pool consumer keeps the fat load, so the lean path stays off there too.
            Assert.IsFalse(PerFileScoringTask.CanUseLeanProjection(simple, hasReconSidecars: false, useFdrProjection: true));
            // FDRBench pass 1 is not such a consumer any more, so it takes the lean load like
            // the default run does.
            Assert.IsTrue(PerFileScoringTask.CanUseLeanProjection(fdrbench1, hasReconSidecars: false, useFdrProjection: true));
            Assert.IsTrue(PerFileScoringTask.CanUseLeanProjection(fdrbenchBoth, hasReconSidecars: false, useFdrProjection: true));

            // Each user-reachable trigger names its own token so the failure is diagnosable.
            // --model-diagnostics is NOT among them any more (#4505): it armed the pool on a
            // full resume, where FirstPassFDR skipped its score pass and reported off the resident
            // entries, and FirstPassFDR's rehydrate now streams that report instead.
            //
            // Pinned on what the message MEANS, not on its prose. Two things have to hold,
            // and neither is "some error came back": mdiag has no trigger at all, so
            // ResidentPoolTrigger returns null and the refusal is the generic one issued
            // BEFORE any token is consulted - a bare non-null assertion would stay green even
            // with ModelDiagnostics = false.
            //
            // 1. The refusal NAMES NO TOKEN. That is exactly "no named route admits mdiag":
            //    the tokened refusal always carries OSPREY_ALLOW_UNFIXED_RESIDENT=<token>, so
            //    if someone re-arms mdiag onto an already-legal token the message gains that
            //    token and this fails. Asserting the absence of every legal token says it
            //    without pinning a sentence, which would break on any rewording.
            // 2. The disposition does not CHANGE under any token, which the absence check
            //    alone does not cover.
            var mdiag = new OspreyConfig { ModelDiagnostics = true };
            string mdiagErr = PerFileScoringTask.ResidentPoolGuardError(mdiag, true, null, true);
            StringAssert.Contains(mdiagErr, "OSPREY_ALLOW_UNFIXED_RESIDENT");
            foreach (string token in ResidentPaths.KNOWN_UNFIXED)
            {
                Assert.IsFalse(mdiagErr.Contains(token),
                    string.Format("--model-diagnostics refusal names token '{0}', so that " +
                                  "token now admits it", token));
                Assert.AreEqual(mdiagErr,
                    PerFileScoringTask.ResidentPoolGuardError(mdiag, true, token, true),
                    string.Format("--model-diagnostics changed disposition under token '{0}'", token));
            }

            // A resident path with NO token is refused unconditionally - no value admits it.
            // This is the ratchet: when something we streamed goes resident again, as transfer
            // did, it cannot be waved through. It has to be fixed, or deliberately listed.
            // (lean is the default config: Percolator, no fdrbench, no mdiag, not SecondPassFDR.)
            foreach (string token in new[] { null, "", "hpc-merge", "anything" })
            {
                Assert.IsNotNull(
                    PerFileScoringTask.ResidentPoolGuardError(lean, true, token, true), token);
            }

            // The high-water mark itself. This list may SHRINK as paths are streamed; it must
            // never GROW. Asserting the WHOLE set rather than membership is the point: an
            // addition then shows up in review as the ratchet running backwards, instead of
            // as an environment variable somebody set months ago and nobody re-examined.
            // LITERALS, not the constants: comparing the constants to themselves would pin
            // membership and order but not the text, and the text is what an operator types
            // into OSPREY_ALLOW_UNFIXED_RESIDENT. Renaming a value would otherwise compile and
            // pass here while silently invalidating every written-down invocation.
            // 'mdiag-full-resume' is GONE (#4505), 'resume-survivor-handoff' is GONE (#4536,
            // the rehydrate got its own survivor loader), and 'hpc-merge' is GONE (#4486, the
            // reconciled-input merge streams its load) - the ratchet shrinking three times.
            // 'stage7-stream-off' is GONE too (2026-09-10) - the ratchet shrinking a FOURTH
            // time. It was added when the streamed Stage-7 join first existed, to name the
            // resident arm an operator could still choose as an A/B byte-identity oracle. That
            // A/B was banked before the switch was retired: the resident arm passed the whole
            // regression against the committed golden at 1e-9, and the diagnostics HTML matched
            // the streamed arm byte for byte apart from generatedUtc. With OSPREY_STAGE7_STREAM
            // gone there is no choice left for a token to record.
            // 'fdrbench-pass1' is GONE (#4507) - the FIFTH shrink. The pass-1 FDRBench emitter
            // streams off the per-file sidecars and the experiment map, byte-identical to the
            // resident one, so no FDRBench selection reaches the resident path and the token
            // had nothing left to admit.
            CollectionAssert.AreEqual(
                new[]
                {
                    "non-percolator-fdr", "projection-off", "compacted-entries-buffer"
                },
                ResidentPaths.KNOWN_UNFIXED.ToArray());

            // The POST-compaction handoff guard (issue #4526). The guard above stops at the
            // compaction line, so the all-files survivor buffer Stage 5 hands to Stage 6 - 28 GB
            // at 163 files, live for the whole rescore - was never named and no token could
            // refuse it. Streaming it is the default; the resident opt-out is a named path.
            AssertStage6HandoffGuard();

            // The ALL-RUNS reconciliation bundle, refused outright. Reaches PAST the compaction
            // line like the Stage 6 guard above, but takes NO token: the bounded alternative
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

            // The trigger SET itself, not just the message it produces. Each of these takes the
            // O(files) resident pool and so arms the guard above.
            AssertNeedsResidentPool(true, simple);
            // OSPREY_FDR_PROJECTION=0 is itself an explicit resident opt-in.
            Assert.IsTrue(PerFileScoringTask.NeedsResidentPool(lean, useFdrProjection: false));
            // FDRBench pass 1 left the set with #4507, and so did `both` - which had never been
            // IN it (the old `== 1` test could not match a mask of 3), the defect that made
            // `both` emit pass 2 only.
            AssertNeedsResidentPool(false, fdrbench1);
            AssertNeedsResidentPool(false, fdrbenchBoth);

            // Nothing else does. Context for OSPREY_PASS2_QVALUE=transfer, which #4438 took off
            // the list (the per-run-only redesign maps each adjusted peak through that file's
            // own 1st-pass score to run-q sidecar, one file at a time) and a #4446 merge
            // artifact silently put back, killing an 82-file transfer run on the guard in ~25 s:
            // the predicate is now env-free apart from the projection switch, so the triggers
            // are exactly the two above plus the projection switch. That is what these two assertions pin.
            AssertNeedsResidentPool(false, lean);
            AssertNeedsResidentPool(false, mdiag);
        }

        /// <summary>
        /// Assert the resident-pool predicate on the projection path (the shipping default),
        /// where <paramref name="config"/> alone decides.
        /// </summary>
        private static void AssertNeedsResidentPool(bool expected, OspreyConfig config)
        {
            Assert.AreEqual(expected,
                PerFileScoringTask.NeedsResidentPool(config, useFdrProjection: true));
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
        /// <para>The refusing half also asserts the message says what happened and what to do -
        /// the shape (O(files x entries)), the measured cost, the bounded alternative, and that
        /// no token admits it, so nobody burns an afternoon hunting the environment variable
        /// that would let it through.</para>
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
                    new[] { 1u, 2u, 3u });
                string err = ScoringTaskShared.AllRunsBundleGuardError(config, null);
                Assert.IsNotNull(err, "the all-runs bundle must never be admitted silently");
                StringAssert.Contains(err, "O(files x entries)");
                StringAssert.Contains(err, "per-run survivor loader");
                StringAssert.Contains(err, "cannot admit this path");

                // A supplied token changes the wording but not the answer. Naming the value back
                // is what stops a stale or misspelled token reading exactly like an unset one,
                // which is the property its two sibling guards are also pinned on.
                string named = ScoringTaskShared.AllRunsBundleGuardError(
                    config, ResidentPaths.PROJECTION_OFF);
                Assert.IsNotNull(named, "no token admits the all-runs bundle");
                StringAssert.Contains(named, ResidentPaths.PROJECTION_OFF);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// The Stage 6 post-compaction handoff guard: streaming (the default) is never guarded,
        /// the resident opt-out is refused unless it is named, and a run that could not stream
        /// in the first place is not asked for a second token on top of the one its own
        /// resident path already requires.
        /// </summary>
        private static void AssertStage6HandoffGuard()
        {
            // Streaming: no error, whatever the token says.
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(
                streamingAvailable: true, streamingEnabled: true, allowUnfixedResident: null));
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(
                true, true, ResidentPaths.NON_PERCOLATOR_FDR));

            // OSPREY_STAGE6_STREAM_SURVIVORS=0 on a run that COULD stream: refused, and the
            // message names the token to set rather than describing a symptom.
            string err = PerFileScoringTask.Stage6ResidentHandoffGuardError(true, false, null);
            Assert.IsNotNull(err);
            StringAssert.Contains(err,
                "OSPREY_ALLOW_UNFIXED_RESIDENT=" + ResidentPaths.COMPACTED_ENTRIES_BUFFER);

            // Naming THIS path admits it - that is the A/B byte-identity oracle. Case-
            // insensitive, matching the pre-compaction guard.
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(
                true, false, ResidentPaths.COMPACTED_ENTRIES_BUFFER));
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(
                true, false, ResidentPaths.COMPACTED_ENTRIES_BUFFER.ToUpperInvariant()));

            // Naming a DIFFERENT path does not, and the message says which value was supplied
            // so a stale token does not read like an unset one.
            string wrongToken = PerFileScoringTask.Stage6ResidentHandoffGuardError(
                true, false, ResidentPaths.PROJECTION_OFF);
            Assert.IsNotNull(wrongToken);
            StringAssert.Contains(wrongToken, ResidentPaths.PROJECTION_OFF);

            // A run that cannot stream at all (no per-file survivor source: the legacy resident
            // path never computes the passing base_id set) is NOT guarded here. It is already
            // resident for a reason carrying its own token, and demanding a second one would
            // make a single decision need two environment variables.
            //
            // The RESUME is no longer in that category and must not drift back into it. Since
            // #4536 the rehydrate builds its own survivor loader, so a resume arrives with
            // streamingAvailable TRUE and is refused/admitted by the assertions above - the
            // same ones a computed run gets. The interim resume-side guard and its dedicated
            // resume-survivor-handoff token are gone with it; the KNOWN_UNFIXED assertion
            // above is what keeps the token from coming back.
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(false, false, null));

            // SEVERAL paths may be named at once. A run can legitimately trip more than one,
            // and a single-value variable made that run impossible: an operator proving the
            // streamed Stage 6 handoff bounded on a configuration that is already resident for
            // its own reason needs that run's token AND compacted-entries-buffer, so the very
            // A/B that establishes the bound aborted on its own guard. Both guards read the
            // list, and every admitted path is still named individually.
            string both = ResidentPaths.NON_PERCOLATOR_FDR + "," + ResidentPaths.COMPACTED_ENTRIES_BUFFER;
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(true, false, both));
            var simpleCfg = new OspreyConfig { FdrMethod = FdrMethod.Simple };
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(simpleCfg, true, both, true));
            // Separators are interchangeable and surrounding whitespace is tolerated - an
            // operator composing the value in a shell should not have to match a spelling.
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(
                true, false, " projection-off ; compacted-entries-buffer "));
            // A list still admits ONLY what it names: an unnamed path is refused as before.
            Assert.IsNotNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(
                true, false, ResidentPaths.PROJECTION_OFF + "," + ResidentPaths.NON_PERCOLATOR_FDR));
        }

        /// <summary>
        /// The predicate the pre-compaction-pool decision now defers to (#4486).
        /// <c>PreCompactionPoolReason</c> used to ask <c>!NoJoin</c> as a stand-in for "will
        /// first-pass Percolator train in this process", which agreed with the real rule for
        /// every task except <c>--task SecondPassFDR</c> - and that one disagreement forced an
        /// O(files) resident pool for a consumer that does not exist.
        ///
        /// <para>Pinned as a TRUTH TABLE over the flag combinations <c>Program</c> derives from
        /// <c>--task</c>, not as a single case, because the defect was a predicate that was
        /// right four times out of five. A future task flag that re-splits membership has to
        /// come through here.</para>
        /// </summary>
        [TestMethod]
        public void TestFirstPassMembershipAcrossTasks()
        {
            // Straight-through (-i, no --task): FirstPassFDR runs.
            Assert.IsTrue(FirstPassFdrTask.IsIncludedFor(new OspreyConfig()));

            // --task PerFileScoring / PerFileRescoring set NoJoin: excluded, they stop before
            // the join. One row each is enough now: these used to be asserted twice, once
            // with a parquet list and once without, because the predicate read the input KIND
            // as well as the flags and the two could disagree.
            Assert.IsFalse(FirstPassFdrTask.IsIncludedFor(
                new OspreyConfig { NoJoin = true }));

            // --task FirstPassFDR sets StopAfterStage5: it IS the first-pass node.
            Assert.IsTrue(FirstPassFdrTask.IsIncludedFor(
                new OspreyConfig { StopAfterStage5 = true }));

            // --task SecondPassFDR: NoJoin FALSE, so the old !NoJoin proxy said "runs" - but
            // ExpectReconciledInput excludes it. This single row is the whole change.
            Assert.IsFalse(FirstPassFdrTask.IsIncludedFor(
                new OspreyConfig { ExpectReconciledInput = true }),
                "--task SecondPassFDR must not be treated as running first-pass Percolator");

            // And the consequence the loader draws from it: the merge no longer demands the
            // RESIDENT pool, so it can take the file-count-bounded STREAMING hydrate.
            //
            // Two things this must not be read as saying, both of which the assertions above
            // contradict. It is NOT the only node on the bounded route: --task PerFileRescoring
            // was the original consumer of HydrateCompactedStreaming, and a straight-through
            // lean resume takes it too. And it does NOT license the LEAN counts-only projection
            // for this config - CanUseLeanProjection is asserted FALSE for it earlier in this
            // file, because that path hands Stage 7 empty per-file lists and would write a
            // near-empty .blib with no error. Streaming hydrate and lean projection are
            // different routes; only the first is what this row unlocks.
            Assert.IsFalse(PerFileScoringTask.NeedsResidentPool(
                new OspreyConfig { ExpectReconciledInput = true }, useFdrProjection: true));
        }
    }
}
