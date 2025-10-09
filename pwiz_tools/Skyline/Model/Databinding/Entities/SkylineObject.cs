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
using System.ComponentModel;
using System.Linq;
using System.Resources;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;
using AttributeCollection = System.ComponentModel.AttributeCollection;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public abstract class SkylineObject : ICustomTypeDescriptor
    {
        [Browsable(false)]
        public SkylineDataSchema DataSchema
        {
            get { return GetDataSchema(); }
        }

        protected abstract SkylineDataSchema GetDataSchema();

        [Browsable(false)]
        protected SrmDocument SrmDocument
        {
            get { return DataSchema.Document; }
        }

        public virtual ElementRef GetElementRef()
        {
            return null;
        }

        public string GetLocator()
        {
            var elementRef = GetElementRef();
            if (elementRef == null)
            {
                return null;
            }
            return elementRef.ToString();
        }

        public virtual object GetAnnotation(AnnotationDef annotationDef)
        {
            return null;
        }
        public virtual void SetAnnotation(AnnotationDef annotationDef, object value)
        {
        }
        protected void ModifyDocument(EditDescription editDescription, Func<SrmDocument, SrmDocument> action)
        {
            DataSchema.ModifyDocument(editDescription, action);
        }

        protected EditDescription EditColumnDescription(string propertyName, object value)
        {
            var columnCaption = DataSchema.GetColumnCaption(DataSchema.DefaultUiMode, GetType(), propertyName);
            string auditLogParseString = AuditLogParseHelper.GetParseString(ParseStringType.column_caption, 
                columnCaption.GetCaption(DataSchemaLocalizer.INVARIANT));
            return new EditDescription(columnCaption, auditLogParseString, GetElementRef(), value);
        }

        #region PropertyGrid Support

        public virtual ResourceManager GetResourceManager() => null;

        protected virtual bool PropertyFilter(PropertyDescriptor prop) =>
            GetResourceManager()?.GetString(prop.Name) != null || prop is AnnotationPropertyDescriptor;

        protected virtual PropertyGridPropertyDescriptor PropertyTransform(PropertyDescriptor prop) =>
            new PropertyGridPropertyDescriptor(prop, GetResourceManager(),
                prop is AnnotationPropertyDescriptor annotationProperty ? annotationProperty.DisplayName : null);
        
        #endregion

        #region ICustomTypeDescriptor Implementation

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            var allProps = DataSchema.GetPropertyDescriptors(GetType());
            var filteredProps = allProps.Where(PropertyFilter);
            var transformedProps = filteredProps.Select(PropertyTransform);
            return new PropertyDescriptorCollection(transformedProps.Cast<PropertyDescriptor>().ToArray());
        }

        public PropertyDescriptorCollection GetProperties() => GetProperties(null);

        public string GetClassName() => TypeDescriptor.GetClassName(this, true);

        public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(this, true);

        public string GetComponentName() => TypeDescriptor.GetComponentName(this, true);

        public TypeConverter GetConverter() => TypeDescriptor.GetConverter(this, true);

        public EventDescriptor GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);

        public PropertyDescriptor GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this, true);

        public object GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType, true);

        public EventDescriptorCollection GetEvents(Attribute[] attributes) => TypeDescriptor.GetEvents(this, attributes, true);

        public EventDescriptorCollection GetEvents() => TypeDescriptor.GetEvents(this, true);

        public object GetPropertyOwner(PropertyDescriptor pd) => this;

        #endregion
    }

    /// <summary>
    /// <see cref="SkylineObject" /> which holds onto a reference to the <see cref="SkylineDataSchema"/>
    /// and returns it in <see cref="GetDataSchema"/>.
    /// </summary>
    public class RootSkylineObject : SkylineObject
    {
        private SkylineDataSchema _dataSchema;
        public RootSkylineObject(SkylineDataSchema dataSchema)
        {
            _dataSchema = dataSchema;
        }

        protected override SkylineDataSchema GetDataSchema()
        {
            return _dataSchema;
        }
    }
}
