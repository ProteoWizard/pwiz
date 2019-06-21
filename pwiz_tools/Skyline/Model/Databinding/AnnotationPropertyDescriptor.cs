/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding
{
    public class AnnotationPropertyDescriptor : PropertyDescriptor
    {
        private bool _isValid;
        private CachedValue<AnnotationDef> _annotationDef;
       
        public AnnotationPropertyDescriptor(SkylineDataSchema dataSchema, AnnotationDef annotationDef, bool isValid) 
            : this(dataSchema, annotationDef, GetAttributes(annotationDef))
        {
            _isValid = isValid;
            _annotationDef = CachedValue.Create(dataSchema, () => FindAnnotationDef(annotationDef));
        }

        protected AnnotationPropertyDescriptor(SkylineDataSchema dataSchema, AnnotationDef annotationDef,
            Attribute[] attributes)
            : base(AnnotationDef.ANNOTATION_PREFIX + annotationDef.Name, attributes)
        {
            SkylineDataSchema = dataSchema;
            _annotationDef = CachedValue.Create(dataSchema, () => FindAnnotationDef(annotationDef));
            _isValid = true;
        }

        public SkylineDataSchema SkylineDataSchema { get; private set; }
        public AnnotationDef AnnotationDef
        {
            get { return _annotationDef.Value; }
        }
        public override bool CanResetValue(object component)
        {
            return null != GetValue(component);
        }

        public override object GetValue(object component)
        {
            var skylineDocNode = component as SkylineObject;
            if (skylineDocNode == null)
            {
                return null;
            }

            return skylineDocNode.GetAnnotation(AnnotationDef);
        }

        public override void ResetValue(object component)
        {
            var skylineDocNode = component as SkylineObject;
            if (skylineDocNode != null)
            {
                skylineDocNode.SetAnnotation(AnnotationDef, null);
            }
        }

        public override void SetValue(object component, object value)
        {
            var skylineDocNode = component as SkylineObject;
            if (skylineDocNode != null)
            {
                skylineDocNode.SetAnnotation(AnnotationDef, value);
            }
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }

        public override Type ComponentType
        {
            get { return typeof(SkylineObject); }
        }

        public override bool IsReadOnly
        {
            get { return !_isValid || null != AnnotationDef.Expression; }
        }

        public override Type PropertyType
        {
            get { return _isValid ? AnnotationDef.ValueType : typeof(object); }
        }

        public static Attribute[] GetAttributes(AnnotationDef annotationDef)
        {
            var attributes = new List<Attribute> { new DisplayNameAttribute(annotationDef.Name) };
            if (annotationDef.Type == AnnotationDef.AnnotationType.number)
            {
                attributes.Add(new FormatAttribute { NullValue = TextUtil.EXCEL_NA });
            }
            else if (annotationDef.Type == AnnotationDef.AnnotationType.true_false)
            {
                attributes.Add(new DataGridViewColumnTypeAttribute(typeof(DataGridViewCheckBoxColumn)));
            }
            else if (annotationDef.Type == AnnotationDef.AnnotationType.value_list)
            {
                attributes.Add(new DataGridViewColumnTypeAttribute(typeof(AnnotationValueListDataGridViewColumn)));
            }
            return attributes.ToArray();
        }

        private AnnotationDef FindAnnotationDef(AnnotationDef annotationDef)
        {
            return SkylineDataSchema.Document.Settings.DataSettings.AnnotationDefs
                       .FirstOrDefault(def => def.Name == annotationDef.Name) ?? annotationDef;
        }
    }
}
