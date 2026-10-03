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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using TorchSharp.Modules;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The Chronologer retention-time model against jchronologer's golden parity cases
    /// (src/test/resources/data/golden/chronologer_parity_cases.*.tsv at searlelab/jchronologer b64a23363d,
    /// produced by the Python Chronologer): the coded sequence of each accepted peptide exactly, its
    /// hydrophobic index to <see cref="HI_TOLERANCE"/>, and the rejected peptides. Also the conversion of
    /// CarafeSharp's alphabase-named forms to Chronologer's mass-annotated sequences, the iRT fit, and the
    /// pinned files.
    /// </summary>
    [TestClass]
    public class ChronologerModelTest
    {
        /// <summary>The golden values are float32 from Python on another machine.</summary>
        private const double HI_TOLERANCE = 2e-5;

        /// <summary>Mass-annotated sequence, coded sequence, hydrophobic index.</summary>
        private static readonly (string ModifiedSequence, string Coded, double Hi)[] GOLDEN_ACCEPTED =
        {
            (@"VATVSLPR", @"-VATVSLPR_", 8.394585609436035),
            (@"LAADDFR", @"-LAADDFR_", 6.878472328186035),
            (@"FLEQQNK", @"-FLEQQNK_", 3.7536282539367676),
            (@"LGEHNIDVLEGNEQFINAAK", @"-LGEHNIDVLEGNEQFINAAK_", 14.286270141601562),
            (@"SHC[+57.021464]IAEVENDEMPADLPSLAADFVESK", @"-SHcIAEVENDEMPADLPSLAADFVESK_", 19.93527603149414),
            (@"C[+57.021464]C[+57.021464]TESLVNR", @"-ccTESLVNR_", 5.078381538391113),
            (@"ETYGEMADC[+57.021464]C[+57.021464]AK", @"-ETYGEMADccAK_", 5.957462310791016),
            (@"MPC[+57.021464]AEDYLSVVLNQLC[+57.021464]VLHEK", @"-MPcAEDYLSVVLNQLcVLHEK_", 24.174541473388672),
            (@"M[+15.994915]PEPTIDEK", @"-mPEPTIDEK_", 4.6299147605896),
            (@"Q[-17.026549]NQEYQVLLDVR", @"(eNQEYQVLLDVR_", 17.283842086791992),
            (@"E[-18.0105647]ATTESTK", @"(eATTESTK_", 2.180934429168701),
            (@"C[+39.99491463]PEPTIDEK", @")dPEPTIDEK_", 8.86428165435791),
            (@"[+42.010565]ACDEFGHIK", @"^ACDEFGHIK_", 9.192350387573242),
            (@"ACDEFG", @"-ACDEFG_", 5.981589317321777),
            (@"S[+79.966331]PEPTIDEK", @"-sPEPTIDEK_", 5.557801246643066),
            (@"[+42.010565]KPEPTIDEK", @"^KPEPTIDEK_", 5.410096168518066),
            (@"R[+14.01565]PEPTIDEK", @"-qPEPTIDEK_", 5.6915154457092285),
            (@"K[+224.152478]PEPTIDEK", @"-zPEPTIDEK_", 4.773852348327637),
            (@"[+229.162932]PEPTIDEK", @"*PEPTIDEK_", 7.6735029220581055),
        };

        /// <summary>An N-terminal mass without its sign, an unknown modification, 5 and 51 residues.</summary>
        private static readonly string[] GOLDEN_REJECTED =
        {
            @"[42.010565]ACDEFGHIK",
            @"ACDE[+123.456]FGHIK",
            @"ACDEF",
            new string('A', 51),
        };

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestChronologerGoldenParity()
        {
            using (var model = ChronologerModel.FromFiles(ChronologerFiles.Open(), CPU))
            {
                foreach (var golden in GOLDEN_ACCEPTED)
                    Assert.AreEqual(golden.Coded, model.Encoding.ToCodedSequence(golden.ModifiedSequence), golden.ModifiedSequence);
                var tokens = GOLDEN_ACCEPTED.Select(g => model.Encoding.EncodeModifiedSequence(g.ModifiedSequence)).ToArray();
                double[] predicted = model.PredictTokens(tokens);
                for (int i = 0; i < GOLDEN_ACCEPTED.Length; i++)
                    Assert.AreEqual(GOLDEN_ACCEPTED[i].Hi, predicted[i], HI_TOLERANCE, GOLDEN_ACCEPTED[i].ModifiedSequence);

                foreach (string rejected in GOLDEN_REJECTED)
                    Assert.IsNull(model.Encoding.EncodeModifiedSequence(rejected), rejected);
                // A rejected peptide predicts NaN without disturbing the others in its batch.
                double[] mixed = model.PredictTokens(new[] { tokens[0], null, tokens[1] });
                Assert.AreEqual(GOLDEN_ACCEPTED[0].Hi, mixed[0], HI_TOLERANCE);
                Assert.IsTrue(double.IsNaN(mixed[1]));
                Assert.AreEqual(GOLDEN_ACCEPTED[1].Hi, mixed[2], HI_TOLERANCE);
            }
        }

        /// <summary>
        /// CarafeSharp's forms carry alphabase names and sites; they reach Chronologer as the mass-annotated
        /// sequences its rules match, and predict as the golden cases do.
        /// </summary>
        [TestMethod]
        public void TestChronologerPeptideForms()
        {
            var cases = new[]
            {
                (new PeptideForm(@"SHCIAEVENDEMPADLPSLAADFVESK", new[] { @"Carbamidomethyl@C" }, new[] { 3 }),
                    @"SHC[+57.021464]IAEVENDEMPADLPSLAADFVESK", 4),
                (new PeptideForm(@"MPEPTIDEK", new[] { @"Oxidation@M" }, new[] { 1 }), @"M[+15.994915]PEPTIDEK", 8),
                (new PeptideForm(@"QNQEYQVLLDVR", new[] { @"Gln->pyro-Glu@Q^Any_N-term" }, new[] { 0 }),
                    @"Q[-17.026549]NQEYQVLLDVR", 9),
                (new PeptideForm(@"ACDEFGHIK", new[] { @"Acetyl@Protein_N-term" }, new[] { 0 }), @"[+42.010565]ACDEFGHIK", 12),
                (new PeptideForm(@"SPEPTIDEK", new[] { @"Phospho@S" }, new[] { 1 }), @"S[+79.966331]PEPTIDEK", 14),
                // Two modifications at one position are one mass, as jchronologer sums them: N-terminal ammonia loss on
                // a carbamidomethyl-Cys, as a DIA-NN-style library lists them, is the cyclized Cys.
                (new PeptideForm(@"CPEPTIDEK", new[] { @"Ammonia-loss@C^Any_N-term", @"Carbamidomethyl@C" }, new[] { 0, 1 }),
                    @"C[+39.994915]PEPTIDEK", 11),
            };
            using (var model = ChronologerModel.FromFiles(ChronologerFiles.Open(), CPU))
            {
                foreach (var (form, modifiedSequence, _) in cases)
                    Assert.AreEqual(modifiedSequence, ChronologerEncoding.ToModifiedSequence(form), form.ToString());
                double[] predicted = model.Predict(cases.Select(c => c.Item1).ToArray());
                for (int i = 0; i < cases.Length; i++)
                    Assert.AreEqual(GOLDEN_ACCEPTED[cases[i].Item3].Hi, predicted[i], HI_TOLERANCE, cases[i].Item2);

                // Chronologer has no C-terminal modifications, and nothing for an unknown name.
                var amidated = new PeptideForm(@"PEPTIDEK", new[] { @"Amidated@Any_C-term" }, new[] { -1 });
                Assert.IsNull(ChronologerEncoding.ToModifiedSequence(amidated));
                Assert.IsTrue(double.IsNaN(model.Predict(new[] { amidated }).Single()));
            }
        }

        /// <summary>
        /// Library prediction's Chronologer: Chronologer's own value for a peptide it encodes, and for one it rejects
        /// the pretrained AlphaPeptDeep prediction, carried onto the hydrophobic-index scale so that on iRT it is
        /// AlphaPeptDeep's own iRT.
        /// </summary>
        [TestMethod]
        public void TestChronologerRtPredictorFallback()
        {
            var plain = new PeptideForm(@"VATVSLPR");
            var amidated = new PeptideForm(@"VATVSLPR", new[] { @"Amidated@Any_C-term" }, new[] { -1 });
            var pretrained = PretrainedModels.Open();
            using (var predictor = new ChronologerRtPredictor(ChronologerModel.FromFiles(ChronologerFiles.Open(), CPU),
                       RtModel.FromPretrained(pretrained, CPU)))
            using (var alphaPeptDeep = RtModel.FromPretrained(pretrained, CPU))
            {
                double[] predicted = predictor.Predict(new[] { plain, amidated });
                Assert.AreEqual(GOLDEN_ACCEPTED[0].Hi, predicted[0], HI_TOLERANCE);
                Assert.AreEqual(1, predictor.FallbackCount);
                var chronologerIrt = predictor.FitIrtCalibration();
                var alphaPeptDeepIrt = alphaPeptDeep.FitIrtCalibration();
                double fallback = alphaPeptDeep.Predict(new[] { amidated }).Single();
                Assert.AreEqual(alphaPeptDeepIrt.Slope * fallback + alphaPeptDeepIrt.Intercept,
                    chronologerIrt.Slope * predicted[1] + chronologerIrt.Intercept, 1e-6);
            }
        }

        /// <summary>
        /// Fine-tuning Chronologer: rescaled to normalized RT, it fits a target that is not a line of its hydrophobic
        /// index better after training, its BatchNorm statistics do not move, and it saves and reloads as a
        /// Chronologer that predicts normalized RT, which an AlphaPeptDeep model file is not taken for.
        /// </summary>
        [TestMethod]
        public void TestChronologerFineTune()
        {
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"ChronologerTune_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                manual_seed(5);
                var files = ChronologerFiles.Open();
                var random = new Random(5);
                const string residues = @"ACDEFGHIKLMNPQRSTVWY";
                var peptides = Enumerable.Range(0, 120).Select(_ =>
                        new PeptideForm(new string(Enumerable.Range(0, random.Next(7, 16)).Select(i => residues[random.Next(residues.Length)]).ToArray()) +
                                        (random.Next(2) == 0 ? @"K" : @"R")))
                    .ToArray();
                string tuned = Path.Combine(folder, ModelFiles.RT_SAFETENSORS);
                double[] tunedPrediction;
                using (var model = ChronologerModel.FromFiles(files, CPU))
                {
                    // A target that is not a line of the hydrophobic index: the linear rescaling leaves its curvature.
                    double[] hi = model.Predict(peptides);
                    var rows = peptides.Select((p, i) => new RtTrainingExample(p, 0.5 + 0.4 * Math.Tanh((hi[i] - 12) / 6))).ToArray();
                    double meanHi = hi.Average();
                    model.RescaleToNormalizedRt(0.4 / 6, 0.5 - 0.4 / 6 * meanHi);
                    Assert.IsTrue(model.PredictsNormalizedRt);
                    var encoded = ChronologerTrainingExample.Encode(model, rows);
                    Assert.AreEqual(rows.Length, encoded.Count);

                    var batchNorm = model.Network.modules().OfType<BatchNorm1d>().First();
                    float[] RunningMean() => (batchNorm.running_mean ?? throw new AssertFailedException(@"no running mean")).data<float>().ToArray();
                    float[] runningMean = RunningMean();
                    var before = RtMetrics.Evaluate(p => model.Predict(p), rows);
                    var settings = new FineTuneSettings { Epochs = 8, WarmupEpochs = 0, BatchSize = 16, LearningRate = 1e-3, AdjustBatchSize = false };
                    var history = ModelFineTuner.TrainChronologer(model, encoded, encoded, settings, 16, new NumpyRandomState(5), null);
                    Assert.IsTrue(history.Last().TrainLoss < history.First().TrainLoss,
                        string.Format(CultureInfo.InvariantCulture, @"loss {0} -> {1}", history.First().TrainLoss, history.Last().TrainLoss));
                    var after = RtMetrics.Evaluate(p => model.Predict(p), rows);
                    Assert.IsTrue(after.MedianAbsoluteError < before.MedianAbsoluteError,
                        string.Format(CultureInfo.InvariantCulture, @"RT MAE {0} -> {1}", before.MedianAbsoluteError, after.MedianAbsoluteError));
                    CollectionAssert.AreEqual(runningMean, RunningMean());
                    Assert.ThrowsException<InvalidOperationException>(() => model.RescaleToNormalizedRt(1, 0));

                    tunedPrediction = model.Predict(peptides);
                    model.Save(tuned);
                }
                Assert.IsTrue(ChronologerModel.IsChronologerFile(tuned));
                Assert.AreEqual(ChronologerFiles.VERSION, StateDict.ReadSafetensorsMetadata(tuned)[ChronologerModel.VERSION_KEY]);
                using (var reloaded = ChronologerModel.FromSafetensors(tuned, files, CPU))
                {
                    Assert.IsTrue(reloaded.PredictsNormalizedRt);
                    CollectionAssert.AreEqual(tunedPrediction, reloaded.Predict(peptides));

                    // A Chronologer fine-tuned from another version is refused: its encoding may differ.
                    string otherVersion = Path.Combine(folder, @"other_version.safetensors");
                    StateDict.WriteSafetensors(reloaded.Network, otherVersion, new Dictionary<string, string>
                    {
                        { ChronologerModel.RT_MODEL_KEY, @"chronologer" },
                        { ChronologerModel.RT_SCALE_KEY, @"normalized_rt" },
                        { ChronologerModel.VERSION_KEY, @"20990101000000" },
                    });
                    StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => ChronologerModel.FromSafetensors(otherVersion, files, CPU)).Message,
                        @"Chronologer 20990101000000");
                }

                // Normalized RT is clipped at 0, as AlphaPeptDeep's is, and the hydrophobic index is not.
                using (var model = ChronologerModel.FromFiles(files, CPU))
                {
                    double[] hi = model.Predict(peptides);
                    double median = hi.OrderBy(v => v).ElementAt(hi.Length / 2);
                    model.RescaleToNormalizedRt(1, -median);
                    double[] clipped = model.Predict(peptides);
                    for (int i = 0; i < peptides.Length; i++)
                        Assert.AreEqual(Math.Max(hi[i] - median, 0), clipped[i], 1e-4, peptides[i].Sequence);
                    Assert.IsTrue(clipped.Contains(0.0));
                }

                string alphaPeptDeep = Path.Combine(folder, @"alphapeptdeep_rt.safetensors");
                using (var rt = RtModel.FromPretrained(PretrainedModels.Open(), CPU))
                    rt.Save(alphaPeptDeep);
                Assert.IsFalse(ChronologerModel.IsChronologerFile(alphaPeptDeep));
                Assert.ThrowsException<InvalidDataException>(() => ChronologerModel.FromSafetensors(alphaPeptDeep, files, CPU));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>The iRT kit peptides map the hydrophobic index onto iRT with a positive slope.</summary>
        [TestMethod]
        public void TestChronologerIrtCalibration()
        {
            using (var model = ChronologerModel.FromFiles(ChronologerFiles.Open(), CPU))
            {
                var (slope, intercept) = model.FitIrtCalibration();
                Assert.IsTrue(slope > 0, @"slope " + slope);
                double[] hi = model.Predict(new[] { new PeptideForm(@"LGGNEQVTR"), new PeptideForm(@"LFLQFGAQGSPFLK") });
                Assert.AreEqual(-24.92, slope * hi[0] + intercept, 10);
                Assert.AreEqual(100.0, slope * hi[1] + intercept, 10);
            }
        }

        /// <summary>
        /// The build puts the committed files beside the test assembly and they match their pins; a missing
        /// file and a changed one are both refused.
        /// </summary>
        [TestMethod]
        public void TestChronologerFilesLocation()
        {
            string bundled = Path.Combine(AppContext.BaseDirectory, ChronologerFiles.BUNDLED_RELATIVE_DIRECTORY);
            var files = ChronologerFiles.Open(bundled);
            Assert.IsTrue(File.Exists(files.WeightsPath));
            Assert.IsTrue(File.Exists(files.EncodingPath));

            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Chronologer_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                Assert.ThrowsException<FileNotFoundException>(() => ChronologerFiles.Open(folder));
                File.Copy(files.WeightsPath, Path.Combine(folder, ChronologerFiles.WEIGHTS_FILE));
                File.WriteAllText(Path.Combine(folder, ChronologerFiles.ENCODING_FILE), @"{}");
                Assert.ThrowsException<InvalidDataException>(() => ChronologerFiles.Open(folder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
