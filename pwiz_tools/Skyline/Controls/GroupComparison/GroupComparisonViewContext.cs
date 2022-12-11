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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Layout;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
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
            get { return BoundDataGridView != null; }
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
                MessageDlg.Show(BoundDataGridView,
                    GroupComparisonStrings.GroupComparisonViewContext_Delete_No_rows_are_selected);
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

        public static IList<TRow> GetSelectedRows<TRow>(DataGridView dataGridView)
        {
            var rows = new List<TRow>();
            var bindingSource = dataGridView.DataSource as BindingListSource;
            if (null == bindingSource)
            {
                return rows;
            }

            var rowSet = new HashSet<TRow>();
            var selectedRows = dataGridView.SelectedCells.Cast<DataGridViewCell>()
                .Select(cell => dataGridView.Rows[cell.RowIndex]).Distinct()
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

                var rowValue = (TRow) rowItem.Value;
                if (rowSet.Add(rowValue))
                {
                    rows.Add(rowValue);
                }
            }

            return rows;
        }

        public override ViewSpecList GetViewSpecList(ViewGroupId viewGroup)
        {
            ViewSpecList viewSpecList = base.GetViewSpecList(viewGroup);
            if (Equals(viewGroup, ViewGroup.BUILT_IN.Id))
            {
                viewSpecList = new ViewSpecList(viewSpecList.ViewSpecs, GetBuiltInViewLayouts());
            }

            return viewSpecList;
        }

        private IEnumerable<ViewLayoutList> GetBuiltInViewLayouts()
        {
            yield return GetClusteredLayout();
        }

        private ViewLayoutList GetClusteredLayout()
        {
            var ppRunAbundances = PropertyPath.Root
                .Property(nameof(FoldChangeBindingSource.FoldChangeDetailRow.ReplicateAbundances)).DictionaryValues();
            var roles = new List<Tuple<PropertyPath, ClusterRole>>();
            roles.Add(Tuple.Create(PropertyPath.Root.Property(nameof(FoldChangeBindingSource.FoldChangeDetailRow.Protein)),
                ClusterRole.ROWHEADER));
            roles.Add(Tuple.Create(PropertyPath.Root.Property(nameof(FoldChangeBindingSource.FoldChangeDetailRow.Peptide)),
                ClusterRole.ROWHEADER));
            roles.Add(Tuple.Create(
                ppRunAbundances.Property(nameof(FoldChangeBindingSource.ReplicateRow.ReplicateSampleIdentity)),
                ClusterRole.COLUMNHEADER));
            roles.Add(Tuple.Create(
                ppRunAbundances.Property(nameof(FoldChangeBindingSource.ReplicateRow.ReplicateGroup)),
                ClusterRole.COLUMNHEADER));
            roles.Add(Tuple.Create(ppRunAbundances.Property(nameof(FoldChangeBindingSource.ReplicateRow.Abundance)),
                (ClusterRole) ClusterRole.ZSCORE));
            var viewLayout = new ViewLayout(@"clustered").ChangeClusterSpec(new ClusteringSpec(roles.Select(role =>
                new ClusteringSpec.ValueSpec(new ClusteringSpec.ColumnRef(role.Item1), role.Item2))));
            var viewLayoutList = new ViewLayoutList(FoldChangeBindingSource.CLUSTERED_VIEW_NAME)
                .ChangeLayouts(new[] {viewLayout})
                .ChangeDefaultLayoutName(viewLayout.Name);
            return viewLayoutList;
        }

        public override void ToggleClustering(BindingListSource bindingListSource, bool turnClusteringOn)
        {
            if (Equals(ViewGroup.BUILT_IN.Id, bindingListSource.ViewInfo.ViewGroup.Id))
            {
                if (turnClusteringOn && bindingListSource.ViewInfo.ViewSpec.Name == AbstractViewContext.DefaultViewName)
                {
                    bindingListSource.SetViewContext(this, GetViewInfo(ViewGroup.BUILT_IN.Id.ViewName(FoldChangeBindingSource.CLUSTERED_VIEW_NAME)));
                    return;
                }

                if (!turnClusteringOn && bindingListSource.ViewInfo.ViewSpec.Name ==
                    FoldChangeBindingSource.CLUSTERED_VIEW_NAME)
                {
                    bindingListSource.SetViewContext(this, GetViewInfo(ViewGroup.BUILT_IN.Id.ViewName(AbstractViewContext.DefaultViewName)));
                }
            }
            base.ToggleClustering(bindingListSource, turnClusteringOn);
        }
    }
}
