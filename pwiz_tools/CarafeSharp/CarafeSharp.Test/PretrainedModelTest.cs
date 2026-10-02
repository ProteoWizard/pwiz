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
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Models.Modules;
using pwiz.CarafeSharp.Proteome;
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

        /// <summary>
        /// Against Carafe's Python CCS predictions (square angstroms, float32 printed in full). They
        /// agree to the bit on the CPU that wrote them; the tolerance leaves room for another CPU's
        /// float32 kernels, about 33 ulps of a 500 A^2 CCS.
        /// </summary>
        private const double CCS_TOLERANCE = 1e-3;

        /// <summary>
        /// The 1/K0 that <see cref="CCS_TOLERANCE"/> allows: 1/K0 per A^2 of CCS is at most about
        /// 0.005, for a singly charged precursor.
        /// </summary>
        private const double INVERSE_K0_TOLERANCE = 6e-6;

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

        /// <summary>
        /// The pretrained CCS model and the timsTOF 1/K0 it converts to, against Carafe 2.2.0's own
        /// Python (py/v2/models.py <c>predict_mobility</c>, generic/ccs.pth, CPU): charges 1 to 4,
        /// lengths 7 to 30, and modifications on the N-term, the C-term and residues. The same
        /// weights in a model folder as Carafe's fine-tuned <c>ccs_model.pt</c> predict the same.
        /// </summary>
        [TestMethod]
        public void TestCcsPrediction()
        {
            // Sequence, mods, sites, charge, then Python's precursor_mz, ccs_pred and mobility_pred.
            var references = new (string Sequence, string Mods, string Sites, int Charge, double PrecursorMz, double Ccs, double InverseK0)[]
            {
                (@"LGGNEQVTR", @"", @"", 2, 487.256705240605, 331.27978515625, 0.8155331505902977),
                (@"LGGNEQVTR", @"", @"", 3, 325.17356231607, 382.4161376953125, 0.6276216232412947),
                (@"GAGSSEPVTGLDAK", @"", @"", 2, 644.82260624973, 382.269775390625, 0.9442863487584952),
                (@"VEATFGVDESNAK", @"", @"", 2, 683.827888591745, 394.2088623046875, 0.9743690844310147),
                (@"YILAGVENSK", @"", @"", 1, 1093.58880101168, 262.2637939453125, 1.293231871170388),
                (@"YILAGVENSK", @"", @"", 2, 547.29803873934, 364.82806396484375, 0.8995002555346188),
                (@"TPVISGGPYEYR", @"", @"", 2, 669.838059352605, 394.31756591796875, 0.9744336416284588),
                (@"TPVISGGPYEYR", @"", @"", 3, 446.89446505740335, 451.3885803222656, 0.7436503619094422),
                (@"DGLDAASYYAPVR", @"", @"", 2, 699.3384234905051, 399.73651123046875, 0.9882517066554944),
                (@"ADVTPADFSEWSK", @"", @"", 3, 484.8929012383167, 465.7850341796875, 0.7679839713431659),
                (@"GTFIIDPGGVIR", @"", @"", 2, 622.85351245548, 379.4434509277344, 0.9369536667861403),
                (@"LFLQFGAQGSPFLK", @"", @"", 2, 776.92975140178, 435.544921875, 1.0778361455168644),
                (@"LFLQFGAQGSPFLK", @"", @"", 3, 518.28892642352, 500.3923034667969, 0.8255466915651517),
                (@"LFLQFGAQGSPFLK", @"", @"", 4, 388.96851393439, 580.3511352539062, 0.7181011275060851),
                (@"PEPTIDE", @"", @"", 1, 800.36724049975, 256.2716979980469, 1.2579451514438),
                (@"ACDEFGHIK", @"Carbamidomethyl@C", @"2", 2, 538.7451163417049, 356.1441345214844, 0.8779158325174755),
                (@"PEPTMIDEK", @"Oxidation@M", @"5", 2, 538.2524398449899, 356.4376220703125, 0.878629111463515),
                (@"MCQEHMK", @"Oxidation@M;Carbamidomethyl@C", @"1;2", 2, 490.1933482815149, 333.2208557128906, 0.8203802476192024),
                (@"SAMPLER", @"Acetyl@Protein_N-term", @"0", 2, 423.21292076066993, 308.4308776855469, 0.7576849900902468),
                (@"SAMPLESR", @"Phospho@S", @"1", 2, 485.706818065695, 326.15277099609375, 0.8028758670614909),
                (@"PEPTIDEK", @"Amidated@Any_C-term", @"-1", 2, 464.24273219951493, 326.0474548339844, 0.8020972931476827),
                (@"VLSIGDGIARVHGLRNVQAEEMVEFSSGLK", @"", @"", 3, 1071.2345868210803, 687.3775634765625, 1.1392478570427942),
                (@"VLSIGDGIARVHGLRNVQAEEMVEFSSGLK", @"", @"", 4, 803.6777592325601, 807.8851318359375, 1.004232643978344),
            };
            var precursors = references.Select(r => new PrecursorForm(PeptideForm.FromAlphabase(r.Sequence, r.Mods, r.Sites), r.Charge))
                .ToArray();
            var pretrained = PretrainedModels.Open();
            double[] ccs;
            using (var model = CcsModel.FromPretrained(pretrained, CPU))
                ccs = model.Predict(precursors);
            for (int i = 0; i < references.Length; i++)
            {
                var reference = references[i];
                string label = precursors[i].ToString();
                Assert.AreEqual(reference.PrecursorMz, AlphabaseFragmentMz.CalculatePrecursorMz(precursors[i]), 1e-9, label);
                Assert.AreEqual(reference.Ccs, ccs[i], CCS_TOLERANCE, label);
                Assert.AreEqual(reference.InverseK0, TimsMobility.CcsToInverseK0(ccs[i], precursors[i]), INVERSE_K0_TOLERANCE, label);
                // The conversion alone, from Python's own CCS and m/z, is alphabase's to the last bits.
                Assert.AreEqual(reference.InverseK0, TimsMobility.CcsToInverseK0(reference.Ccs, reference.PrecursorMz, reference.Charge),
                    1e-15, label);
            }

            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Ccs_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                Assert.IsNull(CarafeModelDirectory.Open(folder).GetCcsModelPath(@"all"));
                string checkpoint = Path.Combine(folder, CarafeModelDirectory.CCS_MODEL_FILE);
                ExtractPretrainedEntry(pretrained, PretrainedModels.CCS_ENTRY, checkpoint);
                var directory = CarafeModelDirectory.Open(folder);
                Assert.AreEqual(checkpoint, directory.GetCcsModelPath(@"all"));
                // Carafe's Python takes the folder's CCS model for --tf_type all only, the generic one otherwise.
                Assert.IsNull(directory.GetCcsModelPath(@"nce"));
                using (var model = CcsModel.FromPthFile(checkpoint, CPU))
                {
                    CollectionAssert.AreEqual(ccs, model.Predict(precursors));
                    // One precursor at a time: batches hold one length, so each prediction is its own.
                    for (int i = 0; i < precursors.Length; i++)
                        Assert.AreEqual(ccs[i], model.Predict(new[] { precursors[i] })[0], CCS_TOLERANCE, precursors[i].ToString());
                }

                // A model whose output is negative predicts 0, as Carafe clips every prediction.
                using (var network = new ModelCcsLstm())
                {
                    var weights = network.state_dict().ToDictionary(p => p.Key, p => p.Value.detach().clone());
                    weights[@"ccs_decoder.nn.2.bias"].fill_(-1e6);
                    using (var negative = CcsModel.Create(weights, CPU))
                        Assert.AreEqual(0.0, negative.Predict(new[] { precursors[0] })[0]);
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>Writes the pretrained archive's <paramref name="entry"/> to <paramref name="path"/>, as a model folder holds it.</summary>
        internal static void ExtractPretrainedEntry(PretrainedModels pretrained, string entry, string path)
        {
            using (var zip = ZipFile.OpenRead(pretrained.ZipPath))
            {
                var member = zip.GetEntry(entry);
                Assert.IsNotNull(member, entry);
                member.ExtractToFile(path);
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
