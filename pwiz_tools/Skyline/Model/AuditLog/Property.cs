/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Linq;
using System.Reflection;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AuditLog
{
    public class Property : Immutable
    {
        protected readonly TrackAttributeBase _trackAttribute;

        // Actual type of the property, should only be used in special cases
        private Type _propertyType
        {
            get { return TypeOverride ?? PropertyInfo.PropertyType; }
        }

        private Type TypeOverride { get; set; }

        public Property(PropertyInfo propertyInfo, TrackAttributeBase trackAttribute)
        {
            Assume.IsNotNull(propertyInfo);
            Assume.IsNotNull(trackAttribute);

            PropertyInfo = new PropertyInfoWrapper(propertyInfo);
            _trackAttribute = trackAttribute;
        }

        // Only to be accessed from this class or tests
        public PropertyInfoWrapper PropertyInfo { get; private set; }

        protected Property(PropertyInfoWrapper wrapper, TrackAttributeBase trackAttribute)
        {
            PropertyInfo = wrapper;
            _trackAttribute = trackAttribute;
        }

        public virtual bool IsRoot
        {
            get { return false; }
        }

        public string PropertyName
        {
            get { return PropertyInfo.Name; }
        }

        public Type DeclaringType { get { return PropertyInfo.DeclaringType; } }

        public bool IgnoreDefaultParent { get { return _trackAttribute.IgnoreDefaultParent; } }
        public bool IsTab { get { return _trackAttribute.IsTab; } }
        public bool IgnoreName { get { return _trackAttribute.IgnoreName; } }
        public bool DiffProperties { get { return _trackAttribute.DiffProperties; } }

        public int? DecimalPlaces
        {
            get
            {
                if (_trackAttribute.DecimalPlaces == -1)
                    return null;
                return _trackAttribute.DecimalPlaces;
            }
        }

        public CustomPropertyLocalizer CustomLocalizer
        {
            get
            {
                if (_trackAttribute.CustomLocalizer == null)
                    return null;

                return CustomPropertyLocalizer.CreateInstance(_trackAttribute.CustomLocalizer);
            }
        }

        public DefaultValues DefaultValues
        {
            get
            {
                return DefaultValues.CreateInstance(_trackAttribute.DefaultValues);
            }
        }

        public PropertyPath AddProperty(PropertyPath path)
        {
            return path.Property(PropertyName);
        }

        // These functions get the most derived, common type of the given objects
        // For instance, if the property type is DocNode, the oldObject a PeptideDocNode and
        // the newObject a PeptideGroupDocNode we only compare based on the DocNode properties. If the newObject
        // was also a PeptideDocNode, we compare based on PeptideDocNode properties.

        public static Type GetPropertyType(Type type, ObjectPair<object> objectPair)
        {
            var type1 = objectPair.OldObject == null ? null : objectPair.OldObject.GetType();
            var type2 = objectPair.NewObject == null ? null : objectPair.NewObject.GetType();

            // Compare based on the object type if the types are the same and not null,
            // otherwise compare by the type of the property, which is guaranteed to be a shared
            // base by both objects
            if ((type1 == type2 || type2 == null) && type1 != null)
                return type1;

            if (type1 == null && type2 != null)
                return type2;

            return type;
        }

        public Type GetPropertyType(ObjectPair<object> objectPair)
        {
            return GetPropertyType(_propertyType, objectPair);
        }

        public Type GetPropertyType(object obj)
        {
            return obj == null ? _propertyType : obj.GetType();
        }

        // Rather use GetPropertyType unless the type the property was declared as is actually required,
        // or there is no instance of this type
        public Type PropertyType
        {
            get { return _propertyType; }
        }

        public bool IsCollectionElement { get { return TypeOverride != null; } }

        public Property ChangeTypeOverride(Type type)
        {
            return ChangeProp(ImClone(this), im => im.TypeOverride = type);
        }

        public bool IsCollectionType(object obj)
        {
            var type = GetPropertyType(obj);
            return Reflector.IsCollectionType(type);
        }

        public object GetValue(object obj)
        {
            return PropertyInfo.GetValue(obj);
        }

        public string GetName(DiffNode root, DiffNode node, DiffNode parent)
        {
            return GetName(ObjectPair.Create(root.Objects.LastOrDefault(), root.Objects.FirstOrDefault()), node, parent);
        }

        public string GetName(ObjectPair<object> docPair, DiffNode node, DiffNode parent)
        {
            return GetName(new ObjectInfo<object>(node.Objects.LastOrDefault(), node.Objects.FirstOrDefault(),
                parent != null ? parent.Objects.LastOrDefault() : null, parent != null ? parent.Objects.FirstOrDefault() : null,
                docPair.OldObject, docPair.NewObject));
        }

        public string GetName(ObjectInfo<object> objectInfo)
        {
            var name = PropertyName;
            if (PropertyInfo.DeclaringType != null)
                name = PropertyInfo.DeclaringType.Name + '_' + name;

            var localizer = CustomLocalizer;
            if (localizer != null)
            {
                name = localizer.Localize(objectInfo);
                if (PropertyInfo.DeclaringType != null)
                    name = PropertyInfo.DeclaringType.Name + '_' + name;
            }
                

            return AuditLogParseHelper.GetParseString(ParseStringType.property_names, name);
        }

        public string GetElementName()
        {
            var name = PropertyName;
            if (PropertyInfo.DeclaringType != null)
                name = PropertyInfo.DeclaringType.Name + '_' + name;

            // if resource manager doesnt have resource
            var hasName = PropertyElementNames.ResourceManager.GetString(name) != null;

            if (hasName)
                return AuditLogParseHelper.GetParseString(ParseStringType.property_element_names, name);

            return null;
        }

        // For Debugging
        public override string ToString()
        {
            return string.Format(@"{0} ({1})", PropertyName, _propertyType.Name);
        }
    }

    public class PropertyInfoWrapper
    {
        public PropertyInfoWrapper(PropertyInfo propertyInfo) : this(propertyInfo.Name, propertyInfo.PropertyType,
            propertyInfo.DeclaringType,
            propertyInfo.GetValue)
        {
            // For override properties, get the declaring type
            var accessors = propertyInfo.GetAccessors(true);
            if (accessors.Length > 0)
            {
                var baseDef = accessors[0].GetBaseDefinition();
                if (baseDef.DeclaringType != null)
                    DeclaringType = baseDef.DeclaringType;    
            }

            PropertyInfo = propertyInfo;
        }

        public PropertyInfoWrapper(string name, Type propertyType, Type declaringType, Func<object, object> getValue)
        {
            Name = name;
            PropertyType = propertyType;
            DeclaringType = declaringType;
            GetValue = getValue;
        }

        public string Name { get; set; }
        public Type PropertyType { get; set; }
        public Type DeclaringType { get; set; }
        public Func<object, object> GetValue { get; set; }

        // For debugging
        // ReSharper disable once NotAccessedField.Global
        public PropertyInfo PropertyInfo { get; private set; }
    }

    public class RootProperty : Property
    {
        private RootProperty(string name, Type type, bool trackChildren)
            : base(new PropertyInfoWrapper(name, type, null, obj => null),
                trackChildren ? (TrackAttributeBase) new TrackChildrenAttribute() : new TrackAttribute())
        {
        }

        public override bool IsRoot
        {
            get { return true; }
        }

        public static RootProperty Create(Type type, string name = null, bool trackChildren = true)
        {
            return new RootProperty(name ?? type.Name, type, trackChildren);
        }
    }
}
