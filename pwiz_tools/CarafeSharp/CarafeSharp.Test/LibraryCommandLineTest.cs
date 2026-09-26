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
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Models.Modules;
using pwiz.CarafeSharp.Proteome;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Carafe's library-prediction command line as CarafeCommandLine reads it, the output
    /// formats an <c>-lf_type</c> selects, a model folder's meta.json and metrics, and the
    /// DecoyPairs plan.
    /// </summary>
    [TestClass]
    public class LibraryCommandLineTest
    {
        /// <summary>The library options of the Carafe GUI run that built the Stellar generic library.</summary>
        private const string GUI_COMMAND_LINE =
            @"-db osprey_train_db_peptides.fasta -o out -fdr 0.01 -ptm_site_prob 0.75 -ptm_site_qvalue 0.01 -itol 0.4 -itolu Da " +
            @"-rf -rf_rt_win auto -cor 0.8 -min_mz 200 -n_ion_min 2 -c_ion_min 2 -mode general -device gpu -enzyme NoCut " +
            @"-miss_c 1 -fixMod 1 -varMod 0 -maxVar 1 -clip_n_m -minLength 7 -maxLength 35 -min_pep_mz 400 -max_pep_mz 900 " +
            @"-min_pep_charge 2 -max_pep_charge 3 -lf_frag_mz_min 200 -lf_frag_mz_max 1960 -lf_top_n_frag 20 -lf_min_n_frag 2 " +
            @"-lf_frag_n_min 2 -lf_type DIA-NN -se Osprey -decoy_prefix decoy_ -nm -nf 4 -min_n 4 -valid -na 0 -ez -fast";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestLibraryCommandLine()
        {
            var commandLine = CarafeCommandLine.Parse(GUI_COMMAND_LINE.Split(' '));
            Assert.AreEqual(CarafeCommandMode.predict_library, commandLine.Mode);
            var settings = commandLine.LibrarySettings;
            Assert.AreEqual(@"osprey_train_db_peptides.fasta", settings.Database);
            Assert.AreEqual(@"out", settings.OutputDirectory);
            Assert.AreEqual(EnzymeTable.NO_CUT_ENZYME_NAME, settings.Digest.Enzyme.Name);
            Assert.IsTrue(settings.Digest.ClipNTermMethionine);
            Assert.AreEqual(0, settings.Modifications.GetVariableModifications().Count);
            CollectionAssert.AreEqual(new[] { 2, 3 }, settings.Charges);
            Assert.AreEqual(400.0, settings.MinPrecursorMz);
            Assert.AreEqual(900.0, settings.MaxPrecursorMz);
            Assert.AreEqual(1960.0, settings.MaxFragmentMz);
            Assert.AreEqual(@"decoy_", settings.DecoyPrefix);
            Assert.IsTrue(settings.Fast);
            Assert.AreEqual(LibrarySettings.DEFAULT_NCE, settings.Nce);
            Assert.IsNull(settings.ModelDirectory);
            var outputs = LibraryOutputs.FromFormat(settings.LibraryFormat, settings.Fast);
            Assert.IsTrue(outputs.WritesTsv && !outputs.WritesBlib);
            Assert.AreEqual(ModifiedPeptideStyle.dia_nn, outputs.TsvStyle);

            // Carafe's code defaults, not its help text's.
            settings = CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta" }).LibrarySettings;
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, settings.Charges);
            Assert.AreEqual(300.0, settings.MinPrecursorMz);
            Assert.AreEqual(2000.0, settings.MaxPrecursorMz);
            Assert.AreEqual(200.0, settings.MinFragmentMz);
            Assert.AreEqual(1800.0, settings.MaxFragmentMz);
            Assert.AreEqual(2, settings.Digest.MaxMissedCleavages);
            Assert.IsFalse(settings.Digest.ClipNTermMethionine);
            Assert.AreEqual(@"rev_", settings.DecoyPrefix);
            Assert.AreEqual(@"./", settings.OutputDirectory);
            Assert.AreEqual(@"gpu", settings.Device);

            // -model_dir reads the folder's meta.json and -tf; -tf is ignored without it.
            settings = CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-model_dir", @"m", @"-tf", @"rt", @"-I2L", @"-pretrained", @"p.zip" })
                .LibrarySettings;
            Assert.AreEqual(@"m", settings.ModelDirectory);
            Assert.IsTrue(settings.ApplyModelDirectoryMeta);
            Assert.AreEqual(@"rt", settings.TrainingType);
            Assert.IsTrue(settings.Digest.ConvertIToL);
            Assert.AreEqual(@"p.zip", settings.PretrainedModels);
            Assert.AreEqual(@"all", CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-tf", @"rt" }).LibrarySettings.TrainingType);

            // -ms is training, which reads Osprey's results (-i) rather than the raw data.
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-ms", @"a.mzML" }));
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-lf_type", @"mzSpecLib", @"-fast" }));
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.tsv" }));
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-mode", @"phosphorylation" }));
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-min_pep_charge", @"3", @"-max_pep_charge", @"2" }));
        }

        [TestMethod]
        public void TestLibraryOutputs()
        {
            AssertOutputs(@"DIA-NN", true, true, false, ModifiedPeptideStyle.dia_nn);
            AssertOutputs(@"Skyline", true, false, true, ModifiedPeptideStyle.dia_nn);
            AssertOutputs(@"blib", false, false, true, ModifiedPeptideStyle.dia_nn);
            AssertOutputs(@"blib,DIA-NN", false, true, true, ModifiedPeptideStyle.dia_nn);
            // A comma list writes a TSV per non-Skyline element; the last one is the file left.
            AssertOutputs(@"Skyline,DIA-NN,EncyclopeDIA", true, true, true, ModifiedPeptideStyle.encyclopedia);
            // Carafe does not trim the elements, so " DIA-NN" is the generic notation.
            AssertOutputs(@"Skyline, DIA-NN", true, true, true, ModifiedPeptideStyle.generic);
            // Without -fast Carafe writes a TSV whatever -lf_type says.
            var outputs = LibraryOutputs.FromFormat(@"Skyline", false);
            Assert.IsTrue(outputs.WritesTsv && !outputs.WritesBlib);
            Assert.AreEqual(ModifiedPeptideStyle.generic, outputs.TsvStyle);
            Assert.IsNotNull(outputs.Warning);
        }

        [TestMethod]
        public void TestModelDirectory()
        {
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Model_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                // Carafe writes meta.json keys as raw Windows paths.
                File.WriteAllText(Path.Combine(folder, CarafeModelDirectory.META_FILE),
                    "{\n\"D:\\data\\run.mzML\":{\"lf_frag_mz_max\":1800.0,\"lf_frag_mz_min\":200.0,\"ms_instrument\":\"\",\"nce\":30.0," +
                    "\"precursor_ion_mz_max\":900.658386230469,\"precursor_ion_mz_min\":400.432800292969,\"rt_max\":24.104256448433002,\"rt_min\":0.0}\n}");
                File.WriteAllText(Path.Combine(folder, CarafeModelDirectory.MS2_MODEL_FILE), string.Empty);
                var directory = CarafeModelDirectory.Open(folder);
                Assert.IsFalse(directory.UseFineTunedMs2, @"no metrics: pretrained");
                Assert.IsNull(directory.GetRtModelPath(@"all"));

                File.WriteAllText(Path.Combine(folder, CarafeModelDirectory.METRICS_FILE), "{\"ms2\":{\"use_finetuned_for_prediction\":true}}");
                File.WriteAllText(Path.Combine(folder, CarafeModelDirectory.RT_MODEL_FILE), string.Empty);
                directory = CarafeModelDirectory.Open(folder);
                Assert.IsNotNull(directory.GetMs2ModelPath(@"all"));
                Assert.IsNull(directory.GetMs2ModelPath(@"rt"));
                Assert.IsNotNull(directory.GetRtModelPath(@"rt"));
                Assert.IsNull(directory.GetRtModelPath(@"ms2"));
                Assert.AreEqual(1, directory.Runs.Count);

                // -model_dir: meta.json overrides the command line, with Carafe's -0.5 at both ends.
                var settings = new LibrarySettings { MaxFragmentMz = 1960 };
                directory.ApplyModelDirectoryOverrides(settings);
                Assert.AreEqual(1800.0, settings.MaxFragmentMz);
                Assert.AreEqual(30.0, settings.Nce);
                Assert.AreEqual(string.Empty, settings.Instrument);
                Assert.AreEqual(24.104256448433002, settings.RtMax);
                Assert.AreEqual(400.432800292969 - 0.5, settings.MinPrecursorMz);
                Assert.AreEqual(900.658386230469 - 0.5, settings.MaxPrecursorMz);

                // After training: +-0.5 around the isolation range, the command line's fragment range.
                settings = new LibrarySettings { MaxFragmentMz = 1960 };
                directory.ApplyTrainingRunOverrides(settings);
                Assert.AreEqual(1960.0, settings.MaxFragmentMz);
                Assert.AreEqual(LibrarySettings.DEFAULT_INSTRUMENT, settings.Instrument);
                Assert.AreEqual(399.932800292969, settings.MinPrecursorMz);
                Assert.AreEqual(901.158386230469, settings.MaxPrecursorMz);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// Library prediction end to end from a model folder of randomly initialized models, so
        /// it needs no pretrained weights.
        /// </summary>
        [TestMethod]
        public void TestLibraryGenerator()
        {
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Generator_" + Guid.NewGuid().ToString(@"N"));
            string models = Path.Combine(folder, @"models");
            Directory.CreateDirectory(models);
            try
            {
                WriteRandomModels(models);
                string fasta = Path.Combine(folder, @"proteins.fasta");
                File.WriteAllText(fasta, ">sp|P1|A\nMPEPTIDEKSAMPLERLVNELTEFAK\n");
                var settings = new LibrarySettings
                {
                    Database = fasta,
                    OutputDirectory = Path.Combine(folder, @"out"),
                    ModelDirectory = models,
                    LibraryFormat = LibraryOutputs.BLIB_FORMAT,
                    Device = TorchDevice.CPU,
                    RtMax = 30,
                    MinFragments = 1,
                    // A pairing manifest that cannot be read: Carafe logs the failure and keeps the library.
                    PairingManifest = models,
                };
                var log = new StringWriter();
                var generator = new LibraryGenerator(settings, log);
                generator.Run();
                Assert.IsTrue(generator.SpectrumCount > 0, log.ToString());
                CollectionAssert.AreEqual(new[] { generator.BlibPath }, Directory.GetFiles(settings.OutputDirectory));
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void TestDecoyPairPlanner()
        {
            Assert.AreEqual(@"Carbamidomethyl@C;Oxidation@M", DecoyPairPlanner.ModKey(new[] { @"Oxidation@M", @" Carbamidomethyl@C", string.Empty }));
            var precursors = new List<DecoyPairPlanner.Precursor>
            {
                new DecoyPairPlanner.Precursor(1, @"PEPTIDEK", 2, string.Empty),
                new DecoyPairPlanner.Precursor(2, @"EDITPEPK", 2, string.Empty),
                new DecoyPairPlanner.Precursor(3, @"PEPTIDEK", 3, string.Empty),
                new DecoyPairPlanner.Precursor(4, @"DIETPEPK", 3, string.Empty),
                new DecoyPairPlanner.Precursor(5, @"EDITPEPK", 3, string.Empty),
                // Differs from PEPTIDEK only by I/L: Carafe's normalization merges the two.
                new DecoyPairPlanner.Precursor(6, @"PEPTLDEK", 2, string.Empty),
                new DecoyPairPlanner.Precursor(7, @"EDLTPEPK", 2, string.Empty),
            };
            var manifest = new List<DecoyPairPlanner.ManifestEntry>
            {
                new DecoyPairPlanner.ManifestEntry(@"PEPTLDEK", false, @"target", 2),
                new DecoyPairPlanner.ManifestEntry(@"EDLTPEPK", true, @"decoy", 2),
                new DecoyPairPlanner.ManifestEntry(@"PEPTIDEK", false, @"Target ", 1),
                new DecoyPairPlanner.ManifestEntry(@"EDITPEPK", true, @"decoy", 1),
                new DecoyPairPlanner.ManifestEntry(@"MISSINGK", false, @"target", 0),
            };
            var rows = DecoyPairPlanner.Plan(precursors, manifest, out int skipped);
            // Group 1 takes the precursors of both I/L variants, zipped per charge bucket in id
            // order; group 2 then finds all of its precursors paired, where Carafe fails.
            Assert.AreEqual(3, skipped);
            CollectionAssert.AreEqual(new[] { 1, 2, 6, 7, 3, 5 }, rows.Select(r => r.RefSpectraId).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2, 3, 3 }, rows.Select(r => r.PairId).ToArray());
            CollectionAssert.AreEqual(new[] { false, true, false, true, false, true }, rows.Select(r => r.IsDecoy).ToArray());
            CollectionAssert.AreEqual(new[] { null, @"reverse", null, @"reverse", null, @"reverse" }, rows.Select(r => r.Method).ToArray());
            Assert.IsFalse(rows.Any(r => r.IsEntrapment));
        }

        /// <summary>Randomly initialized MS2 and RT models, with metrics that say to use the MS2 one.</summary>
        private static void WriteRandomModels(string folder)
        {
            manual_seed(1);
            using (var ms2 = new ModelMs2Bert())
                StateDict.WriteSafetensors(ms2, Path.Combine(folder, ModelFiles.MS2_SAFETENSORS));
            using (var rt = new ModelRtLstmCnn())
                StateDict.WriteSafetensors(rt, Path.Combine(folder, ModelFiles.RT_SAFETENSORS));
            File.WriteAllText(Path.Combine(folder, ModelFiles.METRICS), "{\"ms2\":{\"use_finetuned_for_prediction\":true}}");
        }

        private static void AssertOutputs(string format, bool fast, bool tsv, bool blib, ModifiedPeptideStyle style)
        {
            var outputs = LibraryOutputs.FromFormat(format, fast);
            Assert.AreEqual(tsv, outputs.WritesTsv, format);
            Assert.AreEqual(blib, outputs.WritesBlib, format);
            if (tsv)
                Assert.AreEqual(style, outputs.TsvStyle, format);
        }
    }
}
