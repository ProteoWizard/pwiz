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
using pwiz.Skyline.Properties;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

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
            string message;
            if (docNodes.Count == 1)
            {
                var peptide = docNodes.Values.First() as Peptide;
                if (null != peptide)
                {
                    message = string.Format(GroupComparisonStrings.GroupComparisonViewContext_Delete_Are_you_sure_you_want_to_delete_the_peptide__0_, peptide);
                }
                else
                {
                    var protein = docNodes.Values.First() as Protein;
                    message = string.Format(GroupComparisonStrings.GroupComparisonViewContext_Delete_Are_you_sure_you_want_to_delete_the_protein__0_, protein);
                }
            }
            else
            {
                if (docNodes.Values.First() is Peptide)
                {
                    message = string.Format(GroupComparisonStrings.GroupComparisonViewContext_Delete_Are_you_sure_you_want_to_delete_these__0__peptides, docNodes.Count);
                }
                else
                {
                    message = string.Format(GroupComparisonStrings.GroupComparisonViewContext_Delete_Are_you_sure_you_want_to_delete_these__0__proteins, docNodes.Count);
                }
            }
            if (MultiButtonMsgDlg.Show(BoundDataGridView, message, Resources.OK) != DialogResult.OK)
            {
                return;
            }
            var identityPathsToDelete = new HashSet<IdentityPath>(docNodes.Keys);
            var skylineWindow = ((SkylineDataSchema) DataSchema).SkylineWindow;
            if (null != skylineWindow)
            {
                skylineWindow.ModifyDocument(GroupComparisonStrings.GroupComparisonViewContext_Delete_Delete_items, doc=>DeleteProteins(doc, identityPathsToDelete));
            }
        }

        private SrmDocument DeleteProteins(SrmDocument document, HashSet<IdentityPath> identityPathsToDelete)
        {
            var newProteins = new List<PeptideGroupDocNode>();
            foreach (var protein in document.PeptideGroups)
            {
                if (identityPathsToDelete.Contains(new IdentityPath(protein.Id)))
                {
                    continue;
                }

                if (protein.Children.Count != 0)
                {
                    var newProtein = DeletePeptides(protein, identityPathsToDelete);
                    if (newProtein.Children.Count == 0)
                    {
                        continue;
                    }
                    newProteins.Add(newProtein);
                }
                else
                {
                    newProteins.Add(protein);
                }
            }
            return (SrmDocument) document.ChangeChildren(newProteins.Cast<DocNode>().ToArray());
        }

        private PeptideGroupDocNode DeletePeptides(
            PeptideGroupDocNode peptideGroupDocNode,
            HashSet<IdentityPath> identityPathsToDelete)
        {
            var newPeptides = new List<PeptideDocNode>();
            foreach (var peptide in peptideGroupDocNode.Molecules)
            {
                var identityPath = new IdentityPath(peptideGroupDocNode.Id, peptide.Id);
                if (!identityPathsToDelete.Contains(identityPath))
                {
                    newPeptides.Add(peptide);
                }
            }
            if (newPeptides.Count == peptideGroupDocNode.MoleculeCount)
            {
                return peptideGroupDocNode;
            }
            return (PeptideGroupDocNode) peptideGroupDocNode.ChangeChildren(newPeptides.Cast<DocNode>().ToArray());
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
            // ReSharper disable NonLocalizedString
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
            // ReSharper restore NonLocalizedString

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
            var selectedRows = dataGridView.SelectedRows.Cast<DataGridViewRow>()
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
