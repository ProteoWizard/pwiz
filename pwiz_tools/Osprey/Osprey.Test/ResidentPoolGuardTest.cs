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

            // HPC reconciled-input merge trips the fat pool: guarded (armed), and the message is
            // actionable - it names the token the operator would set, not just a symptom.
            var hpc = new OspreyConfig { ExpectReconciledInput = true };
            string hpcErr = PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnfixedResident: null, useFdrProjection: true);
            Assert.IsNotNull(hpcErr);
            StringAssert.Contains(hpcErr, "OSPREY_ALLOW_UNFIXED_RESIDENT=" + ResidentPaths.HPC_MERGE);

            // Naming THIS path exempts it (no error):
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.HPC_MERGE, useFdrProjection: true));
            // OSPREY_FDR_PROJECTION=0 (the A/B byte-identity oracle) is NOT an automatic
            // exemption any more - it is its own token. Unnamed it is refused like anything
            // else, which closes the last route to a resident pool nobody had to ask for.
            Assert.IsNotNull(PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnfixedResident: null, useFdrProjection: false));
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.PROJECTION_OFF, useFdrProjection: false));
            // It outranks a config-driven trigger, because it selects the legacy implementation
            // for the whole run: naming the other reason is not enough.
            Assert.IsNotNull(PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.HPC_MERGE, useFdrProjection: false));

            // Naming a DIFFERENT path does not: the token grants one exemption, not amnesty.
            // This is the property the former blanket boolean lacked.
            Assert.IsNotNull(PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.FDRBENCH_PASS1, useFdrProjection: true));

            // Capitalization does not defeat it - the error names the exact token to set, so
            // rejecting the operator's own value for case would read as the guard ignoring them.
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.HPC_MERGE.ToUpperInvariant(),
                useFdrProjection: true));

            // Each user-reachable trigger names its own token so the failure is diagnosable.
            // --model-diagnostics is NOT among them any more (#4505): it armed the pool on a
            // full resume, where FirstJoin skipped its score pass and reported off the resident
            // entries, and FirstJoin's rehydrate now streams that report instead.
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

            var fdrbench1 = new OspreyConfig { OutputFdrBench = "bench.tsv", FdrBenchPass = 1 };
            StringAssert.Contains(PerFileScoringTask.ResidentPoolGuardError(fdrbench1, true, null, true),
                ResidentPaths.FDRBENCH_PASS1);

            var simple = new OspreyConfig { FdrMethod = FdrMethod.Simple };
            StringAssert.Contains(PerFileScoringTask.ResidentPoolGuardError(simple, true, null, true),
                ResidentPaths.NON_PERCOLATOR_FDR);

            // A resident path with NO token is refused unconditionally - no value admits it.
            // This is the ratchet: when something we streamed goes resident again, as transfer
            // did, it cannot be waved through. It has to be fixed, or deliberately listed.
            // (lean is the default config: Percolator, no fdrbench, no mdiag, not a merge.)
            foreach (string token in new[] { null, "", ResidentPaths.HPC_MERGE, "anything" })
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
            // 'mdiag-full-resume' is GONE (#4505) - the ratchet shrinking.
            // 'resume-survivor-handoff' is ADDED (#4536), which the class remarks allow only
            // because it names a path that was previously unnamed AND unguarded, not one that
            // had been fixed.
            CollectionAssert.AreEqual(
                new[]
                {
                    "hpc-merge", "fdrbench-pass1", "non-percolator-fdr",
                    "projection-off", "compacted-entries-buffer", "resume-survivor-handoff"
                },
                ResidentPaths.KNOWN_UNFIXED.ToArray());

            // The POST-compaction handoff guard (issue #4526). The guard above stops at the
            // compaction line, so the all-files survivor buffer Stage 5 hands to Stage 6 - 28 GB
            // at 163 files, live for the whole rescore - was never named and no token could
            // refuse it. Streaming it is the default; the resident opt-out is a named path.
            AssertStage6HandoffGuard();

            // The trigger SET itself, not just the message it produces. Each of these takes the
            // O(files) resident pool and so arms the guard above.
            AssertNeedsResidentPool(true, hpc);
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
                true, true, ResidentPaths.HPC_MERGE));

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

            AssertResumeHandoffGuard();

            // A run that cannot stream at all (no per-file survivor source: the legacy resident
            // and rehydrate paths never compute the passing base_id set) is NOT guarded here.
            // It is already resident for a reason carrying its own token, and demanding a
            // second one would make a single decision need two environment variables.
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(false, false, null));

            // SEVERAL paths may be named at once. A run can legitimately trip more than one,
            // and a single-value variable made that run impossible: an operator proving the
            // streamed Stage 6 handoff bounded on a configuration that is already resident for
            // its own reason needs that run's token AND compacted-entries-buffer, so the very
            // A/B that establishes the bound aborted on its own guard. Both guards read the
            // list, and every admitted path is still named individually.
            string both = ResidentPaths.HPC_MERGE + "," + ResidentPaths.COMPACTED_ENTRIES_BUFFER;
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(true, false, both));
            var hpcCfg = new OspreyConfig { ExpectReconciledInput = true };
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(hpcCfg, true, both, true));
            // Separators are interchangeable and surrounding whitespace is tolerated - an
            // operator composing the value in a shell should not have to match a spelling.
            Assert.IsNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(
                true, false, " projection-off ; compacted-entries-buffer "));
            // A list still admits ONLY what it names: an unnamed path is refused as before.
            Assert.IsNotNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(
                true, false, ResidentPaths.PROJECTION_OFF + "," + ResidentPaths.HPC_MERGE));
        }

        /// <summary>
        /// The RESUME Stage 6 handoff guard: a straight-through resume rebuilding its bundle
        /// from its own sidecars cannot stream the survivor handoff, so it must NAME
        /// <see cref="ResidentPaths.RESUME_SURVIVOR_HANDOFF"/> or be refused.
        ///
        /// <para>Pins both halves of the rule, because only having both is the policy: omit
        /// the token and the run FAILS with an actionable message; name it and the run
        /// proceeds (the caller then warns). A warning alone on an untokened default path is
        /// what this guard exists to stop being possible - that state shipped briefly while
        /// #4505 was in review, and is exactly how an O(files) path reaches a user who never
        /// asked for one.</para>
        /// </summary>
        private static void AssertResumeHandoffGuard()
        {
            // Unnamed: refused, and the message names the token to set plus the file count
            // that makes it O(files), so the operator can judge the cost rather than guess.
            string err = PerFileScoringTask.ResumeResidentHandoffGuardError(82, null, false);
            Assert.IsNotNull(err);
            StringAssert.Contains(err,
                "OSPREY_ALLOW_UNFIXED_RESIDENT=" + ResidentPaths.RESUME_SURVIVOR_HANDOFF);
            StringAssert.Contains(err, "82");

            // Named: admitted. Case-insensitive and list-tolerant, like every other token.
            Assert.IsNull(PerFileScoringTask.ResumeResidentHandoffGuardError(
                82, ResidentPaths.RESUME_SURVIVOR_HANDOFF, false));
            Assert.IsNull(PerFileScoringTask.ResumeResidentHandoffGuardError(
                82, ResidentPaths.RESUME_SURVIVOR_HANDOFF.ToUpperInvariant(), false));
            Assert.IsNull(PerFileScoringTask.ResumeResidentHandoffGuardError(
                82, ResidentPaths.PROJECTION_OFF + "," + ResidentPaths.RESUME_SURVIVOR_HANDOFF, false));

            // COMPACTED_ENTRIES_BUFFER must NOT admit it, and this pair of assertions is what
            // the separate-token decision rests on. Both name the same physical buffer, so
            // reusing it here would have been the convenient choice - and would have meant any
            // leg admitting this unfixed resume ALSO admitted an
            // OSPREY_STAGE6_STREAM_SURVIVORS=0 regression on the computed path that #4530
            // already closed. One token, one path, or the high-water mark leaks.
            string sharedToken = PerFileScoringTask.ResumeResidentHandoffGuardError(
                82, ResidentPaths.COMPACTED_ENTRIES_BUFFER, false);
            Assert.IsNotNull(sharedToken);
            StringAssert.Contains(sharedToken, ResidentPaths.COMPACTED_ENTRIES_BUFFER);
            // ... and symmetrically, this run's token does not re-open the computed-path
            // handoff that #4530 fixed.
            Assert.IsNotNull(PerFileScoringTask.Stage6ResidentHandoffGuardError(
                true, false, ResidentPaths.RESUME_SURVIVOR_HANDOFF));

            // A DIFFERENT token does not admit it, and the message says what was supplied so
            // a stale value does not read like an unset one.
            string wrong = PerFileScoringTask.ResumeResidentHandoffGuardError(
                82, ResidentPaths.HPC_MERGE, false);
            Assert.IsNotNull(wrong);
            StringAssert.Contains(wrong, ResidentPaths.HPC_MERGE);

            // Single-file join: "all files" is one file, so there is no O(files) growth to
            // refuse and an ordinary one-file resume must not demand a token to run.
            Assert.IsNull(PerFileScoringTask.ResumeResidentHandoffGuardError(1, null, false));
            Assert.IsNull(PerFileScoringTask.ResumeResidentHandoffGuardError(0, null, false));
            // Two IS O(files) - the threshold is "more than one", not "many". A guard that
            // waited for a big number would let the growth establish itself unnoticed.
            Assert.IsNotNull(PerFileScoringTask.ResumeResidentHandoffGuardError(2, null, false));

            // A run already resident under a token it HAD to name (projection-off,
            // fdrbench-pass1, ...) is exempt with no token of its own. Without this, an
            // OSPREY_FDR_PROJECTION=0 resume needed TWO tokens and neither message mentioned
            // the other, so setting the one it named failed again naming a different one.
            Assert.IsNull(PerFileScoringTask.ResumeResidentHandoffGuardError(82, null, true));
            Assert.IsNull(PerFileScoringTask.ResumeResidentHandoffGuardError(
                82, ResidentPaths.PROJECTION_OFF, true));
        }
    }
}
