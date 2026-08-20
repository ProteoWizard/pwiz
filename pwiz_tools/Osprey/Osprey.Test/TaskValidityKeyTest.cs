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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks;
using pwiz.Osprey.Tasks.ModelDiagnostics;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Pins the two flipped defaults - the learned peak pick and the
    /// protein-compact 2nd-pass q-value mode - into the resume validity key.
    ///
    /// The regression this guards against is silent by construction. With a
    /// suffix absent, a run under the NEW defaults computes the same key as a
    /// directory recorded under the OLD ones, so the resume driver adopts the
    /// old arm's parquets, sidecars and .blib and reports them as the new arm's
    /// result. Nothing fails and nothing warns. The byte-identity regression
    /// gate cannot see it either, because that gate always runs in a fresh
    /// output directory - which is exactly why this has to be a unit test.
    /// </summary>
    [TestClass]
    public class TaskValidityKeyTest
    {
        [TestMethod]
        public void TestFlippedDefaultsParticipateInTheValidityKey()
        {
            AssertSuffixesAreUnconditional();
            AssertEachArmKeysDifferently();
            AssertEveryTaskCarriesTheSuffixesItNeeds();
            AssertLibraryFragmentArmIsPinnedToThePipeline();
            AssertDiagnosticsReportIsADeclaredOutputOnlyWhenAsked();
        }

        /// <summary>
        /// The <c>--model-diagnostics</c> report must be a DECLARED OUTPUT of the task that
        /// finalizes it, and only when the flag is on.
        ///
        /// <para>Declared, because that is the whole mechanism by which a completed run can
        /// regenerate a deleted report: task validity requires every declared output to exist,
        /// so a missing HTML invalidates SecondPassFDR alone, Stages 1-5 stay cached, and the
        /// pass-1 panel is rebuilt by rehydrating the 1st-pass sidecars. Before this the flag
        /// was INERT on a cached directory - it is in no validity key, the HTML was in no
        /// Outputs list, so re-running with it added skipped every task and produced no
        /// report at all.</para>
        ///
        /// <para>Only when asked, because declaring it unconditionally would leave every run
        /// that never wanted diagnostics permanently invalid, re-running SecondPassFDR on
        /// every resume forever.</para>
        /// </summary>
        private static void AssertDiagnosticsReportIsADeclaredOutputOnlyWhenAsked()
        {
            foreach (bool wanted in new[] { false, true })
            {
                var config = new OspreyConfig
                {
                    OutputBlib = @"C:\runs\out.blib",
                    ModelDiagnostics = wanted
                };
                var tasks = AnalysisPipeline.CanonicalPipeline();
                var ctx = new PipelineContext(config, tasks, null, null, null);
                OspreyTask second = null;
                foreach (var t in tasks)
                {
                    if (t.Name == @"SecondPassFDR")
                        second = t;
                }
                Assert.IsNotNull(second, @"SecondPassFDR must be in the canonical pipeline");

                bool declared = false;
                foreach (string o in second.Outputs(ctx))
                {
                    if (o != null && o.EndsWith(ModelDiagnosticsReport.HtmlSuffix, StringComparison.Ordinal))
                        declared = true;
                }
                Assert.AreEqual(wanted, declared, wanted
                    ? @"the report must be a declared output when --model-diagnostics is on, or a deleted report cannot be regenerated"
                    : @"the report must NOT be declared when --model-diagnostics is off, or every plain run is permanently invalid");
            }
        }

        /// <summary>
        /// Both suffixes must be emitted for EVERY arm, the default included.
        /// This is the property that separates them from
        /// <see cref="OspreyEnvironment.ExperimentAggValidityKeySuffix"/>, whose
        /// default arm deliberately emits nothing: that arm's output never
        /// changed, and these two arms' outputs did.
        /// </summary>
        private static void AssertSuffixesAreUnconditional()
        {
            Assert.AreNotEqual(string.Empty, OspreyEnvironment.PickValidityKeySuffix(),
                @"the pick suffix must be emitted for the default arm too - an empty default is precisely what lets a post-flip key equal a pre-flip one");
            Assert.AreNotEqual(string.Empty, OspreyEnvironment.Pass2QValueValidityKeySuffix(),
                @"the 2nd-pass mode suffix must be emitted for the default arm too, for the same reason");
        }

        /// <summary>
        /// Two configurations that produce different output must not share a
        /// key. Both pick levers count: the model PATH overrides the
        /// resolution-keyed default outright, so two runs can differ in nothing
        /// else.
        /// </summary>
        private static void AssertEachArmKeysDifferently()
        {
            Assert.AreNotEqual(OspreyEnvironment.PickValidityKeySuffix(true, null),
                OspreyEnvironment.PickValidityKeySuffix(false, null),
                @"the learned pick and the product-form pick must key differently");
            Assert.AreNotEqual(OspreyEnvironment.PickValidityKeySuffix(true, null),
                OspreyEnvironment.PickValidityKeySuffix(true, @"other-model.json"),
                @"an explicit pick model replaces the default model, so it must key differently");

            var modes = new[]
            {
                OspreyEnvironment.PASS2_QVALUE_TRANSFER,
                OspreyEnvironment.PASS2_QVALUE_TRANSFER_COMPETE,
                OspreyEnvironment.PASS2_QVALUE_PROTEIN_COMPACT
            };
            for (int i = 0; i < modes.Length; i++)
            {
                for (int j = i + 1; j < modes.Length; j++)
                {
                    Assert.AreNotEqual(OspreyEnvironment.Pass2QValueValidityKeySuffix(modes[i]),
                        OspreyEnvironment.Pass2QValueValidityKeySuffix(modes[j]), string.Format(
                            @"{0} and {1} report different q-values, so they must key differently",
                            modes[i], modes[j]));
                }
            }
        }

        /// <summary>
        /// Wiring, stated as a rule rather than a list so a task added later is
        /// covered: EVERY canonical task carries the pick, because the pick
        /// happens in Stage 4 and everything downstream inherits the peak it
        /// chose. Only per-file scoring is exempt from the 2nd-pass mode - its
        /// output is written before pass 2 exists, and keying it on the mode
        /// would re-run Stages 1-4 (hours at 82 files) to reproduce parquets
        /// that cannot have changed.
        /// </summary>
        private static void AssertEveryTaskCarriesTheSuffixesItNeeds()
        {
            var tasks = AnalysisPipeline.CanonicalPipeline();
            var ctx = new PipelineContext(new OspreyConfig(), tasks, null, null, null);
            string pick = OspreyEnvironment.PickValidityKeySuffix();
            string pass2 = OspreyEnvironment.Pass2QValueValidityKeySuffix();

            foreach (var task in tasks)
            {
                string key = task.ValidityKey(ctx);
                StringAssert.Contains(key, pick, string.Format(
                    @"{0} must key on the peak-pick arm", task.Name));
                bool expectPass2 = task.Name != @"PerFileScoring";
                Assert.AreEqual(expectPass2, key.Contains(pass2), string.Format(
                    @"{0} must {1} key on the 2nd-pass q-value mode",
                    task.Name, expectPass2 ? @"" : @"NOT "));
            }
        }

        /// <summary>
        /// The library-fragment arm, pinned to the pipeline by the same walk. It is hand-wired
        /// into three tasks, so without this, dropping any one of those lines stays green.
        ///
        /// <para>Asserted against a NON-EMPTY arm on purpose. The default arm emits nothing -
        /// deliberately, so shipping the feature invalidates no existing output directory - and
        /// a test written against that arm asserts nothing at all. Flipping the flag off is what
        /// makes the term observable.</para>
        ///
        /// <para>The term must also be DISTINCT from every other arm's, which
        /// <c>AreNotEqual(string.Empty, ...)</c> alone would not catch: a suffix that collided
        /// with a token another arm already emits would let two different configurations
        /// compute the same key, which is the failure the whole file exists to prevent.</para>
        /// </summary>
        private static void AssertLibraryFragmentArmIsPinnedToThePipeline()
        {
            bool saved = OspreyEnvironment.ReleaseLibraryFragments;
            try
            {
                OspreyEnvironment.ReleaseLibraryFragments = false;
                var tasks = AnalysisPipeline.CanonicalPipeline();
                var ctx = new PipelineContext(new OspreyConfig(), tasks, null, null, null);
                string libfrag = LibraryFragmentRelease.ValidityKeySuffix(ctx);

                Assert.AreNotEqual(string.Empty, libfrag,
                    @"the resident opt-out must emit a term, or an in-place A/B adopts the other arm's outputs");
                foreach (string other in new[]
                {
                    OspreyEnvironment.PickValidityKeySuffix(),
                    OspreyEnvironment.Pass2QValueValidityKeySuffix(),
                    OspreyEnvironment.ExperimentAggValidityKeySuffix(),
                    OspreyEnvironment.Stage6StreamSurvivorsValidityKeySuffix()
                })
                {
                    Assert.AreNotEqual(libfrag, other,
                        @"the library-fragment term must not collide with another arm's token");
                }

                // Per-file scoring is exempt for the reason it is exempt from the 2nd-pass mode:
                // its parquet is written in Stages 1-4, before any release can happen, and
                // keying it here would re-run hours of scoring to reproduce a byte-identical file.
                foreach (var task in tasks)
                {
                    bool expectLibfrag = task.Name != @"PerFileScoring";
                    Assert.AreEqual(expectLibfrag, task.ValidityKey(ctx).Contains(libfrag),
                        string.Format(@"{0} must {1}key on the library-fragment release arm",
                            task.Name, expectLibfrag ? string.Empty : @"NOT "));
                }
            }
            finally
            {
                OspreyEnvironment.ReleaseLibraryFragments = saved;
            }
        }
    }
}
