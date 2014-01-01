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
using System.Linq;
using System.Threading;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.MsData
{
    public class ResultCalculator : IDisposable
    {
        private readonly Workspace _workspace;
        private readonly EventWaitHandle _eventWaitHandle = new EventWaitHandle(true, EventResetMode.ManualReset);
        private Thread _resultCalculatorThread;
        private bool _isRunning;
        private ISession _session;
        private int _pendingAnalysisCount;

        public ResultCalculator(Workspace workspace)
        {
            _workspace = workspace;
            _workspace.Change += WorkspaceOnChange;
            StatusMessage = "Not running";
        }

        void WorkspaceOnChange(object sender, WorkspaceChangeArgs change)
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
            StatusMessage = "Polling for database changes";
            _workspace.DatabasePoller.MergeChangesNow();
            StatusMessage = "Looking for next peptide";
            var workspaceClone = _workspace.Clone();
            var peptideAnalyses = workspaceClone.PeptideAnalyses;
            var openPeptideAnalysisIds = new HashSet<long>();
            foreach (var peptideAnalysis in peptideAnalyses.Where(pa=>0 != pa.GetChromatogramRefCount()))
            {
                openPeptideAnalysisIds.Add(peptideAnalysis.Id);
                if (peptideAnalysis.FileAnalyses.Count == 0)
                {
                    continue;
                }
                foreach (var peptideFileAnalysis in peptideAnalysis.FileAnalyses)
                {
                    if (peptideFileAnalysis.ValidationStatus == ValidationStatus.reject)
                    {
                        continue;
                    }
                    if (peptideFileAnalysis.ChromatogramSet == null)
                    {
                        continue;
                    }
                    if (peptideFileAnalysis.CalculatedPeaks != null)
                    {
                        continue;
                    }
                    return new ResultCalculatorTask(peptideAnalysis, null);
                }
            }
            if (workspaceClone.SavedWorkspaceChange.HasTurnoverChange)
            {
                return null;
            }
            _pendingAnalysisCount =
                peptideAnalyses.Count(
                    pa =>
                    0 == pa.GetChromatogramRefCount() &&
                    pa.FileAnalyses.Any(fa => !fa.PeakData.IsCalculated && fa.ChromatogramSetId.HasValue));
            var lockedIds = ListLockedAnalyses();
            foreach (var peptideAnalysis in peptideAnalyses)
            {
                if (lockedIds.Contains(peptideAnalysis.Id))
                {
                    continue;
                }
                if (peptideAnalysis.GetChromatogramRefCount() > 0)
                {
                    continue;
                }
                var uncalculatedCount = peptideAnalysis.FileAnalyses.Count(entry =>
                                                                           !entry.PeakData.IsCalculated &&
                                                                           entry.ChromatogramSetId.HasValue);
                if (uncalculatedCount > 0)
                {
                    peptideAnalysis.IncChromatogramRefCount();
                    var workspaceData = _workspace.DatabasePoller.LoadChanges(workspaceClone.Data, new Dictionary<long, bool>
                        {
                            {peptideAnalysis.Id, true}
                        });
                    workspaceClone.Merge(workspaceData);
                    if (!IsRunning())
                    {
                        return null;
                    }
                    DbLock dbLock;
                    using (_session = _workspace.OpenWriteSession())
                    {
                        try
                        {
                            _session.BeginTransaction();
                            dbLock = new DbLock
                                         {
                                             InstanceIdGuid = _workspace.InstanceId,
                                             LockType = LockType.results,
                                             PeptideAnalysisId = peptideAnalysis.Id
                                         };
                            _session.Save(dbLock);
                            _session.Transaction.Commit();
                        }
                        catch (HibernateException hibernateException)
                        {
                            throw new LockException("Could not insert lock", hibernateException);
                        }
                    }
                    return new ResultCalculatorTask(peptideAnalysis, dbLock);
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
            _eventWaitHandle.Set();
        }

        public void Dispose()
        {
            var session = _session;
            _session = null;
            if (session != null)
            {
                session.Dispose();
            }
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
                    if (_isRunning)
                    {
                        ErrorHandler.LogException("Result Calculator", "Exception", exception);
                    }
                }
            }
        }
        void CalculateAnalysisResults(ResultCalculatorTask task)
        {
            StatusMessage = "Processing " + task.PeptideAnalysis.Peptide.FullSequence;
            var peaksList = new List<CalculatedPeaks>();
            var peptideFileAnalyses = task.PeptideAnalysis.FileAnalyses.ToArray();
            Array.Sort(peptideFileAnalyses, 
                (f1, f2) => (null == f1.PsmTimes).CompareTo(null == f2.PsmTimes)
            );
            foreach (var peptideFileAnalysis in peptideFileAnalyses)
            {
                if (peptideFileAnalysis.ChromatogramSet == null
                    || !peptideFileAnalysis.IsMzKeySetComplete(peptideFileAnalysis.ChromatogramSet.Chromatograms.Keys))
                {
                    continue;
                }
                var peaks = CalculatedPeaks.Calculate(peptideFileAnalysis, peaksList);
                if (peaks.Peaks.Count != 0)
                {
                    peaksList.Add(peaks);
                }
            }
            task.PeptideAnalysis.SetCalculatedPeaks(peaksList);
            _workspace.RunOnEventQueue(() => _workspace.RetentionTimeAlignments.MergeFrom(task.Workspace.RetentionTimeAlignments));
            if (task.CanSave())
            {
                using (_session = _workspace.OpenWriteSession())
                {
                    _session.BeginTransaction();
                    foreach (var peptideFileAnalysis in task.PeptideAnalysis.GetFileAnalyses(false))
                    {
                        if (peptideFileAnalysis.ChromatogramSet != null && 
                            !peptideFileAnalysis.IsMzKeySetComplete(peptideFileAnalysis.ChromatogramSet.Chromatograms.Keys))
                        {
                            var dbPeptideFileAnalysis = _session.Get<DbPeptideFileAnalysis>(peptideFileAnalysis.Id);
                            var dbChromatogramSet = dbPeptideFileAnalysis.ChromatogramSet;
                            if (null != dbChromatogramSet)
                            {
                                foreach (var dbChromatogram in dbPeptideFileAnalysis.ChromatogramSet.Chromatograms)
                                {
                                    _session.Delete(dbChromatogram);
                                }
                                dbPeptideFileAnalysis.ChromatogramSet = null;
                                _session.Update(dbPeptideFileAnalysis);
                                _session.Delete(dbChromatogramSet);
                            }
                        }
                        if (null == peptideFileAnalysis.CalculatedPeaks)
                        {
                            var dbPeptideFileAnalysis = _session.Get<DbPeptideFileAnalysis>(peptideFileAnalysis.Id);
                            CalculatedPeaks.DeleteResults(_session, dbPeptideFileAnalysis);
                            _session.Update(dbPeptideFileAnalysis);
                        }
                        else
                        {
                            peptideFileAnalysis.CalculatedPeaks.Save(_session);
                        }
                    }
                    _session.Save(new DbChangeLog(task.PeptideAnalysis));
                    task.FinishLock(_session);
                    try
                    {
                        _session.Transaction.Commit();
                    }
// ReSharper disable RedundantCatchClause
// ReSharper disable UnusedVariable
                    catch (Exception e)
// ReSharper restore UnusedVariable
                    {
                        throw;
                    }
// ReSharper restore RedundantCatchClause
                }
            }
        }
        public String StatusMessage { get; private set; }

        public int PendingAnalysisCount { get
        {
            return _pendingAnalysisCount;
        }}

        public void AddPeptideFileAnalysisId(long id)
        {
            _eventWaitHandle.Set();
        }
        public void AddPeptideFileAnalysisIds(ICollection<long> ids)
        {
            if (ids.Count == 0)
            {
                return;
            }
            _eventWaitHandle.Set();
        }

        public void SetRequeryPending()
        {
            //_pendingIdQueue.SetRequeryPending();
        }

        public bool IsRequeryPending()
        {
            return true;
        }

        class ResultCalculatorTask : TaskItem
        {
            public ResultCalculatorTask(PeptideAnalysis peptideAnalysis, DbLock dbLock) : base(peptideAnalysis.Workspace, dbLock)
            {
                PeptideAnalysis = peptideAnalysis;
            }
            public PeptideAnalysis PeptideAnalysis { get; private set; }
        }
    }
}
