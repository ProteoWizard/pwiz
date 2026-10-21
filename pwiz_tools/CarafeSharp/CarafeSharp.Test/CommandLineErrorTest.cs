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
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Contracts;
using pwiz.Common.CommonResources;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Models.Modules;
using pwiz.CarafeSharp.Proteome;
using pwiz.CarafeSharp.Tasks;
using pwiz.Common.SystemUtil;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// CarafeSharp's command line as the executable runs it: each mode dispatched, a bad or
    /// missing argument stopping the run with the message and the usage before any work, a
    /// failure during the work reported with its cause, and a failed run leaving no final or
    /// partial output behind.
    /// </summary>
    [TestClass]
    public class CommandLineErrorTest
    {
        private const string PROTEINS = ">sp|P1|A\nMPEPTIDEKSAMPLERLVNELTEFAKGAGSSEPVTGLDAKYILAGVENSK\n";

        public TestContext TestContext { get; set; }

        private string _folder;

        [TestInitialize]
        public void CreateFolder()
        {
            _folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"CommandLine_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void DeleteFolder()
        {
            SQLiteConnection.ClearAllPools();
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void TestArgumentErrors()
        {
            // No arguments, or -h, prints the usage and succeeds.
            AssertHelp();
            AssertHelp(@"-h");

            // Every error Carafe would stop on, and every option CarafeSharp does not port: exit 1,
            // the message and the usage on the error stream, nothing on the output.
            AssertUsageError(@"-bogus");
            AssertUsageError(@"--print-ptm");
            AssertUsageError(@"--db");
            AssertUsageError(@"stray");
            AssertUsageError(@"--build-koina-library", @"x");
            AssertUsageError(@"--reconcile-manifest", @"o.tsv", @"--predicted-library", @"l.tsv");
            AssertUsageError(@"--i", @"x.training.parquet", @"--mode", @"phospho");
            AssertUsageError(@"--i", @"x.training.parquet", @"--cs");
            AssertUsageError(@"--i", @"x.training.parquet", @"--device", @"tpu");
            AssertUsageError(@"--db=");
            // Carafe predicts no ion mobility for -tf rt or ms2, and then fails reading it.
            AssertUsageError(@"--db", @"x.fasta", @"--model-dir", _folder, @"--tf", @"rt", @"--ccs");
            AssertUsageError(@"--i", @"x.training.parquet", @"--db", @"x.fasta", @"--tf", @"ms2", @"--ccs");
            AssertUsageError(@"--db", @"x.fasta", @"--user-var-mods", @"1");
            AssertUsageError(@"--db", @"x.fasta", @"--mode", @"phospho");
            AssertUsageError(@"--db", @"x.fasta", @"--lf-format", @"parquet");
            // mzSpecLib only with -fast: without it Carafe writes any -lf_type as a TSV.
            AssertUsageError(@"--db", @"x.fasta", @"--lf-type", @"mzSpecLib", @"--fast");
            AssertUsageError(@"--db", @"x.fasta", @"--model-dir", _folder, @"--tf", @"test");
            // -model is CarafeSharp's: only training's -tf values, in any case.
            AssertUsageError(@"--db", @"x.fasta", @"--model", @"m.carafemodel", @"--tf", @"foo");
            Assert.AreEqual(@"ms2", CarafeCommandLine.Parse(new[] { @"--db", @"x.fasta", @"--model", @"m.carafemodel", @"--tf", @"MS2" })
                .LibrarySettings.TrainingType);
            // -model_dir keeps Carafe's reading, where another value predicts with neither fine-tuned model, and says so.
            Assert.IsTrue(CarafeCommandLine.Parse(new[] { @"--db", @"x.fasta", @"--model-dir", _folder, @"--tf", @"MS2" }).Warnings
                .Any(w => w.StartsWith(@"--tf MS2 is not all, ms2 or rt", StringComparison.Ordinal)));
            AssertUsageError(@"--db", @"x.fasta", @"--fix-mod", @"abc");
            AssertUsageError(@"--db", @"x.fasta", @"--max-pep-charge", @"1");
            AssertUsageError(@"--db", @"x.fasta", @"--nce", @"30x");
            AssertUsageError(@"--build-entrapment-fasta", @"o.fasta", @"--db", @"i.fasta", @"--mz-filter", @"--user-var-mods", @"1");
            AssertUsageError(@"--build-entrapment-fasta", @"o.fasta", @"--db", @"i.fasta", @"--decoy-seed", @"1.5");

            // The options those modes do read.
            var library = CarafeCommandLine.Parse(new[] { @"--db", @"x.fasta", @"--nce", @"30", @"--ms-instrument", @"QE", @"--rt-max", @"45",
                @"--pairing-manifest", @"m.tsv", @"--decoy-prefix", @"rev_" }).LibrarySettings;
            Assert.AreEqual(30.0, library.Nce);
            Assert.AreEqual(@"QE", library.Instrument);
            Assert.IsTrue(library.UserInstrument);
            Assert.AreEqual(45.0, library.RtMax);
            Assert.AreEqual(@"m.tsv", library.PairingManifest);
            Assert.AreEqual(@"rev_", library.DecoyPrefix);
            var training = CarafeCommandLine.Parse(new[] { @"--i", @"x.training.parquet", @"--nce", @"28", @"--ms-instrument", @"QE" }).TrainingSettings;
            Assert.AreEqual(28.0, training.Nce);
            Assert.AreEqual(@"QE", training.Instrument);
            // -ccs with training, as Carafe on Osprey's results: no CCS fine-tune, the library's 1/K0 from the CCS model.
            var withMobility = CarafeCommandLine.Parse(new[] { @"--i", @"x.training.parquet", @"--db", @"x.fasta", @"--ccs" });
            Assert.IsTrue(withMobility.TrainingSettings.Library.PredictIonMobility);
            Assert.IsTrue(withMobility.Warnings.Any(w => w.StartsWith(@"--ccs: the CCS model is not fine-tuned", StringComparison.Ordinal)));
            Assert.IsTrue(CarafeCommandLine.Parse(new[] { @"--i", @"x.training.parquet", @"--ccs" }).Warnings
                .Any(w => w.StartsWith(@"Ignored --ccs", StringComparison.Ordinal)));
            Assert.AreEqual(7L, CarafeCommandLine.Parse(new[] { @"--build-entrapment-fasta", @"o.fasta", @"--db", @"i.fasta", @"--decoy-seed", @"7" })
                .BuildSettings.DecoySeed);
        }

        [TestMethod]
        public void TestFailuresBeforeWork()
        {
            string proteins = WriteText(@"proteins.fasta", PROTEINS);
            string output = Path.Combine(_folder, @"out");
            string missingFasta = Path.Combine(_folder, @"missing.fasta");
            string missingZip = Path.Combine(_folder, @"missing.zip");
            // Training checks the library FASTA, the start model and the pretrained models before
            // it creates the output folder or reads an export.
            string export = Path.Combine(_folder, @"run" + TrainingExportParquet.EXT);
            AssertRunError(output, @"--i", export, @"--o", output, @"--db", missingFasta);
            AssertRunError(output, @"--i", export, @"--o", output, @"--ms2-model", Path.Combine(_folder, @"missing.pt"));
            AssertRunError(output, @"--i", export, @"--o", output, @"--pretrained", missingZip);
            // Library prediction opens the models before the digest: the folder may be made, but it stays empty.
            AssertRunError(output, @"--db", proteins, @"--o", output, @"--pretrained", missingZip);
            Assert.AreEqual(0, Directory.GetFileSystemEntries(output).Length);
            // A stage-1 build of a FASTA that is not there.
            string built = Path.Combine(_folder, @"built.fasta");
            AssertRunError(built, @"--build-entrapment-fasta", built, @"--db", missingFasta);

            // A failure with a cause reports both: a .blib library that is not a database.
            string manifest = WriteText(@"manifest.tsv", PairingManifest.HEADER + "\nPEPTIDEK\tNo\tP1\ttarget\t0\n");
            string notBlib = WriteText(@"library.blib", @"not a database");
            string reconciled = Path.Combine(_folder, @"reconciled.tsv");
            var (code, _, error) = Run(@"--reconcile-manifest", reconciled, @"--manifest", manifest, @"--predicted-library", notBlib);
            Assert.AreEqual(1, code);
            Assert.IsTrue(Lines(error).Length >= 2, error);
            StringAssert.Contains(Lines(error)[0], notBlib);
            Assert.IsFalse(File.Exists(reconciled));
        }

        [TestMethod]
        public void TestModesAndPartialOutput()
        {
            string proteins = WriteText(@"proteins.fasta", PROTEINS);

            // Stage 1, with a warning on the error stream, and the manifest reconciled against a library.
            string built = Path.Combine(_folder, @"built.fasta");
            string manifest = Path.Combine(_folder, @"manifest.tsv");
            var (code, output, error) = Run(@"--build-entrapment-fasta", built, @"--db", proteins, @"--manifest", manifest, @"--no-similarity-gate");
            Assert.AreEqual(0, code, error);
            Assert.IsTrue(File.Exists(built) && File.Exists(manifest));
            Assert.AreEqual(1, Lines(error).Length, error);
            Assert.AreNotEqual(0, output.Length);
            string library = WriteText(@"library.tsv", PairingManifestReconciler.LIBRARY_SEQUENCE_COLUMN + "\nPEPTIDEK\nSAMPLER\n");
            string reconciled = Path.Combine(_folder, @"reconciled.tsv");
            (code, _, error) = Run(@"--reconcile-manifest", reconciled, @"--manifest", manifest, @"--predicted-library", library);
            Assert.AreEqual(0, code, error);
            Assert.IsTrue(File.Exists(reconciled));

            // Library prediction from a folder of random models, as a .blib and a TSV. When the
            // finished .blib cannot take its final name (a folder there here; a library open in
            // another program since the run began is the same), the run fails and the library is
            // saved beside it as "name (2)", the .blib and the TSV with one number, which the error
            // names; no partial file is left, and what was there before is unchanged.
            string models = WriteRandomModels();
            string libraryFolder = Path.Combine(_folder, @"library");
            string blocker = Path.Combine(libraryFolder, BlibLibraryWriter.FILE_NAME);
            Directory.CreateDirectory(blocker);
            var predict = new[] { @"--db", proteins, @"--o", libraryFolder, @"--model-dir", models, @"--lf-type", @"blib,DIA-NN",
                @"--rt-max", @"30", @"--lf-min-n-frag", @"1", @"--device", @"cpu" };
            (code, _, error) = Run(predict);
            Assert.AreEqual(1, code, error);
            string savedBlib = Path.Combine(libraryFolder, @"carafe_spectral_library (2).blib");
            string savedTsv = Path.Combine(libraryFolder, @"carafe_spectral_library (2).tsv");
            CollectionAssert.AreEquivalent(new[] { blocker, savedBlib, savedTsv }, Directory.GetFileSystemEntries(libraryFolder));
            StringAssert.Contains(error, savedBlib);
            StringAssert.Contains(error, savedTsv);
            File.Delete(savedBlib);
            File.Delete(savedTsv);
            // Without the obstacle the same command writes both, and nothing else.
            Directory.Delete(blocker);
            (code, _, error) = Run(predict);
            Assert.AreEqual(0, code, error);
            var written = new[] { blocker, Path.Combine(libraryFolder, CarafeLibraryTsvWriter.FILE_NAME) };
            CollectionAssert.AreEquivalent(written, Directory.GetFileSystemEntries(libraryFolder));
            if (OperatingSystem.IsWindows())
            {
                // A library open in another program fails the run before any work, leaving the folder as it was.
                // (With a changed setting: the same command would be skipped, its stamped library being current.)
                using (new FileStream(blocker, FileMode.Open, FileAccess.Read, FileShare.Read))
                    (code, _, error) = Run(predict.Concat(new[] { @"--nce", @"29" }).ToArray());
                Assert.AreEqual(1, code, error);
                StringAssert.Contains(error, blocker + @" cannot be replaced");
                CollectionAssert.AreEquivalent(written, Directory.GetFileSystemEntries(libraryFolder));
            }

            // The library of stage 1's peptide FASTA with its pairing manifest carries Carafe's
            // DecoyPairs table (and, without -rt_max, iRT retention times): every pair one target
            // and one decoy of the same charge, the decoy the reverse of the target when so named.
            string pairedFolder = Path.Combine(_folder, @"paired");
            (code, _, error) = Run(@"--db", built, @"--o", pairedFolder, @"--model-dir", models, @"--lf-type", @"blib",
                @"--lf-min-n-frag", @"1", @"--device", @"cpu", @"--pairing-manifest", manifest);
            Assert.AreEqual(0, code, error);
            AssertDecoyPairs(Path.Combine(pairedFolder, BlibLibraryWriter.FILE_NAME));
            // A minimum of fragments that only one member of some pairs reaches: each pair is still
            // written whole, the member whose partner fell short left out with it.
            string wholeFolder = Path.Combine(_folder, @"whole");
            (code, output, error) = Run(@"--db", built, @"--o", wholeFolder, @"--model-dir", models, @"--lf-type", @"blib",
                @"--lf-min-n-frag", @"16", @"--device", @"cpu", @"--pairing-manifest", manifest);
            Assert.AreEqual(0, code, error);
            string droppedLine = Lines(output).Single(l => l.StartsWith(@"Pairs: dropped ", StringComparison.Ordinal));
            Assert.IsTrue(int.Parse(droppedLine.Split(' ')[2]) > 0, droppedLine);
            // Progress (the shared ProgressReporter) lands in the same stream as the log lines.
            StringAssert.Contains(output, string.Format(MessageResources.ProgressReporter_ProgressReporter__0____, @"Digesting proteins"));
            AssertDecoyPairs(Path.Combine(wholeFolder, BlibLibraryWriter.FILE_NAME));
            AssertPairsWhole(Path.Combine(wholeFolder, BlibLibraryWriter.FILE_NAME), manifest);
        }

        [TestMethod]
        public void TestOutputAndErrors()
        {
            // -log_file takes every line, stamped by -timestamp, and the console streams stay empty.
            string log = Path.Combine(_folder, @"run.log");
            string missingModel = Path.Combine(_folder, @"missing.carafemodel");
            var (code, output, error) = Run(@"--model-info", missingModel, @"--log-file", log, @"--timestamp");
            Assert.AreEqual(Program.EXIT_CODE_FAILURE_TO_START, code);
            Assert.AreEqual(string.Empty, output);
            Assert.AreEqual(string.Empty, error);
            var logLines = Lines(File.ReadAllText(log));
            Assert.AreEqual(1, logLines.Length, string.Join(Environment.NewLine, logLines));
            Assert.IsTrue(logLines[0].StartsWith(@"[", StringComparison.Ordinal), logLines[0]);
            Assert.IsTrue(CommandStatusWriter.IsErrorLine(logLines[0]), logLines[0]);
            StringAssert.Contains(logLines[0], missingModel);
            // A log file that cannot be opened is itself reported, on the console.
            (code, _, error) = Run(@"--model-info", missingModel, @"--log-file", _folder);
            Assert.AreEqual(Program.EXIT_CODE_FAILURE_TO_START, code);
            Assert.IsTrue(CommandStatusWriter.IsErrorLine(Lines(error)[0]), error);
            // A usage error is an error line too, before the usage.
            (code, _, error) = Run(@"-bogus");
            Assert.AreEqual(Program.EXIT_CODE_FAILURE_TO_START, code);
            Assert.IsTrue(CommandStatusWriter.IsErrorLine(Lines(error)[0]), error);

            // What a failure reports: a user-actionable exception is its message (and its root
            // cause, when it has one); a programming defect is the whole exception, type and stack.
            Assert.AreEqual(@"gone", Program.DescribeFailure(new FileNotFoundException(@"gone")));
            Assert.AreEqual(@"bad value", Program.DescribeFailure(new ArgumentException(@"bad value")));
            string withCause = Program.DescribeFailure(new InvalidDataException(@"outer", new FormatException(@"inner")));
            Assert.IsTrue(withCause.StartsWith(@"outer", StringComparison.Ordinal), withCause);
            StringAssert.Contains(withCause, @"inner");
            Assert.AreEqual(@"on a worker", Program.DescribeFailure(new AggregateException(new IOException(@"on a worker"))));
            string defect = Program.DescribeFailure(new InvalidOperationException(@"defect"));
            StringAssert.Contains(defect, nameof(InvalidOperationException));
            StringAssert.Contains(defect, @"defect");

            // The exit code and the log agree: an error line under a success code is "ran with errors".
            Assert.AreEqual(Program.EXIT_CODE_RAN_WITH_ERRORS, Program.ReconcileExitCode(Program.EXIT_CODE_SUCCESS, true));
            Assert.AreEqual(Program.EXIT_CODE_SUCCESS, Program.ReconcileExitCode(Program.EXIT_CODE_SUCCESS, false));
            Assert.AreEqual(Program.EXIT_CODE_FAILURE_TO_START, Program.ReconcileExitCode(Program.EXIT_CODE_FAILURE_TO_START, true));
        }

        /// <summary>
        /// A task whose stamped outputs are current is skipped, as Osprey's are: a second
        /// prediction of the same library says so and leaves the .blib untouched, and a changed
        /// setting predicts again.
        /// </summary>
        [TestMethod]
        public void TestResumeSkipsCurrentOutputs()
        {
            string proteins = WriteText(@"proteins.fasta", PROTEINS);
            string models = WriteRandomModels();
            string libraryFolder = Path.Combine(_folder, @"library");
            var predict = new[] { @"--db", proteins, @"--o", libraryFolder, @"--model-dir", models, @"--lf-type", @"blib",
                @"--rt-max", @"30", @"--lf-min-n-frag", @"1", @"--device", @"cpu" };
            var (code, output, error) = Run(predict);
            Assert.AreEqual(0, code, error);
            string blib = Path.Combine(libraryFolder, BlibLibraryWriter.FILE_NAME);
            var stamp = CarafeStamps.Read(blib);
            Assert.IsNotNull(stamp, @"the predicted library carries no stamp");
            Assert.AreEqual(PredictLibraryTask.TASK_NAME, stamp.Task);
            Assert.AreEqual(CarafeContext.BuildVersion, stamp.Version);
            string skipped = string.Format(MessageResources.PipelineDriver_OnSkipped__0___skipping__its_outputs_are_current_, PredictLibraryTask.TASK_NAME);
            Assert.IsFalse(output.Contains(skipped), output);
            var written = File.GetLastWriteTimeUtc(blib);

            // The same command again: skipped, the library untouched.
            (code, output, error) = Run(predict);
            Assert.AreEqual(0, code, error);
            StringAssert.Contains(output, skipped);
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(blib));

            // A setting the library depends on changed: predicted again.
            (code, output, error) = Run(predict.Concat(new[] { @"--nce", @"28" }).ToArray());
            Assert.AreEqual(0, code, error);
            Assert.IsFalse(output.Contains(skipped), output);
            Assert.AreNotEqual(stamp.Key, CarafeStamps.Read(blib).Key);

            // A TSV-only prediction declares no output, so it always runs.
            string tsvFolder = Path.Combine(_folder, @"tsv");
            var predictTsv = new[] { @"--db", proteins, @"--o", tsvFolder, @"--model-dir", models, @"--lf-type", @"DIA-NN",
                @"--rt-max", @"30", @"--lf-min-n-frag", @"1", @"--device", @"cpu" };
            (code, _, error) = Run(predictTsv);
            Assert.AreEqual(0, code, error);
            (code, output, error) = Run(predictTsv);
            Assert.AreEqual(0, code, error);
            Assert.IsFalse(output.Contains(string.Format(MessageResources.PipelineDriver_OnSkipped__0___skipping__its_outputs_are_current_, PredictLibraryTask.TASK_NAME)), output);
        }

        [TestMethod]
        public void TestFailedBuildLeavesNoOutput()
        {
            string proteins = WriteText(@"proteins.fasta", PROTEINS);
            string built = Path.Combine(_folder, @"built.fasta");
            // A manifest path that is a folder, or that is the FASTA's own file, is refused before
            // anything is written.
            string manifest = Path.Combine(_folder, @"manifest.tsv");
            Directory.CreateDirectory(manifest);
            foreach (string badManifest in new[] { manifest, built })
            {
                var (badCode, _, badError) = Run(@"--build-entrapment-fasta", built, @"--db", proteins, @"--manifest", badManifest);
                Assert.AreEqual(1, badCode, badError);
                Assert.IsFalse(File.Exists(built), @"The failed build left " + built);
                AssertNoTempFiles();
            }

            string goodManifest = Path.Combine(_folder, @"good-manifest.tsv");
            var (code, _, error) = Run(@"--build-entrapment-fasta", built, @"--db", proteins, @"--manifest", goodManifest);
            Assert.AreEqual(0, code, error);
            if (OperatingSystem.IsWindows())
            {
                // A rebuild that cannot replace the earlier FASTA (read-only here; open in another
                // program is the same) leaves the earlier FASTA and manifest both as they were, not a
                // new manifest beside the old FASTA.
                byte[] fastaBefore = File.ReadAllBytes(built), manifestBefore = File.ReadAllBytes(goodManifest);
                File.SetAttributes(built, FileAttributes.ReadOnly);
                try
                {
                    (code, _, error) = Run(@"--build-entrapment-fasta", built, @"--db", proteins, @"--manifest", goodManifest, @"--no-similarity-gate", @"--miss-c", @"2");
                    Assert.AreEqual(1, code, error);
                }
                finally
                {
                    File.SetAttributes(built, FileAttributes.Normal);
                }
                CollectionAssert.AreEqual(fastaBefore, File.ReadAllBytes(built));
                CollectionAssert.AreEqual(manifestBefore, File.ReadAllBytes(goodManifest));
                AssertNoTempFiles();
            }

            // A reconciliation whose output cannot take its name leaves no partial manifest either.
            string library = WriteText(@"library.tsv", PairingManifestReconciler.LIBRARY_SEQUENCE_COLUMN + "\nPEPTIDEK\nSAMPLER\n");
            string reconciled = Path.Combine(_folder, @"reconciled-folder.tsv");
            Directory.CreateDirectory(reconciled);
            (code, _, error) = Run(@"--reconcile-manifest", reconciled, @"--manifest", goodManifest, @"--predicted-library", library);
            Assert.AreEqual(1, code, error);
            AssertNoTempFiles();
        }

        /// <summary>No temporary file of a failed or finished write is left in the test folder.</summary>
        private void AssertNoTempFiles()
        {
            Assert.AreEqual(0, Directory.GetFiles(_folder, SimpleFileSaver.TEMP_PREFIX + @"*").Length);
        }

        /// <summary>
        /// Every precursor of <paramref name="blib"/> that the manifest lists once, with a partner
        /// listed once, has that partner written at its charge.
        /// </summary>
        private static void AssertPairsWhole(string blib, string manifest)
        {
            var places = DecoyPairPlanner.ReadManifest(manifest)
                .GroupBy(e => PairingManifestReconciler.Normalize(e.Sequence)).ToDictionary(g => g.Key, g => g.ToList());
            var groups = DecoyPairPlanner.ReadManifest(manifest).GroupBy(e => e.PairIndex)
                .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.PeptideType.Trim().ToLowerInvariant(), e => PairingManifestReconciler.Normalize(e.Sequence)));
            var partners = new Dictionary<string, string> { { @"target", @"decoy" }, { @"decoy", @"target" }, { @"p_target", @"p_decoy" }, { @"p_decoy", @"p_target" } };
            var written = new HashSet<(string, long)>();
            using (var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = blib, ReadOnly = true }.ToString()))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"SELECT peptideSeq, precursorCharge FROM RefSpectra", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        written.Add((PairingManifestReconciler.Normalize(reader.GetString(0)), reader.GetInt64(1)));
                }
            }
            int checkedMembers = 0;
            foreach (var (sequence, charge) in written)
            {
                if (!places.TryGetValue(sequence, out var entries) || entries.Count != 1)
                    continue;
                string type = entries[0].PeptideType.Trim().ToLowerInvariant();
                if (!groups[entries[0].PairIndex].TryGetValue(partners[type], out string partner) || places[partner].Count != 1)
                    continue;
                checkedMembers++;
                Assert.IsTrue(written.Contains((partner, charge)), @"{0} {1}+ ({2}) is written without its partner {3}", sequence, charge, type, partner);
            }
            Assert.IsTrue(checkedMembers > 0);
        }

        private static void AssertDecoyPairs(string blib)
        {
            using (var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = blib, ReadOnly = true }.ToString()))
            {
                connection.Open();
                var rows = new List<(long Pairing, long Entrapment, long Decoy, string Method, string Sequence, long Charge, long Id, long? Target)>();
                using (var command = new SQLiteCommand(@"SELECT d.PairingID, d.IsEntrapment, d.IsDecoy, d.Method, r.peptideSeq, r.precursorCharge, d.RefSpectraID, d.TargetRefSpectraID " +
                                                       @"FROM DecoyPairs d JOIN RefSpectra r ON r.id = d.RefSpectraID ORDER BY d.PairingID, d.IsEntrapment, d.IsDecoy", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add((reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.IsDBNull(3) ? null : reader.GetString(3),
                            reader.GetString(4), reader.GetInt64(5), reader.GetInt64(6), reader.IsDBNull(7) ? null : reader.GetInt64(7)));
                    }
                }
                Assert.IsTrue(rows.Count > 0);
                // Every decoy names a target of its pairing and side at its charge, every target is
                // named by one decoy, and a target row names nothing.
                var byId = rows.ToDictionary(r => r.Id);
                var named = new HashSet<long>();
                foreach (var decoy in rows.Where(r => r.Decoy == 1))
                {
                    Assert.IsNotNull(decoy.Target, decoy.Sequence);
                    var target = byId[decoy.Target.Value];
                    Assert.AreEqual(0L, target.Decoy);
                    Assert.AreEqual(target.Pairing, decoy.Pairing);
                    Assert.AreEqual(target.Entrapment, decoy.Entrapment);
                    Assert.AreEqual(target.Charge, decoy.Charge);
                    Assert.IsNull(target.Method);
                    Assert.IsTrue(named.Add(target.Id), target.Sequence);
                    if (decoy.Method == @"reverse")
                        Assert.AreEqual(EntrapmentSequences.ReversePreservingCterm(target.Sequence), decoy.Sequence);
                }
                Assert.AreEqual(rows.Count(r => r.Decoy == 0), named.Count);
                Assert.IsTrue(rows.Where(r => r.Decoy == 0).All(r => r.Target == null));
            }
        }

        private static void AssertHelp(params string[] args)
        {
            var (code, output, error) = Run(args);
            Assert.AreEqual(0, code);
            Assert.AreEqual(CarafeCommandLine.BuildUsage(null), output);
            Assert.AreEqual(string.Empty, error);
        }

        private static void AssertUsageError(params string[] args)
        {
            string commandLine = string.Join(@" ", args);
            // Checked before running it, so a command line that parses after all does no work here.
            try
            {
                CarafeCommandLine.Parse(args);
                Assert.Fail(commandLine);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
            {
                // The error the run below must report.
            }
            var (code, output, error) = Run(args);
            Assert.AreEqual(1, code, commandLine);
            Assert.AreEqual(string.Empty, output, commandLine);
            // The message comes first, on its own line, then where the options are.
            var errorLines = Lines(error);
            Assert.AreEqual(2, errorLines.Length, error);
            Assert.AreEqual(CarafeCommandLine.UsageHint, errorLines[1], commandLine);
        }

        /// <summary>A run that parses, fails during its work with exit 1 and one error line, and leaves nothing at <paramref name="mustNotExist"/>.</summary>
        private static void AssertRunError(string mustNotExist, params string[] args)
        {
            var (code, _, error) = Run(args);
            string commandLine = string.Join(@" ", args);
            Assert.AreEqual(1, code, commandLine);
            Assert.AreEqual(1, Lines(error).Length, error);
            if (!Directory.Exists(mustNotExist))
                Assert.IsFalse(File.Exists(mustNotExist), commandLine);
            else
                Assert.AreEqual(0, Directory.GetFileSystemEntries(mustNotExist).Length, commandLine);
        }

        private static (int Code, string Output, string Error) Run(params string[] args)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            int code = Program.Run(args, output, error);
            return (code, output.ToString(), error.ToString());
        }

        private static string[] Lines(string text)
        {
            return text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(l => l.Trim().Length > 0).ToArray();
        }

        /// <summary>A folder of random MS2 and RT models in Carafe's layout, for a prediction that needs no real model.</summary>
        private string WriteRandomModels()
        {
            string models = Path.Combine(_folder, @"models");
            Directory.CreateDirectory(models);
            manual_seed(3);
            using (var ms2 = new ModelMs2Bert())
                StateDict.WriteSafetensors(ms2, Path.Combine(models, ModelFiles.MS2_SAFETENSORS));
            using (var rt = new ModelRtLstmCnn())
                StateDict.WriteSafetensors(rt, Path.Combine(models, ModelFiles.RT_SAFETENSORS));
            File.WriteAllText(Path.Combine(models, ModelFiles.METRICS), "{\"ms2\":{\"use_finetuned_for_prediction\":true}}");
            return models;
        }

        private string WriteText(string name, string text)
        {
            string path = Path.Combine(_folder, name);
            File.WriteAllText(path, text);
            return path;
        }
    }
}
