/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/models.py,
 *   itself extracted from AlphaPeptDeep (https://github.com/MannLabs/alphapeptdeep), Apache-2.0
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using pwiz.CarafeSharp.Core;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Turns peptides into the input tensors the AlphaPeptDeep networks take. Every batch must
    /// hold peptides of one length: the networks use no attention mask, so padding a shorter
    /// peptide would change its prediction.
    /// </summary>
    public static class PeptdeepFeaturizer
    {
        private static readonly Dictionary<string, int> MOD_ELEMENT_INDEX = BuildElementIndex();

        private static readonly ConcurrentDictionary<string, double[]> MOD_FEATURES =
            new ConcurrentDictionary<string, double[]>(StringComparer.Ordinal);

        /// <summary>
        /// The 109-element feature vector for an alphabase modification name, following
        /// Carafe's <c>_parse_mod_formula</c>: each known element's count is ASSIGNED (a
        /// repeated element keeps its last count), unknown elements accumulate in the final
        /// <c>?</c> slot, and a modification that is unknown or has no composition is all zeros.
        /// </summary>
        public static double[] GetModFeature(string modName)
        {
            return MOD_FEATURES.GetOrAdd(modName, ComputeModFeature);
        }

        /// <summary>
        /// Residue indices <c>[batch, n + 2]</c> (int64): A=1 .. Z=26, with a 0 pad at each end.
        /// </summary>
        public static Tensor AaIndices(IReadOnlyList<PeptideForm> batch)
        {
            int length = CheckSameLength(batch);
            int width = length + 2;
            var data = new long[batch.Count * width];
            for (int b = 0; b < batch.Count; b++)
            {
                string sequence = batch[b].Sequence;
                for (int i = 0; i < length; i++)
                    data[b * width + i + 1] = sequence[i] - 'A' + 1;
            }
            return tensor(data, new long[] { batch.Count, width });
        }

        /// <summary>
        /// Modification features <c>[batch, n + 2, 109]</c> (float32). Site 0 (N-term) lands on
        /// the N pad token, residue site k on position k, and site -1 (C-term) on the C pad
        /// token. Features are summed in float64 in site order and then rounded to float32,
        /// matching peptdeep's iterative featurizer.
        /// </summary>
        public static Tensor ModFeatures(IReadOnlyList<PeptideForm> batch)
        {
            int length = CheckSameLength(batch);
            int width = length + 2;
            int featureSize = PeptdeepConstants.MOD_FEATURE_SIZE;
            var accumulated = new double[batch.Count * width * featureSize];
            for (int b = 0; b < batch.Count; b++)
            {
                var peptide = batch[b];
                for (int m = 0; m < peptide.ModNames.Count; m++)
                {
                    int site = peptide.ModSites[m];
                    int position = site < 0 ? length + 1 : site;
                    double[] feature = GetModFeature(peptide.ModNames[m]);
                    int offset = (b * width + position) * featureSize;
                    for (int f = 0; f < featureSize; f++)
                        accumulated[offset + f] += feature[f];
                }
            }
            var data = new float[accumulated.Length];
            for (int i = 0; i < data.Length; i++)
                data[i] = (float)accumulated[i];
            return tensor(data, new long[] { batch.Count, width, featureSize });
        }

        /// <summary>
        /// Scaled charges <c>[batch, 1]</c>: a float32 tensor multiplied by 0.1 in libtorch, as
        /// peptdeep does it, so the rounding matches.
        /// </summary>
        public static Tensor Charges(IReadOnlyList<int> charges)
        {
            var data = new float[charges.Count];
            for (int i = 0; i < data.Length; i++)
                data[i] = charges[i];
            return tensor(data, new long[] { data.Length }).unsqueeze(1) * PeptdeepConstants.CHARGE_FACTOR;
        }

        /// <summary>Scaled NCEs <c>[batch, 1]</c>, float32 times 0.01 in libtorch.</summary>
        public static Tensor Nces(IReadOnlyList<double> nces)
        {
            var data = new float[nces.Count];
            for (int i = 0; i < data.Length; i++)
                data[i] = (float)nces[i];
            return tensor(data, new long[] { data.Length }).unsqueeze(1) * PeptdeepConstants.NCE_FACTOR;
        }

        /// <summary>Instrument embedding indices <c>[batch]</c> (int64).</summary>
        public static Tensor InstrumentIndices(IReadOnlyList<string> instruments)
        {
            var data = new long[instruments.Count];
            for (int i = 0; i < data.Length; i++)
                data[i] = PeptdeepConstants.GetInstrumentIndex(instruments[i]);
            return tensor(data, new long[] { data.Length });
        }

        private static int CheckSameLength(IReadOnlyList<PeptideForm> batch)
        {
            if (batch.Count == 0)
                throw new ArgumentException(@"Empty batch.", nameof(batch));
            int length = batch[0].Length;
            foreach (var peptide in batch)
            {
                if (peptide.Length != length)
                    throw new ArgumentException(@"A batch must hold peptides of one length.", nameof(batch));
            }
            if (length + 2 > PeptdeepConstants.MAX_SEQUENCE_LENGTH)
            {
                throw new ArgumentException(string.Format(@"Peptide length {0} exceeds the model limit of {1}.",
                    length, PeptdeepConstants.MAX_SEQUENCE_LENGTH - 2), nameof(batch));
            }
            return length;
        }

        private static double[] ComputeModFeature(string modName)
        {
            var feature = new double[PeptdeepConstants.MOD_FEATURE_SIZE];
            if (!ModificationTable.TryGet(modName, out var definition))
                return feature;
            foreach (var term in definition.Composition.Terms)
            {
                if (MOD_ELEMENT_INDEX.TryGetValue(term.Key, out int index))
                    feature[index] = term.Value;
                else
                    feature[feature.Length - 1] += term.Value;
            }
            return feature;
        }

        private static Dictionary<string, int> BuildElementIndex()
        {
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < PeptdeepConstants.MOD_ELEMENTS.Length; i++)
                index[PeptdeepConstants.MOD_ELEMENTS[i]] = i;
            return index;
        }
    }
}
