/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.AIGear
 *   (generate_spectral_library_parquet, generate_spectral_library_parquet_skyline) and
 *   main.java.db.DBGear (add_protein_to_psm_table, the decoy flag)
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
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Turns one precursor's predictions into a library spectrum as Carafe's Java does: the
    /// fragments of <see cref="CarafeFragmentSelector"/> over alphabase's fragment m/z (a
    /// precursor with fewer than <c>-lf_min_n_frag</c> is left out), the peptide's proteins
    /// (<c>-</c> when none), the Decoy flag (always 0 on Carafe's <c>-fast</c> path; otherwise 1
    /// when every protein carries the decoy prefix), and the notations the requested outputs
    /// need. Stateless apart from its settings, so precursors can be built in parallel.
    /// </summary>
    public sealed class LibrarySpectrumBuilder
    {
        /// <summary>
        /// Carafe's library retention time: <c>rt_max * rt_pred</c> when the training gradient
        /// length is known, else <c>irt_pred</c>.
        /// </summary>
        public static double GetRetentionTime(double rtPred, double rtMax, double irtSlope, double irtIntercept)
        {
            return rtMax > 0 ? rtMax * rtPred : rtPred * irtSlope + irtIntercept;
        }

        private readonly CarafeFragmentSelector _selector;
        private readonly int _minFragments;
        private readonly bool _fast;
        private readonly string _decoyPrefix;
        private readonly LibraryOutputs _outputs;
        private readonly IReadOnlyDictionary<string, string> _peptideToProteins;

        public LibrarySpectrumBuilder(LibrarySettings settings, LibraryOutputs outputs, IReadOnlyDictionary<string, string> peptideToProteins)
        {
            _selector = new CarafeFragmentSelector(settings.MinFragmentMz, settings.MaxFragmentMz, settings.TopFragments,
                settings.MinFragmentNumber);
            _minFragments = settings.MinFragments;
            _fast = settings.Fast;
            _decoyPrefix = settings.DecoyPrefix;
            _outputs = outputs;
            _peptideToProteins = peptideToProteins;
        }

        /// <summary>
        /// The library spectrum of <paramref name="precursor"/>, or null when too few fragments
        /// pass.
        /// </summary>
        /// <param name="isoform">The peptidoform the precursor is a charge state of.</param>
        /// <param name="precursor">Its alphabase form (<see cref="PeptideIsoform.ToAlphabase"/>) and charge.</param>
        /// <param name="intensities">Predicted intensities, <paramref name="stride"/> per fragment row, b_z1 b_z2 y_z1 y_z2 first.</param>
        /// <param name="stride">Values per fragment row of <paramref name="intensities"/>.</param>
        /// <param name="retentionTime">From <see cref="GetRetentionTime"/>.</param>
        public LibrarySpectrum Build(PeptideIsoform isoform, PrecursorForm precursor, float[] intensities, int stride, double retentionTime)
        {
            var fragments = _selector.Select(intensities, stride, AlphabaseFragmentMz.Calculate(precursor));
            if (fragments.Count < _minFragments)
                return null;
            string proteins = _peptideToProteins.TryGetValue(isoform.Sequence, out string found) ? found : LibrarySpectrum.NO_PROTEIN;
            var spectrum = new LibrarySpectrum(precursor, isoform.GetMz(precursor.Charge), retentionTime, proteins,
                _fast ? 0 : IsDecoy(proteins) ? 1 : 0, fragments);
            if (_outputs.WritesTsv)
                spectrum.ModifiedPeptide = ModifiedPeptideNotation.Format(isoform, _outputs.TsvStyle);
            if (_outputs.WritesBlib)
            {
                spectrum.SkylineModifiedSequence = ModifiedPeptideNotation.FormatSkyline(isoform, out var modifications);
                spectrum.SkylineModifications = modifications;
            }
            return spectrum;
        }

        /// <summary>DBGear.add_protein_to_psm_table: a decoy when every protein carries the prefix.</summary>
        private bool IsDecoy(string proteins)
        {
            if (proteins == LibrarySpectrum.NO_PROTEIN)
                return false;
            foreach (string protein in JavaText.Split(proteins, ';'))
            {
                if (!protein.StartsWith(_decoyPrefix, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }
    }
}
