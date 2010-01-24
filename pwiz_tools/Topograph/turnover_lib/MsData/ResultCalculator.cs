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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Data.Snapshot;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.MsData
{
    public class ResultCalculator
    {
        private readonly Workspace _workspace;
        private readonly EventWaitHandle _eventWaitHandle = new EventWaitHandle(true, EventResetMode.ManualReset);
        private Thread _resultCalculatorThread;
        private bool _isRunning;
        private readonly PendingIdQueue _pendingIdQueue = new PendingIdQueue();
        private ISession _session;

        public ResultCalculator(Workspace workspace)
        {
            _workspace = workspace;
            _workspace.EntitiesChange += _workspace_EntitiesChangedEvent;
            _workspace.WorkspaceDirty += _workspace_WorkspaceSave;
            StatusMessage = "Not running";
        }

        void _workspace_WorkspaceSave(Workspace workspace)
        {
            _eventWaitHandle.Set();
        }

        void _workspace_EntitiesChangedEvent(EntitiesChangedEventArgs obj)
        {
            _eventWaitHandle.Set();
        }

        public void Start()
        {
            lock(this)
            {
                if (_isRunning)
                {
                    return;
                }
                _isRunning = true;
                if (_resultCalculatorThread == null)
                {
                    _resultCalculatorThread = new Thread(WorkerMethod) { Name = "Result Calculator", Priority = ThreadPriority.BelowNormal};
                    _resultCalculatorThread.Start();
                }
                _eventWaitHandle.Set();
            }
        }
        private ICollection<long> ListLockedAnalyses()
        {
            using (_session = _workspace.OpenSession())
            {
                var list = new List<long>();
                _session.CreateQuery("SELECT T.PeptideAnalysisId FROM " + typeof (DbLock) +
                                    " T WHERE T.PeptideAnalysisId IS NOT NULL").List(list);
                return new HashSet<long>(list);
            }
        }
        private ResultCalculatorTask GetNextPeptideAnalysis()
        {
            if (!_workspace.IsLoaded)
            {
                StatusMessage = "Waiting for workspace to load";
                return null;
            }
            StatusMessage = "Looking for next peptide";
            var openPeptideAnalysisIds = new HashSet<long>();
            foreach (var peptideAnalysis in _workspace.PeptideAnalyses.ListOpenPeptideAnalyses())
            {
                openPeptideAnalysisIds.Add(peptideAnalysis.Id.Value);
                if (peptideAnalysis.FileAnalyses.GetChildCount() == 0)
                {
                    continue;
                }
                foreach (var peptideFileAnalysis in peptideAnalysis.FileAnalyses.ListChildren())
                {
                    if (peptideFileAnalysis.ValidationStatus == ValidationStatus.reject)
                    {
                        continue;
                    }
                    if (peptideFileAnalysis.Chromatograms.GetChildCount() == 0)
                    {
                        continue;
                    }
                    if (peptideFileAnalysis.PeptideDistributions.GetChildCount() != 0)
                    {
                        continue;
                    }
                    return new ResultCalculatorTask(peptideAnalysis, null);
                }
            }
            if (!_workspace.SavedWorkspaceVersion.Equals(_workspace.WorkspaceVersion))
            {
                return null;
            }
            var lockedIds = ListLockedAnalyses();
            long? peptideAnalysisId = null;
            using (_session = _workspace.OpenSession())
            {
                while (peptideAnalysisId == null && (_pendingIdQueue.PendingIdCount() > 0 || _pendingIdQueue.IsRequeryPending()))
                {
                    foreach (var fileAnalysisId in _pendingIdQueue.EnumerateIds())
                    {
                        var peptideFileAnalysis =
                            _session.Get<DbPeptideFileAnalysis>(fileAnalysisId);
                        if (peptideFileAnalysis == null)
                        {
                            continue;
                        }
                        if (peptideFileAnalysis.ChromatogramCount == 0 || peptideFileAnalysis.PeptideDistributionCount != 0)
                        {
                            continue;
                        }
                        var id = peptideFileAnalysis.PeptideAnalysis.Id.Value;
                        if (openPeptideAnalysisIds.Contains(id))
                        {
                            continue;
                        }
                        if (lockedIds.Contains(id))
                        {
                            continue;
                        }
                        peptideAnalysisId = id;
                        break;
                    }
                    if (_pendingIdQueue.IsRequeryPending())
                    {
                        var list = new List<long>();
                        var query = _session.CreateQuery("SELECT F.Id FROM " + typeof(DbPeptideFileAnalysis) + " F"
                            + "\nWHERE F.ChromatogramCount <> 0 AND F.PeptideDistributionCount = 0");
                        query.List(list);
                        _pendingIdQueue.SetQueriedIds(list);
                    }
                }
            }
            if (peptideAnalysisId != null)
            {
                using (_workspace.GetReadLock())
                {
                    var peptideAnalysis = _workspace.PeptideAnalyses.GetChild(peptideAnalysisId.Value);
                    if (peptideAnalysis == null)
                    {
                        var snapshot = PeptideAnalysisSnapshot.LoadSnapshot(_workspace, peptideAnalysisId.Value, false);
                        if (snapshot == null)
                        {
                            return null;
                        }
                        peptideAnalysis = new PeptideAnalysis(_workspace, snapshot);
                        DbLock dbLock;
                        using (_session = _workspace.OpenWriteSession())
                        {
                            _session.BeginTransaction();
                            dbLock = new DbLock()
                                         {
                                             InstanceIdGuid = _workspace.InstanceId,
                                             LockType = LockType.results,
                                             PeptideAnalysisId = peptideAnalysisId
                                         };
                            _session.Save(dbLock);
                            _session.Transaction.Commit();
                        }
                        return new ResultCalculatorTask(peptideAnalysis, dbLock);
                    }
                }
            }
            return null;
        }
        
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }
            _isRunning = false;
            var session = _session;
            try
            {
                if (session != null)
                {
                    session.CancelQuery();
                }
            }
            catch
            {
                // ignore
            }
            _eventWaitHandle.Set();
        }

        public bool IsRunning()
        {
            lock(this)
            {
                return _isRunning;
            }
        }

        void WorkerMethod()
        {
            while (true)
            {
                lock(this)
                {
                    if (!_isRunning)
                    {
                        break;
                    }
                }

                try
                {
                    _eventWaitHandle.Reset();
                    using (var task = GetNextPeptideAnalysis())
                    {
                        if (task == null)
                        {
                            StatusMessage = "Idle";
                            _eventWaitHandle.WaitOne();
                            continue;
                        }
                        _eventWaitHandle.Set();
                        CalculateAnalysisResults(task);
                    }
                }
                catch (Exception exception)
                {
                    _eventWaitHandle.Reset();
                    ErrorHandler.LogException("Result Calculator", "Exception", exception);
                }
            }
        }
        void CalculateAnalysisResults(ResultCalculatorTask task)
        {
            StatusMessage = "Processing " + task.PeptideAnalysis.Peptide.FullSequence;
            var peaksList = new List<Peaks>();
            var peptideDistributionsList = new List<PeptideDistributions>();
            var filteredPeptideDistributions = new List<PeptideDistributions>();
            using (_workspace.GetReadLock())
            {
                foreach (var peptideFileAnalysis in task.PeptideAnalysis.FileAnalyses.ListChildren())
                {
                    if (peptideFileAnalysis.Peaks.ChildCount == 0 && 
                        (peptideFileAnalysis.Chromatograms.ChildCount == 0
                        || !peptideFileAnalysis.IsMzKeySetComplete(peptideFileAnalysis.Chromatograms.GetKeys())))
                    {
                        continue;
                    }
                    var peaks = peptideFileAnalysis.Peaks;
                    PeptideDistributions peptideDistributions;
                    if (peaks.ChildCount == 0 && peptideFileAnalysis.PeptideDistributions.GetChildCount() == 0)
                    {
                        peaks = new Peaks(peptideFileAnalysis);
                        peaks.CalcIntensities();
                    }
                    if (peptideFileAnalysis.PeptideDistributions.GetChildCount() == 0) 
                    {
                        peptideDistributions = new PeptideDistributions(peptideFileAnalysis);
                        peptideDistributions.Calculate(peaks);
                    }
                    else
                    {
                        peptideDistributions = peptideFileAnalysis.PeptideDistributions;
                    }
                    peaksList.Add(peaks);
                    peptideDistributionsList.Add(peptideDistributions);
                    if (peptideFileAnalysis.ValidationStatus != ValidationStatus.reject)
                    {
                        filteredPeptideDistributions.Add(peptideDistributions);
                    }
                }
            }
            using (_workspace.GetWriteLock())
            {
                for (int i = 0; i < peaksList.Count(); i++)
                {
                    var peaks = peaksList[i];
                    var peptideDistributions = peptideDistributionsList[i];
                    peaks.PeptideFileAnalysis.SetDistributions(peaks, peptideDistributions);
                }
            }
            if (task.CanSave())
            {
                using (_session = _workspace.OpenWriteSession())
                {
                    _session.BeginTransaction();
                    foreach (var peptideFileAnalysis in task.PeptideAnalysis.GetFileAnalyses(false))
                    {
                        if (peptideFileAnalysis.Peaks.ChildCount == 0 && 
                            (peptideFileAnalysis.Chromatograms.ChildCount > 0 && 
                            !peptideFileAnalysis.IsMzKeySetComplete(peptideFileAnalysis.Chromatograms.GetKeys())))
                        {
                            var dbPeptideFileAnalysis = _session.Get<DbPeptideFileAnalysis>(peptideFileAnalysis.Id);
                            dbPeptideFileAnalysis.ChromatogramCount = 0;
                            _session.Update(dbPeptideFileAnalysis);
                            foreach (var dbChromatogram in dbPeptideFileAnalysis.Chromatograms)
                            {
                                _session.Delete(dbChromatogram);
                            }
                        }
                        peptideFileAnalysis.Peaks.Save(_session);
                        peptideFileAnalysis.PeptideDistributions.Save(_session);
                    }
                    _session.Save(new DbChangeLog(task.PeptideAnalysis));
                    task.FinishLock(_session);
                    _session.Transaction.Commit();
                }
            }
        }
        public String StatusMessage { get; private set; }

        public int PendingAnalysisCount { get
        {
            return _pendingIdQueue.PendingIdCount();
        }}

        public void AddPeptideFileAnalysisId(long id)
        {
            _pendingIdQueue.AddId(id);
            _eventWaitHandle.Set();
        }
        public void AddPeptideFileAnalysisIds(ICollection<long> ids)
        {
            if (ids.Count == 0)
            {
                return;
            }
            _pendingIdQueue.AddIds(ids);
            _eventWaitHandle.Set();
        }

        public void SetRequeryPending()
        {
            _pendingIdQueue.SetRequeryPending();
        }

        public bool IsRequeryPending()
        {
            return _pendingIdQueue.IsRequeryPending();
        }

        class ResultCalculatorTask : Task
        {
            public ResultCalculatorTask(PeptideAnalysis peptideAnalysis, DbLock dbLock) : base(peptideAnalysis.Workspace, dbLock)
            {
                PeptideAnalysis = peptideAnalysis;
            }
            public PeptideAnalysis PeptideAnalysis { get; set; }
        }
    }
}
