/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.AIGear
 *   (generate_spectral_library(String), get_InputRecord_for_prediction)
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// The peptidoforms and precursors Carafe predicts a library for. Carafe sorts every
    /// peptidoform by compomics mass, stably, from the iteration order of a parallel-built
    /// HashSet; CarafeSharp breaks mass ties by sequence and then by the peptidoform's place in
    /// <see cref="PeptideIsoformGenerator"/>'s order instead, so its order is deterministic but
    /// may differ from Carafe's among peptidoforms of equal mass. Only the order of the output
    /// differs; its content does not.
    /// </summary>
    public static class LibraryPeptideForms
    {
        /// <summary>Every peptidoform of <paramref name="peptides"/>, in mass order.</summary>
        public static List<PeptideIsoform> Enumerate(IEnumerable<string> peptides, PeptideIsoformGenerator generator)
        {
            var forms = new List<PeptideIsoform>();
            var ranks = new List<int>();
            foreach (string peptide in peptides)
            {
                int rank = 0;
                foreach (var isoform in generator.Enumerate(peptide))
                {
                    forms.Add(isoform);
                    ranks.Add(rank++);
                }
            }
            var order = new int[forms.Count];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int byMass = forms[a].Mass.CompareTo(forms[b].Mass);
                if (byMass != 0)
                    return byMass;
                int bySequence = string.CompareOrdinal(forms[a].Sequence, forms[b].Sequence);
                return bySequence != 0 ? bySequence : ranks[a].CompareTo(ranks[b]);
            });
            var sorted = new List<PeptideIsoform>(order.Length);
            foreach (int i in order)
                sorted.Add(forms[i]);
            return sorted;
        }

        /// <summary>
        /// The precursor charges of a peptidoform whose m/z falls in [<paramref name="minMz"/>,
        /// <paramref name="maxMz"/>], both inclusive, in charge order.
        /// </summary>
        public static List<int> GetCharges(PeptideIsoform isoform, IReadOnlyList<int> charges, double minMz, double maxMz)
        {
            var result = new List<int>(charges.Count);
            foreach (int charge in charges)
            {
                double mz = isoform.GetMz(charge);
                if (mz >= minMz && mz <= maxMz)
                    result.Add(charge);
            }
            return result;
        }
    }
}
