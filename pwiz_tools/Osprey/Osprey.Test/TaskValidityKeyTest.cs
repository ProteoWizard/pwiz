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
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.ML;
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
            AssertTrainingSampleLeversKeyDifferently();
            AssertEveryTaskCarriesTheSuffixesItNeeds();
            AssertLibraryFragmentArmIsPinnedToThePipeline();
            AssertDiagnosticsReportIsADeclaredOutputOnlyWhenAsked();
            AssertTreeClassifierKeysTheModelTasks();
        }

        /// <summary>
        /// <c>--fdr-method gbdt</c> must key the three tasks whose output the first-pass model
        /// determines - FirstPassFDR trains it, PerFileRescoring and SecondPassFDR score with it -
        /// and nothing else, and must leave every percolator key exactly as it was.
        ///
        /// <para>Written after the omission let one arm adopt the other: a percolator directory
        /// re-run as gbdt, or a gbdt directory written while the lean first pass still trained
        /// the SVM under the gbdt flag, reported "skipping (outputs valid)" and handed back the
        /// linear model's results as the trees'. An OSPREY_GBT_* sweep had the same hole one
        /// setting at a time, so every tree setting that changes the model must key too.</para>
        /// </summary>
        private static void AssertTreeClassifierKeysTheModelTasks()
        {
            var tasks = OspreyTasks.Create().Pipeline;
            var linearCtx = new PipelineContext(new OspreyConfig(), tasks, null, null, null);
            var treeCtx = new PipelineContext(new OspreyConfig { FdrMethod = FdrMethod.Gbdt }, tasks, null, null, null);
            string treeTerm = PercolatorEngine.GbdtValidityKeySuffix(treeCtx.Config);
            Assert.AreNotEqual(string.Empty, treeTerm, @"gbdt must emit a term");
            foreach (var method in new[] { FdrMethod.Percolator, FdrMethod.Mokapot, FdrMethod.Simple })
            {
                Assert.AreEqual(string.Empty, PercolatorEngine.GbdtValidityKeySuffix(new OspreyConfig { FdrMethod = method }),
                    method + @" must emit nothing, or every existing output directory is invalidated");
            }

            // The term is the ONLY difference: a percolator key is the gbdt key without it.
            foreach (var task in tasks)
            {
                bool expectTerm = task.Name == FirstPassFdrTask.TASK_NAME ||
                                  task.Name == PerFileRescoreTask.TASK_NAME ||
                                  task.Name == SecondPassFdrTask.TASK_NAME;
                string linearKey = task.ValidityKey(linearCtx);
                Assert.AreEqual(expectTerm ? linearKey + treeTerm : linearKey, task.ValidityKey(treeCtx),
                    string.Format(@"{0} must {1}key on the classifier", task.Name, expectTerm ? string.Empty : @"NOT "));
            }

            AssertEveryTreeSettingKeysDifferently();
        }

        /// <summary>
        /// Every field of <see cref="GbtParams"/> but the thread count changes the trained model,
        /// so each must key, and key distinctly. Walked by reflection so a field added later is
        /// covered without this list having to know about it.
        /// </summary>
        private static void AssertEveryTreeSettingKeysDifferently()
        {
            const int iterations = OspreyEnvironment.GBT_MAX_ITERATIONS_DEFAULT;
            const int innerFolds = 5;
            string defaultKey = PercolatorEngine.GbdtValidityKeySuffix(FdrMethod.Gbdt, new GbtParams(), iterations, innerFolds);
            Assert.AreEqual(defaultKey, PercolatorEngine.GbdtValidityKeySuffix(FdrMethod.Gbdt, new GbtParams(), iterations, innerFolds),
                @"the term must be a pure function of its settings");
            var keys = new HashSet<string> { defaultKey };
            foreach (var field in typeof(GbtParams).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var changed = new GbtParams();
                field.SetValue(changed, OtherValue(field.GetValue(changed)));
                string key = PercolatorEngine.GbdtValidityKeySuffix(FdrMethod.Gbdt, changed, iterations, innerFolds);
                if (field.Name == nameof(GbtParams.MaxDegreeOfParallelism))
                {
                    Assert.AreEqual(defaultKey, key, @"training is bit-identical at any thread count, so it must not key");
                    continue;
                }
                Assert.IsTrue(keys.Add(key), field.Name + @" must key, and distinctly from every other setting");
            }
            Assert.IsTrue(keys.Add(PercolatorEngine.GbdtValidityKeySuffix(FdrMethod.Gbdt, new GbtParams(), iterations + 1, innerFolds)),
                @"the tree iteration cap (OSPREY_GBT_MAX_ITERATIONS) must key");
            Assert.IsTrue(keys.Add(PercolatorEngine.GbdtValidityKeySuffix(FdrMethod.Gbdt, new GbtParams(), iterations, 1)),
                @"the inner-fold count (OSPREY_GBT_INNER_FOLDS) must key");
        }

        /// <summary>A value different from <paramref name="value"/>, of its type.</summary>
        private static object OtherValue(object value)
        {
            if (value is int i)
                return i + 1;
            if (value is double d)
                return d + 0.5;
            if (value is ulong u)
                return u + 1;
            if (value is GbtObjective objective)
                return objective == GbtObjective.LogisticBinary ? GbtObjective.SquaredError : GbtObjective.LogisticBinary;
            Assert.Fail(@"no alternative value for a GbtParams field of type " + value.GetType().Name);
            return null;
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
                var tasks = OspreyTasks.Create().Pipeline;
                var ctx = new PipelineContext(config, tasks, null, null, null);
                OspreyTask second = null;
                foreach (var t in tasks)
                {
                    if (t.Name == SecondPassFdrTask.TASK_NAME)
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
            Assert.AreNotEqual(string.Empty, OspreyEnvironment.TrainSampleValidityKeySuffix(),
                @"the training-selection suffix must be emitted for the default arm too - it is a flipped default, so an empty new default would equal every pre-flip key");
        }

        /// <summary>
        /// The first-pass training-sample settings must key differently from each other and from
        /// the default, because they change which rows train the model and therefore every score
        /// and count downstream.
        ///
        /// <para>Written after the omission cost a measurement: a re-run with
        /// OSPREY_TRAIN_PICK_RUN newly set into an existing output directory reported
        /// "FirstPassFDR:skipping (outputs valid)" in under a second and handed back the previous
        /// setting's numbers. That is indistinguishable from a change with no effect, which is the
        /// most expensive way for an A/B to fail.</para>
        ///
        /// <para>The two halves are asserted differently on purpose. The run-vs-maximum selection
        /// is a FLIPPED DEFAULT, so it must key for BOTH arms - an empty new default would equal
        /// every directory written before the flip and let a resume adopt maximum-trained scores.
        /// The training cap's default never moved, so it must stay silent when unset, exactly
        /// like the aggregation suffix.</para>
        /// </summary>
        private static void AssertTrainingSampleLeversKeyDifferently()
        {
            Assert.AreNotEqual(string.Empty, OspreyEnvironment.TrainSampleValidityKeySuffix(true, null),
                @"the shipped reservoir arm must key, or a pre-flip directory is adopted as though it had been trained on it");
            Assert.AreNotEqual(string.Empty, OspreyEnvironment.TrainSampleValidityKeySuffix(false, null),
                @"the forced-maximum arm must key too");
            Assert.AreNotEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(false, null),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, null),
                @"per-run picking trains on different rows than the cross-run maximum");
            Assert.AreEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(true, null),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, null),
                @"the suffix must be a pure function of its arms");
            Assert.AreNotEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(true, null),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, 3000000),
                @"a raised training cap covers more precursors, so it must key differently");
            Assert.AreNotEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(true, 3000000),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, 600000),
                @"two different caps must key differently, not merely differ from the default");
            Assert.AreNotEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(false, 3000000),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, 3000000),
                @"the two settings are independent, so their combination is a fourth arm");
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
            var tasks = OspreyTasks.Create().Pipeline;
            var ctx = new PipelineContext(new OspreyConfig(), tasks, null, null, null);
            string pick = OspreyEnvironment.PickValidityKeySuffix();
            string pass2 = OspreyEnvironment.Pass2QValueValidityKeySuffix();
            // The training-selection arm is hand-wired into the same three tasks the
            // library-fragment arm is, so without it here any one of those lines can be dropped in
            // a merge and the suite stays green - and the failure it lets through is precisely the
            // "skipping (outputs valid)" adoption of another selection's numbers that this file
            // exists to prevent. Same exemption shape: the tasks that run before a model is
            // trained cannot key on how it was trained.
            string train = OspreyEnvironment.TrainSampleValidityKeySuffix();

            foreach (var task in tasks)
            {
                string key = task.ValidityKey(ctx);
                StringAssert.Contains(key, pick, string.Format(
                    @"{0} must key on the peak-pick arm", task.Name));
                bool expectPass2 = task.Name != PerFileScoringTask.TASK_NAME;
                Assert.AreEqual(expectPass2, key.Contains(pass2), string.Format(
                    @"{0} must {1} key on the 2nd-pass q-value mode",
                    task.Name, expectPass2 ? @"" : @"NOT "));
                bool expectTrain = task.Name == FirstPassFdrTask.TASK_NAME ||
                                   task.Name == PerFileRescoreTask.TASK_NAME ||
                                   task.Name == SecondPassFdrTask.TASK_NAME;
                Assert.AreEqual(expectTrain, key.Contains(train), string.Format(
                    @"{0} must {1} key on the first-pass training selection",
                    task.Name, expectTrain ? @"" : @"NOT "));
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
                var tasks = OspreyTasks.Create().Pipeline;
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
                    bool expectLibfrag = task.Name != PerFileScoringTask.TASK_NAME;
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
