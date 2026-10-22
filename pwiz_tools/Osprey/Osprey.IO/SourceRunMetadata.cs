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
using pwiz.ProteowizardWrapper;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// What a run's source file says about its acquisition - instrument vendor and model, and
    /// the dissociation methods, collision energies and mass analyzers of its first MS2 spectra - for the
    /// training export's footer. Read by <see cref="SpectrumFileReader.TryReadSourceMetadata"/>
    /// only when an export asks for it, never during the search's own parse, so a run cached
    /// before the export existed describes itself just as well and the search pays nothing.
    /// </summary>
    public sealed class SourceRunMetadata
    {
        /// <summary>
        /// MS2 spectra sampled from the start of the file: enough to see every dissociation
        /// method, collision energy and mass analyzer of a DIA cycle, not a reason to read the
        /// whole run.
        /// </summary>
        public const int MAX_MS2_SPECTRA = 200;

        /// <summary>The histogram key for a spectrum that carries no value.</summary>
        public const string NONE_KEY = @"none";

        public string InstrumentVendor { get; set; }

        public string InstrumentModel { get; set; }

        /// <summary>MS2 spectra the histograms were counted over.</summary>
        public int NMs2Sampled { get; set; }

        /// <summary>Dissociation method name, counted over the sampled MS2 spectra.</summary>
        public SortedDictionary<string, int> DissociationMethods { get; } =
            new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// The mass analyzers of each sampled MS2 spectrum's scan configuration, counted. The key is
        /// pwiz's: the configuration's analyzers in component order, joined with "/", such as a
        /// Stellar's "radial ejection linear ion trap" or an Astral's MS2
        /// "quadrupole/asymmetric track lossless time-of-flight analyzer". A Tribrid reads MS2 out
        /// in its Orbitrap or ion trap, and a spectral library model treats them differently.
        /// </summary>
        public SortedDictionary<string, int> MassAnalyzers { get; } =
            new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Collision energy (round-trip text), counted over the sampled MS2 spectra.</summary>
        public SortedDictionary<string, int> CollisionEnergies { get; } =
            new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>The file-level instrument: the first declared configuration's model.</summary>
        internal void ObserveFile(MsDataFileImpl msData)
        {
            try
            {
                foreach (var config in msData.GetInstrumentConfigInfoList())
                {
                    if (!string.IsNullOrEmpty(config.Model))
                    {
                        InstrumentModel = config.Model;
                        break;
                    }
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                // Descriptive metadata only: a reader that cannot answer leaves it unknown.
            }
        }

        /// <summary>One MS2 spectrum's instrument, dissociation method, collision energy and mass analyzers.</summary>
        internal void ObserveMs2(MsDataSpectrum spectrum, MsPrecursor precursor)
        {
            if (NMs2Sampled == 0)
            {
                InstrumentVendor = spectrum.InstrumentVendor;
                string model = spectrum.InstrumentInfo?.Model;
                if (!string.IsNullOrEmpty(model))
                    InstrumentModel = model;
            }
            NMs2Sampled++;
            Count(DissociationMethods, string.IsNullOrEmpty(precursor.DissociationMethod) ? NONE_KEY : precursor.DissociationMethod);
            string analyzer = spectrum.InstrumentInfo?.Analyzer;
            Count(MassAnalyzers, string.IsNullOrEmpty(analyzer) ? NONE_KEY : analyzer);
            double? energy = precursor.PrecursorCollisionEnergy;
            Count(CollisionEnergies, energy.HasValue ? energy.Value.ToString(@"R", CultureInfo.InvariantCulture) : NONE_KEY);
        }

        private static void Count(SortedDictionary<string, int> histogram, string key)
        {
            histogram.TryGetValue(key, out int n);
            histogram[key] = n + 1;
        }
    }
}
