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
using System.IO;
using System.Linq;
using pwiz.CarafeSharp.Core;
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
            return Create(weights, device, null);
        }

        /// <summary>
        /// Loads a PyTorch checkpoint, such as the <c>ms2_model.pt</c> Carafe writes after
        /// fine-tuning.
        /// </summary>
        public static Ms2Model FromPthFile(string path, Device device)
        {
            return Create(StateDict.ReadPthFile(path), device, null);
        }

        /// <summary>Loads a model saved with <see cref="Save"/>.</summary>
        public static Ms2Model FromSafetensors(string path, Device device)
        {
            return Create(StateDict.ReadSafetensors(path), device, AcquisitionVocabulary.FromMetadata(StateDict.ReadSafetensorsMetadata(path)));
        }

        /// <summary>Loads a <c>.safetensors</c> model, or else a PyTorch checkpoint.</summary>
        public static Ms2Model FromFile(string path, Device device)
        {
            return string.Equals(Path.GetExtension(path), @".safetensors", StringComparison.OrdinalIgnoreCase)
                ? FromSafetensors(path, device)
                : FromPthFile(path, device);
        }

        /// <param name="weights">The state dict.</param>
        /// <param name="device">Where the model runs.</param>
        /// <param name="stored">
        /// The activations and analyzers the weights' acquisition layer has columns for, as a
        /// CarafeSharp model's metadata records them, or null. Weights without the layer (peptdeep's
        /// pretrained model, a Carafe checkpoint) get it at zero; a stored list gains a zero column
        /// for each known value it lacks.
        /// </param>
        private static Ms2Model Create(IReadOnlyDictionary<string, Tensor> weights, Device device, AcquisitionVocabulary stored)
        {
            var source = new Dictionary<string, Tensor>(weights, StringComparer.Ordinal);
            AcquisitionVocabulary vocabulary;
            if (source.TryGetValue(MetaEmbedding.ACQUISITION_WEIGHT, out var acquisition))
            {
                // A layer without its list was written by a CarafeSharp network saved directly, in the default order.
                var held = stored ?? AcquisitionVocabulary.DEFAULT;
                if (acquisition.shape[1] != held.Width)
                {
                    throw new InvalidDataException(string.Format(@"The MS2 model's acquisition layer has {0} columns, but its list ({1}) has {2}.",
                        acquisition.shape[1], held, held.Width));
                }
                vocabulary = held.WithKnownValues();
                if (vocabulary.Width > held.Width)
                    source[MetaEmbedding.ACQUISITION_WEIGHT] = PlaceColumns(acquisition, held, vocabulary);
            }
            else
            {
                vocabulary = AcquisitionVocabulary.DEFAULT;
                source[MetaEmbedding.ACQUISITION_WEIGHT] = zeros(ModelMs2Bert.META_DIM - 1, vocabulary.Width);
            }
            var network = new ModelMs2Bert(acquisitionWidth: vocabulary.Width);
            StateDict.Load(network, source);
            foreach (var tensor in source.Values.Except(weights.Values))
                tensor.Dispose();
            foreach (var tensor in weights.Values)
                tensor.Dispose();
            network.to(device);
            network.eval();
            return new Ms2Model(network, device, vocabulary);
        }

        /// <summary>
        /// <paramref name="weight"/>'s columns, laid out by <paramref name="held"/>, moved to their
        /// names' columns in <paramref name="vocabulary"/>, every other column zero.
        /// </summary>
        private static Tensor PlaceColumns(Tensor weight, AcquisitionVocabulary held, AcquisitionVocabulary vocabulary)
        {
            var placed = zeros(weight.shape[0], vocabulary.Width, weight.dtype);
            using (no_grad())
            {
                for (int i = 0; i < held.Activations.Count; i++)
                    placed[TensorIndex.Colon, vocabulary.ActivationColumn(held.Activations[i])].copy_(weight[TensorIndex.Colon, i]);
                for (int i = 0; i < held.Analyzers.Count; i++)
                {
                    placed[TensorIndex.Colon, vocabulary.AnalyzerColumn(held.Analyzers[i])]
                        .copy_(weight[TensorIndex.Colon, held.Activations.Count + i]);
                }
            }
            return placed;
        }

        private Ms2Model(ModelMs2Bert network, Device device, AcquisitionVocabulary vocabulary)
        {
            Network = network;
            Device = device;
            Vocabulary = vocabulary;
        }

        /// <summary>The activations and analyzers the model has acquisition columns for.</summary>
        public AcquisitionVocabulary Vocabulary { get; }

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
            var meta = PeptdeepFeaturizer.MetaFeatures(batch, Vocabulary).to(Device);
            return Network.call(aa, mods, charges, nces, meta);
        }

        /// <summary>Saves the weights as safetensors, with the acquisition list in the metadata, readable by <see cref="FromSafetensors"/>.</summary>
        public void Save(string path)
        {
            StateDict.WriteSafetensors(Network, path, Vocabulary.ToMetadata());
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
