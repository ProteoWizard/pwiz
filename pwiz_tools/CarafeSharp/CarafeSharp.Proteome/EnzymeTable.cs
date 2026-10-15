/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/DBGear.java
 *   (init_enzymes, getEnzymeByIndex, getEnzymeIndexByName, isNoCutEnzyme)
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
    /// Carafe's enzyme list, in the order its <c>-enzyme</c> index refers to. Index 0 cleaves
    /// after every residue, and the last entry, NoCut, never cleaves: it treats each FASTA
    /// record as one already digested peptide.
    /// </summary>
    public static class EnzymeTable
    {
        public const string NON_SPECIFIC_ENZYME_NAME = @"non-specific";
        public const string NO_CUT_ENZYME_NAME = @"NoCut";

        /// <summary>Index of plain trypsin, Carafe's default enzyme.</summary>
        public const int DEFAULT_ENZYME_INDEX = 1;

        /// <summary>
        /// Missed cleavages Carafe substitutes for the non-specific enzyme, whatever
        /// <c>-miss_c</c> said.
        /// </summary>
        public const int NON_SPECIFIC_MISSED_CLEAVAGES = 100;

        private static readonly Enzyme[] ENZYMES =
        {
            // Carafe lists every residue except J, O and Z, so a J or Z still cleaves (through
            // I/L and Q/E) but an O does not.
            new Enzyme(NON_SPECIFIC_ENZYME_NAME, @"ABCDEFGHIKLMNPQRSTUVWXY"),
            new Enzyme(@"Trypsin", @"RK", restrictionAfter: @"P"),
            new Enzyme(@"Trypsin (no P rule)", @"RK"),
            new Enzyme(@"Arg-C", @"R", restrictionAfter: @"P"),
            new Enzyme(@"Arg-C (no P rule)", @"R"),
            new Enzyme(@"Arg-N", @"", @"R"),
            new Enzyme(@"Glu-C", @"E"),
            new Enzyme(@"Lys-C", @"K", restrictionAfter: @"P"),
            new Enzyme(@"Lys-C (no P rule)", @"K"),
            new Enzyme(@"Lys-N", @"", @"K"),
            new Enzyme(@"Asp-N", @"", @"D"),
            new Enzyme(@"Asp-N (ambic)", @"", @"DE"),
            new Enzyme(@"Chymotrypsin", @"FYWL", restrictionAfter: @"P"),
            new Enzyme(@"Chymotrypsin (no P rule)", @"FYWL"),
            new Enzyme(@"Pepsin A", @"FL"),
            new Enzyme(@"CNBr", @"M"),
            new Enzyme(@"Thermolysin", @"", @"AFILMV"),
            new Enzyme(@"LysargiNase", @"", @"RK"),
            // Cleaves before X, which no residue's expansion contains, so never.
            new Enzyme(NO_CUT_ENZYME_NAME, @"", @"X"),
        };

        public static IReadOnlyList<Enzyme> All
        {
            get { return ENZYMES; }
        }

        /// <summary>
        /// The enzyme at a Carafe <c>-enzyme</c> index. Carafe exits on an index outside
        /// [0, count] and fails on count itself; both are an error here.
        /// </summary>
        public static Enzyme GetByIndex(int index)
        {
            if (index < 0 || index >= ENZYMES.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    string.Format(@"Please provide a valid enzyme number: {0}", index));
            }
            return ENZYMES[index];
        }

        /// <summary>The index of an enzyme by case-insensitive name, as Carafe's <c>-enzyme NoCut</c> uses.</summary>
        public static int GetIndexByName(string name)
        {
            for (int i = 0; i < ENZYMES.Length; i++)
            {
                if (string.Equals(ENZYMES[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            throw new ArgumentException(string.Format(@"Please provide a valid enzyme name: {0}", name), nameof(name));
        }

        public static bool IsNoCut(Enzyme enzyme)
        {
            return enzyme != null && string.Equals(enzyme.Name, NO_CUT_ENZYME_NAME, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNonSpecific(Enzyme enzyme)
        {
            return enzyme != null && string.Equals(enzyme.Name, NON_SPECIFIC_ENZYME_NAME, StringComparison.OrdinalIgnoreCase);
        }
    }
}
