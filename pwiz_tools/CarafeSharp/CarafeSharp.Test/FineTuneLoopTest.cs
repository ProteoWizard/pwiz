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
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Models.Modules;
using pwiz.CarafeSharp.Proteome;
using pwiz.CarafeSharp.Training;
using TorchSharp;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The fine-tuning loop on randomly initialized models and a handful of synthetic rows, on
    /// the CPU: the loss falls, a fine-tuned model saves and reloads to identical predictions,
    /// and the seed decides everything (the same seed gives identical weights and losses, a
    /// different one does not). Then a training run end to end, from a training export written
    /// in the test to the model folder, with no pretrained models.
    /// </summary>
    [TestClass]
    public class FineTuneLoopTest
    {
        private const int TEST_THREADS = 2;

        private static readonly string[] MS2_PEPTIDES = { @"PEPTIDEK", @"SAMPLERK", @"LVNELTEFAK" };

        private static readonly (string Sequence, double RtNorm)[] RT_PEPTIDES =
        {
            (@"PEPTIDEK", 0.3), (@"SAMPLERK", 0.4), (@"LVNELTEFAK", 0.7),
            (@"GAGSSEPVTGLDAK", 0.2), (@"YILAGVENSK", 0.5), (@"ELVISLIVESK", 0.8),
        };

        public TestContext TestContext { get; set; }

        private string _folder;
        private int _threads;

        [TestInitialize]
        public void CreateFolder()
        {
            _folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"FineTune_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(_folder);
            // Batches this small run faster on few threads, and keep the test light on a busy machine.
            _threads = get_num_threads();
            set_num_threads(Math.Min(_threads, TEST_THREADS));
        }

        [TestCleanup]
        public void DeleteFolder()
        {
            set_num_threads(_threads);
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void TestFineTuneLossAndCheckpoints()
        {
            var ms2Rows = Ms2Rows();
            var settings = new FineTuneSettings { Epochs = 6, WarmupEpochs = 0, BatchSize = 8, LearningRate = 1e-3, AdjustBatchSize = false };
            string tunedMs2 = Path.Combine(_folder, ModelFiles.MS2_SAFETENSORS);
            float[] tunedPrediction;
            using (var model = Ms2Model.FromSafetensors(RandomMs2Model(@"start_ms2", 11), CPU))
            {
                var before = Ms2Metrics.Evaluate(model, ms2Rows);
                var history = ModelFineTuner.TrainMs2(model, ms2Rows, ms2Rows, settings, 8, new NumpyRandomState(11), null);
                AssertLossFalls(history, settings);
                // Better on every row it trained on than the random start.
                var after = Ms2Metrics.Evaluate(model, ms2Rows);
                Assert.AreEqual(ms2Rows.Length, after.Count);
                Assert.IsTrue(after.Cos > before.Cos, string.Format(CultureInfo.InvariantCulture, @"cos {0} -> {1}", before.Cos, after.Cos));
                // The fine-tuned weights save and reload to identical predictions, and save again to the same bytes.
                tunedPrediction = Predict(model, ms2Rows);
                model.Save(tunedMs2);
            }
            using (var reloaded = Ms2Model.FromSafetensors(tunedMs2, CPU))
            {
                CollectionAssert.AreEqual(tunedPrediction, Predict(reloaded, ms2Rows));
                string again = Path.Combine(_folder, @"again.safetensors");
                reloaded.Save(again);
                CollectionAssert.AreEqual(File.ReadAllBytes(tunedMs2), File.ReadAllBytes(again));
            }

            // The same for the RT model, whose loss is plain L1 on the normalized RT.
            var rtRows = RtRows();
            var rtSettings = new FineTuneSettings { Epochs = 6, WarmupEpochs = 0, BatchSize = 4, LearningRate = 1e-3, AdjustBatchSize = false };
            string tunedRt = Path.Combine(_folder, ModelFiles.RT_SAFETENSORS);
            double[] rtPrediction;
            using (var model = RtModel.FromSafetensors(RandomRtModel(@"start_rt", 11), CPU))
            {
                var before = RtMetrics.Evaluate(model, rtRows);
                var history = ModelFineTuner.TrainRt(model, rtRows, rtRows, rtSettings, 4, new NumpyRandomState(11), null);
                AssertLossFalls(history, rtSettings);
                var after = RtMetrics.Evaluate(model, rtRows);
                Assert.IsTrue(after.MedianAbsoluteError < before.MedianAbsoluteError,
                    string.Format(CultureInfo.InvariantCulture, @"RT MAE {0} -> {1}", before.MedianAbsoluteError, after.MedianAbsoluteError));
                rtPrediction = model.Predict(rtRows.Select(r => r.Peptide).ToArray());
                model.Save(tunedRt);
            }
            using (var reloaded = RtModel.FromSafetensors(tunedRt, CPU))
                CollectionAssert.AreEqual(rtPrediction, reloaded.Predict(rtRows.Select(r => r.Peptide).ToArray()));

            // Carafe's r2_score on a constant test set: 1 when fitted exactly, else 0.
            using (var model = RtModel.FromSafetensors(tunedRt, CPU))
            {
                double predicted = model.Predict(new[] { new PeptideForm(@"PEPTIDEK") })[0];
                Assert.AreEqual(1.0, RtMetrics.Evaluate(model, new[] { new RtTrainingExample(new PeptideForm(@"PEPTIDEK"), predicted) }).R2);
                Assert.AreEqual(0.0, RtMetrics.Evaluate(model, new[] { new RtTrainingExample(new PeptideForm(@"PEPTIDEK"), predicted + 1) }).R2);
            }
        }

        [TestMethod]
        public void TestFineTuneSeed()
        {
            // A seed decides a random model's weights.
            CollectionAssert.AreEqual(File.ReadAllBytes(RandomMs2Model(@"seed5a", 5)), File.ReadAllBytes(RandomMs2Model(@"seed5b", 5)));
            CollectionAssert.AreNotEqual(File.ReadAllBytes(RandomMs2Model(@"seed5a", 5)), File.ReadAllBytes(RandomMs2Model(@"seed6", 6)));

            // And a fine-tuning run's: the same seed from the same start gives identical models
            // and loss histories, another seed (another dropout and shuffle) does not.
            string startMs2 = RandomMs2Model(@"start_ms2", 3);
            string startRt = RandomRtModel(@"start_rt", 3);
            var first = FineTune(@"first", 21, startMs2, startRt);
            var second = FineTune(@"second", 21, startMs2, startRt);
            var other = FineTune(@"other", 22, startMs2, startRt);
            foreach (string file in new[] { FineTuneRun.MS2_MODEL_FILE, FineTuneRun.RT_MODEL_FILE })
            {
                CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(_folder, @"first", file)),
                    File.ReadAllBytes(Path.Combine(_folder, @"second", file)), file);
                CollectionAssert.AreNotEqual(File.ReadAllBytes(Path.Combine(_folder, @"first", file)),
                    File.ReadAllBytes(Path.Combine(_folder, @"other", file)), file);
            }
            CollectionAssert.AreEqual(Losses(first.Ms2History), Losses(second.Ms2History));
            CollectionAssert.AreEqual(Losses(first.RtHistory), Losses(second.RtHistory));
            CollectionAssert.AreNotEqual(Losses(first.Ms2History), Losses(other.Ms2History));
            CollectionAssert.AreNotEqual(Losses(first.RtHistory), Losses(other.RtHistory));
            // The baseline both are judged against is the start model, scored identically.
            Assert.AreEqual(first.Ms2Pretrained.Cos, other.Ms2Pretrained.Cos);
            Assert.AreEqual(first.RtPretrained.R2, other.RtPretrained.R2);

            // What the run wrote: Carafe's metrics, which library prediction reads, and CarafeSharp's record.
            string folder = Path.Combine(_folder, @"first");
            using (var metrics = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, ModelFiles.METRICS))))
            {
                var ms2 = metrics.RootElement.GetProperty(ModelFiles.METRICS_MS2);
                Assert.AreEqual(first.UseFineTunedMs2, ms2.GetProperty(ModelFiles.METRICS_USE_FINETUNED).GetBoolean());
                Assert.AreEqual(first.Ms2FineTuned.Cos, ms2.GetProperty(ModelFiles.METRICS_FINETUNED).GetProperty(@"cos").GetDouble());
                Assert.AreEqual(first.Ms2Pretrained.Spc, ms2.GetProperty(ModelFiles.METRICS_PRETRAINED).GetProperty(@"spc").GetDouble());
                var rt = metrics.RootElement.GetProperty(ModelFiles.METRICS_RT);
                Assert.AreEqual(first.RtFineTuned.R2, rt.GetProperty(ModelFiles.METRICS_FINETUNED).GetProperty(@"r2").GetDouble());
            }
            using (var info = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, FineTuneRun.MODEL_INFO_FILE))))
            {
                var root = info.RootElement;
                Assert.AreEqual(21u, root.GetProperty(@"seed").GetUInt32());
                Assert.AreEqual(JsonValueKind.Null, root.GetProperty(@"pretrained_sha256").ValueKind);
                Assert.AreEqual(startMs2, root.GetProperty(@"ms2_start_model").GetString());
                Assert.AreEqual(first.RtTrainCount, root.GetProperty(@"rt").GetProperty(@"train_rows").GetInt32());
                Assert.AreEqual(first.Ms2BatchSize, root.GetProperty(@"ms2").GetProperty(@"batch_size").GetInt32());
                Assert.AreEqual(2, root.GetProperty(@"ms2").GetProperty(@"epochs").GetInt32());
            }
            var modelDirectory = CarafeModelDirectory.Open(folder, true);
            Assert.AreEqual(first.UseFineTunedMs2, modelDirectory.UseFineTunedMs2);
            Assert.IsTrue(modelDirectory.HasRtModel);

            // Asked to train nothing, a run writes empty metrics; asked for a model with no rows, it stops.
            string nothing = Path.Combine(_folder, @"nothing");
            var none = FineTuneRun.Run(null, null, null, new FineTuneOptions(), nothing, null);
            Assert.IsNull(none.Ms2FineTuned);
            Assert.AreEqual(@"{}", File.ReadAllText(Path.Combine(nothing, ModelFiles.METRICS)));
            Assert.ThrowsException<InvalidOperationException>(() =>
                FineTuneRun.Run(null, Array.Empty<Ms2TrainingExample>(), null, new FineTuneOptions(), nothing, null));
        }

        [TestMethod]
        public void TestModelTrainerRun()
        {
            // One run's export of six clean precursors, trained -tf ms2 from a random -ms2_model.
            string export = WriteCleanExport(@"run_a");
            string output = Path.Combine(_folder, @"out");
            var settings = CarafeCommandLine.Parse(new[] { @"-i", export, @"-o", output, @"-tf", @"ms2", @"-seed", @"9",
                @"-ms2_model", RandomMs2Model(@"start_ms2", 9), @"-device", @"cpu", @"-nce", @"28" }).TrainingSettings;
            var log = new StringWriter();
            string pretrainedAsked = null;
            var trainer = new ModelTrainer(settings, log, zip =>
            {
                pretrainedAsked = zip ?? string.Empty;
                return null;
            });
            trainer.Run();

            // The pretrained models are opened (here: asked for) before the export is read.
            Assert.AreEqual(string.Empty, pretrainedAsked);
            Assert.AreEqual(6, trainer.Stats.Ms2Rows);
            Assert.AreEqual(3, trainer.Stats.RtRows);
            Assert.IsNull(trainer.Result.RtFineTuned);
            Assert.IsNotNull(trainer.Result.Ms2FineTuned);
            Assert.AreEqual(FineTuneSettings.Ms2Defaults().Epochs, trainer.Result.Ms2History.Count);
            foreach (string file in new[] { CarafeTrainingDirectory.PSM_FILE, CarafeTrainingDirectory.RT_FILE, FineTuneRun.MS2_MODEL_FILE,
                         ModelFiles.METRICS, FineTuneRun.MODEL_INFO_FILE, ModelFiles.META })
            {
                Assert.IsTrue(File.Exists(Path.Combine(output, file)), file);
            }
            Assert.IsFalse(File.Exists(Path.Combine(output, FineTuneRun.RT_MODEL_FILE)));
            // The training tables hold exactly the rows trained on.
            Assert.AreEqual(6, CarafeTrainingDirectory.ReadMs2(output, 28, @"Astral").Count);
            // meta.json: the run by its stem (no -ms), its detected instrument, its own window and
            // rt_max, and -nce for the collision energy it does not record.
            var run = CarafeModelDirectory.Open(output).Runs.Single();
            Assert.AreEqual(@"run_a", run.MsFile);
            Assert.AreEqual(@"Astral", run.MsInstrument);
            Assert.AreEqual(28.0, run.Nce);
            Assert.AreEqual(12.1, run.RtMax, 1e-12);
            Assert.AreEqual(150.0, run.MinFragmentIonMz);
            Assert.AreEqual(380.0, run.PrecursorMzMin);

            // Every training run saves its model as one file, to predict later libraries from:
            // the MS2 model fine-tuned here (held only when it beat its start model), no RT model
            // (-tf ms2), and the NCE, instrument and rt_max a library takes from this run.
            var saved = CarafeModelFile.Open(Path.Combine(output, CarafeModelFile.DEFAULT_FILE_NAME));
            Assert.IsTrue(saved.Ms2FineTuned);
            Assert.AreEqual(trainer.Result.UseFineTunedMs2, saved.Ms2Used);
            Assert.AreEqual(saved.Ms2Used, saved.Entries.ContainsKey(ModelFiles.MS2_SAFETENSORS));
            Assert.IsFalse(saved.RtFineTuned);
            Assert.IsFalse(saved.RtUsed);
            Assert.AreEqual(@"start_ms2.safetensors", saved.Ms2StartModel);
            Assert.AreEqual(@"run_a", saved.Runs.Single().MsFile);
            Assert.AreEqual(28.0, saved.Nce);
            Assert.AreEqual(@"Astral", saved.Instrument);
            Assert.AreEqual(12.1, saved.RtMax, 1e-12);
            // The export names no activation, and an Astral reads MS2 out in its time-of-flight analyzer.
            Assert.IsNull(saved.Activation);
            Assert.AreEqual(AcquisitionVocabulary.TOF, saved.Analyzer);
            Assert.AreEqual(AcquisitionVocabulary.DEFAULT.ToString(), saved.Acquisition.ToString());
            Assert.IsFalse(File.Exists(Path.Combine(output, CarafeModelFile.DEFAULT_FILE_NAME + @".tmp")));

            // With what the models were trained on, for a user choosing a model: the settings, the
            // training data, the run's acquisition from its export, and the held-out metrics.
            var training = saved.Training;
            Assert.AreEqual(0.01, training.Fdr);
            Assert.IsTrue(training.Masking);
            Assert.AreEqual(9u, training.Seed);
            Assert.AreEqual(trainer.Stats.Ms2Rows, training.Ms2Spectra);
            Assert.AreEqual(trainer.Stats.RtRows, training.RtPeptideForms);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, training.Ms2Charges.Keys.ToList());
            Assert.AreEqual(8, training.MinPeptideLength);
            Assert.AreEqual(10, training.MaxPeptideLength);
            var trainingRun = training.Runs.Single();
            Assert.AreEqual(@"run_a", trainingRun.MsFile);
            Assert.AreEqual(@"Orbitrap Astral", trainingRun.InstrumentModel);
            Assert.AreEqual(@"Astral", trainingRun.Instrument);
            Assert.AreEqual(28.0, trainingRun.Nce);
            Assert.AreEqual(12.0, trainingRun.RtMax);
            Assert.AreEqual(380.0, trainingRun.IsolationMzMin);
            Assert.AreEqual(980.0, trainingRun.IsolationMzMax);
            Assert.AreEqual(150.0, trainingRun.Ms2MzMin);
            Assert.AreEqual(2000.0, trainingRun.Ms2MzMax);
            Assert.AreEqual(6, trainingRun.Precursors);
            Assert.AreEqual(3, trainingRun.PrecursorCharges[2]);
            Assert.AreEqual(3, trainingRun.PrecursorCharges[3]);
            Assert.IsTrue(training.HeldOutMetrics.ContainsKey(@"ms2.finetuned.cos"), string.Join(@", ", training.HeldOutMetrics.Keys));
            // -model_info prints it.
            string info = saved.FormatInfo();
            StringAssert.Contains(info, @"Orbitrap Astral (trained as Astral)");
            StringAssert.Contains(info, @"Run run_a: activation unknown, ToF");
            StringAssert.Contains(info, @"6 precursors (3 at 2+, 3 at 3+)");
        }

        /// <summary>
        /// A training run with <c>-db</c> predicts the final library right after training, as
        /// Carafe's does: from the model this run wrote, not a checkpoint an earlier Carafe run
        /// left in <c>-o</c>, and with the training run's precursor window, NCE and instrument.
        /// Its log names a run whose q-values came from Osprey's first pass, exports of two
        /// different Osprey searches, and <c>-no_masking</c>.
        /// </summary>
        [TestMethod]
        public void TestModelTrainerPredictsLibrary()
        {
            string exportA = WriteCleanExport(@"run_a", @"search_a", @"2");
            string exportB = WriteCleanExport(@"run_b", @"search_b", @"1");
            string output = Path.Combine(_folder, @"out");
            Directory.CreateDirectory(output);
            string staleCheckpoint = Path.Combine(output, ModelFiles.MS2_CHECKPOINT);
            File.WriteAllText(staleCheckpoint, @"not a checkpoint");
            File.WriteAllText(Path.Combine(output, ModelFiles.RT_CHECKPOINT), @"not a checkpoint");
            string fasta = Path.Combine(_folder, @"proteins.fasta");
            File.WriteAllText(fasta, ">sp|P1|A\nMPEPTIDEKSAMPLERLVNELTEFAK\n");
            var settings = CarafeCommandLine.Parse(new[] { @"-i", exportA + @"," + exportB, @"-o", output, @"-tf", @"ms2",
                @"-seed", @"9", @"-ms2_model", RandomMs2Model(@"start_ms2", 9), @"-device", @"cpu", @"-nce", @"28", @"-no_masking",
                @"-db", fasta, @"-lf_type", LibraryOutputs.BLIB_FORMAT, @"-lf_min_n_frag", @"1" }).TrainingSettings;
            var log = new StringWriter();
            new ModelTrainer(settings, log, zip => null).Run();
            string text = log.ToString();

            StringAssert.Contains(text, string.Format(@"WARNING: {0} took its run q-values from Osprey's first pass", exportB));
            Assert.IsFalse(text.Contains(exportA + @" took its run q-values"), @"a second-pass export is not warned about");
            StringAssert.Contains(text, @"come from different Osprey searches");
            StringAssert.Contains(text, @"-no_masking: training on every ion of the kept spectra");
            // The library opens the model this run wrote, over the stale checkpoint beside it.
            StringAssert.Contains(text, string.Format(CarafeModelDirectory.BOTH_MODELS_WARNING_FORMAT,
                Path.Combine(output, ModelFiles.MS2_SAFETENSORS), staleCheckpoint));
            // Each run's isolation window widened by 0.5, the -nce the exports lack, the detected instrument.
            StringAssert.Contains(text, @"From the training run: precursor m/z 379.5-980.5, NCE 28, instrument Astral,");
            Assert.AreEqual(1, Directory.GetFiles(output, @"*.blib").Length, text);
        }

        /// <summary>
        /// CarafeSharp's acquisition layer. A model that has never trained it predicts the same
        /// with an activation and analyzer as without: its columns are zero. Fine-tuning on reCID
        /// spectra read out in an ion trap trains the reCID and LIT columns alone, so beam-CID and
        /// Orbitrap still predict as before. A model records its columns by name, and one whose list
        /// lacks a known value gains a zero column for it, each stored column kept under its name.
        /// </summary>
        [TestMethod]
        public void TestAcquisitionColumns()
        {
            string start = RandomMs2Model(@"acquisition_start", 5);
            var rows = Ms2Rows(@"Lumos", AcquisitionVocabulary.RE_CID, AcquisitionVocabulary.LIT);
            float[] As(Ms2Model model, string activation, string analyzer) =>
                Predict(model, rows.Select(r => new Ms2TrainingExample(r.Precursor, r.Nce, r.Instrument, r.Intensities, r.Invalid, activation, analyzer)));
            using (var untrained = Ms2Model.FromSafetensors(start, CPU))
            {
                var none = As(untrained, null, null);
                CollectionAssert.AreEqual(none, As(untrained, AcquisitionVocabulary.RE_CID, AcquisitionVocabulary.LIT));
                CollectionAssert.AreEqual(none, As(untrained, AcquisitionVocabulary.BEAM_CID, AcquisitionVocabulary.TOF));
                Assert.AreEqual(AcquisitionVocabulary.DEFAULT.ToString(), untrained.Vocabulary.ToString());
            }

            var options = new FineTuneOptions
            {
                Seed = 5,
                Ms2Model = start,
                Ms2 = new FineTuneSettings { Epochs = 2, WarmupEpochs = 0, BatchSize = 4, LearningRate = 1e-3, AdjustBatchSize = false },
            };
            string output = Path.Combine(_folder, @"acquisition_tuned");
            FineTuneRun.Run(null, rows, null, options, output, null);
            string tuned = Path.Combine(output, FineTuneRun.MS2_MODEL_FILE);
            var metadata = StateDict.ReadSafetensorsMetadata(tuned);
            Assert.AreEqual(@"beam-CID,reCID", metadata[AcquisitionVocabulary.ACTIVATIONS_KEY]);
            Assert.AreEqual(@"Orbitrap,LIT,ToF", metadata[AcquisitionVocabulary.ANALYZERS_KEY]);
            using (var model = Ms2Model.FromSafetensors(tuned, CPU))
            {
                var none = As(model, null, null);
                CollectionAssert.AreNotEqual(none, As(model, AcquisitionVocabulary.RE_CID, null), @"the reCID column trained");
                CollectionAssert.AreNotEqual(none, As(model, null, AcquisitionVocabulary.LIT), @"the LIT column trained");
                CollectionAssert.AreEqual(none, As(model, AcquisitionVocabulary.BEAM_CID, AcquisitionVocabulary.ORBITRAP), @"the others did not");
            }

            // A model whose list has only beam-CID and Orbitrap, its beam-CID column trained: loaded,
            // it has every known column, beam-CID's weights under beam-CID and the new ones zero.
            string partial = Path.Combine(_folder, @"acquisition_partial.safetensors");
            manual_seed(6);
            using (var network = new ModelMs2Bert(acquisitionWidth: 2))
            {
                using (no_grad())
                    network.state_dict()[@"meta_nn.acquisition_nn.weight"][TensorIndex.Colon, 0].fill_(0.5);
                var held = new AcquisitionVocabulary(new[] { AcquisitionVocabulary.BEAM_CID }, new[] { AcquisitionVocabulary.ORBITRAP });
                StateDict.WriteSafetensors(network, partial, held.ToMetadata());
            }
            using (var model = Ms2Model.FromSafetensors(partial, CPU))
            {
                Assert.AreEqual(AcquisitionVocabulary.DEFAULT.ToString(), model.Vocabulary.ToString());
                var none = As(model, null, null);
                CollectionAssert.AreNotEqual(none, As(model, AcquisitionVocabulary.BEAM_CID, null), @"beam-CID kept its weights");
                CollectionAssert.AreEqual(none, As(model, AcquisitionVocabulary.RE_CID, AcquisitionVocabulary.ORBITRAP), @"Orbitrap's and reCID's are zero");
                CollectionAssert.AreEqual(none, As(model, null, AcquisitionVocabulary.LIT));
            }
        }

        [TestMethod]
        public void TestDevicesAndIrtCalibration()
        {
            // The CPU by default and on request; a GPU request on a machine without CUDA falls
            // back to the CPU and says so; anything else is an error.
            Assert.AreEqual(DeviceType.CPU, TorchDevice.Resolve(null, out string fallback).type);
            Assert.IsNull(fallback);
            Assert.AreEqual(DeviceType.CPU, TorchDevice.Resolve(@"CPU", out fallback).type);
            Assert.IsNull(fallback);
            var gpu = TorchDevice.Resolve(TorchDevice.GPU, out fallback);
            Assert.AreEqual(fallback != null, gpu.type == DeviceType.CPU);
            Assert.ThrowsException<ArgumentException>(() => TorchDevice.Resolve(@"tpu", out _));

            // The iRT map is the least-squares line from the model's predictions of the eleven
            // Biognosys kit peptides to their iRT values.
            var kit = new (string Sequence, double Irt)[]
            {
                (@"LGGNEQVTR", -24.92), (@"GAGSSEPVTGLDAK", 0.00), (@"VEATFGVDESNAK", 12.39), (@"YILAGVENSK", 19.79),
                (@"TPVISGGPYEYR", 28.71), (@"TPVITGAPYEYR", 33.38), (@"DGLDAASYYAPVR", 42.26), (@"ADVTPADFSEWSK", 54.62),
                (@"GTFIIDPGGVIR", 70.52), (@"GTFIIDPAAVIR", 87.23), (@"LFLQFGAQGSPFLK", 100.00),
            };
            using (var model = RtModel.FromSafetensors(RandomRtModel(@"irt_rt", 4), CPU))
            {
                var (slope, intercept) = model.FitIrtCalibration();
                double[] x = model.Predict(kit.Select(p => new PeptideForm(p.Sequence)).ToArray());
                double[] y = kit.Select(p => p.Irt).ToArray();
                double xMean = x.Average(), yMean = y.Average();
                double expectedSlope = x.Zip(y, (a, b) => (a - xMean) * (b - yMean)).Sum() / x.Sum(a => (a - xMean) * (a - xMean));
                Assert.AreEqual(expectedSlope, slope, 1e-9 * Math.Abs(expectedSlope));
                Assert.AreEqual(yMean - expectedSlope * xMean, intercept, 1e-9 * Math.Max(1, Math.Abs(intercept)));
                // Predictions are clipped at 0.
                Assert.IsTrue(x.All(v => v >= 0));
            }

            // numpy's random_interval of 0 is 0 without a draw, and above 2^32 draws 64 bits.
            var random = new NumpyRandomState(1);
            Assert.AreEqual(0L, random.RandomInterval(0));
            long wide = random.RandomInterval(1L << 40);
            Assert.IsTrue(wide >= 0 && wide <= 1L << 40);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => random.ChooseWithoutReplacement(3, 4));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => random.ChooseWithoutReplacement(3, -1));
            // A training row of the wrong width is refused.
            Assert.ThrowsException<ArgumentException>(() => new Ms2TrainingExample(new PrecursorForm(new PeptideForm(@"PEPTIDEK"), 2),
                30, @"Lumos", new double[4], new double[4]));
        }

        /// <summary>
        /// A training export of six clean precursors for <paramref name="run"/>, from an Astral run
        /// isolating 380-980 m/z, with the search hash and run q pass given (none when null).
        /// </summary>
        private string WriteCleanExport(string run, string searchHash = null, string runQPass = null)
        {
            var records = new List<OspreyTrainingRecord>();
            foreach (string sequence in MS2_PEPTIDES)
            {
                foreach (int charge in new[] { 2, 3 })
                {
                    var record = OspreyTestRecords.CleanRecord(sequence, charge);
                    record.EntryId = (uint)(records.Count + 1);
                    record.FileName = run;
                    record.RunPrecursorQ = 0.001;
                    record.ApexRt = 2 + records.Count;
                    records.Add(record);
                }
            }
            var footer = new Dictionary<string, string>
            {
                { @"osprey.training_export.format_version", OspreyTrainingExport.FORMAT_VERSION },
                { @"osprey.rt_max", @"12" },
                { @"osprey.instrument_model", @"Orbitrap Astral" },
                { @"osprey.isolation_mz_min", @"380" },
                { @"osprey.isolation_mz_max", @"980" },
                { @"osprey.ms2_scan_window", @"150,2000" },
            };
            if (searchHash != null)
                footer[OspreyTrainingExport.SEARCH_HASH_KEY] = searchHash;
            if (runQPass != null)
                footer[@"osprey.training_export.run_q_pass"] = runQPass;
            string export = Path.Combine(_folder, run + OspreyTrainingExport.FILE_SUFFIX);
            OspreyTestRecords.WriteExport(export, records, footer, 4);
            return export;
        }

        /// <summary>A randomly initialized MS2 model from <paramref name="seed"/>, saved under <paramref name="name"/>.</summary>
        private string RandomMs2Model(string name, int seed)
        {
            string path = Path.Combine(_folder, name + @".safetensors");
            manual_seed(seed);
            using (var network = new ModelMs2Bert())
                StateDict.WriteSafetensors(network, path);
            return path;
        }

        private string RandomRtModel(string name, int seed)
        {
            string path = Path.Combine(_folder, name + @".safetensors");
            manual_seed(seed);
            using (var network = new ModelRtLstmCnn())
                StateDict.WriteSafetensors(network, path);
            return path;
        }

        /// <summary>A short fine-tuning run from the given start models into its own folder.</summary>
        private FineTuneResult FineTune(string name, uint seed, string ms2Model, string rtModel)
        {
            var options = new FineTuneOptions
            {
                Seed = seed,
                Ms2Model = ms2Model,
                RtModel = rtModel,
                Ms2 = new FineTuneSettings { Epochs = 2, WarmupEpochs = 0, BatchSize = 4, LearningRate = 1e-3, AdjustBatchSize = false },
                Rt = new FineTuneSettings { Epochs = 2, WarmupEpochs = 0, BatchSize = 4, LearningRate = 1e-3, AdjustBatchSize = false },
            };
            return FineTuneRun.Run(RtRows(), Ms2Rows(), null, options, Path.Combine(_folder, name), null);
        }

        private static void AssertLossFalls(IReadOnlyList<EpochRecord> history, FineTuneSettings settings)
        {
            Assert.AreEqual(settings.Epochs, history.Count);
            CollectionAssert.AreEqual(Enumerable.Range(1, settings.Epochs).ToArray(), history.Select(e => e.Epoch).ToArray());
            string losses = string.Join(@" ", history.Select(e => e.TestLoss.ToString(@"F5", CultureInfo.InvariantCulture)));
            Assert.IsTrue(history.Last().TestLoss < history.First().TestLoss, losses);
            Assert.IsTrue(history.Last().TrainLoss < history.First().TrainLoss, losses);
            // The cosine schedule without warmup decays from the full rate.
            Assert.IsTrue(history.Last().LearningRate < history.First().LearningRate);
            Assert.IsTrue(history.First().LearningRate <= settings.LearningRate);
        }

        private static double[] Losses(IReadOnlyList<EpochRecord> history)
        {
            return history.SelectMany(e => new[] { e.TrainLoss, e.TestLoss }).ToArray();
        }

        private static float[] Predict(Ms2Model model, IEnumerable<Ms2TrainingExample> rows)
        {
            var requests = rows.Select(r => new Ms2Request(r.Precursor, r.Nce, r.Instrument, r.Activation, r.Analyzer)).ToArray();
            return model.Predict(requests).SelectMany(p => p.Intensities).ToArray();
        }

        /// <summary>
        /// Two lengths of peptide at charges 2 and 3: y ions rising along the ladder, weak b
        /// ions, charge 2 fragments only from the 3+ precursor, b1 masked.
        /// </summary>
        private static Ms2TrainingExample[] Ms2Rows(string instrument = @"Lumos", string activation = null, string analyzer = null)
        {
            var rows = new List<Ms2TrainingExample>();
            foreach (string sequence in MS2_PEPTIDES)
            {
                foreach (int charge in new[] { 2, 3 })
                {
                    int positions = sequence.Length - 1;
                    var intensities = new double[positions * Ms2TrainingExample.FRAGMENT_TYPES];
                    var invalid = new double[intensities.Length];
                    for (int row = 0; row < positions; row++)
                    {
                        intensities[row * Ms2TrainingExample.FRAGMENT_TYPES + AlphabaseFragmentMz.B_Z1] = 0.1;
                        intensities[row * Ms2TrainingExample.FRAGMENT_TYPES + AlphabaseFragmentMz.Y_Z1] = 0.2 + 0.8 * (row + 1) / positions;
                        if (charge == 3)
                            intensities[row * Ms2TrainingExample.FRAGMENT_TYPES + AlphabaseFragmentMz.Y_Z2] = 0.3;
                    }
                    invalid[AlphabaseFragmentMz.B_Z1] = 1;
                    rows.Add(new Ms2TrainingExample(new PrecursorForm(new PeptideForm(sequence), charge), 30, instrument, intensities, invalid,
                        activation, analyzer));
                }
            }
            return rows.ToArray();
        }

        private static RtTrainingExample[] RtRows()
        {
            return RT_PEPTIDES.Select(p => new RtTrainingExample(new PeptideForm(p.Sequence), p.RtNorm)).ToArray();
        }
    }
}
