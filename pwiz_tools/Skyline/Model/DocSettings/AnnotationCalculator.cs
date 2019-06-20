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

namespace pwiz.Skyline.Model.DocSettings
{
    public class AnnotationCalculator
    {
        public const string ERROR_VALUE = @"#ERROR#";
        public const string NAME_ERROR = @"#NAME#";

        private Dictionary<Tuple<Type, string>, ColumnDescriptor> _columnDescriptors =
            new Dictionary<Tuple<Type, string>, ColumnDescriptor>();

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

        private static ColumnDescriptor ResolvePath(ColumnDescriptor rootColumn, PropertyPath propertyPath)
        {
            if (propertyPath.IsRoot)
            {
                return rootColumn;
            }

            if (propertyPath.IsLookup)
            {
                return null;
            }

            var parent = ResolvePath(rootColumn, propertyPath.Parent);
            if (parent == null)
            {
                return null;
            }

            if (propertyPath.Name.StartsWith(AnnotationDef.ANNOTATION_PREFIX))
            {
                return null;
            }
            return parent.ResolveChild(propertyPath.Name);
        }


        public object GetAnnotation<T>(AnnotationDef annotationDef, T skylineObject,
            Annotations annotations) where T : SkylineObject
        {
            return GetAnnotation(annotationDef, typeof(T), skylineObject, annotations);
        }

        public object GetAnnotation(AnnotationDef annotationDef, Type skylineObjectType,
            SkylineObject skylineObject, Annotations annotations)
        {
            if (!annotationDef.IsCalculated)
            {
                return annotations.GetAnnotation(annotationDef);
            }

            var column = GetColumnDescriptor(skylineObjectType, annotationDef.Expression);
            if (column == null)
            {
                return NAME_ERROR;
            }

            try
            {
                var value = column.GetPropertyValue(new RowItem(skylineObject), null);
                return ConvertAnnotationValue(annotationDef, value);
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
                    return floatValue;
                }

                if (value is int intValue)
                {
                    return intValue;
                }
            }

            string strValue = LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, value.ToString);
            return annotationDef.ParsePersistedString(strValue);
        }

        private ColumnDescriptor GetColumnDescriptor(Type type, string expression)
        {
            var key = Tuple.Create(type, expression);
            ColumnDescriptor columnDescriptor;
            lock (_columnDescriptors)
            {
                if (_columnDescriptors.TryGetValue(key, out columnDescriptor))
                {
                    return columnDescriptor;
                }

                PropertyPath propertyPath;
                try
                {
                    propertyPath = PropertyPath.Parse(expression);
                }
                catch (Exception)
                {
                    propertyPath = null;
                }

                if (propertyPath != null)
                {
                    var rootColumn = ColumnDescriptor.RootColumn(SkylineDataSchema, type);
                    columnDescriptor = ResolvePath(rootColumn, propertyPath);
                }
                _columnDescriptors.Add(key, columnDescriptor);
            }

            return columnDescriptor;
        }
    }
}
