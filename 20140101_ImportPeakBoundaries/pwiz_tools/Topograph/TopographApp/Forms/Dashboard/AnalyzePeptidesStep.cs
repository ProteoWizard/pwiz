/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

namespace pwiz.Topograph.ui.Forms.Dashboard
{
    public partial class AnalyzePeptidesStep : DashboardStep
    {
        public AnalyzePeptidesStep()
        {
            InitializeComponent();
            Title = "Choose Which Peptides To Analyze";
        }

        public override bool IsCurrent
        {
            get
            {
                return Workspace != null
                       && Workspace.Peptides.Count != 0
                       && Workspace.MsDataFiles.Count != 0
                       && Workspace.GetDataDirectory() != null
                       && Workspace.PeptideAnalyses.Count == 0;
            }
        }

        protected override void UpdateStepStatus()
        {
            base.UpdateStepStatus();
            if (Workspace == null)
            {
                lblStatus.Text = "No workspace is open.";
                Enabled = false;
                return;
            }
            if (!Workspace.IsLoaded)
            {
                lblStatus.Text = "Workspace is in the process of being opened.";
                Enabled = false;
                return;
            }
            Enabled = true;
            if (Workspace.PeptideAnalyses.Count > 0)
            {
                if (Workspace.PeptideAnalyses.Count == 1)
                {
                    lblStatus.Text = "1 peptide has been analyzed";
                }
                else
                {
                    lblStatus.Text = string.Format("{0} peptides have been analyzed.",
                                                   Workspace.PeptideAnalyses.Count);
                }
                return;
            }
            if (Workspace.Peptides.Count == 0 || Workspace.MsDataFiles.Count == 0)
            {
                lblStatus.Text = "You need to add search results before you can analyze any peptides";
                return;
            }
            lblStatus.Text = "No peptides have been analyzed yet.";
        }

        private void BtnAnalyzePeptidesOnClick(object sender, EventArgs e)
        {
            TopographForm.AnalyzePeptides();
        }

        private void LinkPeptideAnalysesOnLinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            TopographForm.ShowPeptideAnalyses();
        }
    }
}
