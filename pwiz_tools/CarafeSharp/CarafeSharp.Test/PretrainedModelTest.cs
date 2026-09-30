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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Models.Modules;
using pwiz.CarafeSharp.Training;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Model weights: saving and reloading them, the strict loader, fine-tuning from a given
    /// MS2 model (on randomly initialized models, so it always runs), and the real AlphaPeptDeep
    /// pretrained weights, whose test is inconclusive when the pinned
    /// <c>pretrained_models.zip</c> is not on the machine. Exact numeric parity with Python is
    /// covered by the reference-dump test.
    /// </summary>
    [TestClass]
    public class PretrainedModelTest
    {
        /// <summary>
        /// Against Carafe's Python run of the pyro-Glu peptide (on another device, printed to 8
        /// digits); CarafeSharp matches Carafe 2.2.0's predictions to about 1e-5.
        /// </summary>
        private const double PYRO_GLU_TOLERANCE = 2e-5;

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestModelCheckpoints()
        {
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Checkpoints_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                manual_seed(7);
                // A saved model reloads to identical predictions, and saves again to identical bytes.
                var requests = new[]
                {
                    new Ms2Request(new PrecursorForm(new PeptideForm(@"PEPTIDEK"), 2), 30, @"Lumos"),
                    new Ms2Request(new PrecursorForm(new PeptideForm(@"SAMPLERK"), 3), 27, @"QE"),
                };
                string random = Path.Combine(folder, @"random_ms2.safetensors");
                using (var network = new ModelMs2Bert())
                    StateDict.WriteSafetensors(network, random);
                string saved = Path.Combine(folder, ModelFiles.MS2_SAFETENSORS);
                float[] predicted;
                using (var model = Ms2Model.FromSafetensors(random, CPU))
                {
                    predicted = model.Predict(requests).SelectMany(p => p.Intensities).ToArray();
                    model.Save(saved);
                }
                string resaved = Path.Combine(folder, @"resaved_ms2.safetensors");
                using (var model = Ms2Model.FromSafetensors(saved, CPU))
                {
                    CollectionAssert.AreEqual(predicted, model.Predict(requests).SelectMany(p => p.Intensities).ToArray());
                    model.Save(resaved);
                }
                CollectionAssert.AreEqual(File.ReadAllBytes(saved), File.ReadAllBytes(resaved));

                var peptides = new[] { new PeptideForm(@"PEPTIDEK"), new PeptideForm(@"LGGNEQVTR") };
                string randomRt = Path.Combine(folder, @"random_rt.safetensors");
                using (var network = new ModelRtLstmCnn())
                    StateDict.WriteSafetensors(network, randomRt);
                string savedRt = Path.Combine(folder, ModelFiles.RT_SAFETENSORS);
                double[] rt;
                using (var model = RtModel.FromSafetensors(randomRt, CPU))
                {
                    rt = model.Predict(peptides);
                    model.Save(savedRt);
                }
                using (var model = RtModel.FromSafetensors(savedRt, CPU))
                    CollectionAssert.AreEqual(rt, model.Predict(peptides));

                // The loader rejects a checkpoint with a key missing, an extra key, or a tensor of another shape.
                var weights = StateDict.ReadSafetensors(saved);
                string key = weights.Keys.First();
                using (var network = new ModelMs2Bert())
                {
                    var missing = weights.Where(p => p.Key != key).ToDictionary(p => p.Key, p => p.Value);
                    Assert.ThrowsException<InvalidDataException>(() => StateDict.Load(network, missing));
                    var extra = new Dictionary<string, Tensor>(weights) { { @"extra.weight", zeros(1) } };
                    Assert.ThrowsException<InvalidDataException>(() => StateDict.Load(network, extra));
                    var reshaped = new Dictionary<string, Tensor>(weights) { [key] = zeros(weights[key].numel() + 1) };
                    Assert.ThrowsException<InvalidDataException>(() => StateDict.Load(network, reshaped));
                    StateDict.Load(network, weights);
                }

                // -ms2_model: fine-tuning starts from that model, which is also the baseline the
                // fine-tuned model must beat. With one sequence every row is a test row.
                var rows = Enumerable.Range(0, 20).Select(TrainingRow).ToArray();
                var options = new FineTuneOptions
                {
                    Ms2Model = saved,
                    Ms2 = new FineTuneSettings { Epochs = 1, WarmupEpochs = 0, BatchSize = 8, LearningRate = 1e-4 },
                };
                var result = FineTuneRun.Run(null, rows, null, options, Path.Combine(folder, @"tuned"), null);
                using (var start = Ms2Model.FromSafetensors(saved, CPU))
                {
                    var baseline = Ms2Metrics.Evaluate(start, rows);
                    Assert.AreEqual(baseline.Pcc, result.Ms2Pretrained.Pcc, 1e-9);
                    Assert.AreEqual(baseline.Cos, result.Ms2Pretrained.Cos, 1e-9);
                    Assert.AreEqual(baseline.Sa, result.Ms2Pretrained.Sa, 1e-9);
                    Assert.AreEqual(baseline.Spc, result.Ms2Pretrained.Spc, 1e-9);
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// Where the pretrained archive is found: a set environment variable wins even when it names a
        /// missing file, then the committed copy beside the executable, then peptdeep's folder. The build
        /// must put the committed copy in this test's output, and it must match the pin.
        /// </summary>
        [TestMethod]
        public void TestPretrainedArchiveLocation()
        {
            string bundled = Path.Combine(AppContext.BaseDirectory, PretrainedModels.BUNDLED_RELATIVE_PATH);
            Assert.IsTrue(File.Exists(bundled), @"The build did not copy the committed archive to " + bundled);
            Assert.AreEqual(PretrainedModels.PINNED_SHA256, PretrainedModels.Open(bundled).Sha256);

            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Pretrained_" + Guid.NewGuid().ToString(@"N"));
            string baseDirectory = Path.Combine(folder, @"bin");
            string profile = Path.Combine(folder, @"home");
            Directory.CreateDirectory(baseDirectory);
            try
            {
                string peptdeep = Path.Combine(profile, @"peptdeep", @"pretrained_models", @"pretrained_models.zip");
                Assert.AreEqual(peptdeep, PretrainedModels.ResolveDefaultPath(null, baseDirectory, profile));

                string beside = Path.Combine(baseDirectory, PretrainedModels.BUNDLED_RELATIVE_PATH);
                Directory.CreateDirectory(Path.GetDirectoryName(beside) ?? baseDirectory);
                File.WriteAllBytes(beside, new byte[] { 0 });
                Assert.AreEqual(beside, PretrainedModels.ResolveDefaultPath(string.Empty, baseDirectory, profile));
                // Any other archive is refused, wherever it was found.
                Assert.ThrowsException<InvalidDataException>(() => PretrainedModels.Open(beside));

                string missing = Path.Combine(folder, @"missing.zip");
                Assert.AreEqual(missing, PretrainedModels.ResolveDefaultPath(missing, baseDirectory, profile));
                Assert.ThrowsException<FileNotFoundException>(() => PretrainedModels.Open(missing));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void TestPretrainedModelsPredict()
        {
            var pretrained = PretrainedModels.Open();

            using (var rt = RtModel.FromPretrained(pretrained, CPU))
            {
                // The iRT kit peptides are ordered by iRT, so a working model ranks them in order.
                var (slope, _) = rt.FitIrtCalibration();
                Assert.IsTrue(slope > 0, @"iRT slope " + slope);
                double[] predicted = rt.Predict(new[]
                {
                    new PeptideForm(@"LGGNEQVTR"), new PeptideForm(@"YILAGVENSK"),
                    new PeptideForm(@"ADVTPADFSEWSK"), new PeptideForm(@"LFLQFGAQGSPFLK"),
                });
                for (int i = 1; i < predicted.Length; i++)
                    Assert.IsTrue(predicted[i] > predicted[i - 1], @"RT order at " + i);
            }

            using (var ms2 = Ms2Model.FromPretrained(pretrained, CPU))
            {
                var precursor = new PrecursorForm(new PeptideForm(@"LGGNEQVTR"), 2);
                var prediction = ms2.Predict(new[] { new Ms2Request(precursor, 30, @"Lumos") }).Single();
                Assert.AreEqual(8, prediction.RowCount);
                Assert.AreEqual(1f, prediction.Intensities.Max());
                Assert.IsTrue(prediction.Intensities.All(v => v >= 0));
                // General mode: the four modloss columns are exactly zero.
                for (int row = 0; row < prediction.RowCount; row++)
                {
                    for (int col = 4; col < 8; col++)
                        Assert.AreEqual(0f, prediction.Get(row, col));
                }
                // A tryptic peptide fragments mostly to singly charged y ions.
                float ySum = Enumerable.Range(0, prediction.RowCount).Sum(r => prediction.Get(r, 2));
                float bSum = Enumerable.Range(0, prediction.RowCount).Sum(r => prediction.Get(r, 0));
                Assert.IsTrue(ySum > bSum, string.Format(@"y {0} vs b {1}", ySum, bSum));
            }
        }

        [TestMethod]
        public void TestPyroGluPrediction()
        {
            // Carafe 2.2.0's own Python (peptdeep 1.1.0, generic models) given the alphabase name it
            // fails to pass itself: QPEPTIDEK with pyro-Glu, 2+, Lumos, NCE 27.
            var pretrained = PretrainedModels.Open();
            var pyroGlu = new PeptideForm(@"QPEPTIDEK", new[] { @"Gln->pyro-Glu@Q^Any_N-term" }, new[] { 0 });
            using (var rt = RtModel.FromPretrained(pretrained, CPU))
            {
                double[] predicted = rt.Predict(new[] { pyroGlu, new PeptideForm(@"QPEPTIDEK") });
                Assert.AreEqual(0.13824185729026794, predicted[0], PYRO_GLU_TOLERANCE);
                // The modification reaches the model: an all-zero feature would predict the unmodified RT.
                Assert.AreEqual(0.14672058820724487, predicted[1], PYRO_GLU_TOLERANCE);
            }
            using (var ms2 = Ms2Model.FromPretrained(pretrained, CPU))
            {
                var prediction = ms2.Predict(new[] { new Ms2Request(new PrecursorForm(pyroGlu, 2), 27, @"Lumos") }).Single();
                float[] bZ1 = { 0, 0.006442173f, 0.007982196f, 0, 0, 0, 0, 0.0028883736f };
                float[] yZ1 = { 0.011913588f, 0.21940427f, 1f, 0.2522991f, 0.22718553f, 0.446979f, 0.56243664f, 0.23292564f };
                for (int row = 0; row < prediction.RowCount; row++)
                {
                    Assert.AreEqual(bZ1[row], prediction.Get(row, 0), PYRO_GLU_TOLERANCE, @"b_z1 row " + row);
                    Assert.AreEqual(yZ1[row], prediction.Get(row, 2), PYRO_GLU_TOLERANCE, @"y_z1 row " + row);
                }
                Assert.AreEqual(0.051533796f, prediction.Get(0, 3), PYRO_GLU_TOLERANCE);
                Assert.AreEqual(0.6012333f, prediction.Get(2, 3), PYRO_GLU_TOLERANCE);
            }
        }

        /// <summary>A PEPTIDEK 2+ training spectrum, its intensities varying with <paramref name="index"/>.</summary>
        private static Ms2TrainingExample TrainingRow(int index)
        {
            var precursor = new PrecursorForm(new PeptideForm(@"PEPTIDEK"), 2);
            int values = (precursor.Peptide.Length - 1) * Ms2TrainingExample.FRAGMENT_TYPES;
            var intensities = Enumerable.Range(0, values).Select(s => (s * 7 + index) % 11 / 10.0).ToArray();
            return new Ms2TrainingExample(precursor, 30, @"Lumos", intensities, new double[values]);
        }
    }
}
