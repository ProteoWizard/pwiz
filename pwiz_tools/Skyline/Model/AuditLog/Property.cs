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
    public class Property
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
        public Type PropertyType
        {
            get { return _propertyInfo.PropertyType; }
        }

        public bool HasPropertyInfo
        {
            get { return _propertyInfo != null; }
        }

        public bool IsTab { get { return _trackAttribute.IsTab; } }
        public bool IgnoreName { get { return _trackAttribute.IgnoreName; } }
        public bool DiffProperties { get { return _trackAttribute.DiffProperties; } }
        public Type CustomLocalizer { get { return _trackAttribute.CustomLocalizer; } }

        public object GetValue(object obj)
        {
            return _propertyInfo.GetValue(obj);
        }

        public PropertyPath AddProperty(PropertyPath path)
        {
            return path.Property(_propertyInfo.Name);
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
            return GetPropertyType(_propertyInfo.PropertyType, oldObject, newObject);
        }

        public static Type GetPropertyType(Type type, object obj)
        {
            return obj == null ? type : obj.GetType();
        }

        public Type GetPropertyType(object obj)
        {
            return GetPropertyType(_propertyInfo.PropertyType, obj);
        }


        public bool IsCollectionType()
        {
            var type = _propertyInfo.PropertyType;
            return CollectionInfo.ForType(type) != null || type.DeclaringType == typeof(Enumerable) ||
                   (type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition()));
        }

        public string GetName(object rootObject, object parentObject)
        {
            var name = _propertyInfo.Name;
            if (parentObject != null)
                name = parentObject.GetType().Name + '_' + name;

            if (_trackAttribute != null && _trackAttribute.CustomLocalizer != null)
            {
                var localizer = CustomPropertyLocalizer.CreateInstance(_trackAttribute.CustomLocalizer);
                if (localizer.Relative || rootObject != null)
                    name = localizer.Localize(rootObject, parentObject) ?? name;
            }

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
            return Reflector<Property>.ToString(this);
        }
    }
}