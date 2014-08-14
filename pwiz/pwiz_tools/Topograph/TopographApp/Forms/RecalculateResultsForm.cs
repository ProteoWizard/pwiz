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
using System.Windows.Forms;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class RecalculateResultsForm : WorkspaceForm
    {
        public RecalculateResultsForm(Workspace workspace)
            : base(workspace)
        {
            InitializeComponent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RefreshStats();
        }

        private void BtnRefreshOnClick(object sender, EventArgs e)
        {
            RefreshStats();
        }

        private void RefreshStats()
        {
            new Action(RefreshStatsNow).BeginInvoke(null, null);
        }

        private void RefreshStatsNow()
        {
            using (var session = Workspace.OpenSession())
            {
                var queryResultsPresent =
                    session.CreateQuery("SELECT COUNT(F.Id) FROM " + typeof (DbPeptideFileAnalysis) +
                                        " F WHERE F.PeakCount <> 0 AND F.TracerPercent IS NOT NULL");
                var queryResultsMissing =
                    session.CreateQuery("SELECT COUNT(F.Id) FROM " + typeof (DbPeptideFileAnalysis) +
                                        " F WHERE F.PeakCount = 0 OR F.TracerPercent IS NULL");
                var queryChromatogramsPresent =
                    session.CreateQuery("SELECT COUNT(F.Id) FROM " + typeof (DbPeptideFileAnalysis) +
                                        " F WHERE F.ChromatogramSet IS NOT NULL");
                var queryChromatogramsMissing =
                    session.CreateQuery("SELECT COUNT(F.Id) FROM " + typeof (DbPeptideFileAnalysis) +
                                        " F WHERE F.ChromatogramSet IS NULL");
                int resultsPresent = Convert.ToInt32(queryResultsPresent.UniqueResult());
                int resultsMissing = Convert.ToInt32(queryResultsMissing.UniqueResult());
                int chromatogramsPresent = Convert.ToInt32(queryChromatogramsPresent.UniqueResult());
                int chromatogramsMissing = Convert.ToInt32(queryChromatogramsMissing.UniqueResult());
                BeginInvoke(new Action(delegate
                                {
                                    tbxResultsPresent.Text = resultsPresent.ToString(CultureInfo.CurrentCulture);
                                    tbxResultsMissing.Text = resultsMissing.ToString(CultureInfo.CurrentCulture);
                                    tbxChromatogramsPresent.Text = chromatogramsPresent.ToString(CultureInfo.CurrentCulture);
                                    tbxChromatogramsMissing.Text = chromatogramsMissing.ToString(CultureInfo.CurrentCulture);

                                }));
            }
        }

        private void BtnRegenerateChromatogramsOnClick(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Are you sure you want to delete all of the chromatograms in this workspace?  Regenerating chromatograms can take a really long time.", Program.AppName, MessageBoxButtons.OKCancel) != DialogResult.OK)
            {
                return;
            }

            var change = new WorkspaceChangeArgs(Workspace.Data, Workspace.SavedData);
            change.AddChromatogramMassChange();
            
            UpdateWorkspaceVersion(change);
            Workspace.ChromatogramGenerator.SetRequeryPending();
            RefreshStats();
        }

        private void BtnRecalculateResultsOnClick(object sender, EventArgs e)
        {
            var change = new WorkspaceChangeArgs(Workspace.Data, Workspace.SavedData);
            change.AddPeakPickingChange();
            UpdateWorkspaceVersion(change);
            Workspace.ResultCalculator.SetRequeryPending();
            RefreshStats();
        }

        // ReSharper disable AccessToDisposedClosure
        private void UpdateWorkspaceVersion(WorkspaceChangeArgs v)
        {
            using (var session = Workspace.OpenSession())
            {
                using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Updating Workspace"))
                {
                    var broker = new LongOperationBroker(b =>
                    {
                        session.BeginTransaction();
                        Workspace.UpdateWorkspaceVersion(b, session, v);
                        session.Transaction.Commit();
                    }, longWaitDialog, session);
                    broker.LaunchJob();
                }
            }
        }
        // ReSharper restore AccessToDisposedClosure
    }
}
