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
using System.IO;
using System.Linq;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models.Modules;
using TorchSharp;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// The Chronologer retention-time model (Searle lab). The pretrained model predicts a hydrophobic index
    /// (HI), a scale of its own that tracks elution to the end of the gradient, where AlphaPeptDeep's generic
    /// model plateaus; <see cref="FitIrtCalibration"/> maps it onto iRT. A fine-tuned model predicts the
    /// training run's normalized RT (<see cref="PredictsNormalizedRt"/>), as a fine-tuned AlphaPeptDeep model
    /// does, clipped at 0 as AlphaPeptDeep's are. Charge does not enter the model. A peptide the encoding rejects
    /// (<see cref="ChronologerEncoding"/>) predicts NaN.
    /// </summary>
    public sealed class ChronologerModel : IDisposable
    {
        public const int DEFAULT_BATCH_SIZE = 2048;

        /// <summary>The safetensors metadata key a saved Chronologer carries, telling it from an AlphaPeptDeep RT model.</summary>
        public const string RT_MODEL_KEY = @"carafesharp.rt_model";
        /// <summary>The safetensors metadata key naming the scale a saved Chronologer predicts.</summary>
        public const string RT_SCALE_KEY = @"carafesharp.rt_scale";
        /// <summary>
        /// The safetensors metadata key naming the Chronologer (<see cref="ChronologerFiles.VERSION"/>) a saved model
        /// was fine-tuned from, whose encoding it needs. A file without it was saved before the key, from 20220601193755.
        /// </summary>
        public const string VERSION_KEY = @"carafesharp.chronologer_version";

        private const string MODEL_NAME = @"chronologer";
        private const string SCALE_HYDROPHOBIC_INDEX = @"hydrophobic_index";
        private const string SCALE_NORMALIZED_RT = @"normalized_rt";

        /// <summary>The pretrained Chronologer.</summary>
        public static ChronologerModel FromFiles(ChronologerFiles files, Device device)
        {
            return Create(StateDict.ReadPthFile(files.WeightsPath), files, false, device);
        }

        /// <summary>
        /// A Chronologer saved by <see cref="Save"/>, with the pinned encoding of <paramref name="files"/>, which must be
        /// the Chronologer it was fine-tuned from.
        /// </summary>
        public static ChronologerModel FromSafetensors(string path, ChronologerFiles files, Device device)
        {
            var metadata = StateDict.ReadSafetensorsMetadata(path);
            if (!IsChronologer(metadata))
                throw new InvalidDataException(path + @" is not a saved Chronologer model.");
            if (metadata.TryGetValue(VERSION_KEY, out string version) && version != ChronologerFiles.VERSION)
            {
                throw new InvalidDataException(string.Format(@"{0} was fine-tuned from Chronologer {1}; this CarafeSharp has Chronologer {2}.",
                    path, version, ChronologerFiles.VERSION));
            }
            bool normalized = metadata.TryGetValue(RT_SCALE_KEY, out string scale) && scale == SCALE_NORMALIZED_RT;
            return Create(StateDict.ReadSafetensors(path), files, normalized, device);
        }

        /// <summary>Whether a safetensors RT model file is a saved Chronologer rather than an AlphaPeptDeep model.</summary>
        public static bool IsChronologerFile(string path)
        {
            return IsChronologer(StateDict.ReadSafetensorsMetadata(path));
        }

        private static bool IsChronologer(IReadOnlyDictionary<string, string> metadata)
        {
            return metadata.TryGetValue(RT_MODEL_KEY, out string model) && model == MODEL_NAME;
        }

        private static ChronologerModel Create(IReadOnlyDictionary<string, Tensor> weights, ChronologerFiles files, bool normalized,
            Device device)
        {
            ModelChronologer network = null;
            try
            {
                var encoding = ChronologerEncoding.Load(files.EncodingPath);
                network = new ModelChronologer();
                StateDict.Load(network, weights);
                network.to(device);
                network.eval();
                return new ChronologerModel(network, encoding, normalized, device);
            }
            catch
            {
                network?.Dispose();
                throw;
            }
            finally
            {
                foreach (var tensor in weights.Values)
                    tensor.Dispose();
            }
        }

        private ChronologerModel(ModelChronologer network, ChronologerEncoding encoding, bool normalized, Device device)
        {
            Network = network;
            Encoding = encoding;
            PredictsNormalizedRt = normalized;
            Device = device;
        }

        internal ModelChronologer Network { get; }

        public ChronologerEncoding Encoding { get; }

        /// <summary>True when the model predicts normalized RT (<c>rt / rt_max</c>), false for the hydrophobic index.</summary>
        public bool PredictsNormalizedRt { get; private set; }

        public Device Device { get; }

        /// <summary>Predictions in input order; NaN for a peptide the encoding rejects.</summary>
        public double[] Predict(IReadOnlyList<PeptideForm> peptides, int batchSize = DEFAULT_BATCH_SIZE)
        {
            return PredictTokens(peptides.Select(Encoding.Encode).ToArray(), batchSize);
        }

        /// <summary>
        /// The linear map from this model's scale to the iRT scale, fitted by predicting the eleven iRT kit
        /// peptides, as for the AlphaPeptDeep model.
        /// </summary>
        public (double Slope, double Intercept) FitIrtCalibration()
        {
            return IrtKit.FitCalibration(peptides => Predict(peptides));
        }

        /// <summary>
        /// Makes the model predict normalized RT: <c>rt_norm = slope * hi + intercept</c>, fitted on the training
        /// peptides, is folded into the output layer, the starting point of fine-tuning.
        /// </summary>
        public void RescaleToNormalizedRt(double slope, double intercept)
        {
            if (PredictsNormalizedRt)
                throw new InvalidOperationException(@"The Chronologer model already predicts normalized RT.");
            Network.ScaleOutput(slope, intercept);
            PredictsNormalizedRt = true;
        }

        /// <summary>
        /// Saves the weights as safetensors, marked as a Chronologer, with the scale it predicts and the Chronologer it
        /// was fine-tuned from.
        /// </summary>
        public void Save(string path)
        {
            StateDict.WriteSafetensors(Network, path, new Dictionary<string, string>
            {
                { RT_MODEL_KEY, MODEL_NAME },
                { RT_SCALE_KEY, PredictsNormalizedRt ? SCALE_NORMALIZED_RT : SCALE_HYDROPHOBIC_INDEX },
                { VERSION_KEY, ChronologerFiles.VERSION },
            });
        }

        /// <summary>
        /// Predictions for encoded peptides (null entries predict NaN), in input order; normalized RT is clipped at 0,
        /// as <see cref="RtModel.Predict"/> clips it.
        /// </summary>
        internal double[] PredictTokens(IReadOnlyList<long[]> tokens, int batchSize = DEFAULT_BATCH_SIZE)
        {
            if (batchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, @"The batch size must be positive.");
            float floor = PredictsNormalizedRt ? 0f : float.NegativeInfinity;
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
                        var batch = Enumerable.Range(start, count).Select(i => tokens[accepted[i]]).ToArray();
                        float[] values = Forward(batch).cpu().data<float>().ToArray();
                        for (int i = 0; i < count; i++)
                            results[accepted[start + i]] = Math.Max(values[i], floor);
                    }
                }
            }
            return results;
        }

        /// <summary>Raw network output <c>[batch]</c> for encoded peptides, on <see cref="Device"/>, with gradients.</summary>
        internal Tensor Forward(IReadOnlyList<long[]> tokens)
        {
            var flat = new long[tokens.Count * ModelChronologer.VECTOR_LENGTH];
            for (int i = 0; i < tokens.Count; i++)
                Array.Copy(tokens[i], 0, flat, i * ModelChronologer.VECTOR_LENGTH, ModelChronologer.VECTOR_LENGTH);
            return Network.call(tensor(flat, new long[] { tokens.Count, ModelChronologer.VECTOR_LENGTH }).to(Device));
        }

        public void Dispose()
        {
            Network.Dispose();
        }
    }
}
