/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Prosit
{
    public static class PrositConstants
    {
        public static readonly int PEPTIDE_SEQ_LEN = 30;
        public static readonly int IONS_PER_RESIDUE = 6;
        public static readonly int PRECURSOR_CHARGES = 6;
        public static readonly int BATCH_SIZE = 4096;
        public static readonly int MIN_NCE = 18;
        public static readonly int MAX_NCE = 39;

        public static bool CACHE_PREV_PREDICTION = true;

        public struct PrositAA
        {
            public PrositAA(char aa, int prositIndex, StaticMod mod = null)
            {
                AA = aa;
                PrositIndex = prositIndex;
                Mod = mod;
            }

            public char AA;
            public int PrositIndex;
            public StaticMod Mod;
        }

        private static readonly HashSet<PrositAA> PrositAAs = new HashSet<PrositAA>()
        {
            new PrositAA('A', 1), new PrositAA('C', 2), new PrositAA('D', 3),
            new PrositAA('E', 4), new PrositAA('F', 5), new PrositAA('G', 6),
            new PrositAA('H', 7), new PrositAA('I', 8), new PrositAA('K', 9),
            new PrositAA('L', 10), new PrositAA('M', 11), new PrositAA('N', 12),
            new PrositAA('P', 13), new PrositAA('Q', 14), new PrositAA('R', 15),
            new PrositAA('S', 16), new PrositAA('T', 17), new PrositAA('V', 18),
            new PrositAA('W', 19), new PrositAA('Y', 20),

            // Mods
            new PrositAA('C', 2, UniMod.DictStructuralModNames[@"Carbamidomethyl (C)"]),
            new PrositAA('M', 21, UniMod.DictStructuralModNames[@"Oxidation (M)"])
        };

        public static readonly Dictionary<char, PrositAA> AMINO_ACIDS =
            PrositAAs.Where(paa => paa.Mod == null).ToDictionary(paa => paa.AA, paa => paa);

        public static readonly Dictionary<int, PrositAA> AMINO_ACIDS_REVERSE =
            AMINO_ACIDS.ToDictionary(kvp => kvp.Value.PrositIndex, kvp => kvp.Value);

        public static readonly Dictionary<string, PrositAA> MODIFICATIONS =
            PrositAAs.Where(paa => paa.Mod != null).ToDictionary(paa => paa.Mod.Name, paa => paa);

        public static readonly Dictionary<int, PrositAA> MODIFICATIONS_REVERSE =
            MODIFICATIONS.ToDictionary(kvp => kvp.Value.PrositIndex, kvp => kvp.Value);
    }
}