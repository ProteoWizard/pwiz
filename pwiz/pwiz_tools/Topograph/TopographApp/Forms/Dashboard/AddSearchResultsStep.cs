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
using System.Linq;

namespace pwiz.Topograph.ui.Forms.Dashboard
{
    public partial class AddSearchResultsStep : DashboardStep
    {
        public AddSearchResultsStep()
        {
            InitializeComponent();
            Title = "Add Peptide Search Results";
        }

        public override bool IsCurrent
        {
            get { return Workspace != null && Workspace.IsLoaded && (Workspace.Peptides.Count == 0 || Workspace.MsDataFiles.Count == 0); }
        }

        private String Description
        {
// ReSharper disable UnusedMember.Local
            get { return lblStatus.Text; }
// ReSharper restore UnusedMember.Local
            set { lblStatus.Text = value; }
        }

        protected override void UpdateStepStatus()
        {
            if (Workspace == null)
            {
                Enabled = false;
            }
            else if (!Workspace.IsLoaded)
            {
                Description = "The workspace is in the process of being opened.  Please wait a few seconds.";
                Enabled = false;
            }
            else
            {
                Enabled = true;
                if (Workspace.Peptides.Count == 0)
                {
                    Description = "No search results have been added to this workspace yet.";
                }
                else
                {
                    string strPeptides = Workspace.Peptides.Count == 1 ? "1 peptide" : (Workspace.Peptides.Count + " peptides");
                    if (Workspace.MsDataFiles.Count == 0)
                    {
                        Description =
                            string.Format(
                                "{0} have been added to this workspace, but no data files.  You should add more search results.", strPeptides);
                    }
                    else
                    {
                        string strDataFiles = Workspace.MsDataFiles.Count == 1
                                                  ? "1 MS data file"
                                                  : (Workspace.MsDataFiles.Count + " MS data files");
                        Description =
                            string.Format("{0} in {1} have been added to this workspace.",
                                          strPeptides, strDataFiles);
                    }
                    var peptidesWithoutDescription =
                        Workspace.Peptides.Where(
                            peptide => string.IsNullOrEmpty(peptide.ProteinDescription)).ToArray();
                    if (peptidesWithoutDescription.Length > Workspace.Peptides.Count / 2)
                    {
                        panelUpdateProteinNames.Visible = true;
                    }
                    else
                    {
                        panelUpdateProteinNames.Visible = false;
                    }
                }
            }
            base.UpdateStepStatus();
        }

        private void BtnAddSearchResultsOnClick(object sender, EventArgs e)
        {
            TopographForm.AddSearchResults();
        }

        private void BtnChooseFastaFileOnClick(object sender, EventArgs e)
        {
            TopographForm.ChooseFastaFile();
        }
    }
}
