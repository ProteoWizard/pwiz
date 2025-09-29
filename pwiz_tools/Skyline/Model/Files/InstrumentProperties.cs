/*
 * Original author: Aaron Banse <acbanse .at. icloud dot com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using pwiz.ProteowizardWrapper;
using System.ComponentModel;
using System.Resources;
using JetBrains.Annotations;

namespace pwiz.Skyline.Model.Files
{
    public class InstrumentProperties : GlobalizedObject
    {
        protected override ResourceManager GetResourceManager()
        {
            return PropertyGridFileNodeResources.ResourceManager;
        }

        public override string ToString()
        {
            // This overrides default behavior which displays the full type name in the property sheet.
            return string.Empty;
        }

        public InstrumentProperties(MsInstrumentConfigInfo instrumentConfigInfo)
        {
            Model = instrumentConfigInfo.Model;
            Ionization = instrumentConfigInfo.Ionization;
            Analyzer = instrumentConfigInfo.Analyzer;
            Detector = instrumentConfigInfo.Detector;
        }

        [Category("Instrument")] public string Model { get; set; }
        [Category("Instrument")] public string Ionization { get; set; }
        [Category("Instrument")] public string Analyzer { get; set; }
        [Category("Instrument")] public string Detector { get; set; }

        // Test Support - enforced by code check
        // Invoked via reflection in InspectPropertySheetResources in CodeInspectionTest
        [UsedImplicitly]
        private static ResourceManager ResourceManager() => PropertyGridFileNodeResources.ResourceManager;
    }
}
