/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class GraphForm : EntityModelForm
    {
        private ZedGraphControl zedGraphControl;
        private PeptideQuantity _peptideQuantity;
        public GraphForm(PeptideAnalysis peptideAnalysis) : base(peptideAnalysis)
        {
            InitializeComponent();
            zedGraphControl = new ZedGraphControl
                                  {
                                      Dock = DockStyle.Fill
                                  };
            splitContainer1.Panel2.Controls.Add(zedGraphControl);
            foreach (var graphValue in Enum.GetValues(typeof(PeptideQuantity)))
            {
                comboGraph.Items.Add(graphValue);
            }
            comboGraph.SelectedIndex = 0;
            foreach (var tracerDef in Workspace.GetTracerDefs())
            {
                comboTracer.Items.Add(tracerDef.Name);
            }
            comboTracer.SelectedIndex = 0;
        }

        public void UpdateGraph()
        {
            Text = TabText = "Graph:" + PeptideAnalysis.GetLabel();
            zedGraphControl.GraphPane.Title.Text = PeptideAnalysis.Peptide.FullSequence;
            zedGraphControl.GraphPane.CurveList.Clear();
            zedGraphControl.GraphPane.GraphObjList.Clear();
            zedGraphControl.GraphPane.XAxis.Title.Text = "Time";
            zedGraphControl.GraphPane.YAxis.Title.Text = GraphValue.ToString();
            var cohorts = PeptideRates.GetCohorts().ToArray();
            var symbolTypes = new[]
                                  {
                                      SymbolType.Circle, SymbolType.Diamond, SymbolType.Square, SymbolType.Star,
                                      SymbolType.Plus
                                  };
            for (int iCohort = 0; iCohort < cohorts.Count(); iCohort ++)
            {
                var cohort = cohorts[iCohort];
                if (cohorts.Count() > 1 && string.IsNullOrEmpty(cohort.Key))
                {
                    continue;
                }
                var points = GetPoints(new RateKey(Convert.ToString(comboTracer.SelectedItem), GraphValue, cohort.Key));

                Color color;
                if (cohorts.Count() > 1)
                {
                    color = Color.FromArgb(128*iCohort/(cohorts.Count() - 1), 128*iCohort/(cohorts.Count() - 1),
                                           255*(cohorts.Count() - iCohort - 1)/(cohorts.Count() - 1));   
                }
                else
                {
                    color = Color.Blue;
                }
                var symbolType = symbolTypes[iCohort%symbolTypes.Length];
                var curve = zedGraphControl.GraphPane.AddCurve(cohort.Value, points, color, symbolType);
                curve.Line.IsVisible = false;
            }
            zedGraphControl.GraphPane.AxisChange();
            zedGraphControl.Invalidate();
            UpdateGrid();
        }

        private void UpdateGrid()
        {
            var cohorts = PeptideRates.GetCohorts().ToArray();
            var tracer = Convert.ToString(comboTracer.SelectedItem);
            if (dataGridView1.Rows.Count != cohorts.Count())
            {
                dataGridView1.Rows.Clear();
                dataGridView1.Rows.Add(cohorts.Count());
            }
            for (int i = 0; i < cohorts.Count(); i++)
            {
                var cohort = cohorts[i].Key;
                var row = dataGridView1.Rows[i];
                row.Cells[colCohort.Index].Value = cohort ?? "<All>";
                var rate = PeptideRates.GetChild(new RateKey(tracer, GraphValue, cohort));
                if (rate == null)
                {
                    row.Cells[colHalfLife.Index].Value = null;
                    row.Cells[colInitialTurnover.Index].Value = null;
                    row.Cells[colScore.Index].Value = null;
                }
                else
                {
                    row.Cells[colHalfLife.Index].Value = rate.HalfLife;
                    row.Cells[colInitialTurnover.Index].Value = rate.InitialTurnover;
                    row.Cells[colScore.Index].Value = rate.Score;
                }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateGraph();
        }

        public PeptideAnalysis PeptideAnalysis 
        {
            get
            {
                return (PeptideAnalysis) EntityModel;
            }
        }
        public PeptideRates PeptideRates
        {
            get
            {
                return PeptideAnalysis.PeptideRates;
            }
        }

        private PointPairList GetPoints(RateKey rateKey)
        {
            var xValues = new List<double>();
            var yValues = new List<double>();
            foreach (var entry in PeptideRates.GetPoints(rateKey))
            {
                xValues.Add(entry.Key);
                yValues.Add(entry.Value);
            }
            return new PointPairList(xValues.ToArray(), yValues.ToArray());
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            if (args.Contains(PeptideAnalysis) || args.GetEntities<MsDataFile>().Count != 0
                || args.ContainsAny(PeptideAnalysis.GetFileAnalyses(false)))
            {
                UpdateGraph();
            }
        }

        public PeptideQuantity GraphValue 
        {
            get
            {
                return _peptideQuantity;
            }
            set
            {
                if (_peptideQuantity == value)
                {
                    return;
                }
                comboGraph.SelectedItem = _peptideQuantity = value;
                UpdateGraph();
            }
        }
        private void comboGraph_SelectedIndexChanged(object sender, EventArgs e)
        {
            GraphValue = (PeptideQuantity) comboGraph.SelectedItem;
        }

        private void comboTracer_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }
    }
}
