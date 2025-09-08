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

using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Resources;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Files
{
    public class ReplicateProperties : FileNodeProperties
    {
        public ReplicateProperties(SrmDocument document, Replicate model, string localFilePath)
            : base(model, localFilePath)
        {
            Assume.IsNotNull(model);
            var chromSet = Replicate.LoadChromSetFromDocument(document, model);
            
            Assume.IsTrue(Name.GetType() == typeof(string));
            _rename = (doc, monitor, newValue) => Replicate.Rename(doc, monitor, model, (string)newValue);

            BatchName = chromSet.BatchName;
            AnalyteConcentration = chromSet.AnalyteConcentration;
            SampleDilutionFactor = chromSet.SampleDilutionFactor;
            SampleType = chromSet.SampleType.ToString();

            Annotations = new List<Annotations.Annotation>();
            _editAnnotationDictionary = new Dictionary<Annotations.Annotation, Func<SrmDocument, SrmSettingsChangeMonitor, object, ModifiedDocument>>();
            foreach (var annotationDef in Settings.Default.AnnotationDefList)
            {
                if (annotationDef.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate))
                {
                    var annotation = new Annotations.Annotation(new KeyValuePair<string, string>(
                        annotationDef.Name, chromSet.Annotations.GetAnnotation(annotationDef.Name)));
                    Annotations.Add(annotation);
                    _editAnnotationDictionary.Add(annotation,
                        (doc, monitor, newValue) => Replicate.EditAnnotation(doc, monitor, model, annotationDef, newValue));
                }
            }

            var dataFileInfo = chromSet.MSDataFileInfos;
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

        [Category("Replicate")] public string BatchName { get; }
        [Category("Replicate")] public double? AnalyteConcentration { get; }
        [Category("Replicate")] public double SampleDilutionFactor { get; }
        [Category("Replicate")] public string SampleType { get; }
        [Category("Replicate")] public double MaxRetentionTime { get; }
        [Category("Replicate")] public double MaxIntensity { get; }
        [Category("Replicate")] public string AcquisitionTime { get; }

        // Replicate Name is editable and updates the document with model.Rename
        [UseCustomHandling]
        [Category("FileInfo")] public override string Name { get; set; }
        private readonly Func<SrmDocument, SrmSettingsChangeMonitor, object, ModifiedDocument> _rename;

        // Instrument properties need to be added as nested properties, as lists don't render well in PropertyGrid
        [UseCustomHandling]
        [Category("Instruments")] public List<InstrumentProperties> Instruments { get; }

        // Annotations need to be added as individual properties under the Annotations category, for the above reason
        [UseCustomHandling]
        [Category("Annotations")] public List<Annotations.Annotation> Annotations { get; }
        private readonly Dictionary<Annotations.Annotation, Func<SrmDocument, SrmSettingsChangeMonitor, object, ModifiedDocument>> _editAnnotationDictionary;

        /// <summary>
        /// Transforms the list of InstrumentProperties into a form that will display better in the property sheet.
        /// List rendering is suboptimal, so we either show all properties of a single instrument at the top level,
        /// or we show each instrument's properties in a separate expandable section.
        /// </summary>
        protected override void AddCustomizedProperties()
        {
            // add editable Name property with ModifiedDocument delegate
            var namePropertyDescriptor = GetBaseDescriptorByName(nameof(Name));
            AddProperty(new GlobalizedPropertyDescriptor(
                namePropertyDescriptor,
                GetResourceManager(),
                getModifiedDocument: _rename));

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

            foreach (var annotation in Annotations)
            {
                List<string> dropDownOptions = null;
                if (annotation.AnnotationDef.Type == AnnotationDef.AnnotationType.value_list &&
                    annotation.AnnotationDef.ValueType == typeof(string))
                {
                    dropDownOptions = annotation.AnnotationDef.Items.ToList();
                    dropDownOptions.Insert(0, null); // Allow blank entry
                }

                AddProperty(new CustomHandledGlobalizedPropertyDescriptor(
                    annotation.AnnotationDef.ValueType,
                    annotation.Name,
                    annotation.GetAnnotation(),
                    GetBaseDescriptorByName(nameof(Annotations)).Category,
                    GetResourceManager(),
                    nonLocalizedDisplayName: annotation.Name,
                    getModifiedDocument: _editAnnotationDictionary[annotation],
                    dropDownOptions: dropDownOptions));
            }
        }

        // Test Support - enforced by code check
        // Invoked via reflection in InspectPropertySheetResources in CodeInspectionTest
        [UsedImplicitly]
        private static ResourceManager ResourceManager() => PropertySheetFileNodeResources.ResourceManager;
    }
}
