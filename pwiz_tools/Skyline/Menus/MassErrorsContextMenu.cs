/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Menus
{
    public partial class MassErrorsContextMenu : SkylineControl
    {
        public MassErrorsContextMenu(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
        }

        public void BuildMassErrorGraphMenu(GraphSummary graph, ToolStrip menuStrip)
        {
            // Store original menuitems in an array, and insert a separator
            ToolStripItem[] items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                string tag = (string)items[i].Tag;
                if (tag == @"unzoom")
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, toolStripSeparator25);

            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;
            var graphType = graph.Type;
            menuStrip.Items.Insert(iInsert++, massErrorGraphContextMenuItem);

            menuStrip.Items.Insert(iInsert++, new ToolStripSeparator());
            if (graphType == GraphTypeSummary.peptide ||
                graphType == GraphTypeSummary.replicate)
            {
                SkylineWindow.AddTransitionContextMenu(menuStrip, iInsert++);
            }
            if (graphType == GraphTypeSummary.replicate)
            {
                iInsert = SkylineWindow.AddReplicateOrderAndGroupByMenuItems(menuStrip, iInsert);
                var massErrorReplicateGraphPane = graph.GraphPanes.FirstOrDefault() as MassErrorReplicateGraphPane;
                if (massErrorReplicateGraphPane != null)
                {
                    // If the mass error graph is being displayed and it shows a legend,
                    // display the "Legend" option
                    if (massErrorReplicateGraphPane.CanShowMassErrorLegend)
                    {
                        showMassErrorLegendContextMenuItem.Checked = set.ShowMassErrorLegend;
                        menuStrip.Items.Insert(iInsert++, showMassErrorLegendContextMenuItem);
                    }
                }
            }
            else if (graphType == GraphTypeSummary.peptide)
            {
                SkylineWindow.AddPeptideOrderContextMenu(menuStrip, iInsert++);
                iInsert = SkylineWindow.AddReplicatesContextMenu(menuStrip, iInsert);
                SkylineWindow.AddScopeContextMenu(menuStrip, iInsert++);
            }
            else if (graphType == GraphTypeSummary.histogram || graphType == GraphTypeSummary.histogram2d)
            {
                iInsert = SkylineWindow.AddReplicatesContextMenu(menuStrip, iInsert);
                iInsert = AddPointsContextMenu(menuStrip, iInsert);
                massErrorTargetsContextMenuItem.Checked = MassErrorGraphController.PointsType == PointsTypeMassError.targets;
                massErrorDecoysContextMenuItem.Checked = MassErrorGraphController.PointsType == PointsTypeMassError.decoys;
                bool trained = DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained;
                massErrorTargets1FDRContextMenuItem.Visible = trained;
                massErrorTargets1FDRContextMenuItem.Checked = MassErrorGraphController.PointsType == PointsTypeMassError.targets_1FDR;
                if (!trained && massErrorTargets1FDRContextMenuItem.Checked)
                {
                    massErrorTargetsContextMenuItem.Checked = true;
                }
                iInsert = AddBinCountContextMenu(menuStrip, iInsert);
                iInsert = AddTransitionsMassErrorContextMenu(menuStrip, iInsert);
            }
            if (graphType == GraphTypeSummary.histogram2d)
            {
                iInsert = AddXAxisContextMenu(menuStrip, iInsert);
                menuStrip.Items.Insert(iInsert++, massErrorlogScaleContextMenuItem);
                massErrorlogScaleContextMenuItem.Checked = Settings.Default.MassErrorHistogram2DLogScale;
            }
            if (graphType == GraphTypeSummary.peptide || (null != Settings.Default.GroupByReplicateAnnotation && graphType == GraphTypeSummary.replicate))
            {
                menuStrip.Items.Insert(iInsert++, SkylineWindow.peptideCvsContextMenuItem);
                SkylineWindow.peptideCvsContextMenuItem.Checked = set.ShowPeptideCV;
            }

            if (graphType == GraphTypeSummary.peptide ||
                graphType == GraphTypeSummary.replicate)
            {
                SkylineWindow.selectionContextMenuItem.Checked = set.ShowReplicateSelection;
                menuStrip.Items.Insert(iInsert++, SkylineWindow.selectionContextMenuItem);
                SkylineWindow.synchronizeSummaryZoomingContextMenuItem.Checked = set.SynchronizeSummaryZooming;
                menuStrip.Items.Insert(iInsert++, SkylineWindow.synchronizeSummaryZoomingContextMenuItem);
            }

            menuStrip.Items.Insert(iInsert++, SkylineWindow.toolStripSeparator24);
            menuStrip.Items.Insert(iInsert++, massErrorPropsContextMenuItem);
            menuStrip.Items.Insert(iInsert, SkylineWindow.toolStripSeparator28);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == @"set_default" || tag == @"show_val")
                    menuStrip.Items.Remove(item);
            }
        }

        private int AddPointsContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert++, massErrorPointsContextMenuItem);
            return iInsert;
        }

        private int AddBinCountContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert++, binCountContextMenuItem);
            return iInsert;
        }

        private int AddTransitionsMassErrorContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert++, massErrorTransitionsContextMenuItem);
            return iInsert;
        }

        private int AddXAxisContextMenu(ToolStrip menuStrip, int iInsert)
        {
            menuStrip.Items.Insert(iInsert++, massErrorXAxisContextMenuItem);
            return iInsert;
        }

        #region Event Handlers

        private void massErrorMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var types = Settings.Default.MassErrorGraphTypes;
            massErrorReplicateComparisonContextMenuItem.Checked = SkylineWindow.GraphChecked(SkylineWindow.ListGraphMassError, types, GraphTypeSummary.replicate);
            massErrorPeptideComparisonContextMenuItem.Checked = SkylineWindow.GraphChecked(SkylineWindow.ListGraphMassError, types, GraphTypeSummary.peptide);
            massErrorHistogramContextMenuItem.Checked = SkylineWindow.GraphChecked(SkylineWindow.ListGraphMassError, types, GraphTypeSummary.histogram);
            massErrorHistogram2DContextMenuItem.Checked = SkylineWindow.GraphChecked(SkylineWindow.ListGraphMassError, types, GraphTypeSummary.histogram2d);
        }

        private void massErrorReplicateComparisonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowMassErrorReplicateComparison();
        }

        private void massErrorPeptideComparisonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowMassErrorPeptideGraph();
        }

        private void massErrorHistogramMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowMassErrorHistogramGraph();
        }

        private void massErrorHistogram2DMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowMassErrorHistogramGraph2D();
        }

        private void massErrorTransitionsContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            massErrorAllTransitionsContextMenuItem.Checked = MassErrorGraphController.HistogramTransiton == TransitionMassError.all;
            massErrorBestTransitionsContextMenuItem.Checked = MassErrorGraphController.HistogramTransiton == TransitionMassError.best;

            MassErrorPrecursorsContextMenuItem.Checked = MassErrorGraphController.HistogramDisplayType == DisplayTypeMassError.precursors;
            MassErrorProductsContextMenuItem.Checked = MassErrorGraphController.HistogramDisplayType == DisplayTypeMassError.products;
        }

        private void massErrorAllTransitionsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ChangeMassErrorTransition(TransitionMassError.all);
        }

        private void massErrorBestTransitionsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ChangeMassErrorTransition(TransitionMassError.best);
        }

        private void MassErrorPrecursorsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ChangeMassErrorDisplayType(DisplayTypeMassError.precursors);
        }

        private void MassErrorProductsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ChangeMassErrorDisplayType(DisplayTypeMassError.products);
        }

        private void massErrorXAxisContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            massErrorMassToChargContextMenuItem.Checked = MassErrorGraphController.Histogram2DXAxis == Histogram2DXAxis.mass_to_charge;
            massErorrRetentionTimeContextMenuItem.Checked = MassErrorGraphController.Histogram2DXAxis == Histogram2DXAxis.retention_time;
        }

        private void massErorrRetentionTimeContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.UpdateXAxis(Histogram2DXAxis.retention_time);
        }

        private void massErrorMassToChargContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.UpdateXAxis(Histogram2DXAxis.mass_to_charge);
        }

        private void showMassErrorLegendContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowMassErrorLegend(!Settings.Default.ShowMassErrorLegend);
        }

        private void massErrorlogScaleContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SwitchLogScale();
        }

        private void binCountContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            UpdatePpmMenuItem(ppm05ContextMenuItem, 0.5);
            UpdatePpmMenuItem(ppm10ContextMenuItem, 1.0);
            UpdatePpmMenuItem(ppm15ContextMenuItem, 1.5);
            UpdatePpmMenuItem(ppm20ContextMenuItem, 2.0);
        }

        private void UpdatePpmMenuItem(ToolStripMenuItem toolStripMenuItem, double ppm)
        {
            toolStripMenuItem.Checked = Settings.Default.MassErorrHistogramBinSize == ppm;
            toolStripMenuItem.Text = string.Format(@"{0:F01} ppm", ppm);
        }

        private void ppm05ContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.UpdateBinSize(0.5);
        }

        private void ppm10ContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.UpdateBinSize(1);
        }

        private void ppm15ContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.UpdateBinSize(1.5);
        }

        private void ppm20ContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.UpdateBinSize(2);
        }

        private void massErrorTargetsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPointsTypeMassError(PointsTypeMassError.targets);
        }

        private void massErrorDecoysContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPointsTypeMassError(PointsTypeMassError.decoys);
        }

        private void massErrorTargets1FDRContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPointsTypeMassError(PointsTypeMassError.targets_1FDR);
        }

        private void massErrorPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowMassErrorPropertyDlg();
        }

        #endregion
    }
}
