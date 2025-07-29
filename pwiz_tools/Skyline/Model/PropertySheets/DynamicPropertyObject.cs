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

using System;
using System.Collections.Generic;
using System.ComponentModel;

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

        private void AddProperty(
            string name,
            Type type,
            object initialValue = null,
            string category = null,
            string displayName = null,
            string description = null)
        {
            var descriptor = new DynamicPropertyDescriptor(
                name,
                type,
                category,
                displayName,
                description);

            _propertyDescriptors.Add(descriptor);
            _values[name] = initialValue;
        }

        public object GetValue(string propertyName) =>
            _values.TryGetValue(propertyName, out var value) ? value : null;

        public void SetValue(string propertyName, object value)
        {
            _values[propertyName] = value;
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
        private readonly string _description;

        public DynamicPropertyDescriptor(
            string name,
            Type propertyType,
            string category = null,
            string displayName = null,
            string description = null)
            : base(name, null)
        {
            _propertyType = propertyType;
            _category = category;
            _displayName = displayName;
            _description = description;
        }

        // Required overrides for PropertyDescriptor.
        public override bool CanResetValue(object component) => false;
        public override Type ComponentType => typeof(DynamicPropertyObject);
        public override object GetValue(object component)
            => ((DynamicPropertyObject)component).GetValue(Name);

        public override bool IsReadOnly => false;
        public override Type PropertyType => _propertyType;

        public override void ResetValue(object component) { }

        public override void SetValue(object component, object value)
        {
            ((DynamicPropertyObject)component).SetValue(Name, value);
            OnValueChanged(component, EventArgs.Empty);
        }

        public override bool ShouldSerializeValue(object component) => false;

        public override string Category => _category ?? base.Category;
        public override string DisplayName => _displayName ?? Name;
        public override string Description => _description ?? base.Description;
    }
}
