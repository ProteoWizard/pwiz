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
namespace pwiz.Skyline.Model.DocSettings
{
    public static class UniModData
    {
        public static readonly UniModModificationData[] UNI_MOD_DATA = new[]
        {
            new UniModModificationData
            {
                 Name = "Acetyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C2O", ID = 1, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H2C2O", ID = 1, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Amidated (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "HN - O", ID = 2, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Ammonia-loss (N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H3N", ID = 385, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Biotin (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H14C10N2O2S", ID = 3, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Biotin (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H14C10N2O2S", ID = 3, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Carboxymethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2C2O2", ID = 6, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Na (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Na - H", ID = 30, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Na (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Na - H", ID = 30, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "cysTMT6plex (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H25C10C'4N2N'O2S", ID = 985, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Deamidated (NQ)", 
                 AAs = "N, Q", LabelAtoms = LabelAtoms.None, Formula = "O - HN", ID = 7, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Dehydro (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "-H", ID = 374, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Ethanolyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4C2O", ID = 278, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ExacTagAmine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H52C25C'12N8N'6O19S", ID = 741, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ExacTagThiol (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H50C23C'12N8N'6O18", ID = 740, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Formyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "CO", ID = 122, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Gln->pyro-Glu (N-term Q)", 
                 AAs = "Q", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H3N", ID = 28, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Glu->pyro-Glu (N-term E)", 
                 AAs = "E", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 27, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Guanidinyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2CN2", ID = 52, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-C (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H17C10N3O3", ID = 105, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-C:13C(9) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H17CC'9N3O3", ID = 106, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICPL (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3C6NO", ID = 365, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:13C(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3C'6NO", ID = 364, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:13C(6)2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4C'6NO - H", ID = 866, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:13C(6)2H(4) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'4C'6NO - H", ID = 866, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4C6NO - H", ID = 687, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 214, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 214, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 214, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H24C7C'7N3N'O3", ID = 730, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H24C7C'7N3N'O3", ID = 730, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H24C7C'7N3N'O3", ID = 730, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Met->Hse (C-term M)", 
                 AAs = "M", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "O - H2CS", ID = 10, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Met->Hsl (C-term M)", 
                 AAs = "M", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H4CS", ID = 11, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Methylthio (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2CS", ID = 39, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C7N2O", ID = 888, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H12C7N2O", ID = 888, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C7N2O", ID = 888, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(3)15N(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 889, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(3)15N(1) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 889, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(3)15N(1) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 889, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "NIPCAM (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C5NO", ID = 17, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (HW)", 
                 AAs = "H, W", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "O", Losses = new [] { new FragmentLoss("H4COS"), }, ID = 35, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "HO3P", Losses = new [] { new FragmentLoss("H3O4P"), }, ID = 21, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Propionamide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H5C3NO", ID = 24, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Pyridylethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H7C7N", ID = 31, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Pyro-carbamidomethyl (N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C2O", ID = 26, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Sulfo (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "O3S", ID = 40, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Sulfo (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "O3S", ID = 40, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Sulfo (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "O3S", ID = 40, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMT (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C12N2O2", ID = 739, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMT (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H20C12N2O2", ID = 739, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMT2plex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C11C'N2O2", ID = 738, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMT2plex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H20C11C'N2O2", ID = 738, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMT6plex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C8C'4NN'O2", ID = 737, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMT6plex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H20C8C'4NN'O2", ID = 737, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Label:18O(1) (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "O' - O", ID = 258, 
                 Structural = false, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Label:18O(2) (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "O'2 - O2", ID = 193, 
                 Structural = false, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "15dB-biotin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H54C35N4O4S", ID = 538, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "2-succinyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4C4O4", ID = 957, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "2HPG (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H10C16O5", ID = 187, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "3-deoxyglucosone (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H8C6O4", ID = 949, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "3sulfo (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C7O4S", ID = 748, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "4-ONE (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H14C9O2", ID = 721, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "4-ONE (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H14C9O2", ID = 721, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "4-ONE (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H14C9O2", ID = 721, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "4-ONE+Delta:H(-2)O(-1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H12C9O", ID = 743, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "4-ONE+Delta:H(-2)O(-1) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H12C9O", ID = 743, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "4-ONE+Delta:H(-2)O(-1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C9O", ID = 743, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "4AcAllylGal (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H24C17O9", ID = 901, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "a-type-ion (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H2CO2", ID = 140, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AccQTag (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C10N2O", ID = 194, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AccQTag (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H6C10N2O", ID = 194, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2C2O", ID = 1, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H2C2O", ID = 1, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H2C2O", ID = 1, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H2C2O", ID = 1, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H2C2O", ID = 1, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl:2H(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'3C2O - H", ID = 56, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl:2H(3) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'3C2O - H", ID = 56, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", ID = 213, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", ID = 213, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", ID = 213, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", ID = 213, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", ID = 213, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEBS (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H9C8NO2S", ID = 276, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEBS (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C8NO2S", ID = 276, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEBS (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H9C8NO2S", ID = 276, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEBS (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H9C8NO2S", ID = 276, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEC-MAEC (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H5C2NS - O", ID = 472, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEC-MAEC (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H5C2NS - O", ID = 472, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEC-MAEC:2H(4) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "HH'4C2NS - O", ID = 792, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEC-MAEC:2H(4) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "HH'4C2NS - O", ID = 792, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AHA-Alkyne (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H5C4N5O - S", ID = 1000, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AHA-Alkyne-KDDDD (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H37C26N11O14 - S", ID = 1001, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Amidine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3C2N", ID = 141, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Amidine (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H3C2N", ID = 141, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Amidino (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2CN2", ID = 440, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Amino (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HN", ID = 342, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ammonia-loss (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "-H3N", ID = 385, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ammonia-loss (Protein N-term S)", 
                 AAs = "S", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H3N", ID = 385, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ammonia-loss (Protein N-term T)", 
                 AAs = "T", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H3N", ID = 385, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ammonium (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H3N", ID = 989, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ammonium (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "H3N", ID = 989, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AMTzHexNAc2 (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H30C19N6O10", ID = 934, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AMTzHexNAc2 (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H30C19N6O10", ID = 934, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AMTzHexNAc2 (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H30C19N6O10", ID = 934, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Archaeol (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H86C43O2", ID = 410, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Arg->GluSA (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "O - H5CN3", ID = 344, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Arg->Npo (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C3NO2 - H", ID = 837, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Arg->Orn (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "-H2CN2", ID = 372, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Arg2PG (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H10C16O4", ID = 848, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Argbiotinhydrazide (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H13C9NO2S", ID = 343, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AROD (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H52C35N10O9S2", ID = 938, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Atto495Maleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H32C27N5O3", ID = 935, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bacillosamine (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H16C10N2O4", ID = 910, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BADGE (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H24C21O4", ID = 493, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BDMAPP (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H12C11NOBr", ID = 684, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BDMAPP (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C11NOBr", ID = 684, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BDMAPP (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H12C11NOBr", ID = 684, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BDMAPP (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C11NOBr", ID = 684, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Benzoyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C7O", ID = 136, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Benzoyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C7O", ID = 136, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BHAc (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H25C16N3O3S", ID = 998, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BHT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H22C15O", ID = 176, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BHT (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H22C15O", ID = 176, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BHT (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H22C15O", ID = 176, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BHTOH (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H22C15O2", ID = 498, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BHTOH (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H22C15O2", ID = 498, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BHTOH (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H22C15O2", ID = 498, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-HPDP (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H32C19N4O3S2", ID = 290, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-maleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H27C20N5O5S", ID = 993, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-PEG-PRA (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H42C26N8O7", ID = 895, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-PEO-Amine (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H28C16N4O3S", ID = 289, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-PEO-Amine (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H28C16N4O3S", ID = 289, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-PEO4-hydrazide (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H37C21N5O6S", ID = 811, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-phenacyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H38C29N8O6S", ID = 774, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-phenacyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H38C29N8O6S", ID = 774, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-phenacyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H38C29N8O6S", ID = 774, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BisANS (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C32N2O6S2", ID = 519, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BITC (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H7C8NS", ID = 978, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BITC (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7C8NS", ID = 978, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BITC (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H7C8NS", ID = 978, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BMOE (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H8C10N2O4", ID = 824, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bodipy (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H21C20N4O3F2B", ID = 878, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bromo (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "Br - H", ID = 340, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bromo (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "Br - H", ID = 340, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bromo (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "Br - H", ID = 340, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bromobimane (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H10C10N2O2", ID = 301, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "C8-QAT (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H29C14NO", ID = 513, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "C8-QAT (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H29C14NO", ID = 513, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CAF (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C3O4S", ID = 272, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CAMthiopropanoyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7C5NO2S", ID = 293, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Can-FP-biotin (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H34C19N3O5PS", ID = 333, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Can-FP-biotin (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H34C19N3O5PS", ID = 333, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Can-FP-biotin (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H34C19N3O5PS", ID = 333, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CarbamidomethylDTT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H11C6NO3S2", ID = 893, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbofuran (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C2NO", ID = 977, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "CO2", ID = 299, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "CO2", ID = 299, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "CO2", ID = 299, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy (Protein N-term M)", 
                 AAs = "M", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "CO2", ID = 299, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "CO2", ID = 299, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy->Thiocarboxy (Protein C-term G)", 
                 AAs = "G", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "S - O", ID = 420, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxyethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3O2", ID = 378, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxymethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C2O2", ID = 6, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxymethyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H2C2O2", ID = 6, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxymethyl (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H2C2O2", ID = 6, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxymethyl:13C(2) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2C'2O2", ID = 775, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CarboxymethylDTT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H10C6O4S2", ID = 894, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ag (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Ag - H", ID = 955, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ag (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Ag - H", ID = 955, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ca[II] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Ca - H2", ID = 951, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ca[II] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Ca - H2", ID = 951, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Cu[I] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Cu - H", ID = 531, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Cu[I] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Cu - H", ID = 531, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Fe[II] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Fe - H2", ID = 952, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Fe[II] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Fe - H2", ID = 952, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:K (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "K - H", ID = 530, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:K (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "K - H", ID = 530, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Li (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Li - H", ID = 950, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Li (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Li - H", ID = 950, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Mg[II] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Mg - H2", ID = 956, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Mg[II] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Mg - H2", ID = 956, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ni[II] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Ni - H2", ID = 953, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ni[II] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Ni - H2", ID = 953, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Zn[II] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Zn - H2", ID = 954, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Zn[II] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Zn - H2", ID = 954, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "cGMP (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H11C10N5O7P", ID = 849, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "cGMP (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H11C10N5O7P", ID = 849, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "cGMP+RMP-loss (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4C5N5O", ID = 851, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "cGMP+RMP-loss (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C5N5O", ID = 851, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CHDH (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H26C17O4", ID = 434, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Chlorination (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "Cl", ID = 936, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Chlorpyrifos (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H10C4O2PS", ID = 975, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Chlorpyrifos (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H10C4O2PS", ID = 975, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Chlorpyrifos (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H10C4O2PS", ID = 975, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ChromoBiotin (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H45C34N7O7S", ID = 884, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_1 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C7N2O", ID = 524, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_1 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H12C7N2O", ID = 524, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_1 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C7N2O", ID = 524, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_2 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C6C'N2O", ID = 525, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_2 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H12C6C'N2O", ID = 525, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_2 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C6C'N2O", ID = 525, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_3 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C11C'N3O4", ID = 536, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_3 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H20C11C'N3O4", ID = 536, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_3 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H20C11C'N3O4", ID = 536, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_4 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C9C'N2O5", ID = 537, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_4 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H15C9C'N2O5", ID = 537, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CLIP_TRAQ_4 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H15C9C'N2O5", ID = 537, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CoenzymeA (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H34C21N7O16P3S", ID = 281, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Crotonaldehyde (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C4O", ID = 253, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Crotonaldehyde (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H6C4O", ID = 253, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Crotonaldehyde (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C4O", ID = 253, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CuSMo (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H24C19N8O15P2S3MoCu", ID = 444, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cy3b-maleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H39C39N4O9F3S", ID = 821, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cyano (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "CN - H", ID = 438, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CyDye-Cy3 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H44C37N4O6S", ID = 494, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CyDye-Cy5 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H44C38N4O6S", ID = 495, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cys->Dha (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "-H2S", ID = 368, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cys->ethylaminoAla (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H5C2N - S", ID = 940, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cys->methylaminoAla (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H3CN - S", ID = 939, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cys->Oxoalanine (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "O - H2S", ID = 402, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cys->PyruvicAcid (Protein N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "O - H3NS", ID = 382, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cysteinyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H5C3NO2S", ID = 312, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "cysTMT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H25C14N3O2S", ID = 984, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DAET (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H9C4NS - O", ID = 178, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DAET (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H9C4NS - O", ID = 178, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dansyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H11C12NO2S", ID = 139, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dansyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H11C12NO2S", ID = 139, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deamidated (Protein N-term F)", 
                 AAs = "F", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "O - HN", ID = 7, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deamidated (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "O - HN", ID = 7, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deamidated:18O(1) (NQ)", 
                 AAs = "N, Q", LabelAtoms = LabelAtoms.None, Formula = "O' - HN", ID = 366, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Decanoyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H18C10O", ID = 449, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Decanoyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H18C10O", ID = 449, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DEDGFLYMVYASQETFG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H122C89N18O31S", Losses = new [] { new FragmentLoss("H2O"), }, ID = 1010, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (Protein C-term N)", 
                 AAs = "N", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (Protein C-term Q)", 
                 AAs = "Q", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(1)O(-1)18O(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "O' - HN", ID = 170, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H2C2", ID = 254, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C2", ID = 254, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C3", ID = 207, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(3)O(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C3O", ID = 319, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(3)O(1) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H2C3O", ID = 319, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(5) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C5", ID = 318, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 255, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 255, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(2)O(-1)S(1) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C2S - O", ID = 327, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H4C3", ID = 256, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3", ID = 256, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3)O(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4C3O", ID = 206, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3)O(1) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H4C3O", ID = 206, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3)O(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3O", ID = 206, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C6", ID = 208, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(5)C(2) (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H5C2", ID = 529, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(6)C(6)O(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C6O", ID = 205, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(8)C(6)O(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C6O2", ID = 209, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:Hg(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "Hg", ID = 291, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:S(-1)Se(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "Se - S", ID = 162, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:S(-1)Se(1) (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "Se - S", ID = 162, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:Se(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "Se", ID = 423, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deoxy (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "-O", ID = 447, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deoxy (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "-O", ID = 447, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deoxy (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "-O", ID = 447, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DeStreak (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4C2OS", ID = 303, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dethiomethyl (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "-H4CS", ID = 526, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DFDNB (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C6N2O4F2", ID = 825, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DFDNB (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H2C6N2O4F2", ID = 825, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DFDNB (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H2C6N2O4F2", ID = 825, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DFDNB (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H2C6N2O4F2", ID = 825, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H10C6O4", ID = 295, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H10C6O4", ID = 295, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H92C56N4O39", ID = 305, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O44", ID = 307, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H112C68N4O49", ID = 308, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DHP (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H8C8N", ID = 488, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diacylglycerol (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H68C37O4", ID = 377, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dibromo (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "Br2 - H2", ID = 534, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dichlorination (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "Cl2", ID = 937, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Didehydro (C-term K)", 
                 AAs = "K", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H2", ID = 401, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Didehydro (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "-H2", ID = 401, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Didehydro (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "-H2", ID = 401, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Didehydro (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "-H2", ID = 401, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Didehydroretinylidene (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H24C20", ID = 433, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C4", ID = 518, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H8C4", ID = 518, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dihydroxyimidazolidine (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H4C3O2", ID = 830, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diiodo (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "I2 - H2", ID = 130, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diironsubcluster (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C5N2O5S2Fe2 - H", ID = 439, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diisopropylphosphate (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H13C6O3P", ID = 362, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diisopropylphosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H13C6O3P", ID = 362, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diisopropylphosphate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H13C6O3P", ID = 362, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diisopropylphosphate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H13C6O3P", ID = 362, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 36, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 36, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 36, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl (Protein N-term P)", 
                 AAs = "P", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 36, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 36, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4C2", ID = 199, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(4) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'4C2", ID = 199, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(4)13C(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4C'2", ID = 510, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(4)13C(2) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'4C'2", ID = 510, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(6)13C(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'6C'2 - H2", ID = 330, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(6)13C(2) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'6C'2 - H2", ID = 330, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(6)13C(2) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H'6C'2 - H2", ID = 330, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DimethylamineGMBS (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H21C13N3O3", ID = 943, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DimethylArsino (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H5C2As", ID = 902, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DimethylpyrroleAdduct (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C6", ID = 316, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diphthamide (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H15C7N2O", ID = 375, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dipyrrolylmethanemethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H22C20N2O8", ID = 416, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dNIC (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "HH'3C6NO", ID = 698, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DNPS (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H3C6N2O4S", ID = 941, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DNPS (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H3C6N2O4S", ID = 941, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DTBP (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C3NS", ID = 324, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DTBP (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H5C3NS", ID = 324, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DTBP (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H5C3NS", ID = 324, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DTBP (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H5C3NS", ID = 324, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DTT_C (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H8C4O2S", ID = 736, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DTT_C:2H(6) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2H'6C4O2S", ID = 764, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DTT_ST (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H8C4OS2", ID = 735, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DTT_ST (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H8C4OS2", ID = 735, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DTT_ST:2H(6) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H2H'6C4OS2", ID = 763, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DTT_ST:2H(6) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H2H'6C4OS2", ID = 763, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DyLight-maleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H48C39N4O15S4", ID = 890, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EDT-iodoacetyl-PEO-biotin (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H34C20N4O4S3", ID = 118, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EDT-iodoacetyl-PEO-biotin (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H34C20N4O4S3", ID = 118, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EDT-maleimide-PEO-biotin (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H39C25N5O6S3", ID = 93, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EDT-maleimide-PEO-biotin (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H39C25N5O6S3", ID = 93, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EGCG1 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H16C22O11", ID = 1002, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EGCG2 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H11C15O6", ID = 1003, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EQAT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H20C10N2O", ID = 197, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EQAT:2H(5) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H15H'5C10N2O", ID = 198, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EQIGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H32C20N6O8", ID = 846, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ESP (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H26C16N4O2S", ID = 90, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ESP (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H26C16N4O2S", ID = 90, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ESP:2H(10) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H16H'10C16N4O2S", ID = 91, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ESP:2H(10) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H16H'10C16N4O2S", ID = 91, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanedithiol (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C2S2 - O", ID = 200, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanedithiol (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H4C2S2 - O", ID = 200, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanolamine (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H5C2N", ID = 734, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanolamine (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H5C2N", ID = 734, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanolamine (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H5C2N", ID = 734, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanolyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C2O", ID = 278, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanolyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H4C2O", ID = 278, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethoxyformyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H5C3O2", ID = 915, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 280, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 280, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 280, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 280, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 280, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EthylAmide (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H5C2", ID = 931, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EthylAmide (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H5C2", ID = 931, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ethylamino (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H5C2N - O", ID = 926, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ethylamino (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H5C2N - O", ID = 926, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FAD (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H31C27N9O15P2", ID = 50, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FAD (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H31C27N9O15P2", ID = 50, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FAD (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H31C27N9O15P2", ID = 50, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Farnesyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H24C15", ID = 44, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Fluorescein (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H14C22NO6", ID = 128, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Fluoro (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "F - H", ID = 127, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Fluoro (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "F - H", ID = 127, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Fluoro (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "F - H", ID = 127, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FMN (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H19C17N4O8P", ID = 442, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FMN (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H19C17N4O8P", ID = 442, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FMNC (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H21C17N4O9P", ID = 443, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FMNH (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H19C17N4O9P", ID = 409, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FMNH (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H19C17N4O9P", ID = 409, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FNEM (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H13C24NO7", ID = 515, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Formyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "CO", ID = 122, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Formyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "CO", ID = 122, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Formyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "CO", ID = 122, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FP-Biotin (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H49C27N4O5PS", ID = 325, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FP-Biotin (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H49C27N4O5PS", ID = 325, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FP-Biotin (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H49C27N4O5PS", ID = 325, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FP-Biotin (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H49C27N4O5PS", ID = 325, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FTC (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H15C21N3O5S", ID = 478, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FTC (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C21N3O5S", ID = 478, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FTC (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H15C21N3O5S", ID = 478, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FTC (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H15C21N3O5S", ID = 478, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FTC (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H15C21N3O5S", ID = 478, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "G-H1 (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C2O", ID = 860, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Galactosyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10C6O6", Losses = new [] { new FragmentLoss("H10C6O5"), }, ID = 907, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GeranylGeranyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H32C20", ID = 48, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H13C7NO", Losses = new [] { new FragmentLoss("H9C3N"), }, ID = 60, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H13C7NO", Losses = new [] { new FragmentLoss("H9C3N"), }, ID = 60, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10H'3C7NO", Losses = new [] { new FragmentLoss("H6H'3C3N"), }, ID = 61, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(3) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H10H'3C7NO", Losses = new [] { new FragmentLoss("H6H'3C3N"), }, ID = 61, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7H'6C7NO", Losses = new [] { new FragmentLoss("H3H'6C3N"), }, ID = 62, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(6) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H7H'6C7NO", Losses = new [] { new FragmentLoss("H3H'6C3N"), }, ID = 62, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(9) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4H'9C7NO", Losses = new [] { new FragmentLoss("H'9C3N"), }, ID = 63, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(9) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4H'9C7NO", Losses = new [] { new FragmentLoss("H'9C3N"), }, ID = 63, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glu (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H7C5NO3", ID = 450, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "glucosone (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H8C6O5", ID = 981, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glucosylgalactosyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C12O11", Losses = new [] { new FragmentLoss("H10C6O5"), new FragmentLoss("H20C12O10"), }, ID = 393, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glucuronyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H8C6O6", ID = 54, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GluGlu (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H14C10N2O6", ID = 451, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GluGluGlu (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H21C15N3O9", ID = 452, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GluGluGluGlu (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H28C20N4O12", ID = 453, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glutathione (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H15C10N3O6S", ID = 55, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Gly-loss+Amide (C-term G)", 
                 AAs = "G", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H2C2O2", ID = 822, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glycerophospho (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H7C3O5P", ID = 419, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GlycerylPE (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H12C5NO5P", ID = 396, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glycosyl (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H8C5O5", ID = 408, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GlyGly (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 121, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GlyGly (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 121, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GlyGly (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 121, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GlyGly (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 121, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Heme (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H32C34N4O4Fe", ID = 390, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Heme (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H32C34N4O4Fe", ID = 390, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hep (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C7O6", ID = 490, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hep (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H12C7O6", ID = 490, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hep (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H12C7O6", ID = 490, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hep (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H12C7O6", ID = 490, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hep (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H12C7O6", ID = 490, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hep (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H12C7O6", ID = 490, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", ID = 41, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", ID = 41, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", ID = 41, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", ID = 41, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", ID = 41, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", ID = 41, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", ID = 41, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", ID = 41, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)dHex(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H33C20NO14", ID = 146, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H40C25N2O18", ID = 149, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(1) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H40C25N2O18", ID = 149, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(1) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H40C25N2O18", ID = 149, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H57C36N3O26", ID = 160, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(2) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H57C36N3O26", ID = 160, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(2) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H57C36N3O26", ID = 160, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H36C22N2O15", ID = 148, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)dHex(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H46C28N2O19", ID = 152, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)dHex(1)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H54C33N2O23", ID = 155, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)dHex(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O23", ID = 156, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H44C27N2O19", ID = 151, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C12O10", ID = 512, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H20C12O10", ID = 512, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H46C28N2O20", ID = 153, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)dHex(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O24", ID = 158, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H54C33N2O24", ID = 157, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H30C18O15", ID = 144, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(1)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H51C31NO24", ID = 154, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O25", ID = 159, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(2)P(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O25P", ID = 161, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H82C50N4O35", ID = 309, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H92C56N4O40", ID = 310, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H76C46N2O35", ID = 137, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O45", ID = 311, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex1HexNAc1 (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H23C14NO10", ID = 793, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex1HexNAc1 (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H23C14NO10", ID = 793, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexN (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H11C6NO4", ID = 454, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexN (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H11C6NO4", ID = 454, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexN (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H11C6NO4", ID = 454, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexN (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H11C6NO4", ID = 454, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H13C8NO5", ID = 43, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H13C8NO5", ID = 43, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H13C8NO5", ID = 43, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(1)dHex(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H23C14NO9", ID = 142, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(1)dHex(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H33C20NO13", ID = 145, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H26C16N2O10", ID = 143, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(2)dHex(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H36C22N2O14", ID = 147, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(2)dHex(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H46C28N2O18", ID = 150, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HMVK (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C4O2", ID = 371, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE (CHK)", 
                 AAs = "C, H, K", LabelAtoms = LabelAtoms.None, Formula = "H16C9O2", ID = 53, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE+Delta:H(2) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H18C9O2", ID = 335, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE+Delta:H(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H18C9O2", ID = 335, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE+Delta:H(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H18C9O2", ID = 335, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE-BAHAH (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H45C25N5O4S", ID = 912, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE-BAHAH (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H45C25N5O4S", ID = 912, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE-BAHAH (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H45C25N5O4S", ID = 912, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE-Delta:H(2)O (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H14C9O", ID = 720, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE-Delta:H(2)O (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H14C9O", ID = 720, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE-Delta:H(2)O (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H14C9O", ID = 720, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HPG (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H4C8O2", ID = 186, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hydroxycinnamyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C9O2", ID = 407, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hydroxyfarnesyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H24C15O", ID = 376, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hydroxyheme (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H30C34N4O4Fe", ID = 436, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hydroxymethyl (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H2CO", ID = 414, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HydroxymethylOP (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C6O2", ID = 886, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hydroxytrimethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7C3O", ID = 445, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hypusine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C4NO", ID = 379, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IBTP (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H21C22P", ID = 119, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-D (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H34C20N4O5S", ID = 13, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-D:2H(8) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H26H'8C20N4O5S", ID = 12, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-G (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H38C22N4O6S", ID = 8, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-G:2H(8) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H30H'8C22N4O6S", ID = 9, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-H (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H20C15NO6Cl", ID = 123, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-H:13C(6) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H20C9C'6NO6Cl", ID = 124, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICPL (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H3C6NO", ID = 365, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:13C(6) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H3C'6NO", ID = 364, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:2H(4) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'4C6NO - H", ID = 687, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IDEnT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H7C9NOCl2", ID = 762, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IED-Biotin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H22C14N4O3S", ID = 294, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IGBP (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H13C12N2O2Br", ID = 243, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IGBP:13C(2) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H13C10C'2N2O2Br", ID = 499, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IMID (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3N2", ID = 94, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IMID:2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4C3N2", ID = 95, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iminobiotin (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C10N3OS", ID = 89, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iminobiotin (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H15C10N3OS", ID = 89, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iodo (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "I - H", ID = 129, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iodo (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "I - H", ID = 129, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IodoU-AMP (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "H11C9N2O9P", ID = 292, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IodoU-AMP (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H11C9N2O9P", ID = 292, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IodoU-AMP (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H11C9N2O9P", ID = 292, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ISD_z+2_ion (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-HN", ID = 991, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Isopropylphospho (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H7C3O3P", ID = 363, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Isopropylphospho (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H7C3O3P", ID = 363, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Isopropylphospho (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H7C3O3P", ID = 363, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex114 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C5C'2N2O'", ID = 532, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex114 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H12C5C'2N2O'", ID = 532, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex114 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C5C'2N2O'", ID = 532, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex115 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C6C'NN'O'", ID = 533, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex115 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H12C6C'NN'O'", ID = 533, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex115 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C6C'NN'O'", ID = 533, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex:13C(6)15N(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H24C8C'6N2N'2O3", ID = 731, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex:13C(6)15N(2) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H24C8C'6N2N'2O3", ID = 731, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex:13C(6)15N(2) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H24C8C'6N2N'2O3", ID = 731, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(1)2H(3)+Oxidation (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H'3C'O - H3C", ID = 885, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(4)15N(2)+GlyGly (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C'4N'2O2", ID = 923, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)+Acetyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C'6O - C4", ID = 835, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)+Dimethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C'6 - C4", ID = 986, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)+GlyGly (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C'6N2O2 - C2", ID = 799, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(2)+Acetyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C'6N'2O - C4N2", ID = 836, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(2)+Dimethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C'6N'2 - C4N2", ID = 987, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(2)+GlyGly (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C'6N'2O2 - C2", ID = 864, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(4)+Dimethyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H4C'6N'4 - C4N4", ID = 1005, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(4)+Dimethyl:2H(6)13C(2) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H'6C'8N'4 - H2C6N4", ID = 1007, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(4)+Methyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H2C'6N'4 - C5N4", ID = 1004, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(4)+Methyl:2H(3)13C(1) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H'3C'7N'4 - HC6N4", ID = 1006, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(9)+Phospho (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HC'9O3P - C9", ID = 185, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(4)+Acetyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4C2O - H2", ID = 834, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(4)+GlyGly (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2H'4C4N2O2", ID = 853, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "lapachenole (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H16C16O2", ID = 771, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Leu->MetOx (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.None, Formula = "OS - H2C", ID = 905, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LeuArgGlyGly (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H29C16N7O4", ID = 535, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LG-anhydrolactam (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H26C20O3", ID = 946, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LG-anhydrolactam (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H26C20O3", ID = 946, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LG-anhyropyrrole (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H26C20O2", ID = 948, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LG-anhyropyrrole (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H26C20O2", ID = 948, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LG-Hlactam-K (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H28C20O5", ID = 504, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LG-Hlactam-R (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H26C19O5 - N2", ID = 506, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LG-lactam-K (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H28C20O4", ID = 503, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LG-lactam-R (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H26C19O4 - N2", ID = 505, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LG-pyrrole (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H28C20O3", ID = 947, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LG-pyrrole (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H28C20O3", ID = 947, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lipoyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C8OS2", ID = 42, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lys->Allysine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "O - H3N", ID = 352, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lys->AminoadipicAcid (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "O2 - H3N", ID = 381, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lys->CamCys (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "OS - H4C", ID = 903, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lys->MetOx (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "OS - H3CN", ID = 906, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lys-loss (Protein C-term K)", 
                 AAs = "K", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H12C6N2O", ID = 313, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lysbiotinhydrazide (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C10N3O2S", ID = 353, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "maleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H3C4NO2", ID = 773, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "maleimide (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3C4NO2", ID = 773, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Maleimide-PEO2-Biotin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H35C23N5O7S", ID = 522, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "maleimide3 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H59C37N7O23", ID = 971, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "maleimide3 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H59C37N7O23", ID = 971, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "maleimide5 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H79C49N7O33", ID = 972, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "maleimide5 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H79C49N7O33", ID = 972, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Malonyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2C3O3", ID = 747, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Malonyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H2C3O3", ID = 747, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MDCC (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H21C20N3O5", ID = 887, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Menadione (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C11O2", ID = 302, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Menadione (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C11O2", ID = 302, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Menadione-HQ (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H8C11O2", ID = 767, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Menadione-HQ (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C11O2", ID = 767, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MercaptoEthanol (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C2S", ID = 928, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MercaptoEthanol (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H4C2S", ID = 928, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Met->Aha (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "N3 - H3CS", ID = 896, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Met->Hpg (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "C - H2S", ID = 899, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Met-loss (Protein N-term M)", 
                 AAs = "M", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H9C5NOS", ID = 765, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Met-loss+Acetyl (Protein N-term M)", 
                 AAs = "M", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H7C3NS", ID = 766, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (I)", 
                 AAs = "I", LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H2C", ID = 34, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl+Acetyl:2H(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "HH'3C3O", ID = 768, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl+Deamidated (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "HCO - N", ID = 528, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl+Deamidated (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "HCO - N", ID = 528, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl-PEO12-Maleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H58C32N2O15", ID = 891, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'2C", ID = 284, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3) (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H'3C - H", ID = 298, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3) (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H'3C - H", ID = 298, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3) (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H'3C - H", ID = 298, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3)13C(1) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H'3C' - H", ID = 329, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylamine (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H3CN - O", ID = 337, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylamine (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H3CN - O", ID = 337, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylmalonylation (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C4O3", ID = 914, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylphosphonate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H3CO2P", ID = 728, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylphosphonate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H3CO2P", ID = 728, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylphosphonate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H3CO2P", ID = 728, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylpyrroline (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7C6NO", ID = 435, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylthio (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H2CS", ID = 39, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylthio (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H2CS", ID = 39, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MG-H1 (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H2C3O", ID = 859, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Molybdopterin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H11C10N5O8PS2Mo", ID = 391, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MolybdopterinGD (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H47C40N20O26P4S4Mo", ID = 424, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MolybdopterinGD (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H47C40N20O26P4S4Mo", ID = 424, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MolybdopterinGD+Delta:S(-1)Se(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H47C40N20O26P4S3SeMo", ID = 415, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MTSL (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H14C9NOS", ID = 911, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Myristoleyl (Protein N-term G)", 
                 AAs = "G", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H24C14O", ID = 134, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Myristoyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H26C14O", ID = 45, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Myristoyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H26C14O", ID = 45, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Myristoyl (N-term G)", 
                 AAs = "G", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H26C14O", ID = 45, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Myristoyl+Delta:H(-4) (Protein N-term G)", 
                 AAs = "G", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H22C14O", ID = 135, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NA-LNO2 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H31C18NO4", ID = 685, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NA-LNO2 (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H31C18NO4", ID = 685, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NA-OA-NO2 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H33C18NO4", ID = 686, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NA-OA-NO2 (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H33C18NO4", ID = 686, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NBS (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H3C6NO2S", ID = 172, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NBS:13C(6) (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H3C'6NO2S", ID = 171, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NDA (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C13N", ID = 457, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NDA (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H5C13N", ID = 457, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEIAA (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H7C4NO", ID = 211, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEIAA (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H7C4NO", ID = 211, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEIAA:2H(5) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2H'5C4NO", ID = 212, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEIAA:2H(5) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H2H'5C4NO", ID = 212, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEM:2H(5) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2H'5C6NO2", ID = 776, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nethylmaleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H7C6NO2", ID = 108, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nethylmaleimide+water (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C6NO3", ID = 320, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nethylmaleimide+water (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C6NO3", ID = 320, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NHS-LC-Biotin (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H25C16N3O3S", ID = 92, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NHS-LC-Biotin (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H25C16N3O3S", ID = 92, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NIC (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H3C6NO", ID = 697, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nitro (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "NO2 - H", ID = 354, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nitro (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "NO2 - H", ID = 354, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nitrosyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "NO - H", ID = 275, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nmethylmaleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H5C5NO2", ID = 314, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nmethylmaleimide (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C5NO2", ID = 314, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nmethylmaleimide+water (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H7C5NO3", ID = 500, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NO_SMX_SEMD (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H10C10N3O3S", ID = 744, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NO_SMX_SIMD (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C10N3O4S", ID = 746, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NO_SMX_SMCT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H10C10N3O4S", ID = 745, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Diethylphosphate (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Diethylphosphate (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Diethylphosphate (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Diethylphosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Diethylphosphate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Diethylphosphate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Dimethylphosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H5C2O3P", ID = 723, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Dimethylphosphate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H5C2O3P", ID = 723, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Dimethylphosphate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H5C2O3P", ID = 723, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Ethylphosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H5C2O3P", ID = 726, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Ethylphosphate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H5C2O3P", ID = 726, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Ethylphosphate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H5C2O3P", ID = 726, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Isopropylmethylphosphonate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H9C4O2P", ID = 729, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Isopropylmethylphosphonate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H9C4O2P", ID = 729, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Isopropylmethylphosphonate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H9C4O2P", ID = 729, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Methylphosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H3CO3P", ID = 724, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Methylphosphate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H3CO3P", ID = 724, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-Methylphosphate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H3CO3P", ID = 724, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-pinacolylmethylphosphonate (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H15C7O2P", ID = 727, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-pinacolylmethylphosphonate (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C7O2P", ID = 727, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-pinacolylmethylphosphonate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H15C7O2P", ID = 727, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-pinacolylmethylphosphonate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H15C7O2P", ID = 727, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "O-pinacolylmethylphosphonate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H15C7O2P", ID = 727, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Octanoyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H14C8O", ID = 426, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Octanoyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H14C8O", ID = 426, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "OxArgBiotin (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H22C15N2O3S", ID = 116, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "OxArgBiotinRed (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H24C15N2O3S", ID = 117, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (C-term G)", 
                 AAs = "G", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "OxLysBiotin (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H24C16N4O3S", ID = 113, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "OxLysBiotinRed (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H26C16N4O3S", ID = 112, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "OxProBiotin (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H27C16N5O3S", ID = 115, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "OxProBiotinRed (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H29C16N5O3S", ID = 114, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoleyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H28C16O", ID = 431, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoleyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H28C16O", ID = 431, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoleyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H28C16O", ID = 431, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H30C16O", ID = 47, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H30C16O", ID = 47, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H30C16O", ID = 47, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H30C16O", ID = 47, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PEITC (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C9NS", ID = 979, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PEITC (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C9NS", ID = 979, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PEITC (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H9C9NS", ID = 979, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pentylamine (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H11C5N", ID = 801, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PentylamineBiotin (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H25C15N3O2S", ID = 800, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PEO-Iodoacetyl-LC-Biotin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H30C18N4O5S", ID = 20, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PET (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H7C7NS - O", ID = 264, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PET (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H7C7NS - O", ID = 264, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PGA1-biotin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H60C36N4O5S", ID = 539, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phe->CamCys (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "NOS - HC4", ID = 904, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phenylisocyanate (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H5C7NO", ID = 411, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phenylisocyanate:2H(5) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'5C7NO", ID = 412, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoadenosine (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O6P", ID = 405, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoadenosine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O6P", ID = 405, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoadenosine (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O6P", ID = 405, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoadenosine (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O6P", ID = 405, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoguanosine (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O7P", ID = 413, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoguanosine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O7P", ID = 413, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoHex (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H11C6O8P", ID = 429, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoHexNAc (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H14C8NO8P", ID = 428, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoHexNAc (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H14C8NO8P", ID = 428, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphopantetheine (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H21C11N2O6PS", ID = 49, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphopropargyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C3NO2P", ID = 959, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphopropargyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H4C3NO2P", ID = 959, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphopropargyl (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H4C3NO2P", ID = 959, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoribosyldephosphoCoA (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H42C26N7O19P3S", ID = 395, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoUridine (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H11C9N2O8P", ID = 417, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoUridine (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H11C9N2O8P", ID = 417, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phycocyanobilin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H38C33N4O6", ID = 387, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phycoerythrobilin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H40C33N4O6", ID = 388, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phytochromobilin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H36C33N4O6", ID = 389, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Piperidine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C5", ID = 520, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Piperidine (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H8C5", ID = 520, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pro->pyro-Glu (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 359, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pro->Pyrrolidinone (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "-H2CO", ID = 360, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pro->Pyrrolidone (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "-CO", ID = 369, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "probiotinhydrazide (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H18C10N4O2S", ID = 357, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propargylamine (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H3C3N - O", ID = 958, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propargylamine (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H3C3N - O", ID = 958, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propargylamine (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H3C3N - O", ID = 958, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionamide:2H(3) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2H'3C3NO", ID = 97, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3O", ID = 58, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C3O", ID = 58, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C3O", ID = 58, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionyl:13C(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C'3O", ID = 59, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionyl:13C(3) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C'3O", ID = 59, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PropylNAGthiazoline (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H14C9NO4S", ID = 514, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Puromycin (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H27C22N7O4", ID = 973, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PyMIC (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H6C7N2O", ID = 501, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PyridoxalPhosphate (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C8NO5P", ID = 46, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pyridylacetyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C7NO", ID = 25, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pyridylacetyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H5C7NO", ID = 25, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "pyrophospho (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H2O6P2", Losses = new [] { new FragmentLoss("H3O7P2"), }, ID = 898, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "pyrophospho (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H2O6P2", Losses = new [] { new FragmentLoss("H3O7P2"), }, ID = 898, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PyruvicAcidIminyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C3O2", ID = 422, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PyruvicAcidIminyl (Protein N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H2C3O2", ID = 422, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PyruvicAcidIminyl (Protein N-term V)", 
                 AAs = "V", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H2C3O2", ID = 422, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "QAT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H19C9N2O", ID = 195, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "QAT:2H(3) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H16H'3C9N2O", ID = 196, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "QEQTGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H36C23N8O11", ID = 876, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "QQQTGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H37C23N9O10", ID = 877, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Quinone (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O2 - H2", ID = 392, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Quinone (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "O2 - H2", ID = 392, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Retinylidene (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H26C20", ID = 380, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SecCarbamidomethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H3C2NOSe - S", ID = 1008, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ser->LacticAcid (Protein N-term S)", 
                 AAs = "S", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-HN", ID = 403, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SMA (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C6NO2", ID = 29, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SMA (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H9C6NO2", ID = 29, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SMCC-maleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H27C17N3O3", ID = 908, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SPITC (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C7NO3S2", ID = 261, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SPITC (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H5C7NO3S2", ID = 261, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SPITC:13C(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5CC'6NO3S2", ID = 464, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SPITC:13C(6) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H5CC'6NO3S2", ID = 464, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C4O3", ID = 64, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C4O3", ID = 64, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl:13C(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C'4O3", ID = 66, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl:13C(4) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C'4O3", ID = 66, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl:2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4C4O3", ID = 65, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl:2H(4) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'4C4O3", ID = 65, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H5C6NO2S", ID = 285, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H5C6NO2S", ID = 285, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H5C6NO2S", ID = 285, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid:13C(6) (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H5C'6NO2S", ID = 286, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid:13C(6) (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H5C'6NO2S", ID = 286, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid:13C(6) (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H5C'6NO2S", ID = 286, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Sulfide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "S", ID = 421, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Sulfide (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "S", ID = 421, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Sulfo (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "O3S", ID = 40, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "sulfo+amino (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HNO3S", ID = 997, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Sulfo-NHS-LC-LC-Biotin (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H36C22N4O4S", ID = 523, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Sulfo-NHS-LC-LC-Biotin (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H36C22N4O4S", ID = 523, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfoGMBS (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H26C22N4O5S", ID = 942, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SUMO2135 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H137C90N21O37S", ID = 960, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SUMO3549 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H224C150N38O60S", ID = 961, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiazolidine (N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C", ID = 1009, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thioacyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3OS", ID = 126, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thioacyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C3OS", ID = 126, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "thioacylPA (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C6NO2S", ID = 967, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiophos-S-S-biotin (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H34C19N4O5PS3", Losses = new [] { new FragmentLoss("H34C19N4O5PS3"), }, ID = 332, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiophos-S-S-biotin (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H34C19N4O5PS3", Losses = new [] { new FragmentLoss("H34C19N4O5PS3"), }, ID = 332, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiophos-S-S-biotin (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H34C19N4O5PS3", Losses = new [] { new FragmentLoss("H34C19N4O5PS3"), }, ID = 332, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiophospho (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "HO2PS", ID = 260, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiophospho (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "HO2PS", ID = 260, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiophospho (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HO2PS", ID = 260, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thrbiotinhydrazide (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H16C10N4OS", ID = 361, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thyroxine (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "C6OI4", ID = 398, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMAB (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H14C7NO", Losses = new [] { new FragmentLoss("H9C3N"), }, ID = 476, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMAB (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H14C7NO", Losses = new [] { new FragmentLoss("H9C3N"), }, ID = 476, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMAB:2H(9) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5H'9C7NO", Losses = new [] { new FragmentLoss("H'9C3N"), }, ID = 477, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMAB:2H(9) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H5H'9C7NO", Losses = new [] { new FragmentLoss("H'9C3N"), }, ID = 477, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMPP-Ac (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H33C29O10P", ID = 827, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TNBS (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "HC6N3O6", ID = 751, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TNBS (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "HC6N3O6", ID = 751, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "trifluoro (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.None, Formula = "F3 - H3", ID = 750, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Triiodo (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "I3 - H3", ID = 131, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Triiodothyronine (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HC6OI3", ID = 397, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trimethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C3", ID = 37, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trimethyl (Protein N-term A)", 
                 AAs = "A", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H6C3", ID = 37, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trimethyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H6C3", ID = 37, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trioxidation (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "O3", ID = 345, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Tripalmitate (Protein N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H96C51O5", ID = 51, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trp->Hydroxykynurenin (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O2 - C", ID = 350, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trp->Kynurenin (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O - C", ID = 351, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trp->Oxolactone (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 288, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Tyr->Dha (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "-H6C6O", ID = 400, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "VFQQQTGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H55C37N11O12", ID = 932, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "VIEVYQEQTGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H81C53N13O19", ID = 933, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:B10621 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H30C31N4O6SI", ID = 323, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DMP (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10C7N2", ID = 456, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DMP-s (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H14C8N2O", ID = 455, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:SSD (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C12NO5", ID = 273, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ZGB (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H53C37N6O6F2SB", ID = 861, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ZGB (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H53C37N6O6F2SB", ID = 861, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(1)2H(3) (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H'3C' - H3C", ID = 862, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(5) (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.C13, ID = 772, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(5)15N(1) (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 268, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(5)15N(1) (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 268, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(5)15N(1) (V)", 
                 AAs = "V", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 268, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6) (I)", 
                 AAs = "I", LabelAtoms = LabelAtoms.C13, ID = 188, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.C13, ID = 188, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6) (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.C13, ID = 188, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.C13, ID = 188, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(1) (I)", 
                 AAs = "I", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 695, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(1) (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 695, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 259, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(4) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 267, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(9) (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.C13, ID = 184, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(9) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.C13, ID = 184, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(9)15N(1) (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 269, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (A)", 
                 AAs = "A", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (G)", 
                 AAs = "G", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (I)", 
                 AAs = "I", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (V)", 
                 AAs = "V", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.N15, ID = 995, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.N15, ID = 995, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(2) (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.N15, ID = 995, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(2) (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.N15, ID = 995, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(2)2H(9) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'9N'2 - H9N2", ID = 944, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(3) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.N15, ID = 996, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(4) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.N15, ID = 897, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:18O(1) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "O' - O", ID = 258, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:18O(1) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "O' - O", ID = 258, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:18O(1) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "O' - O", ID = 258, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(3) (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 262, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(4) (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 481, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 481, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(4) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 481, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(9)13C(6)15N(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'9C'6N'2 - H9C6N2", ID = 696, 
                 Structural = false, Hidden = true, 
            },
            
            // Hardcoded Skyline Mods
            new UniModModificationData
            {
                 Name = "Label:15N", 
                 LabelAtoms = LabelAtoms.N15,  
                 Structural = false, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C", 
                 LabelAtoms = LabelAtoms.C13,  
                 Structural = false, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C15N", 
                 LabelAtoms = LabelAtoms.N15 | LabelAtoms.C13,  
                 Structural = false, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(2) (C-term K)", 
                 AAs = "K", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.N15 | LabelAtoms.C13,  
                 Structural = false, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(4) (C-term R)", 
                 AAs = "R", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.N15 | LabelAtoms.C13,  
                 Structural = false, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6) (C-term K)", 
                 AAs = "K", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.C13,  
                 Structural = false, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6) (C-term R)", 
                 AAs = "R", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.C13,  
                 Structural = false, Hidden = false, 
            }
        };
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
