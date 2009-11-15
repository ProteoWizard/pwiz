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
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.MsData
{
    public class ResultCalculator
    {
        private readonly Workspace _workspace;
        private readonly EventWaitHandle _eventWaitHandle = new EventWaitHandle(true, EventResetMode.ManualReset);
        private Thread _resultCalculatorThread;
        private bool _isRunning;
        private List<long> _fileAnalysisIds;
        private int _fileAnalysisIndex;

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
        private PeptideAnalysis GetNextPeptideAnalysis()
        {
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
                    return peptideAnalysis;
                }
            }
            if (!_workspace.SavedWorkspaceVersion.Equals(_workspace.WorkspaceVersion))
            {
                return null;
            }
            long? peptideAnalysisId = null;
            using (var session = _workspace.OpenSession())
            {
                if (_fileAnalysisIds == null || _fileAnalysisIndex >= _fileAnalysisIds.Count)
                {
                    var list = new List<long>();
                    var query = session.CreateQuery("SELECT F.Id FROM " + typeof(DbPeptideFileAnalysis) + " F"
                        + "\nWHERE F.ChromatogramCount <> 0 AND F.PeptideDistributionCount = 0");
                    query.List(list);
                    _fileAnalysisIds = list;
                    _fileAnalysisIndex = 0;
                }
                for (;_fileAnalysisIndex < _fileAnalysisIds.Count(); _fileAnalysisIndex++)
                {
                    var peptideFileAnalysis =
                        session.Get<DbPeptideFileAnalysis>(_fileAnalysisIds[_fileAnalysisIndex]);
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
                    peptideAnalysisId = id;
                    break;
                }
            }
            if (peptideAnalysisId == null)
            {
                return null;
            }
            using (_workspace.GetReadLock())
            {
                using (var session = _workspace.OpenSession())
                {
                    return _workspace.PeptideAnalyses.GetChild(peptideAnalysisId.Value, session);
                }
            }
        }
        
        public void Stop()
        {
            lock(this)
            {
                if (!_isRunning)
                {
                    return;
                }
                _isRunning = false;
                _eventWaitHandle.Set();
            }
        }

        public void Wake()
        {
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
                PeptideAnalysis peptideAnalysis;
                lock(this)
                {
                    if (!_isRunning)
                    {
                        break;
                    }
                    peptideAnalysis = GetNextPeptideAnalysis();
                    if (peptideAnalysis == null)
                    {
                        _eventWaitHandle.Reset();
                    }
                }
                if (peptideAnalysis == null)
                {
                    StatusMessage = "Idle";
                    _eventWaitHandle.WaitOne();
                    continue;
                }
                CalculateAnalysisResults(peptideAnalysis);
            }
        }
        void CalculateAnalysisResults(PeptideAnalysis peptideAnalysis)
        {
            StatusMessage = "Processing " + peptideAnalysis.Peptide.FullSequence;
            var peaksList = new List<Peaks>();
            var peptideDistributionsList = new List<PeptideDistributions>();
            var filteredPeptideDistributions = new List<PeptideDistributions>();
            bool isComplete = true;
            using (_workspace.GetReadLock())
            {
                foreach (var peptideFileAnalysis in peptideAnalysis.FileAnalyses.ListChildren())
                {
                    if (peptideFileAnalysis.Chromatograms.ChildCount == 0)
                    {
                        if (peptideFileAnalysis.ValidationStatus != ValidationStatus.reject)
                        {
                            isComplete = false;
                        }
                        continue;
                    }
                    var peaks = peptideFileAnalysis.Peaks;
                    PeptideDistributions peptideDistributions;
                    if (peaks.ChildCount == 0 || peptideFileAnalysis.PeptideDistributions.GetChildCount() == 0)
                    {
                        peaks = new Peaks(peptideFileAnalysis);
                        peaks.CalcIntensities();
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
            var peptideRates = new PeptideRates(peptideAnalysis);
            peptideRates.Calculate(filteredPeptideDistributions, isComplete);
            using (_workspace.GetWriteLock())
            {
                for (int i = 0; i < peaksList.Count(); i++)
                {
                    var peaks = peaksList[i];
                    var peptideDistributions = peptideDistributionsList[i];
                    peaks.PeptideFileAnalysis.SetDistributions(peaks, peptideDistributions);
                }
                peptideAnalysis.SetPeptideRates(peptideRates);
            }
            _workspace.SaveIfNotDirty(peptideAnalysis);
        }
        public String StatusMessage { get; private set; }
        public int PendingAnalysisCount { get
        {
            var list = _fileAnalysisIds;
            if (list != null)
            {
                return list.Count - _fileAnalysisIndex;
            }
            return 0;
        }}
    }
}
