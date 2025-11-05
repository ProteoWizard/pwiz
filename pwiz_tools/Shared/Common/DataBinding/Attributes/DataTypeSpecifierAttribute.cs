/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
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
using System;

namespace pwiz.Common.DataBinding.Attributes
{
    /// <summary>
    /// Specifies a marker type that identifies the semantic meaning of a property
    /// without changing its actual type. This allows UI code to map properties
    /// with generic types (like string) to specific UI components without creating
    /// a compile-time dependency from Model to UI.
    /// The marker type should be an empty class defined in the Model layer.
    /// </summary>
    public class DataTypeSpecifierAttribute : Attribute
    {
        public DataTypeSpecifierAttribute(Type markerType)
        {
            MarkerType = markerType;
        }
        public Type MarkerType { get; set; }
    }
}
