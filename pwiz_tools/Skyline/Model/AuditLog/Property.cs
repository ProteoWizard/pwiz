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
using System.Collections.Generic;
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
        protected readonly PropertyInfoWrapper _propertyInfo;

        // Actual type of the property, should only be used in special cases
        private Type _propertyType
        {
            get { return TypeOverride ?? _propertyInfo.PropertyType; }
        }

        private Type TypeOverride { get; set; }

        public Property(PropertyInfo propertyInfo, TrackAttributeBase trackAttribute)
        {
            Assume.IsNotNull(propertyInfo);
            Assume.IsNotNull(trackAttribute);

            _propertyInfo = new PropertyInfoWrapper(propertyInfo);
            _trackAttribute = trackAttribute;
        }

        protected Property(PropertyInfoWrapper wrapper, TrackAttributeBase trackAttribute)
        {
            _propertyInfo = wrapper;
            _trackAttribute = trackAttribute;
        }

        public virtual bool IsRoot
        {
            get { return false; }
        }

        public string PropertyName
        {
            get { return _propertyInfo.Name; }
        }

        public Type DeclaringType { get { return _propertyInfo.DeclaringType; } }

        public bool IgnoreDefaultParent { get { return _trackAttribute.IgnoreDefaultParent; } }
        public bool IsTab { get { return _trackAttribute.IsTab; } }
        public bool IgnoreName { get { return _trackAttribute.IgnoreName; } }
        public bool DiffProperties { get { return _trackAttribute.DiffProperties; } }

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
                if (_trackAttribute.DefaultValues == null)
                    return null;

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

        public static Type GetPropertyType(Type type, object oldObject, object newObject)
        {
            var type1 = oldObject == null ? null : oldObject.GetType();
            var type2 = newObject == null ? null : newObject.GetType();

            // Compared based on the object type if the types are the same and not null,
            // otherwise compare by the type of the property, which is guaranteed to be a shared
            // base by both objects
            if (type1 == type2 && type1 != null)
                return type1;

            return type;
        }

        public Type GetPropertyType(object oldObject, object newObject)
        {
            return GetPropertyType(_propertyType, oldObject, newObject);
        }

        public static Type GetPropertyType(Type type, object obj)
        {
            return obj == null ? type : obj.GetType();
        }

        public Type GetPropertyType(object obj)
        {
            return GetPropertyType(_propertyType, obj);
        }

        public bool IsCollectionElement { get { return TypeOverride != null; } }

        public Property ChangeTypeOverride(Type type)
        {
            return ChangeProp(ImClone(this), im => im.TypeOverride = type);
        }

        public bool IsCollectionType(object obj)
        {
            var type = GetPropertyType(obj);
            return CollectionInfo.ForType(type) != null || type.DeclaringType == typeof(Enumerable) ||
                    (type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition()));
        }

        public object GetValue(object obj)
        {
            return _propertyInfo.GetValue(obj);
        }
        // TODO: reconsider this object pair stuff, is that really easier? and where to use it?

        public string GetName(DiffNode root, DiffNode node, DiffNode parent)
        {
            var oldGroup = new ObjectGroup(node.Objects.LastOrDefault(),
                parent != null ? parent.Objects.LastOrDefault() : null, root.Objects.LastOrDefault());

            var newGroup = new ObjectGroup(node.Objects.FirstOrDefault(),
                parent != null ? parent.Objects.FirstOrDefault() : null, root.Objects.FirstOrDefault());

            return GetName(oldGroup, newGroup);
        }

        public string GetName(ObjectGroup oldGroup, ObjectGroup newGroup)
        {
            var name = PropertyName;
            if (_propertyInfo.DeclaringType != null)
                name = _propertyInfo.DeclaringType.Name + '_' + name;

            var localizer = CustomLocalizer;
            if (localizer != null)
                name = localizer.Localize(oldGroup, newGroup) ?? name;

            return "{0:" + name + "}"; // Not L10N
        }

        public string GetElementName()
        {
            var name = PropertyName;
            if (_propertyInfo.DeclaringType != null)
                name = _propertyInfo.DeclaringType.Name + '_' + name;

            // if resource manager doesnt have resource
            var hasName = PropertyElementNames.ResourceManager.GetString(name) != null;

            if (hasName)
                return "{1:" + name + "}"; // Not L10N

            return null;
        }

        // For Debugging
        public override string ToString()
        {
            return string.Format("{0} ({1})", PropertyName, _propertyType.Name); // Not L10N
        }
    }

    public class PropertyInfoWrapper
    {
        public PropertyInfoWrapper(PropertyInfo propertyInfo) : this(propertyInfo.Name, propertyInfo.PropertyType,
            propertyInfo.DeclaringType,
            propertyInfo.GetValue)
        {
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