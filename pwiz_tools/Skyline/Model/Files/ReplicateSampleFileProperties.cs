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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Resources;
using JetBrains.Annotations;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Files
{
    public class ReplicateSampleFileProperties : FileNodeProperties
    {
        public ReplicateSampleFileProperties(SrmDocument document, ReplicateSampleFile model, string localFilePath)
            : base(model, localFilePath)
        {
            Assume.IsNotNull(model);
            var chromFileInfo = ReplicateSampleFile.LoadChromFileInfoFromDocument(document, model);

            MaxRetentionTime = chromFileInfo.MaxRetentionTime;
            MaxIntensity = chromFileInfo.MaxIntensity;
            AcquisitionTime = chromFileInfo.RunStartTime?.ToString();

            var instrumentInfo = chromFileInfo.InstrumentInfoList;
            if (instrumentInfo.Count > 0)
            {
                Instruments = instrumentInfo
                    .Select(info => new InstrumentProperties(info))
                    .ToList();
            }
        }

        [Category("Replicate")] public double MaxRetentionTime { get; }
        [Category("Replicate")] public double MaxIntensity { get; }
        [Category("Replicate")] public string AcquisitionTime { get; }

        [UseCustomHandling]
        [Category("Instruments")] public List<InstrumentProperties> Instruments { get; }

        /// <summary>
        /// Transforms the list of InstrumentProperties into a form that will display better in the property sheet.
        /// List rendering is suboptimal, so we either show all properties of a single instrument at the top level,
        /// or we show each instrument's properties in a separate expandable section.
        /// </summary>
        protected override void AddCustomizedProperties()
        {
            if (Instruments?.Count == 1)
            {
                // if only one instrument, add instrument properties in one category at top level
                foreach (var globalizedProp in from PropertyDescriptor prop in TypeDescriptor.GetProperties(typeof(InstrumentProperties))
                         select new CustomHandledGlobalizedPropertyDescriptor(
                             prop.PropertyType, prop.Name, prop.GetValue(Instruments[0]), prop.Category,
                             GetResourceManager()))
                {
                    AddProperty(globalizedProp);
                }
            }
            else if (Instruments?.Count > 1)
            {
                // if multiple instruments, add instrument properties nested within each instrument
                for (var i = 0; i < Instruments.Count; i++)
                {
                    var expandableAttr = new Attribute[]
                        { new TypeConverterAttribute(typeof(ExpandableObjectConverter)) };

                    AddProperty(new CustomHandledGlobalizedPropertyDescriptor(
                        typeof(InstrumentProperties),
                        GetBaseDescriptorByName(nameof(Instruments)).Category + i,
                        Instruments[i],
                        GetBaseDescriptorByName(nameof(Instruments)).Category,
                        GetResourceManager(),
                        attributes: expandableAttr,
                        nonLocalizedDisplayName: Instruments[i].Model));
                }
            }
        }

        // Test Support - enforced by code check
        // Invoked via reflection in InspectPropertySheetResources in CodeInspectionTest
        [UsedImplicitly]
        private static ResourceManager ResourceManager() => PropertySheetFileNodeResources.ResourceManager;
    }
}
