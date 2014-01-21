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
using System.Globalization;
using System.Linq;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class StatusForm : WorkspaceForm
    {
        public StatusForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
        }

        private void Timer1OnTick(object sender, EventArgs e)
        {
            String chromatogramStatus;
            int chromatogramProgress;
            Workspace.ChromatogramGenerator.GetProgress(out chromatogramStatus, out chromatogramProgress);
            if (!Workspace.SavedWorkspaceChange.HasChromatogramMassChange)
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
            if (!Workspace.SavedWorkspaceChange.HasTurnoverChange)
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
            int openAnalyses = 0;
            if (Workspace.Data.PeptideAnalyses != null)
            {
                openAnalyses = Workspace.Data.PeptideAnalyses.Values.Count(pa => pa.ChromatogramsWereLoaded);
            }
            if (Workspace.SavedData.PeptideAnalyses != null)
            {
                openAnalyses = Math.Max(openAnalyses, Workspace.SavedData.PeptideAnalyses.Values.Count(pa => pa.ChromatogramsWereLoaded));
            }
            tbxOpenAnalyses.Text = openAnalyses.ToString(CultureInfo.CurrentCulture);
            tbxMemory.Text = Math.Round(System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1048576.0, 1) + "MB";
            btnSuspendChromatogram.Text = Workspace.ChromatogramGenerator.IsSuspended ? "Resume" : "Suspend";
        }

        private void BtnOkOnClick(object sender, EventArgs e)
        {
            Close();
        }

        private void BtnGarbageCollectOnClick(object sender, EventArgs e)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }

        private void BtnSuspendChromatogramOnClick(object sender, EventArgs e)
        {
            Workspace.ChromatogramGenerator.IsSuspended = !Workspace.ChromatogramGenerator.IsSuspended;
        }
    }
}