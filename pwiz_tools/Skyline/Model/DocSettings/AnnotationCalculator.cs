/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.DocSettings
{
    public class AnnotationCalculator
    {
        public const string ERROR_VALUE = @"#ERROR#";
        public const string NAME_ERROR = @"#NAME#";

        private Dictionary<Tuple<Type, PropertyPath>, ColumnSelector> _columnSelectors =
            new Dictionary<Tuple<Type, PropertyPath>, ColumnSelector>();

        public AnnotationCalculator(SrmDocument document)
            : this(SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT))
        {

        }

        public AnnotationCalculator(SkylineDataSchema dataSchema)
        {
            SkylineDataSchema = dataSchema;
        }

        public SrmDocument SrmDocument
        {
            get { return SkylineDataSchema.Document; }
        }

        public SkylineDataSchema SkylineDataSchema { get; private set; }

        public static Type RowTypeFromAnnotationTarget(AnnotationDef.AnnotationTarget annotationTarget)
        {
            switch (annotationTarget)
            {
                case AnnotationDef.AnnotationTarget.protein:
                    return typeof(Protein);
                case AnnotationDef.AnnotationTarget.peptide:
                    return typeof(Databinding.Entities.Peptide);
                case AnnotationDef.AnnotationTarget.precursor:
                    return typeof(Precursor);
                case AnnotationDef.AnnotationTarget.transition:
                    return typeof(Databinding.Entities.Transition);
                case AnnotationDef.AnnotationTarget.replicate:
                    return typeof(Replicate);
                case AnnotationDef.AnnotationTarget.precursor_result:
                    return typeof(PrecursorResult);
                case AnnotationDef.AnnotationTarget.transition_result:
                    return typeof(TransitionResult);
                default:
                    return null;
            }
        }

        public object GetAnnotation<T>(AnnotationDef annotationDef, T skylineObject,
            Annotations annotations) where T : SkylineObject
        {
            return GetAnnotation(annotationDef, typeof(T), skylineObject, annotations);
        }

        public object GetReplicateAnnotation(AnnotationDef annotationDef, ChromatogramSet chromatogramSet)
        {
            if (annotationDef.Expression == null)
            {
                return chromatogramSet.Annotations.GetAnnotation(annotationDef);
            }

            if (!SrmDocument.Settings.HasResults)
            {
                return null;
            }

            int replicateIndex;
            if (!SrmDocument.Settings.MeasuredResults.TryGetChromatogramSet(chromatogramSet.Name, out _,
                out replicateIndex))
            {
                return null;
            }

            return GetAnnotation(annotationDef, new Replicate(SkylineDataSchema, replicateIndex),
                chromatogramSet.Annotations);
        }

        public object GetAnnotation(AnnotationDef annotationDef, Type skylineObjectType,
            SkylineObject skylineObject, Annotations annotations)
        {
            var expression = annotationDef.Expression;
            if (expression == null)
            {
                return annotations.GetAnnotation(annotationDef);
            }

            ColumnSelector columnSelector = GetColumnSelector(skylineObjectType, expression.Column);
            if (!columnSelector.IsValid)
            {
                return NAME_ERROR;
            }

            try
            {
                if (expression.AggregateOperation == null)
                {
                    return ConvertAnnotationValue(annotationDef, columnSelector.GetSingleValue(skylineObject));
                }

                return ConvertAnnotationValue(annotationDef, columnSelector.AggregateValues(expression.AggregateOperation, skylineObject));
            }
            catch (Exception)
            {
                return ERROR_VALUE;
            }
        }

        public object ConvertAnnotationValue(AnnotationDef annotationDef, object value)
        {
            if (value == null || value is string)
            {
                return annotationDef.ParsePersistedString((string) value);
            }

            if (annotationDef.Type == AnnotationDef.AnnotationType.number)
            {
                if (value is double doubleValue)
                {
                    return doubleValue;
                }

                if (value is float floatValue)
                {
                    return (double) floatValue;
                }

                if (value is int intValue)
                {
                    return (double) intValue;
                }
            }

            string strValue = LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, value.ToString);
            return annotationDef.ParsePersistedString(strValue);
        }

        private ColumnSelector GetColumnSelector(Type rootType, PropertyPath propertyPath)
        {
            var key = Tuple.Create(rootType, propertyPath);
            ColumnSelector columnSelector;
            lock (_columnSelectors)
            {
                if (!_columnSelectors.TryGetValue(key, out columnSelector))
                {
                    var rootColumn = ColumnDescriptor.RootColumn(SkylineDataSchema, rootType);
                    columnSelector = new ColumnSelector(rootColumn, propertyPath);
                    _columnSelectors.Add(key, columnSelector);
                }
            }

            return columnSelector;
        }
    }
}
