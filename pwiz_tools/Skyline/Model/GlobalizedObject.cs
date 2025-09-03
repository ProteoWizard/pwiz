/*
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

using Newtonsoft.Json;
using JetBrains.Annotations;
using pwiz.Skyline.Model.DocSettings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;

namespace pwiz.Skyline.Model
{
    public class UseToCompare : Attribute
    {
        public bool IsUsed { get; set; }
        public static readonly UseToCompare YES = new UseToCompare(true);
        public static readonly UseToCompare NO = new UseToCompare(false);

        public UseToCompare(bool isUsed)
        {
            IsUsed = isUsed;
        }
    }

    /// <summary>
    /// GlobalizedObject implements ICustomTypeDescriptor to enable 
    /// required functionality to describe a type (class).<br></br>
    /// The main task of this class is to instantiate our own property descriptor 
    /// of type GlobalizedPropertyDescriptor.  
    /// </summary>
    public abstract class GlobalizedObject : ICustomTypeDescriptor
    {
        private PropertyDescriptorCollection _globalizedProps;

        private static readonly Dictionary<string, MethodInfo> TYPE_CONVERTER_DICTIONARY;

        private static string GetConverterKey(Type fromType, Type toType)
        {
            return fromType.FullName + @" " + toType.FullName;
        }
        static GlobalizedObject()
        {
            //Initialize the dictionary of type conversion methods.
            var methodList = typeof(Convert).GetMethods()
                .Where(method => method.GetParameters().Length == 1 && method.Name.StartsWith(@"To")).ToList();
            TYPE_CONVERTER_DICTIONARY = new Dictionary<string, MethodInfo>();
            foreach (var method in methodList)
            {
                var methodKey = GetConverterKey(method.GetParameters().First().ParameterType, method.ReturnType);
                if(!TYPE_CONVERTER_DICTIONARY.ContainsKey(methodKey))
                    TYPE_CONVERTER_DICTIONARY.Add(methodKey, method);
            }
        }

        protected abstract ResourceManager GetResourceManager();
        
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

        /// <summary>
        /// Gets the base property descriptor of this object by name. Note that GetProperties(GetType()) returns the base properties,
        /// not the properties returned by GetProperties() in this class.
        /// Used by derived classes to get the base property descriptor when adding custom properties.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public PropertyDescriptor GetBaseDescriptorByName(string name) => TypeDescriptor.GetProperties(GetType()).Find(name, false);

        /// <summary>
        /// Called to get the properties of a type.
        /// </summary>
        /// <param name="attributes"></param>
        /// <returns></returns>
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            if (_globalizedProps == null)
            {
                // Get the collection of properties
                var baseProps = TypeDescriptor.GetProperties(this, attributes, true);

                _globalizedProps = new PropertyDescriptorCollection(null);


                // For each property use a property descriptor of our own that is able to be globalized
                foreach (PropertyDescriptor oProp in baseProps)
                {
                    // don't copy over properties that require custom handling
                    if (oProp.Attributes[typeof(UseCustomHandlingAttribute)] != null || 
                        oProp.Attributes[typeof(UsedImplicitlyAttribute)] != null ||
                        oProp.Attributes[typeof(EditablePropertyAttribute)] != null)
                        continue;
                    
                    // Only display properties whose values have been set
                    if (oProp.GetValue(this) == null)
                        continue;

                    _globalizedProps.Add(new GlobalizedPropertyDescriptor(oProp, GetResourceManager()));
                }

                AddCustomizedProperties();
            }

            return _globalizedProps;
        }

        public PropertyDescriptorCollection GetProperties()
        {
            return GetProperties(null);
        }

        protected void AddProperty(PropertyDescriptor propertyDescriptor)
        {
            _globalizedProps.Add(propertyDescriptor);
        }

        /// <summary>
        /// Override this method to add custom properties to the property collection, like if we needed to dynamically add nested properties.
        /// </summary>
        protected virtual void AddCustomizedProperties() {}

        #region Test suppport

        public List<string> GetDifference(GlobalizedObject other)
        {
            if(other == null)
                return new List<string> { @"The other object is null." };
            if (GetType() != other.GetType())
                return new List<string> { $@"The other object of of type {other.GetType().Name}." };
            if (GetPropertiesForComparison().Count != other.GetPropertiesForComparison().Count)
                return new List<string> { $@"This count is {GetPropertiesForComparison().Count}, but other count is {other.GetPropertiesForComparison().Count}" };

            var thisProps = GetPropertiesForComparison()
                .ToDictionary(prop => prop.Name, prop => prop.GetValue(this));
            var otherProps = other.GetPropertiesForComparison()
                .ToDictionary(prop => prop.Name, prop => prop.GetValue(other));

            var joinedValues = (from t in thisProps
                join o in otherProps on t.Key equals o.Key
                select new {k = t.Key, t = t.Value, o = o.Value }).ToList();
            if (joinedValues.Count != thisProps.Count)
                return new List<string> { @"The two objects have different sets of properties." };
            var res = joinedValues.Where(tuple =>
            {
                if(tuple.t is GlobalizedObject tg && tuple.o is GlobalizedObject to)
                    return !tg.IsSameAs(to);
                return !tuple.t.Equals(tuple.o);
            });
            return res.Select(r => $@"Key:{r.k}, this value:{r.t}, other value:{r.o}").ToList();
        }

        public bool IsSameAs(GlobalizedObject other)
        {
            return !GetDifference(other).Any();
        }

        public List<PropertyDescriptor> GetPropertiesForComparison()
        {
            return GetProperties().Cast<PropertyDescriptor>().Where(prop => !prop.Attributes.Contains(UseToCompare.NO)).ToList();
        }


        public string Serialize()
        {
            var sw = new StringWriter();
            SerializeToDictionary(sw);
            return sw.ToString();
        }

        private void SerializeToJson(JsonWriter writer)
        {
            var thisProps = GetProperties().Cast<PropertyDescriptor>()
                .Where(prop => !prop.Attributes.Contains(UseToCompare.NO) && prop.GetValue(this) != null)
                .ToDictionary(prop => prop.Name, prop => prop.GetValue(this));

            writer.WriteStartObject();
            foreach (var propName in thisProps.Keys)
            {
                writer.WritePropertyName(propName);
                if (thisProps[propName] is GlobalizedObject nested)
                    nested.SerializeToJson(writer);
                else
                    writer.WriteValue(thisProps[propName]);
            }
            writer.WriteEndObject();
        }

        private void SerializeToDictionary(StringWriter sw)
        {
            var thisProps = GetProperties().Cast<PropertyDescriptor>()
                .Where(prop => !prop.Attributes.Contains(UseToCompare.NO) && prop.GetValue(this) != null)
                .Select(prop => new {name = prop.Name, val = prop.GetValue(this)}).ToList();
            sw.WriteLine(@"new Dictionary<string, object> {");
            for(int i = 0; i < thisProps.Count; i++)
            {
                sw.Write(@"{");
                sw.Write('"' + thisProps[i].name + '"');
                sw.Write(',');
                if (thisProps[i].val is GlobalizedObject nested)
                    nested.SerializeToDictionary(sw);
                else
                    sw.Write('"' + thisProps[i].val.ToString() + '"');
                if(i < thisProps.Count - 1)
                    sw.WriteLine(@"},");
                else
                    sw.WriteLine(@"}");

            }
            sw.WriteLine(@"}");
        }

        public void Deserialize(Dictionary<string, object> valueDict)
        {
            if(valueDict == null)
                return;

            var propDict = GetType().GetProperties().ToDictionary(prop => prop.Name, prop => prop);
            foreach (var val in valueDict)
            {
                if (propDict.ContainsKey(val.Key) && val.Value != null)
                {
                    var actualPropType = propDict[val.Key].PropertyType;
                    var converterKey = GetConverterKey(val.Value.GetType(), actualPropType);
                    if (actualPropType.BaseType == typeof(GlobalizedObject) && val.Value is Dictionary<string, object> nestedDictionary)
                    {
                        var nestedObject = (GlobalizedObject) actualPropType.InvokeMember(actualPropType.Name, BindingFlags.Public |
                            BindingFlags.Instance |
                            BindingFlags.CreateInstance,
                            null, null, new object[] { });
                        nestedObject.Deserialize(nestedDictionary);
                        propDict[val.Key].SetValue(this, nestedObject);
                    }
                    else
                    {
                        if (!TYPE_CONVERTER_DICTIONARY.ContainsKey(converterKey) && propDict[val.Key].PropertyType.Name.StartsWith(@"Nullable"))
                        {
                            actualPropType = propDict[val.Key].PropertyType.GetGenericArguments()[0];
                            converterKey = GetConverterKey(val.Value.GetType(), actualPropType);
                        }
                        if (TYPE_CONVERTER_DICTIONARY.TryGetValue(converterKey, out var parseMethod))
                        {
                            var value = parseMethod.Invoke(this, new[] { val.Value });
                            propDict[val.Key].SetValue(this, value);
                        }
                        else
                            propDict[val.Key].SetValue(this, null);
                    }
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// GlobalizedPropertyDescriptor enhances the base class bay obtaining the display name for a property
    /// from the resource.
    ///
    /// The classes in RefineInputLocalization.cs were modified from Descriptors.cs at:
    ///     https://www.codeproject.com/script/Articles/ViewDownloads.aspx?aid=2138
    ///
    /// </summary>
    public class GlobalizedPropertyDescriptor : PropertyDescriptor
    {
        private const string DESCRIPTION_PREFIX = @"Description_";
        private const string CATEGORY_PREFIX = @"Category_";

        private readonly PropertyDescriptor _basePropertyDescriptor;
        private readonly ResourceManager _resourceManager;

        private readonly Func<SrmDocument, SrmSettingsChangeMonitor, string, ModifiedDocument> _getModifiedDocument;

        public GlobalizedPropertyDescriptor(PropertyDescriptor basePropertyDescriptor, ResourceManager resourceManager,
            Func<SrmDocument, SrmSettingsChangeMonitor, string, ModifiedDocument> getModifiedDocument = null) 
            : base(basePropertyDescriptor)
        {
            _basePropertyDescriptor = basePropertyDescriptor;
            _resourceManager = resourceManager;
            _getModifiedDocument = getModifiedDocument;
        }

        public ModifiedDocument GetModifiedDocument(SrmDocument document, SrmSettingsChangeMonitor monitor, string newValue)
        {
            return _getModifiedDocument?.Invoke(document, monitor, newValue);
        }

        public override bool CanResetValue(object component)
        {
            return _basePropertyDescriptor.CanResetValue(component);
        }

        public override Type ComponentType => _basePropertyDescriptor.ComponentType;

        public override string DisplayName
        {
            get
            {
                // Get display name from CommandArgName
                var displayNameKey = _basePropertyDescriptor.Name;
                return _resourceManager.GetString(displayNameKey);
            }
        }
        
        public override string Description => _resourceManager.GetString(DESCRIPTION_PREFIX + _basePropertyDescriptor.Name) ?? string.Empty;

        public override string Category
        {
            get
            {
                if (_basePropertyDescriptor.Category != null)
                {
                    return _resourceManager.GetString(CATEGORY_PREFIX + _basePropertyDescriptor.Category) ?? string.Empty;
                }

                return null;
            }
        }

        public override object GetValue(object component)
        {
            // Doesn't display default values to highlight changed ones
            var value = _basePropertyDescriptor.GetValue(component);
            if (value == null || (value is bool b && !b))
                return string.Empty;

            return value;
        }

        public override bool IsReadOnly => _getModifiedDocument == null;

        public override string Name => _basePropertyDescriptor.Name;

        public override Type PropertyType => _basePropertyDescriptor.PropertyType;

        public override TypeConverter Converter => _basePropertyDescriptor.PropertyType == typeof(double) ? new TwoDecimalDoubleConverter() : _basePropertyDescriptor.Converter;

        public override void ResetValue(object component)
        {
            _basePropertyDescriptor.ResetValue(component);
        }

        public override bool ShouldSerializeValue(object component)
        {
            return _basePropertyDescriptor.ShouldSerializeValue(component);
        }

        public override void SetValue(object component, object value)
        {
            _basePropertyDescriptor.SetValue(component, value);
        }
    }

    /// <summary>
    /// Used to support properties returned in GetProperties() that are not present on the globalized object, or need custom handling.
    /// </summary>
    public class CustomHandledGlobalizedPropertyDescriptor : PropertyDescriptor
    {
        private const string DESCRIPTION_PREFIX = @"Description_";
        private const string CATEGORY_PREFIX = @"Category_";

        private readonly ResourceManager _resourceManager;
        private object _value;
        private readonly string _category;
        private readonly string _name;
        private readonly Type _type;
        private readonly string _nonLocalizedDisplayName;
        private readonly Func<string, string> _displayNameFormat;

        public CustomHandledGlobalizedPropertyDescriptor(
            Type type, string name, object value, string category,
            ResourceManager resourceManager, Attribute[] attributes = null,
            string nonLocalizedDisplayName = null, Func<string, string> displayNameFormat = null,
            Func<SrmDocument, SrmSettingsChangeMonitor, object, ModifiedDocument> getModifiedDocument = null) 
            : base(name, attributes)
        {
            _resourceManager = resourceManager;
            _value = value;
            _category = category;
            _name = name;
            _type = type;
            _nonLocalizedDisplayName = nonLocalizedDisplayName;
            _displayNameFormat = displayNameFormat;
        }

        public override bool CanResetValue(object component) => false;

        public override Type ComponentType => _type;

        public override string DisplayName
        {
            get
            {
                var displayName = _resourceManager.GetString(_name);

                if (_displayNameFormat != null && displayName != null)
                    displayName = _displayNameFormat(displayName);

                return displayName ?? _nonLocalizedDisplayName ?? string.Empty;
            }
        }

        public override string Description => _resourceManager.GetString(DESCRIPTION_PREFIX + _name) ?? string.Empty;

        public override string Category
        {
            get
            {
                if (_category != null)
                {
                    return _resourceManager.GetString(CATEGORY_PREFIX + _category) ?? string.Empty;
                }

                return null;
            }
        }

        public override object GetValue(object component) => _value;

        public override bool IsReadOnly => true;

        public override string Name => _name;

        public override Type PropertyType => _type;

        public override void ResetValue(object component) { }

        public override bool ShouldSerializeValue(object component) => false;

        public override void SetValue(object component, object value)
        {
            _value = value;
        }
    }

    public class TwoDecimalDoubleConverter : DoubleConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is double d)
                return d.ToString("F2", culture ?? CultureInfo.CurrentCulture);
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    // Used to flag properties that should not be copied directly to the globalized object, as they require custom handling.
    [AttributeUsage(AttributeTargets.Property)]
    public class UseCustomHandlingAttribute : Attribute { }

    // Used to flag properties that are editable.
    [AttributeUsage(AttributeTargets.Property)]
    public class EditablePropertyAttribute : Attribute { }
}
