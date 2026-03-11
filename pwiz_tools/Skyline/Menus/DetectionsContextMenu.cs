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
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Menus
{
    public partial class DetectionsContextMenu : SkylineControl
    {
        public DetectionsContextMenu(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
        }

        public void BuildDetectionsGraphMenu(GraphSummary graph, ToolStrip menuStrip)
        {
            // Store original menu items in an array, and insert a separator
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
                menuStrip.Items.Insert(iUnzoom, detectionsToolStripSeparator1);

            // Insert skyline specific menus
            int iInsert = 0;
            var graphType = graph.Type;

            menuStrip.Items.Insert(iInsert++, detectionsGraphTypeToolStripMenuItem);
            menuStrip.Items.Insert(iInsert++, detectionsTargetToolStripMenuItem);

            menuStrip.Items.Insert(iInsert++, detectionsToolStripSeparator2);
            if (graphType == GraphTypeSummary.detections)
                menuStrip.Items.Insert(iInsert++, detectionsShowToolStripMenuItem);
            menuStrip.Items.Insert(iInsert++, detectionsYScaleToolStripMenuItem);
            menuStrip.Items.Insert(iInsert++, detectionsPropertiesToolStripMenuItem);
            detectionsPropertiesToolStripMenuItem.Tag = graph;
            menuStrip.Items.Insert(iInsert++, detectionsToolStripSeparator3);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                string tag = (string)item.Tag;
                if (tag == @"set_default" || tag == @"show_val")
                    menuStrip.Items.Remove(item);
            }

            //Update menu according to the current settings
            detectionsShowMeanToolStripMenuItem.Checked = DetectionsGraphController.Settings.ShowMean;
            detectionsShowSelectionToolStripMenuItem.Checked = DetectionsGraphController.Settings.ShowSelection;
            detectionsShowLegendToolStripMenuItem.Checked = DetectionsGraphController.Settings.ShowLegend;
            detectionsShowAtLeastNToolStripMenuItem.Checked = DetectionsGraphController.Settings.ShowAtLeastN;

            foreach (var item in new[]
            {
                detectionsYScaleOneToolStripMenuItem,
                detectionsYScalePercentToolStripMenuItem
            })
            {
                item.Checked = ((int) item.Tag) == DetectionsGraphController.Settings.YScaleFactor.Value;
                item.Text = DetectionsGraphController.YScaleFactorType.GetValues()
                    .First((e) => ((int) item.Tag) == e.Value).ToString();
            }


            foreach (var item in new[]
            {
                detectionsTargetPrecursorToolStripMenuItem,
                detectionsTargetPeptideToolStripMenuItem
            })
                item.Checked = ((int)item.Tag) == DetectionsGraphController.Settings.TargetType.Value;
        }

        private void detectionsPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                if (item.Tag is GraphSummary graph)
                    SkylineWindow.ShowDetectionsPropertyDlg(graph);
            }
        }

        private void detectionsYScaleOneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DetectionsGraphController.Settings.YScaleFactor = DetectionsGraphController.YScaleFactorType.ONE;
            SkylineWindow.UpdateDetectionsGraph();
        }

        private void detectionsYScalePercentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DetectionsGraphController.Settings.YScaleFactor = DetectionsGraphController.YScaleFactorType.PERCENT;
            SkylineWindow.UpdateDetectionsGraph();
        }

        private void detectionsShowSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DetectionsGraphController.Settings.ShowSelection = !DetectionsGraphController.Settings.ShowSelection;
            SkylineWindow.UpdateDetectionsGraph();
        }

        private void detectionsShowLegendToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DetectionsGraphController.Settings.ShowLegend = !DetectionsGraphController.Settings.ShowLegend;
            SkylineWindow.UpdateDetectionsGraph();
        }

        private void detectionsShowMeanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DetectionsGraphController.Settings.ShowMean = !DetectionsGraphController.Settings.ShowMean;
            SkylineWindow.UpdateDetectionsGraph();
        }

        private void detectionsShowAtLeastNToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DetectionsGraphController.Settings.ShowAtLeastN = !DetectionsGraphController.Settings.ShowAtLeastN;
            SkylineWindow.UpdateDetectionsGraph();
        }

        private void detectionsTargetPrecursorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DetectionsGraphController.Settings.TargetType = DetectionsGraphController.TargetType.PRECURSOR;
            SkylineWindow.UpdateDetectionsGraph();
        }

        private void detectionsTargetPeptideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DetectionsGraphController.Settings.TargetType = DetectionsGraphController.TargetType.PEPTIDE;
            SkylineWindow.UpdateDetectionsGraph();
        }

        private void detectionsGraphTypeReplicateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowDetectionsReplicateComparisonGraph();
        }

        private void detectionsGraphTypeHistogramToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowDetectionsHistogramGraph();
        }
    }
}
