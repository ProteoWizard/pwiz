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
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
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
    /// Carafe's library-prediction command line as CarafeCommandLine reads it, the output
    /// formats an <c>-lf_type</c> selects, a model folder's meta.json and metrics, library
    /// prediction itself, and the DecoyPairs plan.
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

        /// <summary>How long a test hook waits for another thread before the test fails.</summary>
        private static readonly TimeSpan HOOK_TIMEOUT = TimeSpan.FromMinutes(1);

        // Set when a run did not end: its thread still holds files in the test folder.
        private bool _runHung;

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
            Assert.IsFalse(settings.PredictIonMobility);
            Assert.IsTrue(CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-ccs" }).LibrarySettings.PredictIonMobility);

            // -model_dir reads the folder's meta.json and -tf; -tf is ignored without it.
            settings = CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-model_dir", @"m", @"-tf", @"rt", @"-I2L", @"-pretrained", @"p.zip" })
                .LibrarySettings;
            Assert.AreEqual(@"m", settings.ModelDirectory);
            Assert.IsTrue(settings.ApplyModelDirectoryMeta);
            Assert.AreEqual(@"rt", settings.TrainingType);
            Assert.IsTrue(settings.Digest.ConvertIToL);
            Assert.AreEqual(@"p.zip", settings.PretrainedModels);
            Assert.AreEqual(@"all", CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-tf", @"rt" }).LibrarySettings.TrainingType);

            // -model names a saved model, which takes no meta.json window; -nce and -rt_max given
            // are marked, so the saved model's own do not replace them.
            settings = CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-model", @"m.carafemodel", @"-tf", @"ms2", @"-nce", @"25" })
                .LibrarySettings;
            Assert.AreEqual(@"m.carafemodel", settings.ModelFile);
            Assert.IsFalse(settings.ApplyModelDirectoryMeta);
            Assert.AreEqual(@"ms2", settings.TrainingType);
            Assert.IsTrue(settings.UserNce);
            Assert.IsFalse(settings.UserRtMax);
            Assert.IsTrue(CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-model", @"m.carafemodel", @"-rt_max", @"30" }).LibrarySettings.UserRtMax);
            var infoCommand = CarafeCommandLine.Parse(new[] { @"-model_info", @"m.carafemodel" });
            Assert.AreEqual(CarafeCommandMode.model_info, infoCommand.Mode);
            Assert.AreEqual(@"m.carafemodel", infoCommand.ModelInfoPath);
            // -activation and -analyzer name known values, whatever their case, and are marked as given.
            settings = CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-activation", @"RECID", @"-analyzer", @"lit" }).LibrarySettings;
            Assert.AreEqual(AcquisitionVocabulary.RE_CID, settings.Activation);
            Assert.AreEqual(AcquisitionVocabulary.LIT, settings.Analyzer);
            Assert.IsTrue(settings.UserActivation && settings.UserAnalyzer);
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-activation", @"CID" }));
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-analyzer", @"FTICR" }));
            var training = CarafeCommandLine.Parse(new[] { @"-i", @"a.training.parquet", @"-activation", @"beam-cid", @"-analyzer", @"ToF" }).TrainingSettings;
            Assert.AreEqual(AcquisitionVocabulary.BEAM_CID, training.Activation);
            Assert.AreEqual(AcquisitionVocabulary.TOF, training.Analyzer);
            // -rt_model names an RT model, whatever its case: in training the one to fine-tune, and for the library
            // after training the one to predict with. Without it, none is named, so a saved model's own can apply,
            // and an explicit alphapeptdeep is not taken for the default.
            Assert.IsNull(CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta" }).LibrarySettings.RtModelType);
            Assert.IsNull(CarafeCommandLine.Parse(new[] { @"-i", @"a.training.parquet" }).TrainingSettings.RtModelType);
            Assert.AreEqual(RtModelType.alphapeptdeep,
                CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-rt_model", @"alphapeptdeep" }).LibrarySettings.RtModelType);
            Assert.AreEqual(RtModelType.chronologer,
                CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-rt_model", @"Chronologer" }).LibrarySettings.RtModelType);
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-rt_model", @"prosit" }));
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-rt_model", @"1" }));
            var fineTuned = CarafeCommandLine.Parse(new[] { @"-i", @"a.training.parquet", @"-db", @"x.fasta", @"-rt_model", @"chronologer" });
            Assert.AreEqual(RtModelType.chronologer, fineTuned.TrainingSettings.RtModelType);
            Assert.AreEqual(RtModelType.chronologer, fineTuned.TrainingSettings.Library.RtModelType);
            var ms2Only = CarafeCommandLine.Parse(new[] { @"-i", @"a.training.parquet", @"-db", @"x.fasta", @"-tf", @"ms2", @"-rt_model", @"chronologer" });
            Assert.AreEqual(RtModelType.chronologer, ms2Only.TrainingSettings.Library.RtModelType);
            // It is refused with -model_dir, with -ms2_model, and without -db or training.
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-model", @"m.carafemodel", @"-model_dir", @"d" }));
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-model", @"m.carafemodel", @"-ms2_model", @"s.safetensors" }));
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-model", @"m.carafemodel", @"-o", @"out" }));
            // With training, -model is the saved model to fine-tune further, with -tf all only;
            // the library after training predicts with the run's own models, not the saved one.
            var further = CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-model", @"m.carafemodel", @"-i", @"a.training.parquet" });
            Assert.AreEqual(CarafeCommandMode.train, further.Mode);
            Assert.AreEqual(@"m.carafemodel", further.TrainingSettings.BaseModel);
            Assert.IsNull(further.TrainingSettings.Library.ModelFile);
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-model", @"m.carafemodel", @"-i", @"a.training.parquet", @"-tf", @"rt" }));

            // -ms is training, which reads Osprey's results (-i) rather than the raw data.
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-ms", @"a.mzML" }));
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-lf_type", @"mzSpecLib", @"-fast" }));
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.tsv" }));
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-mode", @"phosphorylation" }));
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-min_pep_charge", @"3", @"-max_pep_charge", @"2" }));

            // The device and the modifications are checked with the other options, before any work.
            Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-device", @"tpu" }));
            var badIds = Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-varMod", @"2, 7" }));
            Assert.IsInstanceOfType(badIds.InnerException, typeof(FormatException));
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-fixMod", @"99" }));
            // TMT (11) has no alphabase name, so Carafe's library prediction cannot use it.
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-varMod", @"11" }));
            // Only the DIA-NN notation can write a protein N-term acetyl in a TSV; a .blib can too.
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-varMod", @"5", @"-lf_type", @"EncyclopeDIA" }));
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-fixMod", @"5", @"-lf_type", @"Skyline" }));
            CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-varMod", @"5", @"-lf_type", @"DIA-NN" });
            CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-varMod", @"5", @"-lf_type", @"Skyline", @"-fast" });
            // pyro-Glu (27, 28) is predicted into a .blib; no TSV notation can write it.
            CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-varMod", @"28", @"-lf_type", @"blib" });
            CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-fixMod", @"27", @"-lf_type", @"Skyline", @"-fast" });
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-varMod", @"28" }));
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-varMod", @"28", @"-lf_type", @"blib,DIA-NN" }));
            // Stage 1 reads ids 11 to 26 (no alphabase name) for its m/z filter, so its command line accepts them.
            Assert.AreEqual(CarafeCommandMode.build_entrapment_fasta, CarafeCommandLine.Parse(new[]
                { @"-build_entrapment_fasta", @"out.fasta", @"-db", @"x.fasta", @"-varMod", @"11", @"-mz_filter" }).Mode);
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

                // Both Carafe's checkpoint and CarafeSharp's safetensors: Carafe's for -model_dir,
                // the safetensors a training run just wrote for the library that follows it.
                string checkpoint = Path.Combine(folder, CarafeModelDirectory.MS2_MODEL_FILE);
                string safetensors = Path.Combine(folder, ModelFiles.MS2_SAFETENSORS);
                File.WriteAllText(safetensors, string.Empty);
                directory = CarafeModelDirectory.Open(folder);
                Assert.AreEqual(checkpoint, directory.Ms2ModelPath);
                Assert.AreEqual(string.Format(CarafeModelDirectory.BOTH_MODELS_WARNING_FORMAT, checkpoint, safetensors), directory.Warnings.Single());
                directory = CarafeModelDirectory.Open(folder, true);
                Assert.AreEqual(safetensors, directory.Ms2ModelPath);
                Assert.AreEqual(string.Format(CarafeModelDirectory.BOTH_MODELS_WARNING_FORMAT, safetensors, checkpoint), directory.Warnings.Single());

                // A field meta.json lacks takes Carafe's JMeta default.
                File.WriteAllText(Path.Combine(folder, CarafeModelDirectory.META_FILE), "{\"run.mzML\":{\"nce\":30.0}}");
                var run = CarafeModelDirectory.Open(folder).Runs.Single();
                Assert.AreEqual(30.0, run.Nce);
                Assert.AreEqual(@"-", run.MsFile);
                Assert.AreEqual(@"-", run.MsInstrument);
                Assert.AreEqual(200.0, run.LfFragMzMin);
                Assert.AreEqual(1800.0, run.LfFragMzMax);
                Assert.AreEqual(20, run.LfTopNFragmentIons);
                Assert.AreEqual(200.0, run.MinFragmentIonMz);
                Assert.AreEqual(2000.0, run.MaxFragmentIonMz);
                Assert.AreEqual(300.0, run.PrecursorMzMin);
                Assert.AreEqual(1800.0, run.PrecursorMzMax);
                Assert.AreEqual(0.0, run.RtMax);

                // A raw key's \n and \u are path characters, as Carafe's Python reads them, while a
                // value's escaped \\ still decodes. Runs follow the HashMap order of the literal keys.
                const string keyA = @"C:\new\users\a.mzML";
                const string keyB = @"C:\new\users\b.mzML";
                File.WriteAllText(Path.Combine(folder, CarafeModelDirectory.META_FILE),
                    @"{""" + keyA + @""":{""nce"":1.0,""ms_file"":""C:\\new\\users\\a.mzML""}," + "\n" +
                    @"""" + keyB + @""":{""nce"":2.0}}");
                var runs = CarafeModelDirectory.Open(folder).Runs;
                var expectedNce = JavaHashOrder.OrderStringKeys(new[] { keyA, keyB }).Select(k => k == keyA ? 1.0 : 2.0);
                CollectionAssert.AreEqual(expectedNce.ToList(), runs.Select(r => r.Nce).ToList());
                Assert.AreEqual(keyA, runs.Single(r => r.Nce == 1.0).MsFile);

                // use_finetuned_for_prediction is read as Python's bool(): anything but a zero,
                // an empty string, array or object, null or false is true. A metrics file that is
                // not JSON, or has no ms2 object, means the pretrained model.
                string metrics = Path.Combine(folder, CarafeModelDirectory.METRICS_FILE);
                foreach (var (value, expected) in new[]
                         {
                             (@"true", true), (@"false", false), (@"null", false), (@"1", true), (@"0", false), (@"0.0", false),
                             (@"""yes""", true), (@"""""", false), (@"[0]", true), (@"[]", false), (@"{""a"":0}", true), (@"{}", false),
                         })
                {
                    File.WriteAllText(metrics, @"{""ms2"":{""use_finetuned_for_prediction"":" + value + @"}}");
                    Assert.AreEqual(expected, CarafeModelDirectory.Open(folder).UseFineTunedMs2, value);
                }
                foreach (string unreadable in new[] { @"{not json", @"[]", @"{""ms2"":true}", @"{""rt"":{}}" })
                {
                    File.WriteAllText(metrics, unreadable);
                    Assert.IsFalse(CarafeModelDirectory.Open(folder).UseFineTunedMs2, unreadable);
                }

                // After training, the training run's instrument replaces the default, but not an -ms_instrument.
                File.WriteAllText(Path.Combine(folder, CarafeModelDirectory.META_FILE),
                    @"{""run.mzML"":{""nce"":27.0,""ms_instrument"":""Astral"",""rt_max"":40.0}}");
                directory = CarafeModelDirectory.Open(folder);
                settings = new LibrarySettings();
                directory.ApplyTrainingRunOverrides(settings);
                Assert.AreEqual(@"Astral", settings.Instrument);
                Assert.AreEqual(27.0, settings.Nce);
                Assert.AreEqual(40.0, settings.RtMax);
                settings = new LibrarySettings { Instrument = @"Lumos", UserInstrument = true };
                directory.ApplyTrainingRunOverrides(settings);
                Assert.AreEqual(@"Lumos", settings.Instrument);
                // With no meta.json there is no training run, and nothing is overridden.
                File.Delete(Path.Combine(folder, CarafeModelDirectory.META_FILE));
                settings = new LibrarySettings { Nce = 31 };
                CarafeModelDirectory.Open(folder).ApplyTrainingRunOverrides(settings);
                Assert.AreEqual(31.0, settings.Nce);
                Assert.AreEqual(LibrarySettings.DEFAULT_INSTRUMENT, settings.Instrument);

                Assert.ThrowsException<DirectoryNotFoundException>(() => CarafeModelDirectory.Open(Path.Combine(folder, @"missing")));
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
            Directory.CreateDirectory(folder);
            try
            {
                var settings = CreateGeneratorSettings(folder, LibraryOutputs.BLIB_FORMAT);
                string models = settings.ModelDirectory;
                // Checkpoints an earlier Carafe run left: the library after training uses the models it wrote.
                string checkpoint = Path.Combine(models, ModelFiles.MS2_CHECKPOINT);
                File.WriteAllText(checkpoint, @"not a checkpoint");
                File.WriteAllText(Path.Combine(models, ModelFiles.RT_CHECKPOINT), @"not a checkpoint");
                // A pairing manifest that cannot be read: Carafe logs the failure and keeps the library.
                settings.PairingManifest = models;
                var log = new StringWriter();
                var generator = new LibraryGenerator(settings, log);
                generator.Run();
                Assert.IsTrue(generator.SpectrumCount > 0, log.ToString());
                CollectionAssert.AreEqual(new[] { generator.BlibPath }, Directory.GetFiles(settings.OutputDirectory));
                StringAssert.Contains(log.ToString(), @"The pairing manifest could not be read to keep the library's pairs whole");
                Assert.AreEqual(0, generator.PairDropped.Count);
                StringAssert.Contains(log.ToString(), string.Format(CarafeModelDirectory.BOTH_MODELS_WARNING_FORMAT,
                    Path.Combine(models, ModelFiles.MS2_SAFETENSORS), checkpoint));

                // The models are opened, and the pretrained weights checked, before the FASTA is digested.
                string zip = Path.Combine(folder, @"pretrained_models.zip");
                var early = new LibrarySettings
                {
                    Database = Path.Combine(folder, @"missing.fasta"),
                    OutputDirectory = Path.Combine(folder, @"empty"),
                    PretrainedModels = zip,
                    Device = TorchDevice.CPU,
                };
                Assert.AreEqual(zip, Assert.ThrowsException<FileNotFoundException>(() => new LibraryGenerator(early, null).Run()).FileName);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// <c>-ccs</c>: every precursor gets the CCS model's timsTOF 1/K0, in the TSV's IonMobility
        /// column and the .blib's ionMobility (type inverseK0, with the CCS NULL as Carafe writes it)
        /// in both RefSpectra and RetentionTimes; the pretrained model's, or with <c>-tf all</c> the
        /// model folder's Carafe ccs_model.pt when it has one.
        /// </summary>
        [TestMethod]
        public void TestLibraryIonMobility()
        {
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Mobility_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                var pretrained = PretrainedModels.Open();
                var settings = CreateGeneratorSettings(folder, LibraryOutputs.BLIB_FORMAT + @",DIA-NN");
                settings.PredictIonMobility = true;
                var log = new StringWriter();
                var generator = new LibraryGenerator(settings, log, pretrained);
                generator.Run();
                StringAssert.Contains(log.ToString(), @"Using the pretrained CCS model");
                StringAssert.Contains(log.ToString(), @", CCS ");
                var mobilities = ReadMobilities(generator.BlibPath);
                Assert.AreEqual(generator.SpectrumCount, mobilities.Count);
                foreach (var row in mobilities)
                {
                    Assert.AreEqual(BlibLibraryWriter.ION_MOBILITY_TYPE_INVERSE_K0, row.Type, row.Label);
                    Assert.IsTrue(double.IsNaN(row.Ccs), row.Label);
                    Assert.IsTrue(row.InverseK0 > 0.4 && row.InverseK0 < 2.0, row.Label);
                }
                // The unmodified precursors' 1/K0 is the pretrained model's CCS, converted.
                var unmodified = mobilities.Where(m => m.ModifiedSequence == m.Sequence).ToList();
                Assert.IsTrue(unmodified.Count > 0);
                var precursors = unmodified.Select(m => new PrecursorForm(new PeptideForm(m.Sequence), m.Charge)).ToArray();
                using (var ccs = CcsModel.FromPretrained(pretrained, CPU))
                {
                    double[] predicted = ccs.Predict(precursors);
                    for (int i = 0; i < precursors.Length; i++)
                        Assert.AreEqual(TimsMobility.CcsToInverseK0(predicted[i], precursors[i]), unmodified[i].InverseK0, 1e-5, unmodified[i].Label);
                }

                // The TSV's IonMobility column holds the same values, to Carafe's four decimals.
                var tsvLines = File.ReadAllLines(generator.TsvPath);
                Assert.AreEqual(CarafeLibraryTsvWriter.HEADER_WITH_ION_MOBILITY, tsvLines[0]);
                var tsvMobilities = tsvLines.Skip(1).Select(l => l.Split('\t')).Select(c => c[1] + @"/" + c[3] + @" " + c[5]).Distinct().ToList();
                var blibMobilities = mobilities.Select(m => m.Sequence + @"/" + m.Charge + @" " + JavaNumberFormat.FormatFixed(m.InverseK0, 4)).ToList();
                tsvMobilities.Sort(StringComparer.Ordinal);
                blibMobilities.Sort(StringComparer.Ordinal);
                CollectionAssert.AreEqual(blibMobilities.Distinct().ToList(), tsvMobilities);

                // Carafe's fine-tuned ccs_model.pt in the model folder is used instead; these are the
                // pretrained weights, so the values are the same.
                string checkpoint = Path.Combine(settings.ModelDirectory, ModelFiles.CCS_CHECKPOINT);
                PretrainedModelTest.ExtractPretrainedEntry(pretrained, PretrainedModels.CCS_ENTRY, checkpoint);
                log = new StringWriter();
                generator = new LibraryGenerator(settings, log, pretrained);
                generator.Run();
                StringAssert.Contains(log.ToString(), @"Using fine-tuned CCS model " + checkpoint);
                CollectionAssert.AreEqual(mobilities.Select(m => m.Label + @" " + m.InverseK0.ToString(@"R", CultureInfo.InvariantCulture)).ToList(),
                    ReadMobilities(generator.BlibPath).Select(m => m.Label + @" " + m.InverseK0.ToString(@"R", CultureInfo.InvariantCulture)).ToList());

                // A -tf other than all takes the pretrained CCS model, as Carafe's Python does.
                settings.TrainingType = @"nce";
                log = new StringWriter();
                new LibraryGenerator(settings, log, pretrained).Run();
                StringAssert.Contains(log.ToString(), @"Using the pretrained CCS model");
                settings.TrainingType = LibrarySettings.DEFAULT_TRAINING_TYPE;

                // Without -ccs the library has no ion mobility, and the TSV no column for it.
                settings.PredictIonMobility = false;
                generator = new LibraryGenerator(settings, new StringWriter(), pretrained);
                generator.Run();
                Assert.IsTrue(ReadMobilities(generator.BlibPath).All(m => m.Type == BlibLibraryWriter.ION_MOBILITY_TYPE_NONE && double.IsNaN(m.InverseK0)));
                Assert.AreEqual(CarafeLibraryTsvWriter.HEADER, File.ReadLines(generator.TsvPath).First());
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// A saved model (.carafemodel) predicts a library of any peptides with no training: the
        /// same spectra its models predict from their folder, with the command line's m/z ranges
        /// (not the training run's window, which -model_dir would take) and the training run's
        /// NCE, instrument, activation, analyzer and rt_max for those the command line does not
        /// give. A fine-tuned MS2
        /// model that lost to the pretrained one is left out, and a damaged or foreign file fails
        /// before anything is predicted.
        /// </summary>
        [TestMethod]
        public void TestSavedModel()
        {
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Saved_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                var settings = CreateGeneratorSettings(folder, LibraryOutputs.BLIB_FORMAT);
                string models = settings.ModelDirectory;
                // The training run: a precursor window that holds none of the FASTA's peptides.
                File.WriteAllText(Path.Combine(models, ModelFiles.META),
                    @"{""run_a.mzML"":{""ms_file"":""run_a.mzML"",""nce"":31.0,""ms_instrument"":""Astral"",""activation"":""reCID"",""analyzer"":""LIT"",""rt_max"":45.0," +
                    @"""precursor_ion_mz_min"":1500.0,""precursor_ion_mz_max"":1504.0}}");
                string modelFile = Path.Combine(folder, @"hela" + CarafeModelFile.EXTENSION);
                var written = CarafeModelFile.Write(modelFile, CarafeModelDirectory.Open(models, true), @"all", PretrainedModels.PINNED_SHA256, null, null, AcquisitionVocabulary.DEFAULT, null,
                    TestModels.PretrainedOrigin(), TestModels.PretrainedOrigin());
                Assert.IsTrue(written.Ms2Used && written.RtUsed);
                CollectionAssert.AreEquivalent(new[] { ModelFiles.MS2_SAFETENSORS, ModelFiles.RT_SAFETENSORS, ModelFiles.METRICS, ModelFiles.META },
                    written.Entries.Keys.ToList());
                var opened = CarafeModelFile.Open(modelFile);
                Assert.AreEqual(CarafeModelFile.FORMAT, opened.Format);
                Assert.AreEqual(PretrainedModels.PINNED_SHA256, opened.PretrainedSha256);
                Assert.AreEqual(@"run_a.mzML", opened.Runs.Single().MsFile);
                Assert.AreEqual(31.0, opened.Nce);
                Assert.AreEqual(@"Astral", opened.Instrument);
                Assert.AreEqual(AcquisitionVocabulary.RE_CID, opened.Activation);
                Assert.AreEqual(AcquisitionVocabulary.LIT, opened.Analyzer);
                Assert.AreEqual(AcquisitionVocabulary.DEFAULT.ToString(), opened.Acquisition.ToString());
                Assert.AreEqual(45.0, opened.RtMax);

                // From the file, with no NCE, instrument or rt_max given: the training run's, and the
                // command line's precursor window, so every peptide the folder's models predict.
                settings.OutputDirectory = Path.Combine(folder, @"from_folder");
                settings.Nce = 31;
                settings.Instrument = @"Astral";
                settings.RtMax = 45;
                var fromFolder = new LibraryGenerator(settings, null);
                fromFolder.Run();
                var fileSettings = new LibrarySettings
                {
                    Database = settings.Database,
                    OutputDirectory = Path.Combine(folder, @"from_file"),
                    ModelFile = modelFile,
                    LibraryFormat = LibraryOutputs.BLIB_FORMAT,
                    Device = TorchDevice.CPU,
                    MinFragments = 1,
                };
                var log = new StringWriter();
                var fromFile = new LibraryGenerator(fileSettings, log);
                fromFile.Run();
                Assert.AreEqual(31.0, fileSettings.Nce);
                Assert.AreEqual(@"Astral", fileSettings.Instrument);
                Assert.AreEqual(AcquisitionVocabulary.RE_CID, fileSettings.Activation);
                Assert.AreEqual(AcquisitionVocabulary.LIT, fileSettings.Analyzer);
                Assert.AreEqual(45.0, fileSettings.RtMax);
                Assert.IsTrue(fromFile.SpectrumCount > 0, log.ToString());
                CollectionAssert.AreEqual(ReadSpectra(fromFolder.BlibPath), ReadSpectra(fromFile.BlibPath), @"the file holds the folder's models");
                StringAssert.Contains(log.ToString(), @"Use the saved model " + modelFile);

                // The command line's NCE, instrument, activation, analyzer and rt_max stay.
                var given = new LibrarySettings
                {
                    Nce = 25, UserNce = true, Instrument = @"Lumos", UserInstrument = true, RtMax = 60, UserRtMax = true,
                    Activation = AcquisitionVocabulary.BEAM_CID, UserActivation = true, Analyzer = AcquisitionVocabulary.TOF, UserAnalyzer = true,
                };
                opened.ApplyPredictionDefaults(given);
                Assert.AreEqual(25.0, given.Nce);
                Assert.AreEqual(@"Lumos", given.Instrument);
                Assert.AreEqual(AcquisitionVocabulary.BEAM_CID, given.Activation);
                Assert.AreEqual(AcquisitionVocabulary.TOF, given.Analyzer);
                Assert.AreEqual(60.0, given.RtMax);

                // A fine-tuned MS2 model that did not beat the pretrained one is not saved, so the
                // library predicts MS2 with the pretrained model.
                File.WriteAllText(Path.Combine(models, ModelFiles.METRICS), @"{""ms2"":{""use_finetuned_for_prediction"":false}}");
                string rtOnly = Path.Combine(folder, @"rt_only" + CarafeModelFile.EXTENSION);
                var lost = CarafeModelFile.Write(rtOnly, CarafeModelDirectory.Open(models, true), @"all", null, null, null, null, null,
                    TestModels.PretrainedOrigin(), TestModels.PretrainedOrigin());
                Assert.IsTrue(lost.Ms2FineTuned);
                Assert.IsFalse(lost.Ms2Used);
                Assert.IsFalse(lost.Entries.ContainsKey(ModelFiles.MS2_SAFETENSORS));
                var extracted = CarafeModelFile.Open(rtOnly).Extract(Path.Combine(folder, @"rt_only_extracted"));
                Assert.IsNull(extracted.GetMs2ModelPath(@"all"));
                Assert.IsNotNull(extracted.GetRtModelPath(@"all"));

                // A damaged entry, a file that is not a zip, one without a manifest, and a newer format fail on opening.
                string damaged = Path.Combine(folder, @"damaged" + CarafeModelFile.EXTENSION);
                File.Copy(modelFile, damaged);
                using (var zip = ZipFile.Open(damaged, ZipArchiveMode.Update))
                {
                    var rtEntry = zip.GetEntry(ModelFiles.RT_SAFETENSORS);
                    Assert.IsNotNull(rtEntry);
                    rtEntry.Delete();
                    using (var writer = new StreamWriter(zip.CreateEntry(ModelFiles.RT_SAFETENSORS).Open()))
                        writer.Write(@"not the model");
                }
                StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => CarafeModelFile.Open(damaged)).Message, @"does not match its SHA-256");
                string notZip = Path.Combine(folder, @"text" + CarafeModelFile.EXTENSION);
                File.WriteAllText(notZip, @"not a model");
                StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => CarafeModelFile.Open(notZip)).Message, @"is not a CarafeSharp model file");
                string noManifest = Path.Combine(folder, @"bare" + CarafeModelFile.EXTENSION);
                using (var zip = ZipFile.Open(noManifest, ZipArchiveMode.Create))
                    zip.CreateEntryFromFile(Path.Combine(models, ModelFiles.RT_SAFETENSORS), ModelFiles.RT_SAFETENSORS);
                StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => CarafeModelFile.Open(noManifest)).Message, CarafeModelFile.MANIFEST_ENTRY);
                string newer = CopyWithManifest(modelFile, @"newer", m => m.Replace(CarafeModelFile.FORMAT, @"carafemodel-3"));
                StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => CarafeModelFile.Open(newer)).Message, @"carafemodel-3");
                // A model network or start this CarafeSharp does not know is refused too.
                string unknownModel = CopyWithManifest(modelFile, @"unknown_model", m => m.Replace(@"""model"": ""alphapeptdeep""", @"""model"": ""prosit"""));
                StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => CarafeModelFile.Open(unknownModel)).Message, @"model prosit");
                string unknownStart = CopyWithManifest(modelFile, @"unknown_start", m => m.Replace(@"""start"": ""pretrained""", @"""start"": ""koina"""));
                StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => CarafeModelFile.Open(unknownStart)).Message, @"start koina");
                // An entry named outside the folder it would unpack to fails on opening, though its SHA-256 matches,
                // so -model and -model_info refuse it before anything is written.
                foreach (string escape in new[] { @"../", @"sub/", Path.GetFullPath(folder).TrimEnd('\\', '/') + Path.DirectorySeparatorChar })
                {
                    string escaping = Path.Combine(folder, @"escaping" + CarafeModelFile.EXTENSION);
                    File.Copy(modelFile, escaping, true);
                    using (var zip = ZipFile.Open(escaping, ZipArchiveMode.Update))
                    {
                        var manifestEntry = zip.GetEntry(CarafeModelFile.MANIFEST_ENTRY);
                        var rtEntry = zip.GetEntry(ModelFiles.RT_SAFETENSORS);
                        Assert.IsNotNull(manifestEntry);
                        Assert.IsNotNull(rtEntry);
                        string manifest;
                        using (var reader = new StreamReader(manifestEntry.Open()))
                            manifest = reader.ReadToEnd();
                        byte[] rt;
                        using (var stream = new MemoryStream())
                        {
                            using (var source = rtEntry.Open())
                                source.CopyTo(stream);
                            rt = stream.ToArray();
                        }
                        manifestEntry.Delete();
                        string key = "\"" + ModelFiles.RT_SAFETENSORS + "\":";
                        StringAssert.Contains(manifest, key);
                        string escapedName = escape + ModelFiles.RT_SAFETENSORS;
                        using (var writer = new StreamWriter(zip.CreateEntry(CarafeModelFile.MANIFEST_ENTRY).Open()))
                            writer.Write(manifest.Replace(key, JsonSerializer.Serialize(escapedName) + ":"));
                        using (var stream = zip.CreateEntry(escapedName).Open())
                            stream.Write(rt, 0, rt.Length);
                    }
                    StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => CarafeModelFile.Open(escaping)).Message,
                        @"is not a file name", escape);
                }
                Assert.ThrowsException<FileNotFoundException>(() => CarafeModelFile.Open(Path.Combine(folder, @"missing" + CarafeModelFile.EXTENSION)));

                // Each model's network, release and start, as written. The format before them is still read; it says none.
                Assert.AreEqual(RtModelType.alphapeptdeep, opened.RtModel);
                foreach (var origin in new[] { opened.Ms2Origin, opened.RtOrigin })
                {
                    Assert.AreEqual(CarafeModelOrigin.ALPHAPEPTDEEP, origin.Model);
                    Assert.AreEqual(PretrainedModels.VERSION, origin.Version);
                    Assert.AreEqual(CarafeModelOrigin.START_PRETRAINED, origin.Start);
                }
                var older = CarafeModelFile.Open(CopyWithManifest(modelFile, @"older", m => m.Replace(CarafeModelFile.FORMAT, CarafeModelFile.FORMAT_1)));
                Assert.AreEqual(CarafeModelFile.FORMAT_1, older.Format);
                Assert.IsNull(older.RtModel);
                Assert.IsNull(older.Ms2Origin);
                Assert.IsTrue(older.RtUsed);
                Assert.AreEqual(opened.RtMax, older.RtMax);
                var olderSettings = new LibrarySettings();
                older.ApplyPredictionDefaults(olderSettings);
                Assert.IsNull(olderSettings.RtModelType, @"its rt.safetensors says which");
                // A library that leaves its RT model out (-tf ms2) predicts with AlphaPeptDeep's pretrained one, as before.
                var olderMs2Settings = new LibrarySettings { TrainingType = @"ms2" };
                older.ApplyPredictionDefaults(olderMs2Settings);
                Assert.AreEqual(RtModelType.alphapeptdeep, olderMs2Settings.RtModelType);
                // One that holds no RT model predicted with AlphaPeptDeep's pretrained one, whatever the default is now.
                string ms2Only = Path.Combine(folder, @"ms2_only" + CarafeModelFile.EXTENSION);
                CarafeModelFile.Write(ms2Only, CarafeModelDirectory.Open(models, true), @"ms2", null, null, null, null, null,
                    TestModels.PretrainedOrigin(), TestModels.PretrainedOrigin());
                var olderMs2Only = CarafeModelFile.Open(CopyWithManifest(ms2Only, @"older_ms2_only", m => m.Replace(CarafeModelFile.FORMAT, CarafeModelFile.FORMAT_1)));
                Assert.IsFalse(olderMs2Only.RtUsed);
                olderMs2Only.ApplyPredictionDefaults(olderSettings);
                Assert.AreEqual(RtModelType.alphapeptdeep, olderSettings.RtModelType);
                Assert.AreEqual(RtModelType.alphapeptdeep, olderMs2Only.RtModelWithoutFineTuned, @"as a further fine-tune starts it");

                // A lineage entry is history: one naming a network or start this build does not know still reads.
                using (var origin = JsonDocument.Parse(@"{""model"": ""later_network"", ""model_version"": ""9"", ""start"": ""later_start""}"))
                {
                    Assert.AreEqual(@"later_network", CarafeModelOrigin.ReadFields(@"x", @"rt", origin.RootElement, false).Model);
                    Assert.ThrowsException<InvalidDataException>(() => CarafeModelOrigin.ReadFields(@"x", @"rt", origin.RootElement));
                }
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// The library writer thread: it writes every chunk predicted, in order, on all processors
        /// once a chunk is waiting; a writing failure ends a wait for room in the queue and fails
        /// the run; a prediction failure stops the writer; and a failed run leaves the previous
        /// library as it was.
        /// </summary>
        [TestMethod]
        public void TestLibraryWriterThread()
        {
            // A quarter of the processors while the writer keeps up, all of them once it is behind.
            Assert.AreEqual(4, LibraryChunkWriter.WriteThreads(false, 16));
            Assert.AreEqual(1, LibraryChunkWriter.WriteThreads(false, 3));
            Assert.AreEqual(-1, LibraryChunkWriter.WriteThreads(true, 16));

            // A second Dispose, as a using block's after an explicit one, does nothing.
            var idle = new LibraryChunkWriter(null, null, null);
            idle.Dispose();
            idle.Dispose();

            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Writer_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                var settings = CreateGeneratorSettings(folder, @"DIA-NN," + LibraryOutputs.BLIB_FORMAT);
                // One peptidoform per batch, so each is a chunk of its own.
                settings.PeptidesPerBatch = 1;

                // Every chunk predicted is written, in order, on one thread that is not the
                // predicting one. Chunk 0 is written only once chunk 1 waits behind it, so chunk 0
                // gets a quarter of the processors and chunk 1 all of them.
                var generator = new LibraryGenerator(settings, null);
                var predictedChunks = new List<int>();
                var writtenChunks = new List<int>();
                var writeThreads = new List<int>();
                var writerThreads = new HashSet<Thread>();
                using var chunkWaiting = new ManualResetEventSlim();
                generator.BeforePredictChunk = chunk =>
                {
                    predictedChunks.Add(chunk);
                    // Chunk 1 is queued before chunk 2 is predicted.
                    if (chunk == 2)
                        chunkWaiting.Set();
                };
                generator.BeforeWriteChunk = (chunk, threads) =>
                {
                    if (chunk == 0)
                        Assert.IsTrue(chunkWaiting.Wait(HOOK_TIMEOUT));
                    writtenChunks.Add(chunk);
                    writeThreads.Add(threads);
                    writerThreads.Add(Thread.CurrentThread);
                };
                generator.Run();
                Assert.IsTrue(predictedChunks.Count > LibraryChunkWriter.QUEUE_CAPACITY + 2, predictedChunks.Count.ToString());
                CollectionAssert.AreEqual(predictedChunks, writtenChunks);
                Assert.AreEqual(LibraryChunkWriter.WriteThreads(false, Environment.ProcessorCount), writeThreads[0]);
                Assert.AreEqual(-1, writeThreads[1]);
                Assert.AreEqual(1, writerThreads.Count);
                Assert.AreNotSame(Thread.CurrentThread, writerThreads.Single());
                Assert.IsTrue(generator.SpectrumCount > 0);
                byte[] previousBlib = File.ReadAllBytes(generator.BlibPath);
                string previousTsv = File.ReadAllText(generator.TsvPath);

                // The writer fails while prediction waits for room in the full queue: the wait
                // ends, nothing more is predicted, and the run throws the writer's exception.
                var writeFailure = new IOException(@"Test writing failure");
                var predictedBeforeFailure = new List<int>();
                var queueWaits = new List<int>();
                using var queueFull = new ManualResetEventSlim();
                var writerFails = new LibraryGenerator(settings, null)
                {
                    BeforePredictChunk = chunk => predictedBeforeFailure.Add(chunk),
                    // Chunk 0 is being written and chunk 1 fills the queue, so chunk 2 waits.
                    BeforeQueueWait = chunk =>
                    {
                        queueWaits.Add(chunk);
                        if (chunk == LibraryChunkWriter.QUEUE_CAPACITY + 1)
                            queueFull.Set();
                    },
                    BeforeWriteChunk = (chunk, threads) =>
                    {
                        Assert.IsTrue(queueFull.Wait(HOOK_TIMEOUT));
                        throw writeFailure;
                    },
                };
                Assert.AreSame(writeFailure, RunExpectingFailure(writerFails));
                CollectionAssert.AreEqual(Enumerable.Range(0, LibraryChunkWriter.QUEUE_CAPACITY + 2).ToArray(), predictedBeforeFailure.ToArray());
                CollectionAssert.Contains(queueWaits, LibraryChunkWriter.QUEUE_CAPACITY + 1);
                AssertPreviousLibrary(generator, settings.OutputDirectory, previousBlib, previousTsv);

                // Prediction fails while a chunk is being written: the writer thread is stopped
                // and has ended when the run throws the prediction's exception.
                var predictionFailure = new InvalidOperationException(@"Test prediction failure");
                Thread writerThread = null;
                using var writing = new ManualResetEventSlim();
                var predictionFails = new LibraryGenerator(settings, null)
                {
                    BeforeWriteChunk = (chunk, threads) =>
                    {
                        writerThread = Thread.CurrentThread;
                        writing.Set();
                    },
                    BeforePredictChunk = chunk =>
                    {
                        if (chunk == 1)
                        {
                            Assert.IsTrue(writing.Wait(HOOK_TIMEOUT));
                            throw predictionFailure;
                        }
                    },
                };
                Assert.AreSame(predictionFailure, RunExpectingFailure(predictionFails));
                Assert.IsNotNull(writerThread);
                Assert.IsFalse(writerThread.IsAlive);
                AssertPreviousLibrary(generator, settings.OutputDirectory, previousBlib, previousTsv);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                // Deleting files a hung run still holds would replace its failure with an IOException.
                if (!_runHung)
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

        /// <summary>
        /// A library written with a pairing manifest writes each target and decoy, and each
        /// entrapment target and entrapment decoy, only together: one whose partner has too few
        /// fragments is dropped, whichever came first, so nothing is written unpaired. A pair
        /// waits only until all of its precursors are offered. Each side keeps as many as the
        /// other kept. A sequence whose partner has two places in the manifest, a partner that is
        /// not in the library, and a precursor the manifest does not list all pass ungated.
        /// </summary>
        [TestMethod]
        public void TestDecoyPairGate()
        {
            var manifest = new List<DecoyPairPlanner.ManifestEntry>
            {
                new DecoyPairPlanner.ManifestEntry(@"PEPTIDEK", false, @"target", 1),
                new DecoyPairPlanner.ManifestEntry(@"EDITPEPK", true, @"decoy", 1),
                new DecoyPairPlanner.ManifestEntry(@"TIDEPEPK", false, @"p_target", 1),
                new DecoyPairPlanner.ManifestEntry(@"EPEDITPK", true, @"p_decoy", 1),
                new DecoyPairPlanner.ManifestEntry(@"SAMPLERMK", false, @"target", 2),
                new DecoyPairPlanner.ManifestEntry(@"MRELPMASK", true, @"decoy", 2),
                // A decoy that is also the entrapment decoy: the planner pairs it once, so neither is gated.
                new DecoyPairPlanner.ManifestEntry(@"LLRLLGR", false, @"target", 3),
                new DecoyPairPlanner.ManifestEntry(@"GLLRLLR", true, @"decoy", 3),
                new DecoyPairPlanner.ManifestEntry(@"LRGLLLR", false, @"p_target", 3),
                new DecoyPairPlanner.ManifestEntry(@"GLLRLLR", true, @"p_decoy", 3),
                new DecoyPairPlanner.ManifestEntry(@"MISSINGK", false, @"target", 4),
                new DecoyPairPlanner.ManifestEntry(@"GNISSIMK", true, @"decoy", 4),
            };
            var gate = new DecoyPairGate(manifest);
            Assert.IsTrue(gate.IsMember(@"PEPTLDEK"), @"I/L-normalized, as the planner matches");
            Assert.IsFalse(gate.IsMember(@"LLRLLGR"));
            Assert.IsFalse(gate.IsMember(@"GLLRLLR"));
            Assert.IsFalse(gate.IsMember(@"OTHERK"));
            foreach (string sequence in new[] { @"PEPTIDEK", @"EDITPEPK", @"TIDEPEPK", @"EPEDITPK", @"MISSINGK" })
                gate.Expect(sequence, 2, string.Empty);
            // Two forms of each side with one modification set (Oxidation on either M).
            foreach (string sequence in new[] { @"SAMPLERMK", @"SAMPLERMK", @"MRELPMASK", @"MRELPMASK" })
                gate.Expect(sequence, 2, @"Oxidation@M");
            gate.Expect(@"SAMPLERMK", 3, string.Empty);
            gate.Expect(@"MRELPMASK", 3, string.Empty);

            var target = Spectrum(@"PEPTIDEK", 2);
            var decoy = Spectrum(@"EDITPEPK", 2);
            var released = new List<LibrarySpectrum>();
            // A pair split across chunks waits for its decoy; precursors outside the manifest pass.
            var other = Spectrum(@"OTHERK", 2);
            gate.Offer(@"PEPTIDEK", 2, string.Empty, target, released);
            gate.Offer(@"OTHERK", 2, string.Empty, other, released);
            CollectionAssert.AreEqual(new[] { other }, released);
            released.Clear();
            gate.Offer(@"EDITPEPK", 2, string.Empty, decoy, released);
            CollectionAssert.AreEqual(new[] { target, decoy }, released);

            // An entrapment decoy below the minimum takes its entrapment target with it.
            released.Clear();
            var entrapment = Spectrum(@"TIDEPEPK", 2);
            gate.Offer(@"TIDEPEPK", 2, string.Empty, entrapment, released);
            gate.Offer(@"EPEDITPK", 2, string.Empty, null, released);
            Assert.AreEqual(0, released.Count);
            CollectionAssert.AreEqual(new[] { entrapment }, gate.Dropped.ToArray());

            // Two target forms against one decoy form kept: one of each, the first offered.
            var first = Spectrum(@"SAMPLERMK", 2);
            var second = Spectrum(@"SAMPLERMK", 2);
            var kept = Spectrum(@"MRELPMASK", 2);
            gate.Offer(@"SAMPLERMK", 2, @"Oxidation@M", first, released);
            gate.Offer(@"MRELPMASK", 2, @"Oxidation@M", null, released);
            gate.Offer(@"SAMPLERMK", 2, @"Oxidation@M", second, released);
            gate.Offer(@"MRELPMASK", 2, @"Oxidation@M", kept, released);
            CollectionAssert.AreEqual(new[] { first, kept }, released);
            CollectionAssert.AreEqual(new[] { entrapment, second }, gate.Dropped.ToArray());

            // Ungated: the shared decoy and its precursors, and a target whose decoy is not in the library.
            released.Clear();
            var twin = Spectrum(@"LLRLLGR", 2);
            var missing = Spectrum(@"MISSINGK", 2);
            gate.Offer(@"LLRLLGR", 2, string.Empty, twin, released);
            gate.Offer(@"MISSINGK", 2, string.Empty, missing, released);
            CollectionAssert.AreEqual(new[] { twin, missing }, released);

            // A pair whose decoy was expected but never offered is not written whole: Finish drops its target.
            released.Clear();
            var alone = Spectrum(@"SAMPLERMK", 3);
            gate.Offer(@"SAMPLERMK", 3, string.Empty, alone, released);
            Assert.AreEqual(0, released.Count);
            gate.Finish(released);
            Assert.AreEqual(0, released.Count);
            CollectionAssert.AreEqual(new[] { entrapment, second, alone }, gate.Dropped.ToArray());
        }

        private static LibrarySpectrum Spectrum(string sequence, int charge)
        {
            return new LibrarySpectrum(new PrecursorForm(new PeptideForm(sequence), charge), 500, 10, @"P1", 0, Array.Empty<LibraryFragment>());
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

        /// <summary>
        /// Settings that predict a library on the CPU from randomly initialized models and a
        /// one-protein FASTA, both written into <paramref name="folder"/>.
        /// </summary>
        private static LibrarySettings CreateGeneratorSettings(string folder, string libraryFormat)
        {
            string models = Path.Combine(folder, @"models");
            Directory.CreateDirectory(models);
            WriteRandomModels(models);
            string fasta = Path.Combine(folder, @"proteins.fasta");
            File.WriteAllText(fasta, ">sp|P1|A\nMPEPTIDEKSAMPLERLVNELTEFAK\n");
            return new LibrarySettings
            {
                Database = fasta,
                OutputDirectory = Path.Combine(folder, @"out"),
                ModelDirectory = models,
                PreferSafetensors = true,
                LibraryFormat = libraryFormat,
                Device = TorchDevice.CPU,
                RtMax = 30,
                MinFragments = 1,
            };
        }

        /// <summary>
        /// A .blib's ion mobility per spectrum, in id order, checking that RetentionTimes repeats
        /// RefSpectra's; NaN where a value is NULL.
        /// </summary>
        private static List<(string Label, string Sequence, string ModifiedSequence, int Charge, double PrecursorMz, double InverseK0, double Ccs,
            int Type)> ReadMobilities(string blib)
        {
            var rows = new List<(string, string, string, int, double, double, double, int)>();
            using (var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = blib, ReadOnly = true }.ToString()))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"SELECT r.peptideSeq, r.peptideModSeq, r.precursorCharge, r.precursorMZ, r.ionMobility, " +
                                                       @"r.collisionalCrossSectionSqA, CAST(r.ionMobilityType AS INTEGER), t.ionMobility, " +
                                                       @"t.collisionalCrossSectionSqA, CAST(t.ionMobilityType AS INTEGER) " +
                                                       @"FROM RefSpectra r JOIN RetentionTimes t ON t.RefSpectraID = r.id ORDER BY r.id", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string label = reader.GetString(1) + @"/" + reader.GetInt64(2);
                        for (int i = 4; i < 7; i++)
                            Assert.AreEqual(reader.GetValue(i), reader.GetValue(i + 3), label + @": RetentionTimes differs from RefSpectra");
                        rows.Add((label, reader.GetString(0), reader.GetString(1), (int)reader.GetInt64(2), reader.GetDouble(3),
                            reader.IsDBNull(4) ? double.NaN : reader.GetDouble(4), reader.IsDBNull(5) ? double.NaN : reader.GetDouble(5),
                            (int)reader.GetInt64(6)));
                    }
                }
            }
            return rows;
        }

        /// <summary>A .blib's spectra, one line each: modified sequence, charge, precursor m/z, RT and the peak blobs.</summary>
        private static List<string> ReadSpectra(string blib)
        {
            var spectra = new List<string>();
            using (var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = blib, ReadOnly = true }.ToString()))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"SELECT r.peptideModSeq, r.precursorCharge, r.precursorMZ, r.retentionTime, p.peakMZ, p.peakIntensity " +
                                                       @"FROM RefSpectra r JOIN RefSpectraPeaks p ON p.RefSpectraID = r.id", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        spectra.Add(string.Join(@"|", reader.GetString(0), reader.GetInt64(1), reader.GetDouble(2).ToString(@"R", CultureInfo.InvariantCulture),
                            reader.GetDouble(3).ToString(@"R", CultureInfo.InvariantCulture), Convert.ToBase64String((byte[])reader.GetValue(4)),
                            Convert.ToBase64String((byte[])reader.GetValue(5))));
                    }
                }
            }
            spectra.Sort(StringComparer.Ordinal);
            return spectra;
        }

        /// <summary>A copy of the saved model <paramref name="modelFile"/> named <paramref name="name"/>, its manifest changed.</summary>
        private static string CopyWithManifest(string modelFile, string name, Func<string, string> change)
        {
            string copy = Path.Combine(Path.GetDirectoryName(modelFile) ?? string.Empty, name + CarafeModelFile.EXTENSION);
            File.Copy(modelFile, copy);
            using (var zip = ZipFile.Open(copy, ZipArchiveMode.Update))
            {
                var entry = zip.GetEntry(CarafeModelFile.MANIFEST_ENTRY);
                Assert.IsNotNull(entry);
                string manifest;
                using (var reader = new StreamReader(entry.Open()))
                    manifest = reader.ReadToEnd();
                entry.Delete();
                string changed = change(manifest);
                Assert.AreNotEqual(manifest, changed, @"the manifest change applies");
                using (var writer = new StreamWriter(zip.CreateEntry(CarafeModelFile.MANIFEST_ENTRY).Open()))
                    writer.Write(changed);
            }
            return copy;
        }

        /// <summary>
        /// Runs the generator on a thread of its own and returns the exception it throws. A run
        /// that has not ended in time fails the test instead of hanging it.
        /// </summary>
        private Exception RunExpectingFailure(LibraryGenerator generator)
        {
            Exception thrown = null;
            var runner = new Thread(() =>
            {
                try
                {
                    generator.Run();
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            }) { IsBackground = true };
            runner.Start();
            if (!runner.Join(HOOK_TIMEOUT))
            {
                _runHung = true;
                Assert.Fail(@"The run did not end");
            }
            Assert.IsNotNull(thrown, @"The run did not fail");
            return thrown;
        }

        /// <summary>The library <paramref name="previous"/> wrote is as it was, with no partial file beside it.</summary>
        private static void AssertPreviousLibrary(LibraryGenerator previous, string outputDirectory, byte[] blib, string tsv)
        {
            CollectionAssert.AreEquivalent(new[] { previous.BlibPath, previous.TsvPath }, Directory.GetFiles(outputDirectory));
            CollectionAssert.AreEqual(blib, File.ReadAllBytes(previous.BlibPath));
            Assert.AreEqual(tsv, File.ReadAllText(previous.TsvPath));
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
