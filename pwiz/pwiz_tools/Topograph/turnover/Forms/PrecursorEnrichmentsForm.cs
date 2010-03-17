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
using DigitalRune.Windows.Docking;
using pwiz.MSGraph;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PrecursorEnrichmentsForm : DistributionResultsForm
    {
        private readonly ZedGraphControl barGraphControl;
        public PrecursorEnrichmentsForm(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis)
        {
            InitializeComponent();
            barGraphControl = new ZedGraphControl
                                  {
                                      Dock = DockStyle.Fill,
                                  };
            barGraphControl.GraphPane.Title.Text = null;
            splitContainer1.Panel2.Controls.Add(barGraphControl);

            tbxIntermediateLevelCount.Leave +=
                (o, e) => PeptideAnalysis.IntermediateLevels = Convert.ToInt32(tbxIntermediateLevelCount.Text);
            Text = "Precursor Enrichments";
            colTracerPercent.DefaultCellStyle.Format = "0.##%";
            colTracerFormulaPercent.DefaultCellStyle.Format = "0.##%";
        }


        public override void Recalculate()
        {
            tbxIntermediateLevelCount.Text = PeptideAnalysis.IntermediateLevels.ToString();
            IList<double> observedIntensities;
            IDictionary<TracerPercentFormula,IList<double>> predictedIntensities;
            var precursorEnrichments = PeptideFileAnalysis.PeptideDistributions.ComputePrecursorEnrichments(
                PeptideFileAnalysis.Peaks,
                out observedIntensities, out predictedIntensities);
            DisplayDistributionResults(precursorEnrichments, observedIntensities, predictedIntensities, barGraphControl);
            if (precursorEnrichments == null)
            {
                tbxScore.Text = "";
            }
            else
            {
                tbxScore.Text = precursorEnrichments.Score.ToString();
            }
        }

        protected override DataGridView GridViewFormulas
        {
            get { return gridViewFormulas; }
        }

        protected override DataGridView GridViewTracerPercents
        {
            get { return gridViewTracerPercents; }
        }
    }
}
