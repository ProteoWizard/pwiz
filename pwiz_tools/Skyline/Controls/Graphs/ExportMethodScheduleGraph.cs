/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
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
using System.Windows.Forms;
using pwiz.CLI.Bruker.PrmScheduling;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class ExportMethodScheduleGraph : FormEx
    {
        public Exception Exception { get; private set; }

        private readonly RTScheduleGraphPane _pane;
        private readonly double[] _oldWindows;
        private readonly string _oldBrukerTemplate;

        public ExportMethodScheduleGraph(SrmDocument document, string brukerTemplate, BrukerTimsTofMethodExporter.Metrics brukerMetrics)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            if (brukerMetrics != null)
            {
                dataGridView.Size = graphControl.Size;
                dataGridView.Location = graphControl.Location;
                dataGridView.Anchor = graphControl.Anchor;
                dataGridView.DataSource = brukerMetrics.Table;
                foreach (DataGridViewColumn col in dataGridView.Columns)
                {
                    if (col.HeaderText.Equals(BrukerTimsTofMethodExporter.Metrics.ColMz))
                    {
                        col.DefaultCellStyle.Format = Formats.Mz;
                    }
                    else if (col.HeaderText.Equals(BrukerTimsTofMethodExporter.Metrics.ColMeanSamplingTime) ||
                             col.HeaderText.Equals(BrukerTimsTofMethodExporter.Metrics.ColMaxSamplingTime) ||
                             col.HeaderText.Equals(BrukerTimsTofMethodExporter.Metrics.ColRtBegin) ||
                             col.HeaderText.Equals(BrukerTimsTofMethodExporter.Metrics.ColRtEnd))
                    {
                        col.DefaultCellStyle.Format = Formats.SamplingTime;
                    }
                    else if (col.HeaderText.Equals(BrukerTimsTofMethodExporter.Metrics.Col1K0LowerLimit) ||
                             col.HeaderText.Equals(BrukerTimsTofMethodExporter.Metrics.Col1K0UpperLimit))
                    {
                        col.DefaultCellStyle.Format = Formats.OneOverK0;
                    }
                }
            }

            var masterPane = graphControl.MasterPane;
            masterPane.PaneList.Clear();
            masterPane.Border.IsVisible = false;

            _pane = new RTScheduleGraphPane(null, graphControl, true);
            _pane.BrukerMetrics = brukerMetrics;

            // Save existing settings
            _oldWindows = RTScheduleGraphPane.ScheduleWindows;
            RTScheduleGraphPane.ScheduleWindows = new double[] { };
            _oldBrukerTemplate = null;
            if (!string.IsNullOrEmpty(brukerTemplate))
            {
                cbGraphType.Items.AddRange(new object[]
                {
                    new MetricDisplay(GraphsResources.ExportMethodScheduleGraph_ExportMethodScheduleGraph_Concurrent_frames, SchedulingMetrics.CONCURRENT_FRAMES),
                    new MetricDisplay(GraphsResources.ExportMethodScheduleGraph_ExportMethodScheduleGraph_Max_sampling_times, SchedulingMetrics.MAX_SAMPLING_TIMES),
                    new MetricDisplay(GraphsResources.ExportMethodScheduleGraph_ExportMethodScheduleGraph_Mean_sampling_times, SchedulingMetrics.MEAN_SAMPLING_TIMES),
                    new MetricDisplay(GraphsResources.ExportMethodScheduleGraph_ExportMethodScheduleGraph_Redundancy_of_targets, SchedulingMetrics.REDUNDANCY_OF_TARGETS),
                    new MetricDisplay(GraphsResources.ExportMethodScheduleGraph_ExportMethodScheduleGraph_Targets_per_frame, SchedulingMetrics.TARGETS_PER_FRAME),
                    new MetricDisplay(GraphsResources.ExportMethodScheduleGraph_ExportMethodScheduleGraph_Target_table)
                });
                cbGraphType.SelectedIndex = 0;
                cbGraphType.Visible = true;
                _pane.BrukerMetricType = ((MetricDisplay) cbGraphType.SelectedItem).Metrics;
                if (!Equals(brukerTemplate, RTScheduleGraphPane.BrukerTemplateFile))
                {
                    _oldBrukerTemplate = RTScheduleGraphPane.BrukerTemplateFile;
                    RTScheduleGraphPane.BrukerTemplateFile = brukerTemplate;
                }
            }

            masterPane.PaneList.Add(_pane);
            UpdateGraph();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        public class MetricDisplay
        {
            public string Name { get; }
            public SchedulingMetrics Metrics { get; }

            public MetricDisplay(string name, SchedulingMetrics metrics = (SchedulingMetrics)(-1))
            {
                Name = name;
                Metrics = metrics;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        private void UpdateGraph()
        {
            try
            {
                _pane.UpdateGraph(false);
                graphControl.Invalidate();
            }
            catch (Exception x)
            {
                Exception = x;
            }
        }

        private void ExportMethodScheduleGraph_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Restore settings
            if (!Equals(_oldBrukerTemplate, RTScheduleGraphPane.BrukerTemplateFile))
            {
                RTScheduleGraphPane.BrukerTemplateFile = _oldBrukerTemplate;
            }
            RTScheduleGraphPane.ScheduleWindows = _oldWindows;
        }

        private void cbGraphType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var metricDisplay = (MetricDisplay) cbGraphType.SelectedItem;
            if (!metricDisplay.Name.Equals(GraphsResources.ExportMethodScheduleGraph_ExportMethodScheduleGraph_Target_table))
            {
                graphControl.Visible = true;
                dataGridView.Visible = false;
                _pane.BrukerMetricType = metricDisplay.Metrics;
                UpdateGraph();
            }
            else
            {
                graphControl.Visible = false;
                dataGridView.Visible = true;
            }
        }

        public ZedGraphControl GraphControl
        {
            get { return graphControl; }
        }
    }
}
