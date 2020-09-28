/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model.ElementLocators.ExportAnnotations
{
    public class TextColumnWrapper
    {
        public TextColumnWrapper(ColumnDescriptor columnDescriptor)
        {
            ColumnDescriptor = columnDescriptor;
            var importableAttribute = columnDescriptor.GetAttributes().OfType<ImportableAttribute>().FirstOrDefault();
            if (null != importableAttribute)
            {
                IsImportable = true;
                if (null != importableAttribute.Formatter)
                {
                    Formatter = (IPropertyFormatter)Activator.CreateInstance(importableAttribute.Formatter);
                }
            }
            else
            {
                IsImportable = false;
                if (columnDescriptor.PropertyType.IsValueType)
                {
                    DefaultValue = Activator.CreateInstance(columnDescriptor.PropertyType);
                }
                else
                {
                    DefaultValue = null;
                }
            }
        }
        public bool IsImportable { get; }

        public bool IsReadOnly
        {
            get { return ColumnDescriptor.IsReadOnly; }
        }

        public ColumnDescriptor ColumnDescriptor { get; private set; }

        public AnnotationDef AnnotationDef
        {
            get
            {
                var reflectedPropertyDescriptor = ColumnDescriptor.ReflectedPropertyDescriptor;
                if (reflectedPropertyDescriptor is AnnotationPropertyDescriptor annotationPropertyDescriptor)
                {
                    return annotationPropertyDescriptor.AnnotationDef;
                }
                return null;
            }
        }

        public string Name
        {
            get { return ColumnDescriptor.Name; }
        }

        public PropertyPath PropertyPath
        {
            get
            {
                return ColumnDescriptor.PropertyPath;
            }
        }

        public IPropertyFormatter Formatter { get; private set; }

        public object DefaultValue { get; private set; }

        public override string ToString()
        {
            return DisplayName;
        }

        public string DisplayName
        {
            get
            {
                return ColumnDescriptor.GetColumnCaption(ColumnCaptionType.localized);
            }
        }

        public string ValueToText(CultureInfo cultureInfo, object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (Formatter != null)
            {
                return Formatter.FormatValue(cultureInfo, value);
            }
            if (Equals(value, DefaultValue))
            {
                return string.Empty;
            }
            if (value is double d)
            {
                return d.ToString(Formats.RoundTrip, cultureInfo);
            }
            return (string)Convert.ChangeType(value, typeof(string), cultureInfo);
        }

        public object ParseTextValue(CultureInfo cultureInfo, string text)
        {
            if (Formatter != null)
            {
                return Formatter.ParseValue(cultureInfo, text);
            }
            if (string.IsNullOrEmpty(text))
            {
                return DefaultValue;
            }
            var targetType = ColumnDescriptor.PropertyType;
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                targetType = targetType.GetGenericArguments()[0];
            }
            return Convert.ChangeType(text, targetType, cultureInfo);
        }

        public object GetValue(object component)
        {
            if (component == null)
            {
                return null;
            }

            return ColumnDescriptor.GetPropertyValue(new RowItem(component), null);
        }

        public string GetTextValue(CultureInfo cultureInfo, object component)
        {
            return ValueToText(cultureInfo, GetValue(component));
        }

        public void SetValue(object component, object value)
        {
            ColumnDescriptor.SetValue(new RowItem(component), null, value);
        }

        public void SetTextValue(CultureInfo cultureInfo, object component, string text)
        {
            SetValue(component, ParseTextValue(cultureInfo, text));
        }
    }
}