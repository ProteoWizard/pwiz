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

using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.PropertySheets;
using pwiz.Skyline.Model.PropertySheets.Templates;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Resources;

namespace pwiz.Skyline.Model.Files
{
    public class ReplicateProperties : FileNodeProperties
    {
        public ReplicateProperties(Replicate model, string localFilePath)
            : base(model, localFilePath)
        {
            Assume.IsNotNull(model);

            BatchName = model.BatchName;
            AnalyteConcentration = model.AnalyteConcentration;
            SampleDilutionFactor = model.SampleDilutionFactor;
            SampleType = model.SampleType;

            var dataFileInfo = model.MSDataFileInfos;
            if (dataFileInfo.Count == 1)
            {
                MaxRetentionTime = dataFileInfo[0].MaxRetentionTime;
                MaxIntensity = dataFileInfo[0].MaxIntensity;
                AcquisitionTime = dataFileInfo[0].RunStartTime.ToString();

                var instrumentInfo = dataFileInfo[0].InstrumentInfoList;
                if (instrumentInfo.Count > 0)
                {
                    Instruments = instrumentInfo
                        .Select(info => new InstrumentProperties(info))
                        .ToList();
                }
            }
            else if (dataFileInfo.Count > 1)
            {
                // TODO: figure out better way to signal to the user that there are multiple files, and to
                // select a replicate sample file to see its properties
            }
        }

        [Category("Replicate")] public string BatchName { get; set; }
        [Category("Replicate")] public double? AnalyteConcentration { get; set; }
        [Category("Replicate")] public double SampleDilutionFactor { get; set; }
        [Category("Replicate")] public string SampleType { get; set; }
        [Category("Replicate")] public double MaxRetentionTime { get; set; }
        [Category("Replicate")] public double MaxIntensity { get; set; }
        [Category("Replicate")] public string AcquisitionTime { get; set; }

        [UseCustomHandling] public List<InstrumentProperties> Instruments { get; set; }

        /// <summary>
        /// Transforms the list of InstrumentProperties into a form that will display better in the property sheet.
        /// List rendering is suboptimal, so we either show all properties of a single instrument at the top level,
        /// or we show each instrument's properties in a separate expandable section.
        /// </summary>
        protected override void AddCustomizedProperties()
        {
            const string instrumentsCategoryKey = "Instruments";

            if (Instruments?.Count == 1)
            {
                // if only one instrument, add instrument properties in one category at top level
                foreach (var globalizedProp in from PropertyDescriptor prop in TypeDescriptor.GetProperties(typeof(InstrumentProperties)) 
                         select new CustomHandledGlobalizedPropertyDescriptor(
                             GetResourceManager(), prop.GetValue(Instruments[0]), prop.Category, prop.Name,
                             prop.PropertyType))
                {
                    globalizedProps.Add(globalizedProp);
                }
            }
            else if (Instruments?.Count > 1)
            {
                // if multiple instruments, add instrument properties nested within each instrument
                for (var i = 0; i < Instruments.Count; i++)
                {
                    globalizedProps.Add(new CustomHandledGlobalizedPropertyDescriptor(
                        GetResourceManager(), Instruments[i], instrumentsCategoryKey, instrumentsCategoryKey + i,
                        typeof(InstrumentProperties), Instruments[i].Model, null,
                        new Attribute[] { new TypeConverterAttribute(typeof(ExpandableObjectConverter)) }));
                }
            }
        }

        // Test Support - enforced by code check
        // Invoked via reflection in InspectPropertySheetResources in CodeInspectionTest
        private static ResourceManager ResourceManager() => PropertySheetFileNodeResources.ResourceManager;
    }
}
