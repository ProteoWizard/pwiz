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
    public partial class SettingsStep : DashboardStep
    {
        public SettingsStep()
        {
            InitializeComponent();
            Title = "Tell Topograph about your labels and modifications";
        }

        public override bool IsCurrent
        {
            get { return Workspace != null && Workspace.PeptideAnalyses.Count == 0 && Workspace.Peptides.Count > 0; }
        }

        private void BtnEditModificationsOnClick(object sender, EventArgs e)
        {
            TopographForm.EditModifications();
        }

        private void BtnDefineLabelsOnClick(object sender, EventArgs e)
        {
            TopographForm.EditIsotopeLabels();
        }

        protected override void UpdateStepStatus()
        {
            if (Workspace == null)
            {
                Enabled = false;
                lblModifications.Text = lblLabels.Text = "No workspace is open";
            }
            else if (!Workspace.IsLoaded)
            {
                Enabled = false;
                lblModifications.Text = lblLabels.Text = "Waiting for workspace to load";
            }
            else
            {
                Enabled = true;
                if (!Workspace.Modifications.Any())
                {
                    lblModifications.Text =
                        "No modifications are defined.  Use this feature to tell Topograph about the static modifications that your experimental protocol uses.";
                }
                else
                {
                    if (Workspace.Modifications.Count() == 1)
                    {
                        var modification = Workspace.Modifications.First();
                        lblModifications.Text = string.Format(
                            "This workspace has one static modification: '{0}' is {1} by '{2}' Daltons.",
                            modification.Key,
                            modification.Value >= 0 ? "heavier" : "lighter",
                            Math.Abs(modification.Value));
                    }
                    else
                    {
                        lblModifications.Text = string.Format("This workspace uses '{0}' static modifications.",
                                                              Workspace.Modifications.Count());
                    }
                }
                var tracerDefs = Workspace.GetTracerDefs();
                if (tracerDefs.Count == 0)
                {
                    lblLabels.Text = "There are no heavy isotope labels specified in this workspace.";
                    btnDefineLabels.Text = "Define New Isotope Label...";
                }
                else
                {
                    btnDefineLabels.Text = "Manage Isotope Labels...";
                    if (tracerDefs.Count == 1)
                    {
                        var tracerDef = tracerDefs.First();
                        if (tracerDef.AminoAcidSymbol == null)
                        {
                            lblLabels.Text =
                                string.Format(
                                    "This workspace has specified an isotope label on the element '{0}' with mass delta {1}",
                                    tracerDef.TraceeSymbol, tracerDef.DeltaMass);
                        }
                        else
                        {
                            lblLabels.Text =
                                string.Format(
                                    "This workspace has specified an isotope label on the amino acid '{0}' with mass delta {1}",
                                    tracerDef.TraceeSymbol, tracerDef.DeltaMass);
                        }
                    }
                    else
                    {
                        lblLabels.Text = string.Format("There are '{0}' labels specified in this workspace.",
                                                       tracerDefs.Count);
                    }
                }
            }
            base.UpdateStepStatus();
        }

        private void BtnEditMiscOnClick(object sender, EventArgs e)
        {
            TopographForm.EditMachineSettings();
        }
    }
}
