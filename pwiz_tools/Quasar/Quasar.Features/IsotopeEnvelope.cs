/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on skyline-glimpse (https://github.com/maccoss/skyline-glimpse)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

using System.Collections.Generic;

namespace pwiz.Quasar.Features
{
    /// <summary>
    /// One isotope envelope found in one spectrum: a set of peaks spaced by 1.00335/z that match the isotope
    /// distribution of an averagine peptide of <see cref="MonoMass"/> and <see cref="Charge"/>.
    /// </summary>
    /// <param name="MonoMass">Neutral monoisotopic mass (Da).</param>
    /// <param name="Charge">Charge state (1 or more).</param>
    /// <param name="Intensity">The envelope's intensity as Hardklor reports it.</param>
    /// <param name="BasePeakMz">m/z of the envelope's most intense isotope peak.</param>
    /// <param name="Correlation">How well the observed peaks match the model distribution (0 to 1).</param>
    /// <param name="LowMz">Lowest m/z of the envelope.</param>
    /// <param name="HighMz">Highest m/z of the envelope.</param>
    /// <param name="PeakIndices">Indices into the spectrum's m/z and intensity arrays of the peaks the envelope
    /// matched, ascending. Peaks can belong to more than one envelope.</param>
    public sealed record IsotopeEnvelope(
        double MonoMass,
        int Charge,
        float Intensity,
        double BasePeakMz,
        double Correlation,
        double LowMz,
        double HighMz,
        int[] PeakIndices);

    /// <summary>An elemental composition: the atoms an averagine model of a peptide is made of.</summary>
    public readonly record struct ElementCounts(int C, int H, int N, int O, int S);

    /// <summary>One isotope peak of a distribution: its mass and its abundance.</summary>
    public readonly record struct IsotopePeak(double Mass, double Abundance);

    /// <summary>
    /// Isotope distributions of elemental compositions. Quasar.IO implements it with pwiz's isotope
    /// calculator (PwizEnvelopeModel), so this project needs no pwiz reference.
    /// </summary>
    public interface IEnvelopeModel
    {
        /// <summary>
        /// The isotope peaks of <paramref name="formula"/> (neutral masses, ascending; abundances relative to the
        /// most abundant peak = 1), dropping peaks below <paramref name="abundanceCutoff"/> of the most abundant.
        /// </summary>
        IReadOnlyList<IsotopePeak> Distribution(ElementCounts formula, double abundanceCutoff);
    }
}
