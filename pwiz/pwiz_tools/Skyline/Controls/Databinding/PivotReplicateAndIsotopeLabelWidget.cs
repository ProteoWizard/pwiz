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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class PivotReplicateAndIsotopeLabelWidget : ViewEditorWidget
    {
        public PivotReplicateAndIsotopeLabelWidget()
        {
            InitializeComponent();
        }

        protected override void OnViewChange()
        {
            base.OnViewChange();
            var currentViewSpec = ViewInfo.ViewSpec;
            var pivotedViewSpec = PivotIsotopeLabel(currentViewSpec, true);
            var unpivotedViewSpec = PivotIsotopeLabel(currentViewSpec, false);
            if (pivotedViewSpec.Equals(unpivotedViewSpec))
            {
                cbxPivotIsotopeLabel.CheckState = CheckState.Indeterminate;
                cbxPivotIsotopeLabel.Enabled = false;
            }
            else
            {
                cbxPivotIsotopeLabel.Enabled = true;
                if (currentViewSpec.Equals(unpivotedViewSpec))
                {
                    cbxPivotIsotopeLabel.CheckState = CheckState.Unchecked;
                }
                else if (currentViewSpec.Equals(pivotedViewSpec))
                {
                    cbxPivotIsotopeLabel.CheckState = CheckState.Checked;
                }
                else
                {
                    cbxPivotIsotopeLabel.CheckState = CheckState.Indeterminate;
                }
            }

            cbxPivotReplicate.Checked = !ViewSpec.SublistId.StartsWith(SkylineViewContext.GetReplicateSublist(ViewInfo.ParentColumn.PropertyType));
        }

        private void cbxPivotReplicate_CheckedChanged(object sender, EventArgs e)
        {
            if (InChangeView)
            {
                return;
            }
            SetPivotReplicate(cbxPivotReplicate.Checked);
        }

        public void SetPivotReplicate(bool pivot)
        {
            if (pivot)
            {
                ViewSpec = ViewSpec.SetSublistId(PropertyPath.Root);
            }
            else
            {
                ViewSpec = ViewSpec.SetSublistId(SkylineViewContext.GetReplicateSublist(ViewInfo.ParentColumn.PropertyType));
            }
        }

        private void cbxPivotIsotopeLabel_CheckedChanged(object sender, EventArgs e)
        {
            if (InChangeView)
            {
                return;
            }
            SetPivotIsotopeLabel(cbxPivotIsotopeLabel.Checked);
        }

        public void SetPivotIsotopeLabel(bool pivotIsotopeLabel)
        {
            ViewSpec = PivotIsotopeLabel(ViewSpec, pivotIsotopeLabel);
        }
        // ReSharper disable NonLocalizedString
        private static readonly IList<PropertyPath> PrecursorCrosstabValues =
            ImmutableList.ValueOf(new[]
            {
                PropertyPath.Root.Property("IsotopeLabelType"),
                PropertyPath.Root.Property("NeutralMass"),
                PropertyPath.Root.Property("Mz"),
                PropertyPath.Root.Property("CollisionEnergy"),
                PropertyPath.Root.Property("DeclusteringPotential"),
                PropertyPath.Root.Property("ModifiedSequence"),
                PropertyPath.Root.Property("Note"),
                PropertyPath.Root.Property("Results").LookupAllItems(),
                PropertyPath.Root.Property("ResultSummary"),
            });

        public static ViewSpec PivotIsotopeLabel(ViewSpec viewSpec, bool pivot)
        {
            PropertyPath pivotKey;
            IList<PropertyPath> groupBy;
            IList<PropertyPath> crossTabExclude;
            IList<PropertyPath> crosstabValues;
            if (viewSpec.RowSource == typeof (Precursor).FullName)
            {
                pivotKey = PropertyPath.Root.Property("IsotopeLabelType");
                groupBy = new[]
                {
                    PropertyPath.Root.Property("Peptide"),
                    PropertyPath.Root.Property("Charge"),
                };
                crosstabValues = PrecursorCrosstabValues;
                crossTabExclude = new []{PropertyPath.Parse("Results!*.Value.PeptideResult")};
            }
            else if (viewSpec.RowSource == typeof (Transition).FullName)
            {
                pivotKey = PropertyPath.Root.Property("Precursor").Property("IsotopeLabelType");
                groupBy = new[]
                {
                    PropertyPath.Root.Property("ProductCharge"),
                    PropertyPath.Root.Property("FragmentIon"),
                    PropertyPath.Root.Property("Losses"),
                    PropertyPath.Root.Property("Precursor").Property("Peptide"),
                    PropertyPath.Root.Property("Precursor").Property("Charge"),
                };
                crosstabValues = new[]
                {
                    PropertyPath.Root.Property("ProductNeutralMass"),
                    PropertyPath.Root.Property("ProductMz"),
                    PropertyPath.Root.Property("Note"),
                    PropertyPath.Root.Property("Results").LookupAllItems(),
                    PropertyPath.Root.Property("ResultSummary"),
                }.Concat(PrecursorCrosstabValues.Select(
                    propertyPath => PropertyPath.Root.Property("Precursor").Concat(propertyPath)))
                    .ToArray();
                crossTabExclude = new[] {PropertyPath.Parse("Results!*.Value.PrecursorResult.PeptideResult")};
            }
            else
            {
                return viewSpec;
            }
            var newColumns = new List<ColumnSpec>();
            if (!pivot)
            {
                foreach (var column in viewSpec.Columns)
                {
                    if (!column.Hidden)
                    {
                        newColumns.Add(column.SetTotal(TotalOperation.GroupBy));
                    }
                }
            }
            else
            {
                bool pivotKeyHandled = false;
                var missingGroupBy = new HashSet<PropertyPath>(groupBy);
                foreach (var column in viewSpec.Columns)
                {
                    if (pivotKey.Equals(column.PropertyPath))
                    {
                        pivotKeyHandled = true;
                        newColumns.Add(column.SetTotal(TotalOperation.PivotKey));
                    }
                    else if (crossTabExclude.Any(propertyPath => column.PropertyPath.StartsWith(propertyPath)))
                    {
                        newColumns.Add(column.SetTotal(TotalOperation.GroupBy));
                    }
                    else if (crosstabValues.Any(propertyPath => column.PropertyPath.StartsWith(propertyPath)))
                    {
                        newColumns.Add(column.SetTotal(TotalOperation.PivotValue));
                    }
                    else
                    {
                        missingGroupBy.Remove(column.PropertyPath);
                        newColumns.Add(column.SetTotal(TotalOperation.GroupBy));
                    }
                }
                if (!pivotKeyHandled)
                {
                    newColumns.Add(new ColumnSpec(pivotKey).SetTotal(TotalOperation.PivotKey).SetHidden(true));
                }
                if (missingGroupBy.Count > 0)
                {
                    foreach (var propertyPath in groupBy)
                    {
                        if (!missingGroupBy.Contains(propertyPath))
                        {
                            continue;
                        }
                        newColumns.Add(new ColumnSpec(propertyPath).SetTotal(TotalOperation.GroupBy).SetHidden(true));
                    }
                }
            }
            return viewSpec.SetColumns(newColumns);
        }
        // ReSharper restore NonLocalizedString

        #region for testing

        public static void SetPivotReplicate(ViewEditor viewEditor, bool pivotReplicate)
        {
            viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>().First().SetPivotReplicate(pivotReplicate);
        }

        public static void SetPivotIsotopeLabel(ViewEditor viewEditor, bool pivotIsotopeLabel)
        {
            viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>().First().SetPivotIsotopeLabel(pivotIsotopeLabel);
        }
        #endregion
    }
}
