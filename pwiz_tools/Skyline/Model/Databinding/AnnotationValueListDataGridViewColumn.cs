/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Databinding
{
    public class AnnotationValueListDataGridViewColumn : BoundComboBoxColumn
    {
        protected override object[] GetDropdownItems()
        {
            var propertyDescriptor =
                ColumnPropertyDescriptor.DisplayColumn.ColumnDescriptor.ReflectedPropertyDescriptor;
            var annotationDef = (propertyDescriptor as AnnotationPropertyDescriptor)?.AnnotationDef
                                ?? (propertyDescriptor as ListColumnPropertyDescriptor)?.AnnotationDef;
            if (annotationDef == null)
            {
                return null;
            }
            return GetDropdownItems(annotationDef);
        }

        protected virtual string[] GetDropdownItems(AnnotationDef annotationDef)
        {
            annotationDef = SkylineDataSchema.Document.Settings.DataSettings.AnnotationDefs
                                .FirstOrDefault(def => def.Name == annotationDef.Name)
                            ?? annotationDef;

            return new[] { string.Empty }.Concat(annotationDef.Items).ToArray();
        }
    }
}
