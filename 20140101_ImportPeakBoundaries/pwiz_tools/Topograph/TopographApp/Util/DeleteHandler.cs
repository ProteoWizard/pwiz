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
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;

namespace pwiz.Topograph.ui.Util
{
    public abstract class DeleteHandler
    {
        public virtual bool DeleteEnabled { get { return true; } }
        public abstract void Delete();
        public IList<TRow> GetSelectedRows<TRow>(DataGridView dataGridView)
        {
            var rows = new List<TRow>();
            var bindingSource = dataGridView.DataSource as BindingListSource;
            if (null == bindingSource)
            {
                return rows;
            }
            var rowSet = new HashSet<TRow>();
            foreach (DataGridViewRow row in dataGridView.SelectedRows)
            {
                var rowItem = (RowItem) bindingSource[row.Index];
                if (!(rowItem.Value is TRow))
                {
                    continue;
                }
                var rowValue = (TRow) rowItem.Value;
                if (rows.Contains(rowValue))
                {
                    continue;
                }
                rows.Add(rowValue);
                rowSet.Add(rowValue);
            }
            return rows;
        }
    }
}
