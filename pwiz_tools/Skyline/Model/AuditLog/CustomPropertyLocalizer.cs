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
                if (property.PropertyInfo.Name == pathArray[index])
                {
                    var val = property.PropertyInfo.GetValue(obj);
                    return FindObjectByPath(pathArray, ++index, val);
                }         
            }

            return 0;
        }

        public string Localize(object rootObj, object parentObj)
        {
            var pathArrary = PropertyPathToArray(Path);

            object obj;
            if (!Relative)
            {
                if (rootObj == null)
                    return null;

                obj = FindObjectByPath(pathArrary, 0, rootObj);
            }
            else
            {
                if (parentObj == null)
                    return null;

                obj = FindObjectByPath(pathArrary, 0, parentObj);
            }

            return Localize(obj);
        }

        protected abstract string Localize(object obj);

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
