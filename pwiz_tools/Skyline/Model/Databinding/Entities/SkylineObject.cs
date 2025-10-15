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
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;
using AttributeCollection = System.ComponentModel.AttributeCollection;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public abstract class SkylineObject
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
    }

    /// <summary>
    /// <see cref="SkylineObject" /> which holds onto a reference to the <see cref="SkylineDataSchema"/>
    /// and returns it in <see cref="GetDataSchema"/>.
    /// </summary>
    public class RootSkylineObject : SkylineObject, ICustomTypeDescriptor
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

        #region PropertyGrid Support

        public virtual ResourceManager GetResourceManager() => null;

        protected virtual bool PropertyFilter(PropertyDescriptor prop) =>
            prop != null;

        protected virtual PropertyGridPropertyDescriptor PropertyTransform(PropertyDescriptor prop)
        {
            var transformed = new PropertyGridPropertyDescriptor(prop, GetResourceManager());
            if (prop is AnnotationPropertyDescriptor annotationProperty)
                // annotations need invariant display name as they are user-defined and have no localized resource
                transformed.SetDisplayName(annotationProperty.DisplayName);
            else
                // for non-annotation properties, set category to name of this object
                transformed.SetCategory(GetType().Name);
            return transformed;
        }

        // Override to provide a property to use as the alias for the root object, e.g. Sequence for Peptide
        protected virtual PropertyDescriptor GetRootAliasProperty() => null;

        // assumes path is depth 1 or null (for root)
        private PropertyDescriptor GetPropertyDescriptorFromPath(PropertyPath path)
        {
            // if root path used as property, must defer to object to tell what to display.
            // For example, Peptide uses Sequence as its "root" property and is displayed as "Peptide"
            if (path.Name == null && GetRootAliasProperty() != null)
                return GetRootAliasProperty();

            return TypeDescriptor.GetProperties(GetType())[path.Name ?? string.Empty];
        }

        #endregion

        #region ICustomTypeDescriptor Implementation

        public virtual PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            // Get default displayed props
            var propertyPaths = new BuiltInReports(_dataSchema.Document).GetDefaultColumns(GetType());
            if (propertyPaths == null) return new PropertyDescriptorCollection(new PropertyDescriptor[]{});
            var allProps = propertyPaths.Select(GetPropertyDescriptorFromPath);
            var filteredProps = allProps.Where(PropertyFilter).ToList();

            // Add applicable annotation props
            var annotationProps = _dataSchema.GetAnnotations(GetType()).ToList();
            filteredProps.AddRange(annotationProps);

            // Convert to PropertyGridPropertyDescriptor
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
}
