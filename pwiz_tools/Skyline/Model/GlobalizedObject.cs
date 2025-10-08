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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
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
        /// Called to get the properties of a type.
        /// </summary>
        /// <param name="attributes"></param>
        /// <returns></returns>
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            /*if (this is PeptideDocNodeProperties peptideDocNodeProperties)
            {
                return new PropertyDescriptorCollection(peptideDocNodeProperties._peptide.DataSchema.GetPropertyDescriptors(peptideDocNodeProperties
                    ._peptide.GetType()).ToArray());
            }*/
            if (_globalizedProps == null)
            {
                // Get the collection of properties
                var baseProps = TypeDescriptor.GetProperties(this, attributes, true);

                _globalizedProps = new PropertyDescriptorCollection(null);

                // For each property use a property descriptor of our own that is able to be globalized
                foreach (PropertyDescriptor oProp in baseProps)
                {
                    // Only display properties whose values have been set
                    if (oProp.GetValue(this) == null)
                        continue;

                    _globalizedProps.Add(new GlobalizedPropertyDescriptor(oProp, GetResourceManager()));
                }
            }

            return _globalizedProps;
        }

        public PropertyDescriptorCollection GetProperties()
        {
            return GetProperties(null);
        }

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
                        var nestedObject = (GlobalizedObject)actualPropType.InvokeMember(actualPropType.Name, BindingFlags.Public |
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

        private readonly ResourceManager _resourceManager;
        private readonly PropertyDescriptor _basePropertyDescriptor;

        public GlobalizedPropertyDescriptor(PropertyDescriptor basePropertyDescriptor, ResourceManager resourceManager)
            : base(basePropertyDescriptor)
        {
            _basePropertyDescriptor = basePropertyDescriptor;
            _resourceManager = resourceManager;
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

        public override bool IsReadOnly => _basePropertyDescriptor.IsReadOnly;

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

    public class PropertyGridPropertyDescriptor : GlobalizedPropertyDescriptor
    {
        private readonly PropertyDescriptor _basePropertyDescriptor;

        private readonly ResourceManager _resourceManager;
        private readonly string _invariantDisplayName;

        // Since many property grid object setters don't actually change the value, just the document, need to store actual value for display
        private object _displayValue;

        public PropertyGridPropertyDescriptor(PropertyDescriptor basePropertyDescriptor, ResourceManager resourceManager, string invariantDisplayName = null)
            : base(basePropertyDescriptor, resourceManager)
        {
            Assume.IsNotNull(resourceManager);

            _basePropertyDescriptor = basePropertyDescriptor;
            _resourceManager = resourceManager;
            _invariantDisplayName = invariantDisplayName;
        }

        public override string DisplayName => _invariantDisplayName ?? _resourceManager.GetString(_basePropertyDescriptor.Name);

        public override object GetValue(object component)
        {
            if (_displayValue != null) return _displayValue;

            return _basePropertyDescriptor.GetValue(component) ?? string.Empty;
        }

        public override void SetValue(object component, object value)
        {
            _basePropertyDescriptor.SetValue(component, value);
            _displayValue = value;
        }

        public override TypeConverter Converter
        {
            get
            {
                if (_basePropertyDescriptor.PropertyType == typeof(double))
                    return new TwoDecimalDoubleConverter();

                if (_basePropertyDescriptor is AnnotationPropertyDescriptor annotationProperty &&
                    annotationProperty.AnnotationDef.Type == AnnotationDef.AnnotationType.value_list)
                    return new DropDownConverter(annotationProperty.AnnotationDef.Items.ToList());

                return _basePropertyDescriptor.Converter;
            }
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

    public class DropDownConverter : StringConverter
    {
        private readonly List<string> _options;

        public DropDownConverter(List<string> options)
        {
            _options = options;
            _options.Insert(0, null); // default is no option selected
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) => new StandardValuesCollection(_options);
    }
}
