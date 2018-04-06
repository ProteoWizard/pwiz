/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Internal
{
    internal class PivotedRows : Immutable
    {
        public static readonly PivotedRows EMPTY = new PivotedRows(ImmutableList.Empty<RowItem>(),
            ImmutableList.Empty<DataPropertyDescriptor>());
        public PivotedRows(IEnumerable<RowItem> rowItems, IEnumerable<DataPropertyDescriptor> itemProperties)
        {
            RowItems = ImmutableList.ValueOf(rowItems);
            ItemProperties = ImmutableList.ValueOf(itemProperties);
        }

        public IList<RowItem> RowItems { get; private set; }
        public int Count { get { return RowItems.Count; } }

        public PivotedRows ChangeRowItems(IEnumerable<RowItem> rowItems)
        {
            return ChangeProp(ImClone(this), im => im.RowItems = ImmutableList.ValueOf(rowItems));
        }
        public ImmutableList<DataPropertyDescriptor> ItemProperties { get; private set; }
    }
}