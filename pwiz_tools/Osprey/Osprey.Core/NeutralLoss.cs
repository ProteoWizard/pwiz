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

using System.Globalization;
using pwiz.Common.Chemistry;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Neutral loss kind, carried inline on a <see cref="FragmentAnnotation"/> as
    /// a compact value code rather than a per-fragment heap reference. The values
    /// match the on-disk tags written by the library cache, so serialization needs
    /// no translation table. Maps to osprey-core/src/types.rs NeutralLoss enum.
    /// </summary>
    public enum NeutralLossCode : byte
    {
        None = 0,
        H2O = 1,
        NH3 = 2,
        H3PO4 = 3,
        Custom = 4
    }

    /// <summary>
    /// Neutral loss masses and parsing. Formerly a per-fragment reference type;
    /// now a static helper so a <see cref="FragmentAnnotation"/> can hold a
    /// <see cref="NeutralLossCode"/> plus a custom mass entirely by value.
    /// </summary>
    public static class NeutralLoss
    {
        // From the formulas and the isotope table every tool in pwiz shares
        // (pwiz.Common.Chemistry, the values Skyline calculates with), not a copy of them: the
        // tools that compute peptide and fragment masses must agree to the last digit.
        public static readonly double H2OMass = MonoisotopicMass(@"H2O");
        public static readonly double NH3Mass = MonoisotopicMass(@"NH3");
        public static readonly double H3PO4Mass = MonoisotopicMass(@"H3PO4");

        /// <summary>The monoisotopic mass of <paramref name="formula"/> from <see cref="IsotopeAbundances.Default"/>, as <see cref="AminoAcidFormulas"/> sums a peptide's.</summary>
        public static double MonoisotopicMass(string formula)
        {
            double mass = 0.0;
            foreach (var element in Molecule.Parse(formula))
                mass += IsotopeAbundances.Default[element.Key].MostAbundanceMass * element.Value;
            return mass;
        }

        /// <summary>
        /// Mass of the neutral loss for the given code, using
        /// <paramref name="customMass"/> only when the code is
        /// <see cref="NeutralLossCode.Custom"/>. Returns 0 for
        /// <see cref="NeutralLossCode.None"/>.
        /// </summary>
        public static double MassFor(NeutralLossCode code, double customMass)
        {
            switch (code)
            {
                case NeutralLossCode.H2O: return H2OMass;
                case NeutralLossCode.NH3: return NH3Mass;
                case NeutralLossCode.H3PO4: return H3PO4Mass;
                case NeutralLossCode.Custom: return customMass;
                default: return 0.0;
            }
        }

        /// <summary>
        /// Parses a string to a neutral loss: (<see cref="NeutralLossCode.None"/>, 0) for
        /// "NOLOSS", a named code for known losses, and
        /// (<see cref="NeutralLossCode.Custom"/>, mass) for a numeric mass. Null for empty or
        /// unrecognized input, which a library reader must report rather than read as no loss.
        /// </summary>
        public static (NeutralLossCode Code, double CustomMass)? Parse(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            switch (s.ToUpperInvariant())
            {
                case @"H2O":
                case @"WATER":
                    return (NeutralLossCode.H2O, 0.0);
                case @"NH3":
                case @"AMMONIA":
                    return (NeutralLossCode.NH3, 0.0);
                case @"H3PO4":
                case @"PHOSPHO":
                    return (NeutralLossCode.H3PO4, 0.0);
                case @"NOLOSS":
                    return (NeutralLossCode.None, 0.0);
                default:
                    double mass;
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
                        return (NeutralLossCode.Custom, mass);
                    return null;
            }
        }
    }
}
