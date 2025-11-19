/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Databinding
{
    /// <summary>
    /// Subclass of BoundDataGridView which exposes some test methods.
    /// </summary>
    public class BoundDataGridViewEx : BoundDataGridView
    {
        /// <summary>
        /// Testing method: Sends Ctrl-V to this control.
        /// </summary>
        public void SendPaste()
        {
            OnKeyDown(new KeyEventArgs(Keys.V | Keys.Control));
        }

        public void SendKeyDownUp(KeyEventArgs keyEventArgs)
        {
            OnKeyDown(keyEventArgs);
            OnKeyUp(keyEventArgs);
        }

        public void ClickCurrentCell()
        {
            OnCellContentClick(new DataGridViewCellEventArgs(CurrentCell.ColumnIndex, CurrentCell.RowIndex));
        }

        protected override IEnumerable<PropertyDescriptor> GetColumnsToHide(ReportResults reportResults)
        {
            var baseColumns = base.GetColumnsToHide(reportResults);
            if (reportResults is ClusteredReportResults)
            {
                return baseColumns;
            }

            var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(reportResults.ItemProperties);
            if (replicatePivotColumns == null || !replicatePivotColumns.HasConstantAndVariableColumns())
            {
                return baseColumns;
            }
            return baseColumns.Concat(replicatePivotColumns.GetReplicateColumnGroups()
                .SelectMany(group => group.Where(replicatePivotColumns.IsConstantColumn)));
        }

        protected override bool ProcessDataGridViewKey(KeyEventArgs e)
        {
            try
            {
                return base.ProcessDataGridViewKey(e);
            }
            catch (Exception exception)
            {
                ExceptionUtil.HandleProcessKeyException(this, exception, e.KeyData);
                return true;
            }
        }
    }
}
