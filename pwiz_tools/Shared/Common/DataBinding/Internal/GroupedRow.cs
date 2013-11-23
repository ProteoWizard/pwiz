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
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.DataBinding.Internal
{
    /// <summary>
    /// Values used in grouped/totaled reports.
    /// </summary>
    internal class GroupedRow
    {
        private readonly IDictionary<PivotKey, RowItem> _innerRows = new Dictionary<PivotKey, RowItem>();

        public IEnumerable<RowItem> InnerRows
        {
            get
            {
                return _innerRows.Values.AsEnumerable();
            }
        }

        public void AddInnerRow(PivotKey key, RowItem innerRow)
        {
            _innerRows.Add(key, innerRow);
        }

        public bool TryGetInnerRow(PivotKey key, out RowItem row)
        {
            return _innerRows.TryGetValue(key, out row);
        }

        public bool ContainsKey(PivotKey key)
        {
            return _innerRows.ContainsKey(key);
        }
    }
}
