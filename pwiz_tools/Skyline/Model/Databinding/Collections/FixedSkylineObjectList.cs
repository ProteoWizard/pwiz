/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    /// <summary>
    /// A list whose contents do not change when the Skyline document changes.
    /// </summary>
    public class FixedSkylineObjectList<T> : SkylineObjectList<T>
    {
        private ImmutableList<T> _items;
        public FixedSkylineObjectList(SkylineDataSchema schema, IEnumerable<T> items) : base(schema)
        {
            _items = ImmutableList.ValueOf(items);
        }

        public override IEnumerable GetItems()
        {
            return _items;
        }
    }
}
