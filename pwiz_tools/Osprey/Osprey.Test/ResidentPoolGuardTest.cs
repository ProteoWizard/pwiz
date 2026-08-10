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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
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

            // --fdrbench-pass 1 trips the fat pool: guarded (armed), and the message is
            // actionable - it names the token the operator would set, not just a symptom.
            // This was the HPC reconciled-input merge until #4486 streamed it; the properties
            // being pinned are the guard's, so any still-listed trigger exercises them.
            var fdrbench1 = new OspreyConfig { OutputFdrBench = "bench.tsv", FdrBenchPass = 1 };
            string benchErr = PerFileScoringTask.ResidentPoolGuardError(fdrbench1, needsResidentPool: true,
                allowUnfixedResident: null, useFdrProjection: true);
            Assert.IsNotNull(benchErr);
            StringAssert.Contains(benchErr, "OSPREY_ALLOW_UNFIXED_RESIDENT=" + ResidentPaths.FDRBENCH_PASS1);

            // Naming THIS path exempts it (no error):
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(fdrbench1, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.FDRBENCH_PASS1, useFdrProjection: true));
            // OSPREY_FDR_PROJECTION=0 (the A/B byte-identity oracle) is NOT an automatic
            // exemption any more - it is its own token. Unnamed it is refused like anything
            // else, which closes the last route to a resident pool nobody had to ask for.
            Assert.IsNotNull(PerFileScoringTask.ResidentPoolGuardError(fdrbench1, needsResidentPool: true,
                allowUnfixedResident: null, useFdrProjection: false));
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(fdrbench1, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.PROJECTION_OFF, useFdrProjection: false));
            // It outranks a config-driven trigger, because it selects the legacy implementation
            // for the whole run: naming the other reason is not enough.
            Assert.IsNotNull(PerFileScoringTask.ResidentPoolGuardError(fdrbench1, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.FDRBENCH_PASS1, useFdrProjection: false));

            // Naming a DIFFERENT path does not: the token grants one exemption, not amnesty.
            // This is the property the former blanket boolean lacked.
            Assert.IsNotNull(PerFileScoringTask.ResidentPoolGuardError(fdrbench1, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.NON_PERCOLATOR_FDR, useFdrProjection: true));

            // Capitalization does not defeat it - the error names the exact token to set, so
            // rejecting the operator's own value for case would read as the guard ignoring them.
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(fdrbench1, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.FDRBENCH_PASS1.ToUpperInvariant(),
                useFdrProjection: true));

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
            Assert.IsFalse(PerFileScoringTask.CanUseLeanProjection(fdrbench1, hasReconSidecars: false, useFdrProjection: true));

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

            var simple = new OspreyConfig { FdrMethod = FdrMethod.Simple };
            StringAssert.Contains(PerFileScoringTask.ResidentPoolGuardError(simple, true, null, true),
                ResidentPaths.NON_PERCOLATOR_FDR);

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
            CollectionAssert.AreEqual(
                new[]
                {
                    "fdrbench-pass1", "non-percolator-fdr",
                    "projection-off", "compacted-entries-buffer"
                },
                ResidentPaths.KNOWN_UNFIXED.ToArray());

            // The POST-compaction handoff guard (issue #4526). The guard above stops at the
            // compaction line, so the all-files survivor buffer Stage 5 hands to Stage 6 - 28 GB
            // at 163 files, live for the whole rescore - was never named and no token could
            // refuse it. Streaming it is the default; the resident opt-out is a named path.
            AssertStage6HandoffGuard();

            // The trigger SET itself, not just the message it produces. Each of these takes the
            // O(files) resident pool and so arms the guard above.
            AssertNeedsResidentPool(true, fdrbench1);
            AssertNeedsResidentPool(true, simple);
            // OSPREY_FDR_PROJECTION=0 is itself an explicit resident opt-in.
            Assert.IsTrue(PerFileScoringTask.NeedsResidentPool(lean, useFdrProjection: false));

            // Nothing else does. Context for OSPREY_PASS2_QVALUE=transfer, which #4438 took off
            // the list (the per-run-only redesign maps each adjusted peak through that file's
            // own 1st-pass score to run-q sidecar, one file at a time) and a #4446 merge
            // artifact silently put back, killing an 82-file transfer run on the guard in ~25 s:
            // the predicate is now env-free apart from the projection switch, so the triggers
            // are exactly the four above. That is what these two assertions pin.
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
                true, true, ResidentPaths.FDRBENCH_PASS1));

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
            string both = ResidentPaths.FDRBENCH_PASS1 + "," + ResidentPaths.COMPACTED_ENTRIES_BUFFER;
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(true, false, both));
            var benchCfg = new OspreyConfig { OutputFdrBench = "bench.tsv", FdrBenchPass = 1 };
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(benchCfg, true, both, true));
            // Separators are interchangeable and surrounding whitespace is tolerated - an
            // operator composing the value in a shell should not have to match a spelling.
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(
                true, false, " projection-off ; compacted-entries-buffer "));
            // A list still admits ONLY what it names: an unnamed path is refused as before.
            Assert.IsNotNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(
                true, false, ResidentPaths.PROJECTION_OFF + "," + ResidentPaths.FDRBENCH_PASS1));
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
            var scores = new[] { "a.scores.parquet" };

            // Straight-through (-i, no --task): FirstPassFDR runs.
            Assert.IsTrue(FirstPassFdrTask.IsIncludedFor(new OspreyConfig()));

            // --task PerFileScoring / PerFileRescoring set NoJoin: excluded, they stop before
            // the join.
            Assert.IsFalse(FirstPassFdrTask.IsIncludedFor(
                new OspreyConfig { NoJoin = true }));
            Assert.IsFalse(FirstPassFdrTask.IsIncludedFor(
                new OspreyConfig { NoJoin = true, InputScores = scores.ToList() }));

            // --task FirstPassFDR sets StopAfterStage5: it IS the first-pass node.
            Assert.IsTrue(FirstPassFdrTask.IsIncludedFor(
                new OspreyConfig { StopAfterStage5 = true, InputScores = scores.ToList() }));

            // The full --input-scores pipeline (no --task): runs.
            Assert.IsTrue(FirstPassFdrTask.IsIncludedFor(
                new OspreyConfig { InputScores = scores.ToList() }));

            // --task SecondPassFDR: NoJoin FALSE, so the old !NoJoin proxy said "runs" - but
            // ExpectReconciledInput excludes it. This single row is the whole change.
            Assert.IsFalse(FirstPassFdrTask.IsIncludedFor(
                new OspreyConfig { ExpectReconciledInput = true, InputScores = scores.ToList() }),
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
                new OspreyConfig { ExpectReconciledInput = true, InputScores = scores.ToList() },
                useFdrProjection: true));
        }
    }
}
