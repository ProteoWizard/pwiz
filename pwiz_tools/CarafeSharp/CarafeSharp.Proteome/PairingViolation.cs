/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/EntrapmentPairingValidator.java
 *   (Violation)
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
using System.Globalization;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>One kind of pairing integrity violation: how many, and a few examples to act on.</summary>
    public sealed class PairingViolation
    {
        public PairingViolation(string kind, int count, IReadOnlyList<string> examples)
        {
            Kind = kind;
            Count = count;
            Examples = examples;
        }

        public string Kind { get; }

        /// <summary>The full count; <see cref="Examples"/> is capped.</summary>
        public int Count { get; }

        public IReadOnlyList<string> Examples { get; }

        public override string ToString()
        {
            string text = Kind + @": " + Count.ToString(CultureInfo.InvariantCulture);
            return Examples.Count == 0 ? text : text + @" (e.g. " + string.Join(@", ", Examples) + @")";
        }
    }
}
