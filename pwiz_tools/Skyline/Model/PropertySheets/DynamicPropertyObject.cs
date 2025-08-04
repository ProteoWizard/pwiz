/*
 * Original author: Aaron Banse <acbanse .at. icloud.com>,
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

using pwiz.Common.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Resources;

namespace pwiz.Skyline.Model.PropertySheets
{
    /// <summary>
    /// Represents an object that can dynamically manage a collection of properties and their values, for use in property sheets.
    /// </summary>
    /// <remarks>
    /// This class implements <see cref="ICustomTypeDescriptor"/> to provide custom property
    /// descriptors and values, allowing for dynamic property management at runtime. This abstraction avoids having to define separate classes
    /// for each property signature, and allows for optional properties determined at runtime.
    ///
    /// ***Suggested by ChatGPT***
    /// 
    /// </remarks>
    public class DynamicPropertyObject : ICustomTypeDescriptor
    {
        private readonly List<PropertyDescriptor> _propertyDescriptors = new List<PropertyDescriptor>();
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);

        public void AddProperty(
            string name,
            Type type,
            object initialValue = null,
            string category = null,
            string displayName = null,
            bool isNested = false,
            Action<object, object> setter = null)
        {
            var descriptor = new DynamicPropertyDescriptor(
                name,
                type,
                category,
                displayName,
                isNested ? new ExpandableObjectConverter() : null,
                setter);

            _propertyDescriptors.Add(descriptor);
            _values[name] = initialValue;
        }

        public void AddPropertiesFromAnnotatedObject(object source, ResourceManager resource, bool testingUnpopulatedObject = false)
        {
            var sourceType = source.GetType();
            var props = sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public);

            foreach (var prop in props)
            {
                var propAttr = prop.GetCustomAttribute<PropertyAttribute>();
                var typeAttr = prop.GetCustomAttribute<PropertyTypeAttribute>();
                var recurseAttr = prop.GetCustomAttribute<ContainsViewablePropertiesAttribute>();
                var listAttr = prop.GetCustomAttribute<ListContainsViewablePropertiesAttribute>();
                var editableAttr = prop.GetCustomAttribute<EditableInPropertySheetAttribute>();

                // TODO: Throw errors for incorrect or redundant use of attributes
                if (propAttr != null)
                {
                    AddSingleProperty(prop, source, typeAttr, propAttr, editableAttr, resource, sourceType);
                }
                else if (recurseAttr != null)
                {
                    AddNestedProperties(prop, source, resource, testingUnpopulatedObject);
                }
                else if (listAttr != null)
                {
                    AddListProperties(prop, source, listAttr, resource, sourceType, testingUnpopulatedObject);
                }
            }
        }

        private void AddSingleProperty(PropertyInfo prop, object source, PropertyTypeAttribute typeAttr, PropertyAttribute propAttr, EditableInPropertySheetAttribute editableAttr, ResourceManager resource, Type sourceType)
        {
            var value = prop.GetValue(source);
            if (typeAttr != null)
                value = typeAttr.GetValue(value);

            var displayName = resource.GetString(prop.Name);
            if (displayName.IsNullOrEmpty())
                throw new InvalidOperationException(string.Format(
                    PropertySheetResources.Error_NoDisplayNameFound,
                    prop.Name,
                    sourceType.Name));


            Action<object, object> setter = null;
            if (editableAttr != null)
            {
                setter = (object obj, object val) => prop.SetValue(obj, val);
            }

            AddProperty(
                name: prop.Name,
                type: typeAttr != null ? typeAttr.Type : prop.PropertyType,
                initialValue: prop.GetValue(source),
                category: propAttr.Category,
                displayName: displayName,
                setter: setter
            );
        }

        private void AddNestedProperties(PropertyInfo prop, object source, ResourceManager resource, bool testingUnpopulatedObject)
        {
            var value = prop.GetValue(source);
            AddPropertiesFromAnnotatedObject(value, resource, testingUnpopulatedObject);
        }

        private void AddListProperties(PropertyInfo prop, object source, ListContainsViewablePropertiesAttribute listAttr, ResourceManager resource, Type sourceType, bool testingUnpopulatedObject)
        {
            var value = prop.GetValue(source);
            if (!(value is IEnumerable list))
                return;

            foreach (var sourceItem in list)
            {
                var propertyItem = new DynamicPropertyObject();
                propertyItem.AddPropertiesFromAnnotatedObject(sourceItem, resource, testingUnpopulatedObject);
                var displayName = resource.GetString(prop.Name);
                if (displayName.IsNullOrEmpty())
                    throw new InvalidOperationException(string.Format(
                        PropertySheetResources.Error_NoDisplayNameFound,
                        prop.Name,
                        sourceType.Name));

                AddProperty(
                    name: prop.Name,
                    type: typeof(DynamicPropertyObject),
                    initialValue: propertyItem,
                    displayName: displayName,
                    category: listAttr.Category,
                    isNested: true
                );
            }
        }

        public object GetValue(string propertyName) =>
            _values.TryGetValue(propertyName, out var value) ? value : null;

        public void SetValue(string propertyName, object value)
        {
            _values[propertyName] = value;
        }

        public override string ToString()
        {
            return ""; // or something concise and neutral like "..."
        }

        #region ICustomTypeDescriptor Implementation

        public AttributeCollection GetAttributes() => AttributeCollection.Empty;
        public string GetClassName() => null;
        public string GetComponentName() => null;
        public TypeConverter GetConverter() => new TypeConverter();
        public EventDescriptor GetDefaultEvent() => null;
        public PropertyDescriptor GetDefaultProperty() => null;
        public object GetEditor(Type editorBaseType) => null;
        public EventDescriptorCollection GetEvents(Attribute[] attributes) => EventDescriptorCollection.Empty;
        public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
            => new PropertyDescriptorCollection(_propertyDescriptors.ToArray());

        public PropertyDescriptorCollection GetProperties()
            => new PropertyDescriptorCollection(_propertyDescriptors.ToArray());

        public object GetPropertyOwner(PropertyDescriptor pd) => this;

        #endregion
    }

    /// <summary>
    /// Implements <see cref="PropertyDescriptor"/> for dynamic properties.
    /// </summary>
    public class DynamicPropertyDescriptor : PropertyDescriptor
    {
        private readonly Type _propertyType;
        private readonly string _category;
        private readonly string _displayName;
        private readonly TypeConverter _typeConverter;
        private readonly Action<object, object> _setter;

        public DynamicPropertyDescriptor(
            string name,
            Type propertyType,
            string category = null,
            string displayName = null,
            TypeConverter typeConverter = null,
            Action<object, object> setter = null)
            : base(name, null)
        {
            _propertyType = propertyType;
            _category = category;
            _displayName = displayName;
            _typeConverter = typeConverter;
            _setter = setter;
        }

        public override bool CanResetValue(object component) => false;
        public override Type ComponentType => typeof(DynamicPropertyObject);

        public override object GetValue(object component)
            => ((DynamicPropertyObject)component).GetValue(Name);

        public override bool IsReadOnly => _setter == null;
        public override Type PropertyType => _propertyType;

        public override void ResetValue(object component) { }

        public override void SetValue(object component, object value)
        {
            _setter?.Invoke(component, value);
            ((DynamicPropertyObject)component).SetValue(Name, value);

            OnValueChanged(component, EventArgs.Empty);
        }

        public override bool ShouldSerializeValue(object component) => false;

        public override string Category => _category ?? base.Category;
        public override string DisplayName => _displayName ?? Name;
        public override AttributeCollection Attributes
        {
            get
            {
                var attributes = new List<Attribute>();
                if (_typeConverter != null)
                    attributes.Add(new TypeConverterAttribute(_typeConverter.GetType()));
                return new AttributeCollection(attributes.ToArray());
            }
        }
    }
}
