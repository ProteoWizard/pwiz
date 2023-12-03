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
// ReSharper disable LocalizableElement
namespace pwiz.Skyline.Model.DocSettings
{
    public static class UniModData
    {
        public static readonly UniModModificationData DEFAULT = new UniModModificationData
        {
                 Name = "Carbamidomethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, ShortName = "CAM", Hidden = false, 
        };
        
        public static readonly UniModModificationData[] UNI_MOD_DATA =
        {
            new UniModModificationData
            {
                 Name = "6C-CysPAT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H16C8NO4P", ID = 2057, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "6C-CysPAT (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H16C8NO4P", ID = 2057, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C2H2O", ID = 1, 
                 Structural = true, ShortName = "1Ac", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C2H2O", ID = 1, 
                 Structural = true, ShortName = "1Ac", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Amidated (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "HN - O", ID = 2, 
                 Structural = true, ShortName = "Ami", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Ammonia-loss (N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H3N", ID = 385, 
                 Structural = true, ShortName = "dAm", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, ShortName = "CAM", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, ShortName = "CAM", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, ShortName = "CRM", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, ShortName = "CRM", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Carboxymethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C2H2O2", ID = 6, 
                 Structural = true, ShortName = "Cmc", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Na (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Na - H", ID = 30, 
                 Structural = true, ShortName = "NaX", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Na (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Na - H", ID = 30, 
                 Structural = true, ShortName = "NaX", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Deamidated (NQ)", 
                 AAs = "N, Q", LabelAtoms = LabelAtoms.None, Formula = "O - HN", ID = 7, 
                 Structural = true, ShortName = "Dea", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, ShortName = "Dhy", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Dehydro (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "-H", ID = 374, 
                 Structural = true, ShortName = "-1H", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Formyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "CO", ID = 122, 
                 Structural = true, ShortName = "Frm", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Gln->pyro-Glu (N-term Q)", 
                 AAs = "Q", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H3N", ID = 28, 
                 Structural = true, ShortName = "PGQ", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Glu->pyro-Glu (N-term E)", 
                 AAs = "E", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 27, 
                 Structural = true, ShortName = "PGE", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Guanidinyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2CN2", ID = 52, 
                 Structural = true, ShortName = "1Gu", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-C (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C10H17N3O3", ID = 105, 
                 Structural = true, ShortName = "C0I", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICPL (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C6H3NO", ID = 365, 
                 Structural = true, ShortName = "IP0", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:13C(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3C'6NO", ID = 364, 
                 Structural = true, ShortName = "IP6", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 214, 
                 Structural = true, ShortName = "IT4", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 214, 
                 Structural = true, ShortName = "IT4", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 214, 
                 Structural = true, ShortName = "IT4", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'7N'C7H24N3O3", ID = 730, 
                 Structural = true, ShortName = "IT8", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'7N'C7H24N3O3", ID = 730, 
                 Structural = true, ShortName = "IT8", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "C'7N'C7H24N3O3", ID = 730, 
                 Structural = true, ShortName = "IT8", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Met->Hse (C-term M)", 
                 AAs = "M", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "O - H2CS", ID = 10, 
                 Structural = true, ShortName = "Hse", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Met->Hsl (C-term M)", 
                 AAs = "M", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H4CS", ID = 11, 
                 Structural = true, ShortName = "Hsl", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Methylthio (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2CS", ID = 39, 
                 Structural = true, ShortName = "MSH", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C7H12N2O", ID = 888, 
                 Structural = true, ShortName = "M00", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C7H12N2O", ID = 888, 
                 Structural = true, ShortName = "M00", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "C7H12N2O", ID = 888, 
                 Structural = true, ShortName = "M00", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "NIPCAM (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C5NO", ID = 17, 
                 Structural = true, ShortName = "icM", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (HW)", 
                 AAs = "H, W", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "O", Losses = new [] { new FragmentLoss("H4COS"), }, ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "HO3P", Losses = new [] { new FragmentLoss("H3O4P"), }, ID = 21, 
                 Structural = true, ShortName = "Pho", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, ShortName = "Pho", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Propionamide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C3H5NO", ID = 24, 
                 Structural = true, ShortName = "PPa", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Pyridylethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H7C7N", ID = 31, 
                 Structural = true, ShortName = "Pye", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Pyro-carbamidomethyl (N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C2O", ID = 26, 
                 Structural = true, ShortName = "PyC", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Sulfo (STY)", 
                 AAs = "S, T, Y", LabelAtoms = LabelAtoms.None, Formula = "O3S", Losses = new [] { new FragmentLoss("O3S"), }, ID = 40, 
                 Structural = true, ShortName = "SuO", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMT2plex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C11C'N2O2", ID = 738, 
                 Structural = true, ShortName = "TM2", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMT2plex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H20C11C'N2O2", ID = 738, 
                 Structural = true, ShortName = "TM2", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMT6plex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C8C'4NN'O2", ID = 737, 
                 Structural = true, ShortName = "TM6", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMT6plex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H20C8C'4NN'O2", ID = 737, 
                 Structural = true, ShortName = "TM6", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMTpro (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H25C8C'7NN'2O3", ID = 2016, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "TMTpro (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H25C8C'7NN'2O3", ID = 2016, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-C:13C(9) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C'9 - C9", ID = 106, 
                 Structural = false, ShortName = "C9I", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:13C(6)2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'6H'4 - C6H4", ID = 866, 
                 Structural = false, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:13C(6)2H(4) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'6H'4 - C6H4", ID = 866, 
                 Structural = false, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 687, 
                 Structural = false, ShortName = "IP4", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Label:18O(1) (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "O' - O", ID = 258, 
                 Structural = false, ShortName = "Ob1", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Label:18O(2) (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "O'2 - O2", ID = 193, 
                 Structural = false, ShortName = "Ob2", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(3)15N(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'3N' - C3N", ID = 889, 
                 Structural = false, ShortName = "M04", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(3)15N(1) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'3N' - C3N", ID = 889, 
                 Structural = false, ShortName = "M04", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(3)15N(1) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "C'3N' - C3N", ID = 889, 
                 Structural = false, ShortName = "M04", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(6)15N(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'6N'2 - C6N2", ID = 1302, 
                 Structural = false, ShortName = "M08", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(6)15N(2) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'6N'2 - C6N2", ID = 1302, 
                 Structural = false, ShortName = "M08", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(6)15N(2) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "C'6N'2 - C6N2", ID = 1302, 
                 Structural = false, ShortName = "M08", Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "15N-oxobutanoic (N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H3N'", ID = 1419, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "15N-oxobutanoic (Protein N-term S)", 
                 AAs = "S", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H3N'", ID = 1419, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "15N-oxobutanoic (Protein N-term T)", 
                 AAs = "T", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H3N'", ID = 1419, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "2-dimethylsuccinyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H8C6O4", ID = 1262, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "2-monomethylsuccinyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C5O4", ID = 1253, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "2-nitrobenzyl (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H5C7NO2", ID = 1032, 
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
                 Structural = true, ShortName = "2HG", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "3-deoxyglucosone (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H8C6O4", ID = 949, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "3-hydroxybenzyl-phosphate (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7C7O4P", ID = 2041, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "3-hydroxybenzyl-phosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H7C7O4P", ID = 2041, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "3-hydroxybenzyl-phosphate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H7C7O4P", ID = 2041, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "3-hydroxybenzyl-phosphate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H7C7O4P", ID = 2041, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "3-phosphoglyceryl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C3O6P", ID = 1387, 
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
                 Name = "6C-CysPAT (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H16C8NO4P", ID = 2057, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "6C-CysPAT (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H16C8NO4P", ID = 2057, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "6C-CysPAT (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H16C8NO4P", ID = 2057, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "6C-CysPAT (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H16C8NO4P", ID = 2057, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "6C-CysPAT (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H16C8NO4P", ID = 2057, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "6C-CysPAT (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H16C8NO4P", ID = 2057, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "6C-CysPAT (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H16C8NO4P", ID = 2057, 
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
                 Structural = true, ShortName = "AQT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AccQTag (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H6C10N2O", ID = 194, 
                 Structural = true, ShortName = "AQT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C2H2O", ID = 1, 
                 Structural = true, ShortName = "1Ac", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "C2H2O", ID = 1, 
                 Structural = true, ShortName = "1Ac", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C2H2O", ID = 1, 
                 Structural = true, ShortName = "1Ac", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "C2H2O", ID = 1, 
                 Structural = true, ShortName = "1Ac", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "C2H2O", ID = 1, 
                 Structural = true, ShortName = "1Ac", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "C2H2O", ID = 1, 
                 Structural = true, ShortName = "1Ac", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyldeoxyhypusine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H11C6NO", ID = 1042, 
                 Structural = true, ShortName = "Adh", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetylhypusine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H11C6NO2", ID = 1043, 
                 Structural = true, ShortName = "Ahp", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", ID = 213, 
                 Structural = true, ShortName = "ADR", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", ID = 213, 
                 Structural = true, ShortName = "ADR", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", ID = 213, 
                 Structural = true, ShortName = "ADR", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", ID = 213, 
                 Structural = true, ShortName = "ADR", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", Losses = new [] { new FragmentLoss("H21C15N5O13P2"), }, ID = 213, 
                 Structural = true, ShortName = "ADR", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", ID = 213, 
                 Structural = true, ShortName = "ADR", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ADP-Ribosyl (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H21C15N5O13P2", Losses = new [] { new FragmentLoss("H21C15N5O13P2"), }, ID = 213, 
                 Structural = true, ShortName = "ADR", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEBS (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H9C8NO2S", ID = 276, 
                 Structural = true, ShortName = "AEB", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEBS (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C8NO2S", ID = 276, 
                 Structural = true, ShortName = "AEB", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEBS (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H9C8NO2S", ID = 276, 
                 Structural = true, ShortName = "AEB", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEBS (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H9C8NO2S", ID = 276, 
                 Structural = true, ShortName = "AEB", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEC-MAEC (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "C2H5NS - O", ID = 472, 
                 Structural = true, ShortName = "Aec", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEC-MAEC (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "C2H5NS - O", ID = 472, 
                 Structural = true, ShortName = "Aec", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AFB1_Dialdehyde (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10C17O6", ID = 1920, 
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
                 Name = "AHA-SS (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H9C7N5O2", ID = 1249, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AHA-SS_CAM (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H12C9N6O3", ID = 1250, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ahx2+Hsl (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H27C16N3O3", ID = 1015, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Amidine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3C2N", ID = 141, 
                 Structural = true, ShortName = "Ame", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Amidine (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H3C2N", ID = 141, 
                 Structural = true, ShortName = "Ame", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Amidino (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2CN2", ID = 440, 
                 Structural = true, ShortName = "Amd", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Amino (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HN", ID = 342, 
                 Structural = true, ShortName = "Amn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ammonia-loss (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "-H3N", ID = 385, 
                 Structural = true, ShortName = "dAm", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ammonia-loss (Protein N-term S)", 
                 AAs = "S", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H3N", ID = 385, 
                 Structural = true, ShortName = "dAm", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ammonia-loss (Protein N-term T)", 
                 AAs = "T", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-H3N", ID = 385, 
                 Structural = true, ShortName = "dAm", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ammonium (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H3N", ID = 989, 
                 Structural = true, ShortName = "Amm", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ammonium (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "H3N", ID = 989, 
                 Structural = true, ShortName = "Amm", Hidden = true, 
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
                 Name = "Andro-H2O (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H28C20O4", ID = 2025, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Archaeol (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H86C43O2", ID = 410, 
                 Structural = true, ShortName = "Ach", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Arg (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H12C6N4O", ID = 1288, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Arg->GluSA (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "O - H5CN3", ID = 344, 
                 Structural = true, ShortName = "AGA", Hidden = true, 
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
                 Structural = true, ShortName = "Orn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Arg-loss (C-term R)", 
                 AAs = "R", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H12C6N4O", ID = 1287, 
                 Structural = true, ShortName = "-1R", Hidden = true, 
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
                 Name = "Aspartylurea (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "O2 - H2CN2", ID = 1916, 
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
                 Name = "AzidoF (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "N3 - H", ID = 1845, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "azole (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "-H4O", ID = 1355, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "azole (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "-H4O", ID = 1355, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bacillosamine (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H16C10N2O4", Losses = new [] { new FragmentLoss("H16C10N2O4"), }, ID = 910, 
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
                 Name = "BEMAD_C (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C4H8O2S", ID = 736, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BEMAD_ST (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "C4H8OS2", ID = 735, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BEMAD_ST (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "C4H8OS2", ID = 735, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Benzoyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C7O", ID = 136, 
                 Structural = true, ShortName = "Boy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Benzoyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C7O", ID = 136, 
                 Structural = true, ShortName = "Boy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "benzylguanidine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C8N2", ID = 1349, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "betaFNA (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H30C25N2O6", ID = 1839, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "betaFNA (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H30C25N2O6", ID = 1839, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BHT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H22C15O", ID = 176, 
                 Structural = true, ShortName = "BHT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BHT (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H22C15O", ID = 176, 
                 Structural = true, ShortName = "BHT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BHT (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H22C15O", ID = 176, 
                 Structural = true, ShortName = "BHT", Hidden = true, 
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
                 Name = "Biotin (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C10H14N2O2S", ID = 3, 
                 Structural = true, ShortName = "Btn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C10H14N2O2S", ID = 3, 
                 Structural = true, ShortName = "Btn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-HPDP (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H32C19N4O3S2", ID = 290, 
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
                 Name = "Biotin-tyramide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H23C18N3O3S", ID = 1830, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-tyramide (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H23C18N3O3S", ID = 1830, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin-tyramide (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H23C18N3O3S", ID = 1830, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Aha-DADPS (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "C42H70N8O11SSi", ID = 2052, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Aha-PC (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "C29H38N8O10S", ID = 2053, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Cayman-10013 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C36H60N4O5S", ID = 539, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Cayman-10141 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C35H54N4O4S", ID = 538, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Invitrogen-M1602 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C23H33N5O7S", ID = 1012, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Sigma-B1267 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C20H27N5O5S", ID = 993, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-21325 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C34H45N7O7S", ID = 884, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-21328 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C15H23N3O3S3", ID = 1841, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-21328 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C15H23N3O3S3", ID = 1841, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-21330 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C21H35N3O7S", ID = 1423, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-21330 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C21H35N3O7S", ID = 1423, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-21345 (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "C15H25N3O2S", ID = 800, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-21360 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "C21H37N5O6S", ID = 811, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-21901+2H2O (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C23H39N5O9S", ID = 1320, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-21901+H2O (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C23H37N5O8S", ID = 1039, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-21911 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C41H71N5O16S", ID = 1340, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-33033 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C25H36N6O4S2", ID = 1251, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-33033-H (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C25H34N6O4S2", ID = 1252, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-88310 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C10H16N2O2", ID = 1031, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-88317 (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "C22H42N3O4P", ID = 1037, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Biotin:Thermo-88317 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "C22H42N3O4P", ID = 1037, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "biotinAcrolein298 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H22C13N4O2S", ID = 1314, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "biotinAcrolein298 (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H22C13N4O2S", ID = 1314, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "biotinAcrolein298 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H22C13N4O2S", ID = 1314, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BisANS (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H22C32N2O6S2", ID = 519, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "bisANS-sulfonates (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H22C32N2", ID = 1330, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "bisANS-sulfonates (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H22C32N2", ID = 1330, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "bisANS-sulfonates (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H22C32N2", ID = 1330, 
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
                 Name = "BMP-piperidinol (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H17C18NO", ID = 1281, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BMP-piperidinol (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H17C18NO", ID = 1281, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Brij35 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H24C12", ID = 1837, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Brij58 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H32C16", ID = 1838, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bromo (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "Br - H", ID = 340, 
                 Structural = true, ShortName = "1Br", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bromo (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "Br - H", ID = 340, 
                 Structural = true, ShortName = "1Br", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bromo (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "Br - H", ID = 340, 
                 Structural = true, ShortName = "1Br", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bromo (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "Br - H", ID = 340, 
                 Structural = true, ShortName = "1Br", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Bromobimane (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H10C10N2O2", ID = 301, 
                 Structural = true, ShortName = "Bbi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Butyryl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C4O", ID = 1289, 
                 Structural = true, ShortName = "Byr", Hidden = true, 
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
                 Structural = true, ShortName = "CAF", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CAMthiopropanoyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7C5NO2S", ID = 293, 
                 Structural = true, ShortName = "Ctp", Hidden = true, 
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
                 Structural = true, ShortName = "CAM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, ShortName = "CAM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, ShortName = "CAM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, ShortName = "CAM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", Losses = new [] { new FragmentLoss("H7C3NOS"), }, ID = 4, 
                 Structural = true, ShortName = "CAM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, ShortName = "CAM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, ShortName = "CAM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (U)", 
                 AAs = "U", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, ShortName = "CAM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamidomethyl (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 4, 
                 Structural = true, ShortName = "CAM", Hidden = true, 
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
                 Structural = true, ShortName = "CRM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, ShortName = "CRM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, ShortName = "CRM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, ShortName = "CRM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, ShortName = "CRM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbamyl (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HCNO", ID = 5, 
                 Structural = true, ShortName = "CRM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbofuran (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 977, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbonyl (A)", 
                 AAs = "A", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 1918, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbonyl (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 1918, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbonyl (I)", 
                 AAs = "I", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 1918, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbonyl (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 1918, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbonyl (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 1918, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbonyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 1918, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbonyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 1918, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carbonyl (V)", 
                 AAs = "V", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 1918, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "CO2", ID = 299, 
                 Structural = true, ShortName = "Cox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "CO2", ID = 299, 
                 Structural = true, ShortName = "Cox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "CO2", ID = 299, 
                 Structural = true, ShortName = "Cox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy (Protein N-term M)", 
                 AAs = "M", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "CO2", ID = 299, 
                 Structural = true, ShortName = "Cox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "CO2", ID = 299, 
                 Structural = true, ShortName = "Cox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxy->Thiocarboxy (Protein C-term G)", 
                 AAs = "G", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "S - O", ID = 420, 
                 Structural = true, ShortName = "Scx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxyethyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H4C3O2", ID = 378, 
                 Structural = true, ShortName = "CEt", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxyethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3O2", ID = 378, 
                 Structural = true, ShortName = "CEt", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxyethylpyrrole (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C7O2", ID = 1800, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxymethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C2H2O2", ID = 6, 
                 Structural = true, ShortName = "Cmc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxymethyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C2H2O2", ID = 6, 
                 Structural = true, ShortName = "Cmc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxymethyl (U)", 
                 AAs = "U", LabelAtoms = LabelAtoms.None, Formula = "C2H2O2", ID = 6, 
                 Structural = true, ShortName = "Cmc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxymethyl (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "C2H2O2", ID = 6, 
                 Structural = true, ShortName = "Cmc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CarboxymethylDMAP (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H10C9N2O", ID = 1350, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CarboxymethylDTT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H10C6O4S2", ID = 894, 
                 Structural = true, ShortName = "CmD", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ag (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Ag - H", ID = 955, 
                 Structural = true, ShortName = "1Ag", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ag (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Ag - H", ID = 955, 
                 Structural = true, ShortName = "1Ag", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Al[III] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Al - H3", ID = 1910, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Al[III] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Al - H3", ID = 1910, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ca[II] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Ca - H2", ID = 951, 
                 Structural = true, ShortName = "1Ca", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ca[II] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Ca - H2", ID = 951, 
                 Structural = true, ShortName = "1Ca", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Cu[I] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Cu - H", ID = 531, 
                 Structural = true, ShortName = "CuX", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Cu[I] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Cu - H", ID = 531, 
                 Structural = true, ShortName = "CuX", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Cu[I] (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "Cu - H", ID = 531, 
                 Structural = true, ShortName = "CuX", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Fe[II] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Fe - H2", ID = 952, 
                 Structural = true, ShortName = "1Fe", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Fe[II] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Fe - H2", ID = 952, 
                 Structural = true, ShortName = "1Fe", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Fe[III] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Fe - H3", ID = 1870, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Fe[III] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Fe - H3", ID = 1870, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:K (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "K - H", ID = 530, 
                 Structural = true, ShortName = "KXX", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:K (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "K - H", ID = 530, 
                 Structural = true, ShortName = "KXX", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Li (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Li - H", ID = 950, 
                 Structural = true, ShortName = "1Li", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Li (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Li - H", ID = 950, 
                 Structural = true, ShortName = "1Li", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Mg[II] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Mg - H2", ID = 956, 
                 Structural = true, ShortName = "1Mg", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Mg[II] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Mg - H2", ID = 956, 
                 Structural = true, ShortName = "1Mg", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ni[II] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Ni - H2", ID = 953, 
                 Structural = true, ShortName = "1Ni", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Ni[II] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Ni - H2", ID = 953, 
                 Structural = true, ShortName = "1Ni", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Zn[II] (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "Zn - H2", ID = 954, 
                 Structural = true, ShortName = "1Zn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Zn[II] (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "Zn - H2", ID = 954, 
                 Structural = true, ShortName = "1Zn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cation:Zn[II] (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "Zn - H2", ID = 954, 
                 Structural = true, ShortName = "1Zn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "cGMP (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H10C10N5O7P", ID = 849, 
                 Structural = true, ShortName = "cGP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "cGMP (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H10C10N5O7P", ID = 849, 
                 Structural = true, ShortName = "cGP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "cGMP+RMP-loss (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4C5N5O", ID = 851, 
                 Structural = true, ShortName = "GRL", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "cGMP+RMP-loss (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C5N5O", ID = 851, 
                 Structural = true, ShortName = "GRL", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CHDH (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H26C17O4", ID = 434, 
                 Structural = true, ShortName = "CHD", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Chlorination (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "Cl - H", ID = 936, 
                 Structural = true, ShortName = "1Cl", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Chlorination (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "Cl - H", ID = 936, 
                 Structural = true, ShortName = "1Cl", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CIGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H22C13N4O4S", ID = 1990, 
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
                 Structural = true, ShortName = "CzA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cresylphosphate (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H7C7O3P", ID = 1255, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cresylphosphate (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7C7O3P", ID = 1255, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cresylphosphate (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H7C7O3P", ID = 1255, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cresylphosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H7C7O3P", ID = 1255, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cresylphosphate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H7C7O3P", ID = 1255, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cresylphosphate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H7C7O3P", ID = 1255, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CresylSaligeninPhosphate (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H13C14O4P", ID = 1256, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CresylSaligeninPhosphate (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H13C14O4P", ID = 1256, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CresylSaligeninPhosphate (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H13C14O4P", ID = 1256, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CresylSaligeninPhosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H13C14O4P", ID = 1256, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CresylSaligeninPhosphate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H13C14O4P", ID = 1256, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CresylSaligeninPhosphate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H13C14O4P", ID = 1256, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Crotonaldehyde (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C4O", ID = 253, 
                 Structural = true, ShortName = "CrA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Crotonaldehyde (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H6C4O", ID = 253, 
                 Structural = true, ShortName = "CrA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Crotonaldehyde (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C4O", ID = 253, 
                 Structural = true, ShortName = "CrA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Crotonyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C4O", ID = 1363, 
                 Structural = true, ShortName = "Cro", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "CuSMo (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H24C19N8O15P2S3MoCu", ID = 444, 
                 Structural = true, ShortName = "CSM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cy3-maleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H45C37N4O9S2", ID = 1348, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cy3b-maleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H38C37N4O7S", ID = 821, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cyano (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "CN - H", ID = 438, 
                 Structural = true, ShortName = "1CN", Hidden = true, 
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
                 Name = "Cys->CamSec (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H3C2NOSe - S", ID = 1008, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cys->Dha (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "-H2S", ID = 368, 
                 Structural = true, ShortName = "DHA", Hidden = true, 
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
                 Structural = true, ShortName = "COa", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cys->PyruvicAcid (Protein N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "O - H3NS", ID = 382, 
                 Structural = true, ShortName = "CPA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cys->SecNEM (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C6H7NO2Se - S", ID = 1033, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cysteinyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H5C3NO2S", ID = 312, 
                 Structural = true, ShortName = "SCC", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "cysTMT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H25C14N3O2S", ID = 984, 
                 Structural = true, ShortName = "cTM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "cysTMT6plex (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H25C10C'4N2N'O2S", ID = 985, 
                 Structural = true, ShortName = "cT6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, ShortName = "Cpn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, ShortName = "Cpn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, ShortName = "Cpn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, ShortName = "Cpn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, ShortName = "Cpn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, ShortName = "Cpn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H22C19O7", ID = 270, 
                 Structural = true, ShortName = "Cpn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, ShortName = "Cpw", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, ShortName = "Cpw", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, ShortName = "Cpw", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, ShortName = "Cpw", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, ShortName = "Cpw", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, ShortName = "Cpw", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cytopiloyne+water (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H24C19O8", ID = 271, 
                 Structural = true, ShortName = "Cpw", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DABCYL-C2-maleimide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H21C21N5O3", Losses = new [] { new FragmentLoss("H13C15N3O"), }, ID = 2074, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DABCYL-C2-maleimide (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H21C21N5O3", Losses = new [] { new FragmentLoss("H13C15N3O"), }, ID = 2074, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DAET (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H9C4NS - O", ID = 178, 
                 Structural = true, ShortName = "DAT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DAET (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H9C4NS - O", ID = 178, 
                 Structural = true, ShortName = "DAT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dansyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H11C12NO2S", ID = 139, 
                 Structural = true, ShortName = "Dan", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dansyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H11C12NO2S", ID = 139, 
                 Structural = true, ShortName = "Dan", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dap-DSP (A)", 
                 AAs = "A", LabelAtoms = LabelAtoms.None, Formula = "H20C13N2O6S2", ID = 1399, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dap-DSP (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H20C13N2O6S2", ID = 1399, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dap-DSP (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C13N2O6S2", ID = 1399, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DBIA (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H24C14N4O3", ID = 2062, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DCP (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H12C9O3", ID = 2080, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deamidated (Protein N-term F)", 
                 AAs = "F", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "O - HN", ID = 7, 
                 Structural = true, ShortName = "Dea", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deamidated (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "O - HN", Losses = new [] { new FragmentLoss("HCNO"), }, ID = 7, 
                 Structural = true, ShortName = "Dea", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deamidated:18O(1) (NQ)", 
                 AAs = "N, Q", LabelAtoms = LabelAtoms.None, Formula = "O' - HN", ID = 366, 
                 Structural = true, ShortName = "DeO", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Decanoyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H18C10O", ID = 449, 
                 Structural = true, ShortName = "Dec", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Decanoyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H18C10O", ID = 449, 
                 Structural = true, ShortName = "Dec", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Decarboxylation (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "-H2CO", ID = 1915, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Decarboxylation (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "-H2CO", ID = 1915, 
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
                 Structural = true, ShortName = "Dhy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (Protein C-term N)", 
                 AAs = "N", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, ShortName = "Dhy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (Protein C-term Q)", 
                 AAs = "Q", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, ShortName = "Dhy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, ShortName = "Dhy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, ShortName = "Dhy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dehydrated (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "-H2O", ID = 23, 
                 Structural = true, ShortName = "Dhy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(-1)N(-1)18O(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "O' - HN", ID = 170, 
                 Structural = true, ShortName = "DeW", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(-4)O(2) (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O2 - H4", ID = 1923, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(-4)O(3) (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O3 - H4", ID = 1924, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(10)C(8)O(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10C8O", ID = 1928, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H2C2", ID = 254, 
                 Structural = true, ShortName = "AAS", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C2", ID = 254, 
                 Structural = true, ShortName = "AAS", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(2) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H2C2", ID = 254, 
                 Structural = true, ShortName = "AAS", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C3", ID = 207, 
                 Structural = true, ShortName = "AAT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(3)O(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C3O", ID = 319, 
                 Structural = true, ShortName = "AAU", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(3)O(1) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H2C3O", ID = 319, 
                 Structural = true, ShortName = "AAU", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(2)C(5) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C5", ID = 318, 
                 Structural = true, ShortName = "AAV", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 255, 
                 Structural = true, ShortName = "AAR", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 255, 
                 Structural = true, ShortName = "AAR", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(2) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 255, 
                 Structural = true, ShortName = "AAR", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(2)O(-1)S(1) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C2S - O", ID = 327, 
                 Structural = true, ShortName = "AAW", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H4C3", ID = 256, 
                 Structural = true, ShortName = "PrA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3", ID = 256, 
                 Structural = true, ShortName = "PrA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3)O(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4C3O", ID = 206, 
                 Structural = true, ShortName = "Aco", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3)O(1) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H4C3O", ID = 206, 
                 Structural = true, ShortName = "Aco", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3)O(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3O", ID = 206, 
                 Structural = true, ShortName = "Aco", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3)O(1) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H4C3O", ID = 206, 
                 Structural = true, ShortName = "Aco", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(3)O(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3O2", ID = 1926, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(5)O(1) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H4C5O", ID = 1927, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(4)C(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C6", ID = 208, 
                 Structural = true, ShortName = "Acr", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(5)C(2) (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H5C2", ID = 529, 
                 Structural = true, ShortName = "2M+", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(6)C(3)O(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C3O", ID = 1312, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(6)C(3)O(1) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H6C3O", ID = 1312, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(6)C(3)O(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C3O", ID = 1312, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(6)C(6)O(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C6O", ID = 205, 
                 Structural = true, ShortName = "Aci", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(6)C(7)O(4) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H6C7O4", ID = 1929, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(8)C(6)O(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C6O", ID = 1313, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:H(8)C(6)O(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C6O2", ID = 209, 
                 Structural = true, ShortName = "Acp", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:Hg(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "Hg", ID = 291, 
                 Structural = true, ShortName = "1Hg", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:O(4) (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O4", ID = 1925, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:S(-1)Se(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "Se - S", ID = 162, 
                 Structural = true, ShortName = "SSe", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:S(-1)Se(1) (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "Se - S", ID = 162, 
                 Structural = true, ShortName = "SSe", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Delta:Se(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "Se", ID = 423, 
                 Structural = true, ShortName = "1Se", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deoxy (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "-O", ID = 447, 
                 Structural = true, ShortName = "dOx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deoxy (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "-O", ID = 447, 
                 Structural = true, ShortName = "dOx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deoxy (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "-O", ID = 447, 
                 Structural = true, ShortName = "dOx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deoxyhypusine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C4N", ID = 1041, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Deoxyhypusine (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H9C4N", ID = 1041, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DeStreak (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4C2OS", ID = 303, 
                 Structural = true, ShortName = "DSk", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dethiomethyl (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "-H4CS", ID = 526, 
                 Structural = true, ShortName = "DTM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H10C6O4", Losses = new [] { new FragmentLoss("H10C6O4"), }, ID = 295, 
                 Structural = true, ShortName = "dHx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H10C6O4", Losses = new [] { new FragmentLoss("H10C6O4"), }, ID = 295, 
                 Structural = true, ShortName = "dHx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H20C12O9", Losses = new [] { new FragmentLoss("H20C12O9"), }, ID = 1367, 
                 Structural = true, ShortName = "1HF", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexA(1)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H67C42N3O30", Losses = new [] { new FragmentLoss("H67C42N3O30"), }, ID = 1641, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(1)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H47C29NO22", Losses = new [] { new FragmentLoss("H47C29NO22"), }, ID = 1581, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(1)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H50C31N2O22", Losses = new [] { new FragmentLoss("H50C31N2O22"), }, ID = 1588, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(1)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H50C31N2O23", Losses = new [] { new FragmentLoss("H50C31N2O23"), }, ID = 1593, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(2)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H60C37N2O27", Losses = new [] { new FragmentLoss("H60C37N2O27"), }, ID = 1618, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(2)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H63C39N3O27", Losses = new [] { new FragmentLoss("H63C39N3O27"), }, ID = 1624, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(2)NeuAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H63C39N3O30S", Losses = new [] { new FragmentLoss("H63C39N3O30S"), }, ID = 1639, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(2)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H80C50N4O35", Losses = new [] { new FragmentLoss("H80C50N4O35"), }, ID = 1675, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H46C28N2O22S", Losses = new [] { new FragmentLoss("H46C28N2O22S"), }, ID = 1587, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H59C36N3O24", Losses = new [] { new FragmentLoss("H59C36N3O24"), }, ID = 1608, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(3)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H76C47N4O32", Losses = new [] { new FragmentLoss("H76C47N4O32"), }, ID = 1658, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(3)NeuAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H76C47N4O35S", Losses = new [] { new FragmentLoss("H76C47N4O35S"), }, ID = 1673, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(3)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H76C47N4O33", Losses = new [] { new FragmentLoss("H76C47N4O33"), }, ID = 1661, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H59C36N3O27S", Losses = new [] { new FragmentLoss("H59C36N3O27S"), }, ID = 1622, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(1)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H72C44N4O29", Losses = new [] { new FragmentLoss("H72C44N4O29"), }, ID = 1645, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H30C18O14", Losses = new [] { new FragmentLoss("H30C18O14"), }, ID = 1375, 
                 Structural = true, ShortName = "2HF", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexA(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H38C24O20", Losses = new [] { new FragmentLoss("H38C24O20"), }, ID = 1446, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexA(1)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H51C32NO25", Losses = new [] { new FragmentLoss("H51C32NO25"), }, ID = 1597, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexA(1)HexNAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H51C32NO28S", Losses = new [] { new FragmentLoss("H51C32NO28S"), }, ID = 1609, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexA(1)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H64C40N2O30", Losses = new [] { new FragmentLoss("H64C40N2O30"), }, ID = 1634, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H43C26NO19", Losses = new [] { new FragmentLoss("H43C26NO19"), }, ID = 1564, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(1)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H77C48N3O35", Losses = new [] { new FragmentLoss("H77C48N3O35"), }, ID = 1949, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H43C26NO22S", Losses = new [] { new FragmentLoss("H43C26NO22S"), }, ID = 1579, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(2)NeuAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H73C45N3O35S", Losses = new [] { new FragmentLoss("H73C45N3O35S"), }, ID = 1666, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(2)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H90C56N4O40", Losses = new [] { new FragmentLoss("H90C56N4O40"), }, ID = 1708, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(2)NeuAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H90C56N4O43S", Losses = new [] { new FragmentLoss("H90C56N4O43S"), }, ID = 1724, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(2)NeuGc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H73C45N3O33", Losses = new [] { new FragmentLoss("H73C45N3O33"), }, ID = 1657, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(2)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H73C45N3O33", Losses = new [] { new FragmentLoss("H73C45N3O33"), }, ID = 1657, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(2)NeuGc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H90C56N4O42", Losses = new [] { new FragmentLoss("H90C56N4O42"), }, ID = 1714, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(2)NeuGc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H107C67N5O51", Losses = new [] { new FragmentLoss("H107C67N5O51"), }, ID = 1747, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H64C39N2O28", Losses = new [] { new FragmentLoss("H64C39N2O28"), }, ID = 1449, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O27S", Losses = new [] { new FragmentLoss("H56C34N2O27S"), }, ID = 1615, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H69C42N3O29", Losses = new [] { new FragmentLoss("H69C42N3O29"), }, ID = 1762, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H69C42N3O29", Losses = new [] { new FragmentLoss("H69C42N3O29"), }, ID = 1762, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(3)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H86C53N4O37", Losses = new [] { new FragmentLoss("H86C53N4O37"), }, ID = 1688, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(3)NeuAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H86C53N4O40S", Losses = new [] { new FragmentLoss("H86C53N4O40S"), }, ID = 1707, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H69C42N3O32S", Losses = new [] { new FragmentLoss("H69C42N3O32S"), }, ID = 1651, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H82C50N4O34", Losses = new [] { new FragmentLoss("H82C50N4O34"), }, ID = 1671, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H82C50N4O34", Losses = new [] { new FragmentLoss("H82C50N4O34"), }, ID = 1671, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(4)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H99C61N5O42", Losses = new [] { new FragmentLoss("H99C61N5O42"), }, ID = 1727, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(4)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H116C72N6O50", Losses = new [] { new FragmentLoss("H116C72N6O50"), }, ID = 1754, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(2)HexNAc(4)Sulf(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H82C50N4O40S2", Losses = new [] { new FragmentLoss("H82C50N4O40S2"), }, ID = 1952, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H40C24O19", Losses = new [] { new FragmentLoss("H40C24O19"), }, ID = 1376, 
                 Structural = true, ShortName = "3HF", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexA(1)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H61C38NO30", Losses = new [] { new FragmentLoss("H61C38NO30"), }, ID = 1625, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexA(1)HexNAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H61C38NO33S", Losses = new [] { new FragmentLoss("H61C38NO33S"), }, ID = 1640, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexA(1)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H74C46N2O35", Losses = new [] { new FragmentLoss("H74C46N2O35"), }, ID = 1660, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexA(1)HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H74C46N2O38S", Losses = new [] { new FragmentLoss("H74C46N2O38S"), }, ID = 1674, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexA(1)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H87C54N3O43S", Losses = new [] { new FragmentLoss("H87C54N3O43S"), }, ID = 1716, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexA(2)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H82C52N2O41", Losses = new [] { new FragmentLoss("H82C52N2O41"), }, ID = 1693, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H53C32NO24", Losses = new [] { new FragmentLoss("H53C32NO24"), }, ID = 1596, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H53C32NO27S", Losses = new [] { new FragmentLoss("H53C32NO27S"), }, ID = 1607, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H66C40N2O29", Losses = new [] { new FragmentLoss("H66C40N2O29"), }, ID = 1761, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H66C40N2O29", Losses = new [] { new FragmentLoss("H66C40N2O29"), }, ID = 1761, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(2)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H83C51N3O38", Losses = new [] { new FragmentLoss("H83C51N3O38"), }, ID = 1686, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H74C45N2O33", Losses = new [] { new FragmentLoss("H74C45N2O33"), }, ID = 1454, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(2)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H66C40N2O32S", Losses = new [] { new FragmentLoss("H66C40N2O32S"), }, ID = 1764, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H66C40N2O32S", Losses = new [] { new FragmentLoss("H66C40N2O32S"), }, ID = 1764, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H79C48N3O34", Losses = new [] { new FragmentLoss("H79C48N3O34"), }, ID = 1768, 
                 Structural = true, ShortName = "G0m", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H79C48N3O34", Losses = new [] { new FragmentLoss("H79C48N3O34"), }, ID = 1768, 
                 Structural = true, ShortName = "G0m", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(3)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H113C70N5O50", Losses = new [] { new FragmentLoss("H113C70N5O50"), }, ID = 1750, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(3)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H87C53N3O38", Losses = new [] { new FragmentLoss("H87C53N3O38"), }, ID = 1463, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(3)Pent(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H95C58N3O42", Losses = new [] { new FragmentLoss("H95C58N3O42"), }, ID = 1474, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(3)Pent(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H103C63N3O46", Losses = new [] { new FragmentLoss("H103C63N3O46"), }, ID = 1492, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H79C48N3O37S", Losses = new [] { new FragmentLoss("H79C48N3O37S"), }, ID = 1683, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H92C56N4O39", Losses = new [] { new FragmentLoss("H92C56N4O39"), }, ID = 305, 
                 Structural = true, ShortName = "G0F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H92C56N4O39", Losses = new [] { new FragmentLoss("H92C56N4O39"), }, ID = 305, 
                 Structural = true, ShortName = "G0F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(4)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H109C67N5O47", Losses = new [] { new FragmentLoss("H109C67N5O47"), }, ID = 1510, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(4)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H100C61N4O43", Losses = new [] { new FragmentLoss("H100C61N4O43"), }, ID = 1485, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(4)Pent(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H108C66N4O47", Losses = new [] { new FragmentLoss("H108C66N4O47"), }, ID = 1505, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(4)Pent(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H116C71N4O51", Losses = new [] { new FragmentLoss("H116C71N4O51"), }, ID = 1525, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(4)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H92C56N4O42S", Losses = new [] { new FragmentLoss("H92C56N4O42S"), }, ID = 1476, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(5) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H105C64N5O44", Losses = new [] { new FragmentLoss("H105C64N5O44"), }, ID = 1775, 
                 Structural = true, ShortName = "G0X", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H105C64N5O44", Losses = new [] { new FragmentLoss("H105C64N5O44"), }, ID = 1775, 
                 Structural = true, ShortName = "G0X", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(5)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H122C75N6O52", Losses = new [] { new FragmentLoss("H122C75N6O52"), }, ID = 1784, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(5)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H122C75N6O52", Losses = new [] { new FragmentLoss("H122C75N6O52"), }, ID = 1784, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(5)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H105C64N5O47S", Losses = new [] { new FragmentLoss("H105C64N5O47S"), }, ID = 1508, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(6) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H118C72N6O49", Losses = new [] { new FragmentLoss("H118C72N6O49"), }, ID = 1781, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(6) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H118C72N6O49", Losses = new [] { new FragmentLoss("H118C72N6O49"), }, ID = 1781, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(3)HexNAc(6)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H118C72N6O52S", Losses = new [] { new FragmentLoss("H118C72N6O52S"), }, ID = 1546, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H50C30O24", Losses = new [] { new FragmentLoss("H50C30O24"), }, ID = 1377, 
                 Structural = true, ShortName = "4HF", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexA(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H58C36O30", Losses = new [] { new FragmentLoss("H58C36O30"), }, ID = 1943, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexA(1)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H84C52N2O40", Losses = new [] { new FragmentLoss("H84C52N2O40"), }, ID = 1691, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexA(1)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H97C60N3O48S", Losses = new [] { new FragmentLoss("H97C60N3O48S"), }, ID = 1737, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(1)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H71C43NO33", Losses = new [] { new FragmentLoss("H71C43NO33"), }, ID = 1453, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H76C46N2O34", Losses = new [] { new FragmentLoss("H76C46N2O34"), }, ID = 1766, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H76C46N2O34", Losses = new [] { new FragmentLoss("H76C46N2O34"), }, ID = 1766, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H84C51N2O38", Losses = new [] { new FragmentLoss("H84C51N2O38"), }, ID = 1459, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H89C54N3O39", Losses = new [] { new FragmentLoss("H89C54N3O39"), }, ID = 1467, 
                 Structural = true, ShortName = "G1m", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(3)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H106C65N4O47", Losses = new [] { new FragmentLoss("H106C65N4O47"), }, ID = 1501, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(3)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H106C65N4O47", Losses = new [] { new FragmentLoss("H106C65N4O47"), }, ID = 1501, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(3)NeuAc(1)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H106C65N4O50S", Losses = new [] { new FragmentLoss("H106C65N4O50S"), }, ID = 1515, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(3)NeuGc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H106C65N4O48", Losses = new [] { new FragmentLoss("H106C65N4O48"), }, ID = 1506, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(3)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H97C59N3O43", Losses = new [] { new FragmentLoss("H97C59N3O43"), }, ID = 1478, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(3)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H89C54N3O42S", Losses = new [] { new FragmentLoss("H89C54N3O42S"), }, ID = 1471, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O44", Losses = new [] { new FragmentLoss("H102C62N4O44"), }, ID = 307, 
                 Structural = true, ShortName = "G1F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O44", Losses = new [] { new FragmentLoss("H102C62N4O44"), }, ID = 307, 
                 Structural = true, ShortName = "G1F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(4)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H119C73N5O52", Losses = new [] { new FragmentLoss("H119C73N5O52"), }, ID = 1782, 
                 Structural = true, ShortName = "G1Y", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(4)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H119C73N5O52", Losses = new [] { new FragmentLoss("H119C73N5O52"), }, ID = 1782, 
                 Structural = true, ShortName = "G1Y", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(4)NeuAc(1)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H119C73N5O55S", Losses = new [] { new FragmentLoss("H119C73N5O55S"), }, ID = 1556, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(4)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H110C67N4O48", Losses = new [] { new FragmentLoss("H110C67N4O48"), }, ID = 1512, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(4)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O47S", Losses = new [] { new FragmentLoss("H102C62N4O47S"), }, ID = 1499, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(5) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H115C70N5O49", Losses = new [] { new FragmentLoss("H115C70N5O49"), }, ID = 1519, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(4)HexNAc(5)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H115C70N5O52S", Losses = new [] { new FragmentLoss("H115C70N5O52S"), }, ID = 1536, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H60C36O29", Losses = new [] { new FragmentLoss("H60C36O29"), }, ID = 1378, 
                 Structural = true, ShortName = "5HF", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexA(1)HexNAc(3)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H107C66N3O53S", Losses = new [] { new FragmentLoss("H107C66N3O53S"), }, ID = 1520, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexA(1)HexNAc(3)Sulf(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H107C66N3O56S2", Losses = new [] { new FragmentLoss("H107C66N3O56S2"), }, ID = 1539, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H86C52N2O39", Losses = new [] { new FragmentLoss("H86C52N2O39"), }, ID = 1462, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H94C57N2O43", Losses = new [] { new FragmentLoss("H94C57N2O43"), }, ID = 1472, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H99C60N3O44", Losses = new [] { new FragmentLoss("H99C60N3O44"), }, ID = 1484, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(3)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H116C71N4O52", Losses = new [] { new FragmentLoss("H116C71N4O52"), }, ID = 1529, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(3)NeuAc(1)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H116C71N4O55S", Losses = new [] { new FragmentLoss("H116C71N4O55S"), }, ID = 1548, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(3)NeuGc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H116C71N4O53", Losses = new [] { new FragmentLoss("H116C71N4O53"), }, ID = 1534, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(3)NeuGc(1)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H116C71N4O56S", Losses = new [] { new FragmentLoss("H116C71N4O56S"), }, ID = 1550, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(3)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H99C60N3O47S", Losses = new [] { new FragmentLoss("H99C60N3O47S"), }, ID = 1493, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H112C68N4O49", Losses = new [] { new FragmentLoss("H112C68N4O49"), }, ID = 308, 
                 Structural = true, ShortName = "G2F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(4)Me(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H124C75N4O53", Losses = new [] { new FragmentLoss("H124C75N4O53"), }, ID = 1544, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(4)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H129C79N5O57", Losses = new [] { new FragmentLoss("H129C79N5O57"), }, ID = 1410, 
                 Structural = true, ShortName = "G2Y", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(4)NeuAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H146C90N6O65", Losses = new [] { new FragmentLoss("H146C90N6O65"), }, ID = 1411, 
                 Structural = true, ShortName = "G2Z", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(4)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H120C73N4O53", Losses = new [] { new FragmentLoss("H120C73N4O53"), }, ID = 1538, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(4)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H112C68N4O52S", Losses = new [] { new FragmentLoss("H112C68N4O52S"), }, ID = 1527, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(4)Sulf(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H112C68N4O55S2", Losses = new [] { new FragmentLoss("H112C68N4O55S2"), }, ID = 1543, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(5)HexNAc(5) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H125C76N5O54", Losses = new [] { new FragmentLoss("H125C76N5O54"), }, ID = 1555, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(6) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H70C42O34", Losses = new [] { new FragmentLoss("H70C42O34"), }, ID = 1379, 
                 Structural = true, ShortName = "6HF", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(6)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H96C58N2O44", Losses = new [] { new FragmentLoss("H96C58N2O44"), }, ID = 1477, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(6)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H109C66N3O49", Losses = new [] { new FragmentLoss("H109C66N3O49"), }, ID = 1509, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(6)HexNAc(3)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H109C66N3O52S", Losses = new [] { new FragmentLoss("H109C66N3O52S"), }, ID = 1518, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(6)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H122C74N4O54", Losses = new [] { new FragmentLoss("H122C74N4O54"), }, ID = 1547, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(7)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H106C64N2O49", Losses = new [] { new FragmentLoss("H106C64N2O49"), }, ID = 1500, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(7)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H119C72N3O54", Losses = new [] { new FragmentLoss("H119C72N3O54"), }, ID = 1537, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(7)HexNAc(3)Phos(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H120C72N3O57P", Losses = new [] { new FragmentLoss("H120C72N3O57P"), }, ID = 1554, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(7)HexNAc(3)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H119C72N3O57S", Losses = new [] { new FragmentLoss("H119C72N3O57S"), }, ID = 1553, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(7)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H132C80N4O59", ID = 1840, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)Hex(8)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H116C70N2O54", Losses = new [] { new FragmentLoss("H116C70N2O54"), }, ID = 1963, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H49C30N3O19", Losses = new [] { new FragmentLoss("H49C30N3O19"), }, ID = 1580, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H62C38N4O24", Losses = new [] { new FragmentLoss("H62C38N4O24"), }, ID = 1616, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(1)HexNAc(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H75C46N5O29", Losses = new [] { new FragmentLoss("H75C46N5O29"), }, ID = 1652, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(1)HexNAc(1)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H57C35NO26", Losses = new [] { new FragmentLoss("H57C35NO26"), }, ID = 1606, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(1)HexNAc(2)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H70C43N2O31", Losses = new [] { new FragmentLoss("H70C43N2O31"), }, ID = 1644, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(1)HexNAc(2)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H73C45N3O31", Losses = new [] { new FragmentLoss("H73C45N3O31"), }, ID = 1650, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(1)HexNAc(2)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H73C45N3O32", Losses = new [] { new FragmentLoss("H73C45N3O32"), }, ID = 1653, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(1)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H69C42N3O28", Losses = new [] { new FragmentLoss("H69C42N3O28"), }, ID = 1637, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(1)HexNAc(4)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H82C50N4O36S", Losses = new [] { new FragmentLoss("H82C50N4O36S"), }, ID = 1951, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H40C24O18", Losses = new [] { new FragmentLoss("H40C24O18"), }, ID = 1445, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexA(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H48C30O24", Losses = new [] { new FragmentLoss("H48C30O24"), }, ID = 1586, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexA(1)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H61C38NO29", Losses = new [] { new FragmentLoss("H61C38NO29"), }, ID = 1621, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexA(1)HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H74C46N2O37S", Losses = new [] { new FragmentLoss("H74C46N2O37S"), }, ID = 1670, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H53C32NO23", Losses = new [] { new FragmentLoss("H53C32NO23"), }, ID = 1594, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H66C40N2O28", Losses = new [] { new FragmentLoss("H66C40N2O28"), }, ID = 1760, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H66C40N2O28", Losses = new [] { new FragmentLoss("H66C40N2O28"), }, ID = 1760, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(2)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H80C49N2O36", Losses = new [] { new FragmentLoss("H80C49N2O36"), }, ID = 1669, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(2)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H83C51N3O36", Losses = new [] { new FragmentLoss("H83C51N3O36"), }, ID = 1682, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(2)NeuAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H83C51N3O39S", Losses = new [] { new FragmentLoss("H83C51N3O39S"), }, ID = 1695, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(2)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H83C51N3O37", Losses = new [] { new FragmentLoss("H83C51N3O37"), }, ID = 1684, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H66C40N2O31S", Losses = new [] { new FragmentLoss("H66C40N2O31S"), }, ID = 1643, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(2)Sulf(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H66C40N2O34S2", Losses = new [] { new FragmentLoss("H66C40N2O34S2"), }, ID = 1656, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H79C48N3O33", Losses = new [] { new FragmentLoss("H79C48N3O33"), }, ID = 1767, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H79C48N3O33", Losses = new [] { new FragmentLoss("H79C48N3O33"), }, ID = 1767, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(3)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H96C59N4O41", Losses = new [] { new FragmentLoss("H96C59N4O41"), }, ID = 1718, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(3)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H96C59N4O42", Losses = new [] { new FragmentLoss("H96C59N4O42"), }, ID = 1721, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H79C48N3O36S", Losses = new [] { new FragmentLoss("H79C48N3O36S"), }, ID = 1679, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H92C56N4O38", Losses = new [] { new FragmentLoss("H92C56N4O38"), }, ID = 1701, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(4)Sulf(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H92C56N4O44S2", Losses = new [] { new FragmentLoss("H92C56N4O44S2"), }, ID = 1956, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H105C64N5O43", Losses = new [] { new FragmentLoss("H105C64N5O43"), }, ID = 1735, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(2)HexNAc(6)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H118C72N6O51S", Losses = new [] { new FragmentLoss("H118C72N6O51S"), }, ID = 1966, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H50C30O23", Losses = new [] { new FragmentLoss("H50C30O23"), }, ID = 1584, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexA(1)HexNAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H71C44NO37S", Losses = new [] { new FragmentLoss("H71C44NO37S"), }, ID = 1663, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexA(1)HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H84C52N2O42S", Losses = new [] { new FragmentLoss("H84C52N2O42S"), }, ID = 1702, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexA(1)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H97C60N3O47S", Losses = new [] { new FragmentLoss("H97C60N3O47S"), }, ID = 1736, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H63C38NO31S", Losses = new [] { new FragmentLoss("H63C38NO31S"), }, ID = 1635, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H76C46N2O33", Losses = new [] { new FragmentLoss("H76C46N2O33"), }, ID = 1765, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H76C46N2O33", Losses = new [] { new FragmentLoss("H76C46N2O33"), }, ID = 1765, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(2)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H93C57N3O42", Losses = new [] { new FragmentLoss("H93C57N3O42"), }, ID = 1715, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H76C46N2O36S", Losses = new [] { new FragmentLoss("H76C46N2O36S"), }, ID = 1668, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H89C54N3O38", Losses = new [] { new FragmentLoss("H89C54N3O38"), }, ID = 1771, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H89C54N3O38", Losses = new [] { new FragmentLoss("H89C54N3O38"), }, ID = 1771, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(3)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H106C65N4O46", Losses = new [] { new FragmentLoss("H106C65N4O46"), }, ID = 1739, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(3)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H123C76N5O54", Losses = new [] { new FragmentLoss("H123C76N5O54"), }, ID = 1758, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(3)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H97C59N3O42", Losses = new [] { new FragmentLoss("H97C59N3O42"), }, ID = 1475, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(3)Pent(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H105C64N3O46", Losses = new [] { new FragmentLoss("H105C64N3O46"), }, ID = 1494, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H89C54N3O41S", Losses = new [] { new FragmentLoss("H89C54N3O41S"), }, ID = 1954, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O43", Losses = new [] { new FragmentLoss("H102C62N4O43"), }, ID = 1774, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O43", Losses = new [] { new FragmentLoss("H102C62N4O43"), }, ID = 1774, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(4)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H119C73N5O51", Losses = new [] { new FragmentLoss("H119C73N5O51"), }, ID = 1965, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(4)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H110C67N4O47", Losses = new [] { new FragmentLoss("H110C67N4O47"), }, ID = 1507, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(4)Pent(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H118C72N4O51", Losses = new [] { new FragmentLoss("H118C72N4O51"), }, ID = 1528, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(5) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H115C70N5O48", Losses = new [] { new FragmentLoss("H115C70N5O48"), }, ID = 1746, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H115C70N5O48", Losses = new [] { new FragmentLoss("H115C70N5O48"), }, ID = 1746, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(3)HexNAc(6) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H128C78N6O53", Losses = new [] { new FragmentLoss("H128C78N6O53"), }, ID = 1562, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H60C36O28", Losses = new [] { new FragmentLoss("H60C36O28"), }, ID = 1612, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexA(1)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H107C66N3O52S", Losses = new [] { new FragmentLoss("H107C66N3O52S"), }, ID = 1748, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H73C44NO33", Losses = new [] { new FragmentLoss("H73C44NO33"), }, ID = 1648, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H86C52N2O38", Losses = new [] { new FragmentLoss("H86C52N2O38"), }, ID = 1770, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H86C52N2O38", Losses = new [] { new FragmentLoss("H86C52N2O38"), }, ID = 1770, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H99C60N3O43", Losses = new [] { new FragmentLoss("H99C60N3O43"), }, ID = 1481, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(3)NeuAc(1)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H116C71N4O54S", Losses = new [] { new FragmentLoss("H116C71N4O54S"), }, ID = 1542, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(3)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H107C65N3O47", Losses = new [] { new FragmentLoss("H107C65N3O47"), }, ID = 1498, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H112C68N4O48", Losses = new [] { new FragmentLoss("H112C68N4O48"), }, ID = 1778, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H112C68N4O48", Losses = new [] { new FragmentLoss("H112C68N4O48"), }, ID = 1778, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(4)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H120C73N4O52", Losses = new [] { new FragmentLoss("H120C73N4O52"), }, ID = 1535, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(4)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H112C68N4O51S", Losses = new [] { new FragmentLoss("H112C68N4O51S"), }, ID = 1523, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(5) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H125C76N5O53", Losses = new [] { new FragmentLoss("H125C76N5O53"), }, ID = 1785, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(4)HexNAc(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H125C76N5O53", Losses = new [] { new FragmentLoss("H125C76N5O53"), }, ID = 1785, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(5)HexNAc(2)Me(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H98C59N2O43", Losses = new [] { new FragmentLoss("H98C59N2O43"), }, ID = 1955, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(5)HexNAc(3)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H117C71N3O52", Losses = new [] { new FragmentLoss("H117C71N3O52"), }, ID = 1526, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)Hex(5)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H122C74N4O53", Losses = new [] { new FragmentLoss("H122C74N4O53"), }, ID = 1541, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)HexNAc(2)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H60C37N2O26", Losses = new [] { new FragmentLoss("H60C37N2O26"), }, ID = 1614, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)HexNAc(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H85C52N5O33", Losses = new [] { new FragmentLoss("H85C52N5O33"), }, ID = 1680, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(2)HexNAc(7) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H111C68N7O43", Losses = new [] { new FragmentLoss("H111C68N7O43"), }, ID = 1743, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(1)HexNAc(2)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H80C49N2O35", Losses = new [] { new FragmentLoss("H80C49N2O35"), }, ID = 1667, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(1)HexNAc(3)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H93C57N3O40", Losses = new [] { new FragmentLoss("H93C57N3O40"), }, ID = 1709, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(2)HexA(1)HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H84C52N2O41S", Losses = new [] { new FragmentLoss("H84C52N2O41S"), }, ID = 1699, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(2)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H76C46N2O32", Losses = new [] { new FragmentLoss("H76C46N2O32"), }, ID = 1654, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(2)HexNAc(2)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H90C55N2O40", Losses = new [] { new FragmentLoss("H90C55N2O40"), }, ID = 1698, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(2)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H89C54N3O37", Losses = new [] { new FragmentLoss("H89C54N3O37"), }, ID = 1689, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(2)HexNAc(3)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H103C63N3O45", Losses = new [] { new FragmentLoss("H103C63N3O45"), }, ID = 1733, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(2)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O42", Losses = new [] { new FragmentLoss("H102C62N4O42"), }, ID = 1728, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(2)HexNAc(4)Sulf(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O48S2", Losses = new [] { new FragmentLoss("H102C62N4O48S2"), }, ID = 1958, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(3)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H73C44NO32", Losses = new [] { new FragmentLoss("H73C44NO32"), }, ID = 1946, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(3)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H86C52N2O37", Losses = new [] { new FragmentLoss("H86C52N2O37"), }, ID = 1950, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(3)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H99C60N3O42", Losses = new [] { new FragmentLoss("H99C60N3O42"), }, ID = 1722, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(3)HexNAc(3)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H116C71N4O50", Losses = new [] { new FragmentLoss("H116C71N4O50"), }, ID = 1751, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(3)HexNAc(3)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H107C65N3O46", Losses = new [] { new FragmentLoss("H107C65N3O46"), }, ID = 1497, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(3)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H112C68N4O47", Losses = new [] { new FragmentLoss("H112C68N4O47"), }, ID = 1511, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(3)HexNAc(4)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H120C73N4O51", Losses = new [] { new FragmentLoss("H120C73N4O51"), }, ID = 1533, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(4)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H122C74N4O52", Losses = new [] { new FragmentLoss("H122C74N4O52"), }, ID = 1783, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(4)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H122C74N4O52", Losses = new [] { new FragmentLoss("H122C74N4O52"), }, ID = 1783, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)Hex(4)HexNAc(4)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H122C74N4O55S", Losses = new [] { new FragmentLoss("H122C74N4O55S"), }, ID = 1557, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(3)HexNAc(3)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H83C51N3O35", Losses = new [] { new FragmentLoss("H83C51N3O35"), }, ID = 1676, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(4)Hex(1)HexNAc(1)Kdn(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H91C56NO42", Losses = new [] { new FragmentLoss("H91C56NO42"), }, ID = 1706, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(4)Hex(1)HexNAc(2)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H90C55N2O39", Losses = new [] { new FragmentLoss("H90C55N2O39"), }, ID = 1697, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(4)Hex(1)HexNAc(3)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H103C63N3O44", Losses = new [] { new FragmentLoss("H103C63N3O44"), }, ID = 1730, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(4)Hex(2)HexNAc(2)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H100C61N2O44", Losses = new [] { new FragmentLoss("H100C61N2O44"), }, ID = 1726, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(4)Hex(2)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H99C60N3O41", Losses = new [] { new FragmentLoss("H99C60N3O41"), }, ID = 1719, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(4)Hex(3)HexNAc(2)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H113C69N3O49", Losses = new [] { new FragmentLoss("H113C69N3O49"), }, ID = 1960, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(4)Hex(3)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H109C66N3O46", Losses = new [] { new FragmentLoss("H109C66N3O46"), }, ID = 1740, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dHex(4)HexNAc(3)Kdn(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H93C57N3O39", Losses = new [] { new FragmentLoss("H93C57N3O39"), }, ID = 1703, 
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
                 Structural = true, ShortName = "DiG", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C7C'4NN'O2", ID = 1392, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H20C7C'4NN'O2", ID = 1392, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H20C7C'4NN'O2", ID = 1392, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex115 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C8C'3N'2O2", ID = 1393, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex115 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H20C8C'3N'2O2", ID = 1393, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex115 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H20C8C'3N'2O2", ID = 1393, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex116/119 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H18H'2C9C'2NN'O2", ID = 1394, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex116/119 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H18H'2C9C'2NN'O2", ID = 1394, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex116/119 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H18H'2C9C'2NN'O2", ID = 1394, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex117 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H18H'2C10C'N'2O2", ID = 1395, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex117 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H18H'2C10C'N'2O2", ID = 1395, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex117 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H18H'2C10C'N'2O2", ID = 1395, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex118 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H18H'2C8C'3N2O2", ID = 1396, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex118 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H18H'2C8C'3N2O2", ID = 1396, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiART6plex118 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H18H'2C8C'3N2O2", ID = 1396, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dibromo (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "Br2 - H2", ID = 534, 
                 Structural = true, ShortName = "2Br", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dicarbamidomethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 1290, 
                 Structural = true, ShortName = "2CM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dicarbamidomethyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 1290, 
                 Structural = true, ShortName = "2CM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dicarbamidomethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 1290, 
                 Structural = true, ShortName = "2CM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dicarbamidomethyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 1290, 
                 Structural = true, ShortName = "2CM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dicarbamidomethyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 1290, 
                 Structural = true, ShortName = "2CM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dichlorination (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "Cl2 - H2", ID = 937, 
                 Structural = true, ShortName = "2Cl", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dichlorination (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "Cl2 - H2", ID = 937, 
                 Structural = true, ShortName = "2Cl", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Didehydro (C-term K)", 
                 AAs = "K", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H2", ID = 401, 
                 Structural = true, ShortName = "-2H", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Didehydro (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "-H2", ID = 401, 
                 Structural = true, ShortName = "-2H", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Didehydro (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "-H2", ID = 401, 
                 Structural = true, ShortName = "-2H", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Didehydro (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "-H2", ID = 401, 
                 Structural = true, ShortName = "-2H", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Didehydroretinylidene (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H24C20", ID = 433, 
                 Structural = true, ShortName = "ddR", Hidden = true, 
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
                 Name = "Diethylphosphate (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphate (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphate (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphate (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H9C4O3P", ID = 725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphothione (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C4O2PS", ID = 1986, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphothione (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H9C4O2PS", ID = 1986, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphothione (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C4O2PS", ID = 1986, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphothione (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H9C4O2PS", ID = 1986, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphothione (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H9C4O2PS", ID = 1986, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diethylphosphothione (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H9C4O2PS", ID = 1986, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Difuran (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H4C8O2", ID = 1279, 
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
                 Name = "Diiodo (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "I2 - H2", ID = 130, 
                 Structural = true, ShortName = "2Io", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diiodo (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "I2 - H2", ID = 130, 
                 Structural = true, ShortName = "2Io", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diironsubcluster (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C5N2O5S2Fe2 - H", ID = 439, 
                 Structural = true, ShortName = "dFe", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diisopropylphosphate (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H13C6O3P", ID = 362, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diisopropylphosphate (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H13C6O3P", ID = 362, 
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
                 Name = "DiLeu4plex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H13H'2C8NO'", ID = 1322, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiLeu4plex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H13H'2C8NO'", ID = 1322, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiLeu4plex (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H13H'2C8NO'", ID = 1322, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiLeu4plex115 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C7C'N'O'", ID = 1321, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiLeu4plex115 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H15C7C'N'O'", ID = 1321, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiLeu4plex115 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H15C7C'N'O'", ID = 1321, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiLeu4plex117 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H13H'2C7C'N'O", ID = 1323, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiLeu4plex117 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H13H'2C7C'N'O", ID = 1323, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiLeu4plex117 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H13H'2C7C'N'O", ID = 1323, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiLeu4plex118 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H11H'4C8NO", ID = 1324, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiLeu4plex118 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H11H'4C8NO", ID = 1324, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DiLeu4plex118 (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H11H'4C8NO", ID = 1324, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C2H4", ID = 36, 
                 Structural = true, ShortName = "2Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "C2H4", ID = 36, 
                 Structural = true, ShortName = "2Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C2H4", ID = 36, 
                 Structural = true, ShortName = "2Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl (Protein N-term P)", 
                 AAs = "P", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C2H4", ID = 36, 
                 Structural = true, ShortName = "2Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C2H4", ID = 36, 
                 Structural = true, ShortName = "2Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4C2", ID = 199, 
                 Structural = true, ShortName = "DM4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(4) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'4C2", ID = 199, 
                 Structural = true, ShortName = "DM4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(4) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H'4C2", ID = 199, 
                 Structural = true, ShortName = "DM4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(4)13C(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'2H'4", ID = 510, 
                 Structural = true, ShortName = "D6a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(4)13C(2) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'2H'4", ID = 510, 
                 Structural = true, ShortName = "D6a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(4)13C(2) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C'2H'4", ID = 510, 
                 Structural = true, ShortName = "D6a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DimethylamineGMBS (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H21C13N3O3", ID = 943, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethylaminoethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C4N", ID = 1846, 
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
                 Name = "Dimethylphosphothione (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H5C2O2PS", ID = 1987, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethylphosphothione (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H5C2O2PS", ID = 1987, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethylphosphothione (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C2O2PS", ID = 1987, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethylphosphothione (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H5C2O2PS", ID = 1987, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethylphosphothione (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H5C2O2PS", ID = 1987, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethylphosphothione (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H5C2O2PS", ID = 1987, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DimethylpyrroleAdduct (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C6", ID = 316, 
                 Structural = true, ShortName = "DpA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (I)", 
                 AAs = "I", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (U)", 
                 AAs = "U", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (V)", 
                 AAs = "V", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dioxidation (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "O2", ID = 425, 
                 Structural = true, ShortName = "2Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Diphthamide (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H14C7N2O", ID = 375, 
                 Structural = true, ShortName = "Dip", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dipyridyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H11C13N3O", ID = 1277, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dipyrrolylmethanemethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H22C20N2O8", ID = 416, 
                 Structural = true, ShortName = "dpM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DMPO (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C6NO", ID = 1017, 
                 Structural = true, ShortName = "DPO", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DMPO (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H9C6NO", ID = 1017, 
                 Structural = true, ShortName = "DPO", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DMPO (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H9C6NO", ID = 1017, 
                 Structural = true, ShortName = "DPO", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DNCB_hapten (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2C6N2O4", ID = 1331, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DNCB_hapten (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H2C6N2O4", ID = 1331, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DNCB_hapten (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C6N2O4", ID = 1331, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DNCB_hapten (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H2C6N2O4", ID = 1331, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "dNIC (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "HH'3C6NO", ID = 698, 
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
                 Name = "DTT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H8C4O2S2", ID = 1871, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "DVFQQQTGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H60C41N12O15", ID = 2085, 
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
                 Name = "DYn-2 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H13C11O", ID = 1872, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EDEDTIDVFQQQTGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H102C69N18O30", ID = 1406, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EDT-iodoacetyl-PEO-biotin (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H34C20N4O4S3", ID = 118, 
                 Structural = true, ShortName = "EPB", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EDT-iodoacetyl-PEO-biotin (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H34C20N4O4S3", ID = 118, 
                 Structural = true, ShortName = "EPB", Hidden = true, 
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
                 Name = "EEEDVIEVYQEQTGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H107C72N17O31", ID = 1405, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EGCG1 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H16C22O11", ID = 1002, 
                 Structural = true, ShortName = "EGG", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EGCG2 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H11C15O6", ID = 1003, 
                 Structural = true, ShortName = "DEG", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EHD-diphenylpentanone (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H18C18O2", ID = 1317, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EHD-diphenylpentanone (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H18C18O2", ID = 1317, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EQAT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C10H20N2O", ID = 197, 
                 Structural = true, ShortName = "EQ0", Hidden = true, 
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
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C16H26N4O2S", ID = 90, 
                 Structural = true, ShortName = "E00", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ESP (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C16H26N4O2S", ID = 90, 
                 Structural = true, ShortName = "E00", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanedithiol (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C2S2 - O", ID = 200, 
                 Structural = true, ShortName = "Edl", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanedithiol (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H4C2S2 - O", ID = 200, 
                 Structural = true, ShortName = "Edl", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanolamine (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H5C2N", ID = 734, 
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
                 Name = "Ethanolyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4C2O", ID = 278, 
                 Structural = true, ShortName = "EtO", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanolyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C2O", ID = 278, 
                 Structural = true, ShortName = "EtO", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethanolyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H4C2O", ID = 278, 
                 Structural = true, ShortName = "EtO", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 280, 
                 Structural = true, ShortName = "1Et", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 280, 
                 Structural = true, ShortName = "1Et", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 280, 
                 Structural = true, ShortName = "1Et", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 280, 
                 Structural = true, ShortName = "1Et", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C2", ID = 280, 
                 Structural = true, ShortName = "1Et", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl+Deamidated (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H3C2O - N", ID = 931, 
                 Structural = true, ShortName = "EOD", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethyl+Deamidated (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H3C2O - N", ID = 931, 
                 Structural = true, ShortName = "EOD", Hidden = true, 
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
                 Name = "Ethylphosphate (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C2O3P", ID = 726, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethylphosphate (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H5C2O3P", ID = 726, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethylphosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H5C2O3P", ID = 726, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethylphosphate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H5C2O3P", ID = 726, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethylphosphate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H5C2O3P", ID = 726, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ethylsulfonylethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H8C4O2S", ID = 1381, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ethylsulfonylethyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H8C4O2S", ID = 1381, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ethylsulfonylethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C4O2S", ID = 1381, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ethynyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C2", ID = 2081, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ExacTagAmine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H52C25C'12N8N'6O19S", ID = 741, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ExacTagThiol (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H50C23C'12N8N'6O18", ID = 740, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FAD (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H31C27N9O15P2", ID = 50, 
                 Structural = true, ShortName = "FAD", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FAD (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H31C27N9O15P2", ID = 50, 
                 Structural = true, ShortName = "FAD", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FAD (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H31C27N9O15P2", ID = 50, 
                 Structural = true, ShortName = "FAD", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Farnesyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H24C15", ID = 44, 
                 Structural = true, ShortName = "Far", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Fluorescein (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H13C22NO6", ID = 128, 
                 Structural = true, ShortName = "Fsc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Fluorescein-tyramine (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H19C29NO7", ID = 1801, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Fluoro (A)", 
                 AAs = "A", LabelAtoms = LabelAtoms.None, Formula = "F - H", ID = 127, 
                 Structural = true, ShortName = "+1F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Fluoro (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "F - H", ID = 127, 
                 Structural = true, ShortName = "+1F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Fluoro (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "F - H", ID = 127, 
                 Structural = true, ShortName = "+1F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Fluoro (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "F - H", ID = 127, 
                 Structural = true, ShortName = "+1F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FMN (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H19C17N4O8P", ID = 442, 
                 Structural = true, ShortName = "FMN", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FMN (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H19C17N4O8P", ID = 442, 
                 Structural = true, ShortName = "FMN", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FMNC (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H21C17N4O9P", ID = 443, 
                 Structural = true, ShortName = "FNC", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FMNH (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H19C17N4O9P", ID = 409, 
                 Structural = true, ShortName = "FNH", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FMNH (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H19C17N4O9P", ID = 409, 
                 Structural = true, ShortName = "FNH", Hidden = true, 
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
                 Structural = true, ShortName = "Frm", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Formyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "CO", ID = 122, 
                 Structural = true, ShortName = "Frm", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Formyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "CO", ID = 122, 
                 Structural = true, ShortName = "Frm", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Formylasparagine (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "O2 - HCN", ID = 1917, 
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
                 Structural = true, ShortName = "FTC", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FTC (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C21N3O5S", ID = 478, 
                 Structural = true, ShortName = "FTC", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FTC (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H15C21N3O5S", ID = 478, 
                 Structural = true, ShortName = "FTC", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FTC (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H15C21N3O5S", ID = 478, 
                 Structural = true, ShortName = "FTC", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "FTC (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H15C21N3O5S", ID = 478, 
                 Structural = true, ShortName = "FTC", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Furan (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H2C4O", ID = 1278, 
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
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10C6O6", ID = 907, 
                 Structural = true, ShortName = "OH1", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Galactosyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H10C6O6", ID = 907, 
                 Structural = true, ShortName = "OH1", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GEE (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H6C4O2", ID = 1824, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GeranylGeranyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H32C20", ID = 48, 
                 Structural = true, ShortName = "2Ge", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GG (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 121, 
                 Structural = true, ShortName = "UGG", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 121, 
                 Structural = true, ShortName = "UGG", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GG (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 121, 
                 Structural = true, ShortName = "UGG", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GG (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H6C4N2O2", ID = 121, 
                 Structural = true, ShortName = "UGG", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GGQ (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H14C9N4O4", ID = 1292, 
                 Structural = true, ShortName = "GGQ", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C7H13NO", Losses = new [] { new FragmentLoss("H9C3N"), }, ID = 60, 
                 Structural = true, ShortName = "GQ0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C7H13NO", Losses = new [] { new FragmentLoss("H9C3N"), }, ID = 60, 
                 Structural = true, ShortName = "GQ0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glu (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H7C5NO3", ID = 450, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glu+O(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H7C5NO5", ID = 2037, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glu->pyro-Glu+Methyl (N-term E)", 
                 AAs = "E", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C - O", ID = 1826, 
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
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C12O11", Losses = new [] { new FragmentLoss("H20C12O11"), }, ID = 393, 
                 Structural = true, ShortName = "OH2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glucuronyl (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H8C6O6", Losses = new [] { new FragmentLoss("H8C6O6"), }, ID = 54, 
                 Structural = true, ShortName = "GCn", Hidden = true, 
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
                 Name = "Gluratylation (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C5O3", ID = 1848, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glutathione (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H15C10N3O6S", ID = 55, 
                 Structural = true, ShortName = "GSO", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Gly (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 1263, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Gly (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 1263, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Gly (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO", ID = 1263, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Gly+O(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H3C2NO3", ID = 2034, 
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
                 Structural = true, ShortName = "GPh", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glyceroyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3O3", ID = 2072, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GlycerylPE (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H12C5NO5P", ID = 396, 
                 Structural = true, ShortName = "GPE", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "glycidamide (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C3NO2", ID = 1014, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "glycidamide (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H5C3NO2", ID = 1014, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glycosyl (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H8C5O5", ID = 408, 
                 Structural = true, ShortName = "Gsl", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "glyoxalAGE (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C2 - H2", ID = 1913, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GNLLFLACYCIGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H92C61N14O15S2", ID = 1991, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Guanidinyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H2CN2", ID = 52, 
                 Structural = true, ShortName = "1Gu", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Haloxon (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H7C4O3PCl2", ID = 2006, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Haloxon (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H7C4O3PCl2", ID = 2006, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Haloxon (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7C4O3PCl2", ID = 2006, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Haloxon (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H7C4O3PCl2", ID = 2006, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Haloxon (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H7C4O3PCl2", ID = 2006, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HCysteinyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H7C4NO2S", ID = 1271, 
                 Structural = true, ShortName = "hCy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HCysThiolactone (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7C4NOS", ID = 1270, 
                 Structural = true, ShortName = "hCo", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Heme (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H32C34N4O4Fe", ID = 390, 
                 Structural = true, ShortName = "HEM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Heme (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H32C34N4O4Fe", ID = 390, 
                 Structural = true, ShortName = "HEM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hep (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C7O6", ID = 490, 
                 Structural = true, ShortName = "Hep", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hep (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H12C7O6", Losses = new [] { new FragmentLoss("H12C7O6"), }, ID = 490, 
                 Structural = true, ShortName = "Hep", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hep (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H12C7O6", ID = 490, 
                 Structural = true, ShortName = "Hep", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hep (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H12C7O6", ID = 490, 
                 Structural = true, ShortName = "Hep", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hep (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H12C7O6", Losses = new [] { new FragmentLoss("H12C7O6"), }, ID = 490, 
                 Structural = true, ShortName = "Hep", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", ID = 41, 
                 Structural = true, ShortName = "Hex", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (KR)", 
                 AAs = "K, R", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", Losses = new [] { new FragmentLoss("H6O3"), }, ID = 41, 
                 Structural = true, ShortName = "Hex", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", Losses = new [] { new FragmentLoss("H10C6O5"), }, ID = 41, 
                 Structural = true, ShortName = "Hex", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", Losses = new [] { new FragmentLoss("H6O3"), }, ID = 41, 
                 Structural = true, ShortName = "Hex", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", Losses = new [] { new FragmentLoss("H10C6O5"), }, ID = 41, 
                 Structural = true, ShortName = "Hex", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", ID = 41, 
                 Structural = true, ShortName = "Hex", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H10C6O5", ID = 41, 
                 Structural = true, ShortName = "Hex", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexA(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H18C12O11", Losses = new [] { new FragmentLoss("H18C12O11"), }, ID = 1427, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexA(1)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H31C20NO16", Losses = new [] { new FragmentLoss("H31C20NO16"), }, ID = 1439, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexA(1)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H44C28N2O21", Losses = new [] { new FragmentLoss("H44C28N2O21"), }, ID = 1578, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H23C14NO10", Losses = new [] { new FragmentLoss("H23C14NO10"), }, ID = 793, 
                 Structural = true, ShortName = "HHN", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H23C14NO10", Losses = new [] { new FragmentLoss("H23C14NO10"), }, ID = 793, 
                 Structural = true, ShortName = "HHN", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)dHex(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H33C20NO14", Losses = new [] { new FragmentLoss("H33C20NO14"), }, ID = 146, 
                 Structural = true, ShortName = "K1e", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)dHex(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H33C20NO14", Losses = new [] { new FragmentLoss("H33C20NO14"), }, ID = 146, 
                 Structural = true, ShortName = "K1e", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)dHex(1)Me(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H35C21NO14", Losses = new [] { new FragmentLoss("H35C21NO14"), }, ID = 1436, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)dHex(1)Me(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H37C22NO14", Losses = new [] { new FragmentLoss("H37C22NO14"), }, ID = 1437, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)Kdn(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H37C23NO21S", Losses = new [] { new FragmentLoss("H37C23NO21S"), }, ID = 1567, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H40C25N2O18", Losses = new [] { new FragmentLoss("H40C25N2O18"), }, ID = 149, 
                 Structural = true, ShortName = "NHH", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H40C25N2O18", Losses = new [] { new FragmentLoss("H40C25N2O18"), }, ID = 149, 
                 Structural = true, ShortName = "NHH", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(1)Ac(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H42C27N2O19", Losses = new [] { new FragmentLoss("H42C27N2O19"), }, ID = 1786, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(1)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H57C36N3O27", Losses = new [] { new FragmentLoss("H57C36N3O27"), }, ID = 1617, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H40C25N2O21S", Losses = new [] { new FragmentLoss("H40C25N2O21S"), }, ID = 1577, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H57C36N3O26", Losses = new [] { new FragmentLoss("H57C36N3O26"), }, ID = 160, 
                 Structural = true, ShortName = "NHN", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H57C36N3O26", Losses = new [] { new FragmentLoss("H57C36N3O26"), }, ID = 160, 
                 Structural = true, ShortName = "NHN", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(2)Ac(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H59C38N3O27", Losses = new [] { new FragmentLoss("H59C38N3O27"), }, ID = 1620, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(2)Ac(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H61C40N3O28", Losses = new [] { new FragmentLoss("H61C40N3O28"), }, ID = 1630, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H74C47N4O34", Losses = new [] { new FragmentLoss("H74C47N4O34"), }, ID = 1664, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H40C25N2O19", Losses = new [] { new FragmentLoss("H40C25N2O19"), }, ID = 1563, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuGc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H57C36N3O28", Losses = new [] { new FragmentLoss("H57C36N3O28"), }, ID = 1619, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuGc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H74C47N4O37", Losses = new [] { new FragmentLoss("H74C47N4O37"), }, ID = 1672, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuGc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H91C58N5O46", Losses = new [] { new FragmentLoss("H91C58N5O46"), }, ID = 1729, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)NeuGc(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H108C69N6O55", Losses = new [] { new FragmentLoss("H108C69N6O55"), }, ID = 1755, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)Phos(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H24C14NO13P", Losses = new [] { new FragmentLoss("H24C14NO13P"), }, ID = 1429, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H23C14NO13S", Losses = new [] { new FragmentLoss("H23C14NO13S"), }, ID = 1430, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H36C22N2O15", Losses = new [] { new FragmentLoss("H36C22N2O15"), }, ID = 148, 
                 Structural = true, ShortName = "1K1", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H36C22N2O15", Losses = new [] { new FragmentLoss("H36C22N2O15"), }, ID = 148, 
                 Structural = true, ShortName = "1K1", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)dHex(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H46C28N2O19", Losses = new [] { new FragmentLoss("H46C28N2O19"), }, ID = 152, 
                 Structural = true, ShortName = "K1F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)dHex(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H46C28N2O19", Losses = new [] { new FragmentLoss("H46C28N2O19"), }, ID = 152, 
                 Structural = true, ShortName = "K1F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)dHex(1)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H54C33N2O23", Losses = new [] { new FragmentLoss("H54C33N2O23"), }, ID = 155, 
                 Structural = true, ShortName = "K1G", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)dHex(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O23", Losses = new [] { new FragmentLoss("H56C34N2O23"), }, ID = 156, 
                 Structural = true, ShortName = "K1H", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)dHex(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O23", Losses = new [] { new FragmentLoss("H56C34N2O23"), }, ID = 156, 
                 Structural = true, ShortName = "K1H", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)dHex(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O26S", Losses = new [] { new FragmentLoss("H56C34N2O26S"), }, ID = 1941, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H53C33N3O23", Losses = new [] { new FragmentLoss("H53C33N3O23"), }, ID = 1600, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)NeuAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H53C33N3O26S", Losses = new [] { new FragmentLoss("H53C33N3O26S"), }, ID = 1611, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H70C44N4O31", Losses = new [] { new FragmentLoss("H70C44N4O31"), }, ID = 1649, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)NeuAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H70C44N4O34S", Losses = new [] { new FragmentLoss("H70C44N4O34S"), }, ID = 1662, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H53C33N3O24", Losses = new [] { new FragmentLoss("H53C33N3O24"), }, ID = 1602, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H44C27N2O19", Losses = new [] { new FragmentLoss("H44C27N2O19"), }, ID = 151, 
                 Structural = true, ShortName = "K1P", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H36C22N2O18S", Losses = new [] { new FragmentLoss("H36C22N2O18S"), }, ID = 1447, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H49C30N3O20", Losses = new [] { new FragmentLoss("H49C30N3O20"), }, ID = 1582, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(3)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H66C41N4O28", Losses = new [] { new FragmentLoss("H66C41N4O28"), }, ID = 1636, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(3)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H83C52N5O36", Losses = new [] { new FragmentLoss("H83C52N5O36"), }, ID = 1687, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(3)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H66C41N4O29", Losses = new [] { new FragmentLoss("H66C41N4O29"), }, ID = 1638, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H49C30N3O23S", Losses = new [] { new FragmentLoss("H49C30N3O23S"), }, ID = 1598, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)HexNAc(4)dHex(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H72C44N4O32S", Losses = new [] { new FragmentLoss("H72C44N4O32S"), }, ID = 1948, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H27C17NO13", Losses = new [] { new FragmentLoss("H27C17NO13"), }, ID = 1431, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)NeuAc(1)Pent(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H35C22NO17", Losses = new [] { new FragmentLoss("H35C22NO17"), }, ID = 1442, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H27C17NO14", Losses = new [] { new FragmentLoss("H27C17NO14"), }, ID = 1432, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)Pent(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H18C11O9", Losses = new [] { new FragmentLoss("H18C11O9"), }, ID = 1426, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)Pent(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H26C16O13", Losses = new [] { new FragmentLoss("H26C16O13"), }, ID = 1428, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)Pent(2)Me(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H28C17O13", Losses = new [] { new FragmentLoss("H28C17O13"), }, ID = 1933, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)Pent(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H34C21O17", Losses = new [] { new FragmentLoss("H34C21O17"), }, ID = 1441, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(1)Pent(3)Me(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H36C22O17", Losses = new [] { new FragmentLoss("H36C22O17"), }, ID = 1935, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(10)HexNAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H113C68NO55", Losses = new [] { new FragmentLoss("H113C68NO55"), }, ID = 1962, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(10)Phos(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H103C60O59P3", Losses = new [] { new FragmentLoss("H103C60O59P3"), }, ID = 1753, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C12O10", ID = 512, 
                 Structural = true, ShortName = "2Hx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H20C12O10", ID = 512, 
                 Structural = true, ShortName = "2Hx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H20C12O10", Losses = new [] { new FragmentLoss("H20C12O10"), }, ID = 512, 
                 Structural = true, ShortName = "2Hx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexA(1)HexNAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H41C26NO24S", Losses = new [] { new FragmentLoss("H41C26NO24S"), }, ID = 1585, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexA(1)NeuAc(1)Pent(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H53C34NO31S", Losses = new [] { new FragmentLoss("H53C34NO31S"), }, ID = 1623, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexA(1)Pent(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H36C23O23S", Losses = new [] { new FragmentLoss("H36C23O23S"), }, ID = 1572, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H33C20NO15", Losses = new [] { new FragmentLoss("H33C20NO15"), }, ID = 1438, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H33C20NO15", Losses = new [] { new FragmentLoss("H33C20NO15"), }, ID = 1438, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(1)Me(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H35C21NO15", Losses = new [] { new FragmentLoss("H35C21NO15"), }, ID = 1440, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(1)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H50C31N2O24", Losses = new [] { new FragmentLoss("H50C31N2O24"), }, ID = 1595, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(1)NeuGc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H67C42N3O33", Losses = new [] { new FragmentLoss("H67C42N3O33"), }, ID = 1647, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(1)NeuGc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H84C53N4O42", Losses = new [] { new FragmentLoss("H84C53N4O42"), }, ID = 1705, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(1)NeuGc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H101C64N5O51", Losses = new [] { new FragmentLoss("H101C64N5O51"), }, ID = 1744, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(1)Pent(1)HexA(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H49C31NO25", Losses = new [] { new FragmentLoss("H49C31NO25"), }, ID = 1939, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H33C20NO18S", Losses = new [] { new FragmentLoss("H33C20NO18S"), }, ID = 1443, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H46C28N2O20", Losses = new [] { new FragmentLoss("H46C28N2O20"), }, ID = 153, 
                 Structural = true, ShortName = "1K2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H46C28N2O20", Losses = new [] { new FragmentLoss("H46C28N2O20"), }, ID = 153, 
                 Structural = true, ShortName = "1K2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)dHex(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O24", Losses = new [] { new FragmentLoss("H56C34N2O24"), }, ID = 158, 
                 Structural = true, ShortName = "K2F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)dHex(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O24", Losses = new [] { new FragmentLoss("H56C34N2O24"), }, ID = 158, 
                 Structural = true, ShortName = "K2F", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H63C39N3O28", Losses = new [] { new FragmentLoss("H63C39N3O28"), }, ID = 1450, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H63C39N3O28", Losses = new [] { new FragmentLoss("H63C39N3O28"), }, ID = 1450, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)NeuAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H63C39N3O31S", Losses = new [] { new FragmentLoss("H63C39N3O31S"), }, ID = 1642, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H80C50N4O36", Losses = new [] { new FragmentLoss("H80C50N4O36"), }, ID = 1681, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)NeuAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H80C50N4O39S", Losses = new [] { new FragmentLoss("H80C50N4O39S"), }, ID = 1694, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H63C39N3O29", Losses = new [] { new FragmentLoss("H63C39N3O29"), }, ID = 1631, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H54C33N2O24", Losses = new [] { new FragmentLoss("H54C33N2O24"), }, ID = 157, 
                 Structural = true, ShortName = "K2P", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H46C28N2O23S", Losses = new [] { new FragmentLoss("H46C28N2O23S"), }, ID = 1589, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H59C36N3O25", Losses = new [] { new FragmentLoss("H59C36N3O25"), }, ID = 1610, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H59C36N3O25", Losses = new [] { new FragmentLoss("H59C36N3O25"), }, ID = 1610, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(3)NeuAc(1)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H93C58N5O42", Losses = new [] { new FragmentLoss("H93C58N5O42"), }, ID = 1720, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(3)NeuAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H76C47N4O36S", Losses = new [] { new FragmentLoss("H76C47N4O36S"), }, ID = 1678, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(3)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H93C58N5O41", Losses = new [] { new FragmentLoss("H93C58N5O41"), }, ID = 1717, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(3)NeuAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H110C69N6O49", Losses = new [] { new FragmentLoss("H110C69N6O49"), }, ID = 1749, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(3)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H76C47N4O34", Losses = new [] { new FragmentLoss("H76C47N4O34"), }, ID = 1665, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(3)NeuGc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H93C58N5O43", Losses = new [] { new FragmentLoss("H93C58N5O43"), }, ID = 1725, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(3)NeuGc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H110C69N6O52", Losses = new [] { new FragmentLoss("H110C69N6O52"), }, ID = 1752, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H59C36N3O28S", Losses = new [] { new FragmentLoss("H59C36N3O28S"), }, ID = 1626, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H72C44N4O30", Losses = new [] { new FragmentLoss("H72C44N4O30"), }, ID = 1646, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H72C44N4O30", Losses = new [] { new FragmentLoss("H72C44N4O30"), }, ID = 1646, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(4)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H89C55N5O38", Losses = new [] { new FragmentLoss("H89C55N5O38"), }, ID = 1700, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)HexNAc(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H85C52N5O35", Losses = new [] { new FragmentLoss("H85C52N5O35"), }, ID = 1685, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H37C23NO18", Losses = new [] { new FragmentLoss("H37C23NO18"), }, ID = 1444, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)Pent(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H36C22O18", Losses = new [] { new FragmentLoss("H36C22O18"), }, ID = 1936, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)Pent(2)Me(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H38C23O18", Losses = new [] { new FragmentLoss("H38C23O18"), }, ID = 1937, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H20C12O13S", Losses = new [] { new FragmentLoss("H20C12O13S"), }, ID = 1932, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H30C18O15", Losses = new [] { new FragmentLoss("H30C18O15"), }, ID = 144, 
                 Structural = true, ShortName = "3Hx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H30C18O15", Losses = new [] { new FragmentLoss("H30C18O15"), }, ID = 144, 
                 Structural = true, ShortName = "3Hx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H43C26NO20", Losses = new [] { new FragmentLoss("H43C26NO20"), }, ID = 1566, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H43C26NO20", Losses = new [] { new FragmentLoss("H43C26NO20"), }, ID = 1566, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(1)HexA(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H51C32NO26", Losses = new [] { new FragmentLoss("H51C32NO26"), }, ID = 1940, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(1)Me(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H45C27NO20", Losses = new [] { new FragmentLoss("H45C27NO20"), }, ID = 1571, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(1)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H51C31NO24", Losses = new [] { new FragmentLoss("H51C31NO24"), }, ID = 154, 
                 Structural = true, ShortName = "K3e", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O25", Losses = new [] { new FragmentLoss("H56C34N2O25"), }, ID = 159, 
                 Structural = true, ShortName = "1K3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H56C34N2O25", Losses = new [] { new FragmentLoss("H56C34N2O25"), }, ID = 159, 
                 Structural = true, ShortName = "1K3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(2)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H73C45N3O33", Losses = new [] { new FragmentLoss("H73C45N3O33"), }, ID = 1455, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(2)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H90C56N4O41", Losses = new [] { new FragmentLoss("H90C56N4O41"), }, ID = 1712, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H64C39N2O29", Losses = new [] { new FragmentLoss("H64C39N2O29"), }, ID = 1451, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(2)Phos(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H57C34N2O28P", Losses = new [] { new FragmentLoss("H57C34N2O28P"), }, ID = 161, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H69C42N3O30", Losses = new [] { new FragmentLoss("H69C42N3O30"), }, ID = 1763, 
                 Structural = true, ShortName = "G0c", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H69C42N3O30", Losses = new [] { new FragmentLoss("H69C42N3O30"), }, ID = 1763, 
                 Structural = true, ShortName = "G0c", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H86C53N4O38", Losses = new [] { new FragmentLoss("H86C53N4O38"), }, ID = 1692, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3)NeuAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H86C53N4O41S", Losses = new [] { new FragmentLoss("H86C53N4O41S"), }, ID = 1711, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3)NeuAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H103C64N5O46", Losses = new [] { new FragmentLoss("H103C64N5O46"), }, ID = 1738, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3)NeuAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H103C64N5O49S", Losses = new [] { new FragmentLoss("H103C64N5O49S"), }, ID = 1745, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3)NeuAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H120C75N6O54", Losses = new [] { new FragmentLoss("H120C75N6O54"), }, ID = 1968, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H86C53N4O39", Losses = new [] { new FragmentLoss("H86C53N4O39"), }, ID = 1696, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3)NeuGc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H86C53N4O42S", Losses = new [] { new FragmentLoss("H86C53N4O42S"), }, ID = 1713, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H77C47N3O34", Losses = new [] { new FragmentLoss("H77C47N3O34"), }, ID = 1457, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H69C42N3O33S", Losses = new [] { new FragmentLoss("H69C42N3O33S"), }, ID = 1655, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H69C42N3O33S", Losses = new [] { new FragmentLoss("H69C42N3O33S"), }, ID = 1655, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H82C50N4O35", Losses = new [] { new FragmentLoss("H82C50N4O35"), }, ID = 309, 
                 Structural = true, ShortName = "1G0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H82C50N4O35", Losses = new [] { new FragmentLoss("H82C50N4O35"), }, ID = 309, 
                 Structural = true, ShortName = "1G0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(4)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H99C61N5O43", Losses = new [] { new FragmentLoss("H99C61N5O43"), }, ID = 1488, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(4)NeuAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H116C72N6O51", Losses = new [] { new FragmentLoss("H116C72N6O51"), }, ID = 1964, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(4)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H90C55N4O39", Losses = new [] { new FragmentLoss("H90C55N4O39"), }, ID = 1469, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(4)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H82C50N4O38S", Losses = new [] { new FragmentLoss("H82C50N4O38S"), }, ID = 1464, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(5) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H95C58N5O40", Losses = new [] { new FragmentLoss("H95C58N5O40"), }, ID = 1772, 
                 Structural = true, ShortName = "G0N", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H95C58N5O40", Losses = new [] { new FragmentLoss("H95C58N5O40"), }, ID = 1772, 
                 Structural = true, ShortName = "G0N", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(5)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H112C69N6O48", Losses = new [] { new FragmentLoss("H112C69N6O48"), }, ID = 1961, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(5)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H95C58N5O43S", Losses = new [] { new FragmentLoss("H95C58N5O43S"), }, ID = 1486, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(6) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H108C66N6O45", Losses = new [] { new FragmentLoss("H108C66N6O45"), }, ID = 1776, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(6) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H108C66N6O45", Losses = new [] { new FragmentLoss("H108C66N6O45"), }, ID = 1776, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(6)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H125C77N7O53", Losses = new [] { new FragmentLoss("H125C77N7O53"), }, ID = 1561, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(6)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H108C66N6O48S", Losses = new [] { new FragmentLoss("H108C66N6O48S"), }, ID = 1517, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(6)Sulf(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H108C66N6O51S2", Losses = new [] { new FragmentLoss("H108C66N6O51S2"), }, ID = 1530, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(7) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H121C74N7O50", Losses = new [] { new FragmentLoss("H121C74N7O50"), }, ID = 1540, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(3)HexNAc(7)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H121C74N7O53S", Losses = new [] { new FragmentLoss("H121C74N7O53S"), }, ID = 1558, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H40C24O20", Losses = new [] { new FragmentLoss("H40C24O20"), }, ID = 1448, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexA(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H48C30O26", Losses = new [] { new FragmentLoss("H48C30O26"), }, ID = 1938, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexA(1)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H61C38NO31", Losses = new [] { new FragmentLoss("H61C38NO31"), }, ID = 1945, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H53C32NO25", Losses = new [] { new FragmentLoss("H53C32NO25"), }, ID = 1599, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H53C32NO25", Losses = new [] { new FragmentLoss("H53C32NO25"), }, ID = 1599, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H66C40N2O30", Losses = new [] { new FragmentLoss("H66C40N2O30"), }, ID = 1452, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(2)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H83C51N3O38", Losses = new [] { new FragmentLoss("H83C51N3O38"), }, ID = 1461, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(2)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H83C51N3O38", Losses = new [] { new FragmentLoss("H83C51N3O38"), }, ID = 1461, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H74C45N2O34", Losses = new [] { new FragmentLoss("H74C45N2O34"), }, ID = 1456, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H79C48N3O35", Losses = new [] { new FragmentLoss("H79C48N3O35"), }, ID = 1769, 
                 Structural = true, ShortName = "G1c", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H79C48N3O35", Losses = new [] { new FragmentLoss("H79C48N3O35"), }, ID = 1769, 
                 Structural = true, ShortName = "G1c", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(3)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H96C59N4O43", Losses = new [] { new FragmentLoss("H96C59N4O43"), }, ID = 1773, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(3)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H96C59N4O43", Losses = new [] { new FragmentLoss("H96C59N4O43"), }, ID = 1773, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(3)NeuAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H113C70N5O51", Losses = new [] { new FragmentLoss("H113C70N5O51"), }, ID = 1524, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(3)NeuGc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H96C59N4O44", Losses = new [] { new FragmentLoss("H96C59N4O44"), }, ID = 1483, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(3)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H87C53N3O39", Losses = new [] { new FragmentLoss("H87C53N3O39"), }, ID = 1466, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H92C56N4O40", Losses = new [] { new FragmentLoss("H92C56N4O40"), }, ID = 310, 
                 Structural = true, ShortName = "1G1", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H92C56N4O40", Losses = new [] { new FragmentLoss("H92C56N4O40"), }, ID = 310, 
                 Structural = true, ShortName = "1G1", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4)Me(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H104C63N4O44", Losses = new [] { new FragmentLoss("H104C63N4O44"), }, ID = 1491, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H109C67N5O48", Losses = new [] { new FragmentLoss("H109C67N5O48"), }, ID = 1777, 
                 Structural = true, ShortName = "G1S", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H109C67N5O48", Losses = new [] { new FragmentLoss("H109C67N5O48"), }, ID = 1777, 
                 Structural = true, ShortName = "G1S", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4)NeuAc(1)Sulf(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H109C67N5O54S2", Losses = new [] { new FragmentLoss("H109C67N5O54S2"), }, ID = 1756, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4)NeuAc(1)Sulf(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H109C67N5O57S3", Losses = new [] { new FragmentLoss("H109C67N5O57S3"), }, ID = 1759, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4)NeuGc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H109C67N5O49", Losses = new [] { new FragmentLoss("H109C67N5O49"), }, ID = 1959, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H109C67N5O49", Losses = new [] { new FragmentLoss("H109C67N5O49"), }, ID = 1959, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4)NeuGc(1)Sulf(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H109C67N5O55S2", Losses = new [] { new FragmentLoss("H109C67N5O55S2"), }, ID = 1757, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H100C61N4O44", Losses = new [] { new FragmentLoss("H100C61N4O44"), }, ID = 1489, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H92C56N4O43S", Losses = new [] { new FragmentLoss("H92C56N4O43S"), }, ID = 1479, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(4)Sulf(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H92C56N4O46S2", Losses = new [] { new FragmentLoss("H92C56N4O46S2"), }, ID = 1732, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(5) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H105C64N5O45", Losses = new [] { new FragmentLoss("H105C64N5O45"), }, ID = 1496, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(5)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H122C75N6O53", Losses = new [] { new FragmentLoss("H122C75N6O53"), }, ID = 1551, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(5)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H105C64N5O48S", Losses = new [] { new FragmentLoss("H105C64N5O48S"), }, ID = 1513, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)HexNAc(6) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H118C72N6O50", Losses = new [] { new FragmentLoss("H118C72N6O50"), }, ID = 1532, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(4)Phos(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H41C24O23P", Losses = new [] { new FragmentLoss("H41C24O23P"), }, ID = 1575, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H50C30O25", Losses = new [] { new FragmentLoss("H50C30O25"), }, ID = 1590, 
                 Structural = true, ShortName = "5Hx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexA(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H58C36O31", Losses = new [] { new FragmentLoss("H58C36O31"), }, ID = 1944, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H63C38NO30", Losses = new [] { new FragmentLoss("H63C38NO30"), }, ID = 1627, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H63C38NO30", Losses = new [] { new FragmentLoss("H63C38NO30"), }, ID = 1627, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H76C46N2O35", Losses = new [] { new FragmentLoss("H76C46N2O35"), }, ID = 137, 
                 Structural = true, ShortName = "G2d", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(2)Phos(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H77C46N2O38P", Losses = new [] { new FragmentLoss("H77C46N2O38P"), }, ID = 1458, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H89C54N3O40", Losses = new [] { new FragmentLoss("H89C54N3O40"), }, ID = 1468, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(3)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H97C59N3O44", Losses = new [] { new FragmentLoss("H97C59N3O44"), }, ID = 1482, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O45", Losses = new [] { new FragmentLoss("H102C62N4O45"), }, ID = 311, 
                 Structural = true, ShortName = "1G2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O45", Losses = new [] { new FragmentLoss("H102C62N4O45"), }, ID = 311, 
                 Structural = true, ShortName = "1G2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(4)Me(2)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H114C69N4O49", Losses = new [] { new FragmentLoss("H114C69N4O49"), }, ID = 1516, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(4)NeuAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H119C73N5O53", Losses = new [] { new FragmentLoss("H119C73N5O53"), }, ID = 1409, 
                 Structural = true, ShortName = "G2S", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(4)NeuAc(1)Ac(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H121C75N5O54", Losses = new [] { new FragmentLoss("H121C75N5O54"), }, ID = 1967, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(4)NeuAc(1)Ac(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H123C77N5O55", Losses = new [] { new FragmentLoss("H123C77N5O55"), }, ID = 1969, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(4)NeuAc(1)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H119C73N5O56S", Losses = new [] { new FragmentLoss("H119C73N5O56S"), }, ID = 1560, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(4)NeuAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H136C84N6O61", Losses = new [] { new FragmentLoss("H136C84N6O61"), }, ID = 1408, 
                 Structural = true, ShortName = "G2W", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(4)NeuGc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H119C73N5O54", Losses = new [] { new FragmentLoss("H119C73N5O54"), }, ID = 1545, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(4)Sulf(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H102C62N4O48S", Losses = new [] { new FragmentLoss("H102C62N4O48S"), }, ID = 1503, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(5) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H115C70N5O50", Losses = new [] { new FragmentLoss("H115C70N5O50"), }, ID = 1780, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)HexNAc(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H115C70N5O50", Losses = new [] { new FragmentLoss("H115C70N5O50"), }, ID = 1780, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)Phos(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H51C30O28P", Losses = new [] { new FragmentLoss("H51C30O28P"), }, ID = 1604, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(5)Phos(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H53C30O34P3", Losses = new [] { new FragmentLoss("H53C30O34P3"), }, ID = 1632, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)HexNAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H73C44NO35", Losses = new [] { new FragmentLoss("H73C44NO35"), }, ID = 1947, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H86C52N2O40", Losses = new [] { new FragmentLoss("H86C52N2O40"), }, ID = 1465, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)HexNAc(2)Phos(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H87C52N2O43P", Losses = new [] { new FragmentLoss("H87C52N2O43P"), }, ID = 1470, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H99C60N3O45", Losses = new [] { new FragmentLoss("H99C60N3O45"), }, ID = 1487, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)HexNAc(3)Phos(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H100C60N3O48P", Losses = new [] { new FragmentLoss("H100C60N3O48P"), }, ID = 1495, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H112C68N4O50", Losses = new [] { new FragmentLoss("H112C68N4O50"), }, ID = 1779, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H112C68N4O50", Losses = new [] { new FragmentLoss("H112C68N4O50"), }, ID = 1779, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)HexNAc(4)Me(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H118C71N4O50", Losses = new [] { new FragmentLoss("H118C71N4O50"), }, ID = 1522, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)HexNAc(4)Me(3)Pent(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H126C76N4O54", Losses = new [] { new FragmentLoss("H126C76N4O54"), }, ID = 1552, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)HexNAc(5) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H125C76N5O55", Losses = new [] { new FragmentLoss("H125C76N5O55"), }, ID = 1559, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)HexNAc(5)NeuAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H176C109N8O79", Losses = new [] { new FragmentLoss("H176C109N8O79"), }, ID = 2028, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)Phos(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H61C36O33P", Losses = new [] { new FragmentLoss("H61C36O33P"), }, ID = 1633, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(6)Phos(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H63C36O39P3", Losses = new [] { new FragmentLoss("H63C36O39P3"), }, ID = 1659, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(7)HexNAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H83C50NO40", Losses = new [] { new FragmentLoss("H83C50NO40"), }, ID = 1460, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(7)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H96C58N2O45", Losses = new [] { new FragmentLoss("H96C58N2O45"), }, ID = 1480, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(7)HexNAc(2)Phos(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H97C58N2O48P", Losses = new [] { new FragmentLoss("H97C58N2O48P"), }, ID = 1490, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(7)HexNAc(2)Phos(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H98C58N2O51P2", Losses = new [] { new FragmentLoss("H98C58N2O51P2"), }, ID = 1502, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(7)HexNAc(3) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H109C66N3O50", Losses = new [] { new FragmentLoss("H109C66N3O50"), }, ID = 1514, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(7)HexNAc(3)Phos(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H110C66N3O53P", Losses = new [] { new FragmentLoss("H110C66N3O53P"), }, ID = 1521, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(7)HexNAc(4) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H122C74N4O55", Losses = new [] { new FragmentLoss("H122C74N4O55"), }, ID = 1549, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(7)HexNAc(6) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H148C90N6O65", Losses = new [] { new FragmentLoss("H148C90N6O65"), }, ID = 2029, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(7)HexNAc(6) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H148C90N6O65", Losses = new [] { new FragmentLoss("H148C90N6O65"), }, ID = 2029, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(7)Phos(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H73C42O44P3", Losses = new [] { new FragmentLoss("H73C42O44P3"), }, ID = 1690, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(8)HexNAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H93C56NO45", Losses = new [] { new FragmentLoss("H93C56NO45"), }, ID = 1473, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(8)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H106C64N2O50", Losses = new [] { new FragmentLoss("H106C64N2O50"), }, ID = 1504, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(8)Phos(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H83C48O49P3", Losses = new [] { new FragmentLoss("H83C48O49P3"), }, ID = 1723, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(9) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H90C54O45", Losses = new [] { new FragmentLoss("H90C54O45"), }, ID = 1953, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(9)HexNAc(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H103C62NO50", Losses = new [] { new FragmentLoss("H103C62NO50"), }, ID = 1957, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(9)HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H116C70N2O55", Losses = new [] { new FragmentLoss("H116C70N2O55"), }, ID = 1531, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hex(9)Phos(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H93C54O54P3", Losses = new [] { new FragmentLoss("H93C54O54P3"), }, ID = 1742, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexA(2)HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H55C36N3O27", Losses = new [] { new FragmentLoss("H55C36N3O27"), }, ID = 1942, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexN (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H11C6NO4", ID = 454, 
                 Structural = true, ShortName = "Hmn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexN (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H11C6NO4", Losses = new [] { new FragmentLoss("H11C6NO4"), }, ID = 454, 
                 Structural = true, ShortName = "Hmn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexN (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H11C6NO4", Losses = new [] { new FragmentLoss("H11C6NO4"), }, ID = 454, 
                 Structural = true, ShortName = "Hmn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexN (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H11C6NO4", ID = 454, 
                 Structural = true, ShortName = "Hmn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H13C8NO5", Losses = new [] { new FragmentLoss("H13C8NO5"), }, ID = 43, 
                 Structural = true, ShortName = "HNc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H13C8NO5", Losses = new [] { new FragmentLoss("H13C8NO5"), }, ID = 43, 
                 Structural = true, ShortName = "HNc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H13C8NO5", Losses = new [] { new FragmentLoss("H13C8NO5"), }, ID = 43, 
                 Structural = true, ShortName = "HNc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(1)dHex(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H23C14NO9", Losses = new [] { new FragmentLoss("H23C14NO9"), }, ID = 142, 
                 Structural = true, ShortName = "1NF", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(1)dHex(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H23C14NO9", Losses = new [] { new FragmentLoss("H23C14NO9"), }, ID = 142, 
                 Structural = true, ShortName = "1NF", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(1)dHex(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H33C20NO13", Losses = new [] { new FragmentLoss("H33C20NO13"), }, ID = 145, 
                 Structural = true, ShortName = "NF2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(1)Kdn(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H41C26NO21", Losses = new [] { new FragmentLoss("H41C26NO21"), }, ID = 1570, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(1)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H30C19N2O13", Losses = new [] { new FragmentLoss("H30C19N2O13"), }, ID = 1434, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(1)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H30C19N2O14", Losses = new [] { new FragmentLoss("H30C19N2O14"), }, ID = 1435, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(1)NeuGc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H47C30N3O23", Losses = new [] { new FragmentLoss("H47C30N3O23"), }, ID = 1592, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H26C16N2O10", Losses = new [] { new FragmentLoss("H26C16N2O10"), }, ID = 143, 
                 Structural = true, ShortName = "1HN", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H26C16N2O10", Losses = new [] { new FragmentLoss("H26C16N2O10"), }, ID = 143, 
                 Structural = true, ShortName = "1HN", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(2)dHex(1) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H36C22N2O14", Losses = new [] { new FragmentLoss("H36C22N2O14"), }, ID = 147, 
                 Structural = true, ShortName = "2NF", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(2)dHex(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H46C28N2O18", Losses = new [] { new FragmentLoss("H46C28N2O18"), }, ID = 150, 
                 Structural = true, ShortName = "NFX", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(2)NeuAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H43C27N3O18", Losses = new [] { new FragmentLoss("H43C27N3O18"), }, ID = 1568, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(2)NeuAc(1)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H43C27N3O21S", Losses = new [] { new FragmentLoss("H43C27N3O21S"), }, ID = 1583, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(2)NeuGc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H43C27N3O19", Losses = new [] { new FragmentLoss("H43C27N3O19"), }, ID = 1573, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(2)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H26C16N2O13S", Losses = new [] { new FragmentLoss("H26C16N2O13S"), }, ID = 1934, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(3) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H39C24N3O15", Losses = new [] { new FragmentLoss("H39C24N3O15"), }, ID = 1433, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(3)Sulf(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H39C24N3O18S", Losses = new [] { new FragmentLoss("H39C24N3O18S"), }, ID = 1565, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(4) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H52C32N4O20", Losses = new [] { new FragmentLoss("H52C32N4O20"), }, ID = 1591, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HexNAc(5) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H65C40N5O25", Losses = new [] { new FragmentLoss("H65C40N5O25"), }, ID = 1628, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "His+O(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H7C6N3O3", ID = 2027, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HMVK (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C4O2", ID = 371, 
                 Structural = true, ShortName = "HMV", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HN2_mustard (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H11C5NO", ID = 1388, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HN2_mustard (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H11C5NO", ID = 1388, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HN2_mustard (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H11C5NO", ID = 1388, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HN3_mustard (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H13C6NO2", ID = 1389, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HN3_mustard (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H13C6NO2", ID = 1389, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HN3_mustard (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H13C6NO2", ID = 1389, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE (A)", 
                 AAs = "A", LabelAtoms = LabelAtoms.None, Formula = "H16C9O2", ID = 53, 
                 Structural = true, ShortName = "HNE", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H16C9O2", ID = 53, 
                 Structural = true, ShortName = "HNE", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H16C9O2", ID = 53, 
                 Structural = true, ShortName = "HNE", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H16C9O2", ID = 53, 
                 Structural = true, ShortName = "HNE", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.None, Formula = "H16C9O2", ID = 53, 
                 Structural = true, ShortName = "HNE", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE+Delta:H(2) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H18C9O2", ID = 335, 
                 Structural = true, ShortName = "HN2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE+Delta:H(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H18C9O2", ID = 335, 
                 Structural = true, ShortName = "HN2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HNE+Delta:H(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H18C9O2", ID = 335, 
                 Structural = true, ShortName = "HN2", Hidden = true, 
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
                 Name = "Homocysteic_acid (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "O3 - H2C", ID = 1384, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "HPG (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H4C8O2", ID = 186, 
                 Structural = true, ShortName = "HPG", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hydroxamic_acid (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "HN", ID = 1385, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hydroxycinnamyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C9O2", ID = 407, 
                 Structural = true, ShortName = "hCn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hydroxyfarnesyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H24C15O", ID = 376, 
                 Structural = true, ShortName = "HFr", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hydroxyheme (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H30C34N4O4Fe", ID = 436, 
                 Structural = true, ShortName = "hHm", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "hydroxyisobutyryl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C4O2", ID = 1849, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hydroxymethyl (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H2CO", ID = 414, 
                 Structural = true, ShortName = "h1M", Hidden = true, 
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
                 Structural = true, ShortName = "h3M", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Hypusine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C4NO", ID = 379, 
                 Structural = true, ShortName = "Hps", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IASD (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H16C18N2O8S2", ID = 1832, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IBTP (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H21C22P", ID = 119, 
                 Structural = true, ShortName = "BTP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-D (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H34C20N4O5S", ID = 13, 
                 Structural = true, ShortName = "d0I", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-D:2H(8) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H26H'8C20N4O5S", ID = 12, 
                 Structural = true, ShortName = "d8I", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-G (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C22H38N4O6S", ID = 8, 
                 Structural = true, ShortName = "GG0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-H (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C15ClH20NO6", ID = 123, 
                 Structural = true, ShortName = "GH0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICDID (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C8H10O2", ID = 1018, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICPL (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C6H3NO", ID = 365, 
                 Structural = true, ShortName = "IP0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:13C(6) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H3C'6NO", ID = 364, 
                 Structural = true, ShortName = "IP6", Hidden = true, 
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
                 Structural = true, ShortName = "IEB", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IGBP (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "BrC12H13N2O2", ID = 243, 
                 Structural = true, ShortName = "ID0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IMEHex(2)NeuAc(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H40C25N2O18S", ID = 1286, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IMID (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C3H4N2", ID = 94, 
                 Structural = true, ShortName = "IM0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iminobiotin (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C10N3OS", ID = 89, 
                 Structural = true, ShortName = "ImB", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iminobiotin (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H15C10N3OS", ID = 89, 
                 Structural = true, ShortName = "ImB", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iodo (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "I - H", ID = 129, 
                 Structural = true, ShortName = "Iod", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iodo (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "I - H", ID = 129, 
                 Structural = true, ShortName = "Iod", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iodoacetanilide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C8H7NO", ID = 1397, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iodoacetanilide (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C8H7NO", ID = 1397, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iodoacetanilide (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C8H7NO", ID = 1397, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iodoTMT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H28C16N4O3", ID = 1341, 
                 Structural = true, ShortName = "iTM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iodoTMT (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H28C16N4O3", ID = 1341, 
                 Structural = true, ShortName = "iTM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iodoTMT (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H28C16N4O3", ID = 1341, 
                 Structural = true, ShortName = "iTM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iodoTMT (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H28C16N4O3", ID = 1341, 
                 Structural = true, ShortName = "iTM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iodoTMT (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H28C16N4O3", ID = 1341, 
                 Structural = true, ShortName = "iTM", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iodoTMT6plex (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H28C12C'4N3N'O3", ID = 1342, 
                 Structural = true, ShortName = "iT6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iodoTMT6plex (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H28C12C'4N3N'O3", ID = 1342, 
                 Structural = true, ShortName = "iT6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iodoTMT6plex (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H28C12C'4N3N'O3", ID = 1342, 
                 Structural = true, ShortName = "iT6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iodoTMT6plex (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H28C12C'4N3N'O3", ID = 1342, 
                 Structural = true, ShortName = "iT6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iodoTMT6plex (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H28C12C'4N3N'O3", ID = 1342, 
                 Structural = true, ShortName = "iT6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IodoU-AMP (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "H11C9N2O9P", ID = 292, 
                 Structural = true, ShortName = "IUP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IodoU-AMP (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H11C9N2O9P", ID = 292, 
                 Structural = true, ShortName = "IUP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IodoU-AMP (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H11C9N2O9P", ID = 292, 
                 Structural = true, ShortName = "IUP", Hidden = true, 
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
                 Name = "iTRAQ4plex (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 214, 
                 Structural = true, ShortName = "IT4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 214, 
                 Structural = true, ShortName = "IT4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 214, 
                 Structural = true, ShortName = "IT4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H12C4C'3NN'O", ID = 214, 
                 Structural = true, ShortName = "IT4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ4plex114 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H12C5C'2N2O'", ID = 532, 
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
                 Name = "iTRAQ4plex115 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H12C6C'NN'O'", ID = 533, 
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
                 Name = "iTRAQ8plex (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C'7N'C7H24N3O3", ID = 730, 
                 Structural = true, ShortName = "IT8", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "C'7N'C7H24N3O3", ID = 730, 
                 Structural = true, ShortName = "IT8", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "C'7N'C7H24N3O3", ID = 730, 
                 Structural = true, ShortName = "IT8", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "C'7N'C7H24N3O3", ID = 730, 
                 Structural = true, ShortName = "IT8", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex:13C(6)15N(2) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C'6N'2C8H24N2O3", ID = 731, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex:13C(6)15N(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'6N'2C8H24N2O3", ID = 731, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex:13C(6)15N(2) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'6N'2C8H24N2O3", ID = 731, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "iTRAQ8plex:13C(6)15N(2) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "C'6N'2C8H24N2O3", ID = 731, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "L-Gln (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H8C5N2O2", ID = 2070, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "L-Gln (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H8C5N2O2", ID = 2070, 
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
                 Name = "Label:13C(4)+Oxidation (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "C'4O - C4", ID = 1267, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(4)15N(2)+GG (K)", 
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
                 Name = "Label:13C(6)+GG (K)", 
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
                 Name = "Label:13C(6)15N(2)+GG (K)", 
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
                 Structural = true, ShortName = "Ph9", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(3)+Oxidation (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H'3O - H3", ID = 1370, 
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
                 Name = "Label:2H(4)+GG (K)", 
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
                 Name = "LG-pyrrole (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H28C20O3", ID = 947, 
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
                 Structural = true, ShortName = "Lip", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LRGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H29C16N7O4", ID = 535, 
                 Structural = true, ShortName = "Umc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LRGG+dimethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H33C18N7O4", ID = 1829, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LRGG+methyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H31C17N7O4", ID = 1828, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "LTX+Lophotoxin (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H24C22O8", ID = 2039, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lys (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H12C6N2O", ID = 1301, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lys+O(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H12C6N2O3", ID = 2036, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lys->Allysine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "O - H3N", ID = 352, 
                 Structural = true, ShortName = "LAA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lys->AminoadipicAcid (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "O2 - H3N", ID = 381, 
                 Structural = true, ShortName = "AAA", Hidden = true, 
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
                 Name = "Lys-loss (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "-H12C6N2O", ID = 313, 
                 Structural = true, ShortName = "-1K", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Lys-loss (Protein C-term K)", 
                 AAs = "K", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "-H12C6N2O", ID = 313, 
                 Structural = true, ShortName = "-1K", Hidden = true, 
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
                 Structural = true, ShortName = "Mal", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Malonyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C3O3", ID = 747, 
                 Structural = true, ShortName = "Mal", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Malonyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H2C3O3", ID = 747, 
                 Structural = true, ShortName = "Mal", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MBS+peptide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H108C81N7O19", ID = 2040, 
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
                 Name = "MeMePhosphorothioate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H5C2OPS", ID = 1868, 
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
                 Name = "MesitylOxide (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H10C6O", ID = 1873, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MesitylOxide (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10C6O", ID = 1873, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Met+O(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H9C5NO3S", ID = 2033, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Met->Aha (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "N3 - H3CS", ID = 896, 
                 Structural = true, ShortName = "MAH", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Met->AspSA (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "O - H4CS", ID = 1914, 
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
                 Name = "Methamidophos-O (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4CNO2P", ID = 2008, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methamidophos-O (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H4CNO2P", ID = 2008, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methamidophos-O (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4CNO2P", ID = 2008, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methamidophos-O (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4CNO2P", ID = 2008, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methamidophos-O (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H4CNO2P", ID = 2008, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methamidophos-O (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H4CNO2P", ID = 2008, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methamidophos-S (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4CNOPS", ID = 2007, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methamidophos-S (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H4CNOPS", ID = 2007, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methamidophos-S (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4CNOPS", ID = 2007, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methamidophos-S (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4CNOPS", ID = 2007, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methamidophos-S (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H4CNOPS", ID = 2007, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methamidophos-S (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H4CNOPS", ID = 2007, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (I)", 
                 AAs = "I", LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "CH2", ID = 34, 
                 Structural = true, ShortName = "1Me", Hidden = true, 
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
                 Structural = true, ShortName = "MDe", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl+Deamidated (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "HCO - N", ID = 528, 
                 Structural = true, ShortName = "MDe", Hidden = true, 
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
                 Structural = true, ShortName = "M+2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(2) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'2C", ID = 284, 
                 Structural = true, ShortName = "M+2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3)+Acetyl:2H(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'6C3O - H2", ID = 1368, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylamine (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H3CN - O", ID = 337, 
                 Structural = true, ShortName = "MeA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylamine (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H3CN - O", ID = 337, 
                 Structural = true, ShortName = "MeA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylmalonylation (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H4C4O3", ID = 914, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "methylol (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2CO", ID = 1875, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "methylol (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H2CO", ID = 1875, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "methylol (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H2CO", ID = 1875, 
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
                 Structural = true, ShortName = "MPr", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "methylsulfonylethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C3O2S", ID = 1380, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "methylsulfonylethyl (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H6C3O2S", ID = 1380, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "methylsulfonylethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C3O2S", ID = 1380, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylthio (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H2CS", ID = 39, 
                 Structural = true, ShortName = "MSH", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylthio (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2CS", ID = 39, 
                 Structural = true, ShortName = "MSH", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylthio (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H2CS", ID = 39, 
                 Structural = true, ShortName = "MSH", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methylthio (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H2CS", ID = 39, 
                 Structural = true, ShortName = "MSH", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MG-H1 (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H2C3O", ID = 859, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MM-diphenylpentanone (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H19C18NO", ID = 1315, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Molybdopterin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H11C10N5O8PS2Mo", ID = 391, 
                 Structural = true, ShortName = "Mdt", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MolybdopterinGD (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H47C40N20O26P4S4Mo", ID = 424, 
                 Structural = true, ShortName = "MGD", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MolybdopterinGD (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H47C40N20O26P4S4Mo", ID = 424, 
                 Structural = true, ShortName = "MGD", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MolybdopterinGD (U)", 
                 AAs = "U", LabelAtoms = LabelAtoms.None, Formula = "H47C40N20O26P4S4Mo", ID = 424, 
                 Structural = true, ShortName = "MGD", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MolybdopterinGD+Delta:S(-1)Se(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H47C40N20O26P4S3SeMo", ID = 415, 
                 Structural = true, ShortName = "MtD", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "monomethylphosphothione (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H3CO2PS", ID = 1989, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "monomethylphosphothione (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H3CO2PS", ID = 1989, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "monomethylphosphothione (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3CO2PS", ID = 1989, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "monomethylphosphothione (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H3CO2PS", ID = 1989, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "monomethylphosphothione (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H3CO2PS", ID = 1989, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "monomethylphosphothione (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H3CO2PS", ID = 1989, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "C7H12N2O", ID = 888, 
                 Structural = true, ShortName = "M00", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "C7H12N2O", ID = 888, 
                 Structural = true, ShortName = "M00", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "C7H12N2O", ID = 888, 
                 Structural = true, ShortName = "M00", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MTSL (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H14C9NOS", ID = 911, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "MurNAc (A)", 
                 AAs = "A", LabelAtoms = LabelAtoms.None, Formula = "H17C11NO7", ID = 1400, 
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
                 Structural = true, ShortName = "Myr", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Myristoyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H26C14O", ID = 45, 
                 Structural = true, ShortName = "Myr", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Myristoyl (N-term G)", 
                 AAs = "G", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H26C14O", ID = 45, 
                 Structural = true, ShortName = "Myr", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Myristoyl+Delta:H(-4) (Protein N-term G)", 
                 AAs = "G", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H22C14O", ID = 135, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "N-dimethylphosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H6C2NO2P", ID = 1365, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "N6pAMP (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H14C13N5O6P", ID = 2073, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "N6pAMP (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H14C13N5O6P", ID = 2073, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "N6pAMP (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H14C13N5O6P", ID = 2073, 
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
                 Name = "NBF (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "HC6N3O3", ID = 2079, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NBF (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "HC6N3O3", ID = 2079, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NBF (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "HC6N3O3", ID = 2079, 
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
                 Structural = true, ShortName = "eI0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEIAA (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H7C4NO", ID = 211, 
                 Structural = true, ShortName = "eI0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEIAA:2H(5) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2H'5C4NO", ID = 212, 
                 Structural = true, ShortName = "eI5", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEIAA:2H(5) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H2H'5C4NO", ID = 212, 
                 Structural = true, ShortName = "eI5", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEM:2H(5) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H2H'5C6NO2", ID = 776, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEM:2H(5)+H2O (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4H'5C6NO3", ID = 1358, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEMsulfur (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H7C6NO2S", ID = 1326, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NEMsulfurWater (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C6NO3S", ID = 1328, 
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
                 Name = "NeuAc (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H17C11NO8", Losses = new [] { new FragmentLoss("H17C11NO8"), }, ID = 1303, 
                 Structural = true, ShortName = "NAc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NeuAc (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H17C11NO8", Losses = new [] { new FragmentLoss("H17C11NO8"), }, ID = 1303, 
                 Structural = true, ShortName = "NAc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NeuGc (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H17C11NO9", Losses = new [] { new FragmentLoss("H17C11NO9"), }, ID = 1304, 
                 Structural = true, ShortName = "NGc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NeuGc (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H17C11NO9", Losses = new [] { new FragmentLoss("H17C11NO9"), }, ID = 1304, 
                 Structural = true, ShortName = "NGc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NHS-fluorescein (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H21C27NO7", ID = 1391, 
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
                 Name = "NIC (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3C6NO", ID = 697, 
                 Structural = true, ShortName = "Ncl", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NIC (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H3C6NO", ID = 697, 
                 Structural = true, ShortName = "Ncl", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nitrene (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "N - H", ID = 2014, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nitro (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "NO2 - H", ID = 354, 
                 Structural = true, ShortName = "Ntr", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nitro (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "NO2 - H", ID = 354, 
                 Structural = true, ShortName = "Ntr", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nitro (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "NO2 - H", ID = 354, 
                 Structural = true, ShortName = "Ntr", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nitrosyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "NO - H", ID = 275, 
                 Structural = true, ShortName = "Nsl", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Nitrosyl (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "NO - H", ID = 275, 
                 Structural = true, ShortName = "Nsl", Hidden = true, 
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
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H9C10N3O3S", ID = 744, 
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
                 Name = "NP40 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H24C15O", ID = 1833, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NQIGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H31C19N7O7", ID = 1799, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "NQTGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H27C17N7O8", ID = 2084, 
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
                 Name = "O-Et-N-diMePhospho (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H10C4NO2P", ID = 1364, 
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
                 Name = "Octanoyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H14C8O", ID = 426, 
                 Structural = true, ShortName = "Oct", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Octanoyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H14C8O", ID = 426, 
                 Structural = true, ShortName = "Oct", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Octanoyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H14C8O", ID = 426, 
                 Structural = true, ShortName = "Oct", Hidden = true, 
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
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (C-term G)", 
                 AAs = "G", Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (I)", 
                 AAs = "I", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (U)", 
                 AAs = "U", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (V)", 
                 AAs = "V", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "O", ID = 35, 
                 Structural = true, ShortName = "Oxi", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Oxidation+NEM (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H7C6NO3", ID = 1390, 
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
                 Structural = true, ShortName = "Pty", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoleyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H28C16O", ID = 431, 
                 Structural = true, ShortName = "Pty", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoleyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H28C16O", ID = 431, 
                 Structural = true, ShortName = "Pty", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H30C16O", ID = 47, 
                 Structural = true, ShortName = "Pal", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H30C16O", ID = 47, 
                 Structural = true, ShortName = "Pal", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H30C16O", ID = 47, 
                 Structural = true, ShortName = "Pal", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Palmitoyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H30C16O", ID = 47, 
                 Structural = true, ShortName = "Pal", Hidden = true, 
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
                 Name = "Pent(1)HexNAc(1) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H21C13NO9", Losses = new [] { new FragmentLoss("H21C13NO9"), }, ID = 1931, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pent(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H16C10O8", Losses = new [] { new FragmentLoss("H16C10O8"), }, ID = 1930, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pentose (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H8C5O4", Losses = new [] { new FragmentLoss("H8C5O4"), }, ID = 1425, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pentylamine (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H10C5", ID = 801, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PEO-Iodoacetyl-LC-Biotin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H30C18N4O5S", ID = 20, 
                 Structural = true, ShortName = "PEO", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PET (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H7C7NS - O", ID = 264, 
                 Structural = true, ShortName = "PET", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PET (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H7C7NS - O", ID = 264, 
                 Structural = true, ShortName = "PET", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phe->CamCys (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "NOS - HC4", ID = 904, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "phenyl-phosphate (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C6O3P", ID = 2042, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "phenyl-phosphate (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H5C6O3P", ID = 2042, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "phenyl-phosphate (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H5C6O3P", ID = 2042, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "phenyl-phosphate (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H5C6O3P", ID = 2042, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phenylisocyanate (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C7H5NO", ID = 411, 
                 Structural = true, ShortName = "Pc0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "phenylsulfonylethyl (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H8C8O2S", ID = 1382, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, ShortName = "Pho", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, ShortName = "Pho", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, ShortName = "Pho", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, ShortName = "Pho", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, ShortName = "Pho", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phospho (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "HO3P", ID = 21, 
                 Structural = true, ShortName = "Pho", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoadenosine (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O6P", ID = 405, 
                 Structural = true, ShortName = "AMP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoadenosine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O6P", ID = 405, 
                 Structural = true, ShortName = "AMP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoadenosine (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O6P", Losses = new [] { new FragmentLoss("H14C10N5O7P"), }, ID = 405, 
                 Structural = true, ShortName = "AMP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoadenosine (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O6P", Losses = new [] { new FragmentLoss("H14C10N5O7P"), }, ID = 405, 
                 Structural = true, ShortName = "AMP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoadenosine (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O6P", Losses = new [] { new FragmentLoss("H11C10N5O3"), new FragmentLoss("H5C5N5"), }, ID = 405, 
                 Structural = true, ShortName = "AMP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoCytidine (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H12C9N3O7P", ID = 1843, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoCytidine (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H12C9N3O7P", ID = 1843, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoCytidine (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C9N3O7P", ID = 1843, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphogluconoylation (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H11C6O9P", ID = 1344, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphogluconoylation (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H11C6O9P", ID = 1344, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoguanosine (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O7P", ID = 413, 
                 Structural = true, ShortName = "GMP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphoguanosine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C10N5O7P", ID = 413, 
                 Structural = true, ShortName = "GMP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoHex (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H11C6O8P", Losses = new [] { new FragmentLoss("H11C6O8P"), }, ID = 429, 
                 Structural = true, ShortName = "pHx", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoHex(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H21C12O13P", Losses = new [] { new FragmentLoss("H21C12O13P"), }, ID = 1413, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoHex(2) (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H21C12O13P", Losses = new [] { new FragmentLoss("H21C12O13P"), }, ID = 1413, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoHexNAc (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H14C8NO8P", Losses = new [] { new FragmentLoss("H14C8NO8P"), }, ID = 428, 
                 Structural = true, ShortName = "pHc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phosphopantetheine (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H21C11N2O6PS", ID = 49, 
                 Structural = true, ShortName = "PPE", Hidden = true, 
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
                 Name = "phosphoRibosyl (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H9C5O7P", ID = 1356, 
                 Structural = true, ShortName = "pRb", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "phosphoRibosyl (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H9C5O7P", ID = 1356, 
                 Structural = true, ShortName = "pRb", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "phosphoRibosyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H9C5O7P", ID = 1356, 
                 Structural = true, ShortName = "pRb", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoribosyldephosphoCoA (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H42C26N7O19P3S", ID = 395, 
                 Structural = true, ShortName = "PRC", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoUridine (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H11C9N2O8P", ID = 417, 
                 Structural = true, ShortName = "UMP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PhosphoUridine (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H11C9N2O8P", ID = 417, 
                 Structural = true, ShortName = "UMP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phycocyanobilin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H38C33N4O6", ID = 387, 
                 Structural = true, ShortName = "pcb", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phycoerythrobilin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H40C33N4O6", ID = 388, 
                 Structural = true, ShortName = "pct", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phytochromobilin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H36C33N4O6", ID = 389, 
                 Structural = true, ShortName = "pcm", Hidden = true, 
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
                 Name = "pRBS-ID_4-thiouridine (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "H10C9N2O5", Losses = new [] { new FragmentLoss("H8C5O4"), }, ID = 2054, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "pRBS-ID_6-thioguanosine (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H11C10N5O4", Losses = new [] { new FragmentLoss("H8C5O4"), }, ID = 2055, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pro+O(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H7C5NO3", ID = 2035, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pro->HAVA (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H2O", ID = 1922, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pro->pyro-Glu (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 359, 
                 Structural = true, ShortName = "PGP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pro->Pyrrolidinone (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "-H2CO", ID = 360, 
                 Structural = true, ShortName = "PYD", Hidden = true, 
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
                 Name = "Propionamide (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C3H5NO", ID = 24, 
                 Structural = true, ShortName = "PPa", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionamide (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C3H5NO", ID = 24, 
                 Structural = true, ShortName = "PPa", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C3H4O", ID = 58, 
                 Structural = true, ShortName = "Poy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C3H4O", ID = 58, 
                 Structural = true, ShortName = "Poy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionyl (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "C3H4O", ID = 58, 
                 Structural = true, ShortName = "Poy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionyl (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "C3H4O", ID = 58, 
                 Structural = true, ShortName = "Poy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propiophenone (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H8C9O", ID = 1310, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propiophenone (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H8C9O", ID = 1310, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propiophenone (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C9O", ID = 1310, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propiophenone (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H8C9O", ID = 1310, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propiophenone (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H8C9O", ID = 1310, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propiophenone (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H8C9O", ID = 1310, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propiophenone (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "H8C9O", ID = 1310, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propyl (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "C3H6", ID = 1305, 
                 Structural = true, ShortName = "Prp", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propyl (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "C3H6", ID = 1305, 
                 Structural = true, ShortName = "Prp", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propyl (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "C3H6", ID = 1305, 
                 Structural = true, ShortName = "Prp", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C3H6", ID = 1305, 
                 Structural = true, ShortName = "Prp", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C3H6", ID = 1305, 
                 Structural = true, ShortName = "Prp", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propyl:2H(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'6C3", ID = 1306, 
                 Structural = true, ShortName = "Pr6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propyl:2H(6) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'6C3", ID = 1306, 
                 Structural = true, ShortName = "Pr6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PropylNAGthiazoline (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H14C9NO4S", ID = 514, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PS_Hapten (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H4C7O2", ID = 1345, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PS_Hapten (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H4C7O2", ID = 1345, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PS_Hapten (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C7O2", ID = 1345, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "pupylation (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H13C9N3O5", ID = 1264, 
                 Structural = true, ShortName = "Pup", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Puromycin (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H27C22N7O4", ID = 973, 
                 Structural = true, ShortName = "Pmn", Hidden = true, 
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
                 Structural = true, ShortName = "PyP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PyridoxalPhosphateH2 (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10C8NO5P", ID = 1383, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pyridylacetyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C7NO", ID = 25, 
                 Structural = true, ShortName = "PyA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pyridylacetyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H5C7NO", ID = 25, 
                 Structural = true, ShortName = "PyA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Pyro-QQTGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H26C18N6O8", ID = 2083, 
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
                 Structural = true, ShortName = "PAI", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PyruvicAcidIminyl (Protein N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H2C3O2", ID = 422, 
                 Structural = true, ShortName = "PAI", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "PyruvicAcidIminyl (Protein N-term V)", 
                 AAs = "V", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H2C3O2", ID = 422, 
                 Structural = true, ShortName = "PAI", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "QAT (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C9H19N2O", ID = 195, 
                 Structural = true, ShortName = "QT0", Hidden = true, 
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
                 Structural = true, ShortName = "SU2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "QQTGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H29C18N7O8", ID = 2082, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "QTGG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H21C13N5O6", ID = 1293, 
                 Structural = true, ShortName = "SU1", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Quinone (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O2 - H2", ID = 392, 
                 Structural = true, ShortName = "Qin", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Quinone (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "O2 - H2", ID = 392, 
                 Structural = true, ShortName = "Qin", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "RBS-ID_Uridine (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H12C9N2O6", ID = 2044, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Retinylidene (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H26C20", ID = 380, 
                 Structural = true, ShortName = "Ret", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "RNPXL (N-term K)", 
                 AAs = "K", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H13C9N2O9P", Losses = new [] { new FragmentLoss("H13C9N2O9P"), }, ID = 1825, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "RNPXL (N-term R)", 
                 AAs = "R", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H13C9N2O9P", Losses = new [] { new FragmentLoss("H13C9N2O9P"), }, ID = 1825, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "s-GlcNAc (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H13C8NO8S", Losses = new [] { new FragmentLoss("H13C8NO8S"), }, ID = 1412, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Saligenin (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H6C7O", ID = 1254, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Saligenin (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C7O", ID = 1254, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ser->LacticAcid (Protein N-term S)", 
                 AAs = "S", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "-HN", ID = 403, 
                 Structural = true, ShortName = "SLA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ser/Thr-KDO (ST)", 
                 AAs = "S, T", LabelAtoms = LabelAtoms.None, Formula = "H12C8O7", Losses = new [] { new FragmentLoss("H12C8O7"), }, ID = 2022, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "serotonylation (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H9C10NO", ID = 1992, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "shTMT (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C3C'9N'2O2", ID = 2015, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "shTMT (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H20C3C'9N'2O2", ID = 2015, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "shTMTpro (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H25C'15N'3O3", ID = 2050, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "shTMTpro (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H25C'15N'3O3", ID = 2050, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SMA (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C6NO2", ID = 29, 
                 Structural = true, ShortName = "SMA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SMA (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H9C6NO2", ID = 29, 
                 Structural = true, ShortName = "SMA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "spermidine (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H16C7N2", ID = 1421, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "spermine (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "H23C10N3", ID = 1420, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SPITC (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C7H5NO3S2", ID = 261, 
                 Structural = true, ShortName = "SI0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SPITC (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C7H5NO3S2", ID = 261, 
                 Structural = true, ShortName = "SI0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C4H4O3", ID = 64, 
                 Structural = true, ShortName = "Suc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C4H4O3", ID = 64, 
                 Structural = true, ShortName = "Suc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "C6H5NO2S", ID = 285, 
                 Structural = true, ShortName = "SA0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "C6H5NO2S", ID = 285, 
                 Structural = true, ShortName = "SA0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "C6H5NO2S", ID = 285, 
                 Structural = true, ShortName = "SA0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Sulfide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "S", ID = 421, 
                 Structural = true, ShortName = "Sud", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Sulfide (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "S", ID = 421, 
                 Structural = true, ShortName = "Sud", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Sulfide (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "S", ID = 421, 
                 Structural = true, ShortName = "Sud", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Sulfo (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "O3S", ID = 40, 
                 Structural = true, ShortName = "SuO", Hidden = true, 
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
                 Name = "SulfurDioxide (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "O2S", ID = 1327, 
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
                 Name = "TAMRA-FP (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H46C37N3O6P", ID = 1038, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TAMRA-FP (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H46C37N3O6P", ID = 1038, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiadiazole (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H6C9N2S", ID = 1035, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiazolidine (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C", ID = 1009, 
                 Structural = true, ShortName = "FRT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiazolidine (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "C", ID = 1009, 
                 Structural = true, ShortName = "FRT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiazolidine (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "C", ID = 1009, 
                 Structural = true, ShortName = "FRT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiazolidine (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C", ID = 1009, 
                 Structural = true, ShortName = "FRT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiazolidine (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C", ID = 1009, 
                 Structural = true, ShortName = "FRT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiazolidine (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "C", ID = 1009, 
                 Structural = true, ShortName = "FRT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiazolidine (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "C", ID = 1009, 
                 Structural = true, ShortName = "FRT", Hidden = true, 
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
                 Structural = true, ShortName = "TSB", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiophos-S-S-biotin (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H34C19N4O5PS3", Losses = new [] { new FragmentLoss("H34C19N4O5PS3"), }, ID = 332, 
                 Structural = true, ShortName = "TSB", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiophos-S-S-biotin (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H34C19N4O5PS3", Losses = new [] { new FragmentLoss("H34C19N4O5PS3"), }, ID = 332, 
                 Structural = true, ShortName = "TSB", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiophospho (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "HO2PS", ID = 260, 
                 Structural = true, ShortName = "ThP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiophospho (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "HO2PS", ID = 260, 
                 Structural = true, ShortName = "ThP", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Thiophospho (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HO2PS", ID = 260, 
                 Structural = true, ShortName = "ThP", Hidden = true, 
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
                 Structural = true, ShortName = "Trx", Hidden = true, 
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
                 Name = "TMPP-Ac (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C29H33O10P", ID = 827, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMPP-Ac (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C29H33O10P", ID = 827, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMPP-Ac (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "C29H33O10P", ID = 827, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMT (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H20C12N2O2", ID = 739, 
                 Structural = true, ShortName = "TM0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMT (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H20C12N2O2", ID = 739, 
                 Structural = true, ShortName = "TM0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMT (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H20C12N2O2", ID = 739, 
                 Structural = true, ShortName = "TM0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMT (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H20C12N2O2", ID = 739, 
                 Structural = true, ShortName = "TM0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMT (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H20C12N2O2", ID = 739, 
                 Structural = true, ShortName = "TM0", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMT2plex (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H20C11C'N2O2", ID = 738, 
                 Structural = true, ShortName = "TM2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMT2plex (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H20C11C'N2O2", ID = 738, 
                 Structural = true, ShortName = "TM2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMT2plex (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H20C11C'N2O2", ID = 738, 
                 Structural = true, ShortName = "TM2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMT6plex (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H20C8C'4NN'O2", ID = 737, 
                 Structural = true, ShortName = "TM6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMT6plex (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H20C8C'4NN'O2", ID = 737, 
                 Structural = true, ShortName = "TM6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMT6plex (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H20C8C'4NN'O2", ID = 737, 
                 Structural = true, ShortName = "TM6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMTpro (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H25C8C'7NN'2O3", ID = 2016, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMTpro (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H25C8C'7NN'2O3", ID = 2016, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMTpro (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H25C8C'7NN'2O3", ID = 2016, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMTpro_zero (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H25C15N3O3", ID = 2017, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMTpro_zero (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H25C15N3O3", ID = 2017, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMTpro_zero (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H25C15N3O3", ID = 2017, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMTpro_zero (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H25C15N3O3", ID = 2017, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMTpro_zero (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H25C15N3O3", ID = 2017, 
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
                 Structural = true, ShortName = "3Io", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Triiodothyronine (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "HC6OI3", ID = 397, 
                 Structural = true, ShortName = "3IT", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trimethyl (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C3H6", Losses = new [] { new FragmentLoss("H9C3N"), }, ID = 37, 
                 Structural = true, ShortName = "3Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trimethyl (Protein N-term A)", 
                 AAs = "A", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C3H6", ID = 37, 
                 Structural = true, ShortName = "3Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trimethyl (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C3H6", ID = 37, 
                 Structural = true, ShortName = "3Me", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trioxidation (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "O3", ID = 345, 
                 Structural = true, ShortName = "3Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trioxidation (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "O3", ID = 345, 
                 Structural = true, ShortName = "3Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trioxidation (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O3", ID = 345, 
                 Structural = true, ShortName = "3Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trioxidation (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "O3", ID = 345, 
                 Structural = true, ShortName = "3Ox", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Tripalmitate (Protein N-term C)", 
                 AAs = "C", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H96C51O5", ID = 51, 
                 Structural = true, ShortName = "3Pa", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Tris (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "H10C4NO2", ID = 1831, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Triton (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H20C14", ID = 1836, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Triton (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H20C14", ID = 1836, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trp->Hydroxykynurenin (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O2 - C", ID = 350, 
                 Structural = true, ShortName = "HKy", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trp->Kynurenin (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O - C", ID = 351, 
                 Structural = true, ShortName = "Kyn", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trp->Oxolactone (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.None, Formula = "O - H2", ID = 288, 
                 Structural = true, ShortName = "Oxo", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Tween20 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H21C12", ID = 1834, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Tween80 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H31C18O", ID = 1835, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Tyr->Dha (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "-H6C6O", ID = 400, 
                 Structural = true, ShortName = "YDA", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ub-Br2 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H8C4N2O", ID = 1257, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ub-fluorescein (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H29C31N6O7", ID = 1261, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Ub-VME (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H12C7N2O3", ID = 1258, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "UgiJoullie (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H60C47N23O10", ID = 1276, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "UgiJoullie (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H60C47N23O10", ID = 1276, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "UgiJoullieProGly (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H10C7N2O2", ID = 1282, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "UgiJoullieProGly (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H10C7N2O2", ID = 1282, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "UgiJoullieProGlyProGly (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H20C14N4O4", ID = 1283, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "UgiJoullieProGlyProGly (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H20C14N4O4", ID = 1283, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:162 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H18C8O3", ID = 1970, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:162 (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "H18C8O3", ID = 1970, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:162 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H18C8O3", ID = 1970, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:177 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "OFe3 - H7", ID = 1971, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:177 (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "OFe3 - H7", ID = 1971, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:177 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "OFe3 - H7", ID = 1971, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:210 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H22C13O2", ID = 1972, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:210 (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "H22C13O2", ID = 1972, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:210 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H22C13O2", ID = 1972, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:216 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H16C10O5", ID = 1973, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:216 (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "H16C10O5", ID = 1973, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:216 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H16C10O5", ID = 1973, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:234 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H14C9O7", ID = 1974, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:234 (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "H14C9O7", ID = 1974, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:234 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H14C9O7", ID = 1974, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:248 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H28C13O4", ID = 1975, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:248 (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "H28C13O4", ID = 1975, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:248 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H28C13O4", ID = 1975, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:250 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H4C10NO5S", ID = 1976, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:250 (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "H4C10NO5S", ID = 1976, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:250 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H4C10NO5S", ID = 1976, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:302 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H8C4N5O7S2", ID = 1977, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:302 (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "H8C4N5O7S2", ID = 1977, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:302 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H8C4N5O7S2", ID = 1977, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:306 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H18C12O9", ID = 1978, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:306 (DE)", 
                 AAs = "D, E", LabelAtoms = LabelAtoms.None, Formula = "H18C12O9", ID = 1978, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:306 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H18C12O9", ID = 1978, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:420 (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H24C12N2O6S4", Losses = new [] { new FragmentLoss("H24C12N2O6S4"), }, ID = 1979, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Unknown:420 (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H24C12N2O6S4", Losses = new [] { new FragmentLoss("H24C12N2O6S4"), }, ID = 1979, 
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
                 Name = "Withaferin (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H38C28O6", ID = 1036, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:B10621 (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H30C31N4O6SI", ID = 323, 
                 Structural = true, ShortName = "XB1", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:BMOE (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H8C10N2O4", ID = 824, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:BS2G[113] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7C5NO2", ID = 1906, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:BS2G[114] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C5O3", ID = 1907, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:BS2G[217] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C9NO5", ID = 1908, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:BS2G[96] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C5O2", ID = 1905, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:BuUrBu[111] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C5NO2", ID = 1885, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:BuUrBu[196] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C9N2O3", ID = 1899, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:BuUrBu[213] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C9N3O3", ID = 1887, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:BuUrBu[214] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H14C9N2O4", ID = 1888, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:BuUrBu[317] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H23C13N3O6", ID = 1889, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:BuUrBu[85] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H7C4NO", ID = 1886, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DFDNB (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C6N2O4", ID = 825, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DFDNB (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.None, Formula = "C6N2O4", ID = 825, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DFDNB (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.None, Formula = "C6N2O4", ID = 825, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DFDNB (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C6N2O4", ID = 825, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DMP[122] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10C7N2", ID = 1912, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DMP[139] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H13C7N3", ID = 1911, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DMP[140] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C7N2O", ID = 1027, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DMP[154] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H14C8N2O", ID = 455, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSPP[210] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H3C8O5P", ID = 2058, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSPP[226] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C8NO5P", ID = 2061, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSPP[228] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C8O6P", ID = 2059, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSPP[331] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H14C12NO8P", ID = 2060, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSS[138] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10C8O2", ID = 1898, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSS[155] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H13C8NO2", ID = 1789, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSS[156] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C8O3", ID = 1020, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSS[259] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H21C12NO5", ID = 1877, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSSO[104] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3O2S", ID = 1883, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSSO[158] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C6O3S", ID = 1896, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSSO[175] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H9C6NO3S", ID = 1879, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSSO[176] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C6O4S", ID = 1878, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSSO[279] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H17C10NO6S", ID = 1880, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSSO[54] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C3O", ID = 1881, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DSSO[86] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C3OS", ID = 1882, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DST[114] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H2C4O4", ID = 1901, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DST[132] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C4O5", ID = 1022, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DST[56] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C2O2", ID = 1999, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DTBP[172] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C6N2S2", ID = 1900, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DTBP[87] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C3NS", ID = 324, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DTSSP[174] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H6C6O2S2", ID = 1902, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DTSSP[192] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H8C6O3S2", ID = 1023, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:DTSSP[88] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H4C3OS", ID = 126, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:EGS[115] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H5C4NO3", ID = 1028, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:EGS[226] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H10C10O6", ID = 1897, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:EGS[244] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H12C10O7", ID = 1021, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:SMCC[219] (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H13C12NO3", ID = 1903, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:SMCC[219] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H13C12NO3", ID = 1903, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:SMCC[237] (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H15C12NO4", ID = 1024, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:SMCC[237] (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H15C12NO4", ID = 1024, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Xlink:SMCC[321] (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H27C17N3O3", ID = 908, 
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
                 Name = "ZQG (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H16C15N2O6", Losses = new [] { new FragmentLoss("H6C8O2"), }, ID = 2001, 
                 Structural = true, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl:13C(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'2 - C2", ID = 1372, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl:2H(3) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 56, 
                 Structural = false, ShortName = "DAc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl:2H(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 56, 
                 Structural = false, ShortName = "DAc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl:2H(3) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 56, 
                 Structural = false, ShortName = "DAc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl:2H(3) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 56, 
                 Structural = false, ShortName = "DAc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl:2H(3) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 56, 
                 Structural = false, ShortName = "DAc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Acetyl:2H(3) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 56, 
                 Structural = false, ShortName = "DAc", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEC-MAEC:2H(4) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 792, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "AEC-MAEC:2H(4) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 792, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BEMAD_C:2H(6) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H'6 - H6", ID = 764, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BEMAD_ST:2H(6) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "H'6 - H6", ID = 763, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "BEMAD_ST:2H(6) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "H'6 - H6", ID = 763, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Carboxymethyl:13C(2) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C'2 - C2", ID = 775, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Cys->SecNEM:2H(5) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H'5 - H5", ID = 1034, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'6 - H6", ID = 1291, 
                 Structural = false, ShortName = "DM6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(6) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'6 - H6", ID = 1291, 
                 Structural = false, ShortName = "DM6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(6) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H'6 - H6", ID = 1291, 
                 Structural = false, ShortName = "DM6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(6)13C(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'2H'6 - C2H6", ID = 330, 
                 Structural = false, ShortName = "DM8", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(6)13C(2) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'2H'6 - C2H6", ID = 330, 
                 Structural = false, ShortName = "DM8", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Dimethyl:2H(6)13C(2) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C'2H'6 - C2H6", ID = 330, 
                 Structural = false, ShortName = "DM8", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "EQAT:2H(5) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H'5 - H5", ID = 198, 
                 Structural = false, ShortName = "EQ5", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ESP:2H(10) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'10 - H10", ID = 91, 
                 Structural = false, ShortName = "E10", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ESP:2H(10) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'10 - H10", ID = 91, 
                 Structural = false, ShortName = "E10", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 61, 
                 Structural = false, ShortName = "GQ3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(3) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 61, 
                 Structural = false, ShortName = "GQ3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'6 - H6", ID = 62, 
                 Structural = false, ShortName = "GQ6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(6) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'6 - H6", ID = 62, 
                 Structural = false, ShortName = "GQ6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(9) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'9 - H9", ID = 63, 
                 Structural = false, ShortName = "GQ9", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "GIST-Quat:2H(9) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'9 - H9", ID = 63, 
                 Structural = false, ShortName = "GQ9", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Glu->pyro-Glu+Methyl:2H(2)13C(1) (N-term E)", 
                 AAs = "E", Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'H'2 - CH2", ID = 1827, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-G:2H(8) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H'8 - H8", ID = 9, 
                 Structural = false, ShortName = "GG8", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICAT-H:13C(6) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C'6 - C6", ID = 124, 
                 Structural = false, ShortName = "GH6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICDID:2H(6) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H'6 - H6", ID = 1019, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "ICPL:2H(4) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 687, 
                 Structural = false, ShortName = "IP4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IGBP:13C(2) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C'2 - C2", ID = 499, 
                 Structural = false, ShortName = "ID2", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "IMID:2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 95, 
                 Structural = false, ShortName = "IM4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iodoacetanilide:13C(6) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "C'6 - C6", ID = 1398, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iodoacetanilide:13C(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'6 - C6", ID = 1398, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Iodoacetanilide:13C(6) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'6 - C6", ID = 1398, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(1)2H(3) (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H'3C' - H3C", ID = 862, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(2)15N(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'2N'2 - C2N2", ID = 1787, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(3) (A)", 
                 AAs = "A", LabelAtoms = LabelAtoms.C13, ID = 1296, 
                 Structural = false, ShortName = "+3a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(3)15N(1) (A)", 
                 AAs = "A", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 1297, 
                 Structural = false, ShortName = "+4a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(3)15N(1) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 1297, 
                 Structural = false, ShortName = "+4a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(4) (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "C'4 - C4", ID = 1266, 
                 Structural = false, ShortName = "+4b", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(4)15N(1) (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 1298, 
                 Structural = false, ShortName = "+05", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(5) (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.C13, ID = 772, 
                 Structural = false, ShortName = "+5b", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(5)15N(1) (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 268, 
                 Structural = false, ShortName = "+6a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(5)15N(1) (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 268, 
                 Structural = false, ShortName = "+6a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(5)15N(1) (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 268, 
                 Structural = false, ShortName = "+6a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(5)15N(1) (V)", 
                 AAs = "V", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 268, 
                 Structural = false, ShortName = "+6a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6) (I)", 
                 AAs = "I", LabelAtoms = LabelAtoms.C13, ID = 188, 
                 Structural = false, ShortName = "+06", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.C13, ID = 188, 
                 Structural = false, ShortName = "+06", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6) (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.C13, ID = 188, 
                 Structural = false, ShortName = "+06", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.C13, ID = 188, 
                 Structural = false, ShortName = "+06", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(1) (I)", 
                 AAs = "I", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 695, 
                 Structural = false, ShortName = "+07", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(1) (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 695, 
                 Structural = false, ShortName = "+07", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 259, 
                 Structural = false, ShortName = "+08", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(6)15N(4) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 267, 
                 Structural = false, ShortName = "+10", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(9) (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.C13, ID = 184, 
                 Structural = false, ShortName = "+09", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(9) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.C13, ID = 184, 
                 Structural = false, ShortName = "+09", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:13C(9)15N(1) (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.C13|LabelAtoms.N15, ID = 269, 
                 Structural = false, ShortName = "10a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (A)", 
                 AAs = "A", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (G)", 
                 AAs = "G", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (I)", 
                 AAs = "I", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (V)", 
                 AAs = "V", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(1) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.N15, ID = 994, 
                 Structural = false, ShortName = "+01", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.N15, ID = 995, 
                 Structural = false, ShortName = "+02", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(2) (N)", 
                 AAs = "N", LabelAtoms = LabelAtoms.N15, ID = 995, 
                 Structural = false, ShortName = "+02", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(2) (Q)", 
                 AAs = "Q", LabelAtoms = LabelAtoms.N15, ID = 995, 
                 Structural = false, ShortName = "+02", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(2) (W)", 
                 AAs = "W", LabelAtoms = LabelAtoms.N15, ID = 995, 
                 Structural = false, ShortName = "+02", Hidden = true, 
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
                 Structural = false, ShortName = "+03", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:15N(4) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.N15, ID = 897, 
                 Structural = false, ShortName = "+04", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:18O(1) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "O' - O", ID = 258, 
                 Structural = false, ShortName = "Ob1", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:18O(1) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "O' - O", ID = 258, 
                 Structural = false, ShortName = "Ob1", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:18O(1) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "O' - O", ID = 258, 
                 Structural = false, ShortName = "Ob1", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(10) (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.None, Formula = "H'10 - H10", ID = 1299, 
                 Structural = false, ShortName = "D10", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(3) (L)", 
                 AAs = "L", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 262, 
                 Structural = false, ShortName = "D03", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(3) (M)", 
                 AAs = "M", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 262, 
                 Structural = false, ShortName = "D03", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(4) (A)", 
                 AAs = "A", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 481, 
                 Structural = false, ShortName = "D04", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(4) (F)", 
                 AAs = "F", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 481, 
                 Structural = false, ShortName = "D04", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 481, 
                 Structural = false, ShortName = "D04", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(4) (U)", 
                 AAs = "U", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 481, 
                 Structural = false, ShortName = "D04", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(4) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 481, 
                 Structural = false, ShortName = "D04", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(4)13C(1) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H'4C' - H4C", ID = 1300, 
                 Structural = false, ShortName = "+5a", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(6)15N(1) (P)", 
                 AAs = "P", LabelAtoms = LabelAtoms.None, Formula = "H'6N' - H6N", ID = 1403, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(7)15N(4) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H'7N'4 - H7N4", ID = 1402, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Label:2H(9)13C(6)15N(2) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'9C'6N'2 - H9C6N2", ID = 696, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3) (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 298, 
                 Structural = false, ShortName = "M+3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3) (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 298, 
                 Structural = false, ShortName = "M+3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3) (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 298, 
                 Structural = false, ShortName = "M+3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 298, 
                 Structural = false, ShortName = "M+3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 298, 
                 Structural = false, ShortName = "M+3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3)13C(1) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'H'3 - CH3", ID = 329, 
                 Structural = false, ShortName = "eMe", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3)13C(1) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'H'3 - CH3", ID = 329, 
                 Structural = false, ShortName = "eMe", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Methyl:2H(3)13C(1) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C'H'3 - CH3", ID = 329, 
                 Structural = false, ShortName = "eMe", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(3)15N(1) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "C'3N' - C3N", ID = 889, 
                 Structural = false, ShortName = "M04", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(3)15N(1) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "C'3N' - C3N", ID = 889, 
                 Structural = false, ShortName = "M04", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(3)15N(1) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "C'3N' - C3N", ID = 889, 
                 Structural = false, ShortName = "M04", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(6)15N(2) (H)", 
                 AAs = "H", LabelAtoms = LabelAtoms.None, Formula = "C'6N'2 - C6N2", ID = 1302, 
                 Structural = false, ShortName = "M08", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(6)15N(2) (S)", 
                 AAs = "S", LabelAtoms = LabelAtoms.None, Formula = "C'6N'2 - C6N2", ID = 1302, 
                 Structural = false, ShortName = "M08", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "mTRAQ:13C(6)15N(2) (T)", 
                 AAs = "T", LabelAtoms = LabelAtoms.None, Formula = "C'6N'2 - C6N2", ID = 1302, 
                 Structural = false, ShortName = "M08", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Phenylisocyanate:2H(5) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'5 - H5", ID = 412, 
                 Structural = false, ShortName = "Pc5", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionamide:2H(3) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 97, 
                 Structural = false, ShortName = "PP3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionyl:13C(3) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'3 - C3", ID = 59, 
                 Structural = false, ShortName = "Po3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Propionyl:13C(3) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'3 - C3", ID = 59, 
                 Structural = false, ShortName = "Po3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "QAT:2H(3) (C)", 
                 AAs = "C", LabelAtoms = LabelAtoms.None, Formula = "H'3 - H3", ID = 196, 
                 Structural = false, ShortName = "QT3", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SPITC:13C(6) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'6 - C6", ID = 464, 
                 Structural = false, ShortName = "SI6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SPITC:13C(6) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'6 - C6", ID = 464, 
                 Structural = false, ShortName = "SI6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl:13C(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'4 - C4", ID = 66, 
                 Structural = false, ShortName = "Su4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl:13C(4) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'4 - C4", ID = 66, 
                 Structural = false, ShortName = "Su4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl:2H(4) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 65, 
                 Structural = false, ShortName = "Sd4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Succinyl:2H(4) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "H'4 - H4", ID = 65, 
                 Structural = false, ShortName = "Sd4", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid:13C(6) (C-term)", 
                 Terminus = ModTerminus.C, LabelAtoms = LabelAtoms.None, Formula = "C'6 - C6", ID = 286, 
                 Structural = false, ShortName = "SA6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid:13C(6) (D)", 
                 AAs = "D", LabelAtoms = LabelAtoms.None, Formula = "C'6 - C6", ID = 286, 
                 Structural = false, ShortName = "SA6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "SulfanilicAcid:13C(6) (E)", 
                 AAs = "E", LabelAtoms = LabelAtoms.None, Formula = "C'6 - C6", ID = 286, 
                 Structural = false, ShortName = "SA6", Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMPP-Ac:13C(9) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'9 - C9", ID = 1993, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMPP-Ac:13C(9) (N-term)", 
                 Terminus = ModTerminus.N, LabelAtoms = LabelAtoms.None, Formula = "C'9 - C9", ID = 1993, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "TMPP-Ac:13C(9) (Y)", 
                 AAs = "Y", LabelAtoms = LabelAtoms.None, Formula = "C'9 - C9", ID = 1993, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trimethyl:13C(3)2H(9) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "C'3H'9 - C3H9", ID = 1414, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trimethyl:13C(3)2H(9) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "C'3H'9 - C3H9", ID = 1414, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trimethyl:2H(9) (K)", 
                 AAs = "K", LabelAtoms = LabelAtoms.None, Formula = "H'9 - H9", ID = 1371, 
                 Structural = false, Hidden = true, 
            },
            new UniModModificationData
            {
                 Name = "Trimethyl:2H(9) (R)", 
                 AAs = "R", LabelAtoms = LabelAtoms.None, Formula = "H'9 - H9", ID = 1371, 
                 Structural = false, Hidden = true, 
            },
            
            // Hardcoded Skyline Mods
            new UniModModificationData
            {
                 Name = "Ammonia Loss (K, N, Q, R)", 
                 AAs = "K, N, Q, R", LabelAtoms = LabelAtoms.None, Losses = new [] { new FragmentLoss("NH3"), }, 
                 Structural = true, Hidden = false, 
            },
            new UniModModificationData
            {
                 Name = "Water Loss (D, E, S, T)", 
                 AAs = "D, E, S, T", LabelAtoms = LabelAtoms.None, Losses = new [] { new FragmentLoss("H2O"), }, 
                 Structural = true, Hidden = false, 
            },
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
//Unable to match: Acetyl (Protein N-term)
//Unable to match: Amidated (Protein C-term)
//Unable to match: Formyl (Protein N-term)
//Unable to match: ICPL (Protein N-term)
//Unable to match: ICPL:13C(6) (Protein N-term)
//Unable to match: ICPL:13C(6)2H(4) (Protein N-term)
//Unable to match: ICPL:2H(4) (Protein N-term)
//Unable to match: Acetyl:13C(2) (Protein N-term)
//Unable to match: Acetyl:2H(3) (Protein N-term)
//Unable to match: AEBS (Protein N-term)
//Unable to match: BDMAPP (Protein N-term)
//Unable to match: Biotin-PEO-Amine (Protein C-term)
//Unable to match: biotinAcrolein298 (Protein N-term)
//Unable to match: CAMthiopropanoyl (Protein N-term)
//Unable to match: Carbamyl (Protein N-term)
//Unable to match: Cholesterol (Protein C-term)
//Unable to match: Delta:H(2)C(2) (Protein N-term)
//Unable to match: Delta:H(4)C(3) (Protein N-term)
//Unable to match: Delta:H(6)C(3)O(1) (Protein N-term)
//Unable to match: Delta:H(8)C(6)O(1) (Protein N-term)
//Unable to match: Di_L-Gln_N-propargyl-L-Gln_desthiobiotin (D)
//Unable to match: Di_L-Gln_N-propargyl-L-Gln_desthiobiotin (E)
//Unable to match: Di_L-Glu_N-propargyl-L-Gln_desthiobiotin (D)
//Unable to match: Di_L-Glu_N-propargyl-L-Gln_desthiobiotin (E)
//Unable to match: DiART6plex (Protein N-term)
//Unable to match: DiART6plex115 (Protein N-term)
//Unable to match: DiART6plex116/119 (Protein N-term)
//Unable to match: DiART6plex117 (Protein N-term)
//Unable to match: DiART6plex118 (Protein N-term)
//Unable to match: Dimethyl (Protein N-term)
//Unable to match: Dimethyl:2H(4) (Protein N-term)
//Unable to match: Dimethyl:2H(4)13C(2) (Protein N-term)
//Unable to match: Dimethyl:2H(6)13C(2) (Protein N-term)
//Unable to match: Ethyl (Protein N-term)
//Unable to match: FormylMet (Protein N-term)
//Unable to match: GG (Protein N-term)
//Unable to match: Glu (Protein C-term)
//Unable to match: Glucuronyl (Protein N-term)
//Unable to match: GluGlu (Protein C-term)
//Unable to match: GluGluGlu (Protein C-term)
//Unable to match: GluGluGluGlu (Protein C-term)
//Unable to match: Glyceroyl (Protein N-term)
//Unable to match: GPIanchor (Protein C-term)
//Unable to match: iTRAQ4plex (Protein N-term)
//Unable to match: iTRAQ8plex (Protein N-term)
//Unable to match: LG-Hlactam-K (Protein N-term)
//Unable to match: LG-lactam-K (Protein N-term)
//Unable to match: MesitylOxide (Protein N-term)
//Unable to match: Methyl (Protein N-term)
//Unable to match: Microcin (Protein C-term)
//Unable to match: MicrocinC7 (Protein C-term)
//Unable to match: Mono_N-propargyl-L-Gln_desthiobiotin (C)
//Unable to match: Palmitoyl (Protein N-term)
//Unable to match: Propionyl (Protein N-term)
//Unable to match: Propyl (Protein C-term)
//Unable to match: shTMT (Protein N-term)
//Unable to match: shTMTpro (Protein N-term)
//Unable to match: Succinyl (Protein N-term)
//Unable to match: Thiazolidine (Protein N-term)
//Unable to match: TMT (Protein N-term)
//Unable to match: TMT2plex (Protein N-term)
//Unable to match: TMT6plex (Protein N-term)
//Unable to match: TMTpro (Protein N-term)
//Unable to match: TMTpro_zero (Protein N-term)
//Unable to match: Xlink:BS2G[113] (Protein N-term)
//Unable to match: Xlink:BS2G[114] (Protein N-term)
//Unable to match: Xlink:BS2G[217] (Protein N-term)
//Unable to match: Xlink:BS2G[96] (Protein N-term)
//Unable to match: Xlink:BuUrBu[111] (Protein N-term)
//Unable to match: Xlink:BuUrBu[196] (Protein N-term)
//Unable to match: Xlink:BuUrBu[213] (Protein N-term)
//Unable to match: Xlink:BuUrBu[214] (Protein N-term)
//Unable to match: Xlink:BuUrBu[317] (Protein N-term)
//Unable to match: Xlink:BuUrBu[85] (Protein N-term)
//Unable to match: Xlink:DMP[122] (Protein N-term)
//Unable to match: Xlink:DMP[139] (Protein N-term)
//Unable to match: Xlink:DMP[140] (Protein N-term)
//Unable to match: Xlink:DMP[154] (Protein N-term)
//Unable to match: Xlink:DSPP[210] (Protein N-term)
//Unable to match: Xlink:DSPP[226] (Protein N-term)
//Unable to match: Xlink:DSPP[228] (Protein N-term)
//Unable to match: Xlink:DSPP[331] (Protein N-term)
//Unable to match: Xlink:DSS[138] (Protein N-term)
//Unable to match: Xlink:DSS[155] (Protein N-term)
//Unable to match: Xlink:DSS[156] (Protein N-term)
//Unable to match: Xlink:DSS[259] (Protein N-term)
//Unable to match: Xlink:DSSO[104] (Protein N-term)
//Unable to match: Xlink:DSSO[158] (Protein N-term)
//Unable to match: Xlink:DSSO[175] (Protein N-term)
//Unable to match: Xlink:DSSO[176] (Protein N-term)
//Unable to match: Xlink:DSSO[279] (Protein N-term)
//Unable to match: Xlink:DSSO[54] (Protein N-term)
//Unable to match: Xlink:DSSO[86] (Protein N-term)
//Unable to match: Xlink:DST[114] (Protein N-term)
//Unable to match: Xlink:DST[132] (Protein N-term)
//Unable to match: Xlink:DST[56] (Protein N-term)
//Unable to match: Xlink:DTBP[172] (Protein N-term)
//Unable to match: Xlink:DTBP[87] (Protein N-term)
//Unable to match: Xlink:DTSSP[174] (Protein N-term)
//Unable to match: Xlink:DTSSP[192] (Protein N-term)
//Unable to match: Xlink:DTSSP[88] (Protein N-term)
//Unable to match: Xlink:EGS[115] (Protein N-term)
//Unable to match: Xlink:EGS[226] (Protein N-term)
//Unable to match: Xlink:EGS[244] (Protein N-term)
//Unable to match: Xlink:SMCC[219] (Protein N-term)
//Unable to match: Xlink:SMCC[237] (Protein N-term)
//Unused code: arg-add = +1R
//Unused code: benzyl = Bnz
//Unused code: benzyloxycarbonyl = CBZ
//Unused code: boc = Boc
//Unused code: cholesterol = Chl
//Unused code: c-terminal ethyl = 1Et
//Unused code: c-terminal methyl = 1Me
//Unused code: c-terminal methyl:2h(3) = M+3
//Unused code: cys->hcy = CHC
//Unused code: cys->pyro-cam = PCa
//Unused code: cys->pyro-cmc = PCm
//Unused code: delta:h(-1)o(-1)18o(1) = DeW
//Unused code: diart6plex  = DRT
//Unused code: dinitrophenyl = 2Np
//Unused code: dipropionyl = 2Pr
//Unused code: dtt_c = DTS
//Unused code: dtt_st = DTO
//Unused code: ethylamide = EAm
//Unused code: fmoc = Fmc
//Unused code: formylmet = FMA
//Unused code: gpianchor = GPI
//Unused code: hex(3)hexnac(2)p(1) = GdP
//Unused code: hex(6) = 6Hx
//Unused code: hn2_mustard  = Mu2
//Unused code: hn3_mustard  = Mu3
//Unused code: hydrazide = Hyz
//Unused code: iodoacetanilide  = An0
//Unused code: iodoacetanilide:13c(6)  = An6
//Unused code: itraq8plexcold = T8C
//Unused code: label:2h(6)15n(1)  = +7a
//Unused code: label:2h(7)15n(4)  = +11
//Unused code: lys->hydroxyallysine = HAA
//Unused code: lys-add = +1K
//Unused code: met->aha+tag = MAT
//Unused code: met->aha+tagmcl = MAC
//Unused code: met->carbamidomethyl-hcy = CaS
//Unused code: met->carboxymethyl-hcy = CoS
//Unused code: met->hcy = MHC
//Unused code: methyl+propionyl = MeP
//Unused code: microcin = Mic
//Unused code: microcinc7 = Mi7
//Unused code: naphthyl = Npl
//Unused code: napthylacetyl = NpA
//Unused code: n-ethylmaleimide = NEM
//Unused code: n-ethylmaleimide+water = NMH
//Unused code: nhs-fluorescein  = Flr
//Unused code: n-methylmaleimide = NMM
//Unused code: n-methylmaleimide+water = MMH
//Unused code: n-terminal ethyl = 1Et
//Unused code: n-terminal methyl = 1Me
//Unused code: oxidation+nem  = OxN
//Unused code: phospho(ser,thr) = Pho
//Unused code: phosphoglyceryl = Pgl
//Unused code: pmc = PMC
//Unused code: propionyl:2h(5) = PD5
//Unused code: sec->dha = UHA
//Unused code: ser->oxoalanine = SOa
//Unused code: thioacyl = ThA
//Unused code: trifluoroacetyl = TFA
//Unused code: trinitrophenyl = 3Np
//Unused code: xlink:ssd = XSD
