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

namespace pwiz.Topograph.ui.Forms
{
    public partial class StatusForm : WorkspaceForm
    {
        public StatusForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            String chromatogramStatus;
            int chromatogramProgress;
            Workspace.ChromatogramGenerator.GetProgress(out chromatogramStatus, out chromatogramProgress);
            if (Workspace.SavedWorkspaceVersion.ChromatogramsValid(Workspace.WorkspaceVersion))
            {
                tbxChromatogramMessage.Text = Workspace.ChromatogramGenerator.PendingAnalysisCount + " analyses left to process.";
                if (Workspace.ChromatogramGenerator.IsRequeryPending())
                {
                    tbxChromatogramMessage.Text += " (Approximate)";
                }
                if (string.IsNullOrEmpty(Workspace.GetDataDirectory()))
                {
                    tbxChromatogramMessage.Text += " (No data directory)";
                }
            }
            else
            {
                tbxChromatogramMessage.Text =
                    "Workspace settings are unsaved.  Only processing open Analyses.";
            }
            if (Workspace.SavedWorkspaceVersion.Equals(Workspace.WorkspaceVersion))
            {
                tbxResultCalculatorMessage.Text = Workspace.ResultCalculator.PendingAnalysisCount + " analyses left to process.";
                if (Workspace.ResultCalculator.IsRequeryPending())
                {
                    tbxResultCalculatorMessage.Text += " (Approximate)";
                }
            }
            else
            {
                tbxResultCalculatorMessage.Text =
                    "Workspace settings are unsaved.  Only processing open Analyses.";
            }
            tbxChromatogramStatus.Text = chromatogramStatus;
            pbChromatogram.Value = chromatogramProgress;
            tbxResultCalculatorStatus.Text = Workspace.ResultCalculator.StatusMessage;
            tbxOpenAnalyses.Text = Workspace.PeptideAnalyses.ListChildren().Count.ToString();
            tbxMemory.Text = Math.Round(System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1048576.0, 1) + "MB";
            btnSuspendChromatogram.Text = Workspace.ChromatogramGenerator.IsSuspended ? "Resume" : "Suspend";
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnGarbageCollect_Click(object sender, EventArgs e)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }

        private void btnSuspendChromatogram_Click(object sender, EventArgs e)
        {
            Workspace.ChromatogramGenerator.IsSuspended = !Workspace.ChromatogramGenerator.IsSuspended;
        }
    }
}