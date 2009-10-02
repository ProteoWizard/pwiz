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
using MSGraph;
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
                                      Dock = DockStyle.Fill
                                  };
            splitContainer1.Panel2.Controls.Add(barGraphControl);

            tbxInitialPrecursorEnrichment.Leave +=
                (o, e) => PeptideAnalysis.InitialEnrichment = Convert.ToDouble(tbxInitialPrecursorEnrichment.Text);
            tbxFinalPrecursorEnrichment.Leave +=
                (o, e) => PeptideAnalysis.FinalEnrichment = Convert.ToDouble(tbxFinalPrecursorEnrichment.Text);
            tbxIntermediateLevelCount.Leave +=
                (o, e) => PeptideAnalysis.IntermediateLevels = Convert.ToInt32(tbxIntermediateLevelCount.Text);
            Text = "Precursor Enrichments";
        }


        public override void Recalculate()
        {
            IList<double> observedIntensities;
            IList<IList<double>> predictedIntensities;
            var precursorEnrichments =
                PeptideFileAnalysis.ComputePrecursorEnrichments(out observedIntensities, out predictedIntensities);
            if (precursorEnrichments == null)
            {
                return;
            }
            List<String> labels = new List<string>();
            for (int i = 0; i < predictedIntensities.Count; i++)
            {
                var ape = precursorEnrichments.GetChild(i).EnrichmentValue;
                labels.Add(Math.Round(ape) + "%");
            }
            DisplayDistributionResults(precursorEnrichments, observedIntensities, predictedIntensities, 
                labels, dataGridView, barGraphControl);
            if (precursorEnrichments != null)
            {
                tbxScore.Text = precursorEnrichments.Score.ToString();
            }
            else
            {
                tbxScore.Text = "";
            }
        }

        protected override void PeptideFileAnalysisChanged()
        {
            base.PeptideFileAnalysisChanged();
            tbxInitialPrecursorEnrichment.Text = PeptideFileAnalysis.PeptideAnalysis.InitialEnrichment.ToString();
            tbxFinalPrecursorEnrichment.Text = PeptideFileAnalysis.PeptideAnalysis.FinalEnrichment.ToString();
            tbxIntermediateLevelCount.Text = PeptideFileAnalysis.PeptideAnalysis.IntermediateLevels.ToString();
        }
    }
}
