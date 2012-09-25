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
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class MiscSettingsForm : WorkspaceForm
    {
        public MiscSettingsForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            tbxMassAccuracy.Text = workspace.GetMassAccuracy().ToString();
            tbxProteinDescriptionKey.Text = workspace.GetProteinDescriptionKey();
            tbxMaxRetentionTimeShift.Text = workspace.GetMaxIsotopeRetentionTimeShift().ToString();
            tbxMinCorrelationCoefficient.Text = workspace.GetMinCorrelationCoefficient().ToString();
            tbxMinDeconvolutionScoreForAvgPrecursorPool.Text = workspace.GetMinDeconvolutionScoreForAvgPrecursorPool().ToString();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            using (Workspace.GetWriteLock())
            {
                Workspace.SetMassAccuracy(Convert.ToDouble(tbxMassAccuracy.Text));
                Workspace.SetProteinDescriptionKey(tbxProteinDescriptionKey.Text);
                Workspace.SetMaxIsotopeRetentionTimeShift(Convert.ToDouble(tbxMaxRetentionTimeShift.Text));
                Workspace.SetMinCorrelationCoefficient(double.Parse(tbxMinCorrelationCoefficient.Text));
                Workspace.SetMinDeconvolutionScoreForAvgPrecursorPool(double.Parse(tbxMinDeconvolutionScoreForAvgPrecursorPool.Text));
            }
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
