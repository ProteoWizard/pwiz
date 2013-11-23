/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using System.Collections;
using System.Collections.Generic;

namespace pwiz.Common.DataBinding.RowSources
{
    public interface ICloneableList : IEnumerable
    {
        IEnumerable DeepClone();
    }

    /// <summary>
    /// A list which can make an immutable copy of itself.
    /// If the <see cref="pwiz.Common.DataBinding.Controls.BindingListSource.RowSource"/>
    /// is an ICloneableList, then the pivoting and filtering can be performed on a background 
    /// thread.
    /// </summary>
    public interface ICloneableList<TKey, TItem> : IList<TItem>, ICloneableList
    {
        int IndexOfKey(TKey key);
        /// <summary>
        /// Returns the key associate with an item in this list, or a cloned copy of this list.
        /// The key must be preserved by the act of cloning.
        /// </summary>
        TKey GetKey(TItem value);
        /// <summary>
        /// Returns a copy of this list whose elements will not change.
        /// </summary>
        new IList<TItem> DeepClone();
    }
}
