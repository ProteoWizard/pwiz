/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
    /// Provides extra information about how to display one-to-many
    /// relationships (i.e. properties whose values are IList or 
    /// IDictionary.)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class OneToManyAttribute : Attribute
    {
        /// <summary>
        /// Specifies the name of a property on the Item class
        /// which points back to this object.  The ForeignKey
        /// column will be hidden by default in the AvailableFieldsTree.
        /// </summary>
        public string ForeignKey { get; set; }

        /// <summary>
        /// Specifies the caption to use on the Index property of this collection.
        /// The Index property is either the Key from a Dictionary, or the
        /// integer index in a List.
        /// (Currently there is no way to display the index of a List in a grid).
        /// </summary>
        public string IndexDisplayName { get; set; }

        /// <summary>
        /// Specifies the caption to use on elements from this collection.
        /// The default is what is returned by 
        /// <see cref="DataSchema.ColumnCaptionFromType"/>.
        /// </summary>
        public string ItemDisplayName { get; set; }
    }
}
