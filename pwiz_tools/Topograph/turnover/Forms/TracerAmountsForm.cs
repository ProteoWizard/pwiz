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
using pwiz.Topograph.Model;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class TracerAmountsForm : DistributionResultsForm
    {
        public const String FORM_TYPE_NAME = "Tracer Amounts";
        private readonly ZedGraphControl barGraphControl;
        public TracerAmountsForm(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis)
        {
            InitializeComponent();
            barGraphControl = new ZedGraphControl
                                  {
                                      Dock = DockStyle.Fill
                                  };
            splitContainer1.Panel2.Controls.Add(barGraphControl);
            Text = "Tracer Amounts";
        }

        public override void Recalculate()
        {
            List<String> labels = new List<string>();
            for (int i = 0; i <= PeptideFileAnalysis.TracerCount; i++)
            {
                labels.Add(i.ToString());
            }
            IList<double> observedIntensities;
            IList<IList<double>> predictedIntensities;
            var tracerAmounts = PeptideFileAnalysis.ComputeTracerAmounts(out observedIntensities, out predictedIntensities);
            
            DisplayDistributionResults(tracerAmounts, observedIntensities, predictedIntensities, labels, dataGridView1, barGraphControl);
            if (tracerAmounts != null)
            {
                tbxScore.Text = tracerAmounts.Score.ToString();
                tbxAPE.Text = tracerAmounts.AverageEnrichmentValue.ToString();
            }
            else
            {
                tbxScore.Text = "";
                tbxAPE.Text = "";
            }
        }
    }
}
