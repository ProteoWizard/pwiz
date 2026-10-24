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

namespace pwiz.Osprey.Demux
{
    /// <summary>The kinds of mass analyzer whose ion response is known (<see cref="IonResponse"/>).</summary>
    public enum MassAnalyzerKind
    {
        unknown,
        astral,
        orbitrap,
        ion_trap,
    }

    /// <summary>
    /// A mass analyzer's ion response, alpha: the factor between the intensity a spectrum reports and the ions it
    /// detected, I = alpha x N / t, with t the spectrum's ion injection (fill) time in seconds. So a spectrum's ions are
    /// N = I x t / alpha, and one ion is alpha / t intensity units (alpha x 1000 / fill time in ms).
    /// <para>The values are from Table 1 of Hsu C, Shulman N, Stewart HI, Petzoldt J, Pashkova A, Plubell DL, Denisov E,
    /// Hagedorn B, Damoc NE, MacLean BX, Remes PM, Canterbury JD, Makarov AA, Hock C, Zabrouskov V, Wu CC, MacCoss MJ.
    /// "Evaluation of a Prototype Orbitrap Astral Zoom Mass Spectrometer for Quantitative Proteomics - Beyond
    /// Identification Lists." J. Proteome Res. 2025, 24, 5742-5754. doi:10.1021/acs.jproteome.5c00593. alpha is the slope
    /// of the variance of fragment-ion intensity ratios against the inverse of the ions estimated from intensity x
    /// injection time, over 5,000 MS/MS spectra of infused Glu[1]-Fibrinopeptide B (the paper's eq 8, after Canterbury
    /// et al.). Table 1, by analyzer and instrument:</para>
    /// <list type="bullet">
    /// <item>Astral analyzer: Orbitrap Astral 1.53; Orbitrap Astral Zoom prototype 1.31.</item>
    /// <item>Orbitrap analyzer: Orbitrap Astral 10.62, Astral Zoom prototype 10.30, Exploris 480 10.95, Ascend 9.40,
    /// Fusion Lumos 15.44.</item>
    /// <item>Ion trap: Stellar 0.97, Ascend 0.98, Fusion Lumos 1.09.</item>
    /// </list>
    /// <para>Each kind of analyzer takes one value here: the production Orbitrap Astral's for the Astral analyzer, and
    /// the median of the instruments measured for the Orbitrap and the ion trap. An Astral or an ion trap reports
    /// close to ions per second; an Orbitrap about ten times that.</para>
    /// <para>Used for the ions only, which set the demultiplexers' Poisson weights and thresholds: the spectra written
    /// stay in the instrument's own intensity units.</para>
    /// </summary>
    public static class IonResponse
    {
        /// <summary>The Astral analyzer's alpha (an Orbitrap Astral's, Table 1).</summary>
        public const double ASTRAL = 1.53;

        /// <summary>An Orbitrap analyzer's alpha (the median of the five Orbitraps of Table 1).</summary>
        public const double ORBITRAP = 10.62;

        /// <summary>A linear ion trap's alpha (the median of the three ion traps of Table 1).</summary>
        public const double ION_TRAP = 0.98;

        /// <summary>Milliseconds per second: fill times are reported in ms.</summary>
        private const double MS_PER_SECOND = 1000;

        /// <summary>An analyzer's alpha; NaN when it is not known.</summary>
        public static double Of(MassAnalyzerKind kind)
        {
            switch (kind)
            {
                case MassAnalyzerKind.astral:
                    return ASTRAL;
                case MassAnalyzerKind.orbitrap:
                    return ORBITRAP;
                case MassAnalyzerKind.ion_trap:
                    return ION_TRAP;
                default:
                    return double.NaN;
            }
        }

        /// <summary>
        /// The kind of analyzer a PSI-MS analyzer term name, or several of an instrument configuration's joined,
        /// describes: "asymmetric track lossless time-of-flight analyzer" (an Astral), "orbitrap", or any "ion trap".
        /// The Astral's and the Orbitrap's are taken first, as a configuration that includes them may also list the
        /// quadrupole or the ion trap that feeds them.
        /// </summary>
        public static MassAnalyzerKind KindOf(string analyzerNames)
        {
            if (string.IsNullOrEmpty(analyzerNames))
                return MassAnalyzerKind.unknown;
            string names = analyzerNames.ToLowerInvariant();
            if (names.Contains(@"asymmetric track"))
                return MassAnalyzerKind.astral;
            if (names.Contains(@"orbitrap"))
                return MassAnalyzerKind.orbitrap;
            if (names.Contains(@"ion trap"))
                return MassAnalyzerKind.ion_trap;
            return MassAnalyzerKind.unknown;
        }

        /// <summary>
        /// The intensity units one ion makes in a spectrum filled for <paramref name="fillMs"/> milliseconds by an
        /// analyzer of ion response <paramref name="alpha"/>: alpha x 1000 / fill time; NaN when either is not known.
        /// </summary>
        public static double IntensityPerIon(double alpha, double fillMs)
        {
            return alpha > 0 && fillMs > 0 ? alpha * MS_PER_SECOND / fillMs : double.NaN;
        }
    }
}
