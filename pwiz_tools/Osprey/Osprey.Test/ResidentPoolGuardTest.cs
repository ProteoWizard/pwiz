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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for the resident first-pass pool guard
    /// (<see cref="PerFileScoringTask.ResidentPoolGuardError"/>): a run that would take the
    /// O(files) resident pool must fail fast with an actionable error naming the trigger,
    /// UNLESS the operator explicitly accepted unbounded memory
    /// (OSPREY_ALLOW_UNBOUNDED_MEMORY, or OSPREY_FDR_PROJECTION=0 which forces the resident
    /// A/B-oracle path). So no user reaches an O(files) memory path by accident.
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
                allowUnbounded: false, useFdrProjection: true));

            // HPC reconciled-input merge trips the fat pool: guarded (armed), and the message is
            // actionable -- it names the trigger AND the env var the operator would set.
            var hpc = new OspreyConfig { ExpectReconciledInput = true };
            string hpcErr = PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnbounded: false, useFdrProjection: true);
            Assert.IsNotNull(hpcErr);
            StringAssert.Contains(hpcErr, "OSPREY_ALLOW_UNBOUNDED_MEMORY");
            StringAssert.Contains(hpcErr, "reconciled-input merge");

            // Both explicit opt-ins exempt the same fat path (no error):
            //   OSPREY_ALLOW_UNBOUNDED_MEMORY (allowUnbounded == true)
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnbounded: true, useFdrProjection: true));
            //   OSPREY_FDR_PROJECTION=0 (useFdrProjection == false, the A/B-oracle switch)
            Assert.IsNull(PerFileScoringTask.ResidentPoolGuardError(hpc, needsResidentPool: true,
                allowUnbounded: false, useFdrProjection: false));

            // Each user-reachable trigger names itself so the failure is diagnosable:
            var mdiag = new OspreyConfig { ModelDiagnostics = true };
            StringAssert.Contains(
                PerFileScoringTask.ResidentPoolGuardError(mdiag, true, false, true), "--model-diagnostics");

            var fdrbench1 = new OspreyConfig { OutputFdrBench = "bench.tsv", FdrBenchPass = 1 };
            StringAssert.Contains(
                PerFileScoringTask.ResidentPoolGuardError(fdrbench1, true, false, true), "--fdrbench-pass 1");

            var simple = new OspreyConfig { FdrMethod = FdrMethod.Simple };
            StringAssert.Contains(
                PerFileScoringTask.ResidentPoolGuardError(simple, true, false, true), "non-Percolator");
        }
    }
}
