/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on alphabase (https://github.com/MannLabs/alphabase), Apache-2.0
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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// One row of alphabase's modification table: an alphabase name such as
    /// <c>Carbamidomethyl@C</c> with its elemental composition and neutral-loss data.
    /// </summary>
    public sealed class ModificationDefinition
    {
        public ModificationDefinition(string name, ChemicalFormula composition, ChemicalFormula modLossComposition,
            int unimodId, double modLossImportance)
        {
            Name = name;
            Composition = composition;
            ModLossComposition = modLossComposition;
            UnimodId = unimodId;
            ModLossImportance = modLossImportance;
            Mass = composition.MonoisotopicMass;
        }

        /// <summary>The alphabase name, <c>&lt;mod&gt;@&lt;site&gt;</c>.</summary>
        public string Name { get; }

        public ChemicalFormula Composition { get; }

        public ChemicalFormula ModLossComposition { get; }

        public int UnimodId { get; }

        public double ModLossImportance { get; }

        /// <summary>Monoisotopic mass computed from <see cref="Composition"/>, as alphabase does.</summary>
        public double Mass { get; }

        /// <summary>
        /// The neutral-loss mass alphabase keeps for modloss fragment types: the loss mass when
        /// the modification's importance reaches <paramref name="importanceLevel"/>, else 0.
        /// </summary>
        public double GetModLossMass(double importanceLevel = 1)
        {
            return ModLossImportance >= importanceLevel ? ModLossComposition.MonoisotopicMass : 0;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
