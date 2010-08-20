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
using pwiz.Common.Chemistry;
using pwiz.Topograph.Controls;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DistributionResultsForm : PeptideFileAnalysisForm
    {
        private WorkspaceVersion _workspaceVersion;
        private DistributionResultsForm() : base(null)
        {
            
        }
        protected DistributionResultsForm(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis)
        {
            InitializeComponent();
        }
        
        protected void DisplayDistributionResults<T,N>(PeptideDistribution peptideDistribution, IList<double> observedIntensities, IDictionary<T,IList<double>> predictedIntensities, ZedGraphControl barGraphControl) 
            where T:AbstractFormula<T,N>, new()
            where N : IComparable<N>
        {
            GridViewTracerPercents.Rows.Clear();
            GridViewFormulas.Rows.Clear();
            barGraphControl.GraphPane.GraphObjList.Clear();
            barGraphControl.GraphPane.CurveList.Clear();
            if (peptideDistribution == null)
            {
                return;
            }
            var entries = predictedIntensities.ToArray();
            int massCount = observedIntensities.Count;
            double monoIsotopicMass =
                Workspace.GetAminoAcidFormulas().GetMonoisotopicMass(PeptideAnalysis.Peptide.Sequence);
            var masses = PeptideAnalysis.GetTurnoverCalculator().GetMzs(0);
            var actualBarPoints = new PointPairList();
            var excludedBarPoints = new PointPairList();
            var predictedBarPoints = new PointPairList();
            for (int iMass = 0; iMass < massCount; iMass++)
            {
                double mass = masses[iMass].Center - monoIsotopicMass;
                if (PeptideFileAnalysis.ExcludedMzs.IsMassExcluded(iMass))
                {
                    excludedBarPoints.Add(mass, observedIntensities[iMass]);
                }
                else
                {
                    actualBarPoints.Add(mass, observedIntensities[iMass]);
                }
                predictedBarPoints.Add(mass + 1.0 / 3, 0);
            }
            barGraphControl.GraphPane.XAxis.Title.Text = "Mass";
            barGraphControl.GraphPane.YAxis.Title.Text = "Fractional Abundance";
            barGraphControl.GraphPane.BarSettings.Type = BarType.Overlay;
            barGraphControl.GraphPane.AddBar("Observed Peptide", actualBarPoints, Color.Black);
            barGraphControl.GraphPane.AddBar(null, excludedBarPoints, Color.White);
            var distributions = peptideDistribution.ListChildren();
            int candidateCount = distributions.Count;

            for (int iCandidate = 0; iCandidate < candidateCount; iCandidate++)
            {
                predictedBarPoints = new PointPairList(predictedBarPoints);
                for (int iMass = 0; iMass < massCount; iMass ++)
                {
                    predictedBarPoints[iMass].Y += entries[iCandidate].Value[iMass];
                }
                
                var color = GetColor(iCandidate, candidateCount);
                var row = GridViewFormulas.Rows[GridViewFormulas.Rows.Add()];
                row.Cells[0].Value = entries[iCandidate].Key.ToString();
                row.Cells[0].Style.BackColor = color;
                row.Cells[1].Value = distributions[iCandidate].PercentAmountValue / 100;
                var label = entries[iCandidate].Key.ToDisplayString();
                barGraphControl.GraphPane.AddBar(label, predictedBarPoints, color);
            }
            barGraphControl.GraphPane.AxisChange();
            barGraphControl.Invalidate();
            foreach (var tracerDef in Workspace.GetTracerDefs())
            {
                var row = GridViewTracerPercents.Rows[GridViewTracerPercents.Rows.Add()];
                row.Cells[0].Value = tracerDef.Name;
                row.Cells[1].Value = peptideDistribution.GetTracerPercent(tracerDef) / 100;
            }
        }

        public static Color GetColor(int iCandidate, int candidateCount)
        {
            var colors = new[]
                                       {
                                            Color.FromArgb(69,114,167),
                                            Color.FromArgb(170,70,67),
                                            Color.FromArgb(137,165,78),
                                            Color.FromArgb(113,88,143),
                                            Color.FromArgb(65,152,175),
                                            Color.FromArgb(219,132,61),
                                            Color.FromArgb(147,169,207),
                                       };
            if (iCandidate < colors.Length)
            {
                return colors[iCandidate];
            }
            
            if (candidateCount == 1)
            {
                return Color.FromArgb(0, 0, 255);
            }
            return Color.FromArgb(0, 255 * iCandidate / (candidateCount - 1),
                           255 * (candidateCount - iCandidate - 1) / (candidateCount - 1));
            
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Workspace != null)
            {
                _workspaceVersion = Workspace.WorkspaceVersion;
                Recalculate();
            }
        }

        public virtual void Recalculate()
        {
        }

        protected virtual DataGridView GridViewFormulas {get { return null;}}
        protected virtual DataGridView GridViewTracerPercents { get { return null;} }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            if (!_workspaceVersion.Equals(Workspace.WorkspaceVersion) 
                || args.Contains(PeptideFileAnalysis) 
                || args.Contains(PeptideFileAnalysis.Chromatograms) 
                || args.Contains(PeptideFileAnalysis.PeptideDistributions) 
                || args.Contains(PeptideAnalysis))
            {
                _workspaceVersion = Workspace.WorkspaceVersion;
                Recalculate();
            }
        }
        protected void barGraphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            menuStrip.Items.Insert(0, new CopyEmfToolStripMenuItem(sender));
        }
    }


}
