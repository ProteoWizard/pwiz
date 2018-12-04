/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.ElementLocators.ExportAnnotations
{
    /// <summary>
    /// Class for importing and exporting all of the annotations in a Skyline document.
    /// </summary>
    public class DocumentAnnotations
    {
        // ReSharper disable LocalizableElement
        public const string COLUMN_LOCATOR = "ElementLocator";
        public const string COLUMN_NOTE = "Note";
        public const string ANNOTATION_PREFIX = "annotation_";
        public const string PROPERTY_PREFIX = "property_";

        private IDictionary<string, ElementHandler> _elementHandlers;
        // ReSharper restore LocalizableElement
        public DocumentAnnotations(SkylineDataSchema skylineDataSchema)
        {
            DataSchema = skylineDataSchema;
            _elementHandlers = ElementHandler.GetElementHandlers(DataSchema).ToDictionary(handler => handler.Name);
        }

        public DocumentAnnotations(SrmDocument document) : this (SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT))
        {
        }

        public SkylineDataSchema DataSchema { get; private set; }

        public SrmDocument Document { get { return DataSchema.Document; } }
        public CultureInfo CultureInfo { get { return DataSchema.DataSchemaLocalizer.FormatProvider; } }

        public void WriteAnnotationsToFile(CancellationToken cancellationToken, ExportAnnotationSettings settings, string filename)
        {
            using (var writer = new StreamWriter(filename))
            {
                SaveAnnotations(cancellationToken, settings, writer, TextUtil.SEPARATOR_CSV);
            }
        }
        
        public void SaveAnnotations(CancellationToken cancellationToken, ExportAnnotationSettings settings, TextWriter writer, char separator)
        {
            WriteAllAnnotations(cancellationToken, settings, writer, separator);
        }
        
        private void WriteAllAnnotations(CancellationToken cancellationToken, ExportAnnotationSettings settings, TextWriter textWriter, char separator)
        {
            WriteRow(textWriter, separator, GetColumnHeaders(settings));
            foreach (var elementType in settings.ElementTypes)
            {
                var handler = _elementHandlers[elementType];
                foreach (var skylineObject in handler.ListElements())
                {
                    var values = GetRowValues(settings, handler, skylineObject).ToArray();
                    if (settings.RemoveBlankRows && values.All(string.IsNullOrEmpty))
                    {
                        continue;
                    }
                    WriteRow(textWriter, separator, new[]{skylineObject.GetLocator()}.Concat(values));
                }
            }
        }

        private string FormatValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (value is SampleType sampleType)
            {
                return sampleType.Name;
            }
            if (value is NormalizationMethod normalizationMethod)
            {
                return normalizationMethod.Name;
            }
            if (value is double)
            {
                return ((double) value).ToString(Formats.RoundTrip, CultureInfo);
            }
            return value.ToString();
        }

        private object ParseValue(string value, Type type)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            if (type == typeof(SampleType))
            {
                return SampleType.FromName(value);
            }
            if (type == typeof(NormalizationMethod))
            {
                return NormalizationMethod.FromName(value);
            }
            return Convert.ChangeType(value, type, DataSchema.DataSchemaLocalizer.FormatProvider);
        }

        public SrmDocument ReadAnnotationsFromFile(CancellationToken cancellationToken, string filename)
        {
            DataSchema.BeginBatchModifyDocument();
            using (var streamReader = new StreamReader(filename))
            {
                var dsvReader = new DsvFileReader(streamReader, TextUtil.SEPARATOR_CSV);
                ReadAllAnnotations(cancellationToken, dsvReader);
            }
            DataSchema.CommitBatchModifyDocument(string.Empty, null);
            return DataSchema.Document;
        }

        public void ReadAllAnnotations(CancellationToken cancellationToken, DsvFileReader fileReader)
        {
            var fieldNames = fileReader.FieldNames;
            int locatorColumnIndex = fieldNames.IndexOf(COLUMN_LOCATOR);
            if (locatorColumnIndex < 0)
            {
                throw new InvalidDataException(string.Format(Resources.Columns_Columns_Missing_column___0__,
                    COLUMN_LOCATOR));
            }
            string[] row;
            while ((row = fileReader.ReadLine()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ElementLocator elementLocator = ElementLocator.Parse(row[locatorColumnIndex]);
                var elementRef = ElementRefs.FromObjectReference(elementLocator);
                ElementHandler handler;
                if (!_elementHandlers.TryGetValue(elementRef.ElementType, out handler))
                {
                    throw ElementNotSupportedException(elementRef);
                }
                SkylineObject element = handler.FindElement(elementRef);
                if (element == null)
                {
                    throw ElementNotFoundException(elementRef);
                }
                for (int icol = 0; icol < fieldNames.Count; icol++)
                {
                    if (icol == locatorColumnIndex)
                    {
                        continue;
                    }
                    string fieldName = fieldNames[icol];
                    ImportablePropertyInfo propertyInfo = null;
                    AnnotationDef annotationDef = null;
                    if (fieldName.StartsWith(PROPERTY_PREFIX))
                    {
                        propertyInfo = handler.FindProperty(fieldName.Substring(PROPERTY_PREFIX.Length));
                    }
                    else if (fieldName.StartsWith(ANNOTATION_PREFIX))
                    {
                        annotationDef = handler.FindAnnotation(fieldName.Substring(ANNOTATION_PREFIX.Length));
                    }
                    if (propertyInfo == null && annotationDef == null)
                    {
                        propertyInfo = handler.FindProperty(fieldName);
                        if (propertyInfo == null)
                        {
                            annotationDef = handler.FindAnnotation(fieldName);
                        }
                    }
                    string fieldValue = row[icol];
                    if (propertyInfo == null && annotationDef == null)
                    {
                        if (string.IsNullOrEmpty(fieldValue))
                        {
                            continue;
                        }
                        throw AnnotationDoesNotApplyException(fieldName, elementRef);
                    }
                    
                    if (propertyInfo != null)
                    {
                        object value = propertyInfo.ParsePropertyValue(CultureInfo, fieldValue);
                        propertyInfo.PropertyDescriptor.SetValue(element, value);
                    }
                    if (annotationDef != null)
                    {
                        SetAnnotationValue(element, annotationDef, fieldValue);
                    }
                }
            }
        }

        private static Exception ElementNotFoundException(ElementRef elementRef)
        {
            return new InvalidDataException(string.Format(Resources.DocumentAnnotations_ElementNotFoundException_Could_not_find_element___0___, elementRef));
        }

        private static Exception ElementNotSupportedException(ElementRef elementRef)
        {
            throw new InvalidDataException(string.Format(@"Importing annotations is not supported for the element {0}.",
                elementRef));
        }

        private static Exception AnnotationDoesNotApplyException(string name, ElementRef elementRef)
        {
            return new InvalidDataException(string.Format(Resources.DocumentAnnotations_AnnotationDoesNotApplyException_Annotation___0___does_not_apply_to_element___1___,
                name, elementRef));
        }

        public IEnumerable<string> GetColumnHeaders(ExportAnnotationSettings settings)
        {
            return new[] {COLUMN_LOCATOR}
                .Concat(settings.AnnotationNames.Select(name => ANNOTATION_PREFIX + name))
                .Concat(settings.PropertyNames.Select(name => PROPERTY_PREFIX + name));
        }

        public void WriteRow(TextWriter writer, char separator, IEnumerable<object> values)
        {
            string strSeparator = new string(separator, 1);
            var row = string.Join(strSeparator,
                values.Select(value => DsvWriter.ToDsvField(separator, FormatValue(value))));
            writer.WriteLine(row);
        }

        private IEnumerable<string> GetRowValues(ExportAnnotationSettings settings,
            ElementHandler elementHandler, SkylineObject skylineObject)
        {
            foreach (var annotation in settings.AnnotationNames)
            {
                var annotationDef = elementHandler.FindAnnotation(annotation);
                if (annotationDef == null)
                {
                    yield return null;
                }
                else
                {
                    yield return FormatAnnotationValue(skylineObject, annotationDef);
                }
            }
            foreach (var property in settings.PropertyNames)
            {
                var propertyInfo = elementHandler.FindProperty(property);
                if (propertyInfo == null)
                {
                    yield return null;
                }
                else
                {
                    yield return propertyInfo.FormatPropertyValue(CultureInfo, propertyInfo.PropertyDescriptor.GetValue(skylineObject));
                }
            }
        }

        private string FormatAnnotationValue(SkylineObject skylineObject, AnnotationDef annotationDef)
        {
            var value = skylineObject.GetAnnotation(annotationDef);
            if (value == null || false.Equals(value))
            {
                return string.Empty;
            }
            if (value is double d)
            {
                return d.ToString(Formats.RoundTrip, CultureInfo);
            }
            return value.ToString();
        }

        private void SetAnnotationValue(SkylineObject skylineObject, AnnotationDef annotationDef, string strValue)
        {
            object value;
            if (string.IsNullOrEmpty(strValue))
            {
                value = null;
            }
            else
            {
                switch (annotationDef.Type)
                {
                    case AnnotationDef.AnnotationType.number:
                        value = double.Parse(strValue, CultureInfo);
                        break;
                    case AnnotationDef.AnnotationType.true_false:
                        if (false.ToString(CultureInfo) == strValue)
                        {
                            value = false;
                        }
                        else
                        {
                            value = true;
                        }
                        break;
                    default:
                        value = strValue;
                        break;
                }
            }
            skylineObject.SetAnnotation(annotationDef, value);
        }
    }
}
