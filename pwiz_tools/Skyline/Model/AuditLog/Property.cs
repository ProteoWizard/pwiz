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
using System.Reflection;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.AuditLog
{
    public class Property
    {
        private readonly TrackAttributeBase _trackAttribute;

        public static readonly Property ROOT_PROPERTY = new Property(null, null);

        public Property(PropertyInfo propertyInfo, TrackAttributeBase trackAttribute)
        {
            PropertyInfo = propertyInfo;
            _trackAttribute = trackAttribute;
        }

        public PropertyInfo PropertyInfo { get; private set; }
        [Track]
        public bool IsRoot { get { return PropertyInfo == null && _trackAttribute == null; } }

        public string GetName(object rootObject, object parentObject)
        {
            var name = PropertyInfo.Name;
            if (parentObject != null)
                name = parentObject.GetType().Name + '_' + name;

            if (_trackAttribute.CustomLocalizer != null)
            {
                var localizer = CustomPropertyLocalizer.CreateInstance(_trackAttribute.CustomLocalizer);
                if (localizer.Relative || rootObject != null)
                    name = localizer.Localize(rootObject, parentObject) ?? name;
            }

            return "{0:" + name + "}"; // Not L10N
        }

        public string GetElementName(object parentObject)
        {
            var name = PropertyInfo.Name;
            if (parentObject != null)
                name = parentObject.GetType().Name + '_' + name;

            // if resource manager doesnt have resource
            var hasName = PropertyElementNames.ResourceManager.GetString(name) != null;

            if (hasName)
                return "{1:" + name + "}"; // Not L10N

            return null;
        }

        [Track]
        public bool IsTab { get { return _trackAttribute.IsTab; } }
        [Track]
        public bool IgnoreName { get { return _trackAttribute.IgnoreName; } }
        [Track]
        public bool DiffProperties { get { return _trackAttribute.DiffProperties; } }
        [Track]
        public Type CustomLocalizer { get { return _trackAttribute.CustomLocalizer; } }

        // For Debugging
        public override string ToString()
        {
            return Reflector<Property>.ToString(this);
        }
    }
}