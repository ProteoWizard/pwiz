/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/models.py
 *   (pDeepModel), itself extracted from AlphaPeptDeep (https://github.com/MannLabs/alphapeptdeep),
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
using pwiz.CarafeSharp.Models.Modules;
using TorchSharp;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// The AlphaPeptDeep MS2 intensity model: loading, prediction, and the network itself for
    /// fine-tuning.
    /// </summary>
    public sealed class Ms2Model : IDisposable
    {
        public const int DEFAULT_BATCH_SIZE = 512;

        /// <summary>
        /// Loads the pretrained generic MS2 model. As in peptdeep's general mode, the four
        /// modloss columns are predicted as zeros.
        /// </summary>
        public static Ms2Model FromPretrained(PretrainedModels pretrained, Device device)
        {
            var weights = StateDict.ReadPthFromZip(pretrained.ZipPath, PretrainedModels.MS2_ENTRY);
            return Create(weights, device);
        }

        /// <summary>
        /// Loads a PyTorch checkpoint, such as the <c>ms2_model.pt</c> Carafe writes after
        /// fine-tuning.
        /// </summary>
        public static Ms2Model FromPthFile(string path, Device device)
        {
            return Create(StateDict.ReadPthFile(path), device);
        }

        /// <summary>Loads a model saved with <see cref="Save"/>.</summary>
        public static Ms2Model FromSafetensors(string path, Device device)
        {
            return Create(StateDict.ReadSafetensors(path), device);
        }

        private static Ms2Model Create(IReadOnlyDictionary<string, Tensor> weights, Device device)
        {
            var network = new ModelMs2Bert();
            StateDict.Load(network, weights);
            foreach (var tensor in weights.Values)
                tensor.Dispose();
            network.to(device);
            network.eval();
            return new Ms2Model(network, device);
        }

        private Ms2Model(ModelMs2Bert network, Device device)
        {
            Network = network;
            Device = device;
        }

        internal ModelMs2Bert Network { get; }

        public Device Device { get; }

        /// <summary>
        /// Predicts every request, batching by peptide length. Results are in request order.
        /// </summary>
        public IReadOnlyList<Ms2Prediction> Predict(IReadOnlyList<Ms2Request> requests, int batchSize = DEFAULT_BATCH_SIZE)
        {
            var results = new Ms2Prediction[requests.Count];
            Network.eval();
            using (no_grad())
            {
                foreach (int[] batch in LengthBatches.Split(requests, r => r.Precursor.Peptide.Length, batchSize))
                {
                    var batchRequests = batch.Select(i => requests[i]).ToArray();
                    float[][] intensities = PredictBatch(batchRequests);
                    for (int i = 0; i < batch.Length; i++)
                        results[batch[i]] = new Ms2Prediction(requests[batch[i]].Precursor, intensities[i]);
                }
            }
            return results;
        }

        /// <summary>
        /// Raw network output <c>[batch, nAA - 1, 8]</c> for one same-length batch, on
        /// <see cref="Device"/>, before normalization. Used by training and by the parity tests.
        /// </summary>
        internal Tensor Forward(IReadOnlyList<Ms2Request> batch)
        {
            var peptides = batch.Select(r => r.Precursor.Peptide).ToArray();
            var aa = PeptdeepFeaturizer.AaIndices(peptides).to(Device);
            var mods = PeptdeepFeaturizer.ModFeatures(peptides).to(Device);
            var charges = PeptdeepFeaturizer.Charges(batch.Select(r => r.Precursor.Charge).ToArray()).to(Device);
            var nces = PeptdeepFeaturizer.Nces(batch.Select(r => r.Nce).ToArray()).to(Device);
            var instruments = PeptdeepFeaturizer.InstrumentIndices(batch.Select(r => r.Instrument).ToArray()).to(Device);
            return Network.call(aa, mods, charges, nces, instruments);
        }

        /// <summary>Saves the weights as safetensors, readable by <see cref="FromSafetensors"/>.</summary>
        public void Save(string path)
        {
            StateDict.WriteSafetensors(Network, path);
        }

        public void Dispose()
        {
            Network.Dispose();
        }

        private float[][] PredictBatch(IReadOnlyList<Ms2Request> batch)
        {
            using (NewDisposeScope())
            {
                var raw = Forward(batch).cpu();
                long count = raw.shape[0];
                // peptdeep: divide each precursor by its maximum over all 8 columns (1 when that
                // maximum is not positive), then zero everything below 1e-4, negatives included.
                var apex = raw.reshape(count, -1).amax(new long[] { 1 });
                apex = apex.masked_fill(apex <= 0, 1);
                var normalized = raw / apex.view(count, 1, 1);
                normalized = normalized.masked_fill(normalized < PeptdeepConstants.MIN_INTENSITY, 0);
                float[] flat = normalized.contiguous().data<float>().ToArray();
                int perPrecursor = (int)(flat.Length / count);
                var result = new float[count][];
                for (int i = 0; i < count; i++)
                {
                    result[i] = new float[perPrecursor];
                    Array.Copy(flat, i * perPrecursor, result[i], 0, perPrecursor);
                }
                return result;
            }
        }
    }
}
