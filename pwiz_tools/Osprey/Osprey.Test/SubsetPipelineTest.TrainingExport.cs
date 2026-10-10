/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The training export (<c>--training-export</c>, docs/22-training-export.md) through the
    /// whole pipeline on the committed subsets: every route that writes
    /// <c>&lt;stem&gt;.training.parquet</c> - in flight, the export-only arm on a finished
    /// analysis, <c>--task TrainingExport</c>, the export-only arm while the second pass is still
    /// outstanding, and a <c>--task PerFileRescoring</c> node - must write the same bytes, and adding
    /// the flag later must re-score nothing.
    /// </summary>
    public partial class SubsetPipelineTest
    {
        private const string RUN_Q_PASS_FIRST = @"1";
        private const string RUN_Q_PASS_SECOND = @"2";
        private const double EXPORT_MAX_Q = 0.01;

        /// <summary>
        /// Stellar: the flag up front, the flag added to a finished analysis, the same command
        /// again, <c>--task TrainingExport</c>, the flag added while the second pass is outstanding,
        /// and one run whose export cannot be written.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetTrainingExport()
        {
            // Up front: every task runs, and every run gets an export that reproduced the scored
            // median polish cosine for every fitted precursor.
            string upFrontDir = CreateDir(@"export-up-front");
            string log = RunAnalysis(upFrontDir, DataInputs(), Verifier(false), ExportArgs());
            AssertTasks(log, Array.Empty<string>(), ALL_TASKS);
            var upFront = AssertExports(upFrontDir, RUN_NAMES, RUN_Q_PASS_SECOND);

            // Without the flag no export exists and the library is the same; with it added, every
            // other task skips, nothing is re-scored, and each export is the up-front one byte for byte.
            string payLaterDir = CreateDir(@"export-pay-later");
            RunAnalysis(payLaterDir, DataInputs(), Verifier(false));
            Assert.AreEqual(0, Directory.GetFiles(payLaterDir, @"*" + TrainingExportParquet.EXT, SearchOption.AllDirectories).Length,
                @"an export was written without the flag");
            AssertBlibsEqual(Path.Combine(upFrontDir, BLIB_FILE), Path.Combine(payLaterDir, BLIB_FILE));
            log = RunAnalysis(payLaterDir, DataInputs(), Verifier(false), ExportArgs());
            AssertExportOnlyArm(log, RUN_NAMES);
            AssertExportsEqual(upFront, payLaterDir);

            // The same command again writes nothing.
            log = RunAnalysis(payLaterDir, DataInputs(), Verifier(false), ExportArgs());
            AssertTasks(log, ALL_TASKS, Array.Empty<string>());
            AssertExportsEqual(upFront, payLaterDir);

            // --task TrainingExport on a finished analysis is the same request.
            string selectorDir = CreateDir(@"export-selector");
            RunAnalysis(selectorDir, DataInputs(), Verifier(false));
            log = RunAnalysis(selectorDir, DataInputs(), Verifier(false),
                OspreyCommandArgs.ARG_TASK.ArgumentText, TrainingExportTask.TASK_NAME);
            AssertExportOnlyArm(log, RUN_NAMES);
            AssertExportsEqual(upFront, selectorDir);

            // Added while the second pass is outstanding: the exports come from the export-only arm,
            // re-scoring nothing, and the second pass then writes the same library.
            string outstandingDir = CreateDir(@"export-second-pass-outstanding");
            RunAnalysis(outstandingDir, DataInputs(), Verifier(false));
            DeleteSecondPassOutputs(Path.Combine(outstandingDir, BLIB_FILE));
            log = RunAnalysis(outstandingDir, DataInputs(), Verifier(false), ExportArgs());
            AssertTasks(log, new[] { PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME },
                new[] { PerFileRescoreTask.TASK_NAME, SecondPassFdrTask.TASK_NAME });
            AssertNoRecompute(log);
            AssertExportsEqual(upFront, outstandingDir);
            AssertBlibsEqual(Path.Combine(upFrontDir, BLIB_FILE), Path.Combine(outstandingDir, BLIB_FILE));

            AssertFailedExportIsRetriedAlone(payLaterDir, upFront);
        }

        /// <summary>
        /// Astral (HRAM: ppm tolerances, MS1 isotope scoring): up front and added to a finished
        /// analysis give the same exports.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestAstralSubsetTrainingExport()
        {
            string dataDir = Path.Combine(_testDir, @"astral-data");
            ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, @"TestData", ASTRAL_ZIP), dataDir);

            string upFrontDir = CreateDir(@"astral-export-up-front");
            string log = RunOsprey(AstralExportArgs(dataDir, upFrontDir, ExportArgs()), Verifier(false));
            AssertTasks(log, Array.Empty<string>(), ALL_TASKS);
            var upFront = AssertExports(upFrontDir, ASTRAL_RUN_NAMES, RUN_Q_PASS_SECOND);
            foreach (string run in ASTRAL_RUN_NAMES)
            {
                TrainingExportParquet.Read(ExportPath(upFrontDir, run), out var footer);
                Assert.AreEqual(@"ppm", footer[@"osprey.fragment_tolerance_unit"], run + @" was not searched as HRAM");
            }

            string payLaterDir = CreateDir(@"astral-export-pay-later");
            RunOsprey(AstralExportArgs(dataDir, payLaterDir), Verifier(false));
            log = RunOsprey(AstralExportArgs(dataDir, payLaterDir, ExportArgs()), Verifier(false));
            AssertExportOnlyArm(log, ASTRAL_RUN_NAMES);
            AssertExportsEqual(upFront, payLaterDir);
        }

        /// <summary>
        /// An HPC <c>--task PerFileRescoring</c> node given its run's artifacts writes that run's
        /// export: byte-identical to the straight-through one when the data file is on the node,
        /// and with the same rows but empty source footer keys when it is not.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetTrainingExportOnRescoreNode()
        {
            const string straightName = @"export-straight";
            string straightDir = CreateDir(straightName);
            RunAnalysis(straightDir, DataInputs(), Verifier(false), ExportArgs());
            var straight = AssertExports(straightDir, RUN_NAMES, RUN_Q_PASS_SECOND);

            string run = RUN_NAMES[0];
            foreach (bool withDataFile in new[] { true, false })
            {
                string nodeDir = CreateDir(withDataFile ? @"rescore-node" : @"rescore-node-no-data");
                ShipFiles(straightName, nodeDir, run + SPECTRA_CACHE_EXTENSION, run + @".scores.parquet",
                    run + @".calibration.json", run + @".1st-pass.fdr_scores.bin", run + @".reconciliation.json",
                    @"output.1st-pass.retained_base_ids.bin");
                ShipFilesIfPresent(straightName, nodeDir, run + @".1st-pass.model.json", run + @".1st-pass.stratum.json",
                    @"output.1st-pass.fdr_experiment.bin", @"output.1st-pass.model-diagnostics.json");
                if (withDataFile)
                    File.Copy(Path.Combine(_dataDir, run + MZML_EXTENSION), Path.Combine(nodeDir, run + MZML_EXTENSION));
                string log = RunNodeTask(nodeDir, PerFileRescoreTask.TASK_NAME, run, ExportArgs());
                AssertHasLine(log, PathLine(LogKey.ROUTE_RESCORE_HYDRATE, @"per-run"));

                string export = ExportPath(nodeDir, run);
                if (withDataFile)
                {
                    CollectionAssert.AreEqual(straight[run], HashArtifactContent(export), run + @": the node's export differs from the straight-through one");
                    continue;
                }
                var records = TrainingExportParquet.Read(export, out var footer);
                var straightRecords = TrainingExportParquet.Read(ExportPath(straightDir, run), out _);
                CollectionAssert.AreEqual(straightRecords.Select(r => r.EntryId).ToArray(), records.Select(r => r.EntryId).ToArray(),
                    run + @": without its data file the node exported other rows");
                Assert.AreEqual(string.Empty, footer[@"osprey.instrument_model"]);
                Assert.AreEqual(@"0", footer[@"osprey.source_ms2_sampled"]);
                Assert.AreEqual(string.Empty, footer[@"osprey.ms2_mass_analyzers"]);
            }
        }

        /// <summary>
        /// A library that supplies its own decoys and carries shuffled entrapment peptides,
        /// exported at every run q: no decoy is exported, entrapment precursors are, and each row
        /// is marked by kind as the FDRBench manifest spells it.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetTrainingExportLibraryDecoysAndEntrapment()
        {
            string workDir = CreateDir(@"export-libdecoy");
            RunAnalysis(workDir, DataInputs(), LIBDECOY_FILE, Verifier(false),
                OspreyCommandArgs.ARG_DECOYS_IN_LIBRARY.ArgumentText,
                OspreyCommandArgs.ARG_DECOY_PAIRING_MANIFEST.ArgumentText, Path.Combine(_dataDir, LIBDECOY_PAIRING_FILE),
                OspreyCommandArgs.ARG_TRAINING_EXPORT.ArgumentText,
                OspreyCommandArgs.ARG_TRAINING_EXPORT_MAX_Q.ArgumentText, @"1");
            AssertExports(workDir, RUN_NAMES, RUN_Q_PASS_SECOND, 1.0);
            var records = RUN_NAMES.SelectMany(run => TrainingExportParquet.Read(ExportPath(workDir, run), out _)).ToList();
            Assert.IsTrue(records.Any(r => r.IsEntrapment), @"no entrapment precursor was exported");
            Assert.IsTrue(records.Any(r => !r.IsEntrapment), @"no target precursor was exported");
            foreach (var record in records)
            {
                Assert.AreEqual(record.IsEntrapment ? @"p_target" : @"target", record.PeptideKind,
                    record.ModifiedSequence + @" is marked with the wrong kind");
            }
        }

        /// <summary>
        /// A one-run analysis, which re-scores nothing in Stage 6 - no multi-charge consensus,
        /// no cross-run reconciliation, no gap-fill - under both pass-2 modes (#4729, #4665).
        ///
        /// <para>Such an analysis used to fail in SecondPassFDR ("No second-pass experiment-scope
        /// records were published"): the run never reached the per-run second pass, because the
        /// worker answered only the runs it re-scored, and Stage 7 published nothing when nothing
        /// was re-scored anywhere. Now the worker answers every run, so the run's second pass
        /// comes from PerFileRescoring like any other: the analysis finishes, the run's
        /// .2nd-pass.fdr_scores.bin names PerFileRescoring as its producer, and the export
        /// selects on the second-pass run q. Two more invocations of the same command leave the
        /// export as it is.</para>
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetTrainingExportSingleRun()
        {
            string run = RUN_NAMES[0];
            var inputs = DataInputs().Take(1).ToArray();
            foreach (string mode in new[] { OspreyEnvironment.PASS2_QVALUE_PROTEIN_COMPACT, OspreyEnvironment.PASS2_QVALUE_TRANSFER })
            {
                string workDir = CreateDir(@"export-single-run-" + mode);
                RunSingleRunExport(workDir, inputs, mode);
                // The no-work path, asserted rather than assumed: a run Stage 6 re-scored would
                // reach the worker the ordinary way and test nothing new.
                Assert.AreEqual(@"0", ParquetScoreCache.LoadFooterMetadata(Path.Combine(workDir,
                        run + ParquetScoreCache.EXT_SCORES_RECONCILED))[@"osprey.rescored"],
                    mode + @": Stage 6 re-scored this run, so the no-work path was not taken");
                string pass2Path = Path.Combine(workDir,
                    PassArtifact(run, FdrScoresSidecar.Pass.SecondPass, FdrScoresSidecar.EXT));
                Assert.AreEqual(PerFileRescoreTask.TASK_NAME,
                    FdrScoresSidecar.ReadStamp(pass2Path, FdrScoresSidecar.Pass.SecondPass)?.Task,
                    mode + @": the run's second-pass sidecar was not written by re-scoring");
                Assert.IsTrue(BlibComparer.CountRows(Path.Combine(workDir, BLIB_FILE), @"RefSpectra") > 0,
                    mode + @": the one-run analysis reported no precursors");
                var hashes = AssertExports(workDir, new[] { run }, RUN_Q_PASS_SECOND);

                for (int invocation = 2; invocation <= 3; invocation++)
                {
                    string log = RunSingleRunExport(workDir, inputs, mode);
                    Assert.IsFalse(HasLine(log, ExportLine(run)),
                        string.Format(@"{0}: invocation {1} of the same command wrote the export again", mode, invocation) +
                        Environment.NewLine + log);
                    AssertExportsEqual(hashes, workDir);
                }
            }
        }

        /// <summary>
        /// The export command for a one-run analysis under <paramref name="pass2Mode"/>, which
        /// must finish.
        /// </summary>
        private string RunSingleRunExport(string workDir, IEnumerable<string> inputs, string pass2Mode)
        {
            var args = InputArgs(inputs).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(_dataDir, LIBRARY_FILE),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir
            }).Concat(CommonArgs()).Concat(ExportArgs()).ToArray();
            var variables = Verifier(false).ToDictionary(kv => kv.Key, kv => kv.Value);
            variables[@"OSPREY_PASS2_QVALUE"] = pass2Mode;
            string output;
            int exitCode;
            using (OspreyEnvironment.OverrideVariables(variables))
            {
                exitCode = InProcessOsprey.Run(args, out output);
            }
            Assert.AreEqual(Program.EXIT_CODE_SUCCESS, exitCode, pass2Mode + Environment.NewLine + output);
            return output;
        }

        /// <summary>
        /// One run's export path held by a directory: the run fails with that run named and exit
        /// code 1, no other export and not the library is rewritten, and the same command with the
        /// path clear writes that export alone, byte-identical to the up-front one.
        /// </summary>
        private void AssertFailedExportIsRetriedAlone(string workDir, Dictionary<string, byte[]> expected)
        {
            string run = RUN_NAMES[0];
            string export = ExportPath(workDir, run);
            File.Delete(export);
            Directory.CreateDirectory(export);
            string blib = Path.Combine(workDir, BLIB_FILE);
            byte[] blibHash = HashFile(blib);

            string output;
            int exitCode;
            using (OspreyEnvironment.OverrideVariables(Verifier(false)))
            {
                exitCode = InProcessOsprey.Run(ExportAnalysisArgs(workDir), out output);
            }
            Assert.AreNotEqual(Program.EXIT_CODE_SUCCESS, exitCode, output);
            var errors = SplitLines(output).Where(CommandStatusWriter.IsErrorLine).ToList();
            Assert.AreEqual(1, errors.Count, output);
            StringAssert.Contains(errors[0], Path.Combine(_dataDir, run + MZML_EXTENSION));
            AssertTasks(output, new[] { PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME },
                new[] { PerFileRescoreTask.TASK_NAME });
            Assert.IsFalse(HasLine(output, TaskLine(SecondPassFdrTask.TASK_NAME, @"starting")),
                @"the second pass ran after a failed export" + Environment.NewLine + output);
            AssertExportLines(output, Array.Empty<string>());
            CollectionAssert.AreEqual(blibHash, HashFile(blib), @"the failed run rewrote the library");

            Directory.Delete(export);
            string log = RunAnalysis(workDir, DataInputs(), Verifier(false), ExportArgs());
            AssertExportLines(log, new[] { run });
            AssertExportsEqual(expected, workDir);
        }

        /// <summary>
        /// The flag added to a finished analysis: the three other tasks skip, PerFileRescoring
        /// runs and re-scores nothing, and every run is exported.
        /// </summary>
        private static void AssertExportOnlyArm(string log, IReadOnlyCollection<string> runs)
        {
            AssertTasks(log, new[] { PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME, SecondPassFdrTask.TASK_NAME },
                new[] { PerFileRescoreTask.TASK_NAME });
            AssertNoRecompute(log);
            AssertExportLines(log, runs);
        }

        /// <summary>Exactly the runs in <paramref name="runs"/> logged an export.</summary>
        private static void AssertExportLines(string log, IReadOnlyCollection<string> runs)
        {
            foreach (string run in RUN_NAMES.Concat(ASTRAL_RUN_NAMES))
            {
                Assert.AreEqual(runs.Contains(run), HasLine(log, ExportLine(run)),
                    run + (runs.Contains(run) ? @" was not exported" : @" was exported again") + Environment.NewLine + log);
            }
        }

        /// <summary>
        /// Each run's export: rows written and the footer's count of them, every row a target at
        /// run q within the threshold, the run q from <paramref name="runQPass"/>, the scored median
        /// polish cosine reproduced for every fitted row, and the data file read for the source
        /// footer. Returns each run's file hash.
        /// </summary>
        private static Dictionary<string, byte[]> AssertExports(string workDir, IEnumerable<string> runs, string runQPass,
            double maxQ = EXPORT_MAX_Q)
        {
            var hashes = new Dictionary<string, byte[]>();
            foreach (string run in runs)
            {
                string path = ExportPath(workDir, run);
                var records = TrainingExportParquet.Read(path, out var footer);
                Assert.IsTrue(records.Count > 0, run + @" exported no precursors");
                Assert.AreEqual(records.Count.ToString(CultureInfo.InvariantCulture), footer[@"osprey.training_export.rows"]);
                Assert.AreEqual(run, footer[@"osprey.file_name"]);
                Assert.AreEqual(runQPass, footer[TrainingExportWriter.KEY_RUN_Q_PASS], run);
                Assert.IsTrue(records.All(r => !r.IsDecoy && r.RunPrecursorQ <= maxQ && r.FileName == run),
                    run + @" exported a decoy, another run's row or a row above the threshold");
                int fitted = records.Count(r => r.MpFitted);
                Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, @"{0}/{0}", fitted),
                    footer[@"osprey.training_export.mp_cosine_parity_fitted"],
                    run + @": the export did not reproduce the scored median polish cosine");
                Assert.AreNotEqual(@"0", footer[@"osprey.source_ms2_sampled"], run + @": the data file was not read");
                AssertMassAnalyzers(footer, run);
                hashes[run] = HashArtifactContent(path);
            }
            return hashes;
        }

        /// <summary>
        /// The footer's MS2 mass analyzers: every sampled spectrum counted under its instrument's MS2
        /// analyzers as pwiz joins them, a Stellar's ion trap or an Astral's quadrupole and Astral analyzer.
        /// </summary>
        private static void AssertMassAnalyzers(Dictionary<string, string> footer, string run)
        {
            string analyzer = ASTRAL_RUN_NAMES.Contains(run)
                ? @"quadrupole/asymmetric track lossless time-of-flight analyzer"
                : @"radial ejection linear ion trap";
            string expected = JsonConvert.SerializeObject(new SortedDictionary<string, int>
            {
                { analyzer, int.Parse(footer[@"osprey.source_ms2_sampled"], CultureInfo.InvariantCulture) },
            });
            Assert.AreEqual(expected, footer[@"osprey.ms2_mass_analyzers"], run);
        }

        private static void AssertExportsEqual(Dictionary<string, byte[]> expected, string workDir)
        {
            foreach (var pair in expected)
            {
                CollectionAssert.AreEqual(pair.Value, HashArtifactContent(ExportPath(workDir, pair.Key)),
                    pair.Key + @": the export differs from the up-front one");
            }
        }

        private static string ExportPath(string workDir, string run)
        {
            var found = Directory.GetFiles(workDir, run + TrainingExportParquet.EXT, SearchOption.AllDirectories);
            Assert.AreEqual(1, found.Length, @"expected one export for " + run + @" under " + workDir);
            return found[0];
        }

        private static string ExportLine(string run)
        {
            return @"[" + LogTag.TRAIN_EXPORT + @"] " + run + @":";
        }

        private static string[] ExportArgs()
        {
            return new[] { OspreyCommandArgs.ARG_TRAINING_EXPORT.ArgumentText };
        }

        /// <summary>The command line <see cref="RunAnalysis(string,IEnumerable{string},IReadOnlyDictionary{string,string},string[])"/> runs, with the export on.</summary>
        private string[] ExportAnalysisArgs(string workDir)
        {
            return InputArgs(DataInputs()).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(_dataDir, LIBRARY_FILE),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir
            }).Concat(CommonArgs()).Concat(ExportArgs()).ToArray();
        }

        /// <summary>
        /// <see cref="RunTask"/> for one run, with <paramref name="extraArgs"/>: the task in a
        /// directory holding only what the node was shipped, plus the library.
        /// </summary>
        private string RunNodeTask(string nodeDir, string taskName, string run, params string[] extraArgs)
        {
            string library = Path.Combine(nodeDir, LIBRARY_FILE);
            File.Copy(Path.Combine(_dataDir, LIBRARY_FILE), library);
            var args = new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, taskName }
                .Concat(InputArgs(RunNames(nodeDir, MZML_EXTENSION, run)))
                .Concat(new[]
                {
                    OspreyCommandArgs.ARG_LIBRARY.ArgumentText, library,
                    OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(nodeDir, BLIB_FILE)
                }).Concat(CommonArgs()).Concat(extraArgs);
            return RunOsprey(args.ToArray(), Verifier(false));
        }

        private string[] AstralExportArgs(string dataDir, string workDir, params string[] extraArgs)
        {
            return InputArgs(ASTRAL_RUN_NAMES.Select(run => Path.Combine(dataDir, run + MZML_EXTENSION))).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(dataDir, ASTRAL_LIBRARY_FILE),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir,
                OspreyCommandArgs.ARG_RESOLUTION.ArgumentText, @"hram",
                OspreyCommandArgs.ARG_PROTEIN_FDR.ArgumentText, @"0.01",
                OspreyCommandArgs.ARG_THREADS.ArgumentText, @"4",
                OspreyCommandArgs.ARG_PERF_STATS.ArgumentText
            }).Concat(extraArgs).ToArray();
        }
    }
}
