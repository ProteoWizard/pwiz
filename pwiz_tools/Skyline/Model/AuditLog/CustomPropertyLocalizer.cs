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
using pwiz.Common.DataBinding;

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
            if (path.IsRoot)
                return new string[0];
            else
                return PropertyPathToArray(path.Parent).Concat(new[] { path.Name }).ToArray();
        }

        protected object FindObjectByPath(string[] pathArray, int index, object obj)
        {
            if (index == pathArray.Length)
                return obj;

            foreach (var property in Reflector.GetProperties(obj.GetType()))
            {
                if (property.PropertyName == pathArray[index])
                {
                    var val = property.GetValue(obj);
                    return FindObjectByPath(pathArray, ++index, val);
                }         
            }

            return 0;
        }

        private object FindObject(ObjectGroup objectGroup)
        {
            if (objectGroup == null)
                return null;

            var pathArrary = PropertyPathToArray(Path);

            if (!Relative)
            {
                if (objectGroup.RootObject == null)
                    return null;

                return FindObjectByPath(pathArrary, 0, objectGroup.RootObject);
            }
            else
            {
                if (objectGroup.ParentObject == null)
                    return null;

                return FindObjectByPath(pathArrary, 0, objectGroup.ParentObject);
            }
        }

        public string Localize(ObjectGroup oldGroup, ObjectGroup newGroup)
        {
            var oldObj = FindObject(oldGroup);
            var newObj = FindObject(newGroup);

            return Localize(oldObj, newObj);
        }

        protected abstract string Localize(object oldObj, object newObj);

        public static CustomPropertyLocalizer CreateInstance(Type localizerType)
        {
            return (CustomPropertyLocalizer)Activator.CreateInstance(localizerType);
        }

        public PropertyPath Path { get; protected set; }
        public bool Relative { get; protected set; }

        // Test support
        public abstract string[] PossibleResourceNames { get; }
    }

    public class ObjectGroup
    {
        public ObjectGroup(object obj, object parentObject, object rootObject)
        {
            Object = obj;
            ParentObject = parentObject;
            RootObject = rootObject;
        }

        public static ObjectGroup Create(object obj, object parentObject, object rootObject)
        {
            return new ObjectGroup(obj, parentObject, rootObject);
        }

        public object Object { get; private set; }
        public object ParentObject { get; private set; }
        public object RootObject { get; private set; }
    }
}
