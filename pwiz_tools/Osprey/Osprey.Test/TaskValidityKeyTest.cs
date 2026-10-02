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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;
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
            AssertTrainingExportKey();
            AssertTrainingExportKeyFollowsEachRunsInputs();
            AssertLibraryTermsKeyOnlyWhatChanged();
        }

        /// <summary>
        /// Each library term keys what its change reaches, and no more:
        /// <list type="bullet">
        /// <item>every blib is read differently since the reader started typing its peaks from
        /// m/z (and reading modification text residue- and precision-aware), so every task of a
        /// blib search keys on <see cref="OspreyTask.BLIB_READER_TERM"/>, and the
        /// <c>.libcache</c> composition carries the reader version with the fragment tolerance
        /// the cached types were computed within;</item>
        /// <item>a DIA-NN TSV search keys exactly as before - its columns are still the typing -
        /// while its <c>.libcache</c> is re-read once through the validating reader;</item>
        /// <item>the output blib's rows changed for every library, so SecondPassFDR keys on
        /// <c>;blibout</c> for both.</item>
        /// </list>
        /// </summary>
        private static void AssertLibraryTermsKeyOnlyWhatChanged()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"osprey_libterms_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                string tsv = Path.Combine(dir, @"library.tsv");
                File.WriteAllText(tsv, @"not read");
                string blib = Path.Combine(dir, @"library.blib");
                File.WriteAllText(blib, @"not read");

                var tsvConfig = new OspreyConfig { LibrarySource = LibrarySource.FromPath(tsv) };
                var tsvKeys = TaskKeys(tsvConfig);
                Assert.AreEqual(PreUpgradeBaseKey(tsvConfig), tsvKeys[PerFileScoringTask.TASK_NAME],
                    @"a TSV search keys as before the reader change");
                foreach (var key in tsvKeys)
                    Assert.IsFalse(key.Value.Contains(OspreyTask.BLIB_READER_TERM), key.Key);
                Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, "tsv_reader:{0}\n", DiannTsvLoader.READER_VERSION),
                    LibraryLoader.LibraryReaderTerms(tsvConfig));

                var blibConfig = new OspreyConfig { LibrarySource = LibrarySource.FromPath(blib) };
                var blibKeys = TaskKeys(blibConfig);
                Assert.AreEqual(PreUpgradeBaseKey(blibConfig) + OspreyTask.BLIB_READER_TERM,
                    blibKeys[PerFileScoringTask.TASK_NAME]);
                foreach (var key in blibKeys)
                    StringAssert.Contains(key.Value, OspreyTask.BLIB_READER_TERM, key.Key + @" must key on the blib reader");
                Assert.AreEqual(BlibLoader.CacheTerm(blibConfig.FragmentTolerance), LibraryLoader.LibraryReaderTerms(blibConfig));
                // The cached types were computed within the fragment tolerance, so another
                // tolerance is another cache (the task keys follow it through the search hash).
                var unitConfig = new OspreyConfig
                {
                    LibrarySource = LibrarySource.FromPath(blib),
                    FragmentTolerance = FragmentToleranceConfig.UnitResolution(0.5)
                };
                Assert.AreNotEqual(LibraryLoader.LibraryReaderTerms(blibConfig), LibraryLoader.LibraryReaderTerms(unitConfig));
                Assert.AreNotEqual(blibConfig.Identity.SearchParameterHash(), unitConfig.Identity.SearchParameterHash());

                foreach (var config in new[] { tsvConfig, blibConfig })
                {
                    StringAssert.Contains(TaskKeys(config)[SecondPassFdrTask.TASK_NAME],
                        @";blibout=" + BlibSpectrum.FORMAT_VERSION);
                }
            }
            finally
            {
                DeleteTempDirectory(dir);
            }
        }

        /// <summary>Every pipeline task's validity key for <paramref name="config"/>, by task name.</summary>
        private static Dictionary<string, string> TaskKeys(OspreyConfig config)
        {
            var tasks = OspreyTasks.Create().Pipeline;
            var ctx = new PipelineContext(config, tasks, null, null, null);
            return tasks.ToDictionary(t => t.Name, t => t.ValidityKey(ctx));
        }

        /// <summary>
        /// Removes a test's temp folder best effort, so a lingering SQLite handle cannot replace
        /// the assertion that failed with an IOException from a finally block.
        /// </summary>
        private static void DeleteTempDirectory(string dir)
        {
            foreach (string file in Directory.GetFiles(dir))
                BlibLibraryInputTest.TryDeleteFile(file);
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
                // A test's temp folder; a lingering handle must not fail the test.
            }
        }

        /// <summary>
        /// A run's export is PerFileRescoring's product, and it reads that run's reconciled
        /// parquet, one q-value sidecar, calibration and spectra cache, so each one's identity
        /// keys that run's export - absent and present differ, and so do two versions of one file
        /// - while another run's export, the task key and the task's other outputs do not move.
        /// The q-value sidecar is the second-pass one only when PerFileRescoring wrote it - its
        /// stamp and its decoys file. One SecondPassFDR writes after the export (a run with no
        /// Stage 6 work, or no readable first-pass model) is not read and must not key it, even
        /// once the driver has stamped it under PerFileRescoring's name, or the export flips to
        /// it on a later invocation of the same command.
        /// </summary>
        private static void AssertTrainingExportKeyFollowsEachRunsInputs()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"osprey_trainrun_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            string savedOutput = ArtifactPaths.OutputDir;
            string savedCache = ArtifactPaths.CacheDir;
            try
            {
                ArtifactPaths.OutputDir = dir;
                ArtifactPaths.CacheDir = dir;
                string runA = Path.Combine(dir, @"a.mzML");
                string runB = Path.Combine(dir, @"b.mzML");
                var config = TaskConfigs.StraightThrough();
                config.InputFiles = new List<string> { runA, runB };
                config.LibrarySource = LibrarySource.FromPath(@"ref.tsv");
                config.OutputBlib = Path.Combine(dir, @"out.blib");
                config.TrainingExport.Enabled = true;
                var ctx = TaskConfigs.ContextFor(config);
                var task = config.Pipeline.OfType<PerFileRescoreTask>().Single();
                string key = task.ValidityKey(ctx);
                string RunKey(string input) => task.OutputValidityKey(ctx, key, TrainingExportParquet.PathFor(input));

                string otherRun = RunKey(runB);
                string current = RunKey(runA);
                foreach (string artifact in new[]
                         {
                             ParquetScoreCache.GetReconciledScoresPath(runA),
                             FdrScoresSidecar.Pass1Path(runA),
                             CalibrationIO.CalibrationPathForInput(runA, ArtifactPaths.ResolveOutputDir(runA)),
                             SpectraCache.GetCachePath(runA),
                         })
                {
                    current = AssertRewritesInvalidate(artifact, current, () => RunKey(runA));
                    Assert.AreEqual(otherRun, RunKey(runB), @"another run's export does not depend on this run's files");
                }

                string pass2 = FdrScoresSidecar.Pass2Path(runA);
                File.WriteAllText(pass2, @"SecondPassFDR's");
                Assert.AreEqual(current, RunKey(runA), @"a second-pass sidecar PerFileRescoring did not write is not read, so it must not key the export");
                Assert.AreEqual(FdrScoresSidecar.Pass1Path(runA), TrainingExportWriter.RunQPath(runA, out var pass));
                Assert.AreEqual(FdrScoresSidecar.Pass.FirstPass, pass);
                // The driver stamps every declared output that exists after a PerFileRescoring
                // run, so SecondPassFDR's sidecar can carry a PerFileRescoring stamp it did not
                // earn. The worker's decoys file is what the driver cannot supply.
                File.WriteAllText(TaskValiditySidecar.PathFor(pass2, PerFileRescoreTask.TASK_NAME), @"stamp");
                Assert.AreEqual(current, RunKey(runA), @"a PerFileRescoring stamp without the worker's decoys must not flip the export to the second pass");
                Assert.AreEqual(FdrScoresSidecar.Pass1Path(runA), TrainingExportWriter.RunQPath(runA, out pass));
                File.WriteAllText(Pass2CompetitionDecoys.PathFor(runA), @"decoys");
                Assert.AreEqual(pass2, TrainingExportWriter.RunQPath(runA, out pass), @"the worker's stamp and decoys make the second pass the one read");
                Assert.AreEqual(FdrScoresSidecar.Pass.SecondPass, pass);
                Assert.AreNotEqual(current, RunKey(runA), @"switching the sidecar read must invalidate the run's export");
                current = AssertRewritesInvalidate(pass2, RunKey(runA), () => RunKey(runA));
                File.WriteAllText(FdrScoresSidecar.Pass1Path(runA), @"no longer read");
                Assert.AreEqual(current, RunKey(runA), @"the first-pass sidecar is not read once the second pass is");
                Assert.AreEqual(otherRun, RunKey(runB), @"another run's export does not depend on this run's files");

                Assert.AreEqual(key, task.ValidityKey(ctx), @"the per-run identities belong to the run's export, not the task key (P4)");
                string reconciled = ParquetScoreCache.GetReconciledScoresPath(runA);
                Assert.AreEqual(key, task.OutputValidityKey(ctx, key, reconciled),
                    @"the task's other outputs key on the task key alone");
            }
            finally
            {
                ArtifactPaths.OutputDir = savedOutput;
                ArtifactPaths.CacheDir = savedCache;
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// Writes <paramref name="artifact"/> and then rewrites it longer, asserting that each
        /// moves the key <paramref name="runKey"/> computes; returns the key after the rewrite.
        /// </summary>
        private static string AssertRewritesInvalidate(string artifact, string current, Func<string> runKey)
        {
            File.WriteAllText(artifact, @"first");
            string written = runKey();
            Assert.AreNotEqual(current, written, Path.GetFileName(artifact) + @" appearing must invalidate the run's export");
            File.WriteAllText(artifact, @"rewritten, and longer");
            string rewritten = runKey();
            Assert.AreNotEqual(written, rewritten, Path.GetFileName(artifact) + @" rewritten must invalidate the run's export");
            return rewritten;
        }

        /// <summary>
        /// The training export is a declared output of PerFileRescoring only when asked for, and
        /// asking for it moves no other key: PerFileRescoring's own key is the same with the flag
        /// or without, so adding the flag to a finished analysis leaves every other output
        /// valid and only the exports outstanding (P17). The export's key follows each export
        /// setting, and the straight-through run and a <c>--task TrainingExport</c> selector
        /// compute the same key, so a pay-later export is never redone.
        /// </summary>
        private static void AssertTrainingExportKey()
        {
            var off = TaskConfigs.StraightThrough();
            off.InputFiles = new List<string> { @"a.mzML" };
            off.LibrarySource = LibrarySource.FromPath(@"ref.tsv");
            off.OutputBlib = @"out.blib";
            var offCtx = TaskConfigs.ContextFor(off);
            var offTask = off.Pipeline.OfType<PerFileRescoreTask>().Single();
            string export = TrainingExportParquet.PathFor(@"a.mzML");
            Assert.IsFalse(offTask.Outputs(offCtx).Contains(export), @"no export is declared with the flag off");

            string straight = ExportKey(TaskConfigs.StraightThrough(), c => { }, out string taskKeyOn, out bool declared);
            Assert.IsTrue(declared, @"the export is a declared output of PerFileRescoring under the flag");
            Assert.AreEqual(offTask.ValidityKey(offCtx), taskKeyOn, @"the flag must not move PerFileRescoring's key");
            Assert.AreEqual(straight, ExportKey(TaskConfigs.ForTask(TrainingExportTask.TASK_NAME), c => { }, out _, out _),
                OspreyArgNames.TaskText(TrainingExportTask.TASK_NAME) + @" must compute the straight-through key");
            Assert.AreEqual(straight, ExportKey(TaskConfigs.StraightThrough(), c => c.TrainingExport.MaxQ = c.RunFdr, out _, out _),
                @"an explicit max-q equal to the default is the same export");
            foreach (var change in new Action<OspreyConfig>[]
                     {
                         c => c.TrainingExport.MaxQ = 0.05,
                         c => c.TrainingExport.ClaimantQ = 0.05,
                         c => c.TrainingExport.WriteXics = true,
                         c => c.RunFdr = 0.05,
                     })
            {
                Assert.AreNotEqual(straight, ExportKey(TaskConfigs.StraightThrough(), change, out _, out _),
                    @"an export setting must change the key");
            }
        }

        private static string ExportKey(OspreyConfig config, Action<OspreyConfig> mutate, out string taskKey, out bool declared)
        {
            config.InputFiles = new List<string> { @"a.mzML" };
            config.LibrarySource = LibrarySource.FromPath(@"ref.tsv");
            config.OutputBlib = @"out.blib";
            config.TrainingExport.Enabled = true;
            mutate(config);
            var ctx = TaskConfigs.ContextFor(config);
            var task = config.Pipeline.OfType<PerFileRescoreTask>().Single();
            string export = TrainingExportParquet.PathFor(@"a.mzML");
            taskKey = task.ValidityKey(ctx);
            declared = task.Outputs(ctx).Contains(export);
            return task.OutputValidityKey(ctx, taskKey, export);
        }

        /// <summary>The base task key as every build before the blib reader change wrote it.</summary>
        private static string PreUpgradeBaseKey(OspreyConfig config)
        {
            return string.Format(@"search={0};library={1}{2}", config.Identity.SearchParameterHash(),
                config.Identity.LibraryIdentityHash(), OspreyEnvironment.PickValidityKeySuffix());
        }

        /// <summary>
        /// <c>OSPREY_FDR_MODEL=gbdt</c> must key the three tasks whose output the first-pass model
        /// determines - FirstPassFDR trains it, PerFileRescoring and SecondPassFDR score with it -
        /// and nothing else, and must leave every linear-SVM key exactly as it was.
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
            // The linear SVM emits NOTHING, through both overloads and whatever the tree settings
            // say, so moving the choice from --fdr-method to OSPREY_FDR_MODEL invalidates no
            // existing SVM output directory.
            Assert.AreEqual(string.Empty, PercolatorEngine.GbdtValidityKeySuffix(new OspreyConfig()),
                @"the linear SVM must emit nothing, or every existing output directory is invalidated");
            Assert.AreEqual(string.Empty, PercolatorEngine.GbdtValidityKeySuffix(FdrMethod.Percolator,
                    new GbtParams { NTrees = 7 }, OspreyEnvironment.GBT_MAX_ITERATIONS_DEFAULT + 1, 1),
                @"tree settings must not reach a linear-SVM key");

            // The spelling of the default tree term, pinned whole: it is what every gbdt output
            // directory records, and it is named for the variable that selects it. It read
            // ;fdrmethod=gbdt while --fdr-method did, which no release carried.
            Assert.AreEqual(
                @";fdrmodel=gbdt;gbtobjective=LogisticBinary;gbttrees=200;gbtdepth=6;gbtlr=0.1" +
                @";gbtminchild=1;gbtsubsample=0.8;gbtcolsample=0.8;gbtgamma=0;gbtlambda=1;gbtalpha=0" +
                @";gbtbins=64;gbtseed=42;gbtiterations=30;gbtinnerfolds=5",
                PercolatorEngine.GbdtValidityKeySuffix(FdrMethod.Gbdt, new GbtParams(),
                    OspreyEnvironment.GBT_MAX_ITERATIONS_DEFAULT, 5));

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
                    if (o != null && o.EndsWith(ModelDiagnosticsReport.EXT_HTML, StringComparison.Ordinal))
                        declared = true;
                }
                Assert.AreEqual(wanted, declared, wanted
                    ? string.Format(@"the report must be a declared output when {0} is on, or a deleted report cannot be regenerated", OspreyCommandArgs.ARG_MODEL_DIAGNOSTICS.ArgumentText)
                    : string.Format(@"the report must NOT be declared when {0} is off, or every plain run is permanently invalid", OspreyCommandArgs.ARG_MODEL_DIAGNOSTICS.ArgumentText));
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
        /// <para>The levers are asserted differently on purpose. The run-vs-maximum selection and
        /// the C-selection tolerance are FLIPPED DEFAULTS, so they must key for EVERY arm - an
        /// empty new default would equal every directory written before the flip and let a
        /// resume adopt the old rule's scores. The training cap's default never moved, so it must
        /// stay silent when unset, exactly like the aggregation suffix.</para>
        /// </summary>
        private static void AssertTrainingSampleLeversKeyDifferently()
        {
            const double tol = OspreyEnvironment.DEFAULT_SVM_C_SELECTION_TOLERANCE;
            Assert.AreEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(OspreyEnvironment.TrainPickRun,
                    OspreyEnvironment.MaxTrainSizeOverride, OspreyEnvironment.SvmCSelectionTolerance),
                OspreyEnvironment.TrainSampleValidityKeySuffix(),
                @"the key the tasks use must be built from every lever the environment read");
            Assert.AreNotEqual(string.Empty, OspreyEnvironment.TrainSampleValidityKeySuffix(true, null, tol),
                @"the shipped reservoir arm must key, or a pre-flip directory is adopted as though it had been trained on it");
            Assert.AreNotEqual(string.Empty, OspreyEnvironment.TrainSampleValidityKeySuffix(false, null, tol),
                @"the forced-maximum arm must key too");
            Assert.AreNotEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(false, null, tol),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, null, tol),
                @"per-run picking trains on different rows than the cross-run maximum");
            Assert.AreEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(true, null, tol),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, null, tol),
                @"the suffix must be a pure function of its arms");
            Assert.AreNotEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(true, null, tol),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, 3000000, tol),
                @"a raised training cap covers more precursors, so it must key differently");
            Assert.AreNotEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(true, 3000000, tol),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, 600000, tol),
                @"two different caps must key differently, not merely differ from the default");
            Assert.AreNotEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(false, 3000000, tol),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, 3000000, tol),
                @"the two settings are independent, so their combination is a fourth arm");
            // The SVM C-selection tolerance is a flipped default (the grid search used to keep the
            // strict maximum), so it keys for every arm, and each tolerance keys differently.
            StringAssert.Contains(OspreyEnvironment.TrainSampleValidityKeySuffix(true, null, tol),
                @";csel=" + tol.ToString(@"R", CultureInfo.InvariantCulture),
                @"the default C-selection tolerance must key, or a directory trained under the strict maximum is adopted");
            Assert.AreNotEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(true, null, 0),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, null, tol),
                @"the strict maximum (OSPREY_SVM_C_TOLERANCE=0) trains a different model than the default tolerance");
            Assert.AreNotEqual(OspreyEnvironment.TrainSampleValidityKeySuffix(true, null, 0.01),
                OspreyEnvironment.TrainSampleValidityKeySuffix(true, null, 0.02),
                @"two tolerances must key differently");
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
