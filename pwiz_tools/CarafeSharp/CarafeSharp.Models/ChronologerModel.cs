/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Chronologer (https://github.com/searlelab/chronologer) src/chronologer/predict.py, Apache-2.0
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
    /// The Chronologer retention-time model (Searle lab). Predictions are a hydrophobic index (HI), a scale of
    /// its own that tracks elution to the end of the gradient, where AlphaPeptDeep's generic model plateaus;
    /// <see cref="FitIrtCalibration"/> maps it onto iRT. Charge does not enter the model. A peptide the
    /// encoding rejects (<see cref="ChronologerEncoding"/>) predicts NaN.
    /// </summary>
    public sealed class ChronologerModel : IDisposable
    {
        public const int DEFAULT_BATCH_SIZE = 2048;

        public static ChronologerModel FromFiles(ChronologerFiles files, Device device)
        {
            var weights = StateDict.ReadPthFile(files.WeightsPath);
            var network = new ModelChronologer();
            StateDict.Load(network, weights);
            foreach (var tensor in weights.Values)
                tensor.Dispose();
            network.to(device);
            network.eval();
            return new ChronologerModel(network, ChronologerEncoding.Load(files.EncodingPath), device);
        }

        private ChronologerModel(ModelChronologer network, ChronologerEncoding encoding, Device device)
        {
            Network = network;
            Encoding = encoding;
            Device = device;
        }

        internal ModelChronologer Network { get; }

        public ChronologerEncoding Encoding { get; }

        public Device Device { get; }

        /// <summary>Hydrophobic indexes in input order; NaN for a peptide the encoding rejects.</summary>
        public double[] Predict(IReadOnlyList<PeptideForm> peptides, int batchSize = DEFAULT_BATCH_SIZE)
        {
            return PredictTokens(peptides.Select(Encoding.Encode).ToArray(), batchSize);
        }

        /// <summary>
        /// The linear map from the hydrophobic index to the iRT scale, fitted by predicting the eleven iRT
        /// kit peptides, as for the AlphaPeptDeep model.
        /// </summary>
        public (double Slope, double Intercept) FitIrtCalibration()
        {
            return IrtKit.FitCalibration(peptides => Predict(peptides));
        }

        /// <summary>Predictions for encoded peptides (null entries predict NaN), in input order.</summary>
        internal double[] PredictTokens(IReadOnlyList<long[]> tokens, int batchSize = DEFAULT_BATCH_SIZE)
        {
            var results = Enumerable.Repeat(double.NaN, tokens.Count).ToArray();
            int[] accepted = Enumerable.Range(0, tokens.Count).Where(i => tokens[i] != null).ToArray();
            Network.eval();
            using (no_grad())
            {
                for (int start = 0; start < accepted.Length; start += batchSize)
                {
                    using (NewDisposeScope())
                    {
                        int count = Math.Min(batchSize, accepted.Length - start);
                        var flat = new long[count * ModelChronologer.VECTOR_LENGTH];
                        for (int i = 0; i < count; i++)
                            Array.Copy(tokens[accepted[start + i]], 0, flat, i * ModelChronologer.VECTOR_LENGTH, ModelChronologer.VECTOR_LENGTH);
                        var input = tensor(flat, new long[] { count, ModelChronologer.VECTOR_LENGTH }).to(Device);
                        float[] values = Network.call(input).cpu().data<float>().ToArray();
                        for (int i = 0; i < count; i++)
                            results[accepted[start + i]] = values[i];
                    }
                }
            }
            return results;
        }

        public void Dispose()
        {
            Network.Dispose();
        }
    }
}
