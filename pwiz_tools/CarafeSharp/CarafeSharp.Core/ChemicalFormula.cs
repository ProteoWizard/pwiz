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

using System;
using System.Collections.Generic;
using System.Globalization;

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// An alphabase composition string such as <c>H(3)C(2)N(1)O(1)</c>, kept as the ordered
    /// (element, count) terms it was written with. The order matters to the AlphaPeptDeep
    /// modification featurizer, which assigns rather than accumulates repeated elements.
    /// </summary>
    public sealed class ChemicalFormula
    {
        public static readonly ChemicalFormula EMPTY = new ChemicalFormula(string.Empty, Array.Empty<KeyValuePair<string, int>>());

        /// <summary>
        /// Parses an alphabase composition string. Terms without a parenthesized count are
        /// skipped, exactly as alphabase and Carafe's <c>_parse_mod_formula</c> skip them.
        /// </summary>
        public static ChemicalFormula Parse(string composition)
        {
            if (string.IsNullOrEmpty(composition))
                return EMPTY;
            var terms = new List<KeyValuePair<string, int>>();
            foreach (string term in composition.TrimEnd(')').Split(')'))
            {
                int open = term.IndexOf('(');
                if (open < 0)
                    continue;
                string element = term.Substring(0, open);
                int count = int.Parse(term.Substring(open + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                terms.Add(new KeyValuePair<string, int>(element, count));
            }
            return new ChemicalFormula(composition, terms.ToArray());
        }

        private ChemicalFormula(string text, KeyValuePair<string, int>[] terms)
        {
            Text = text;
            Terms = terms;
        }

        public string Text { get; }

        /// <summary>The (element, count) terms in the order they were written.</summary>
        public IReadOnlyList<KeyValuePair<string, int>> Terms { get; }

        public bool IsEmpty
        {
            get { return Terms.Count == 0; }
        }

        /// <summary>
        /// Monoisotopic mass using the most abundant isotope of each element, the definition
        /// alphabase's <c>calc_mass_from_formula</c> uses.
        /// </summary>
        public double MonoisotopicMass
        {
            get
            {
                double mass = 0;
                foreach (var term in Terms)
                    mass += AlphabaseMasses.GetElementMass(term.Key) * term.Value;
                return mass;
            }
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
