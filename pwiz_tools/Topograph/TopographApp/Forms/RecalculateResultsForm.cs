using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
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

        private void btnRefresh_Click(object sender, EventArgs e)
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
                                    tbxResultsPresent.Text = resultsPresent.ToString();
                                    tbxResultsMissing.Text = resultsMissing.ToString();
                                    tbxChromatogramsPresent.Text = chromatogramsPresent.ToString();
                                    tbxChromatogramsMissing.Text = chromatogramsMissing.ToString();

                                }));
            }
        }

        private void btnRegenerateChromatograms_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Are you sure you want to delete all of the chromatograms in this workspace?  Regenerating chromatograms can take a really long time.", Program.AppName, MessageBoxButtons.OKCancel) != DialogResult.OK)
            {
                return;
            }

            UpdateWorkspaceVersion(Workspace.SavedWorkspaceVersion.IncMassVersion());
            Workspace.ChromatogramGenerator.SetRequeryPending();
            RefreshStats();
        }

        private void btnRecalculateResults_Click(object sender, EventArgs e)
        {
            UpdateWorkspaceVersion(Workspace.SavedWorkspaceVersion.IncChromatogramPeakVersion());
            Workspace.ResultCalculator.SetRequeryPending();
            RefreshStats();
        }

        private void UpdateWorkspaceVersion(WorkspaceVersion v)
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
    }
}
