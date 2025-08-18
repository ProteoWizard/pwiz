/*
 * Original author: Aaron Banse <acbanse .at. acbanse dot com>,
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

using System;
using System.ComponentModel;
using System.Resources;
using pwiz.Skyline.Model.PropertySheets;
using pwiz.Skyline.Model.PropertySheets.Templates;

namespace pwiz.Skyline.Model.Files
{
    public class ReplicateProperties : FileNodeProperties
    {
        public ReplicateProperties(Replicate replicate)
            : base(replicate)
        {
            if (replicate == null)
                throw new ArgumentNullException(nameof(replicate));
            BatchName = replicate.ChromatogramSet.BatchName ?? string.Empty;
            AnalyteConcentration = replicate.ChromatogramSet.AnalyteConcentration;
            SampleDilutionFactor = replicate.ChromatogramSet.SampleDilutionFactor;
            SampleType = replicate.ChromatogramSet.SampleType?.ToString() ?? string.Empty;

            var dataFileInfo = replicate.ChromatogramSet.MSDataFileInfos;
            if (dataFileInfo.Count == 1)
            {
                MaxRetentionTime = dataFileInfo[0].MaxRetentionTime.ToString(format: "F2");
                MaxIntensity = dataFileInfo[0].MaxIntensity.ToString("F2");

                var instrumentInfo = dataFileInfo[0].InstrumentInfoList;
                if (instrumentInfo.Count > 0)
                {
                    Model = instrumentInfo[0].Model ?? string.Empty;
                    Ionization = instrumentInfo[0].Ionization ?? string.Empty;
                    Analyzer = instrumentInfo[0].Analyzer ?? string.Empty;
                    Detector = instrumentInfo[0].Detector ?? string.Empty;
                }
            }
        }

        [Category("Replicate")] public string BatchName { get; set; }
        [Category("Replicate")] public double? AnalyteConcentration { get; set; }
        [Category("Replicate")] public double SampleDilutionFactor { get; set; }
        [Category("Replicate")] public string SampleType { get; set; }
        [Category("Replicate")] public string MaxRetentionTime { get; set; }
        [Category("Replicate")] public string MaxIntensity { get; set; }
        [Category("Instrument")] public string Model { get; set; }
        [Category("Instrument")] public string Ionization { get; set; }
        [Category("Instrument")] public string Analyzer { get; set; }
        [Category("Instrument")] public string Detector { get; set; }

        // Test Support - enforced by code check
        // Invoked via reflection in InspectPropertySheetResources in CodeInspectionTest
        private static ResourceManager ResourceManager() => PropertySheetFileNodeResources.ResourceManager;
    }
}
