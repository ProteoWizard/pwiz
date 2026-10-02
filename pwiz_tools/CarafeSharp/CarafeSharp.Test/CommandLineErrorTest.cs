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
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Models.Modules;
using pwiz.CarafeSharp.Proteome;
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
            AssertUsageError(@"-printPTM");
            AssertUsageError(@"-db");
            AssertUsageError(@"stray");
            AssertUsageError(@"-build_koina_library", @"x");
            AssertUsageError(@"-reconcile_manifest", @"o.tsv", @"-predicted_library", @"l.tsv");
            AssertUsageError(@"-i", @"x.training.parquet", @"-mode", @"phospho");
            AssertUsageError(@"-i", @"x.training.parquet", @"-cs");
            AssertUsageError(@"-i", @"x.training.parquet", @"-device", @"tpu");
            AssertUsageError(@"-db=");
            // Carafe predicts no ion mobility for -tf rt or ms2, and then fails reading it.
            AssertUsageError(@"-db", @"x.fasta", @"-model_dir", _folder, @"-tf", @"rt", @"-ccs");
            AssertUsageError(@"-i", @"x.training.parquet", @"-db", @"x.fasta", @"-tf", @"ms2", @"-ccs");
            AssertUsageError(@"-db", @"x.fasta", @"-user_var_mods", @"1");
            AssertUsageError(@"-db", @"x.fasta", @"-mode", @"phospho");
            AssertUsageError(@"-db", @"x.fasta", @"-lf_format", @"parquet");
            // mzSpecLib only with -fast: without it Carafe writes any -lf_type as a TSV.
            AssertUsageError(@"-db", @"x.fasta", @"-lf_type", @"mzSpecLib", @"-fast");
            AssertUsageError(@"-db", @"x.fasta", @"-model_dir", _folder, @"-tf", @"test");
            AssertUsageError(@"-db", @"x.fasta", @"-fixMod", @"abc");
            AssertUsageError(@"-db", @"x.fasta", @"-max_pep_charge", @"1");
            AssertUsageError(@"-db", @"x.fasta", @"-nce", @"30x");
            AssertUsageError(@"-build_entrapment_fasta", @"o.fasta", @"-db", @"i.fasta", @"-mz_filter", @"-user_var_mods", @"1");
            AssertUsageError(@"-build_entrapment_fasta", @"o.fasta", @"-db", @"i.fasta", @"-decoy_seed", @"1.5");

            // The options those modes do read.
            var library = CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-nce", @"30", @"-ms_instrument", @"QE", @"-rt_max", @"45",
                @"-pairing_manifest", @"m.tsv", @"-decoy_prefix", @"rev_" }).LibrarySettings;
            Assert.AreEqual(30.0, library.Nce);
            Assert.AreEqual(@"QE", library.Instrument);
            Assert.IsTrue(library.UserInstrument);
            Assert.AreEqual(45.0, library.RtMax);
            Assert.AreEqual(@"m.tsv", library.PairingManifest);
            Assert.AreEqual(@"rev_", library.DecoyPrefix);
            var training = CarafeCommandLine.Parse(new[] { @"-i", @"x.training.parquet", @"-nce", @"28", @"-ms_instrument", @"QE" }).TrainingSettings;
            Assert.AreEqual(28.0, training.Nce);
            Assert.AreEqual(@"QE", training.Instrument);
            // -ccs with training, as Carafe on Osprey's results: no CCS fine-tune, the library's 1/K0 from the CCS model.
            var withMobility = CarafeCommandLine.Parse(new[] { @"-i", @"x.training.parquet", @"-db", @"x.fasta", @"-ccs" });
            Assert.IsTrue(withMobility.TrainingSettings.Library.PredictIonMobility);
            Assert.IsTrue(withMobility.Warnings.Any(w => w.StartsWith(@"-ccs: the CCS model is not fine-tuned", StringComparison.Ordinal)));
            Assert.IsTrue(CarafeCommandLine.Parse(new[] { @"-i", @"x.training.parquet", @"-ccs" }).Warnings
                .Any(w => w.StartsWith(@"Ignored -ccs", StringComparison.Ordinal)));
            Assert.AreEqual(7L, CarafeCommandLine.Parse(new[] { @"-build_entrapment_fasta", @"o.fasta", @"-db", @"i.fasta", @"-decoy_seed", @"7" })
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
            string export = Path.Combine(_folder, @"run" + OspreyTrainingExport.FILE_SUFFIX);
            AssertRunError(output, @"-i", export, @"-o", output, @"-db", missingFasta);
            AssertRunError(output, @"-i", export, @"-o", output, @"-ms2_model", Path.Combine(_folder, @"missing.pt"));
            AssertRunError(output, @"-i", export, @"-o", output, @"-pretrained", missingZip);
            // Library prediction opens the models before the digest: the folder may be made, but it stays empty.
            AssertRunError(output, @"-db", proteins, @"-o", output, @"-pretrained", missingZip);
            Assert.AreEqual(0, Directory.GetFileSystemEntries(output).Length);
            // A stage-1 build of a FASTA that is not there.
            string built = Path.Combine(_folder, @"built.fasta");
            AssertRunError(built, @"-build_entrapment_fasta", built, @"-db", missingFasta);

            // A failure with a cause reports both: a .blib library that is not a database.
            string manifest = WriteText(@"manifest.tsv", EntrapmentFastaBuilder.MANIFEST_HEADER + "\nPEPTIDEK\tNo\tP1\ttarget\t0\n");
            string notBlib = WriteText(@"library.blib", @"not a database");
            string reconciled = Path.Combine(_folder, @"reconciled.tsv");
            var (code, _, error) = Run(@"-reconcile_manifest", reconciled, @"-manifest", manifest, @"-predicted_library", notBlib);
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
            var (code, output, error) = Run(@"-build_entrapment_fasta", built, @"-db", proteins, @"-manifest", manifest, @"-no_similarity_gate");
            Assert.AreEqual(0, code, error);
            Assert.IsTrue(File.Exists(built) && File.Exists(manifest));
            Assert.AreEqual(1, Lines(error).Length, error);
            Assert.AreNotEqual(0, output.Length);
            string library = WriteText(@"library.tsv", PairingManifestReconciler.LIBRARY_SEQUENCE_COLUMN + "\nPEPTIDEK\nSAMPLER\n");
            string reconciled = Path.Combine(_folder, @"reconciled.tsv");
            (code, _, error) = Run(@"-reconcile_manifest", reconciled, @"-manifest", manifest, @"-predicted_library", library);
            Assert.AreEqual(0, code, error);
            Assert.IsTrue(File.Exists(reconciled));

            // Library prediction from a folder of random models, as a .blib and a TSV. When the
            // .blib cannot take its final name, the run fails and leaves neither library nor
            // either partial file, only what was there before.
            string models = Path.Combine(_folder, @"models");
            Directory.CreateDirectory(models);
            manual_seed(3);
            using (var ms2 = new ModelMs2Bert())
                StateDict.WriteSafetensors(ms2, Path.Combine(models, ModelFiles.MS2_SAFETENSORS));
            using (var rt = new ModelRtLstmCnn())
                StateDict.WriteSafetensors(rt, Path.Combine(models, ModelFiles.RT_SAFETENSORS));
            File.WriteAllText(Path.Combine(models, ModelFiles.METRICS), "{\"ms2\":{\"use_finetuned_for_prediction\":true}}");
            string libraryFolder = Path.Combine(_folder, @"library");
            string blocker = Path.Combine(libraryFolder, BlibLibraryWriter.FILE_NAME);
            Directory.CreateDirectory(blocker);
            var predict = new[] { @"-db", proteins, @"-o", libraryFolder, @"-model_dir", models, @"-lf_type", @"blib,DIA-NN",
                @"-rt_max", @"30", @"-lf_min_n_frag", @"1", @"-device", @"cpu" };
            (code, _, error) = Run(predict);
            Assert.AreEqual(1, code, error);
            CollectionAssert.AreEqual(new[] { blocker }, Directory.GetFileSystemEntries(libraryFolder));
            // Without the obstacle the same command writes both, and nothing else.
            Directory.Delete(blocker);
            (code, _, error) = Run(predict);
            Assert.AreEqual(0, code, error);
            CollectionAssert.AreEquivalent(new[] { blocker, Path.Combine(libraryFolder, CarafeLibraryTsvWriter.FILE_NAME) },
                Directory.GetFileSystemEntries(libraryFolder));

            // The library of stage 1's peptide FASTA with its pairing manifest carries Carafe's
            // DecoyPairs table (and, without -rt_max, iRT retention times): every pair one target
            // and one decoy of the same charge, the decoy the reverse of the target when so named.
            string pairedFolder = Path.Combine(_folder, @"paired");
            (code, _, error) = Run(@"-db", built, @"-o", pairedFolder, @"-model_dir", models, @"-lf_type", @"blib",
                @"-lf_min_n_frag", @"1", @"-device", @"cpu", @"-pairing_manifest", manifest);
            Assert.AreEqual(0, code, error);
            AssertDecoyPairs(Path.Combine(pairedFolder, BlibLibraryWriter.FILE_NAME));
            // A minimum of fragments that only one member of some pairs reaches: each pair is still
            // written whole, the member whose partner fell short left out with it.
            string wholeFolder = Path.Combine(_folder, @"whole");
            (code, output, error) = Run(@"-db", built, @"-o", wholeFolder, @"-model_dir", models, @"-lf_type", @"blib",
                @"-lf_min_n_frag", @"16", @"-device", @"cpu", @"-pairing_manifest", manifest);
            Assert.AreEqual(0, code, error);
            string droppedLine = Lines(output).Single(l => l.StartsWith(@"Pairs: dropped ", StringComparison.Ordinal));
            Assert.IsTrue(int.Parse(droppedLine.Split(' ')[2]) > 0, droppedLine);
            AssertDecoyPairs(Path.Combine(wholeFolder, BlibLibraryWriter.FILE_NAME));
            AssertPairsWhole(Path.Combine(wholeFolder, BlibLibraryWriter.FILE_NAME), manifest);
        }

        [TestMethod]
        public void TestFailedBuildLeavesNoOutput()
        {
            string proteins = WriteText(@"proteins.fasta", PROTEINS);
            string built = Path.Combine(_folder, @"built.fasta");
            // A manifest path that is a folder, or that shares the FASTA's file or temporary file, is
            // refused before anything is written.
            string manifest = Path.Combine(_folder, @"manifest.tsv");
            Directory.CreateDirectory(manifest);
            foreach (string badManifest in new[] { manifest, built, built + PartialFile.SUFFIX })
            {
                var (badCode, _, badError) = Run(@"-build_entrapment_fasta", built, @"-db", proteins, @"-manifest", badManifest);
                Assert.AreEqual(1, badCode, badError);
                Assert.IsFalse(File.Exists(built), @"The failed build left " + built);
                Assert.IsFalse(File.Exists(built + PartialFile.SUFFIX));
                Assert.IsFalse(File.Exists(badManifest + PartialFile.SUFFIX));
            }

            string goodManifest = Path.Combine(_folder, @"good-manifest.tsv");
            var (code, _, error) = Run(@"-build_entrapment_fasta", built, @"-db", proteins, @"-manifest", goodManifest);
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
                    (code, _, error) = Run(@"-build_entrapment_fasta", built, @"-db", proteins, @"-manifest", goodManifest, @"-no_similarity_gate", @"-miss_c", @"2");
                    Assert.AreEqual(1, code, error);
                }
                finally
                {
                    File.SetAttributes(built, FileAttributes.Normal);
                }
                CollectionAssert.AreEqual(fastaBefore, File.ReadAllBytes(built));
                CollectionAssert.AreEqual(manifestBefore, File.ReadAllBytes(goodManifest));
                Assert.IsFalse(File.Exists(built + PartialFile.SUFFIX));
                Assert.IsFalse(File.Exists(goodManifest + PartialFile.SUFFIX));
            }

            // A reconciliation whose output cannot take its name leaves no partial manifest either.
            string library = WriteText(@"library.tsv", PairingManifestReconciler.LIBRARY_SEQUENCE_COLUMN + "\nPEPTIDEK\nSAMPLER\n");
            string reconciled = Path.Combine(_folder, @"reconciled-folder.tsv");
            Directory.CreateDirectory(reconciled);
            (code, _, error) = Run(@"-reconcile_manifest", reconciled, @"-manifest", goodManifest, @"-predicted_library", library);
            Assert.AreEqual(1, code, error);
            Assert.IsFalse(File.Exists(reconciled + PartialFile.SUFFIX));
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
                var rows = new List<(long Pair, long Decoy, string Method, string Sequence, long Charge)>();
                using (var command = new SQLiteCommand(@"SELECT d.PairID, d.IsDecoy, d.Method, r.peptideSeq, r.precursorCharge " +
                                                       @"FROM DecoyPairs d JOIN RefSpectra r ON r.id = d.RefSpectraID ORDER BY d.PairID, d.IsDecoy", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add((reader.GetInt64(0), reader.GetInt64(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                            reader.GetString(3), reader.GetInt64(4)));
                    }
                }
                Assert.IsTrue(rows.Count > 0);
                foreach (var pair in rows.GroupBy(r => r.Pair))
                {
                    var members = pair.ToArray();
                    Assert.AreEqual(2, members.Length);
                    Assert.AreEqual(0L, members[0].Decoy);
                    Assert.AreEqual(1L, members[1].Decoy);
                    Assert.AreEqual(members[0].Charge, members[1].Charge);
                    Assert.IsNull(members[0].Method);
                    if (members[1].Method == @"reverse")
                        Assert.AreEqual(EntrapmentSequences.ReversePreservingCterm(members[0].Sequence), members[1].Sequence);
                }
            }
        }

        private static void AssertHelp(params string[] args)
        {
            var (code, output, error) = Run(args);
            Assert.AreEqual(0, code);
            Assert.AreEqual(CarafeCommandLine.Usage + Environment.NewLine, output);
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
            Assert.IsTrue(error.EndsWith(CarafeCommandLine.Usage + Environment.NewLine, StringComparison.Ordinal), commandLine);
            // The message comes first, on its own line.
            Assert.IsTrue(Lines(error).Length > Lines(CarafeCommandLine.Usage).Length, commandLine);
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

        private string WriteText(string name, string text)
        {
            string path = Path.Combine(_folder, name);
            File.WriteAllText(path, text);
            return path;
        }
    }
}
