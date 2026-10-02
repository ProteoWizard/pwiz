/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/models.py
 *   (AlphaCCSModel, ModelManager.predict_mobility), itself extracted from AlphaPeptDeep
 *   (https://github.com/MannLabs/alphapeptdeep), Apache-2.0
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
    /// The AlphaPeptDeep collisional cross section model. Predictions are CCS in square
    /// angstroms, clipped at 0, which <see cref="TimsMobility"/> converts to timsTOF 1/K0.
    /// Charge enters the model, so callers predict each precursor.
    /// </summary>
    public sealed class CcsModel : IDisposable
    {
        public const int DEFAULT_BATCH_SIZE = 1024;

        public static CcsModel FromPretrained(PretrainedModels pretrained, Device device)
        {
            return Create(StateDict.ReadPthFromZip(pretrained.ZipPath, PretrainedModels.CCS_ENTRY), device);
        }

        /// <summary>Loads a PyTorch checkpoint, such as Carafe's fine-tuned <c>ccs_model.pt</c>.</summary>
        public static CcsModel FromPthFile(string path, Device device)
        {
            return Create(StateDict.ReadPthFile(path), device);
        }

        /// <summary>The model with <paramref name="weights"/>, which it disposes once loaded.</summary>
        internal static CcsModel Create(IReadOnlyDictionary<string, Tensor> weights, Device device)
        {
            var network = new ModelCcsLstm();
            StateDict.Load(network, weights);
            foreach (var tensor in weights.Values)
                tensor.Dispose();
            network.to(device);
            network.eval();
            return new CcsModel(network, device);
        }

        private CcsModel(ModelCcsLstm network, Device device)
        {
            Network = network;
            Device = device;
        }

        internal ModelCcsLstm Network { get; }

        public Device Device { get; }

        /// <summary>
        /// Collisional cross sections (square angstroms, the network's float32), in input order,
        /// clipped at 0 as Carafe's <c>ModelInterface</c> clips every prediction (<c>min_pred_value</c>).
        /// </summary>
        public double[] Predict(IReadOnlyList<PrecursorForm> precursors, int batchSize = DEFAULT_BATCH_SIZE)
        {
            var results = new double[precursors.Count];
            Network.eval();
            using (no_grad())
            {
                foreach (int[] batch in LengthBatches.Split(precursors, p => p.Peptide.Length, batchSize))
                {
                    using (NewDisposeScope())
                    {
                        var output = Forward(batch.Select(i => precursors[i]).ToArray()).cpu();
                        float[] values = output.data<float>().ToArray();
                        for (int i = 0; i < batch.Length; i++)
                            results[batch[i]] = Math.Max(values[i], 0f);
                    }
                }
            }
            return results;
        }

        /// <summary>Raw network output <c>[batch]</c> for one same-length batch, on <see cref="Device"/>.</summary>
        internal Tensor Forward(IReadOnlyList<PrecursorForm> batch)
        {
            var peptides = batch.Select(p => p.Peptide).ToArray();
            var aa = PeptdeepFeaturizer.AaIndices(peptides).to(Device);
            var mods = PeptdeepFeaturizer.ModFeatures(peptides).to(Device);
            var charges = PeptdeepFeaturizer.Charges(batch.Select(p => p.Charge).ToArray()).to(Device);
            return Network.call(aa, mods, charges);
        }

        public void Dispose()
        {
            Network.Dispose();
        }
    }
}
