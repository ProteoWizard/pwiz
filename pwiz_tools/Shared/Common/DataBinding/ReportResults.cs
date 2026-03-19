/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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

namespace pwiz.Common.DataBinding
{
    public class ReportResults : Immutable
    {
        private BigList<RowItem> _allRowItems;
        public static readonly ReportResults EMPTY = new ReportResults(BigList<RowItem>.Empty, ItemProperties.EMPTY);
        public ReportResults(BigList<RowItem> rowItems, IEnumerable<DataPropertyDescriptor> itemProperties) 
        {
            RowItems = rowItems;
            ItemProperties = ItemProperties.FromList(itemProperties);
        }

        public BigList<RowItem> RowItems
        {
            get
            {
                return _allRowItems;
            }
            private set
            {
                _allRowItems = value;
            }
        }


        public virtual ReportResults ChangeRowItems(BigList<RowItem> rowItems)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.RowItems = rowItems;
            });
        }

        public long RowCount
        {
            get { return RowItems.Count; }
        }
        public ItemProperties ItemProperties
        {
            get;
            private set;
        }
    }
}
