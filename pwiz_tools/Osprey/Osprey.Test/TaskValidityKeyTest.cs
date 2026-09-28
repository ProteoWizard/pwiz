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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
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
        /// <summary>The base key term an annotated blib library adds (<c>OspreyTask.ValidityKey</c>).</summary>
        private const string LIBEXT_TERM = @";libext=ann2";

        [TestMethod]
        public void TestFlippedDefaultsParticipateInTheValidityKey()
        {
            AssertSuffixesAreUnconditional();
            AssertEachArmKeysDifferently();
            AssertTrainingSampleLeversKeyDifferently();
            AssertEveryTaskCarriesTheSuffixesItNeeds();
            AssertLibraryFragmentArmIsPinnedToThePipeline();
            AssertDiagnosticsReportIsADeclaredOutputOnlyWhenAsked();
            AssertTrainingExportKey();
            AssertTrainingExportKeyFollowsEachRunsInputs();
            AssertLibraryTermsKeyOnlyWhatChanged();
        }

        /// <summary>
        /// Each library term keys the tasks its change reaches, and no others:
        /// <list type="bullet">
        /// <item>a blib whose <c>RefSpectraPeakAnnotations</c> table has rows is read differently
        /// since the reader started typing fragments from it, so every task keys on
        /// <see cref="OspreyTask.LIBRARY_READER_TERM"/>; a TSV library and a blib with no
        /// annotation rows get no reader term, in the keys or the <c>.libcache</c> composition;</item>
        /// <item>BiblioSpec's one-decimal modification text now reads with exact masses, which reach
        /// only the output blib, so only SecondPassFDR keys on
        /// <see cref="OspreyTask.LIBRARY_MODS_TERM"/>, while the <c>.libcache</c> is re-read once;</item>
        /// <item>every search that generates its decoys keys on
        /// <see cref="OspreyTask.DECOY_MODS_TERM"/>, because decoy fragments now add two
        /// modifications on one residue; a search whose decoys come from the library keys exactly
        /// as before.</item>
        /// </list>
        /// </summary>
        private static void AssertLibraryTermsKeyOnlyWhatChanged()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"osprey_libext_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                double[] peaks = { 300.0, 400.0, 500.0 };
                string tsv = Path.Combine(dir, @"library.tsv");
                File.WriteAllText(tsv, @"not read");
                string plain = BlibLibraryInputTest.CreateBlib(Path.Combine(dir, @"plain.blib"),
                    @"PEPC[+57.021464]TIDEK", peaks, new (int, string, int)[0]);
                string annotated = BlibLibraryInputTest.CreateBlib(Path.Combine(dir, @"annotated.blib"),
                    @"PEPC[+57.021464]TIDEK", peaks, new[] { (0, @"y3", 1) });
                string oneDecimal = BlibLibraryInputTest.CreateBlib(Path.Combine(dir, @"bibliospec.blib"),
                    @"PEPC[+57.0]TIDEK", peaks, new (int, string, int)[0]);

                foreach (string unchanged in new[] { tsv, plain })
                {
                    string name = Path.GetFileName(unchanged);
                    var config = new OspreyConfig { LibrarySource = LibrarySource.FromPath(unchanged) };
                    var keys = TaskKeys(config);
                    Assert.AreEqual(PreUpgradeBaseKey(config) + OspreyTask.DECOY_MODS_TERM, keys[PerFileScoringTask.TASK_NAME],
                        name + @" keys as before the reader change, plus the generated-decoy term");
                    foreach (var key in keys)
                    {
                        Assert.IsFalse(key.Value.Contains(LIBEXT_TERM), key.Key);
                        Assert.IsFalse(key.Value.Contains(OspreyTask.LIBRARY_MODS_TERM), key.Key);
                    }
                    Assert.AreEqual(string.Empty, LibraryLoader.LibraryReaderTerms(config), name + @" .libcache reader terms");

                    var inLibrary = new OspreyConfig { LibrarySource = LibrarySource.FromPath(unchanged), DecoysInLibrary = true };
                    var fromLibrary = new OspreyConfig { LibrarySource = LibrarySource.FromPath(unchanged), DecoyMethod = DecoyMethod.FromLibrary };
                    foreach (var supplied in new[] { inLibrary, fromLibrary })
                    {
                        Assert.AreEqual(PreUpgradeBaseKey(supplied), TaskKeys(supplied)[PerFileScoringTask.TASK_NAME],
                            name + @" with decoys from the library keys exactly as before");
                    }
                }

                var oneDecimalConfig = new OspreyConfig { LibrarySource = LibrarySource.FromPath(oneDecimal) };
                var oneDecimalKeys = TaskKeys(oneDecimalConfig);
                Assert.AreEqual(PreUpgradeBaseKey(oneDecimalConfig) + OspreyTask.DECOY_MODS_TERM,
                    oneDecimalKeys[PerFileScoringTask.TASK_NAME], @"the masses reach no score of an unannotated blib");
                foreach (var key in oneDecimalKeys)
                {
                    Assert.AreEqual(key.Key == SecondPassFdrTask.TASK_NAME, key.Value.Contains(OspreyTask.LIBRARY_MODS_TERM),
                        key.Key + @": only the task that writes the output blib keys on the modification reader");
                    Assert.IsFalse(key.Value.Contains(LIBEXT_TERM), key.Key);
                }
                Assert.AreEqual("blib_mods:2\n", LibraryLoader.LibraryReaderTerms(oneDecimalConfig));

                var annotatedConfig = new OspreyConfig { LibrarySource = LibrarySource.FromPath(annotated) };
                var annotatedKeys = TaskKeys(annotatedConfig);
                Assert.AreEqual(LIBEXT_TERM, OspreyTask.LIBRARY_READER_TERM);
                Assert.AreEqual(PreUpgradeBaseKey(annotatedConfig) + LIBEXT_TERM + OspreyTask.DECOY_MODS_TERM,
                    annotatedKeys[PerFileScoringTask.TASK_NAME]);
                foreach (var key in annotatedKeys)
                    StringAssert.Contains(key.Value, LIBEXT_TERM, key.Key + @" must key on the annotated blib's reader");
                Assert.AreEqual("blib_reader:2\n", LibraryLoader.LibraryReaderTerms(annotatedConfig));
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
        /// The q-value sidecar is the second-pass one only when PerFileRescoring wrote it: one
        /// SecondPassFDR writes after the export (PerFileRescoring had no readable first-pass
        /// model to compete with) is not read and must not key it, or every export is redone on
        /// the next resume.
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
                File.WriteAllText(TaskValiditySidecar.PathFor(pass2, PerFileRescoreTask.TASK_NAME), @"stamp");
                Assert.AreEqual(pass2, TrainingExportWriter.RunQPath(runA, out pass), @"PerFileRescoring's stamp makes the second pass the one read");
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
        /// valid and only the exports outstanding (P16). The export's key follows each export
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
                @"the --task TrainingExport selector must compute the straight-through key");
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
