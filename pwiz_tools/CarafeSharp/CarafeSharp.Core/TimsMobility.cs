/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on alphabase 1.2.1 (https://github.com/MannLabs/alphabase) alphabase/peptide/mobility.py
 *   (ccs_to_mobility_bruker) and constants/const_files/common_constants.yaml, Apache-2.0
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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// The timsTOF ion mobility of a collisional cross section, as alphabase converts it for
    /// Carafe (<c>ccs_to_mobility_bruker</c>): the reduced ion mobility 1/K0 in V s/cm^2, from
    /// the Mason-Schamp relation with alphabase's constant and an N2 drift gas of mass 28.
    /// </summary>
    public static class TimsMobility
    {
        /// <summary>alphabase's <c>CCS_IM_COEF</c>.</summary>
        public const double CCS_IM_COEF = 1059.62245;

        /// <summary>alphabase's <c>IM_GAS_MASS</c>: the drift gas, N2, rounded to 28.</summary>
        public const double IM_GAS_MASS = 28.0;

        /// <summary>
        /// The 1/K0 of a precursor with collisional cross section <paramref name="ccs"/> (square
        /// angstroms), from its alphabase precursor m/z
        /// (<see cref="AlphabaseFragmentMz.CalculatePrecursorMz"/>), in alphabase's order of operations.
        /// </summary>
        public static double CcsToInverseK0(double ccs, PrecursorForm precursor)
        {
            return CcsToInverseK0(ccs, AlphabaseFragmentMz.CalculatePrecursorMz(precursor), precursor.Charge);
        }

        public static double CcsToInverseK0(double ccs, double precursorMz, int charge)
        {
            double mass = precursorMz * charge;
            double reducedMass = mass * IM_GAS_MASS / (mass + IM_GAS_MASS);
            return ccs * Math.Sqrt(reducedMass) / charge / CCS_IM_COEF;
        }
    }
}
