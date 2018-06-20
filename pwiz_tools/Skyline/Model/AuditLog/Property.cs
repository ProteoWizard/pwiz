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

namespace pwiz.Skyline.Model.AuditLog
{
    public class Property : Immutable
    {
        private readonly TrackAttributeBase _trackAttribute;
        private readonly PropertyInfo _propertyInfo;

        public static readonly Property ROOT_PROPERTY = new Property(null, null);

        public Property(PropertyInfo propertyInfo, TrackAttributeBase trackAttribute)
        {
            _propertyInfo = propertyInfo;
            _trackAttribute = trackAttribute;
        }

        public bool IsRoot
        {
            get { return _propertyInfo == null && _trackAttribute == null; }
        }

        [Track]
        public string PropertyName
        {
            get { return _propertyInfo.Name; }
        }

        // Actual type of the property, should only be used in special cases
        [Track]
        private Type _propertyType
        {
            get { return TypeOverride ?? (_propertyInfo == null ? null : _propertyInfo.PropertyType); }
        }

        public Type TypeOverride { get; private set; }

        public bool IsCollectionElement { get { return TypeOverride != null; } }

        public Property ChangeTypeOverride(Type type)
        {
            return ChangeProp(ImClone(this), im => im.TypeOverride = type);
        }

        public bool HasPropertyInfo
        {
            get { return _propertyInfo != null; }
        }

        public bool IsTab { get { return _trackAttribute.IsTab; } }
        public bool IgnoreName { get { return _trackAttribute.IgnoreName; } }
        public bool DiffProperties { get { return _trackAttribute.DiffProperties; } }

        public CustomPropertyLocalizer CustomLocalizer
        {
            get
            {
                if (_trackAttribute == null || _trackAttribute.CustomLocalizer == null)
                    return null;

                return CustomPropertyLocalizer.CreateInstance(_trackAttribute.CustomLocalizer);
            }
        }

        public DefaultValues DefaultValues
        {
            get
            {
                if (_trackAttribute == null || _trackAttribute.DefaultValues == null)
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

        public string GetName(object rootObject, object parentObject)
        {
            var name = _propertyInfo.Name;
            if (parentObject != null)
                name = parentObject.GetType().Name + '_' + name;

            var localizer = CustomLocalizer;
            if (localizer != null && (localizer.Relative || rootObject != null))
                name = localizer.Localize(rootObject, parentObject) ?? name;

            return "{0:" + name + "}"; // Not L10N
        }

        public string GetElementName(object parentObject)
        {
            var name = _propertyInfo.Name;
            if (parentObject != null)
                name = parentObject.GetType().Name + '_' + name;

            // if resource manager doesnt have resource
            var hasName = PropertyElementNames.ResourceManager.GetString(name) != null;

            if (hasName)
                return "{1:" + name + "}"; // Not L10N

            return null;
        }

        // For Debugging
        public override string ToString()
        {
            return Reflector.ToString(this);
        }
    }
}