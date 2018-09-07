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
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.AuditLog
{
    /// <summary>
    /// Used for properties where the name in the audit log depends on something
    /// else, such as a checkbox or a combo box. For instance, ProductRes in TransitionFullScan
    /// can be either mass accuracy or resolving power
    /// </summary>
    public abstract class CustomPropertyLocalizer
    {
        protected CustomPropertyLocalizer(PropertyPath path, bool relative)
        {
            Path = path;
            Relative = relative;
        }

        private string[] PropertyPathToArray(PropertyPath path)
        {
            return path.IsRoot
                ? new string[0]
                : PropertyPathToArray(path.Parent).Concat(ImmutableList.Singleton(path.Name)).ToArray();
        }

        protected object FindObjectByPath(string[] pathArray, int index, object obj)
        {
            if (index == pathArray.Length)
                return obj;

            // Also allow properties that don't have track attributes
            foreach (var property in obj.GetType().GetProperties())
            {
                if (property.Name == pathArray[index])
                {
                    var val = property.GetValue(obj);
                    return FindObjectByPath(pathArray, ++index, val);
                }         
            }

            return 0;
        }

        private object FindObject(ObjectGroup<object> objectGroup)
        {
            if (objectGroup == null)
                return null;

            var pathArrary = PropertyPathToArray(Path);

            if (!Relative)
            {
                return objectGroup.RootObject == null
                    ? null
                    : FindObjectByPath(pathArrary, 0, objectGroup.RootObject);
            }
            else
            {
                return objectGroup.ParentObject == null
                    ? null
                    : FindObjectByPath(pathArrary, 0, objectGroup.ParentObject);
            }
        }

        public string Localize(ObjectInfo<object> objectInfo)
        {
            var pair = ObjectPair<object>.Create(
                FindObject(objectInfo.OldObjectGroup),
                FindObject(objectInfo.NewObjectGroup));

            return Localize(pair);
        }

        protected abstract string Localize(ObjectPair<object> objectPair);

        public static CustomPropertyLocalizer CreateInstance(Type localizerType)
        {
            return (CustomPropertyLocalizer)Activator.CreateInstance(localizerType);
        }

        public PropertyPath Path { get; protected set; }
        public bool Relative { get; protected set; }

        // Test support
        public abstract string[] PossibleResourceNames { get; }
    }
}
