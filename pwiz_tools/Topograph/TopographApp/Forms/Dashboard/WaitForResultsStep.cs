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
using System.Collections.Generic;
using System.Windows.Forms;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.SystemUtil;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms.Dashboard
{
    public partial class WaitForResultsStep : DashboardStep
    {
        private BackgroundQuery _completedBackgroundQuery;
        private BackgroundQuery _runningBackgroundQuery;
        public WaitForResultsStep()
        {
            InitializeComponent();
            Title = "Wait for chromatograms to be generated and results to be calculated.";
        }
        public override bool IsCurrent
        {
            get
            {
                if (Workspace == null || Workspace.PeptideAnalyses.ChildCount == 0)
                {
                    return false;
                }
                if (_completedBackgroundQuery == null)
                {
                    return true;
                }
                if (_completedBackgroundQuery.ChromatogramsMissing > 0 || _completedBackgroundQuery.ResultsMissing > 0)
                {
                    return true;
                }
                return false;
            }
        }

        protected override void UpdateStepStatus()
        {
            if (null != _runningBackgroundQuery)
            {
                if (!_runningBackgroundQuery.IsWorkspace(Workspace))
                {
                    _runningBackgroundQuery.Dispose();
                    _runningBackgroundQuery = null;
                }
                else if (!_runningBackgroundQuery.IsRunning)
                {
                    _completedBackgroundQuery = _runningBackgroundQuery;
                    _runningBackgroundQuery = null;
                }
            }
            if (null != _completedBackgroundQuery)
            {
                if (0 == _completedBackgroundQuery.TotalChromatograms)
                {
                    lblChromatogramStatus.Text =
                        "No chromatograms need to be generated in this workspace.  Choose some peptides to analyze.";
                    SetProgressBarValue(progressBarChromatograms, 0);
                }
                else
                {
                    lblChromatogramStatus.Text = string.Format("{0} of {1} chromatograms generated.",
                                                               _completedBackgroundQuery.ChromatogramsPresent,
                                                               _completedBackgroundQuery.TotalChromatograms);
                    SetProgressBarValue(progressBarChromatograms, 100*_completedBackgroundQuery.ChromatogramsPresent/
                                                     _completedBackgroundQuery.TotalChromatograms);
                }
                if (0 == _completedBackgroundQuery.TotalResults)
                {
                    lblResultsStatus.Text =
                        "No results need to be calculated in this workspace.  Choose some peptides to analyze.";
                    SetProgressBarValue(progressBarResults, 0);
                }
                else
                {
                    lblResultsStatus.Text = string.Format("{0} of {1} results calculated.",
                                                          _completedBackgroundQuery.ResultsPresent,
                                                          _completedBackgroundQuery.TotalResults);
                    SetProgressBarValue(progressBarResults, 100*_completedBackgroundQuery.ResultsPresent/
                                               _completedBackgroundQuery.TotalResults);
                }
                if (0 == _completedBackgroundQuery.ForeignLockCount)
                {
                    panelForeignLocks.Visible = false;
                }
                else
                {
                    lblForeignLocks.Text =
                        string.Format(
                            "The database says that there are {0} tasks being performed by other instances of Topograph",
                            _completedBackgroundQuery.ForeignLockCount);
                    panelForeignLocks.Visible = true;
                }
            }
            else
            {
                lblChromatogramStatus.Text = "No information yet";
                lblResultsStatus.Text = "No information yet";
                SetProgressBarValue(progressBarChromatograms, 0);
                SetProgressBarValue(progressBarResults, 0);
            }
            if (Workspace != null && _runningBackgroundQuery == null)
            {
                if (_completedBackgroundQuery == null || _completedBackgroundQuery.IsStale())
                {
                    _runningBackgroundQuery = new BackgroundQuery(this);
                    _runningBackgroundQuery.RunQuery();
                }
            }
            base.UpdateStepStatus();
        }

        /// <summary>
        /// Sets the value of a ProgressBar in a roundabout way to prevent it from animating
        /// the transition from the current value to the new value.
        /// Workaround from:
        /// http://stackoverflow.com/questions/5332616/disabling-net-progressbar-animation-when-changing-value
        /// "If you move the progress backwards, the animation is not shown."
        /// </summary>
        private static void SetProgressBarValue(ProgressBar progressBar, int value)
        {
            if (value <= progressBar.Value)
            {
                progressBar.Value = value;
            }
            else if (value == 100)
            {
                progressBar.Value = 100;
                progressBar.Value = 99;
                progressBar.Value = 100;
            }
            else
            {
                progressBar.Value = value + 1;
                progressBar.Value = value;
            }
        }

        class BackgroundQuery : MustDispose
        {
            private readonly WaitForResultsStep _waitForResultsStep;
            private long _lastChangeId;
            private ISession _session;
            private readonly WeakReference _workspaceRef = new WeakReference(null);

            public BackgroundQuery(WaitForResultsStep waitForResultsStep)
            {
                _waitForResultsStep = waitForResultsStep;
                _workspaceRef.Target = waitForResultsStep.Workspace;
            }

            public void RunQuery()
            {
                var workspace = _workspaceRef.Target as Workspace;
                if (null == workspace)
                {
                    return;
                }
                _session = workspace.OpenSession();
                IsRunning = true;
                _lastChangeId = workspace.Reconciler.LastChangeId;
                new Action<Workspace>(RunQueryBackground).BeginInvoke(workspace, null, null);
            }

            public bool IsWorkspace(Workspace workspace)
            {
                return ReferenceEquals(workspace, _workspaceRef.Target);
            }

            private void RunQueryBackground(Workspace workspace)
            {
                try
                {
                    var queryResultsPresent =
                    _session.CreateQuery("SELECT COUNT(F.Id) FROM " + typeof(DbPeptideFileAnalysis) +
                        " F WHERE F.PeakCount <> 0 AND F.TracerPercent IS NOT NULL");
                    var queryResultsMissing =
                        _session.CreateQuery("SELECT COUNT(F.Id) FROM " + typeof(DbPeptideFileAnalysis) +
                                            " F WHERE F.PeakCount = 0 OR F.TracerPercent IS NULL");
                    var queryChromatogramsPresent =
                        _session.CreateQuery("SELECT COUNT(F.Id) FROM " + typeof(DbPeptideFileAnalysis) +
                                            " F WHERE F.ChromatogramSet IS NOT NULL");
                    var queryChromatogramsMissing =
                        _session.CreateQuery("SELECT COUNT(F.Id) FROM " + typeof(DbPeptideFileAnalysis) +
                                            " F WHERE F.ChromatogramSet IS NULL");
                    var queryForeignLock = _session.CreateQuery("SELECT COUNT(L.Id) FROM " + typeof (DbLock) + " L WHERE L.InstanceIdBytes <> :instanceIdBytes")
                        .SetParameter("instanceIdBytes", workspace.InstanceId.ToByteArray());

                    ResultsPresent = Convert.ToInt32(queryResultsPresent.UniqueResult());
                    ResultsMissing = Convert.ToInt32(queryResultsMissing.UniqueResult());
                    ChromatogramsPresent = Convert.ToInt32(queryChromatogramsPresent.UniqueResult());
                    ChromatogramsMissing = Convert.ToInt32(queryChromatogramsMissing.UniqueResult());
                    ForeignLockCount = Convert.ToInt32(queryForeignLock.UniqueResult());
                    CheckDisposed();
                    _waitForResultsStep.BeginInvoke(new Action(_waitForResultsStep.UpdateStepStatus));
                }
                catch (Exception exception)
                {
                    if (!IsDisposed())
                    {
                        ErrorHandler.LogException("Dashboard", "Exception while querying chromatogram status", exception);
                    }
                }
                finally
                {
                    _session.Dispose();
                    IsRunning = false;
                }
            }

            public override void Dispose()
            {
                _session.Dispose();
                base.Dispose();
            }

            public bool IsRunning
            {
                get; private set;
            }

            public int ChromatogramsPresent { get; private set; }
            public int ChromatogramsMissing { get; private set; }
            public int TotalChromatograms { get { return ChromatogramsPresent + ChromatogramsMissing; } }
            public int ResultsPresent { get; private set; }
            public int ResultsMissing { get; private set; }
            public int TotalResults { get { return ResultsPresent + ResultsMissing; } }
            public int ForeignLockCount { get; private set; }
            public bool IsStale()
            {
                return !IsRunning && !IsWorkspace(_waitForResultsStep.Workspace)
                       || _waitForResultsStep.Workspace != null && _lastChangeId != _waitForResultsStep.Workspace.Reconciler.LastChangeId
                       || ForeignLockCount > 0;
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            UpdateStepStatus();
        }

        private void btnCancelTasks_Click(object sender, EventArgs e)
        {
            IList<long> idsToDelete;
            using (var session = Workspace.OpenSession())
            {
                idsToDelete =
                    session.CreateQuery("SELECT L.Id FROM " + typeof (DbLock) + " L WHERE L.InstanceIdBytes <> :instanceIdBytes").
                        SetParameter("instanceIdBytes", Workspace.InstanceId.ToByteArray()).List<long>();
            }
            foreach (var id in idsToDelete)
            {
                try
                {
                    using (var session = Workspace.OpenWriteSession())
                    {
                        session.BeginTransaction();
                        var dbLock = session.Load<DbLock>(id);
                        session.Delete(dbLock);
                        session.Transaction.Commit();
                    }
                }
                catch (StaleObjectStateException)
                {
                    // ignore
                }
            }
        }
    }
}
