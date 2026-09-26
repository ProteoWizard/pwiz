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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// One precursor of a predicted spectral library, with everything the DIA-NN TSV and the
    /// BiblioSpec writers need. The notations are filled for the outputs requested: a spectrum
    /// bound only for a .blib has no <see cref="ModifiedPeptide"/>, and one bound only for a
    /// TSV has no Skyline notation.
    /// </summary>
    public sealed class LibrarySpectrum
    {
        /// <summary>Carafe's value for a peptide that no database entry maps to.</summary>
        public const string NO_PROTEIN = @"-";

        public LibrarySpectrum(PrecursorForm precursor, double precursorMz, double retentionTime, string proteinId,
            int decoy, IReadOnlyList<LibraryFragment> fragments)
        {
            Precursor = precursor;
            PrecursorMz = precursorMz;
            RetentionTime = retentionTime;
            ProteinId = proteinId;
            Decoy = decoy;
            Fragments = fragments;
        }

        /// <summary>The sequence, alphabase modifications and charge the models predicted.</summary>
        public PrecursorForm Precursor { get; }

        public string Sequence
        {
            get { return Precursor.Peptide.Sequence; }
        }

        public int Charge
        {
            get { return Precursor.Charge; }
        }

        /// <summary>The precursor m/z Carafe's Java computes from compomics masses.</summary>
        public double PrecursorMz { get; }

        /// <summary>
        /// The library retention time: minutes (<c>rt_max * rt_pred</c>) when the training
        /// gradient length is known, else iRT.
        /// </summary>
        public double RetentionTime { get; }

        /// <summary>The <c>;</c>-joined protein accessions, or <see cref="NO_PROTEIN"/>.</summary>
        public string ProteinId { get; }

        /// <summary>The TSV Decoy column value.</summary>
        public int Decoy { get; }

        /// <summary>The selected fragments, most intense first.</summary>
        public IReadOnlyList<LibraryFragment> Fragments { get; }

        /// <summary>The TSV ModifiedPeptide value, in the notation of the library format.</summary>
        public string ModifiedPeptide { get; set; }

        /// <summary>The BiblioSpec <c>peptideModSeq</c> (mass-shift notation).</summary>
        public string SkylineModifiedSequence { get; set; }

        /// <summary>The BiblioSpec <c>Modifications</c> rows.</summary>
        public IReadOnlyList<SkylineModification> SkylineModifications { get; set; } = Array.Empty<SkylineModification>();

        public override string ToString()
        {
            return Precursor.ToString();
        }
    }
}
