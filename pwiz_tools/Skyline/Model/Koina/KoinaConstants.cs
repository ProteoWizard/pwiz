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

namespace pwiz.Skyline.Model.Koina
{
    public static class KoinaConstants
    {
        public static readonly int PEPTIDE_SEQ_LEN = 30;
        public static readonly int IONS_PER_RESIDUE = 6;
        public static readonly int PRECURSOR_CHARGES = 6;
        public static readonly int BATCH_SIZE = 1000;
        public static readonly int MIN_NCE = 18;
        public static readonly int MAX_NCE = 39;
        public static readonly int MAX_THREADS = 4;

        public static bool CACHE_PREV_PREDICTION = true;

        public struct KoinaAA
        {
            public KoinaAA(char aa, int koinaIndex, StaticMod mod = null)
            {
                AA = aa;
                KoinaIndex = koinaIndex;
                Mod = mod;
            }

            public char AA;
            public int KoinaIndex;
            public StaticMod Mod;
        }

        private static readonly IList<KoinaAA> KoinaAAs = new List<KoinaAA>
        {
            new KoinaAA('A', 1), new KoinaAA('C', 2), new KoinaAA('D', 3),
            new KoinaAA('E', 4), new KoinaAA('F', 5), new KoinaAA('G', 6),
            new KoinaAA('H', 7), new KoinaAA('I', 8), new KoinaAA('K', 9),
            new KoinaAA('L', 10), new KoinaAA('M', 11), new KoinaAA('N', 12),
            new KoinaAA('P', 13), new KoinaAA('Q', 14), new KoinaAA('R', 15),
            new KoinaAA('S', 16), new KoinaAA('T', 17), new KoinaAA('V', 18),
            new KoinaAA('W', 19), new KoinaAA('Y', 20),

            // Mods - order has meaning - giving the first mod for an AA priority during testing
            new KoinaAA('C', 2, UniMod.DictStructuralModNames[@"Carbamidomethyl (C)"]),
            new KoinaAA('C', 2, UniMod.DictStructuralModNames[@"Propionamide (C)"]),   // Apparently has very similar fragmentation and RT as above (see https://skyline.ms/announcements/home/support/thread.view?rowId=44151)
            new KoinaAA('M', 21, UniMod.DictStructuralModNames[@"Oxidation (M)"])
        };

        public static readonly Dictionary<char, KoinaAA> AMINO_ACIDS =
            KoinaAAs.Where(paa => paa.Mod == null).ToDictionary(paa => paa.AA, paa => paa);

        public static readonly Dictionary<int, KoinaAA> AMINO_ACIDS_REVERSE =
            AMINO_ACIDS.ToDictionary(kvp => kvp.Value.KoinaIndex, kvp => kvp.Value);

        public static readonly Dictionary<string, KoinaAA> MODIFICATIONS =
            KoinaAAs.Where(paa => paa.Mod != null).ToDictionary(paa => paa.Mod.Name, paa => paa);

        public static readonly Dictionary<int, KoinaAA[]> MODIFICATIONS_REVERSE =
            MODIFICATIONS.GroupBy(kvp => kvp.Value.KoinaIndex).ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Value).ToArray());
    }
}