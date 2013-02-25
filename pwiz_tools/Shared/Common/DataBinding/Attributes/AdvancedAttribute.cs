/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
    /// Specifies that the property is "advanced" and should only be displayed
    /// in column choosers if the user has chosen to "show advanced".
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AdvancedAttribute : Attribute
    {
        public AdvancedAttribute() : this(true)
        {
        }
        public AdvancedAttribute(bool advanced)
        {
            Advanced = advanced;
        }
        public bool Advanced { get; private set; }
    }
}
