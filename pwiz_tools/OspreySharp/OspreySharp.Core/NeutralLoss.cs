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

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Represents a neutral loss from a fragment ion.
    /// Maps to osprey-core/src/types.rs NeutralLoss enum.
    /// </summary>
    public class NeutralLoss
    {
        public static readonly NeutralLoss H2O = new NeutralLoss(18.010565);
        public static readonly NeutralLoss NH3 = new NeutralLoss(17.026549);
        public static readonly NeutralLoss H3PO4 = new NeutralLoss(97.976896);

        public double Mass { get; private set; }

        private NeutralLoss(double mass)
        {
            Mass = mass;
        }

        /// <summary>
        /// Creates a custom neutral loss with the specified mass.
        /// </summary>
        public static NeutralLoss Custom(double mass)
        {
            return new NeutralLoss(mass);
        }

        /// <summary>
        /// Parses a string to a <see cref="NeutralLoss"/>. Returns null for empty, "NOLOSS", or unrecognized input.
        /// </summary>
        public static NeutralLoss Parse(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            switch (s.ToUpperInvariant())
            {
                case "H2O":
                case "WATER":
                    return H2O;
                case "NH3":
                case "AMMONIA":
                    return NH3;
                case "H3PO4":
                case "PHOSPHO":
                    return H3PO4;
                case "NOLOSS":
                    return null;
                default:
                    double mass;
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
                        return Custom(mass);
                    return null;
            }
        }
    }
}
