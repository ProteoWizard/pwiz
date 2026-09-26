/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/models.py
 *   (AlphaRTModel), itself extracted from AlphaPeptDeep (https://github.com/MannLabs/alphapeptdeep),
 *   Apache-2.0
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
using System.Linq;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models.Modules;
using TorchSharp;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// The AlphaPeptDeep retention time model. Predictions are normalized retention times
    /// (the fraction of the training gradient, <c>rt / rt_max</c>), clipped at 0. Charge does
    /// not enter the model, so callers predict each peptide form once.
    /// </summary>
    public sealed class RtModel : IDisposable
    {
        public const int DEFAULT_BATCH_SIZE = 1024;

        /// <summary>
        /// The Biognosys iRT kit peptides and their iRT values, which peptdeep predicts to map
        /// normalized RT onto the iRT scale.
        /// </summary>
        private static readonly (string Sequence, double Irt)[] IRT_PEPTIDES =
        {
            (@"LGGNEQVTR", -24.92),
            (@"GAGSSEPVTGLDAK", 0.00),
            (@"VEATFGVDESNAK", 12.39),
            (@"YILAGVENSK", 19.79),
            (@"TPVISGGPYEYR", 28.71),
            (@"TPVITGAPYEYR", 33.38),
            (@"DGLDAASYYAPVR", 42.26),
            (@"ADVTPADFSEWSK", 54.62),
            (@"GTFIIDPGGVIR", 70.52),
            (@"GTFIIDPAAVIR", 87.23),
            (@"LFLQFGAQGSPFLK", 100.00),
        };

        public static RtModel FromPretrained(PretrainedModels pretrained, Device device)
        {
            return Create(StateDict.ReadPthFromZip(pretrained.ZipPath, PretrainedModels.RT_ENTRY), device);
        }

        /// <summary>Loads a PyTorch checkpoint, such as Carafe's fine-tuned <c>rt_model.pt</c>.</summary>
        public static RtModel FromPthFile(string path, Device device)
        {
            return Create(StateDict.ReadPthFile(path), device);
        }

        public static RtModel FromSafetensors(string path, Device device)
        {
            return Create(StateDict.ReadSafetensors(path), device);
        }

        private static RtModel Create(IReadOnlyDictionary<string, Tensor> weights, Device device)
        {
            var network = new ModelRtLstmCnn();
            StateDict.Load(network, weights);
            foreach (var tensor in weights.Values)
                tensor.Dispose();
            network.to(device);
            network.eval();
            return new RtModel(network, device);
        }

        private RtModel(ModelRtLstmCnn network, Device device)
        {
            Network = network;
            Device = device;
        }

        internal ModelRtLstmCnn Network { get; }

        public Device Device { get; }

        /// <summary>Normalized retention times, in input order.</summary>
        public double[] Predict(IReadOnlyList<PeptideForm> peptides, int batchSize = DEFAULT_BATCH_SIZE)
        {
            var results = new double[peptides.Count];
            Network.eval();
            using (no_grad())
            {
                foreach (int[] batch in LengthBatches.Split(peptides, p => p.Length, batchSize))
                {
                    using (NewDisposeScope())
                    {
                        var output = Forward(batch.Select(i => peptides[i]).ToArray()).cpu();
                        float[] values = output.data<float>().ToArray();
                        for (int i = 0; i < batch.Length; i++)
                            results[batch[i]] = Math.Max(values[i], 0f);
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// The linear map from this model's normalized RT to the iRT scale, fitted by predicting
        /// the eleven iRT kit peptides (peptdeep's <c>add_irt_column_to_precursor_df</c>).
        /// </summary>
        public (double Slope, double Intercept) FitIrtCalibration()
        {
            var peptides = IRT_PEPTIDES.Select(p => new PeptideForm(p.Sequence)).ToArray();
            double[] predicted = Predict(peptides);
            double predictedMean = predicted.Average();
            double irtMean = IRT_PEPTIDES.Average(p => p.Irt);
            double sxy = 0, sxx = 0;
            for (int i = 0; i < predicted.Length; i++)
            {
                double x = predicted[i] - predictedMean;
                double y = IRT_PEPTIDES[i].Irt - irtMean;
                sxy += x * y;
                sxx += x * x;
            }
            double slope = sxy / sxx;
            return (slope, irtMean - slope * predictedMean);
        }

        /// <summary>Raw network output <c>[batch]</c> for one same-length batch, on <see cref="Device"/>.</summary>
        internal Tensor Forward(IReadOnlyList<PeptideForm> batch)
        {
            var aa = PeptdeepFeaturizer.AaIndices(batch).to(Device);
            var mods = PeptdeepFeaturizer.ModFeatures(batch).to(Device);
            return Network.call(aa, mods);
        }

        public void Save(string path)
        {
            StateDict.WriteSafetensors(Network, path);
        }

        public void Dispose()
        {
            Network.Dispose();
        }
    }
}
