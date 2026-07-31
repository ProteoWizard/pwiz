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
            // actionable -- it names the token the operator would set, not just a symptom.
            var hpc = new OspreyConfig { ExpectReconciledInput = true };
            string hpcErr = PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnfixedResident: null, useFdrProjection: true);
            Assert.IsNotNull(hpcErr);
            StringAssert.Contains(hpcErr, "OSPREY_ALLOW_UNFIXED_RESIDENT=" + ResidentPaths.HPC_MERGE);

            // Naming THIS path exempts it (no error):
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.HPC_MERGE, useFdrProjection: true));
            // OSPREY_FDR_PROJECTION=0 (the A/B byte-identity oracle) is NOT an automatic
            // exemption any more -- it is its own token. Unnamed it is refused like anything
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

            // Capitalization does not defeat it -- the error names the exact token to set, so
            // rejecting the operator's own value for case would read as the guard ignoring them.
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnfixedResident: ResidentPaths.HPC_MERGE.ToUpperInvariant(),
                useFdrProjection: true));

            // Each user-reachable trigger names its own token so the failure is diagnosable:
            var mdiag = new OspreyConfig { ModelDiagnostics = true };
            StringAssert.Contains(PerFileScoringTask.ResidentPoolGuardError(mdiag, true, null, true),
                ResidentPaths.MDIAG_FULL_RESUME);

            var fdrbench1 = new OspreyConfig { OutputFdrBench = "bench.tsv", FdrBenchPass = 1 };
            StringAssert.Contains(PerFileScoringTask.ResidentPoolGuardError(fdrbench1, true, null, true),
                ResidentPaths.FDRBENCH_PASS1);

            var simple = new OspreyConfig { FdrMethod = FdrMethod.Simple };
            StringAssert.Contains(PerFileScoringTask.ResidentPoolGuardError(simple, true, null, true),
                ResidentPaths.NON_PERCOLATOR_FDR);

            // A resident path with NO token is refused unconditionally -- no value admits it.
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
            CollectionAssert.AreEqual(
                new[]
                {
                    ResidentPaths.HPC_MERGE, ResidentPaths.FDRBENCH_PASS1,
                    ResidentPaths.MDIAG_FULL_RESUME, ResidentPaths.NON_PERCOLATOR_FDR,
                    ResidentPaths.PROJECTION_OFF
                },
                ResidentPaths.KNOWN_UNFIXED.ToArray());

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
    }
}
