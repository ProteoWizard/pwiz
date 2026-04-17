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
    /// Fragment ion types. Maps to osprey-core/src/types.rs IonType enum.
    /// </summary>
    public enum IonType
    {
        B,
        Y,
        A,
        C,
        X,
        Z,
        Precursor,
        Internal,
        Immonium,
        Unknown
    }

    /// <summary>
    /// Extension methods for <see cref="IonType"/>.
    /// </summary>
    public static class IonTypeExtensions
    {
        /// <summary>
        /// Parses a character to an <see cref="IonType"/>, case-insensitive.
        /// </summary>
        public static IonType FromChar(char c)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'B': return IonType.B;
                case 'Y': return IonType.Y;
                case 'A': return IonType.A;
                case 'C': return IonType.C;
                case 'X': return IonType.X;
                case 'Z': return IonType.Z;
                default: return IonType.Unknown;
            }
        }
    }
}
