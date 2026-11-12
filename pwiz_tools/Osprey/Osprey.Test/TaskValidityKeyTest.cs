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
using pwiz.Osprey.FDR.ModelDiagnostics;
using pwiz.Osprey.IO;
using pwiz.Osprey.ML;
using pwiz.Osprey.Scoring;
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
            // The keys below are spelled out for the core set, whose recorded-feature term is empty,
            // as they were before the HRAM default became the default set; the feature-set checks set
            // their own resolution and set.
            using (new FeatureSetScope(ResolutionMode.HRAM, OspreyFeatureSet.core))
            {
                AssertSuffixesAreUnconditional();
                AssertEachArmKeysDifferently();
                AssertTrainingSampleLeversKeyDifferently();
                AssertFdrExperimentSwitchesKeyDifferently();
                AssertRecordedFeaturesFollowTheMask();
                AssertHramOnlyFamiliesDropOutAtUnitResolution();
                AssertEveryTaskCarriesTheSuffixesItNeeds();
                AssertDiagnosticsReportIsADeclaredOutputOnlyWhenAsked();
                AssertTreeClassifierKeysTheModelTasks();
                AssertTrainingExportKey();
                AssertFragmentMassErrorReaderKey();
                AssertTrainingExportKeyFollowsEachRunsInputs();
                AssertLibraryTermsKeyOnlyWhatChanged();
            }
        }

        /// <summary>
        /// Each library term keys what its change reaches, and no more:
        /// <list type="bullet">
        /// <item>every blib is read differently since the reader started typing its peaks from
        /// m/z (and reading modification text residue- and precision-aware), so every task of a
        /// blib search keys on <see cref="OspreyTask.BLIB_READER_TERM"/>, and the derived
        /// library's composition carries the reader version with the fragment tolerance the
        /// typed peaks were computed within;</item>
        /// <item>a DIA-NN TSV search keys exactly as before - its columns are still the typing -
        /// while its derived library is rebuilt once through the validating reader;</item>
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
                // The TSV term is a literal (an int does not concatenate into a const string), so
                // hold it to the reader's version here.
                Assert.AreEqual(@";tsvreader=" + DiannTsvLoader.READER_VERSION.ToString(CultureInfo.InvariantCulture),
                    OspreyTask.TSV_READER_TERM);
                Assert.AreEqual(PreUpgradeBaseKey(tsvConfig) + OspreyTask.TSV_READER_TERM + XicTerm(), tsvKeys[PerFileScoringTask.TASK_NAME],
                    @"a TSV search keys on its reader version, as a blib search does");
                foreach (var key in tsvKeys)
                    Assert.IsFalse(key.Value.Contains(OspreyTask.BLIB_READER_TERM), key.Key);
                Assert.AreEqual(DiannTsvLoader.CacheTerm(tsvConfig.FragmentTolerance), LibraryLoader.LibraryReaderTerms(tsvConfig));

                var blibConfig = new OspreyConfig { LibrarySource = LibrarySource.FromPath(blib) };
                var blibKeys = TaskKeys(blibConfig);
                Assert.AreEqual(PreUpgradeBaseKey(blibConfig) + OspreyTask.BLIB_READER_TERM + XicTerm(),
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
        /// The q-value sidecar is the first-pass one, always: a second-pass sidecar, whether
        /// SecondPassFDR or the PerFileRescoring worker (its stamp, decoys and plan) wrote it, is
        /// not read and must not key the export, or the export would change on a later invocation
        /// of the same command.
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
                string RunKey(string input) => task.OutputValidityKey(ctx, key, TrainingExportWriter.PathFor(input));

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

                // The export selects on the first-pass run q, whatever second pass exists: a
                // second-pass sidecar, from SecondPassFDR or from the PerFileRescoring worker with
                // its decoys and the run's plan under the reconciled parquet's stamp, is not read,
                // so none of them keys the export, and the first-pass sidecar always does.
                string pass2 = FdrScoresSidecar.Pass2Path(runA);
                WritePass2Sidecar(pass2, SecondPassFdrTask.TASK_NAME, 1);
                Assert.AreEqual(current, RunKey(runA), @"a second-pass sidecar is not read, so it must not key the export");
                Assert.AreEqual(FdrScoresSidecar.Pass1Path(runA), TrainingExportWriter.RunQPath(runA, out var pass));
                Assert.AreEqual(FdrScoresSidecar.Pass.FirstPass, pass);
                WritePass2Sidecar(pass2, PerFileRescoreTask.TASK_NAME, 1);
                Assert.AreEqual(current, RunKey(runA), @"a PerFileRescoring sidecar must not flip the export to the second pass");
                Assert.AreEqual(FdrScoresSidecar.Pass1Path(runA), TrainingExportWriter.RunQPath(runA, out pass));
                var workerStamp = OspreyVersion.Stamp(PerFileRescoreTask.TASK_NAME, @"key");
                // One row: an empty entry list writes no parquet at all. Written as Stage 6
                // writes it - its producer over a Stage 4 parquet - because a reconciled parquet
                // without the survivor shape reads as unstamped, whatever its footer says.
                string stage4 = Path.Combine(dir, @"a_stage4" + ParquetScoreCache.EXT_SCORES);
                ParquetScoreCache.WriteScoresParquet(stage4,
                    new List<CoelutionScoredEntry>
                    {
                        new CoelutionScoredEntry
                        {
                            EntryId = 1, Sequence = @"PEPTIDE", ModifiedSequence = @"PEPTIDE", Charge = 2,
                            FileName = @"a.mzML", PeakBounds = new XICPeakBounds(), Features = new CoelutionFeatureSet(),
                        },
                    },
                    null);
                ParquetScoreCache.StreamReconciledScoresParquet(stage4, ParquetScoreCache.GetReconciledScoresPath(runA),
                    null, null, ParquetScoreCache.WithStamp(
                        ReconciledParquetWriter.BuildReconciliationMetadata(new OspreyConfig(), null), workerStamp),
                    null, @"a.mzML", null, PerFileRescoreTask.TASK_NAME, null, null, true);
                current = RunKey(runA);
                Pass2CompetitionDecoys.Write(Pass2CompetitionDecoys.PathFor(runA),
                    new Dictionary<uint, (double score, uint entryId)>(), workerStamp);
                ReconciliationFile.Save(ReconciliationFile.PathForInput(runA),
                    new ReconciliationFile { FormatVersion = ReconciliationFile.CurrentFormatVersion }, workerStamp);
                Assert.IsTrue(Pass2FdrSidecar.HasWorkerStamp(runA), @"the worker's stamp, decoys and plan are the worker's complete second pass");
                Assert.AreEqual(FdrScoresSidecar.Pass1Path(runA), TrainingExportWriter.RunQPath(runA, out pass),
                    @"the worker's complete second pass is still not what the export selects on");
                Assert.AreEqual(FdrScoresSidecar.Pass.FirstPass, pass);
                Assert.AreEqual(current, RunKey(runA), @"the worker's second pass must not key the export");
                WritePass2Sidecar(pass2, PerFileRescoreTask.TASK_NAME, 2);
                Assert.AreEqual(current, RunKey(runA), @"the worker's sidecar rewritten must not invalidate the run's export");
                current = AssertRewritesInvalidate(FdrScoresSidecar.Pass1Path(runA), current, () => RunKey(runA));
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
        /// A real second-pass sidecar of <paramref name="records"/> records whose embedded stamp
        /// names <paramref name="producerTask"/> as the task that wrote it.
        /// </summary>
        private static void WritePass2Sidecar(string path, string producerTask, int records)
        {
            FdrScoresSidecar.BeginRun();
            FdrScoresSidecar.Write(path,
                Enumerable.Range(1, records).Select(i => new FdrScoreRecord((uint)i, 0.5, 0.01, 0.01, 10.0)).ToList(),
                FdrScoresSidecar.Pass.SecondPass, OspreyVersion.Stamp(producerTask, @"key"));
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
        /// The training export is a declared output of PerFileRescoring only when asked for.
        /// Asking for it moves the keys of the two tasks that write a scores parquet, and no
        /// other: those parquets carry the reference XIC the export reads only when it is
        /// requested (#5023), so a finished analysis without it re-scores Stages 4 and 6, while
        /// its FDR outputs stay valid. The export's key follows each export setting, and the
        /// straight-through run and a <c>--task TrainingExport</c> selector compute the same key,
        /// so an export is never redone.
        /// </summary>
        private static void AssertTrainingExportKey()
        {
            var off = TaskConfigs.StraightThrough();
            off.InputFiles = new List<string> { @"a.mzML" };
            off.LibrarySource = LibrarySource.FromPath(@"ref.tsv");
            off.OutputBlib = @"out.blib";
            var offCtx = TaskConfigs.ContextFor(off);
            var offTask = off.Pipeline.OfType<PerFileRescoreTask>().Single();
            string export = TrainingExportWriter.PathFor(@"a.mzML");
            Assert.IsFalse(offTask.Outputs(offCtx).Contains(export), @"no export is declared with the flag off");

            string straight = ExportKey(TaskConfigs.StraightThrough(), c => { }, out string taskKeyOn, out bool declared);
            Assert.IsTrue(declared, @"the export is a declared output of PerFileRescoring under the flag");
            Assert.AreEqual(offTask.ValidityKey(offCtx) + @";refxic", taskKeyOn,
                @"the flag moves PerFileRescoring's key by the reference XIC term alone");
            var on = TaskConfigs.StraightThrough();
            on.InputFiles = off.InputFiles;
            on.LibrarySource = off.LibrarySource;
            on.OutputBlib = off.OutputBlib;
            on.TrainingExport.Enabled = true;
            var onCtx = TaskConfigs.ContextFor(on);
            var scoringOff = off.Pipeline.OfType<PerFileScoringTask>().Single();
            var scoringOn = on.Pipeline.OfType<PerFileScoringTask>().Single();
            Assert.AreEqual(scoringOff.ValidityKey(offCtx) + @";refxic", scoringOn.ValidityKey(onCtx),
                @"the flag moves PerFileScoring's key by the reference XIC term alone");
            Assert.AreEqual(off.Pipeline.OfType<FirstPassFdrTask>().Single().ValidityKey(offCtx),
                on.Pipeline.OfType<FirstPassFdrTask>().Single().ValidityKey(onCtx),
                @"the flag must not move an FDR task's key");
            Assert.AreEqual(off.Pipeline.OfType<SecondPassFdrTask>().Single().ValidityKey(offCtx),
                on.Pipeline.OfType<SecondPassFdrTask>().Single().ValidityKey(onCtx),
                @"the flag must not move an FDR task's key");
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
            string export = TrainingExportWriter.PathFor(@"a.mzML");
            taskKey = task.ValidityKey(ctx);
            declared = task.Outputs(ctx).Contains(export);
            return task.OutputValidityKey(ctx, taskKey, export);
        }

        /// <summary>
        /// The report renders a fragment mass-error summary by the terms of its key that the
        /// configuration alone decides, so a node holding no spectra can ask it: it accepts the
        /// summary PerFileRescoring stamps for this analysis, whether the analysis or
        /// <c>--task ModelDiagnostics</c> wrote it, and not one left by another run FDR or library,
        /// or stamped by another task.
        /// </summary>
        private static void AssertFragmentMassErrorReaderKey()
        {
            var config = MassErrorConfig(TaskConfigs.StraightThrough(), c => { });
            var run = new ModelDiagnosticsData.FragmentMassErrorRun { MaxQ = config.RunFdr };
            var stamp = OspreyVersion.Stamp(PerFileRescoreTask.TASK_NAME, MassErrorKey(config));
            Assert.IsTrue(FragmentMassErrorWriter.IsForAnalysis(stamp, run, config), @"the analysis's own summary");
            var selector = MassErrorConfig(TaskConfigs.ForTask(ModelDiagnosticsTask.TASK_NAME), c => { });
            Assert.IsTrue(FragmentMassErrorWriter.IsForAnalysis(stamp, run, selector),
                OspreyArgNames.TaskText(ModelDiagnosticsTask.TASK_NAME) + @" must render the analysis's summary");
            Assert.IsTrue(FragmentMassErrorWriter.IsForAnalysis(
                    OspreyVersion.Stamp(PerFileRescoreTask.TASK_NAME, MassErrorKey(selector)), run, config),
                @"the analysis must render the summary " + OspreyArgNames.TaskText(ModelDiagnosticsTask.TASK_NAME) + @" wrote");
            Assert.IsFalse(FragmentMassErrorWriter.IsForAnalysis(
                OspreyVersion.Stamp(SecondPassFdrTask.TASK_NAME, MassErrorKey(config)), run, config), @"another task's stamp");
            Assert.IsFalse(FragmentMassErrorWriter.IsForAnalysis(null, run, config), @"no stamp");

            var otherFdr = MassErrorConfig(TaskConfigs.StraightThrough(), c => c.RunFdr = 0.05);
            Assert.IsFalse(FragmentMassErrorWriter.IsForAnalysis(
                    OspreyVersion.Stamp(PerFileRescoreTask.TASK_NAME, MassErrorKey(otherFdr)),
                    new ModelDiagnosticsData.FragmentMassErrorRun { MaxQ = otherFdr.RunFdr }, config),
                @"a summary measured at another run FDR");
            var otherLibrary = MassErrorConfig(TaskConfigs.StraightThrough(), c => c.LibrarySource = LibrarySource.FromPath(@"other.tsv"));
            Assert.IsFalse(FragmentMassErrorWriter.IsForAnalysis(
                OspreyVersion.Stamp(PerFileRescoreTask.TASK_NAME, MassErrorKey(otherLibrary)), run, config),
                @"a summary measured against another library");
        }

        private static OspreyConfig MassErrorConfig(OspreyConfig config, Action<OspreyConfig> mutate)
        {
            config.InputFiles = new List<string> { @"a.mzML" };
            config.LibrarySource = LibrarySource.FromPath(@"ref.tsv");
            config.OutputBlib = @"out.blib";
            config.ModelDiagnostics = true;
            mutate(config);
            return config;
        }

        /// <summary>The key PerFileRescoring stamps the one run's fragment mass-error summary with.</summary>
        private static string MassErrorKey(OspreyConfig config)
        {
            var ctx = TaskConfigs.ContextFor(config);
            var task = config.Pipeline.OfType<PerFileRescoreTask>().Single();
            string summary = FragmentMassErrorWriter.PathFor(@"a.mzML");
            Assert.IsTrue(task.Outputs(ctx).Contains(summary), @"the summary is a declared output of PerFileRescoring");
            return task.OutputValidityKey(ctx, task.ValidityKey(ctx), summary);
        }

        /// <summary>The XIC fragment-count term every task's key ends with.</summary>
        private static string XicTerm()
        {
            return @";xic=" + TopFragmentExtractor.SearchTopNFragments.ToString(CultureInfo.InvariantCulture);
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
            // The protein-best column off in both, so the classifier is the only difference.
            var linearCtx = new PipelineContext(new OspreyConfig { ProteinBest = false }, tasks, null, null, null);
            var treeCtx = new PipelineContext(new OspreyConfig { FdrClassifier = FdrClassifier.Gbdt, ProteinBest = false }, tasks, null, null, null);
            string treeTerm = PercolatorEngine.GbdtValidityKeySuffix(treeCtx.Config);
            Assert.AreNotEqual(string.Empty, treeTerm, @"gbdt must emit a term");
            // The linear SVM with the protein-best column off emits NOTHING, through both overloads
            // and whatever the tree settings say, so moving the choice from --fdr-method to
            // OSPREY_FDR_MODEL invalidates no existing SVM output directory.
            Assert.AreEqual(string.Empty, PercolatorEngine.GbdtValidityKeySuffix(new OspreyConfig { ProteinBest = false }),
                @"the linear SVM must emit nothing, or every existing output directory is invalidated");
            // Off is the default, so the default key is the one before the column existed.
            Assert.AreEqual(string.Empty, PercolatorEngine.GbdtValidityKeySuffix(new OspreyConfig()),
                @"the default must not change existing keys");
            // On, it emits the two-round training and the column, spelled as the
            // OSPREY_GBT_PREFILTER_SIZE=200000 OSPREY_PROTEIN_BEST=max experiment spelled them, so
            // that experiment's directories stay current and no other arm's are adopted.
            Assert.AreEqual(@";gbtprefilter=200000;proteinbest=max;proteinbestdecoy=paired",
                PercolatorEngine.GbdtValidityKeySuffix(new OspreyConfig { ProteinBest = true }));
            Assert.AreEqual(string.Empty, PercolatorEngine.GbdtValidityKeySuffix(FdrClassifier.LinearSvm,
                    new GbtParams { NTrees = 7 }, OspreyEnvironment.GBT_MAX_ITERATIONS_DEFAULT + 1, 1),
                @"tree settings must not reach a linear-SVM key");

            // The spelling of the default tree term, pinned whole: it is what every gbdt output
            // directory records, and it is named for the variable that selects it. It read
            // ;fdrmethod=gbdt while --fdr-method did, which no release carried.
            Assert.AreEqual(
                @";fdrmodel=gbdt;gbtobjective=LogisticBinary;gbttrees=200;gbtdepth=6;gbtlr=0.1" +
                @";gbtminchild=1;gbtsubsample=0.8;gbtcolsample=0.8;gbtgamma=0;gbtlambda=1;gbtalpha=0" +
                @";gbtbins=64;gbtseed=42;gbtiterations=30;gbtinnerfolds=5",
                PercolatorEngine.GbdtValidityKeySuffix(FdrClassifier.Gbdt, new GbtParams { UseDeterministicMath = true },
                    OspreyEnvironment.GBT_MAX_ITERATIONS_DEFAULT, 5));
            // OSPREY_DETERMINISTIC_MATH=0 appends one term and leaves the rest alone, so the
            // default key above - every existing gbdt directory - did not move when it arrived.
            Assert.AreEqual(
                @";fdrmodel=gbdt;gbtobjective=LogisticBinary;gbttrees=200;gbtdepth=6;gbtlr=0.1" +
                @";gbtminchild=1;gbtsubsample=0.8;gbtcolsample=0.8;gbtgamma=0;gbtlambda=1;gbtalpha=0" +
                @";gbtbins=64;gbtseed=42;gbtiterations=30;gbtinnerfolds=5;gbtmath=system",
                PercolatorEngine.GbdtValidityKeySuffix(FdrClassifier.Gbdt, new GbtParams { UseDeterministicMath = false },
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

            // --protein-best keys the same three tasks, so a resume with the other setting retrains.
            var proteinBestCtx = new PipelineContext(new OspreyConfig { ProteinBest = true }, tasks, null, null, null);
            foreach (var task in tasks)
            {
                bool expectTerm = task.Name == FirstPassFdrTask.TASK_NAME ||
                                  task.Name == PerFileRescoreTask.TASK_NAME ||
                                  task.Name == SecondPassFdrTask.TASK_NAME;
                Assert.AreEqual(expectTerm, task.ValidityKey(linearCtx) != task.ValidityKey(proteinBestCtx),
                    string.Format(@"{0} must {1}key on {2}", task.Name, expectTerm ? string.Empty : @"NOT ",
                        OspreyArgNames.Text(OspreyArgNames.PROTEIN_BEST)));
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
            string defaultKey = PercolatorEngine.GbdtValidityKeySuffix(FdrClassifier.Gbdt, new GbtParams(), iterations, innerFolds);
            Assert.AreEqual(defaultKey, PercolatorEngine.GbdtValidityKeySuffix(FdrClassifier.Gbdt, new GbtParams(), iterations, innerFolds),
                @"the term must be a pure function of its settings");
            var keys = new HashSet<string> { defaultKey };
            foreach (var field in typeof(GbtParams).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var changed = new GbtParams();
                field.SetValue(changed, OtherValue(field.GetValue(changed)));
                string key = PercolatorEngine.GbdtValidityKeySuffix(FdrClassifier.Gbdt, changed, iterations, innerFolds);
                if (field.Name == nameof(GbtParams.MaxDegreeOfParallelism))
                {
                    Assert.AreEqual(defaultKey, key, @"training is bit-identical at any thread count, so it must not key");
                    continue;
                }
                Assert.IsTrue(keys.Add(key), field.Name + @" must key, and distinctly from every other setting");
            }
            Assert.IsTrue(keys.Add(PercolatorEngine.GbdtValidityKeySuffix(FdrClassifier.Gbdt, new GbtParams(), iterations + 1, innerFolds)),
                @"the tree iteration cap (OSPREY_GBT_MAX_ITERATIONS) must key");
            Assert.IsTrue(keys.Add(PercolatorEngine.GbdtValidityKeySuffix(FdrClassifier.Gbdt, new GbtParams(), iterations, 1)),
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
            if (value is bool b)
                return !b;
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
        /// OSPREY_RECONCILIATION and OSPREY_PEP_FORMULA change the first-pass consensus and PEP, so
        /// each setting keys the FDR tasks differently. Unset they emit nothing, so default output
        /// directories keep their key.
        /// </summary>
        private static void AssertFdrExperimentSwitchesKeyDifferently()
        {
            string none = OspreyEnvironment.FdrExperimentValidityKeySuffix(null, false);
            string recon = OspreyEnvironment.FdrExperimentValidityKeySuffix(@"symmetric-decoys", false);
            string pep = OspreyEnvironment.FdrExperimentValidityKeySuffix(null, true);
            string both = OspreyEnvironment.FdrExperimentValidityKeySuffix(@"symmetric-decoys", true);
            Assert.AreEqual(string.Empty, none, @"the default must not change existing keys");
            var distinct = new HashSet<string> { none, recon, pep, both };
            Assert.AreEqual(4, distinct.Count, @"every combination must key differently");

            // The override-path runner-up change keys the rescoring tasks only when the classifier
            // sees a runner-up feature: empty under the core set, present with every feature on and
            // under the default set, which uses them.
            var coreMask = PinFeatureMask.FeatureSetMask(OspreyFeatureSet.core);
            Assert.AreEqual(string.Empty, PinFeatureMask.RescoreValidityKeySuffix(coreMask),
                @"the core set hides the runner-up features, so core keys must not change");
            Assert.AreNotEqual(string.Empty, PinFeatureMask.RescoreValidityKeySuffix(PinFeatureMask.FeatureSetMask(OspreyFeatureSet.@default)),
                @"the default set uses the runner-up features");
            Assert.AreNotEqual(string.Empty, PinFeatureMask.RescoreValidityKeySuffix(f => false),
                @"with the runner-up features on, the rescoring tasks must key on the change");
            int margin = Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, @"peak_rank_margin");
            Assert.AreNotEqual(string.Empty, PinFeatureMask.RescoreValidityKeySuffix(f => f != margin),
                @"any one runner-up feature unmasked must key the change");
            // The key term and the second detection it records must switch together.
            Assert.IsFalse(PinFeatureMask.UsesRunnerUpFeatures(coreMask),
                @"the core set must skip the override-path detection");
            Assert.IsTrue(PinFeatureMask.UsesRunnerUpFeatures(f => f != margin),
                @"any one runner-up feature unmasked must run the override-path detection");

            // The FDR classifier switches change the draw or the positives the classifier trains on, so each
            // keys differently too.
            var classifierKeys = new[]
            {
                OspreyEnvironment.FdrClassifierValidityKeySuffix(null, null),
                OspreyEnvironment.FdrClassifierValidityKeySuffix(7, null),
                OspreyEnvironment.FdrClassifierValidityKeySuffix(42, null),
                OspreyEnvironment.FdrClassifierValidityKeySuffix(null, 0.01),
                OspreyEnvironment.FdrClassifierValidityKeySuffix(null, 1.0),
                OspreyEnvironment.FdrClassifierValidityKeySuffix(7, 1.0),
            };
            Assert.AreEqual(string.Empty, classifierKeys[0], @"unset switches must not change existing keys");
            Assert.AreEqual(classifierKeys.Length, classifierKeys.Distinct().Count(), @"every switch must key differently");

            // OSPREY_FDR_TRAIN_FDR accepts only a q-value in (0, 1]; anything else stops the search at startup.
            Assert.AreEqual(0.05, OspreyEnvironment.ParseFdrTrainFdr(@"0.05"));
            Assert.AreEqual(1.0, OspreyEnvironment.ParseFdrTrainFdr(@"1"));
            foreach (string rejected in new[] { @"0", @"-0.1", @"1.5", @"5%", @"0,05", @"NaN", @"" })
                Assert.IsNull(OspreyEnvironment.ParseFdrTrainFdr(rejected), rejected);
        }

        /// <summary>
        /// Scoring records the core features and each feature the classifier uses, or everything
        /// under OSPREY_RECORD_ALL_FEATURES=1, and computes each family a recorded feature belongs
        /// to. The key term names the recorded columns: empty for the core alone (the core set, so
        /// core keys do not change), the same for two arms that record the same columns, and
        /// different whenever a column is added - including another column of a family already
        /// computed, which the file would not hold.
        /// </summary>
        private static void AssertRecordedFeaturesFollowTheMask()
        {
            int n = ParquetScoreCache.NUM_PIN_FEATURES;
            int core = PinFeatureMask.CORE_FEATURE_COUNT;
            Assert.AreEqual(21, core, @"the core is the 21 features Osprey had before the families");
            var starts = PinFeatureMask.FAMILY_FIRST_FEATURES
                .Select(name => Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, name)).ToArray();
            Assert.IsTrue(starts.All(start => start >= core), @"every family is a PIN feature after the core");
            CollectionAssert.AreEqual(starts.OrderBy(start => start).Distinct().ToArray(), starts, @"families are in PIN order");

            var coreMask = PinFeatureMask.FeatureSetMask(OspreyFeatureSet.core);
            bool[] coreOnly = PinFeatureMask.RecordedFeatures(coreMask, false);
            bool[] recordAll = PinFeatureMask.RecordedFeatures(coreMask, true);
            bool[] maskNone = PinFeatureMask.RecordedFeatures(f => false, false);
            bool[] maskAll = PinFeatureMask.RecordedFeatures(f => true, false);
            for (int f = 0; f < n; f++)
            {
                Assert.AreEqual(f < core, coreOnly[f], @"the core set records the core alone");
                Assert.AreEqual(f < core, maskAll[f], @"the core is recorded whatever the mask");
                Assert.IsTrue(recordAll[f] && maskNone[f], @"recording all and masking none record every feature");
            }
            CollectionAssert.AreEqual(coreOnly, PinFeatureMask.ComputedFeatures(coreOnly), @"the core set computes the core alone");

            // Unmasking one feature records that column alone, and computes its whole family.
            int tcRank3 = Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, @"tc_corr_rank3");
            int tcFirst = Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, @"tc_sum6");
            int tcEnd = Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, @"heavy_sum6");
            Func<int, bool> oneUnmasked = f => coreMask(f) && f != tcRank3;
            bool[] oneColumn = PinFeatureMask.RecordedFeatures(oneUnmasked, false);
            bool[] oneFamily = PinFeatureMask.ComputedFeatures(oneColumn);
            for (int f = 0; f < n; f++)
            {
                string name = ParquetScoreCache.PIN_FEATURE_NAMES[f];
                Assert.AreEqual(f < core || f == tcRank3, oneColumn[f], name);
                Assert.AreEqual(f < core || (f >= tcFirst && f < tcEnd), oneFamily[f], name);
            }
            // Whatever the mask, every feature the classifier reads is recorded, and every recorded one computed.
            foreach (var isMasked in new[] { coreMask, oneUnmasked, f => false, f => f % 3 == 0 })
            {
                bool[] recorded = PinFeatureMask.RecordedFeatures(isMasked, false);
                bool[] computed = PinFeatureMask.ComputedFeatures(recorded);
                foreach (int f in PinFeatureMask.UnmaskedIndices(isMasked))
                    Assert.IsTrue(recorded[f], ParquetScoreCache.PIN_FEATURE_NAMES[f]);
                for (int f = 0; f < n; f++)
                    Assert.IsTrue(!recorded[f] || computed[f], ParquetScoreCache.PIN_FEATURE_NAMES[f]);
            }

            string coreKey = PinFeatureMask.RecordedFeaturesValidityKeySuffix(f => coreOnly[f]);
            string allKey = PinFeatureMask.RecordedFeaturesValidityKeySuffix(f => recordAll[f]);
            string oneKey = PinFeatureMask.RecordedFeaturesValidityKeySuffix(f => oneColumn[f]);
            Assert.AreEqual(string.Empty, coreKey, @"the core set must not change existing keys");
            Assert.AreEqual(@";featrec=all;featavail=109", allKey);
            Assert.AreEqual(allKey, PinFeatureMask.RecordedFeaturesValidityKeySuffix(f => maskNone[f]),
                @"masking none records what recording all does, so the two share their scores");
            Assert.AreEqual(@";featcols=tc_corr_rank3", oneKey);
            Func<int, bool> twoUnmasked = f => oneUnmasked(f) && f != tcFirst;
            bool[] twoColumns = PinFeatureMask.RecordedFeatures(twoUnmasked, false);
            CollectionAssert.AreEqual(oneFamily, PinFeatureMask.ComputedFeatures(twoColumns), @"the same family computed");
            Assert.AreEqual(@";featcols=tc_sum6+tc_corr_rank3", PinFeatureMask.RecordedFeaturesValidityKeySuffix(f => twoColumns[f]),
                @"another column of a computed family is another file, so it keys differently");

            // The default set records its own columns, and keys them by name, so an edit of the list rekeys.
            bool[] defaultRecorded = PinFeatureMask.RecordedFeatures(PinFeatureMask.FeatureSetMask(OspreyFeatureSet.@default), false);
            var defaultColumns = Enumerable.Range(core, n - core).Where(f => defaultRecorded[f])
                .Select(f => ParquetScoreCache.PIN_FEATURE_NAMES[f]).ToArray();
            Assert.AreEqual(PinFeatureMask.DEFAULT_FEATURES.Length - core, defaultColumns.Length);
            Assert.AreEqual(@";featcols=" + string.Join(@"+", defaultColumns),
                PinFeatureMask.RecordedFeaturesValidityKeySuffix(f => defaultRecorded[f]));

            // Under the core set no task carries the term, so no core output directory is invalidated;
            // under the HRAM default the scoring and rescoring tasks do.
            var tasks = OspreyTasks.Create().Pipeline;
            var ctx = new PipelineContext(new OspreyConfig(), tasks, null, null, null);
            using (new FeatureSetScope(ResolutionMode.UnitResolution, null))
            {
                Assert.AreEqual(string.Empty, PinFeatureMask.RecordedFeaturesValidityKeySuffix());
                foreach (var task in tasks)
                {
                    string key = task.ValidityKey(ctx);
                    Assert.IsFalse(key.Contains(@";featrec=") || key.Contains(@";featcols="), task.Name);
                }
            }
            using (new FeatureSetScope(ResolutionMode.HRAM, null))
            {
                string defaultKey = PinFeatureMask.RecordedFeaturesValidityKeySuffix();
                Assert.AreEqual(PinFeatureMask.RecordedFeaturesValidityKeySuffix(f => defaultRecorded[f]), defaultKey);
                foreach (var task in tasks)
                {
                    bool expected = task.Name == PerFileScoringTask.TASK_NAME || task.Name == PerFileRescoreTask.TASK_NAME;
                    Assert.AreEqual(expected, task.ValidityKey(ctx).Contains(defaultKey), task.Name);
                }
            }
        }

        /// <summary>
        /// At unit resolution the HRAM-only families are masked and unrecorded whatever the switches say, so
        /// unmasking everything gives the FDR vectors and scores columns of the features before those families,
        /// in their order: the first families on any data, and the key the same as if they did not exist.
        /// At HRAM, and under the default mask at either resolution, the resolution changes nothing.
        /// </summary>
        private static void AssertHramOnlyFamiliesDropOutAtUnitResolution()
        {
            const ResolutionMode unit = ResolutionMode.UnitResolution;
            const ResolutionMode hram = ResolutionMode.HRAM;
            var names = ParquetScoreCache.PIN_FEATURE_NAMES;
            int firstHramOnly = Array.IndexOf(names, @"lib_cosine_coel");
            Assert.AreEqual(names.Length - 8, firstHramOnly, @"the HRAM-only features are the last 8");
            Assert.AreEqual(firstHramOnly + 3, Array.IndexOf(names, @"spec_match_prob"));
            foreach (string family in PinFeatureMask.HRAM_ONLY_FAMILIES)
                CollectionAssert.Contains(PinFeatureMask.FAMILY_FIRST_FEATURES, family);
            for (int f = 0; f < names.Length; f++)
            {
                Assert.AreEqual(f < firstHramOnly, PinFeatureMask.IsAvailable(f, unit), names[f]);
                Assert.IsTrue(PinFeatureMask.IsAvailable(f, hram), names[f]);
                Assert.AreEqual(f >= PinFeatureMask.CORE_FEATURE_COUNT, PinFeatureMask.FeatureSetMask(OspreyFeatureSet.core)(f),
                    names[f] + @" masked by the core set like the other families");
            }

            // Unmasked: dropped at unit resolution, kept at HRAM, from the FDR vectors and the recorded columns.
            var beforeFamilies = Enumerable.Range(0, firstHramOnly).ToArray();
            CollectionAssert.AreEqual(beforeFamilies, PinFeatureMask.UnmaskedIndices(PinFeatureMask.MaskAtResolution(f => false, unit)));
            Assert.AreEqual(names.Length, PinFeatureMask.UnmaskedIndices(PinFeatureMask.MaskAtResolution(f => false, hram)).Length);
            foreach (bool recordAll in new[] { false, true })
            {
                bool[] unitRecorded = PinFeatureMask.RecordedFeatures(f => false, recordAll, unit);
                bool[] hramRecorded = PinFeatureMask.RecordedFeatures(f => false, recordAll, hram);
                for (int f = 0; f < names.Length; f++)
                {
                    Assert.AreEqual(f < firstHramOnly, unitRecorded[f], names[f]);
                    Assert.IsTrue(hramRecorded[f], names[f]);
                }
            }
            // Asking for nothing but an HRAM-only feature records the core alone at unit resolution.
            int specProb = Array.IndexOf(names, @"spec_match_prob");
            bool[] onlySpec = PinFeatureMask.RecordedFeatures(f => f != specProb, false, unit);
            for (int f = 0; f < names.Length; f++)
                Assert.AreEqual(f < PinFeatureMask.CORE_FEATURE_COUNT, onlySpec[f], names[f]);

            // The core set is the same at either resolution; the default set loses its HRAM-only members at unit resolution.
            var byCore = PinFeatureMask.FeatureSetMask(OspreyFeatureSet.core);
            CollectionAssert.AreEqual(PinFeatureMask.UnmaskedIndices(byCore),
                PinFeatureMask.UnmaskedIndices(PinFeatureMask.MaskAtResolution(byCore, unit)));
            CollectionAssert.AreEqual(PinFeatureMask.RecordedFeatures(byCore, false, hram),
                PinFeatureMask.RecordedFeatures(byCore, false, unit));
            var byDefault = PinFeatureMask.FeatureSetMask(OspreyFeatureSet.@default);
            int[] defaultHram = PinFeatureMask.UnmaskedIndices(PinFeatureMask.MaskAtResolution(byDefault, hram));
            int[] defaultUnit = PinFeatureMask.UnmaskedIndices(PinFeatureMask.MaskAtResolution(byDefault, unit));
            CollectionAssert.AreEqual(PinFeatureMask.FeatureSetIndices(OspreyFeatureSet.@default), defaultHram);
            CollectionAssert.AreEqual(defaultHram.Where(f => f < firstHramOnly).ToArray(), defaultUnit);
            Assert.IsTrue(defaultUnit.Length < defaultHram.Length, @"the default set has HRAM-only members");

            // Recording everything available keys as all at either resolution, and HRAM also counts the
            // features, so its outputs from before the families are not adopted. Unit keys do not change.
            bool[] unitAll = PinFeatureMask.RecordedFeatures(f => false, true, unit);
            bool[] hramAll = PinFeatureMask.RecordedFeatures(f => false, true, hram);
            Assert.AreEqual(@";featrec=all", PinFeatureMask.RecordedFeaturesValidityKeySuffix(f => unitAll[f], unit));
            Assert.AreEqual(@";featrec=all;featavail=109", PinFeatureMask.RecordedFeaturesValidityKeySuffix(f => hramAll[f], hram));
            Assert.AreNotEqual(@";featrec=all", PinFeatureMask.RecordedFeaturesValidityKeySuffix(f => unitAll[f], hram),
                @"at HRAM the unit-resolution columns are not all of them");
            Assert.AreEqual(string.Empty, PinFeatureMask.RecordedFeaturesValidityKeySuffix(f => onlySpec[f], unit));

            // The mask term likewise: core keys are the old default's everywhere (the default at unit
            // resolution), the default set lists what it uses at the resolution, all counts at HRAM,
            // and only an HRAM mask counts.
            const string coreMaskKey = @";featmask=default;fdrcols=unmasked";
            Assert.AreEqual(coreMaskKey, PinFeatureMask.ValidityKeySuffix(null, OspreyFeatureSet.core, hram));
            Assert.AreEqual(coreMaskKey, PinFeatureMask.ValidityKeySuffix(null, OspreyFeatureSet.core, unit));
            Assert.AreEqual(coreMaskKey, PinFeatureMask.ValidityKeySuffix(null, unit));
            Func<int[], string> setKey = used => @";featset=" + string.Join(@"+",
                used.Where(f => f >= PinFeatureMask.CORE_FEATURE_COUNT).Select(f => names[f])) + @";fdrcols=unmasked";
            Assert.AreEqual(setKey(defaultHram), PinFeatureMask.ValidityKeySuffix(null, hram));
            Assert.AreEqual(setKey(defaultHram), PinFeatureMask.ValidityKeySuffix(null, OspreyFeatureSet.@default, hram));
            Assert.AreEqual(setKey(defaultUnit), PinFeatureMask.ValidityKeySuffix(null, OspreyFeatureSet.@default, unit));
            Assert.AreEqual(@";featset=all;featavail=109;fdrcols=unmasked", PinFeatureMask.ValidityKeySuffix(null, OspreyFeatureSet.all, hram));
            Assert.AreEqual(@";featset=all;fdrcols=unmasked", PinFeatureMask.ValidityKeySuffix(null, OspreyFeatureSet.all, unit));
            Assert.AreEqual(@";featmask=none;fdrcols=unmasked", PinFeatureMask.ValidityKeySuffix(Array.Empty<string>(), OspreyFeatureSet.core, unit),
                @"a mask overrides the feature set");
            var none = Array.Empty<string>();
            Assert.AreEqual(@";featmask=none;fdrcols=unmasked", PinFeatureMask.ValidityKeySuffix(none, unit));
            Assert.AreEqual(@";featmask=none;featavail=109;fdrcols=unmasked", PinFeatureMask.ValidityKeySuffix(none, hram));
            var listed = new[] { @"tc_sum6", @"peak_width", @"tc_sum6" };
            Assert.AreEqual(@";featmask=peak_width+tc_sum6;fdrcols=unmasked", PinFeatureMask.ValidityKeySuffix(listed, unit));
            Assert.AreEqual(@";featmask=peak_width+tc_sum6;featavail=109;fdrcols=unmasked", PinFeatureMask.ValidityKeySuffix(listed, hram));
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
        ///
        /// The XIC fragment count follows the pick: it decides the peaks and
        /// features Stage 4 writes, so every task carries it. The feature mask
        /// follows the training selection: the mask is applied when the FDR stage
        /// loads features, so per-file scoring must NOT key on it. What scoring
        /// records keys it instead (<see cref="PinFeatureMask.RecordedFeaturesValidityKeySuffix()"/>),
        /// so a mask arm that records the same families adopts another arm's scores.
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
            string mask = PinFeatureMask.ValidityKeySuffix();

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
                StringAssert.Contains(key, XicTerm(), string.Format(
                    @"{0} must key on the XIC fragment count", task.Name));
                Assert.AreEqual(expectTrain, key.Contains(mask), string.Format(
                    @"{0} must {1} key on the feature mask",
                    task.Name, expectTrain ? @"" : @"NOT "));
                // The complement's q changed meaning without a byte or mode string moving (issue
                // #4581), so the two tasks that write it must key on it and the rest must not -
                // FirstPassFDR least of all, whose re-run costs hours at cohort scale.
                bool expectStrata = task.Name == PerFileRescoreTask.TASK_NAME ||
                                    task.Name == SecondPassFdrTask.TASK_NAME;
                Assert.AreEqual(expectStrata, key.Contains(OspreyEnvironment.PASS2_STRATA_TERM), string.Format(
                    @"{0} must {1} key on the protein-compact complement competition",
                    task.Name, expectStrata ? @"" : @"NOT "));
            }
            Assert.AreEqual(OspreyEnvironment.PASS2_STRATA_TERM, OspreyEnvironment.Pass2StrataValidityKeySuffix(),
                @"the default protein-compact mode carries the complement term");
            using (OspreyEnvironment.OverrideVariables(new Dictionary<string, string>
                   {
                       { @"OSPREY_PASS2_QVALUE", OspreyEnvironment.PASS2_QVALUE_TRANSFER }
                   }))
            {
                Assert.AreEqual(string.Empty, OspreyEnvironment.Pass2StrataValidityKeySuffix(),
                    @"transfer does not compete a complement, so its outputs keep their key");
            }
            AssertReconRefitKeysTheTasksItReaches(tasks, ctx);
            AssertTransferReconciliationSetKeysTheBlibGate(tasks);
        }

        /// <summary>
        /// Under transfer, reconciliation takes only the precursors the blib reports, and the blib
        /// gates on --fdr-level and --experiment-fdr, which no other key term covers. So
        /// FirstPassFDR (it writes the consensus), PerFileRescoring and SecondPassFDR key on both,
        /// and no other task does: resuming with a looser experiment FDR must not keep the
        /// reconciliation planned for the old reported set.
        /// </summary>
        private static void AssertTransferReconciliationSetKeysTheBlibGate(IReadOnlyList<OspreyTask> tasks)
        {
            var defaultCtx = new PipelineContext(new OspreyConfig(), tasks, null, null, null);
            var looserCtx = new PipelineContext(new OspreyConfig { ExperimentFdr = 0.05 }, tasks, null, null, null);
            var peptideCtx = new PipelineContext(new OspreyConfig { FdrLevel = FdrLevel.Peptide }, tasks, null, null, null);
            foreach (var task in tasks)
            {
                Assert.AreEqual(task.ValidityKey(defaultCtx), task.ValidityKey(looserCtx),
                    string.Format(@"{0} must not key on {1} under protein-compact", task.Name,
                        OspreyCommandArgs.ARG_EXPERIMENT_FDR.ArgumentText));
            }
            using (OspreyEnvironment.OverrideVariables(new Dictionary<string, string>
                   {
                       { @"OSPREY_PASS2_QVALUE", OspreyEnvironment.PASS2_QVALUE_TRANSFER }
                   }))
            {
                foreach (var task in tasks)
                {
                    bool expect = task.Name == FirstPassFdrTask.TASK_NAME ||
                                  task.Name == PerFileRescoreTask.TASK_NAME ||
                                  task.Name == SecondPassFdrTask.TASK_NAME;
                    string key = task.ValidityKey(defaultCtx);
                    Assert.AreEqual(expect, key != task.ValidityKey(looserCtx), string.Format(
                        @"{0} must {1} key on {2} under transfer", task.Name, expect ? @"" : @"NOT ",
                        OspreyCommandArgs.ARG_EXPERIMENT_FDR.ArgumentText));
                    Assert.AreEqual(expect, key != task.ValidityKey(peptideCtx), string.Format(
                        @"{0} must {1} key on {2} under transfer", task.Name, expect ? @"" : @"NOT ",
                        OspreyCommandArgs.ARG_FDR_LEVEL.ArgumentText));
                }
            }
        }

        /// <summary>
        /// OSPREY_RECON_REFIT=off plans reconciliation on each run's first-pass calibration, so
        /// the tasks downstream of the plan - PerFileRescoring and SecondPassFDR - key on it, and
        /// no task does when it is unset, so no existing output directory moves.
        /// </summary>
        private static void AssertReconRefitKeysTheTasksItReaches(IReadOnlyList<OspreyTask> tasks, PipelineContext ctx)
        {
            const string term = @";reconrefit=off";
            Assert.AreEqual(string.Empty, OspreyEnvironment.ReconRefitValidityKeySuffix(false));
            Assert.AreEqual(term, OspreyEnvironment.ReconRefitValidityKeySuffix(true));
            var unset = tasks.ToDictionary(task => task.Name, task => task.ValidityKey(ctx));
            foreach (var key in unset)
                Assert.IsFalse(key.Value.Contains(@"reconrefit"), key.Key + @" must not key on an unset OSPREY_RECON_REFIT");
            using (OspreyEnvironment.OverrideVariables(new Dictionary<string, string>
                   {
                       { @"OSPREY_RECON_REFIT", @"off" }
                   }))
            {
                Assert.AreEqual(term, OspreyEnvironment.ReconRefitValidityKeySuffix());
                foreach (var task in tasks)
                {
                    string key = task.ValidityKey(ctx);
                    bool expect = task.Name == PerFileRescoreTask.TASK_NAME || task.Name == SecondPassFdrTask.TASK_NAME;
                    Assert.AreEqual(expect, key.Contains(term), string.Format(
                        @"{0} must {1} key on OSPREY_RECON_REFIT=off", task.Name, expect ? @"" : @"NOT "));
                    // Nothing else in the key moves.
                    Assert.AreEqual(unset[task.Name], key.Replace(term, string.Empty), task.Name);
                }
            }
        }
    }
}
