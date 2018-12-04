/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public class GroupComparisonViewContext : SkylineViewContext
    {
        public GroupComparisonViewContext(SkylineDataSchema dataSchema, IEnumerable<RowSourceInfo> rowSourceInfos) 
            : base(dataSchema, rowSourceInfos)
        {
        }

        public BoundDataGridView BoundDataGridView { get; set; }

        public override bool DeleteEnabled
        {
            get
            {
                return BoundDataGridView != null;
            }
        }

        public override void Delete()
        {
            if (null == BoundDataGridView)
            {
                return;
            }
            var selectedRows = GetSelectedRows<FoldChangeBindingSource.FoldChangeRow>(BoundDataGridView);
            if (!selectedRows.Any())
            {
                MessageDlg.Show(BoundDataGridView, GroupComparisonStrings.GroupComparisonViewContext_Delete_No_rows_are_selected);
                return;
            }
            var docNodes = new Dictionary<IdentityPath, SkylineDocNode>();
            foreach (var row in selectedRows)
            {
                if (row.Peptide != null)
                {
                    docNodes[row.Peptide.IdentityPath] = row.Peptide;
                }
                else
                {
                    docNodes[row.Protein.IdentityPath] = row.Protein;
                }
            }
            DeleteSkylineDocNodes(BoundDataGridView, docNodes.Values);
        }

        public static ViewSpec GetDefaultViewSpec(GroupComparisonDef groupComparisonDef, 
            IList<FoldChangeBindingSource.FoldChangeRow> foldChangeRows)
        {
            bool showPeptide; 
            bool showLabelType;
            bool showMsLevel;
            if (foldChangeRows.Any())
            {
                showPeptide = foldChangeRows.Any(row => null != row.Peptide);
                showLabelType = foldChangeRows.Select(row => row.IsotopeLabelType).Distinct().Count() > 1;
                showMsLevel = foldChangeRows.Select(row => row.MsLevel).Distinct().Count() > 1;
            }
            else
            {
                showPeptide = !groupComparisonDef.PerProtein;
                showLabelType = false;
                showMsLevel = false;
            }
            // ReSharper disable LocalizableElement
            var columns = new List<PropertyPath>
            {
                PropertyPath.Root.Property("Protein")
            };
            if (showPeptide)
            {
                columns.Add(PropertyPath.Root.Property("Peptide"));
            }
            if (showMsLevel)
            {
                columns.Add(PropertyPath.Root.Property("MsLevel"));
            }
            if (showLabelType)
            {
                columns.Add(PropertyPath.Root.Property("IsotopeLabelType"));
            }
            columns.Add(PropertyPath.Root.Property("FoldChangeResult"));
            columns.Add(PropertyPath.Root.Property("FoldChangeResult").Property("AdjustedPValue"));
            // ReSharper restore LocalizableElement

            var viewSpec = new ViewSpec()
                .SetName(DefaultViewName)
                .SetRowType(typeof (FoldChangeBindingSource.FoldChangeRow))
                .SetColumns(columns.Select(col => new ColumnSpec(col)));
            return viewSpec;
        }

        public static IList<TRow> GetSelectedRows<TRow>(DataGridView dataGridView)
        {
            var rows = new List<TRow>();
            var bindingSource = dataGridView.DataSource as BindingListSource;
            if (null == bindingSource)
            {
                return rows;
            }
            var rowSet = new HashSet<TRow>();
            var selectedRows = dataGridView.SelectedCells.Cast<DataGridViewCell>().Select(cell => dataGridView.Rows[cell.RowIndex]).Distinct()
                .Select(row => (RowItem) bindingSource[row.Index]).ToArray();
            if (!selectedRows.Any())
            {
                selectedRows = new[] {bindingSource.Current as RowItem};
            }

            foreach (var rowItem in selectedRows)
            {
                if (rowItem == null || !(rowItem.Value is TRow))
                {
                    continue;
                }
                var rowValue = (TRow)rowItem.Value;
                if (rowSet.Add(rowValue))
                {
                    rows.Add(rowValue);
                }
            }
            return rows;
        }
    }
}
