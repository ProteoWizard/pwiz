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
using System.Linq;
using System.Windows.Forms;
using NHibernate;
using pwiz.Common.SystemUtil;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms.Dashboard
{
    public partial class WaitForResultsStep : DashboardStep
    {
        private const int NewForeignLockStaleTimeMinutes = 20;
        private const int ExistingForeignLockStaleTimeMinutes = 5;
        private int _totalChromatograms;
        private int _completedChromatograms;
        private int _totalResults;
        private int _completedResults;
        private BackgroundQuery _completedBackgroundQuery;
        private BackgroundQuery _runningBackgroundQuery;
        private readonly IDictionary<long, DateTime> _firstSeenLockTime = new Dictionary<long, DateTime>();
        private DateTime? _workspaceOpenTime;
        public WaitForResultsStep()
        {
            InitializeComponent();
            Title = "Wait for chromatograms to be generated and results to be calculated.";
        }
        public override bool IsCurrent
        {
            get
            {
                if (Workspace == null || Workspace.PeptideAnalyses.Count == 0)
                {
                    return false;
                }
                if (_completedBackgroundQuery == null)
                {
                    return true;
                }
                if (_completedChromatograms < _totalChromatograms || _completedResults < _totalResults)
                {
                    return true;
                }
                return false;
            }
        }

        protected override void UpdateStepStatus()
        {
            if (null == Workspace)
            {
                _totalResults = _totalChromatograms = 0;
                _completedResults = _completedChromatograms = 0;
            }
            else
            {
                _totalChromatograms = Workspace.PeptideAnalyses.Select(peptideAnalysis => peptideAnalysis.FileAnalyses.Count).Sum();
                _completedChromatograms = Workspace.PeptideAnalyses
                    .Select(peptideAnalysis => peptideAnalysis.FileAnalyses.Count(
                        fileAnalysis => fileAnalysis.ChromatogramSetId.HasValue))
                    .Sum();
                _totalResults = _totalChromatograms;
                _completedResults = Workspace.PeptideAnalyses
                                         .Select(peptideAnalysis => peptideAnalysis.FileAnalyses.Count(
                                             fileAnalysis => fileAnalysis.PeakData.IsCalculated))
                                         .Sum();
            }

            if (null != _completedBackgroundQuery && !_completedBackgroundQuery.IsWorkspace(Workspace))
            {
                _workspaceOpenTime = null;
                _completedBackgroundQuery.Dispose();
                _completedBackgroundQuery = null;
            }
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
            if (null == Workspace || !Workspace.IsLoaded)
            {
                if (null == Workspace)
                {
                    lblResultsStatus.Text = lblChromatogramStatus.Text = "No workspace is open";
                }
                else
                {
                    lblResultsStatus.Text = lblChromatogramStatus.Text = "Waiting for workspace to load";
                }
                SetProgressBarValue(progressBarChromatograms, 0);
                SetProgressBarValue(progressBarResults, 0);
            }
            if (0 == _totalChromatograms)
            {
                lblChromatogramStatus.Text =
                    "No chromatograms need to be calculated in this workspace.  Choose some peptides to analyze.";
                SetProgressBarValue(progressBarChromatograms, 0);
            }
            else
            {
                lblChromatogramStatus.Text = string.Format("{0} of {1} chromatograms generated.",
                                                            _completedChromatograms,
                                                            _totalChromatograms);
                SetProgressBarValue(progressBarChromatograms, 100 * _completedChromatograms /
                                                    _totalChromatograms);
            }
            if (0 == _totalResults || 0 == _totalChromatograms)
            {
                lblResultsStatus.Text =
                    "No results need to be calculated in this workspace.  Choose some peptides to analyze.";
                SetProgressBarValue(progressBarResults, 0);
            }
            else
            {
                lblResultsStatus.Text = string.Format("{0} of {1} results calculated.",
                                                      _completedResults,
                                                      _totalResults);
                SetProgressBarValue(progressBarResults, 100 * _completedResults /
                                           _totalResults);
            }

            if (null != _completedBackgroundQuery)
            {
                if (_workspaceOpenTime == null)
                {
                    _firstSeenLockTime.Clear();
                }
                foreach (var lockId in _completedBackgroundQuery.ForeignLockIds)
                {
                    if (!_firstSeenLockTime.ContainsKey(lockId))
                    {
                        _firstSeenLockTime.Add(lockId, DateTime.Now);
                    }
                }
                foreach (var lockId in _firstSeenLockTime.Keys.ToArray().Except(_completedBackgroundQuery.ForeignLockIds))
                {
                    _firstSeenLockTime.Remove(lockId);
                }
                if (_workspaceOpenTime == null)
                {
                    _workspaceOpenTime = DateTime.Now;
                }
                DateTime staleTime = new DateTime(Math.Min(DateTime.Now.AddMinutes(-ExistingForeignLockStaleTimeMinutes).Ticks, 
                    Math.Max(_workspaceOpenTime.Value.Ticks, DateTime.Now.AddMinutes(-NewForeignLockStaleTimeMinutes).Ticks)));

                int staleForeignLockCount = _firstSeenLockTime.Values.Count(time => staleTime.CompareTo(time) > 0);
                if (0 == staleForeignLockCount)
                {
                    panelForeignLocks.Visible = false;
                }
                else
                {
                    lblForeignLocks.Text =
                        string.Format(
                            "The database says that there are {0} tasks being performed by other instances of Topograph",
                            staleForeignLockCount);
                    panelForeignLocks.Visible = true;
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
            private long? _lastChangeId;
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
                _lastChangeId = workspace.Data.LastChangeLogId;
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
                    var queryForeignLock = _session.CreateQuery("SELECT L.Id FROM " + typeof (DbLock) + " L WHERE L.InstanceIdBytes <> :instanceIdBytes")
                        .SetParameter("instanceIdBytes", workspace.InstanceId.ToByteArray());
                    ForeignLockIds = new HashSet<long>(queryForeignLock.List<long>());
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
                var session = _session;
                _session = null;
                if (session != null)
                {
                    session.Dispose();
                }
                base.Dispose();
            }

            public bool IsRunning
            {
                get; private set;
            }

            public HashSet<long> ForeignLockIds { get; private set; }
            public bool IsStale()
            {
                return !IsRunning && !IsWorkspace(_waitForResultsStep.Workspace)
                       || _waitForResultsStep.Workspace != null && _lastChangeId != _waitForResultsStep.Workspace.Data.LastChangeLogId
                       || ForeignLockIds.Count > 0;
            }
        }

        private void TimerOnTick(object sender, EventArgs e)
        {
            UpdateStepStatus();
            if (Workspace != null && _runningBackgroundQuery == null)
            {
                if (_completedBackgroundQuery == null || _completedBackgroundQuery.IsStale())
                {
                    _runningBackgroundQuery = new BackgroundQuery(this);
                    _runningBackgroundQuery.RunQuery();
                }
            }
        }

        private void BtnCancelTasksOnClick(object sender, EventArgs e)
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
