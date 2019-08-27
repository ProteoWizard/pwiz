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

namespace pwiz.Skyline.Model.Prosit
{
    public static class Constants
    {
        public static readonly int PEPTIDE_SEQ_LEN = 30;
        public static readonly int PRECURSOR_CHARGES = 6;
        public static readonly int BATCH_SIZE = 2048;

        public static readonly Dictionary<char, int> AMINO_ACIDS = new Dictionary<char, int>
        {
            {'A', 1}, {'C', 2}, {'D', 3}, {'E', 4}, {'F', 5}, {'G', 6}, {'H', 7}, {'I', 8},
            {'K', 9}, {'L', 10}, {'M', 11}, {'N', 12}, {'P', 13}, {'Q', 14}, {'R', 15}, {'S', 16},
            {'T', 17}, {'V', 18}, {'W', 19}, {'Y', 20}
        };

        public static readonly Dictionary<string, int> MODIFICATIONS = new Dictionary<string, int>
        {
            { "Carbamidomethyl Cysteine", 2 },
            { "Carbamidomethyl (C)", 2 },
            { "Oxidation (M)", 21 },
            /*{ "Phospho (ST)", 22 },
            { "Phospho (ST)", 23 },
            { "Phospho (Y)", 24 },
            { "Citrullination (R)", 25 }, // ?
            { "GlyGly (K)", 26 },
            { "GlucNac (T)", 27 }, // ?
            { "GlucNac (S)", 28 }, // ?
            { "Q", 29 }, // ???
            { "Methyl (R)", 30 },
            { "Methyl (K)", 31 },
            { "GalNac (T)", 32 }, // ?
            { "GalNac (S)", 33 }, // ?
            { "Acetyl (K)", 34 }*/
        };
    }
}