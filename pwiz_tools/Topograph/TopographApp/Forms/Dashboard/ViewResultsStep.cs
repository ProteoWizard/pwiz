/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Topograph.ui.Forms.Dashboard
{
    public partial class ViewResultsStep : DashboardStep
    {
        public ViewResultsStep()
        {
            InitializeComponent();
            Title = "View Results";
        }

        public override bool IsCurrent
        {
            get { return Workspace != null && Workspace.PeptideAnalyses.Count > 0; }
        }

        private void LinkResultsPerReplicateOnLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            TopographForm.ShowResultsByReplicate();
        }

        private void LinkResultsByCohortOnLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            TopographForm.ShowResultsPerGroup();
        }

        private void LinkHalfLivesOnLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            TopographForm.ShowHalfLivesForm();
        }

        protected override void UpdateStepStatus()
        {
            if (Workspace == null || !Workspace.IsLoaded)
            {
                Enabled = false;
            }
            else
            {
                Enabled = true;
                if (Workspace.GetTracerDefs().Count == 0)
                {
                    lblHalfLives.Text = "Topograph can calculate the half lives of proteins, but only if you have told Topograph about the heavy isotope labels you are using.";
                    linkHalfLives.Enabled = false;
                }
                else
                {
                    bool anyTimePoints =
                        Workspace.MsDataFiles.Any(msDataFile => null != msDataFile.TimePoint);
                    if (!anyTimePoints)
                    {
                        linkHalfLives.Enabled = false;
                        lblHalfLives.Text =
                            "Topograph can calculate the half lives of proteins, but you need to first tell Topograph which time point each sample corresponds to.";
                    }
                    else
                    {
                        linkHalfLives.Enabled = true;
                        lblHalfLives.Text = "Topograph can calculate the half lives of proteins.";
                    }
                }
            }
            base.UpdateStepStatus();
        }

        private void LinkDataFilesOnLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            TopographForm.ShowDataFiles();
        }

        private void LinkPeptideAnalysesOnLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            TopographForm.ShowPeptideAnalyses();
        }
    }
}
