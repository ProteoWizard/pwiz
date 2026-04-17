/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
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

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Isotope envelope extracted from an MS1 spectrum. Maps to osprey-core/src/types.rs IsotopeEnvelope.
    /// </summary>
    public class IsotopeEnvelope
    {
        public const double NEUTRON_MASS = 1.002868;

        /// <summary>
        /// Intensities at five isotope positions: [M-1, M+0, M+1, M+2, M+3].
        /// </summary>
        public double[] Intensities { get; set; }

        /// <summary>
        /// Observed m/z of the monoisotopic (M+0) peak, if found.
        /// </summary>
        public double? M0ObservedMz { get; set; }

        /// <summary>
        /// Mass error of the M+0 peak in Da, if found.
        /// </summary>
        public double? M0MassError { get; set; }

        public double M0Intensity { get { return Intensities[1]; } }
        public bool HasM0 { get { return M0ObservedMz.HasValue; } }

        public IsotopeEnvelope()
        {
            Intensities = new double[5];
        }

        /// <summary>
        /// Calculates the m/z values for five isotope positions around the precursor.
        /// Returns [M-1, M+0, M+1, M+2, M+3].
        /// </summary>
        public static double[] CalculateIsotopeMzs(double precursorMz, int charge)
        {
            double gap = NEUTRON_MASS / charge;
            return new[]
            {
                precursorMz - gap,
                precursorMz,
                precursorMz + gap,
                precursorMz + 2 * gap,
                precursorMz + 3 * gap
            };
        }

        /// <summary>
        /// Extracts an isotope envelope from an MS1 spectrum around the given precursor m/z.
        /// </summary>
        public static IsotopeEnvelope Extract(MS1Spectrum ms1, double precursorMz, int charge, double tolerancePpm)
        {
            var envelope = new IsotopeEnvelope();
            double[] isotopeMzs = CalculateIsotopeMzs(precursorMz, charge);

            for (int i = 0; i < 5; i++)
            {
                var peak = ms1.FindPeakPpm(isotopeMzs[i], tolerancePpm);
                if (peak.HasValue)
                {
                    envelope.Intensities[i] = peak.Value.Intensity;

                    // M+0 is at index 1
                    if (i == 1)
                    {
                        envelope.M0ObservedMz = peak.Value.Mz;
                        envelope.M0MassError = peak.Value.Mz - precursorMz;
                    }
                }
            }

            return envelope;
        }

        /// <summary>
        /// Calculates the ppm error of the observed M+0 peak relative to the expected precursor m/z.
        /// </summary>
        public double? M0PpmError(double precursorMz)
        {
            if (!M0ObservedMz.HasValue)
                return null;

            return (M0ObservedMz.Value - precursorMz) / precursorMz * 1e6;
        }
    }
}
