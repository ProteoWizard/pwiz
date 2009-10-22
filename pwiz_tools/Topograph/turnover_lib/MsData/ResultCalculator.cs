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
                var query = session.CreateQuery("FROM " + typeof(DbPeptideFileAnalysis) + " F"
                    + "\nWHERE F.ChromatogramCount <> 0 AND F.PeptideDistributionCount = 0");
                foreach (DbPeptideFileAnalysis dbPeptideFileAnalysis in query.Enumerable())
                {
                    var id = dbPeptideFileAnalysis.PeptideAnalysis.Id.Value;
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
            lock (_workspace.Lock)
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
            peptideAnalysis.PeptideRates.Calculate();
            _workspace.SaveIfNotDirty(peptideAnalysis);
        }
        public String StatusMessage { get; private set; }
    }
}
