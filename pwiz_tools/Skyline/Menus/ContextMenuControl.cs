/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Menus
{
    public partial class ContextMenuControl : SkylineControl
    {
        public ContextMenuControl(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Required to enable design view on derived classes
        /// </summary>
        private ContextMenuControl()
        {
            InitializeComponent();
        }

        private void selectionContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowReplicateSelection = selectionContextMenuItem.Checked;
            SkylineWindow.UpdateSummaryGraphs();
        }

        private void synchronizeSummaryZoomingContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.SynchronizeSummaryZooming = synchronizeSummaryZoomingContextMenuItem.Checked;
            SkylineWindow.SynchronizeSummaryZooming();
        }

        private void peptideCvsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowCVValues(peptideCvsContextMenuItem.Checked);
        }

        protected void AddTransitionContextMenu(ToolStrip menuStrip, int iInsert)
        {
            using var chromatogramContextMenu = new ChromatogramContextMenu(SkylineWindow);
            menuStrip.Items.Insert(iInsert, chromatogramContextMenu.TransitionsContextMenuItem);
        }

        protected void AddPeptideOrderContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert, peptideOrderContextMenuItem);
            if (peptideOrderContextMenuItem.DropDownItems.Count == 0)
            {
                peptideOrderContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    peptideOrderDocumentContextMenuItem,
                    peptideOrderRTContextMenuItem,
                    peptideOrderAreaContextMenuItem
                });
            }
        }

        protected void AddScopeContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert, scopeContextMenuItem);
            if (scopeContextMenuItem.DropDownItems.Count == 0)
            {
                scopeContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    documentScopeContextMenuItem,
                    proteinScopeContextMenuItem
                });
            }
        }

        protected int AddReplicatesContextMenu(ToolStrip menuStrip, int iInsert)
        {
            if (DocumentUI.Settings.HasResults &&
                DocumentUI.Settings.MeasuredResults.Chromatograms.Count > 1)
            {
                menuStrip.Items.Insert(iInsert++, replicatesRTContextMenuItem);
                if (replicatesRTContextMenuItem.DropDownItems.Count == 0)
                {
                    replicatesRTContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        averageReplicatesContextMenuItem,
                        singleReplicateRTContextMenuItem,
                        bestReplicateRTContextMenuItem
                    });
                }
            }
            return iInsert;
        }

        private void peptideOrderContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            SummaryPeptideOrder peptideOrder = SummaryPeptideGraphPane.PeptideOrder;
            peptideOrderDocumentContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.document);
            peptideOrderRTContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.time);
            peptideOrderAreaContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.area);
            peptideOrderMassErrorContextMenuItem.Checked = (peptideOrder == SummaryPeptideOrder.mass_error);
        }

        private void peptideOrderDocumentContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.document);
        }

        private void peptideOrderRTContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.time);
        }

        private void peptideOrderAreaContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.area);
        }

        private void peptideOrderMassErrorContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.mass_error);
        }

        private void scopeContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var areaScope = AreaGraphController.AreaScope;
            documentScopeContextMenuItem.Checked = (areaScope == AreaScope.document);
            proteinScopeContextMenuItem.Checked = (areaScope == AreaScope.protein);
        }

        private void documentScopeContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AreaScopeTo(AreaScope.document);
        }

        private void proteinScopeContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.AreaScopeTo(AreaScope.protein);
        }

        private void replicatesRTContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ReplicateDisplay replicate = RTLinearRegressionGraphPane.ShowReplicate;
            averageReplicatesContextMenuItem.Checked = (replicate == ReplicateDisplay.all);
            singleReplicateRTContextMenuItem.Checked = (replicate == ReplicateDisplay.single);
            bestReplicateRTContextMenuItem.Checked = (replicate == ReplicateDisplay.best);
        }

        private void averageReplicatesContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowAverageReplicates();
        }

        private void singleReplicateRTContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowSingleReplicate();
        }

        private void bestReplicateRTContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRegressionReplicateEnum = ReplicateDisplay.best.ToString();
            Settings.Default.ShowPeptideCV = false;
            SkylineWindow.UpdateSummaryGraphs();
        }

        protected int AddReplicateOrderAndGroupByMenuItems(ToolStrip menuStrip, int iInsert)
        {
            return AddReplicateOrderAndGroupByMenuItems(menuStrip, iInsert,
                SummaryReplicateGraphPane.GroupByReplicateAnnotation,
                GroupByReplicateAnnotationMenuItem);
        }

        protected int AddReplicateOrderAndGroupByMenuItems(ToolStrip menuStrip, int iInsert,
            string currentGroupByAnnotation,
            Func<ReplicateValue, bool, ToolStripMenuItem> getGroupByMenuItem)
        {
            var currentGroupBy = ReplicateValue.FromPersistedString(DocumentUI.Settings, currentGroupByAnnotation);
            var groupByValues = ReplicateValue.GetGroupableReplicateValues(DocumentUI).ToArray();

            var orderByReplicateAnnotationDef = groupByValues.FirstOrDefault(
                value => SummaryReplicateGraphPane.OrderByReplicateAnnotation == value.ToPersistedString());
            menuStrip.Items.Insert(iInsert++, replicateOrderContextMenuItem);
            replicateOrderContextMenuItem.DropDownItems.Clear();
            replicateOrderContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                replicateOrderDocumentContextMenuItem,
                replicateOrderAcqTimeContextMenuItem
            });
            replicateOrderDocumentContextMenuItem.Checked
                = null == orderByReplicateAnnotationDef &&
                  SummaryReplicateOrder.document == SummaryReplicateGraphPane.ReplicateOrder;
            replicateOrderAcqTimeContextMenuItem.Checked
                = null == orderByReplicateAnnotationDef &&
                  SummaryReplicateOrder.time == SummaryReplicateGraphPane.ReplicateOrder;
            foreach (var replicateValue in groupByValues)
            {
                replicateOrderContextMenuItem.DropDownItems.Add(OrderByReplicateAnnotationMenuItem(
                    replicateValue, SummaryReplicateGraphPane.OrderByReplicateAnnotation));
            }

            if (groupByValues.Length > 0)
            {
                menuStrip.Items.Insert(iInsert++, groupReplicatesByContextMenuItem);
                groupReplicatesByContextMenuItem.DropDownItems.Clear();
                groupReplicatesByContextMenuItem.DropDownItems.Add(
                    getGroupByMenuItem(null, currentGroupBy == null));
                foreach (var replicateValue in groupByValues)
                {
                    groupReplicatesByContextMenuItem.DropDownItems
                        .Add(getGroupByMenuItem(replicateValue, Equals(replicateValue, currentGroupBy)));
                }
            }
            return iInsert;
        }

        private ToolStripMenuItem GroupByReplicateAnnotationMenuItem(ReplicateValue replicateValue, bool isChecked)
        {
            if (replicateValue == null)
            {
                groupByReplicateContextMenuItem.Checked = isChecked;
                return groupByReplicateContextMenuItem;
            }
            return new ToolStripMenuItem(replicateValue.Title, null,
                (sender, eventArgs) => SkylineWindow.GroupByReplicateValue(replicateValue))
            {
                Checked = isChecked
            };
        }

        private ToolStripMenuItem OrderByReplicateAnnotationMenuItem(ReplicateValue replicateValue, string currentOrderBy)
        {
            return new ToolStripMenuItem(replicateValue.Title, null,
                                         (sender, eventArgs) => SkylineWindow.OrderByReplicateAnnotation(replicateValue))
                {
                    Checked = replicateValue.ToPersistedString() == currentOrderBy
                };
        }

        private void replicateOrderDocumentContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.document);
        }

        private void replicateOrderAcqTimeContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.time);
        }

        private void groupByReplicateContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.GroupByReplicateValue(null);
        }
    }
}
