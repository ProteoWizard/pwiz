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
// ReSharper disable NonLocalizedString
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
            // ADD MODS.
            
            // Hardcoded Skyline Mods
            new UniModModificationData
            {
                 Name = "Amonia Loss (K, N, Q, R)", 
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