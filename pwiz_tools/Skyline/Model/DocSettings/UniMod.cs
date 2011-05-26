/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    public static class UniMod
    {
        public static Dictionary<string, StaticMod> DictStructuralModNames { get; private set; }
        public static Dictionary<string, StaticMod> DictHiddenStructuralModNames { get; private set; }
        public static Dictionary<string, StaticMod> DictIsotopeModNames { get; private set; }
        public static Dictionary<string, StaticMod> DictHiddenIsotopeModNames { get; private set; }
        public static Dictionary<UniModIdKey, StaticMod> DictUniModIds { get; private set; }
        public static ModMassLookup MassLookup { get; private set; }

        public static readonly char[] AMINO_ACIDS = 
            {
                'A', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'K', 'L', 'M', 'N', 
                'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'Y'
            };
        private static readonly bool INITIALIZING;

        static UniMod()
        {
            DictStructuralModNames = new Dictionary<string, StaticMod>();
            DictHiddenStructuralModNames = new Dictionary<string, StaticMod>();
            DictIsotopeModNames = new Dictionary<string, StaticMod>();
            DictHiddenIsotopeModNames = new Dictionary<string, StaticMod>();
            DictUniModIds = new Dictionary<UniModIdKey, StaticMod>();
            MassLookup = new ModMassLookup();

            INITIALIZING = true;
            
            AddMod("Acetyl (K)", "K", null, LabelAtoms.None, "H2C2O", null, 1, true, false);
            AddMod("Acetyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H2C2O", null, 1, true, false);
            AddMod("Amidated (C-term)", null, ModTerminus.C, LabelAtoms.None, "HN - O", null, 2, true, false);
            AddMod("Ammonia-loss (N-term C)", "C", ModTerminus.N, LabelAtoms.None, "-H3N", null, 385, true, false);
            AddMod("Biotin (K)", "K", null, LabelAtoms.None, "H14C10N2O2S", null, 3, true, false);
            AddMod("Biotin (N-term)", null, ModTerminus.N, LabelAtoms.None, "H14C10N2O2S", null, 3, true, false);
            AddMod("Carbamidomethyl (C)", "C", null, LabelAtoms.None, "H3C2NO", null, 4, true, false);
            AddMod("Carbamyl (K)", "K", null, LabelAtoms.None, "HCNO", null, 5, true, false);
            AddMod("Carbamyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "HCNO", null, 5, true, false);
            AddMod("Carboxymethyl (C)", "C", null, LabelAtoms.None, "H2C2O2", null, 6, true, false);
            AddMod("Cation:Na (C-term)", null, ModTerminus.C, LabelAtoms.None, "Na - H", null, 30, true, false);
            AddMod("Cation:Na (DE)", "D, E", null, LabelAtoms.None, "Na - H", null, 30, true, false);
            AddMod("cysTMT6plex (C)", "C", null, LabelAtoms.None, "H25C10C'4N2N'O2S", null, 985, true, false);
            AddMod("Deamidated (NQ)", "N, Q", null, LabelAtoms.None, "O - HN", null, 7, true, false);
            AddMod("Dehydrated (N-term C)", "C", ModTerminus.N, LabelAtoms.None, "-H2O", null, 23, true, false);
            AddMod("Dehydro (C)", "C", null, LabelAtoms.None, "-H", null, 374, true, false);
            AddMod("Dioxidation (M)", "M", null, LabelAtoms.None, "O2", null, 425, true, false);
            AddMod("Ethanolyl (C)", "C", null, LabelAtoms.None, "H4C2O", null, 278, true, false);
            AddMod("ExacTagAmine (K)", "K", null, LabelAtoms.None, "H52C25C'12N8N'6O19S", null, 741, true, false);
            AddMod("ExacTagThiol (C)", "C", null, LabelAtoms.None, "H50C23C'12N8N'6O18", null, 740, true, false);
            AddMod("Formyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "CO", null, 122, true, false);
            AddMod("Gln->pyro-Glu (N-term Q)", "Q", ModTerminus.N, LabelAtoms.None, "-H3N", null, 28, true, false);
            AddMod("Glu->pyro-Glu (N-term E)", "E", ModTerminus.N, LabelAtoms.None, "-H2O", null, 27, true, false);
            AddMod("Guanidinyl (K)", "K", null, LabelAtoms.None, "H2CN2", null, 52, true, false);
            AddMod("ICAT-C (C)", "C", null, LabelAtoms.None, "H17C10N3O3", null, 105, true, false);
            AddMod("ICAT-C:13C(9) (C)", "C", null, LabelAtoms.None, "H17CC'9N3O3", null, 106, true, false);
            AddMod("ICPL (K)", "K", null, LabelAtoms.None, "H3C6NO", null, 365, true, false);
            AddMod("ICPL:13C(6) (K)", "K", null, LabelAtoms.None, "H3C'6NO", null, 364, true, false);
            AddMod("ICPL:13C(6)2H(4) (K)", "K", null, LabelAtoms.None, "H'4C'6NO - H", null, 866, true, false);
            AddMod("ICPL:13C(6)2H(4) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H'4C'6NO - H", null, 866, true, false);
            AddMod("ICPL:2H(4) (K)", "K", null, LabelAtoms.None, "H'4C6NO - H", null, 687, true, false);
            AddMod("iTRAQ4plex (K)", "K", null, LabelAtoms.None, "H12C4C'3NN'O", null, 214, true, false);
            AddMod("iTRAQ4plex (N-term)", null, ModTerminus.N, LabelAtoms.None, "H12C4C'3NN'O", null, 214, true, false);
            AddMod("iTRAQ4plex (Y)", "Y", null, LabelAtoms.None, "H12C4C'3NN'O", null, 214, true, false);
            AddMod("iTRAQ8plex (K)", "K", null, LabelAtoms.None, "H24C7C'7N3N'O3", null, 730, true, false);
            AddMod("iTRAQ8plex (N-term)", null, ModTerminus.N, LabelAtoms.None, "H24C7C'7N3N'O3", null, 730, true, false);
            AddMod("iTRAQ8plex (Y)", "Y", null, LabelAtoms.None, "H24C7C'7N3N'O3", null, 730, true, false);
            AddMod("Met->Hse (C-term M)", "M", ModTerminus.C, LabelAtoms.None, "O - H2CS", null, 10, true, false);
            AddMod("Met->Hsl (C-term M)", "M", ModTerminus.C, LabelAtoms.None, "-H4CS", null, 11, true, false);
            AddMod("Methyl (C-term)", null, ModTerminus.C, LabelAtoms.None, "H2C", null, 34, true, false);
            AddMod("Methyl (DE)", "D, E", null, LabelAtoms.None, "H2C", null, 34, true, false);
            AddMod("Methylthio (C)", "C", null, LabelAtoms.None, "H2CS", null, 39, true, false);
            AddMod("mTRAQ (K)", "K", null, LabelAtoms.None, "H12C7N2O", null, 888, true, false);
            AddMod("mTRAQ (N-term)", null, ModTerminus.N, LabelAtoms.None, "H12C7N2O", null, 888, true, false);
            AddMod("mTRAQ (Y)", "Y", null, LabelAtoms.None, "H12C7N2O", null, 888, true, false);
            AddMod("mTRAQ:13C(3)15N(1) (K)", "K", null, LabelAtoms.None, "H12C4C'3NN'O", null, 889, true, false);
            AddMod("mTRAQ:13C(3)15N(1) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H12C4C'3NN'O", null, 889, true, false);
            AddMod("mTRAQ:13C(3)15N(1) (Y)", "Y", null, LabelAtoms.None, "H12C4C'3NN'O", null, 889, true, false);
            AddMod("NIPCAM (C)", "C", null, LabelAtoms.None, "H9C5NO", null, 17, true, false);
            AddMod("Oxidation (HW)", "H, W", null, LabelAtoms.None, "O", null, 35, true, false);
            AddMod("Oxidation (M)", "M", null, LabelAtoms.None, "O", new [] { new FragmentLoss("H4COS"), }, 35, true, false);
            AddMod("Phospho (ST)", "S, T", null, LabelAtoms.None, "HO3P", new [] { new FragmentLoss("H3O4P"), }, 21, true, false);
            AddMod("Phospho (Y)", "Y", null, LabelAtoms.None, "HO3P", null, 21, true, false);
            AddMod("Propionamide (C)", "C", null, LabelAtoms.None, "H5C3NO", null, 24, true, false);
            AddMod("Pyridylethyl (C)", "C", null, LabelAtoms.None, "H7C7N", null, 31, true, false);
            AddMod("Pyro-carbamidomethyl (N-term C)", "C", ModTerminus.N, LabelAtoms.None, "C2O", null, 26, true, false);
            AddMod("Sulfo (S)", "S", null, LabelAtoms.None, "O3S", null, 40, true, false);
            AddMod("Sulfo (T)", "T", null, LabelAtoms.None, "O3S", null, 40, true, false);
            AddMod("Sulfo (Y)", "Y", null, LabelAtoms.None, "O3S", null, 40, true, false);
            AddMod("TMT (K)", "K", null, LabelAtoms.None, "H20C12N2O2", null, 739, true, false);
            AddMod("TMT (N-term)", null, ModTerminus.N, LabelAtoms.None, "H20C12N2O2", null, 739, true, false);
            AddMod("TMT2plex (K)", "K", null, LabelAtoms.None, "H20C11C'N2O2", null, 738, true, false);
            AddMod("TMT2plex (N-term)", null, ModTerminus.N, LabelAtoms.None, "H20C11C'N2O2", null, 738, true, false);
            AddMod("TMT6plex (K)", "K", null, LabelAtoms.None, "H20C8C'4NN'O2", null, 737, true, false);
            AddMod("TMT6plex (N-term)", null, ModTerminus.N, LabelAtoms.None, "H20C8C'4NN'O2", null, 737, true, false);
            AddMod("Label:18O(1) (C-term)", null, ModTerminus.C, LabelAtoms.None, "O' - O", null, 258, false, false);
            AddMod("Label:18O(2) (C-term)", null, ModTerminus.C, LabelAtoms.None, "O'2 - O2", null, 193, false, false);
            AddMod("15dB-biotin (C)", "C", null, LabelAtoms.None, "H54C35N4O4S", null, 538, true, true);
            AddMod("2-succinyl (C)", "C", null, LabelAtoms.None, "H4C4O4", null, 957, true, true);
            AddMod("2HPG (R)", "R", null, LabelAtoms.None, "H10C16O5", null, 187, true, true);
            AddMod("3-deoxyglucosone (R)", "R", null, LabelAtoms.None, "H8C6O4", null, 949, true, true);
            AddMod("3sulfo (N-term)", null, ModTerminus.N, LabelAtoms.None, "H4C7O4S", null, 748, true, true);
            AddMod("4-ONE (C)", "C", null, LabelAtoms.None, "H14C9O2", null, 721, true, true);
            AddMod("4-ONE (H)", "H", null, LabelAtoms.None, "H14C9O2", null, 721, true, true);
            AddMod("4-ONE (K)", "K", null, LabelAtoms.None, "H14C9O2", null, 721, true, true);
            AddMod("4-ONE+Delta:H(-2)O(-1) (C)", "C", null, LabelAtoms.None, "H12C9O", null, 743, true, true);
            AddMod("4-ONE+Delta:H(-2)O(-1) (H)", "H", null, LabelAtoms.None, "H12C9O", null, 743, true, true);
            AddMod("4-ONE+Delta:H(-2)O(-1) (K)", "K", null, LabelAtoms.None, "H12C9O", null, 743, true, true);
            AddMod("4AcAllylGal (C)", "C", null, LabelAtoms.None, "H24C17O9", null, 901, true, true);
            AddMod("a-type-ion (C-term)", null, ModTerminus.C, LabelAtoms.None, "-H2CO2", null, 140, true, true);
            AddMod("AccQTag (K)", "K", null, LabelAtoms.None, "H6C10N2O", null, 194, true, true);
            AddMod("AccQTag (N-term)", null, ModTerminus.N, LabelAtoms.None, "H6C10N2O", null, 194, true, true);
            AddMod("Acetyl (C)", "C", null, LabelAtoms.None, "H2C2O", null, 1, true, true);
            AddMod("Acetyl (H)", "H", null, LabelAtoms.None, "H2C2O", null, 1, true, true);
            AddMod("Acetyl (S)", "S", null, LabelAtoms.None, "H2C2O", null, 1, true, true);
            AddMod("Acetyl (T)", "T", null, LabelAtoms.None, "H2C2O", null, 1, true, true);
            AddMod("Acetyl (Y)", "Y", null, LabelAtoms.None, "H2C2O", null, 1, true, true);
            AddMod("Acetyl:2H(3) (K)", "K", null, LabelAtoms.None, "H'3C2O - H", null, 56, true, true);
            AddMod("Acetyl:2H(3) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H'3C2O - H", null, 56, true, true);
            AddMod("ADP-Ribosyl (C)", "C", null, LabelAtoms.None, "H21C15N5O13P2", null, 213, true, true);
            AddMod("ADP-Ribosyl (E)", "E", null, LabelAtoms.None, "H21C15N5O13P2", null, 213, true, true);
            AddMod("ADP-Ribosyl (N)", "N", null, LabelAtoms.None, "H21C15N5O13P2", null, 213, true, true);
            AddMod("ADP-Ribosyl (R)", "R", null, LabelAtoms.None, "H21C15N5O13P2", null, 213, true, true);
            AddMod("ADP-Ribosyl (S)", "S", null, LabelAtoms.None, "H21C15N5O13P2", null, 213, true, true);
            AddMod("AEBS (H)", "H", null, LabelAtoms.None, "H9C8NO2S", null, 276, true, true);
            AddMod("AEBS (K)", "K", null, LabelAtoms.None, "H9C8NO2S", null, 276, true, true);
            AddMod("AEBS (S)", "S", null, LabelAtoms.None, "H9C8NO2S", null, 276, true, true);
            AddMod("AEBS (Y)", "Y", null, LabelAtoms.None, "H9C8NO2S", null, 276, true, true);
            AddMod("AEC-MAEC (S)", "S", null, LabelAtoms.None, "H5C2NS - O", null, 472, true, true);
            AddMod("AEC-MAEC (T)", "T", null, LabelAtoms.None, "H5C2NS - O", null, 472, true, true);
            AddMod("AEC-MAEC:2H(4) (S)", "S", null, LabelAtoms.None, "HH'4C2NS - O", null, 792, true, true);
            AddMod("AEC-MAEC:2H(4) (T)", "T", null, LabelAtoms.None, "HH'4C2NS - O", null, 792, true, true);
            AddMod("AHA-Alkyne (M)", "M", null, LabelAtoms.None, "H5C4N5O - S", null, 1000, true, true);
            AddMod("AHA-Alkyne-KDDDD (M)", "M", null, LabelAtoms.None, "H37C26N11O14 - S", null, 1001, true, true);
            AddMod("Amidine (K)", "K", null, LabelAtoms.None, "H3C2N", null, 141, true, true);
            AddMod("Amidine (N-term)", null, ModTerminus.N, LabelAtoms.None, "H3C2N", null, 141, true, true);
            AddMod("Amidino (C)", "C", null, LabelAtoms.None, "H2CN2", null, 440, true, true);
            AddMod("Amino (Y)", "Y", null, LabelAtoms.None, "HN", null, 342, true, true);
            AddMod("Ammonia-loss (N)", "N", null, LabelAtoms.None, "-H3N", null, 385, true, true);
            AddMod("Ammonia-loss (Protein N-term S)", "S", ModTerminus.N, LabelAtoms.None, "-H3N", null, 385, true, true);
            AddMod("Ammonia-loss (Protein N-term T)", "T", ModTerminus.N, LabelAtoms.None, "-H3N", null, 385, true, true);
            AddMod("Ammonium (C-term)", null, ModTerminus.C, LabelAtoms.None, "H3N", null, 989, true, true);
            AddMod("Ammonium (DE)", "D, E", null, LabelAtoms.None, "H3N", null, 989, true, true);
            AddMod("AMTzHexNAc2 (N)", "N", null, LabelAtoms.None, "H30C19N6O10", null, 934, true, true);
            AddMod("AMTzHexNAc2 (S)", "S", null, LabelAtoms.None, "H30C19N6O10", null, 934, true, true);
            AddMod("AMTzHexNAc2 (T)", "T", null, LabelAtoms.None, "H30C19N6O10", null, 934, true, true);
            AddMod("Archaeol (C)", "C", null, LabelAtoms.None, "H86C43O2", null, 410, true, true);
            AddMod("Arg->GluSA (R)", "R", null, LabelAtoms.None, "O - H5CN3", null, 344, true, true);
            AddMod("Arg->Npo (R)", "R", null, LabelAtoms.None, "C3NO2 - H", null, 837, true, true);
            AddMod("Arg->Orn (R)", "R", null, LabelAtoms.None, "-H2CN2", null, 372, true, true);
            AddMod("Arg2PG (R)", "R", null, LabelAtoms.None, "H10C16O4", null, 848, true, true);
            AddMod("Argbiotinhydrazide (R)", "R", null, LabelAtoms.None, "H13C9NO2S", null, 343, true, true);
            AddMod("AROD (C)", "C", null, LabelAtoms.None, "H52C35N10O9S2", null, 938, true, true);
            AddMod("Atto495Maleimide (C)", "C", null, LabelAtoms.None, "H32C27N5O3", null, 935, true, true);
            AddMod("Bacillosamine (N)", "N", null, LabelAtoms.None, "H16C10N2O4", null, 910, true, true);
            AddMod("BADGE (C)", "C", null, LabelAtoms.None, "H24C21O4", null, 493, true, true);
            AddMod("BDMAPP (H)", "H", null, LabelAtoms.None, "H12C11NOBr", null, 684, true, true);
            AddMod("BDMAPP (K)", "K", null, LabelAtoms.None, "H12C11NOBr", null, 684, true, true);
            AddMod("BDMAPP (W)", "W", null, LabelAtoms.None, "H12C11NOBr", null, 684, true, true);
            AddMod("BDMAPP (Y)", "Y", null, LabelAtoms.None, "H12C11NOBr", null, 684, true, true);
            AddMod("Benzoyl (K)", "K", null, LabelAtoms.None, "H4C7O", null, 136, true, true);
            AddMod("Benzoyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H4C7O", null, 136, true, true);
            AddMod("BHAc (K)", "K", null, LabelAtoms.None, "H25C16N3O3S", null, 998, true, true);
            AddMod("BHT (C)", "C", null, LabelAtoms.None, "H22C15O", null, 176, true, true);
            AddMod("BHT (H)", "H", null, LabelAtoms.None, "H22C15O", null, 176, true, true);
            AddMod("BHT (K)", "K", null, LabelAtoms.None, "H22C15O", null, 176, true, true);
            AddMod("BHTOH (C)", "C", null, LabelAtoms.None, "H22C15O2", null, 498, true, true);
            AddMod("BHTOH (H)", "H", null, LabelAtoms.None, "H22C15O2", null, 498, true, true);
            AddMod("BHTOH (K)", "K", null, LabelAtoms.None, "H22C15O2", null, 498, true, true);
            AddMod("Biotin-HPDP (C)", "C", null, LabelAtoms.None, "H32C19N4O3S2", null, 290, true, true);
            AddMod("Biotin-maleimide (C)", "C", null, LabelAtoms.None, "H27C20N5O5S", null, 993, true, true);
            AddMod("Biotin-PEG-PRA (M)", "M", null, LabelAtoms.None, "H42C26N8O7", null, 895, true, true);
            AddMod("Biotin-PEO-Amine (D)", "D", null, LabelAtoms.None, "H28C16N4O3S", null, 289, true, true);
            AddMod("Biotin-PEO-Amine (E)", "E", null, LabelAtoms.None, "H28C16N4O3S", null, 289, true, true);
            AddMod("Biotin-PEO4-hydrazide (C-term)", null, ModTerminus.C, LabelAtoms.None, "H37C21N5O6S", null, 811, true, true);
            AddMod("Biotin-phenacyl (C)", "C", null, LabelAtoms.None, "H38C29N8O6S", null, 774, true, true);
            AddMod("Biotin-phenacyl (H)", "H", null, LabelAtoms.None, "H38C29N8O6S", null, 774, true, true);
            AddMod("Biotin-phenacyl (S)", "S", null, LabelAtoms.None, "H38C29N8O6S", null, 774, true, true);
            AddMod("BisANS (K)", "K", null, LabelAtoms.None, "H20C32N2O6S2", null, 519, true, true);
            AddMod("BITC (C)", "C", null, LabelAtoms.None, "H7C8NS", null, 978, true, true);
            AddMod("BITC (K)", "K", null, LabelAtoms.None, "H7C8NS", null, 978, true, true);
            AddMod("BITC (N-term)", null, ModTerminus.N, LabelAtoms.None, "H7C8NS", null, 978, true, true);
            AddMod("BMOE (C)", "C", null, LabelAtoms.None, "H8C10N2O4", null, 824, true, true);
            AddMod("Bodipy (C)", "C", null, LabelAtoms.None, "H21C20N4O3F2B", null, 878, true, true);
            AddMod("Bromo (F)", "F", null, LabelAtoms.None, "Br - H", null, 340, true, true);
            AddMod("Bromo (H)", "H", null, LabelAtoms.None, "Br - H", null, 340, true, true);
            AddMod("Bromo (W)", "W", null, LabelAtoms.None, "Br - H", null, 340, true, true);
            AddMod("Bromobimane (C)", "C", null, LabelAtoms.None, "H10C10N2O2", null, 301, true, true);
            AddMod("C8-QAT (K)", "K", null, LabelAtoms.None, "H29C14NO", null, 513, true, true);
            AddMod("C8-QAT (N-term)", null, ModTerminus.N, LabelAtoms.None, "H29C14NO", null, 513, true, true);
            AddMod("CAF (N-term)", null, ModTerminus.N, LabelAtoms.None, "H4C3O4S", null, 272, true, true);
            AddMod("CAMthiopropanoyl (K)", "K", null, LabelAtoms.None, "H7C5NO2S", null, 293, true, true);
            AddMod("Can-FP-biotin (S)", "S", null, LabelAtoms.None, "H34C19N3O5PS", null, 333, true, true);
            AddMod("Can-FP-biotin (T)", "T", null, LabelAtoms.None, "H34C19N3O5PS", null, 333, true, true);
            AddMod("Can-FP-biotin (Y)", "Y", null, LabelAtoms.None, "H34C19N3O5PS", null, 333, true, true);
            AddMod("Carbamidomethyl (D)", "D", null, LabelAtoms.None, "H3C2NO", null, 4, true, true);
            AddMod("Carbamidomethyl (E)", "E", null, LabelAtoms.None, "H3C2NO", null, 4, true, true);
            AddMod("Carbamidomethyl (H)", "H", null, LabelAtoms.None, "H3C2NO", null, 4, true, true);
            AddMod("Carbamidomethyl (K)", "K", null, LabelAtoms.None, "H3C2NO", null, 4, true, true);
            AddMod("Carbamidomethyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H3C2NO", null, 4, true, true);
            AddMod("CarbamidomethylDTT (C)", "C", null, LabelAtoms.None, "H11C6NO3S2", null, 893, true, true);
            AddMod("Carbamyl (C)", "C", null, LabelAtoms.None, "HCNO", null, 5, true, true);
            AddMod("Carbamyl (M)", "M", null, LabelAtoms.None, "HCNO", null, 5, true, true);
            AddMod("Carbamyl (R)", "R", null, LabelAtoms.None, "HCNO", null, 5, true, true);
            AddMod("Carbamyl (S)", "S", null, LabelAtoms.None, "HCNO", null, 5, true, true);
            AddMod("Carbamyl (T)", "T", null, LabelAtoms.None, "HCNO", null, 5, true, true);
            AddMod("Carbamyl (Y)", "Y", null, LabelAtoms.None, "HCNO", null, 5, true, true);
            AddMod("Carbofuran (S)", "S", null, LabelAtoms.None, "H4C2NO", null, 977, true, true);
            AddMod("Carboxy (D)", "D", null, LabelAtoms.None, "CO2", null, 299, true, true);
            AddMod("Carboxy (E)", "E", null, LabelAtoms.None, "CO2", null, 299, true, true);
            AddMod("Carboxy (K)", "K", null, LabelAtoms.None, "CO2", null, 299, true, true);
            AddMod("Carboxy (Protein N-term M)", "M", ModTerminus.N, LabelAtoms.None, "CO2", null, 299, true, true);
            AddMod("Carboxy (W)", "W", null, LabelAtoms.None, "CO2", null, 299, true, true);
            AddMod("Carboxy->Thiocarboxy (Protein C-term G)", "G", ModTerminus.C, LabelAtoms.None, "S - O", null, 420, true, true);
            AddMod("Carboxyethyl (K)", "K", null, LabelAtoms.None, "H4C3O2", null, 378, true, true);
            AddMod("Carboxymethyl (K)", "K", null, LabelAtoms.None, "H2C2O2", null, 6, true, true);
            AddMod("Carboxymethyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H2C2O2", null, 6, true, true);
            AddMod("Carboxymethyl (W)", "W", null, LabelAtoms.None, "H2C2O2", null, 6, true, true);
            AddMod("Carboxymethyl:13C(2) (C)", "C", null, LabelAtoms.None, "H2C'2O2", null, 775, true, true);
            AddMod("CarboxymethylDTT (C)", "C", null, LabelAtoms.None, "H10C6O4S2", null, 894, true, true);
            AddMod("Cation:Ag (C-term)", null, ModTerminus.C, LabelAtoms.None, "Ag - H", null, 955, true, true);
            AddMod("Cation:Ag (DE)", "D, E", null, LabelAtoms.None, "Ag - H", null, 955, true, true);
            AddMod("Cation:Ca[II] (C-term)", null, ModTerminus.C, LabelAtoms.None, "Ca - H2", null, 951, true, true);
            AddMod("Cation:Ca[II] (DE)", "D, E", null, LabelAtoms.None, "Ca - H2", null, 951, true, true);
            AddMod("Cation:Cu[I] (C-term)", null, ModTerminus.C, LabelAtoms.None, "Cu - H", null, 531, true, true);
            AddMod("Cation:Cu[I] (DE)", "D, E", null, LabelAtoms.None, "Cu - H", null, 531, true, true);
            AddMod("Cation:Fe[II] (C-term)", null, ModTerminus.C, LabelAtoms.None, "Fe - H2", null, 952, true, true);
            AddMod("Cation:Fe[II] (DE)", "D, E", null, LabelAtoms.None, "Fe - H2", null, 952, true, true);
            AddMod("Cation:K (C-term)", null, ModTerminus.C, LabelAtoms.None, "K - H", null, 530, true, true);
            AddMod("Cation:K (DE)", "D, E", null, LabelAtoms.None, "K - H", null, 530, true, true);
            AddMod("Cation:Li (C-term)", null, ModTerminus.C, LabelAtoms.None, "Li - H", null, 950, true, true);
            AddMod("Cation:Li (DE)", "D, E", null, LabelAtoms.None, "Li - H", null, 950, true, true);
            AddMod("Cation:Mg[II] (C-term)", null, ModTerminus.C, LabelAtoms.None, "Mg - H2", null, 956, true, true);
            AddMod("Cation:Mg[II] (DE)", "D, E", null, LabelAtoms.None, "Mg - H2", null, 956, true, true);
            AddMod("Cation:Ni[II] (C-term)", null, ModTerminus.C, LabelAtoms.None, "Ni - H2", null, 953, true, true);
            AddMod("Cation:Ni[II] (DE)", "D, E", null, LabelAtoms.None, "Ni - H2", null, 953, true, true);
            AddMod("Cation:Zn[II] (C-term)", null, ModTerminus.C, LabelAtoms.None, "Zn - H2", null, 954, true, true);
            AddMod("Cation:Zn[II] (DE)", "D, E", null, LabelAtoms.None, "Zn - H2", null, 954, true, true);
            AddMod("cGMP (C)", "C", null, LabelAtoms.None, "H11C10N5O7P", null, 849, true, true);
            AddMod("cGMP (S)", "S", null, LabelAtoms.None, "H11C10N5O7P", null, 849, true, true);
            AddMod("cGMP+RMP-loss (C)", "C", null, LabelAtoms.None, "H4C5N5O", null, 851, true, true);
            AddMod("cGMP+RMP-loss (S)", "S", null, LabelAtoms.None, "H4C5N5O", null, 851, true, true);
            AddMod("CHDH (D)", "D", null, LabelAtoms.None, "H26C17O4", null, 434, true, true);
            AddMod("Chlorination (Y)", "Y", null, LabelAtoms.None, "Cl", null, 936, true, true);
            AddMod("Chlorpyrifos (S)", "S", null, LabelAtoms.None, "H10C4O2PS", null, 975, true, true);
            AddMod("Chlorpyrifos (T)", "T", null, LabelAtoms.None, "H10C4O2PS", null, 975, true, true);
            AddMod("Chlorpyrifos (Y)", "Y", null, LabelAtoms.None, "H10C4O2PS", null, 975, true, true);
            AddMod("ChromoBiotin (K)", "K", null, LabelAtoms.None, "H45C34N7O7S", null, 884, true, true);
            AddMod("CLIP_TRAQ_1 (K)", "K", null, LabelAtoms.None, "H12C7N2O", null, 524, true, true);
            AddMod("CLIP_TRAQ_1 (N-term)", null, ModTerminus.N, LabelAtoms.None, "H12C7N2O", null, 524, true, true);
            AddMod("CLIP_TRAQ_1 (Y)", "Y", null, LabelAtoms.None, "H12C7N2O", null, 524, true, true);
            AddMod("CLIP_TRAQ_2 (K)", "K", null, LabelAtoms.None, "H12C6C'N2O", null, 525, true, true);
            AddMod("CLIP_TRAQ_2 (N-term)", null, ModTerminus.N, LabelAtoms.None, "H12C6C'N2O", null, 525, true, true);
            AddMod("CLIP_TRAQ_2 (Y)", "Y", null, LabelAtoms.None, "H12C6C'N2O", null, 525, true, true);
            AddMod("CLIP_TRAQ_3 (K)", "K", null, LabelAtoms.None, "H20C11C'N3O4", null, 536, true, true);
            AddMod("CLIP_TRAQ_3 (N-term)", null, ModTerminus.N, LabelAtoms.None, "H20C11C'N3O4", null, 536, true, true);
            AddMod("CLIP_TRAQ_3 (Y)", "Y", null, LabelAtoms.None, "H20C11C'N3O4", null, 536, true, true);
            AddMod("CLIP_TRAQ_4 (K)", "K", null, LabelAtoms.None, "H15C9C'N2O5", null, 537, true, true);
            AddMod("CLIP_TRAQ_4 (N-term)", null, ModTerminus.N, LabelAtoms.None, "H15C9C'N2O5", null, 537, true, true);
            AddMod("CLIP_TRAQ_4 (Y)", "Y", null, LabelAtoms.None, "H15C9C'N2O5", null, 537, true, true);
            AddMod("CoenzymeA (C)", "C", null, LabelAtoms.None, "H34C21N7O16P3S", null, 281, true, true);
            AddMod("Crotonaldehyde (C)", "C", null, LabelAtoms.None, "H6C4O", null, 253, true, true);
            AddMod("Crotonaldehyde (H)", "H", null, LabelAtoms.None, "H6C4O", null, 253, true, true);
            AddMod("Crotonaldehyde (K)", "K", null, LabelAtoms.None, "H6C4O", null, 253, true, true);
            AddMod("CuSMo (C)", "C", null, LabelAtoms.None, "H24C19N8O15P2S3MoCu", null, 444, true, true);
            AddMod("Cy3b-maleimide (C)", "C", null, LabelAtoms.None, "H39C39N4O9F3S", null, 821, true, true);
            AddMod("Cyano (C)", "C", null, LabelAtoms.None, "CN - H", null, 438, true, true);
            AddMod("CyDye-Cy3 (C)", "C", null, LabelAtoms.None, "H44C37N4O6S", null, 494, true, true);
            AddMod("CyDye-Cy5 (C)", "C", null, LabelAtoms.None, "H44C38N4O6S", null, 495, true, true);
            AddMod("Cys->Dha (C)", "C", null, LabelAtoms.None, "-H2S", null, 368, true, true);
            AddMod("Cys->ethylaminoAla (C)", "C", null, LabelAtoms.None, "H5C2N - S", null, 940, true, true);
            AddMod("Cys->methylaminoAla (C)", "C", null, LabelAtoms.None, "H3CN - S", null, 939, true, true);
            AddMod("Cys->Oxoalanine (C)", "C", null, LabelAtoms.None, "O - H2S", null, 402, true, true);
            AddMod("Cys->PyruvicAcid (Protein N-term C)", "C", ModTerminus.N, LabelAtoms.None, "O - H3NS", null, 382, true, true);
            AddMod("Cysteinyl (C)", "C", null, LabelAtoms.None, "H5C3NO2S", null, 312, true, true);
            AddMod("cysTMT (C)", "C", null, LabelAtoms.None, "H25C14N3O2S", null, 984, true, true);
            AddMod("Cytopiloyne (C)", "C", null, LabelAtoms.None, "H22C19O7", null, 270, true, true);
            AddMod("Cytopiloyne (K)", "K", null, LabelAtoms.None, "H22C19O7", null, 270, true, true);
            AddMod("Cytopiloyne (N-term)", null, ModTerminus.N, LabelAtoms.None, "H22C19O7", null, 270, true, true);
            AddMod("Cytopiloyne (P)", "P", null, LabelAtoms.None, "H22C19O7", null, 270, true, true);
            AddMod("Cytopiloyne (R)", "R", null, LabelAtoms.None, "H22C19O7", null, 270, true, true);
            AddMod("Cytopiloyne (S)", "S", null, LabelAtoms.None, "H22C19O7", null, 270, true, true);
            AddMod("Cytopiloyne (Y)", "Y", null, LabelAtoms.None, "H22C19O7", null, 270, true, true);
            AddMod("Cytopiloyne+water (C)", "C", null, LabelAtoms.None, "H24C19O8", null, 271, true, true);
            AddMod("Cytopiloyne+water (K)", "K", null, LabelAtoms.None, "H24C19O8", null, 271, true, true);
            AddMod("Cytopiloyne+water (N-term)", null, ModTerminus.N, LabelAtoms.None, "H24C19O8", null, 271, true, true);
            AddMod("Cytopiloyne+water (R)", "R", null, LabelAtoms.None, "H24C19O8", null, 271, true, true);
            AddMod("Cytopiloyne+water (S)", "S", null, LabelAtoms.None, "H24C19O8", null, 271, true, true);
            AddMod("Cytopiloyne+water (T)", "T", null, LabelAtoms.None, "H24C19O8", null, 271, true, true);
            AddMod("Cytopiloyne+water (Y)", "Y", null, LabelAtoms.None, "H24C19O8", null, 271, true, true);
            AddMod("DAET (S)", "S", null, LabelAtoms.None, "H9C4NS - O", null, 178, true, true);
            AddMod("DAET (T)", "T", null, LabelAtoms.None, "H9C4NS - O", null, 178, true, true);
            AddMod("Dansyl (K)", "K", null, LabelAtoms.None, "H11C12NO2S", null, 139, true, true);
            AddMod("Dansyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H11C12NO2S", null, 139, true, true);
            AddMod("Deamidated (Protein N-term F)", "F", ModTerminus.N, LabelAtoms.None, "O - HN", null, 7, true, true);
            AddMod("Deamidated (R)", "R", null, LabelAtoms.None, "O - HN", null, 7, true, true);
            AddMod("Deamidated:18O(1) (NQ)", "N, Q", null, LabelAtoms.None, "O' - HN", null, 366, true, true);
            AddMod("Decanoyl (S)", "S", null, LabelAtoms.None, "H18C10O", null, 449, true, true);
            AddMod("Decanoyl (T)", "T", null, LabelAtoms.None, "H18C10O", null, 449, true, true);
            AddMod("DEDGFLYMVYASQETFG (K)", "K", null, LabelAtoms.None, "H122C89N18O31S", new [] { new FragmentLoss("H2O"), }, 1010, true, true);
            AddMod("Dehydrated (D)", "D", null, LabelAtoms.None, "-H2O", null, 23, true, true);
            AddMod("Dehydrated (Protein C-term N)", "N", ModTerminus.C, LabelAtoms.None, "-H2O", null, 23, true, true);
            AddMod("Dehydrated (Protein C-term Q)", "Q", ModTerminus.C, LabelAtoms.None, "-H2O", null, 23, true, true);
            AddMod("Dehydrated (S)", "S", null, LabelAtoms.None, "-H2O", null, 23, true, true);
            AddMod("Dehydrated (T)", "T", null, LabelAtoms.None, "-H2O", null, 23, true, true);
            AddMod("Dehydrated (Y)", "Y", null, LabelAtoms.None, "-H2O", null, 23, true, true);
            AddMod("Delta:H(1)O(-1)18O(1) (N)", "N", null, LabelAtoms.None, "O' - HN", null, 170, true, true);
            AddMod("Delta:H(2)C(2) (H)", "H", null, LabelAtoms.None, "H2C2", null, 254, true, true);
            AddMod("Delta:H(2)C(2) (K)", "K", null, LabelAtoms.None, "H2C2", null, 254, true, true);
            AddMod("Delta:H(2)C(3) (K)", "K", null, LabelAtoms.None, "H2C3", null, 207, true, true);
            AddMod("Delta:H(2)C(3)O(1) (K)", "K", null, LabelAtoms.None, "H2C3O", null, 319, true, true);
            AddMod("Delta:H(2)C(3)O(1) (R)", "R", null, LabelAtoms.None, "H2C3O", null, 319, true, true);
            AddMod("Delta:H(2)C(5) (K)", "K", null, LabelAtoms.None, "H2C5", null, 318, true, true);
            AddMod("Delta:H(4)C(2) (H)", "H", null, LabelAtoms.None, "H4C2", null, 255, true, true);
            AddMod("Delta:H(4)C(2) (K)", "K", null, LabelAtoms.None, "H4C2", null, 255, true, true);
            AddMod("Delta:H(4)C(2)O(-1)S(1) (S)", "S", null, LabelAtoms.None, "H4C2S - O", null, 327, true, true);
            AddMod("Delta:H(4)C(3) (H)", "H", null, LabelAtoms.None, "H4C3", null, 256, true, true);
            AddMod("Delta:H(4)C(3) (K)", "K", null, LabelAtoms.None, "H4C3", null, 256, true, true);
            AddMod("Delta:H(4)C(3)O(1) (C)", "C", null, LabelAtoms.None, "H4C3O", null, 206, true, true);
            AddMod("Delta:H(4)C(3)O(1) (H)", "H", null, LabelAtoms.None, "H4C3O", null, 206, true, true);
            AddMod("Delta:H(4)C(3)O(1) (K)", "K", null, LabelAtoms.None, "H4C3O", null, 206, true, true);
            AddMod("Delta:H(4)C(6) (K)", "K", null, LabelAtoms.None, "H4C6", null, 208, true, true);
            AddMod("Delta:H(5)C(2) (P)", "P", null, LabelAtoms.None, "H5C2", null, 529, true, true);
            AddMod("Delta:H(6)C(6)O(1) (K)", "K", null, LabelAtoms.None, "H6C6O", null, 205, true, true);
            AddMod("Delta:H(8)C(6)O(2) (K)", "K", null, LabelAtoms.None, "H8C6O2", null, 209, true, true);
            AddMod("Delta:Hg(1) (C)", "C", null, LabelAtoms.None, "Hg", null, 291, true, true);
            AddMod("Delta:S(-1)Se(1) (C)", "C", null, LabelAtoms.None, "Se - S", null, 162, true, true);
            AddMod("Delta:S(-1)Se(1) (M)", "M", null, LabelAtoms.None, "Se - S", null, 162, true, true);
            AddMod("Delta:Se(1) (C)", "C", null, LabelAtoms.None, "Se", null, 423, true, true);
            AddMod("Deoxy (D)", "D", null, LabelAtoms.None, "-O", null, 447, true, true);
            AddMod("Deoxy (S)", "S", null, LabelAtoms.None, "-O", null, 447, true, true);
            AddMod("Deoxy (T)", "T", null, LabelAtoms.None, "-O", null, 447, true, true);
            AddMod("DeStreak (C)", "C", null, LabelAtoms.None, "H4C2OS", null, 303, true, true);
            AddMod("Dethiomethyl (M)", "M", null, LabelAtoms.None, "-H4CS", null, 526, true, true);
            AddMod("DFDNB (K)", "K", null, LabelAtoms.None, "H2C6N2O4F2", null, 825, true, true);
            AddMod("DFDNB (N)", "N", null, LabelAtoms.None, "H2C6N2O4F2", null, 825, true, true);
            AddMod("DFDNB (Q)", "Q", null, LabelAtoms.None, "H2C6N2O4F2", null, 825, true, true);
            AddMod("DFDNB (R)", "R", null, LabelAtoms.None, "H2C6N2O4F2", null, 825, true, true);
            AddMod("dHex (S)", "S", null, LabelAtoms.None, "H10C6O4", null, 295, true, true);
            AddMod("dHex (T)", "T", null, LabelAtoms.None, "H10C6O4", null, 295, true, true);
            AddMod("dHex(1)Hex(3)HexNAc(4) (N)", "N", null, LabelAtoms.None, "H92C56N4O39", null, 305, true, true);
            AddMod("dHex(1)Hex(4)HexNAc(4) (N)", "N", null, LabelAtoms.None, "H102C62N4O44", null, 307, true, true);
            AddMod("dHex(1)Hex(5)HexNAc(4) (N)", "N", null, LabelAtoms.None, "H112C68N4O49", null, 308, true, true);
            AddMod("DHP (C)", "C", null, LabelAtoms.None, "H8C8N", null, 488, true, true);
            AddMod("Diacylglycerol (C)", "C", null, LabelAtoms.None, "H68C37O4", null, 377, true, true);
            AddMod("Dibromo (Y)", "Y", null, LabelAtoms.None, "Br2 - H2", null, 534, true, true);
            AddMod("dichlorination (Y)", "Y", null, LabelAtoms.None, "Cl2", null, 937, true, true);
            AddMod("Didehydro (C-term K)", "K", ModTerminus.C, LabelAtoms.None, "-H2", null, 401, true, true);
            AddMod("Didehydro (S)", "S", null, LabelAtoms.None, "-H2", null, 401, true, true);
            AddMod("Didehydro (T)", "T", null, LabelAtoms.None, "-H2", null, 401, true, true);
            AddMod("Didehydro (Y)", "Y", null, LabelAtoms.None, "-H2", null, 401, true, true);
            AddMod("Didehydroretinylidene (K)", "K", null, LabelAtoms.None, "H24C20", null, 433, true, true);
            AddMod("Diethyl (K)", "K", null, LabelAtoms.None, "H8C4", null, 518, true, true);
            AddMod("Diethyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H8C4", null, 518, true, true);
            AddMod("Dihydroxyimidazolidine (R)", "R", null, LabelAtoms.None, "H4C3O2", null, 830, true, true);
            AddMod("Diiodo (Y)", "Y", null, LabelAtoms.None, "I2 - H2", null, 130, true, true);
            AddMod("Diironsubcluster (C)", "C", null, LabelAtoms.None, "C5N2O5S2Fe2 - H", null, 439, true, true);
            AddMod("Diisopropylphosphate (K)", "K", null, LabelAtoms.None, "H13C6O3P", null, 362, true, true);
            AddMod("Diisopropylphosphate (S)", "S", null, LabelAtoms.None, "H13C6O3P", null, 362, true, true);
            AddMod("Diisopropylphosphate (T)", "T", null, LabelAtoms.None, "H13C6O3P", null, 362, true, true);
            AddMod("Diisopropylphosphate (Y)", "Y", null, LabelAtoms.None, "H13C6O3P", null, 362, true, true);
            AddMod("Dimethyl (K)", "K", null, LabelAtoms.None, "H4C2", null, 36, true, true);
            AddMod("Dimethyl (N)", "N", null, LabelAtoms.None, "H4C2", null, 36, true, true);
            AddMod("Dimethyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H4C2", null, 36, true, true);
            AddMod("Dimethyl (Protein N-term P)", "P", ModTerminus.N, LabelAtoms.None, "H4C2", null, 36, true, true);
            AddMod("Dimethyl (R)", "R", null, LabelAtoms.None, "H4C2", null, 36, true, true);
            AddMod("Dimethyl:2H(4) (K)", "K", null, LabelAtoms.None, "H'4C2", null, 199, true, true);
            AddMod("Dimethyl:2H(4) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H'4C2", null, 199, true, true);
            AddMod("Dimethyl:2H(4)13C(2) (K)", "K", null, LabelAtoms.None, "H'4C'2", null, 510, true, true);
            AddMod("Dimethyl:2H(4)13C(2) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H'4C'2", null, 510, true, true);
            AddMod("Dimethyl:2H(6)13C(2) (K)", "K", null, LabelAtoms.None, "H'6C'2 - H2", null, 330, true, true);
            AddMod("Dimethyl:2H(6)13C(2) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H'6C'2 - H2", null, 330, true, true);
            AddMod("Dimethyl:2H(6)13C(2) (R)", "R", null, LabelAtoms.None, "H'6C'2 - H2", null, 330, true, true);
            AddMod("DimethylamineGMBS (C)", "C", null, LabelAtoms.None, "H21C13N3O3", null, 943, true, true);
            AddMod("DimethylArsino (C)", "C", null, LabelAtoms.None, "H5C2As", null, 902, true, true);
            AddMod("DimethylpyrroleAdduct (K)", "K", null, LabelAtoms.None, "H6C6", null, 316, true, true);
            AddMod("Dioxidation (C)", "C", null, LabelAtoms.None, "O2", null, 425, true, true);
            AddMod("Dioxidation (F)", "F", null, LabelAtoms.None, "O2", null, 425, true, true);
            AddMod("Dioxidation (K)", "K", null, LabelAtoms.None, "O2", null, 425, true, true);
            AddMod("Dioxidation (P)", "P", null, LabelAtoms.None, "O2", null, 425, true, true);
            AddMod("Dioxidation (R)", "R", null, LabelAtoms.None, "O2", null, 425, true, true);
            AddMod("Dioxidation (W)", "W", null, LabelAtoms.None, "O2", null, 425, true, true);
            AddMod("Dioxidation (Y)", "Y", null, LabelAtoms.None, "O2", null, 425, true, true);
            AddMod("Diphthamide (H)", "H", null, LabelAtoms.None, "H15C7N2O", null, 375, true, true);
            AddMod("Dipyrrolylmethanemethyl (C)", "C", null, LabelAtoms.None, "H22C20N2O8", null, 416, true, true);
            AddMod("dNIC (N-term)", null, ModTerminus.N, LabelAtoms.None, "HH'3C6NO", null, 698, true, true);
            AddMod("DNPS (C)", "C", null, LabelAtoms.None, "H3C6N2O4S", null, 941, true, true);
            AddMod("DNPS (W)", "W", null, LabelAtoms.None, "H3C6N2O4S", null, 941, true, true);
            AddMod("DTBP (K)", "K", null, LabelAtoms.None, "H5C3NS", null, 324, true, true);
            AddMod("DTBP (N)", "N", null, LabelAtoms.None, "H5C3NS", null, 324, true, true);
            AddMod("DTBP (Q)", "Q", null, LabelAtoms.None, "H5C3NS", null, 324, true, true);
            AddMod("DTBP (R)", "R", null, LabelAtoms.None, "H5C3NS", null, 324, true, true);
            AddMod("DTT_C (C)", "C", null, LabelAtoms.None, "H8C4O2S", null, 736, true, true);
            AddMod("DTT_C:2H(6) (C)", "C", null, LabelAtoms.None, "H2H'6C4O2S", null, 764, true, true);
            AddMod("DTT_ST (S)", "S", null, LabelAtoms.None, "H8C4OS2", null, 735, true, true);
            AddMod("DTT_ST (T)", "T", null, LabelAtoms.None, "H8C4OS2", null, 735, true, true);
            AddMod("DTT_ST:2H(6) (S)", "S", null, LabelAtoms.None, "H2H'6C4OS2", null, 763, true, true);
            AddMod("DTT_ST:2H(6) (T)", "T", null, LabelAtoms.None, "H2H'6C4OS2", null, 763, true, true);
            AddMod("DyLight-maleimide (C)", "C", null, LabelAtoms.None, "H48C39N4O15S4", null, 890, true, true);
            AddMod("EDT-iodoacetyl-PEO-biotin (S)", "S", null, LabelAtoms.None, "H34C20N4O4S3", null, 118, true, true);
            AddMod("EDT-iodoacetyl-PEO-biotin (T)", "T", null, LabelAtoms.None, "H34C20N4O4S3", null, 118, true, true);
            AddMod("EDT-maleimide-PEO-biotin (S)", "S", null, LabelAtoms.None, "H39C25N5O6S3", null, 93, true, true);
            AddMod("EDT-maleimide-PEO-biotin (T)", "T", null, LabelAtoms.None, "H39C25N5O6S3", null, 93, true, true);
            AddMod("EGCG1 (C)", "C", null, LabelAtoms.None, "H16C22O11", null, 1002, true, true);
            AddMod("EGCG2 (C)", "C", null, LabelAtoms.None, "H11C15O6", null, 1003, true, true);
            AddMod("EQAT (C)", "C", null, LabelAtoms.None, "H20C10N2O", null, 197, true, true);
            AddMod("EQAT:2H(5) (C)", "C", null, LabelAtoms.None, "H15H'5C10N2O", null, 198, true, true);
            AddMod("EQIGG (K)", "K", null, LabelAtoms.None, "H32C20N6O8", null, 846, true, true);
            AddMod("ESP (K)", "K", null, LabelAtoms.None, "H26C16N4O2S", null, 90, true, true);
            AddMod("ESP (N-term)", null, ModTerminus.N, LabelAtoms.None, "H26C16N4O2S", null, 90, true, true);
            AddMod("ESP:2H(10) (K)", "K", null, LabelAtoms.None, "H16H'10C16N4O2S", null, 91, true, true);
            AddMod("ESP:2H(10) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H16H'10C16N4O2S", null, 91, true, true);
            AddMod("Ethanedithiol (S)", "S", null, LabelAtoms.None, "H4C2S2 - O", null, 200, true, true);
            AddMod("Ethanedithiol (T)", "T", null, LabelAtoms.None, "H4C2S2 - O", null, 200, true, true);
            AddMod("Ethanolamine (C-term)", null, ModTerminus.C, LabelAtoms.None, "H5C2N", null, 734, true, true);
            AddMod("Ethanolamine (D)", "D", null, LabelAtoms.None, "H5C2N", null, 734, true, true);
            AddMod("Ethanolamine (E)", "E", null, LabelAtoms.None, "H5C2N", null, 734, true, true);
            AddMod("Ethanolyl (K)", "K", null, LabelAtoms.None, "H4C2O", null, 278, true, true);
            AddMod("Ethanolyl (R)", "R", null, LabelAtoms.None, "H4C2O", null, 278, true, true);
            AddMod("Ethoxyformyl (H)", "H", null, LabelAtoms.None, "H5C3O2", null, 915, true, true);
            AddMod("Ethyl (C-term)", null, ModTerminus.C, LabelAtoms.None, "H4C2", null, 280, true, true);
            AddMod("Ethyl (D)", "D", null, LabelAtoms.None, "H4C2", null, 280, true, true);
            AddMod("Ethyl (E)", "E", null, LabelAtoms.None, "H4C2", null, 280, true, true);
            AddMod("Ethyl (K)", "K", null, LabelAtoms.None, "H4C2", null, 280, true, true);
            AddMod("Ethyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H4C2", null, 280, true, true);
            AddMod("EthylAmide (N)", "N", null, LabelAtoms.None, "H5C2", null, 931, true, true);
            AddMod("EthylAmide (Q)", "Q", null, LabelAtoms.None, "H5C2", null, 931, true, true);
            AddMod("ethylamino (S)", "S", null, LabelAtoms.None, "H5C2N - O", null, 926, true, true);
            AddMod("ethylamino (T)", "T", null, LabelAtoms.None, "H5C2N - O", null, 926, true, true);
            AddMod("FAD (C)", "C", null, LabelAtoms.None, "H31C27N9O15P2", null, 50, true, true);
            AddMod("FAD (H)", "H", null, LabelAtoms.None, "H31C27N9O15P2", null, 50, true, true);
            AddMod("FAD (Y)", "Y", null, LabelAtoms.None, "H31C27N9O15P2", null, 50, true, true);
            AddMod("Farnesyl (C)", "C", null, LabelAtoms.None, "H24C15", null, 44, true, true);
            AddMod("Fluorescein (C)", "C", null, LabelAtoms.None, "H14C22NO6", null, 128, true, true);
            AddMod("Fluoro (F)", "F", null, LabelAtoms.None, "F - H", null, 127, true, true);
            AddMod("Fluoro (W)", "W", null, LabelAtoms.None, "F - H", null, 127, true, true);
            AddMod("Fluoro (Y)", "Y", null, LabelAtoms.None, "F - H", null, 127, true, true);
            AddMod("FMN (S)", "S", null, LabelAtoms.None, "H19C17N4O8P", null, 442, true, true);
            AddMod("FMN (T)", "T", null, LabelAtoms.None, "H19C17N4O8P", null, 442, true, true);
            AddMod("FMNC (C)", "C", null, LabelAtoms.None, "H21C17N4O9P", null, 443, true, true);
            AddMod("FMNH (C)", "C", null, LabelAtoms.None, "H19C17N4O9P", null, 409, true, true);
            AddMod("FMNH (H)", "H", null, LabelAtoms.None, "H19C17N4O9P", null, 409, true, true);
            AddMod("FNEM (C)", "C", null, LabelAtoms.None, "H13C24NO7", null, 515, true, true);
            AddMod("Formyl (K)", "K", null, LabelAtoms.None, "CO", null, 122, true, true);
            AddMod("Formyl (S)", "S", null, LabelAtoms.None, "CO", null, 122, true, true);
            AddMod("Formyl (T)", "T", null, LabelAtoms.None, "CO", null, 122, true, true);
            AddMod("FP-Biotin (K)", "K", null, LabelAtoms.None, "H49C27N4O5PS", null, 325, true, true);
            AddMod("FP-Biotin (S)", "S", null, LabelAtoms.None, "H49C27N4O5PS", null, 325, true, true);
            AddMod("FP-Biotin (T)", "T", null, LabelAtoms.None, "H49C27N4O5PS", null, 325, true, true);
            AddMod("FP-Biotin (Y)", "Y", null, LabelAtoms.None, "H49C27N4O5PS", null, 325, true, true);
            AddMod("FTC (C)", "C", null, LabelAtoms.None, "H15C21N3O5S", null, 478, true, true);
            AddMod("FTC (K)", "K", null, LabelAtoms.None, "H15C21N3O5S", null, 478, true, true);
            AddMod("FTC (P)", "P", null, LabelAtoms.None, "H15C21N3O5S", null, 478, true, true);
            AddMod("FTC (R)", "R", null, LabelAtoms.None, "H15C21N3O5S", null, 478, true, true);
            AddMod("FTC (S)", "S", null, LabelAtoms.None, "H15C21N3O5S", null, 478, true, true);
            AddMod("G-H1 (R)", "R", null, LabelAtoms.None, "C2O", null, 860, true, true);
            AddMod("Galactosyl (K)", "K", null, LabelAtoms.None, "H10C6O6", new [] { new FragmentLoss("H10C6O5"), }, 907, true, true);
            AddMod("GeranylGeranyl (C)", "C", null, LabelAtoms.None, "H32C20", null, 48, true, true);
            AddMod("GIST-Quat (K)", "K", null, LabelAtoms.None, "H13C7NO", new [] { new FragmentLoss("H9C3N"), }, 60, true, true);
            AddMod("GIST-Quat (N-term)", null, ModTerminus.N, LabelAtoms.None, "H13C7NO", new [] { new FragmentLoss("H9C3N"), }, 60, true, true);
            AddMod("GIST-Quat:2H(3) (K)", "K", null, LabelAtoms.None, "H10H'3C7NO", new [] { new FragmentLoss("H6H'3C3N"), }, 61, true, true);
            AddMod("GIST-Quat:2H(3) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H10H'3C7NO", new [] { new FragmentLoss("H6H'3C3N"), }, 61, true, true);
            AddMod("GIST-Quat:2H(6) (K)", "K", null, LabelAtoms.None, "H7H'6C7NO", new [] { new FragmentLoss("H3H'6C3N"), }, 62, true, true);
            AddMod("GIST-Quat:2H(6) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H7H'6C7NO", new [] { new FragmentLoss("H3H'6C3N"), }, 62, true, true);
            AddMod("GIST-Quat:2H(9) (K)", "K", null, LabelAtoms.None, "H4H'9C7NO", new [] { new FragmentLoss("H'9C3N"), }, 63, true, true);
            AddMod("GIST-Quat:2H(9) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H4H'9C7NO", new [] { new FragmentLoss("H'9C3N"), }, 63, true, true);
            AddMod("Glu (E)", "E", null, LabelAtoms.None, "H7C5NO3", null, 450, true, true);
            AddMod("glucosone (R)", "R", null, LabelAtoms.None, "H8C6O5", null, 981, true, true);
            AddMod("Glucosylgalactosyl (K)", "K", null, LabelAtoms.None, "H20C12O11", new [] { new FragmentLoss("H10C6O5"), new FragmentLoss("H20C12O10"), }, 393, true, true);
            AddMod("Glucuronyl (S)", "S", null, LabelAtoms.None, "H8C6O6", null, 54, true, true);
            AddMod("GluGlu (E)", "E", null, LabelAtoms.None, "H14C10N2O6", null, 451, true, true);
            AddMod("GluGluGlu (E)", "E", null, LabelAtoms.None, "H21C15N3O9", null, 452, true, true);
            AddMod("GluGluGluGlu (E)", "E", null, LabelAtoms.None, "H28C20N4O12", null, 453, true, true);
            AddMod("Glutathione (C)", "C", null, LabelAtoms.None, "H15C10N3O6S", null, 55, true, true);
            AddMod("Gly-loss+Amide (C-term G)", "G", ModTerminus.C, LabelAtoms.None, "-H2C2O2", null, 822, true, true);
            AddMod("Glycerophospho (S)", "S", null, LabelAtoms.None, "H7C3O5P", null, 419, true, true);
            AddMod("GlycerylPE (E)", "E", null, LabelAtoms.None, "H12C5NO5P", null, 396, true, true);
            AddMod("Glycosyl (P)", "P", null, LabelAtoms.None, "H8C5O5", null, 408, true, true);
            AddMod("GlyGly (C)", "C", null, LabelAtoms.None, "H6C4N2O2", null, 121, true, true);
            AddMod("GlyGly (K)", "K", null, LabelAtoms.None, "H6C4N2O2", null, 121, true, true);
            AddMod("GlyGly (S)", "S", null, LabelAtoms.None, "H6C4N2O2", null, 121, true, true);
            AddMod("GlyGly (T)", "T", null, LabelAtoms.None, "H6C4N2O2", null, 121, true, true);
            AddMod("Heme (C)", "C", null, LabelAtoms.None, "H32C34N4O4Fe", null, 390, true, true);
            AddMod("Heme (H)", "H", null, LabelAtoms.None, "H32C34N4O4Fe", null, 390, true, true);
            AddMod("Hep (K)", "K", null, LabelAtoms.None, "H12C7O6", null, 490, true, true);
            AddMod("Hep (N)", "N", null, LabelAtoms.None, "H12C7O6", null, 490, true, true);
            AddMod("Hep (Q)", "Q", null, LabelAtoms.None, "H12C7O6", null, 490, true, true);
            AddMod("Hep (R)", "R", null, LabelAtoms.None, "H12C7O6", null, 490, true, true);
            AddMod("Hep (S)", "S", null, LabelAtoms.None, "H12C7O6", null, 490, true, true);
            AddMod("Hep (T)", "T", null, LabelAtoms.None, "H12C7O6", null, 490, true, true);
            AddMod("Hex (C)", "C", null, LabelAtoms.None, "H10C6O5", null, 41, true, true);
            AddMod("Hex (K)", "K", null, LabelAtoms.None, "H10C6O5", null, 41, true, true);
            AddMod("Hex (N)", "N", null, LabelAtoms.None, "H10C6O5", null, 41, true, true);
            AddMod("Hex (N-term)", null, ModTerminus.N, LabelAtoms.None, "H10C6O5", null, 41, true, true);
            AddMod("Hex (R)", "R", null, LabelAtoms.None, "H10C6O5", null, 41, true, true);
            AddMod("Hex (T)", "T", null, LabelAtoms.None, "H10C6O5", null, 41, true, true);
            AddMod("Hex (W)", "W", null, LabelAtoms.None, "H10C6O5", null, 41, true, true);
            AddMod("Hex (Y)", "Y", null, LabelAtoms.None, "H10C6O5", null, 41, true, true);
            AddMod("Hex(1)HexNAc(1)dHex(1) (N)", "N", null, LabelAtoms.None, "H33C20NO14", null, 146, true, true);
            AddMod("Hex(1)HexNAc(1)NeuAc(1) (N)", "N", null, LabelAtoms.None, "H40C25N2O18", null, 149, true, true);
            AddMod("Hex(1)HexNAc(1)NeuAc(1) (S)", "S", null, LabelAtoms.None, "H40C25N2O18", null, 149, true, true);
            AddMod("Hex(1)HexNAc(1)NeuAc(1) (T)", "T", null, LabelAtoms.None, "H40C25N2O18", null, 149, true, true);
            AddMod("Hex(1)HexNAc(1)NeuAc(2) (N)", "N", null, LabelAtoms.None, "H57C36N3O26", null, 160, true, true);
            AddMod("Hex(1)HexNAc(1)NeuAc(2) (S)", "S", null, LabelAtoms.None, "H57C36N3O26", null, 160, true, true);
            AddMod("Hex(1)HexNAc(1)NeuAc(2) (T)", "T", null, LabelAtoms.None, "H57C36N3O26", null, 160, true, true);
            AddMod("Hex(1)HexNAc(2) (N)", "N", null, LabelAtoms.None, "H36C22N2O15", null, 148, true, true);
            AddMod("Hex(1)HexNAc(2)dHex(1) (N)", "N", null, LabelAtoms.None, "H46C28N2O19", null, 152, true, true);
            AddMod("Hex(1)HexNAc(2)dHex(1)Pent(1) (N)", "N", null, LabelAtoms.None, "H54C33N2O23", null, 155, true, true);
            AddMod("Hex(1)HexNAc(2)dHex(2) (N)", "N", null, LabelAtoms.None, "H56C34N2O23", null, 156, true, true);
            AddMod("Hex(1)HexNAc(2)Pent(1) (N)", "N", null, LabelAtoms.None, "H44C27N2O19", null, 151, true, true);
            AddMod("Hex(2) (K)", "K", null, LabelAtoms.None, "H20C12O10", null, 512, true, true);
            AddMod("Hex(2) (R)", "R", null, LabelAtoms.None, "H20C12O10", null, 512, true, true);
            AddMod("Hex(2)HexNAc(2) (N)", "N", null, LabelAtoms.None, "H46C28N2O20", null, 153, true, true);
            AddMod("Hex(2)HexNAc(2)dHex(1) (N)", "N", null, LabelAtoms.None, "H56C34N2O24", null, 158, true, true);
            AddMod("Hex(2)HexNAc(2)Pent(1) (N)", "N", null, LabelAtoms.None, "H54C33N2O24", null, 157, true, true);
            AddMod("Hex(3) (N)", "N", null, LabelAtoms.None, "H30C18O15", null, 144, true, true);
            AddMod("Hex(3)HexNAc(1)Pent(1) (N)", "N", null, LabelAtoms.None, "H51C31NO24", null, 154, true, true);
            AddMod("Hex(3)HexNAc(2) (N)", "N", null, LabelAtoms.None, "H56C34N2O25", null, 159, true, true);
            AddMod("Hex(3)HexNAc(2)P(1) (N)", "N", null, LabelAtoms.None, "H56C34N2O25P", null, 161, true, true);
            AddMod("Hex(3)HexNAc(4) (N)", "N", null, LabelAtoms.None, "H82C50N4O35", null, 309, true, true);
            AddMod("Hex(4)HexNAc(4) (N)", "N", null, LabelAtoms.None, "H92C56N4O40", null, 310, true, true);
            AddMod("Hex(5)HexNAc(2) (N)", "N", null, LabelAtoms.None, "H76C46N2O35", null, 137, true, true);
            AddMod("Hex(5)HexNAc(4) (N)", "N", null, LabelAtoms.None, "H102C62N4O45", null, 311, true, true);
            AddMod("Hex1HexNAc1 (S)", "S", null, LabelAtoms.None, "H23C14NO10", null, 793, true, true);
            AddMod("Hex1HexNAc1 (T)", "T", null, LabelAtoms.None, "H23C14NO10", null, 793, true, true);
            AddMod("HexN (K)", "K", null, LabelAtoms.None, "H11C6NO4", null, 454, true, true);
            AddMod("HexN (N)", "N", null, LabelAtoms.None, "H11C6NO4", null, 454, true, true);
            AddMod("HexN (T)", "T", null, LabelAtoms.None, "H11C6NO4", null, 454, true, true);
            AddMod("HexN (W)", "W", null, LabelAtoms.None, "H11C6NO4", null, 454, true, true);
            AddMod("HexNAc (N)", "N", null, LabelAtoms.None, "H13C8NO5", null, 43, true, true);
            AddMod("HexNAc (S)", "S", null, LabelAtoms.None, "H13C8NO5", null, 43, true, true);
            AddMod("HexNAc (T)", "T", null, LabelAtoms.None, "H13C8NO5", null, 43, true, true);
            AddMod("HexNAc(1)dHex(1) (N)", "N", null, LabelAtoms.None, "H23C14NO9", null, 142, true, true);
            AddMod("HexNAc(1)dHex(2) (N)", "N", null, LabelAtoms.None, "H33C20NO13", null, 145, true, true);
            AddMod("HexNAc(2) (N)", "N", null, LabelAtoms.None, "H26C16N2O10", null, 143, true, true);
            AddMod("HexNAc(2)dHex(1) (N)", "N", null, LabelAtoms.None, "H36C22N2O14", null, 147, true, true);
            AddMod("HexNAc(2)dHex(2) (N)", "N", null, LabelAtoms.None, "H46C28N2O18", null, 150, true, true);
            AddMod("HMVK (C)", "C", null, LabelAtoms.None, "H6C4O2", null, 371, true, true);
            AddMod("HNE (CHK)", "C, H, K", null, LabelAtoms.None, "H16C9O2", null, 53, true, true);
            AddMod("HNE+Delta:H(2) (C)", "C", null, LabelAtoms.None, "H18C9O2", null, 335, true, true);
            AddMod("HNE+Delta:H(2) (H)", "H", null, LabelAtoms.None, "H18C9O2", null, 335, true, true);
            AddMod("HNE+Delta:H(2) (K)", "K", null, LabelAtoms.None, "H18C9O2", null, 335, true, true);
            AddMod("HNE-BAHAH (C)", "C", null, LabelAtoms.None, "H45C25N5O4S", null, 912, true, true);
            AddMod("HNE-BAHAH (H)", "H", null, LabelAtoms.None, "H45C25N5O4S", null, 912, true, true);
            AddMod("HNE-BAHAH (K)", "K", null, LabelAtoms.None, "H45C25N5O4S", null, 912, true, true);
            AddMod("HNE-Delta:H(2)O (C)", "C", null, LabelAtoms.None, "H14C9O", null, 720, true, true);
            AddMod("HNE-Delta:H(2)O (H)", "H", null, LabelAtoms.None, "H14C9O", null, 720, true, true);
            AddMod("HNE-Delta:H(2)O (K)", "K", null, LabelAtoms.None, "H14C9O", null, 720, true, true);
            AddMod("HPG (R)", "R", null, LabelAtoms.None, "H4C8O2", null, 186, true, true);
            AddMod("Hydroxycinnamyl (C)", "C", null, LabelAtoms.None, "H6C9O2", null, 407, true, true);
            AddMod("Hydroxyfarnesyl (C)", "C", null, LabelAtoms.None, "H24C15O", null, 376, true, true);
            AddMod("Hydroxyheme (E)", "E", null, LabelAtoms.None, "H30C34N4O4Fe", null, 436, true, true);
            AddMod("Hydroxymethyl (N)", "N", null, LabelAtoms.None, "H2CO", null, 414, true, true);
            AddMod("HydroxymethylOP (K)", "K", null, LabelAtoms.None, "H4C6O2", null, 886, true, true);
            AddMod("Hydroxytrimethyl (K)", "K", null, LabelAtoms.None, "H7C3O", null, 445, true, true);
            AddMod("Hypusine (K)", "K", null, LabelAtoms.None, "H9C4NO", null, 379, true, true);
            AddMod("IBTP (C)", "C", null, LabelAtoms.None, "H21C22P", null, 119, true, true);
            AddMod("ICAT-D (C)", "C", null, LabelAtoms.None, "H34C20N4O5S", null, 13, true, true);
            AddMod("ICAT-D:2H(8) (C)", "C", null, LabelAtoms.None, "H26H'8C20N4O5S", null, 12, true, true);
            AddMod("ICAT-G (C)", "C", null, LabelAtoms.None, "H38C22N4O6S", null, 8, true, true);
            AddMod("ICAT-G:2H(8) (C)", "C", null, LabelAtoms.None, "H30H'8C22N4O6S", null, 9, true, true);
            AddMod("ICAT-H (C)", "C", null, LabelAtoms.None, "H20C15NO6Cl", null, 123, true, true);
            AddMod("ICAT-H:13C(6) (C)", "C", null, LabelAtoms.None, "H20C9C'6NO6Cl", null, 124, true, true);
            AddMod("ICPL (N-term)", null, ModTerminus.N, LabelAtoms.None, "H3C6NO", null, 365, true, true);
            AddMod("ICPL:13C(6) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H3C'6NO", null, 364, true, true);
            AddMod("ICPL:2H(4) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H'4C6NO - H", null, 687, true, true);
            AddMod("IDEnT (C)", "C", null, LabelAtoms.None, "H7C9NOCl2", null, 762, true, true);
            AddMod("IED-Biotin (C)", "C", null, LabelAtoms.None, "H22C14N4O3S", null, 294, true, true);
            AddMod("IGBP (C)", "C", null, LabelAtoms.None, "H13C12N2O2Br", null, 243, true, true);
            AddMod("IGBP:13C(2) (C)", "C", null, LabelAtoms.None, "H13C10C'2N2O2Br", null, 499, true, true);
            AddMod("IMID (K)", "K", null, LabelAtoms.None, "H4C3N2", null, 94, true, true);
            AddMod("IMID:2H(4) (K)", "K", null, LabelAtoms.None, "H'4C3N2", null, 95, true, true);
            AddMod("Iminobiotin (K)", "K", null, LabelAtoms.None, "H15C10N3OS", null, 89, true, true);
            AddMod("Iminobiotin (N-term)", null, ModTerminus.N, LabelAtoms.None, "H15C10N3OS", null, 89, true, true);
            AddMod("Iodo (H)", "H", null, LabelAtoms.None, "I - H", null, 129, true, true);
            AddMod("Iodo (Y)", "Y", null, LabelAtoms.None, "I - H", null, 129, true, true);
            AddMod("IodoU-AMP (F)", "F", null, LabelAtoms.None, "H11C9N2O9P", null, 292, true, true);
            AddMod("IodoU-AMP (W)", "W", null, LabelAtoms.None, "H11C9N2O9P", null, 292, true, true);
            AddMod("IodoU-AMP (Y)", "Y", null, LabelAtoms.None, "H11C9N2O9P", null, 292, true, true);
            AddMod("ISD_z+2_ion (N-term)", null, ModTerminus.N, LabelAtoms.None, "-HN", null, 991, true, true);
            AddMod("Isopropylphospho (S)", "S", null, LabelAtoms.None, "H7C3O3P", null, 363, true, true);
            AddMod("Isopropylphospho (T)", "T", null, LabelAtoms.None, "H7C3O3P", null, 363, true, true);
            AddMod("Isopropylphospho (Y)", "Y", null, LabelAtoms.None, "H7C3O3P", null, 363, true, true);
            AddMod("iTRAQ4plex114 (K)", "K", null, LabelAtoms.None, "H12C5C'2N2O'", null, 532, true, true);
            AddMod("iTRAQ4plex114 (N-term)", null, ModTerminus.N, LabelAtoms.None, "H12C5C'2N2O'", null, 532, true, true);
            AddMod("iTRAQ4plex114 (Y)", "Y", null, LabelAtoms.None, "H12C5C'2N2O'", null, 532, true, true);
            AddMod("iTRAQ4plex115 (K)", "K", null, LabelAtoms.None, "H12C6C'NN'O'", null, 533, true, true);
            AddMod("iTRAQ4plex115 (N-term)", null, ModTerminus.N, LabelAtoms.None, "H12C6C'NN'O'", null, 533, true, true);
            AddMod("iTRAQ4plex115 (Y)", "Y", null, LabelAtoms.None, "H12C6C'NN'O'", null, 533, true, true);
            AddMod("iTRAQ8plex:13C(6)15N(2) (K)", "K", null, LabelAtoms.None, "H24C8C'6N2N'2O3", null, 731, true, true);
            AddMod("iTRAQ8plex:13C(6)15N(2) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H24C8C'6N2N'2O3", null, 731, true, true);
            AddMod("iTRAQ8plex:13C(6)15N(2) (Y)", "Y", null, LabelAtoms.None, "H24C8C'6N2N'2O3", null, 731, true, true);
            AddMod("Label:13C(1)2H(3)+Oxidation (M)", "M", null, LabelAtoms.None, "H'3C'O - H3C", null, 885, true, true);
            AddMod("Label:13C(4)15N(2)+GlyGly (K)", "K", null, LabelAtoms.None, "H6C'4N'2O2", null, 923, true, true);
            AddMod("Label:13C(6)+Acetyl (K)", "K", null, LabelAtoms.None, "H2C'6O - C4", null, 835, true, true);
            AddMod("Label:13C(6)+Dimethyl (K)", "K", null, LabelAtoms.None, "H4C'6 - C4", null, 986, true, true);
            AddMod("Label:13C(6)+GlyGly (K)", "K", null, LabelAtoms.None, "H6C'6N2O2 - C2", null, 799, true, true);
            AddMod("Label:13C(6)15N(2)+Acetyl (K)", "K", null, LabelAtoms.None, "H2C'6N'2O - C4N2", null, 836, true, true);
            AddMod("Label:13C(6)15N(2)+Dimethyl (K)", "K", null, LabelAtoms.None, "H4C'6N'2 - C4N2", null, 987, true, true);
            AddMod("Label:13C(6)15N(2)+GlyGly (K)", "K", null, LabelAtoms.None, "H6C'6N'2O2 - C2", null, 864, true, true);
            AddMod("Label:13C(6)15N(4)+Dimethyl (R)", "R", null, LabelAtoms.None, "H4C'6N'4 - C4N4", null, 1005, true, true);
            AddMod("Label:13C(6)15N(4)+Dimethyl:2H(6)13C(2) (R)", "R", null, LabelAtoms.None, "H'6C'8N'4 - H2C6N4", null, 1007, true, true);
            AddMod("Label:13C(6)15N(4)+Methyl (R)", "R", null, LabelAtoms.None, "H2C'6N'4 - C5N4", null, 1004, true, true);
            AddMod("Label:13C(6)15N(4)+Methyl:2H(3)13C(1) (R)", "R", null, LabelAtoms.None, "H'3C'7N'4 - HC6N4", null, 1006, true, true);
            AddMod("Label:13C(9)+Phospho (Y)", "Y", null, LabelAtoms.None, "HC'9O3P - C9", null, 185, true, true);
            AddMod("Label:2H(4)+Acetyl (K)", "K", null, LabelAtoms.None, "H'4C2O - H2", null, 834, true, true);
            AddMod("Label:2H(4)+GlyGly (K)", "K", null, LabelAtoms.None, "H2H'4C4N2O2", null, 853, true, true);
            AddMod("lapachenole (C)", "C", null, LabelAtoms.None, "H16C16O2", null, 771, true, true);
            AddMod("Leu->MetOx (L)", "L", null, LabelAtoms.None, "OS - H2C", null, 905, true, true);
            AddMod("LeuArgGlyGly (K)", "K", null, LabelAtoms.None, "H29C16N7O4", null, 535, true, true);
            AddMod("LG-anhydrolactam (K)", "K", null, LabelAtoms.None, "H26C20O3", null, 946, true, true);
            AddMod("LG-anhydrolactam (N-term)", null, ModTerminus.N, LabelAtoms.None, "H26C20O3", null, 946, true, true);
            AddMod("LG-anhyropyrrole (K)", "K", null, LabelAtoms.None, "H26C20O2", null, 948, true, true);
            AddMod("LG-anhyropyrrole (N-term)", null, ModTerminus.N, LabelAtoms.None, "H26C20O2", null, 948, true, true);
            AddMod("LG-Hlactam-K (K)", "K", null, LabelAtoms.None, "H28C20O5", null, 504, true, true);
            AddMod("LG-Hlactam-R (R)", "R", null, LabelAtoms.None, "H26C19O5 - N2", null, 506, true, true);
            AddMod("LG-lactam-K (K)", "K", null, LabelAtoms.None, "H28C20O4", null, 503, true, true);
            AddMod("LG-lactam-R (R)", "R", null, LabelAtoms.None, "H26C19O4 - N2", null, 505, true, true);
            AddMod("LG-pyrrole (K)", "K", null, LabelAtoms.None, "H28C20O3", null, 947, true, true);
            AddMod("LG-pyrrole (N-term)", null, ModTerminus.N, LabelAtoms.None, "H28C20O3", null, 947, true, true);
            AddMod("Lipoyl (K)", "K", null, LabelAtoms.None, "H12C8OS2", null, 42, true, true);
            AddMod("Lys->Allysine (K)", "K", null, LabelAtoms.None, "O - H3N", null, 352, true, true);
            AddMod("Lys->AminoadipicAcid (K)", "K", null, LabelAtoms.None, "O2 - H3N", null, 381, true, true);
            AddMod("Lys->CamCys (K)", "K", null, LabelAtoms.None, "OS - H4C", null, 903, true, true);
            AddMod("Lys->MetOx (K)", "K", null, LabelAtoms.None, "OS - H3CN", null, 906, true, true);
            AddMod("Lys-loss (Protein C-term K)", "K", ModTerminus.C, LabelAtoms.None, "-H12C6N2O", null, 313, true, true);
            AddMod("Lysbiotinhydrazide (K)", "K", null, LabelAtoms.None, "H15C10N3O2S", null, 353, true, true);
            AddMod("maleimide (C)", "C", null, LabelAtoms.None, "H3C4NO2", null, 773, true, true);
            AddMod("maleimide (K)", "K", null, LabelAtoms.None, "H3C4NO2", null, 773, true, true);
            AddMod("Maleimide-PEO2-Biotin (C)", "C", null, LabelAtoms.None, "H35C23N5O7S", null, 522, true, true);
            AddMod("maleimide3 (C)", "C", null, LabelAtoms.None, "H59C37N7O23", null, 971, true, true);
            AddMod("maleimide3 (K)", "K", null, LabelAtoms.None, "H59C37N7O23", null, 971, true, true);
            AddMod("maleimide5 (C)", "C", null, LabelAtoms.None, "H79C49N7O33", null, 972, true, true);
            AddMod("maleimide5 (K)", "K", null, LabelAtoms.None, "H79C49N7O33", null, 972, true, true);
            AddMod("Malonyl (C)", "C", null, LabelAtoms.None, "H2C3O3", null, 747, true, true);
            AddMod("Malonyl (S)", "S", null, LabelAtoms.None, "H2C3O3", null, 747, true, true);
            AddMod("MDCC (C)", "C", null, LabelAtoms.None, "H21C20N3O5", null, 887, true, true);
            AddMod("Menadione (C)", "C", null, LabelAtoms.None, "H6C11O2", null, 302, true, true);
            AddMod("Menadione (K)", "K", null, LabelAtoms.None, "H6C11O2", null, 302, true, true);
            AddMod("Menadione-HQ (C)", "C", null, LabelAtoms.None, "H8C11O2", null, 767, true, true);
            AddMod("Menadione-HQ (K)", "K", null, LabelAtoms.None, "H8C11O2", null, 767, true, true);
            AddMod("MercaptoEthanol (S)", "S", null, LabelAtoms.None, "H4C2S", null, 928, true, true);
            AddMod("MercaptoEthanol (T)", "T", null, LabelAtoms.None, "H4C2S", null, 928, true, true);
            AddMod("Met->Aha (M)", "M", null, LabelAtoms.None, "N3 - H3CS", null, 896, true, true);
            AddMod("Met->Hpg (M)", "M", null, LabelAtoms.None, "C - H2S", null, 899, true, true);
            AddMod("Met-loss (Protein N-term M)", "M", ModTerminus.N, LabelAtoms.None, "-H9C5NOS", null, 765, true, true);
            AddMod("Met-loss+Acetyl (Protein N-term M)", "M", ModTerminus.N, LabelAtoms.None, "-H7C3NS", null, 766, true, true);
            AddMod("Methyl (C)", "C", null, LabelAtoms.None, "H2C", null, 34, true, true);
            AddMod("Methyl (H)", "H", null, LabelAtoms.None, "H2C", null, 34, true, true);
            AddMod("Methyl (I)", "I", null, LabelAtoms.None, "H2C", null, 34, true, true);
            AddMod("Methyl (K)", "K", null, LabelAtoms.None, "H2C", null, 34, true, true);
            AddMod("Methyl (L)", "L", null, LabelAtoms.None, "H2C", null, 34, true, true);
            AddMod("Methyl (N)", "N", null, LabelAtoms.None, "H2C", null, 34, true, true);
            AddMod("Methyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H2C", null, 34, true, true);
            AddMod("Methyl (Q)", "Q", null, LabelAtoms.None, "H2C", null, 34, true, true);
            AddMod("Methyl (R)", "R", null, LabelAtoms.None, "H2C", null, 34, true, true);
            AddMod("Methyl (S)", "S", null, LabelAtoms.None, "H2C", null, 34, true, true);
            AddMod("Methyl (T)", "T", null, LabelAtoms.None, "H2C", null, 34, true, true);
            AddMod("Methyl+Acetyl:2H(3) (K)", "K", null, LabelAtoms.None, "HH'3C3O", null, 768, true, true);
            AddMod("Methyl+Deamidated (N)", "N", null, LabelAtoms.None, "HCO - N", null, 528, true, true);
            AddMod("Methyl+Deamidated (Q)", "Q", null, LabelAtoms.None, "HCO - N", null, 528, true, true);
            AddMod("Methyl-PEO12-Maleimide (C)", "C", null, LabelAtoms.None, "H58C32N2O15", null, 891, true, true);
            AddMod("Methyl:2H(2) (K)", "K", null, LabelAtoms.None, "H'2C", null, 284, true, true);
            AddMod("Methyl:2H(3) (C-term)", null, ModTerminus.C, LabelAtoms.None, "H'3C - H", null, 298, true, true);
            AddMod("Methyl:2H(3) (D)", "D", null, LabelAtoms.None, "H'3C - H", null, 298, true, true);
            AddMod("Methyl:2H(3) (E)", "E", null, LabelAtoms.None, "H'3C - H", null, 298, true, true);
            AddMod("Methyl:2H(3)13C(1) (R)", "R", null, LabelAtoms.None, "H'3C' - H", null, 329, true, true);
            AddMod("Methylamine (S)", "S", null, LabelAtoms.None, "H3CN - O", null, 337, true, true);
            AddMod("Methylamine (T)", "T", null, LabelAtoms.None, "H3CN - O", null, 337, true, true);
            AddMod("Methylmalonylation (S)", "S", null, LabelAtoms.None, "H4C4O3", null, 914, true, true);
            AddMod("Methylphosphonate (S)", "S", null, LabelAtoms.None, "H3CO2P", null, 728, true, true);
            AddMod("Methylphosphonate (T)", "T", null, LabelAtoms.None, "H3CO2P", null, 728, true, true);
            AddMod("Methylphosphonate (Y)", "Y", null, LabelAtoms.None, "H3CO2P", null, 728, true, true);
            AddMod("Methylpyrroline (K)", "K", null, LabelAtoms.None, "H7C6NO", null, 435, true, true);
            AddMod("Methylthio (D)", "D", null, LabelAtoms.None, "H2CS", null, 39, true, true);
            AddMod("Methylthio (N)", "N", null, LabelAtoms.None, "H2CS", null, 39, true, true);
            AddMod("MG-H1 (R)", "R", null, LabelAtoms.None, "H2C3O", null, 859, true, true);
            AddMod("Molybdopterin (C)", "C", null, LabelAtoms.None, "H11C10N5O8PS2Mo", null, 391, true, true);
            AddMod("MolybdopterinGD (C)", "C", null, LabelAtoms.None, "H47C40N20O26P4S4Mo", null, 424, true, true);
            AddMod("MolybdopterinGD (D)", "D", null, LabelAtoms.None, "H47C40N20O26P4S4Mo", null, 424, true, true);
            AddMod("MolybdopterinGD+Delta:S(-1)Se(1) (C)", "C", null, LabelAtoms.None, "H47C40N20O26P4S3SeMo", null, 415, true, true);
            AddMod("MTSL (C)", "C", null, LabelAtoms.None, "H14C9NOS", null, 911, true, true);
            AddMod("Myristoleyl (Protein N-term G)", "G", ModTerminus.N, LabelAtoms.None, "H24C14O", null, 134, true, true);
            AddMod("Myristoyl (C)", "C", null, LabelAtoms.None, "H26C14O", null, 45, true, true);
            AddMod("Myristoyl (K)", "K", null, LabelAtoms.None, "H26C14O", null, 45, true, true);
            AddMod("Myristoyl (N-term G)", "G", ModTerminus.N, LabelAtoms.None, "H26C14O", null, 45, true, true);
            AddMod("Myristoyl+Delta:H(-4) (Protein N-term G)", "G", ModTerminus.N, LabelAtoms.None, "H22C14O", null, 135, true, true);
            AddMod("NA-LNO2 (C)", "C", null, LabelAtoms.None, "H31C18NO4", null, 685, true, true);
            AddMod("NA-LNO2 (H)", "H", null, LabelAtoms.None, "H31C18NO4", null, 685, true, true);
            AddMod("NA-OA-NO2 (C)", "C", null, LabelAtoms.None, "H33C18NO4", null, 686, true, true);
            AddMod("NA-OA-NO2 (H)", "H", null, LabelAtoms.None, "H33C18NO4", null, 686, true, true);
            AddMod("NBS (W)", "W", null, LabelAtoms.None, "H3C6NO2S", null, 172, true, true);
            AddMod("NBS:13C(6) (W)", "W", null, LabelAtoms.None, "H3C'6NO2S", null, 171, true, true);
            AddMod("NDA (K)", "K", null, LabelAtoms.None, "H5C13N", null, 457, true, true);
            AddMod("NDA (N-term)", null, ModTerminus.N, LabelAtoms.None, "H5C13N", null, 457, true, true);
            AddMod("NEIAA (C)", "C", null, LabelAtoms.None, "H7C4NO", null, 211, true, true);
            AddMod("NEIAA (Y)", "Y", null, LabelAtoms.None, "H7C4NO", null, 211, true, true);
            AddMod("NEIAA:2H(5) (C)", "C", null, LabelAtoms.None, "H2H'5C4NO", null, 212, true, true);
            AddMod("NEIAA:2H(5) (Y)", "Y", null, LabelAtoms.None, "H2H'5C4NO", null, 212, true, true);
            AddMod("NEM:2H(5) (C)", "C", null, LabelAtoms.None, "H2H'5C6NO2", null, 776, true, true);
            AddMod("Nethylmaleimide (C)", "C", null, LabelAtoms.None, "H7C6NO2", null, 108, true, true);
            AddMod("Nethylmaleimide+water (C)", "C", null, LabelAtoms.None, "H9C6NO3", null, 320, true, true);
            AddMod("Nethylmaleimide+water (K)", "K", null, LabelAtoms.None, "H9C6NO3", null, 320, true, true);
            AddMod("NHS-LC-Biotin (K)", "K", null, LabelAtoms.None, "H25C16N3O3S", null, 92, true, true);
            AddMod("NHS-LC-Biotin (N-term)", null, ModTerminus.N, LabelAtoms.None, "H25C16N3O3S", null, 92, true, true);
            AddMod("NIC (N-term)", null, ModTerminus.N, LabelAtoms.None, "H3C6NO", null, 697, true, true);
            AddMod("Nitro (W)", "W", null, LabelAtoms.None, "NO2 - H", null, 354, true, true);
            AddMod("Nitro (Y)", "Y", null, LabelAtoms.None, "NO2 - H", null, 354, true, true);
            AddMod("Nitrosyl (C)", "C", null, LabelAtoms.None, "NO - H", null, 275, true, true);
            AddMod("Nmethylmaleimide (C)", "C", null, LabelAtoms.None, "H5C5NO2", null, 314, true, true);
            AddMod("Nmethylmaleimide (K)", "K", null, LabelAtoms.None, "H5C5NO2", null, 314, true, true);
            AddMod("Nmethylmaleimide+water (C)", "C", null, LabelAtoms.None, "H7C5NO3", null, 500, true, true);
            AddMod("NO_SMX_SEMD (C)", "C", null, LabelAtoms.None, "H10C10N3O3S", null, 744, true, true);
            AddMod("NO_SMX_SIMD (C)", "C", null, LabelAtoms.None, "H9C10N3O4S", null, 746, true, true);
            AddMod("NO_SMX_SMCT (C)", "C", null, LabelAtoms.None, "H10C10N3O4S", null, 745, true, true);
            AddMod("O-Diethylphosphate (C)", "C", null, LabelAtoms.None, "H9C4O3P", null, 725, true, true);
            AddMod("O-Diethylphosphate (H)", "H", null, LabelAtoms.None, "H9C4O3P", null, 725, true, true);
            AddMod("O-Diethylphosphate (K)", "K", null, LabelAtoms.None, "H9C4O3P", null, 725, true, true);
            AddMod("O-Diethylphosphate (S)", "S", null, LabelAtoms.None, "H9C4O3P", null, 725, true, true);
            AddMod("O-Diethylphosphate (T)", "T", null, LabelAtoms.None, "H9C4O3P", null, 725, true, true);
            AddMod("O-Diethylphosphate (Y)", "Y", null, LabelAtoms.None, "H9C4O3P", null, 725, true, true);
            AddMod("O-Dimethylphosphate (S)", "S", null, LabelAtoms.None, "H5C2O3P", null, 723, true, true);
            AddMod("O-Dimethylphosphate (T)", "T", null, LabelAtoms.None, "H5C2O3P", null, 723, true, true);
            AddMod("O-Dimethylphosphate (Y)", "Y", null, LabelAtoms.None, "H5C2O3P", null, 723, true, true);
            AddMod("O-Ethylphosphate (S)", "S", null, LabelAtoms.None, "H5C2O3P", null, 726, true, true);
            AddMod("O-Ethylphosphate (T)", "T", null, LabelAtoms.None, "H5C2O3P", null, 726, true, true);
            AddMod("O-Ethylphosphate (Y)", "Y", null, LabelAtoms.None, "H5C2O3P", null, 726, true, true);
            AddMod("O-Isopropylmethylphosphonate (S)", "S", null, LabelAtoms.None, "H9C4O2P", null, 729, true, true);
            AddMod("O-Isopropylmethylphosphonate (T)", "T", null, LabelAtoms.None, "H9C4O2P", null, 729, true, true);
            AddMod("O-Isopropylmethylphosphonate (Y)", "Y", null, LabelAtoms.None, "H9C4O2P", null, 729, true, true);
            AddMod("O-Methylphosphate (S)", "S", null, LabelAtoms.None, "H3CO3P", null, 724, true, true);
            AddMod("O-Methylphosphate (T)", "T", null, LabelAtoms.None, "H3CO3P", null, 724, true, true);
            AddMod("O-Methylphosphate (Y)", "Y", null, LabelAtoms.None, "H3CO3P", null, 724, true, true);
            AddMod("O-pinacolylmethylphosphonate (H)", "H", null, LabelAtoms.None, "H15C7O2P", null, 727, true, true);
            AddMod("O-pinacolylmethylphosphonate (K)", "K", null, LabelAtoms.None, "H15C7O2P", null, 727, true, true);
            AddMod("O-pinacolylmethylphosphonate (S)", "S", null, LabelAtoms.None, "H15C7O2P", null, 727, true, true);
            AddMod("O-pinacolylmethylphosphonate (T)", "T", null, LabelAtoms.None, "H15C7O2P", null, 727, true, true);
            AddMod("O-pinacolylmethylphosphonate (Y)", "Y", null, LabelAtoms.None, "H15C7O2P", null, 727, true, true);
            AddMod("Octanoyl (S)", "S", null, LabelAtoms.None, "H14C8O", null, 426, true, true);
            AddMod("Octanoyl (T)", "T", null, LabelAtoms.None, "H14C8O", null, 426, true, true);
            AddMod("OxArgBiotin (R)", "R", null, LabelAtoms.None, "H22C15N2O3S", null, 116, true, true);
            AddMod("OxArgBiotinRed (R)", "R", null, LabelAtoms.None, "H24C15N2O3S", null, 117, true, true);
            AddMod("Oxidation (C)", "C", null, LabelAtoms.None, "O", null, 35, true, true);
            AddMod("Oxidation (C-term G)", "G", ModTerminus.C, LabelAtoms.None, "O", null, 35, true, true);
            AddMod("Oxidation (D)", "D", null, LabelAtoms.None, "O", null, 35, true, true);
            AddMod("Oxidation (F)", "F", null, LabelAtoms.None, "O", null, 35, true, true);
            AddMod("Oxidation (K)", "K", null, LabelAtoms.None, "O", null, 35, true, true);
            AddMod("Oxidation (N)", "N", null, LabelAtoms.None, "O", null, 35, true, true);
            AddMod("Oxidation (P)", "P", null, LabelAtoms.None, "O", null, 35, true, true);
            AddMod("Oxidation (R)", "R", null, LabelAtoms.None, "O", null, 35, true, true);
            AddMod("Oxidation (Y)", "Y", null, LabelAtoms.None, "O", null, 35, true, true);
            AddMod("OxLysBiotin (K)", "K", null, LabelAtoms.None, "H24C16N4O3S", null, 113, true, true);
            AddMod("OxLysBiotinRed (K)", "K", null, LabelAtoms.None, "H26C16N4O3S", null, 112, true, true);
            AddMod("OxProBiotin (P)", "P", null, LabelAtoms.None, "H27C16N5O3S", null, 115, true, true);
            AddMod("OxProBiotinRed (P)", "P", null, LabelAtoms.None, "H29C16N5O3S", null, 114, true, true);
            AddMod("Palmitoleyl (C)", "C", null, LabelAtoms.None, "H28C16O", null, 431, true, true);
            AddMod("Palmitoleyl (S)", "S", null, LabelAtoms.None, "H28C16O", null, 431, true, true);
            AddMod("Palmitoleyl (T)", "T", null, LabelAtoms.None, "H28C16O", null, 431, true, true);
            AddMod("Palmitoyl (C)", "C", null, LabelAtoms.None, "H30C16O", null, 47, true, true);
            AddMod("Palmitoyl (K)", "K", null, LabelAtoms.None, "H30C16O", null, 47, true, true);
            AddMod("Palmitoyl (S)", "S", null, LabelAtoms.None, "H30C16O", null, 47, true, true);
            AddMod("Palmitoyl (T)", "T", null, LabelAtoms.None, "H30C16O", null, 47, true, true);
            AddMod("PEITC (C)", "C", null, LabelAtoms.None, "H9C9NS", null, 979, true, true);
            AddMod("PEITC (K)", "K", null, LabelAtoms.None, "H9C9NS", null, 979, true, true);
            AddMod("PEITC (N-term)", null, ModTerminus.N, LabelAtoms.None, "H9C9NS", null, 979, true, true);
            AddMod("Pentylamine (Q)", "Q", null, LabelAtoms.None, "H11C5N", null, 801, true, true);
            AddMod("PentylamineBiotin (Q)", "Q", null, LabelAtoms.None, "H25C15N3O2S", null, 800, true, true);
            AddMod("PEO-Iodoacetyl-LC-Biotin (C)", "C", null, LabelAtoms.None, "H30C18N4O5S", null, 20, true, true);
            AddMod("PET (S)", "S", null, LabelAtoms.None, "H7C7NS - O", null, 264, true, true);
            AddMod("PET (T)", "T", null, LabelAtoms.None, "H7C7NS - O", null, 264, true, true);
            AddMod("PGA1-biotin (C)", "C", null, LabelAtoms.None, "H60C36N4O5S", null, 539, true, true);
            AddMod("Phe->CamCys (F)", "F", null, LabelAtoms.None, "NOS - HC4", null, 904, true, true);
            AddMod("Phenylisocyanate (N-term)", null, ModTerminus.N, LabelAtoms.None, "H5C7NO", null, 411, true, true);
            AddMod("Phenylisocyanate:2H(5) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H'5C7NO", null, 412, true, true);
            AddMod("Phospho (C)", "C", null, LabelAtoms.None, "HO3P", null, 21, true, true);
            AddMod("Phospho (D)", "D", null, LabelAtoms.None, "HO3P", null, 21, true, true);
            AddMod("Phospho (H)", "H", null, LabelAtoms.None, "HO3P", null, 21, true, true);
            AddMod("Phospho (R)", "R", null, LabelAtoms.None, "HO3P", null, 21, true, true);
            AddMod("Phosphoadenosine (H)", "H", null, LabelAtoms.None, "H12C10N5O6P", null, 405, true, true);
            AddMod("Phosphoadenosine (K)", "K", null, LabelAtoms.None, "H12C10N5O6P", null, 405, true, true);
            AddMod("Phosphoadenosine (T)", "T", null, LabelAtoms.None, "H12C10N5O6P", null, 405, true, true);
            AddMod("Phosphoadenosine (Y)", "Y", null, LabelAtoms.None, "H12C10N5O6P", null, 405, true, true);
            AddMod("Phosphoguanosine (H)", "H", null, LabelAtoms.None, "H12C10N5O7P", null, 413, true, true);
            AddMod("Phosphoguanosine (K)", "K", null, LabelAtoms.None, "H12C10N5O7P", null, 413, true, true);
            AddMod("PhosphoHex (S)", "S", null, LabelAtoms.None, "H11C6O8P", null, 429, true, true);
            AddMod("PhosphoHexNAc (S)", "S", null, LabelAtoms.None, "H14C8NO8P", null, 428, true, true);
            AddMod("PhosphoHexNAc (T)", "T", null, LabelAtoms.None, "H14C8NO8P", null, 428, true, true);
            AddMod("Phosphopantetheine (S)", "S", null, LabelAtoms.None, "H21C11N2O6PS", null, 49, true, true);
            AddMod("Phosphopropargyl (S)", "S", null, LabelAtoms.None, "H4C3NO2P", null, 959, true, true);
            AddMod("Phosphopropargyl (T)", "T", null, LabelAtoms.None, "H4C3NO2P", null, 959, true, true);
            AddMod("Phosphopropargyl (Y)", "Y", null, LabelAtoms.None, "H4C3NO2P", null, 959, true, true);
            AddMod("PhosphoribosyldephosphoCoA (S)", "S", null, LabelAtoms.None, "H42C26N7O19P3S", null, 395, true, true);
            AddMod("PhosphoUridine (H)", "H", null, LabelAtoms.None, "H11C9N2O8P", null, 417, true, true);
            AddMod("PhosphoUridine (Y)", "Y", null, LabelAtoms.None, "H11C9N2O8P", null, 417, true, true);
            AddMod("Phycocyanobilin (C)", "C", null, LabelAtoms.None, "H38C33N4O6", null, 387, true, true);
            AddMod("Phycoerythrobilin (C)", "C", null, LabelAtoms.None, "H40C33N4O6", null, 388, true, true);
            AddMod("Phytochromobilin (C)", "C", null, LabelAtoms.None, "H36C33N4O6", null, 389, true, true);
            AddMod("Piperidine (K)", "K", null, LabelAtoms.None, "H8C5", null, 520, true, true);
            AddMod("Piperidine (N-term)", null, ModTerminus.N, LabelAtoms.None, "H8C5", null, 520, true, true);
            AddMod("Pro->pyro-Glu (P)", "P", null, LabelAtoms.None, "O - H2", null, 359, true, true);
            AddMod("Pro->Pyrrolidinone (P)", "P", null, LabelAtoms.None, "-H2CO", null, 360, true, true);
            AddMod("Pro->Pyrrolidone (P)", "P", null, LabelAtoms.None, "-CO", null, 369, true, true);
            AddMod("probiotinhydrazide (P)", "P", null, LabelAtoms.None, "H18C10N4O2S", null, 357, true, true);
            AddMod("Propargylamine (C-term)", null, ModTerminus.C, LabelAtoms.None, "H3C3N - O", null, 958, true, true);
            AddMod("Propargylamine (D)", "D", null, LabelAtoms.None, "H3C3N - O", null, 958, true, true);
            AddMod("Propargylamine (E)", "E", null, LabelAtoms.None, "H3C3N - O", null, 958, true, true);
            AddMod("Propionamide:2H(3) (C)", "C", null, LabelAtoms.None, "H2H'3C3NO", null, 97, true, true);
            AddMod("Propionyl (K)", "K", null, LabelAtoms.None, "H4C3O", null, 58, true, true);
            AddMod("Propionyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H4C3O", null, 58, true, true);
            AddMod("Propionyl (S)", "S", null, LabelAtoms.None, "H4C3O", null, 58, true, true);
            AddMod("Propionyl:13C(3) (K)", "K", null, LabelAtoms.None, "H4C'3O", null, 59, true, true);
            AddMod("Propionyl:13C(3) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H4C'3O", null, 59, true, true);
            AddMod("PropylNAGthiazoline (C)", "C", null, LabelAtoms.None, "H14C9NO4S", null, 514, true, true);
            AddMod("Puromycin (C-term)", null, ModTerminus.C, LabelAtoms.None, "H27C22N7O4", null, 973, true, true);
            AddMod("PyMIC (N-term)", null, ModTerminus.N, LabelAtoms.None, "H6C7N2O", null, 501, true, true);
            AddMod("PyridoxalPhosphate (K)", "K", null, LabelAtoms.None, "H8C8NO5P", null, 46, true, true);
            AddMod("Pyridylacetyl (K)", "K", null, LabelAtoms.None, "H5C7NO", null, 25, true, true);
            AddMod("Pyridylacetyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H5C7NO", null, 25, true, true);
            AddMod("pyrophospho (S)", "S", null, LabelAtoms.None, "H2O6P2", new [] { new FragmentLoss("H3O7P2"), }, 898, true, true);
            AddMod("pyrophospho (T)", "T", null, LabelAtoms.None, "H2O6P2", new [] { new FragmentLoss("H3O7P2"), }, 898, true, true);
            AddMod("PyruvicAcidIminyl (K)", "K", null, LabelAtoms.None, "H2C3O2", null, 422, true, true);
            AddMod("PyruvicAcidIminyl (Protein N-term C)", "C", ModTerminus.N, LabelAtoms.None, "H2C3O2", null, 422, true, true);
            AddMod("PyruvicAcidIminyl (Protein N-term V)", "V", ModTerminus.N, LabelAtoms.None, "H2C3O2", null, 422, true, true);
            AddMod("QAT (C)", "C", null, LabelAtoms.None, "H19C9N2O", null, 195, true, true);
            AddMod("QAT:2H(3) (C)", "C", null, LabelAtoms.None, "H16H'3C9N2O", null, 196, true, true);
            AddMod("QEQTGG (K)", "K", null, LabelAtoms.None, "H36C23N8O11", null, 876, true, true);
            AddMod("QQQTGG (K)", "K", null, LabelAtoms.None, "H37C23N9O10", null, 877, true, true);
            AddMod("Quinone (W)", "W", null, LabelAtoms.None, "O2 - H2", null, 392, true, true);
            AddMod("Quinone (Y)", "Y", null, LabelAtoms.None, "O2 - H2", null, 392, true, true);
            AddMod("Retinylidene (K)", "K", null, LabelAtoms.None, "H26C20", null, 380, true, true);
            AddMod("SecCarbamidomethyl (C)", "C", null, LabelAtoms.None, "H3C2NOSe - S", null, 1008, true, true);
            AddMod("Ser->LacticAcid (Protein N-term S)", "S", ModTerminus.N, LabelAtoms.None, "-HN", null, 403, true, true);
            AddMod("SMA (K)", "K", null, LabelAtoms.None, "H9C6NO2", null, 29, true, true);
            AddMod("SMA (N-term)", null, ModTerminus.N, LabelAtoms.None, "H9C6NO2", null, 29, true, true);
            AddMod("SMCC-maleimide (C)", "C", null, LabelAtoms.None, "H27C17N3O3", null, 908, true, true);
            AddMod("SPITC (K)", "K", null, LabelAtoms.None, "H5C7NO3S2", null, 261, true, true);
            AddMod("SPITC (N-term)", null, ModTerminus.N, LabelAtoms.None, "H5C7NO3S2", null, 261, true, true);
            AddMod("SPITC:13C(6) (K)", "K", null, LabelAtoms.None, "H5CC'6NO3S2", null, 464, true, true);
            AddMod("SPITC:13C(6) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H5CC'6NO3S2", null, 464, true, true);
            AddMod("Succinyl (K)", "K", null, LabelAtoms.None, "H4C4O3", null, 64, true, true);
            AddMod("Succinyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H4C4O3", null, 64, true, true);
            AddMod("Succinyl:13C(4) (K)", "K", null, LabelAtoms.None, "H4C'4O3", null, 66, true, true);
            AddMod("Succinyl:13C(4) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H4C'4O3", null, 66, true, true);
            AddMod("Succinyl:2H(4) (K)", "K", null, LabelAtoms.None, "H'4C4O3", null, 65, true, true);
            AddMod("Succinyl:2H(4) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H'4C4O3", null, 65, true, true);
            AddMod("SulfanilicAcid (C-term)", null, ModTerminus.C, LabelAtoms.None, "H5C6NO2S", null, 285, true, true);
            AddMod("SulfanilicAcid (D)", "D", null, LabelAtoms.None, "H5C6NO2S", null, 285, true, true);
            AddMod("SulfanilicAcid (E)", "E", null, LabelAtoms.None, "H5C6NO2S", null, 285, true, true);
            AddMod("SulfanilicAcid:13C(6) (C-term)", null, ModTerminus.C, LabelAtoms.None, "H5C'6NO2S", null, 286, true, true);
            AddMod("SulfanilicAcid:13C(6) (D)", "D", null, LabelAtoms.None, "H5C'6NO2S", null, 286, true, true);
            AddMod("SulfanilicAcid:13C(6) (E)", "E", null, LabelAtoms.None, "H5C'6NO2S", null, 286, true, true);
            AddMod("Sulfide (C)", "C", null, LabelAtoms.None, "S", null, 421, true, true);
            AddMod("Sulfide (D)", "D", null, LabelAtoms.None, "S", null, 421, true, true);
            AddMod("Sulfo (C)", "C", null, LabelAtoms.None, "O3S", null, 40, true, true);
            AddMod("sulfo+amino (Y)", "Y", null, LabelAtoms.None, "HNO3S", null, 997, true, true);
            AddMod("Sulfo-NHS-LC-LC-Biotin (K)", "K", null, LabelAtoms.None, "H36C22N4O4S", null, 523, true, true);
            AddMod("Sulfo-NHS-LC-LC-Biotin (N-term)", null, ModTerminus.N, LabelAtoms.None, "H36C22N4O4S", null, 523, true, true);
            AddMod("SulfoGMBS (C)", "C", null, LabelAtoms.None, "H26C22N4O5S", null, 942, true, true);
            AddMod("SUMO2135 (K)", "K", null, LabelAtoms.None, "H137C90N21O37S", null, 960, true, true);
            AddMod("SUMO3549 (K)", "K", null, LabelAtoms.None, "H224C150N38O60S", null, 961, true, true);
            AddMod("Thiazolidine (N-term C)", "C", ModTerminus.N, LabelAtoms.None, "C", null, 1009, true, true);
            AddMod("Thioacyl (K)", "K", null, LabelAtoms.None, "H4C3OS", null, 126, true, true);
            AddMod("Thioacyl (N-term)", null, ModTerminus.N, LabelAtoms.None, "H4C3OS", null, 126, true, true);
            AddMod("thioacylPA (K)", "K", null, LabelAtoms.None, "H9C6NO2S", null, 967, true, true);
            AddMod("Thiophos-S-S-biotin (S)", "S", null, LabelAtoms.None, "H34C19N4O5PS3", new [] { new FragmentLoss("H34C19N4O5PS3"), }, 332, true, true);
            AddMod("Thiophos-S-S-biotin (T)", "T", null, LabelAtoms.None, "H34C19N4O5PS3", new [] { new FragmentLoss("H34C19N4O5PS3"), }, 332, true, true);
            AddMod("Thiophos-S-S-biotin (Y)", "Y", null, LabelAtoms.None, "H34C19N4O5PS3", new [] { new FragmentLoss("H34C19N4O5PS3"), }, 332, true, true);
            AddMod("Thiophospho (S)", "S", null, LabelAtoms.None, "HO2PS", null, 260, true, true);
            AddMod("Thiophospho (T)", "T", null, LabelAtoms.None, "HO2PS", null, 260, true, true);
            AddMod("Thiophospho (Y)", "Y", null, LabelAtoms.None, "HO2PS", null, 260, true, true);
            AddMod("Thrbiotinhydrazide (T)", "T", null, LabelAtoms.None, "H16C10N4OS", null, 361, true, true);
            AddMod("Thyroxine (Y)", "Y", null, LabelAtoms.None, "C6OI4", null, 398, true, true);
            AddMod("TMAB (K)", "K", null, LabelAtoms.None, "H14C7NO", new [] { new FragmentLoss("H9C3N"), }, 476, true, true);
            AddMod("TMAB (N-term)", null, ModTerminus.N, LabelAtoms.None, "H14C7NO", new [] { new FragmentLoss("H9C3N"), }, 476, true, true);
            AddMod("TMAB:2H(9) (K)", "K", null, LabelAtoms.None, "H5H'9C7NO", new [] { new FragmentLoss("H'9C3N"), }, 477, true, true);
            AddMod("TMAB:2H(9) (N-term)", null, ModTerminus.N, LabelAtoms.None, "H5H'9C7NO", new [] { new FragmentLoss("H'9C3N"), }, 477, true, true);
            AddMod("TMPP-Ac (N-term)", null, ModTerminus.N, LabelAtoms.None, "H33C29O10P", null, 827, true, true);
            AddMod("TNBS (K)", "K", null, LabelAtoms.None, "HC6N3O6", null, 751, true, true);
            AddMod("TNBS (N-term)", null, ModTerminus.N, LabelAtoms.None, "HC6N3O6", null, 751, true, true);
            AddMod("trifluoro (L)", "L", null, LabelAtoms.None, "F3 - H3", null, 750, true, true);
            AddMod("Triiodo (Y)", "Y", null, LabelAtoms.None, "I3 - H3", null, 131, true, true);
            AddMod("Triiodothyronine (Y)", "Y", null, LabelAtoms.None, "HC6OI3", null, 397, true, true);
            AddMod("Trimethyl (K)", "K", null, LabelAtoms.None, "H6C3", null, 37, true, true);
            AddMod("Trimethyl (Protein N-term A)", "A", ModTerminus.N, LabelAtoms.None, "H6C3", null, 37, true, true);
            AddMod("Trimethyl (R)", "R", null, LabelAtoms.None, "H6C3", null, 37, true, true);
            AddMod("Trioxidation (C)", "C", null, LabelAtoms.None, "O3", null, 345, true, true);
            AddMod("Tripalmitate (Protein N-term C)", "C", ModTerminus.N, LabelAtoms.None, "H96C51O5", null, 51, true, true);
            AddMod("Trp->Hydroxykynurenin (W)", "W", null, LabelAtoms.None, "O2 - C", null, 350, true, true);
            AddMod("Trp->Kynurenin (W)", "W", null, LabelAtoms.None, "O - C", null, 351, true, true);
            AddMod("Trp->Oxolactone (W)", "W", null, LabelAtoms.None, "O - H2", null, 288, true, true);
            AddMod("Tyr->Dha (Y)", "Y", null, LabelAtoms.None, "-H6C6O", null, 400, true, true);
            AddMod("VFQQQTGG (K)", "K", null, LabelAtoms.None, "H55C37N11O12", null, 932, true, true);
            AddMod("VIEVYQEQTGG (K)", "K", null, LabelAtoms.None, "H81C53N13O19", null, 933, true, true);
            AddMod("Xlink:B10621 (C)", "C", null, LabelAtoms.None, "H30C31N4O6SI", null, 323, true, true);
            AddMod("Xlink:DMP (K)", "K", null, LabelAtoms.None, "H10C7N2", null, 456, true, true);
            AddMod("Xlink:DMP-s (K)", "K", null, LabelAtoms.None, "H14C8N2O", null, 455, true, true);
            AddMod("Xlink:SSD (K)", "K", null, LabelAtoms.None, "H15C12NO5", null, 273, true, true);
            AddMod("ZGB (K)", "K", null, LabelAtoms.None, "H53C37N6O6F2SB", null, 861, true, true);
            AddMod("ZGB (N-term)", null, ModTerminus.N, LabelAtoms.None, "H53C37N6O6F2SB", null, 861, true, true);
            AddMod("Label:13C(1)2H(3) (M)", "M", null, LabelAtoms.None, "H'3C' - H3C", null, 862, false, true);
            AddMod("Label:13C(5) (P)", "P", null, LabelAtoms.C13, null, null, 772, false, true);
            AddMod("Label:13C(5)15N(1) (M)", "M", null, LabelAtoms.C13|LabelAtoms.N15, null, null, 268, false, true);
            AddMod("Label:13C(5)15N(1) (P)", "P", null, LabelAtoms.C13|LabelAtoms.N15, null, null, 268, false, true);
            AddMod("Label:13C(5)15N(1) (V)", "V", null, LabelAtoms.C13|LabelAtoms.N15, null, null, 268, false, true);
            AddMod("Label:13C(6) (I)", "I", null, LabelAtoms.C13, null, null, 188, false, true);
            AddMod("Label:13C(6) (K)", "K", null, LabelAtoms.C13, null, null, 188, false, true);
            AddMod("Label:13C(6) (L)", "L", null, LabelAtoms.C13, null, null, 188, false, true);
            AddMod("Label:13C(6) (R)", "R", null, LabelAtoms.C13, null, null, 188, false, true);
            AddMod("Label:13C(6)15N(1) (I)", "I", null, LabelAtoms.C13|LabelAtoms.N15, null, null, 695, false, true);
            AddMod("Label:13C(6)15N(1) (L)", "L", null, LabelAtoms.C13|LabelAtoms.N15, null, null, 695, false, true);
            AddMod("Label:13C(6)15N(2) (K)", "K", null, LabelAtoms.C13|LabelAtoms.N15, null, null, 259, false, true);
            AddMod("Label:13C(6)15N(4) (R)", "R", null, LabelAtoms.C13|LabelAtoms.N15, null, null, 267, false, true);
            AddMod("Label:13C(9) (F)", "F", null, LabelAtoms.C13, null, null, 184, false, true);
            AddMod("Label:13C(9) (Y)", "Y", null, LabelAtoms.C13, null, null, 184, false, true);
            AddMod("Label:13C(9)15N(1) (F)", "F", null, LabelAtoms.C13|LabelAtoms.N15, null, null, 269, false, true);
            AddMod("Label:15N(1) (A)", "A", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (C)", "C", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (D)", "D", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (E)", "E", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (F)", "F", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (G)", "G", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (I)", "I", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (L)", "L", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (M)", "M", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (P)", "P", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (S)", "S", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (T)", "T", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (V)", "V", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(1) (Y)", "Y", null, LabelAtoms.N15, null, null, 994, false, true);
            AddMod("Label:15N(2) (K)", "K", null, LabelAtoms.N15, null, null, 995, false, true);
            AddMod("Label:15N(2) (N)", "N", null, LabelAtoms.N15, null, null, 995, false, true);
            AddMod("Label:15N(2) (Q)", "Q", null, LabelAtoms.N15, null, null, 995, false, true);
            AddMod("Label:15N(2) (W)", "W", null, LabelAtoms.N15, null, null, 995, false, true);
            AddMod("Label:15N(2)2H(9) (K)", "K", null, LabelAtoms.None, "H'9N'2 - H9N2", null, 944, false, true);
            AddMod("Label:15N(3) (H)", "H", null, LabelAtoms.N15, null, null, 996, false, true);
            AddMod("Label:15N(4) (R)", "R", null, LabelAtoms.N15, null, null, 897, false, true);
            AddMod("Label:18O(1) (S)", "S", null, LabelAtoms.None, "O' - O", null, 258, false, true);
            AddMod("Label:18O(1) (T)", "T", null, LabelAtoms.None, "O' - O", null, 258, false, true);
            AddMod("Label:18O(1) (Y)", "Y", null, LabelAtoms.None, "O' - O", null, 258, false, true);
            AddMod("Label:2H(3) (L)", "L", null, LabelAtoms.None, "H'3 - H3", null, 262, false, true);
            AddMod("Label:2H(4) (F)", "F", null, LabelAtoms.None, "H'4 - H4", null, 481, false, true);
            AddMod("Label:2H(4) (K)", "K", null, LabelAtoms.None, "H'4 - H4", null, 481, false, true);
            AddMod("Label:2H(4) (Y)", "Y", null, LabelAtoms.None, "H'4 - H4", null, 481, false, true);
            AddMod("Label:2H(9)13C(6)15N(2) (K)", "K", null, LabelAtoms.None, "H'9C'6N'2 - H9C6N2", null, 696, false, true);

            // Hardcoded Skyline Mods
            AddMod("Label:15N", null, null, LabelAtoms.N15, null, null, null, false, false);
            AddMod("Label:13C", null, null, LabelAtoms.C13, null, null, null, false, false);
            AddMod("Label:13C15N", null, null, LabelAtoms.N15 | LabelAtoms.C13, null, null, null, false, false);
            AddMod("Label:13C(6)15N(2) (C-term K)", "K", ModTerminus.C, LabelAtoms.C13 | LabelAtoms.N15, null, null, null, false, false);
            AddMod("Label:13C(6)15N(4) (C-term R)", "R", ModTerminus.C, LabelAtoms.C13 | LabelAtoms.N15, null, null, null, false, false);
            AddMod("Label:13C(6) (C-term K)", "K", ModTerminus.C, LabelAtoms.C13, null, null, null, false, false);
            AddMod("Label:13C(6) (C-term R)", "R", ModTerminus.C, LabelAtoms.C13, null, null, null, false, false);

            MassLookup.Complete();

            INITIALIZING = false;
        }
        
        private static void AddMod(string name, string aas, ModTerminus? terminus, LabelAtoms labelAtoms, string formula, FragmentLoss[] losses,
            int? id, bool structural, bool hidden)
        {
            var newMod =
                new StaticMod(name, aas, terminus, false, formula, labelAtoms, RelativeRT.Matching, null, null, losses, id);
            AddMod(newMod, id, structural, hidden);
        }

        private static void AddMod(StaticMod mod, int? id, bool structural, bool hidden)
        {
            // Add to dictionary by name.
            Dictionary<string, StaticMod> dictNames;
            if (structural)
                dictNames = hidden ? DictHiddenStructuralModNames : DictStructuralModNames;
            else
                dictNames = hidden ? DictHiddenIsotopeModNames : DictIsotopeModNames;
            dictNames.Add(mod.Name, mod);
            
            bool allAas = mod.AAs == null;
            IEnumerable<char> aas = allAas ? AMINO_ACIDS : mod.AminoAcids;
            foreach(char aa in aas)
            {
                // Add to mass lookup.
                MassLookup.Add(aa, mod, structural, true);
                
                // Add to dictionary by ID.
                if (id == null)
                    continue;
                var idKey = new UniModIdKey
                {
                    Id = (int) id,
                    Aa = aa,
                    AllAas = allAas,
                    Terminus = mod.Terminus
                };
                if (!DictUniModIds.ContainsKey(idKey))
                    DictUniModIds.Add(idKey, mod);
            }
        }

        /// <summary>
        /// Searches for A UniMod modification by name.
        /// </summary>
        public static StaticMod GetModification(string modName, bool structural)
        {
            StaticMod mod;
            var dict = structural ? DictStructuralModNames : DictIsotopeModNames;
            var hiddenDict = structural ? DictHiddenStructuralModNames : DictHiddenIsotopeModNames;
            dict.TryGetValue(modName, out mod);
            if (mod == null)
                hiddenDict.TryGetValue(modName, out mod);
            return mod;
        }

        /// <summary>
        /// Searches for a UniMod modification by modification definition.
        /// </summary>
        public static StaticMod FindMatchingStaticMod(StaticMod modToMatch, bool structural)
        {       
            var dict = structural ? DictStructuralModNames : DictIsotopeModNames;
            var hiddenDict = structural ? DictHiddenStructuralModNames : DictHiddenIsotopeModNames;
            foreach (StaticMod mod in dict.Values)
            {
                if (mod.Equivalent(modToMatch))
                    return mod;
            }
            foreach (StaticMod mod in hiddenDict.Values)
            {
                if (mod.Equivalent(modToMatch))
                    return mod;
            }
            return null;
        }

        public static bool ValidateID(StaticMod mod)
        {
            if (INITIALIZING || mod.UnimodId == null)
                return true;
            var idKey = new UniModIdKey
            {
                Aa = mod.AAs == null ? 'A' : mod.AminoAcids.First(),
                AllAas = mod.AAs == null,
                Id = (int)mod.UnimodId,
                Terminus = mod.Terminus
            };
            StaticMod unimod;
            return DictUniModIds.TryGetValue(idKey, out unimod) 
                && Equals(mod.Name, unimod.Name) 
                && mod.Equivalent(unimod);
        }

        public struct UniModIdKey
        {
            public int Id { get; set; }
            public char Aa { get; set; }
            public bool AllAas { get; set; }
            public ModTerminus? Terminus { get; set; }
        }
    }


    public class ModMassLookup
    {
        private static readonly SequenceMassCalc CALC = new SequenceMassCalc(MassType.Monoisotopic);
        private readonly AAMassLookup[] _aaMassLookups;
        private bool _completed;

        public ModMassLookup()
        {
            _aaMassLookups = new AAMassLookup[128];
            foreach (char aa in UniMod.AMINO_ACIDS)
            {
                _aaMassLookups[aa] = new AAMassLookup();
                _aaMassLookups[Char.ToLower(aa)] = new AAMassLookup();
            }
        }

        public void Add(char aa, StaticMod mod, bool structural, bool allowDuplicates)
        {
            if (_completed)
                throw new InvalidOperationException("Invalid attempt to add data to completed MassLookup.");
            // If structural, store in lowercase AA.
            _aaMassLookups[structural ? Char.ToLower(aa) : Char.ToUpper(aa)]
                .Add(CALC.GetModMass(aa, mod), mod, allowDuplicates);
        }

        public StaticMod MatchModificationMass(double mass, char aa, int roundTo, bool structural,
            ModTerminus? terminus, bool specific)
        {
            if (!_completed)
                throw new InvalidOperationException("Invalid attempt to access incomplete MassLookup.");
            return _aaMassLookups[structural ? Char.ToLower(aa) : Char.ToUpper(aa)]
                .ClosestMatch(mass, roundTo, terminus, specific);
        }

        public void Complete()
        {
            foreach (char aa in UniMod.AMINO_ACIDS)
            {
                _aaMassLookups[aa].Sort();
                _aaMassLookups[Char.ToLower(aa)].Sort();
            }
            _completed = true;
        }
    }

    public class AAMassLookup
    {
        private readonly List<KeyValuePair<double, StaticMod>> _listMasses =
            new List<KeyValuePair<double, StaticMod>>();
        private readonly List<KeyValuePair<double, StaticMod>> _listCTerminalMasses =
            new List<KeyValuePair<double, StaticMod>>();
        private readonly List<KeyValuePair<double, StaticMod>> _listNTerminalMasses =
            new List<KeyValuePair<double, StaticMod>>();
        private readonly List<KeyValuePair<double, StaticMod>> _listAllAAsMasses =
            new List<KeyValuePair<double, StaticMod>>();

        private static readonly IComparer<KeyValuePair<double, StaticMod>> MASS_COMPARER = new MassComparer();

        public void Add(double mass, StaticMod mod, bool allowDuplicates)
        {
            var list = _listMasses;
            switch (mod.Terminus)
            {
                case null:
                    if (string.IsNullOrEmpty(mod.AAs))
                        list = _listAllAAsMasses;
                    break;
                case ModTerminus.C:
                    list = _listCTerminalMasses;
                    break;
                case ModTerminus.N:
                    list = _listNTerminalMasses;
                    break;
            }
            if (allowDuplicates || !list.Contains(pair => pair.Key == mass))
                list.Add(new KeyValuePair<double, StaticMod>(mass, mod));

        }

        public void Sort()
        {
            _listMasses.Sort(MASS_COMPARER);
            _listCTerminalMasses.Sort(MASS_COMPARER);
            _listNTerminalMasses.Sort(MASS_COMPARER);
            _listAllAAsMasses.Sort(MASS_COMPARER);
        }

        public StaticMod ClosestMatch(double mass, int roundTo, ModTerminus? terminus, bool specific)
        {
            return specific
               ? ClosestMatchSpecific(mass, roundTo, terminus)
               : ClosestMatchGeneral(mass, roundTo, terminus);
        }

        public StaticMod ClosestMatchSpecific(double mass, int roundTo, ModTerminus? terminus)
        {
            StaticMod match = null;
            if (terminus != null)
            {
                match = ClosestMatch(terminus == ModTerminus.C ? _listCTerminalMasses : _listNTerminalMasses, mass,
                                     roundTo);
            }
            return match
                ?? ClosestMatch(_listMasses, mass, roundTo)
                ?? ClosestMatch(_listAllAAsMasses, mass, roundTo);
        }

        public StaticMod ClosestMatchGeneral(double mass, int roundTo, ModTerminus? terminus)
        {
            StaticMod match = ClosestMatch(_listAllAAsMasses, mass, roundTo);
            if (match == null && terminus != null)
            {
                match = ClosestMatch(terminus == ModTerminus.C ? _listCTerminalMasses : _listNTerminalMasses, mass,
                                     roundTo);
            }
            return match ?? ClosestMatch(_listMasses, mass, roundTo);
        }

        private static StaticMod ClosestMatch(List<KeyValuePair<double, StaticMod>> listSearch, double mass, int roundTo)
        {
            if (listSearch.Count == 0)
                return null;
            int i = listSearch.BinarySearch(new KeyValuePair<double, StaticMod>(mass, null), MASS_COMPARER);
            i = i < 0 ? ~i : i;
            var match = listSearch[i == listSearch.Count ? i - 1 : i];
            if (Math.Round(match.Key, roundTo) == mass)
                return match.Value;
            if (i > 0)
            {
                match = listSearch[i - 1];
                if (Math.Round(match.Key, roundTo) == mass)
                    return match.Value;
            }
            return null;
        }

        internal class MassComparer : IComparer<KeyValuePair<double, StaticMod>>
        {
            public int Compare(KeyValuePair<double, StaticMod> s1, KeyValuePair<double, StaticMod> s2)
            {
                return Comparer<double>.Default.Compare(s1.Key, s2.Key);
            }
        }
    }
}
//Impossible Modification: Label:13C(8)15N(2) (R)
//Unable to match: Acetyl (Protein N-term)
//Unable to match: Amidated (Protein C-term)
//Unable to match: Formyl (Protein N-term)
//Unable to match: ICPL (Protein N-term)
//Unable to match: ICPL:13C(6) (Protein N-term)
//Unable to match: ICPL:13C(6)2H(4) (Protein N-term)
//Unable to match: ICPL:2H(4) (Protein N-term)
//Unable to match: AEBS (Protein N-term)
//Unable to match: BDMAPP (Protein N-term)
//Unable to match: Biotin-PEO-Amine (Protein C-term)
//Unable to match: CAMthiopropanoyl (Protein N-term)
//Unable to match: Cholesterol (Protein C-term)
//Unable to match: Delta:H(2)C(2) (Protein N-term)
//Unable to match: Delta:H(4)C(3) (Protein N-term)
//Unable to match: Dimethyl:2H(4) (Protein N-term)
//Unable to match: DTBP (Protein N-term)
//Unable to match: Ethyl (Protein N-term)
//Unable to match: FormylMet (Protein N-term)
//Unable to match: Glu (Protein C-term)
//Unable to match: Glucuronyl (Protein N-term)
//Unable to match: GluGlu (Protein C-term)
//Unable to match: GluGluGlu (Protein C-term)
//Unable to match: GluGluGluGlu (Protein C-term)
//Unable to match: GPIanchor (Protein C-term)
//Unable to match: LG-Hlactam-K (Protein N-term)
//Unable to match: LG-lactam-K (Protein N-term)
//Unable to match: Methyl (Protein N-term)
//Unable to match: Microcin (Protein C-term)
//Unable to match: MicrocinC7 (Protein C-term)
//Unable to match: Palmitoyl (Protein N-term)
//Unable to match: Succinyl (Protein N-term)
//Unable to match: Xlink:DMP (Protein N-term)
//Unable to match: Xlink:DMP-s (Protein N-term)
