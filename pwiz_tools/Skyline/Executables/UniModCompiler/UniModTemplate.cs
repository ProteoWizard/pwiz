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
    public static class UniMod
    {
        public static StaticMod[] StructuralMods { get; private set; }
        public static StaticMod[] IsotopeMods { get; private set; }
        public static StaticMod[] HiddenStructuralMods { get; private set; }
        public static StaticMod[] HiddenIsotopeMods { get; private set; }

        public static void Init()
        {
// ReSharper disable RedundantExplicitArrayCreation
            StructuralMods = new[] {   
                // INSERT StructuralMods
            };

            IsotopeMods = new StaticMod[] {
                // INSERT IsotopeMods
            };

            HiddenStructuralMods = new StaticMod[] {
                // INSERT HiddenStructuralMods
            };

            HiddenIsotopeMods = new StaticMod[] {
                // INSERT HiddenIsotopeMods
            };
// ReSharper restore RedundantExplicitArrayCreation
        }
    }
}
