/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

namespace pwiz.CarafeSharp.Models
{
    /// <summary>What a model predicts: fragment intensities (MS2), retention time, or CCS.</summary>
    [Flags]
    public enum ModelPredictions
    {
        none = 0,
        ms2 = 1,
        rt = 2,
        ccs = 4,
        all = ms2 | rt | ccs
    }

    /// <summary>
    /// What the models were trained to predict for a peptide's modifications, so a library never
    /// carries a value from a model that was not trained for it. The pretrained AlphaPeptDeep models
    /// were validated per modification by David Shteynberg for Skyline's first AlphaPeptDeep
    /// integration: Carbamidomethyl and Oxidation for everything; Phospho and GlyGly for MS2 alone,
    /// their RT and CCS withheld. A modification the table says nothing about is predicted as
    /// Carafe predicts it; the table records what was validated. It is applied to a precursor
    /// table's library (Skyline's Build Library); a digest's library is written as Carafe's Java
    /// writes it, which the parity tests hold it to. A fine-tuned model adds the modifications it was trained on, for MS2 and
    /// RT; the CCS model is always the pretrained one. Chronologer, the default RT model, is a case
    /// of its own: it predicts any peptide it can encode, and only its fallback to the AlphaPeptDeep
    /// RT model is subject to the table (<see cref="ChronologerRtPredictor.LastFallback"/>).
    /// </summary>
    public sealed class ModelSupport
    {
        /// <summary>The pretrained models' support, by Unimod accession.</summary>
        public static readonly ModelSupport PRETRAINED = new ModelSupport(new Dictionary<int, ModelPredictions>
        {
            { 4, ModelPredictions.all },    // Carbamidomethyl
            { 35, ModelPredictions.all },   // Oxidation
            { 21, ModelPredictions.ms2 },   // Phospho
            { 121, ModelPredictions.ms2 },  // GlyGly
        });

        private readonly IReadOnlyDictionary<int, ModelPredictions> _byUnimod;

        private ModelSupport(IReadOnlyDictionary<int, ModelPredictions> byUnimod)
        {
            _byUnimod = byUnimod;
        }

        /// <summary>
        /// This support extended by a fine-tuned model's training modifications, named as alphabase
        /// names them (<c>Phospho@S</c>), each for MS2 and RT on top of whatever the pretrained
        /// models support for it; a name the modification table does not know adds nothing.
        /// </summary>
        public ModelSupport WithTrainingModifications(IEnumerable<string> alphabaseNames)
        {
            var byUnimod = _byUnimod.ToDictionary(p => p.Key, p => p.Value);
            foreach (string name in alphabaseNames)
            {
                if (!ModificationTable.TryGet(name, out var definition) || definition.UnimodId <= 0)
                    continue;
                byUnimod.TryGetValue(definition.UnimodId, out var existing);
                byUnimod[definition.UnimodId] = existing | ModelPredictions.ms2 | ModelPredictions.rt;
            }
            return new ModelSupport(byUnimod);
        }

        /// <summary>What the models predict for every modification of <paramref name="peptide"/> the table has a verdict on.</summary>
        public ModelPredictions Get(PeptideForm peptide)
        {
            var predictions = ModelPredictions.all;
            foreach (string name in peptide.ModNames)
            {
                if (ModificationTable.TryGet(name, out var definition) && _byUnimod.TryGetValue(definition.UnimodId, out var supported))
                    predictions &= supported;
            }
            return predictions;
        }
    }
}
