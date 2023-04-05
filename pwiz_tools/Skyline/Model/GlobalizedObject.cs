using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using Newtonsoft.Json;

namespace pwiz.Skyline.Model
{
    public class UseToCompare : Attribute
    {
        public bool IsUsed { get; set; }
        public static readonly UseToCompare Yes = new UseToCompare(true);
        public static readonly UseToCompare No = new UseToCompare(false);

        public UseToCompare(bool isUsed)
        {
            IsUsed = isUsed;
            BrowsableAttribute myAttribute = BrowsableAttribute.Yes;
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
        private PropertyDescriptorCollection globalizedProps;

        private static Dictionary<string, MethodInfo> TypeConverterDictionary;

        private static string GetConverterKey(Type fromType, Type toType)
        {
            return fromType.FullName + @" " + toType.FullName;
        }
        static GlobalizedObject()
        {
            //Initialize the dictionary of type conversion methods.
            var methodList = typeof(Convert).GetMethods()
                .Where(method => method.GetParameters().Length == 1 && method.Name.StartsWith(@"To")).ToList();
            TypeConverterDictionary = new Dictionary<string, MethodInfo>();
            foreach (var method in methodList)
            {
                var methodKey = GetConverterKey(method.GetParameters().First().ParameterType, method.ReturnType);
                if(!TypeConverterDictionary.ContainsKey(methodKey))
                    TypeConverterDictionary.Add(methodKey, method);
            }
        }

        protected abstract ResourceManager GetResourceManager();
        
        public String GetClassName() => TypeDescriptor.GetClassName(this, true);

        public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(this, true);

        public String GetComponentName() => TypeDescriptor.GetComponentName(this, true);

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
            if (globalizedProps == null)
            {
                // Get the collection of properties
                PropertyDescriptorCollection baseProps = TypeDescriptor.GetProperties(this, attributes, true);

                globalizedProps = new PropertyDescriptorCollection(null);

                // For each property use a property descriptor of our own that is able to be globalized
                foreach (PropertyDescriptor oProp in baseProps)
                {
                    // Only display properties whose values have been set
                    if (oProp.GetValue(this) != null)
                    {
                        globalizedProps.Add(new GlobalizedPropertyDescriptor(oProp, GetResourceManager()));
                    }
                }
            }
            return globalizedProps;
        }

        public PropertyDescriptorCollection GetProperties()
        {
            // Only do once
            if (globalizedProps == null)
            {
                // Get the collection of properties
                PropertyDescriptorCollection baseProps = TypeDescriptor.GetProperties(this, true);
                globalizedProps = new PropertyDescriptorCollection(null);

                // For each property use a property descriptor of our own that is able to be globalized
                foreach (PropertyDescriptor oProp in baseProps)
                {
                    // Only display properties whose values have been set
                    if (oProp.GetValue(this) != null)
                    {
                        globalizedProps.Add(new GlobalizedPropertyDescriptor(oProp, GetResourceManager()));
                    }
                }
            }
            return globalizedProps;
        }

        #region Test suppport

        public bool IsSameAs(GlobalizedObject other)
        {
            if(other == null)
                return false;
            if(this.GetType() != other.GetType())
                return false;
            if(GetPropertiesForComparison().Count != other.GetPropertiesForComparison().Count)
                return false;
            var thisProps = GetPropertiesForComparison()
                .ToDictionary(prop => prop.Name, prop => prop.GetValue(this));
            var otherProps = other.GetPropertiesForComparison()
                .ToDictionary(prop => prop.Name, prop => prop.GetValue(other));

            var joinedValues = (from t in thisProps
                join o in otherProps on t.Key equals o.Key
                select new { t = t.Value, o = o.Value }).ToList();
            if (joinedValues.Count != thisProps.Count)
                return false;
            var res = joinedValues.Where(tuple =>
            {
                if(tuple.t is GlobalizedObject tg && tuple.o is GlobalizedObject to)
                    return !tg.IsSameAs(to);
                return !tuple.t.Equals(tuple.o);
            });
            return !res.Any();
        }

        public List<PropertyDescriptor> GetPropertiesForComparison()
        {
            return GetProperties().Cast<PropertyDescriptor>().Where(prop => !prop.Attributes.Contains(UseToCompare.No)).ToList();
        }


        public string Serialize()
        {
            StringWriter sw = new StringWriter();
            SerializeToDictionary(sw);
            return sw.ToString();
        }

        private void SerializeToJson(JsonWriter writer)
        {
            var thisProps = GetProperties().Cast<PropertyDescriptor>()
                .Where(prop => !prop.Attributes.Contains(UseToCompare.No) && prop.GetValue(this) != null)
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
                .Where(prop => !prop.Attributes.Contains(UseToCompare.No) && prop.GetValue(this) != null)
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
                        if (!TypeConverterDictionary.ContainsKey(converterKey) && propDict[val.Key].PropertyType.Name.StartsWith(@"Nullable"))
                        {
                            actualPropType = propDict[val.Key].PropertyType.GetGenericArguments()[0];
                            converterKey = GetConverterKey(val.Value.GetType(), actualPropType);
                        }
                        if (TypeConverterDictionary.ContainsKey(converterKey))
                        {
                            var parseMethod = TypeConverterDictionary[converterKey];
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
        private readonly PropertyDescriptor basePropertyDescriptor;
        public bool ReadOnly = true;
        private static string _descriptionPrefix = @"Description_";
        private static string _categoryPrefix = @"Category_";
        private readonly ResourceManager _resourceManager;

        public GlobalizedPropertyDescriptor(PropertyDescriptor basePropertyDescriptor, ResourceManager resourceManager) : base(basePropertyDescriptor)
        {
            this.basePropertyDescriptor = basePropertyDescriptor;
            _resourceManager = resourceManager;
        }

        public override bool CanResetValue(object component)
        {
            return basePropertyDescriptor.CanResetValue(component);
        }

        public override Type ComponentType
        {
            get => basePropertyDescriptor.ComponentType;
        }

        public override string DisplayName
        {
            get
            {
                // Get display name from CommandArgName
                var displayNameKey = basePropertyDescriptor.Name;
                return _resourceManager.GetString(displayNameKey);
            }
        }
        
        public override string Description
        {
            get
            {
                return _resourceManager.GetString(_descriptionPrefix + basePropertyDescriptor.Name) ?? string.Empty;
            }
        }
        
        public override string Category
        {
            get
            {
                if (basePropertyDescriptor.Category != null)
                {
                    return _resourceManager.GetString(_categoryPrefix + basePropertyDescriptor.Category) ?? string.Empty;
                }

                return null;
            }
        }

        public override object GetValue(object component)
        {
            // Doesn't display default values to highlight changed ones
            var value = basePropertyDescriptor.GetValue(component);
            if (value == null)
                return string.Empty;
            if (value is bool && !(bool)value)
                return string.Empty;

            return value;
        }

        public override bool IsReadOnly
        {
            get => ReadOnly;
        }

        public override string Name
        {
            get => basePropertyDescriptor.Name;
        }

        public override Type PropertyType
        {
            get => basePropertyDescriptor.PropertyType;
        }

        public override void ResetValue(object component)
        {
            basePropertyDescriptor.ResetValue(component);
        }

        public override bool ShouldSerializeValue(object component)
        {
            return basePropertyDescriptor.ShouldSerializeValue(component);
        }

        public override void SetValue(object component, object value)
        {
            basePropertyDescriptor.SetValue(component, value);
        }
    }
}
