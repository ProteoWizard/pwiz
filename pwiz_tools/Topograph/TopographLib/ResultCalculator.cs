using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using turnover.Data;
using turnover.Model;

namespace turnover
{
    public class ResultCalculator
    {
        private readonly Workspace _workspace;
        private readonly HashSet<PeptideAnalysis> _dirtyPeptideAnalysesSet = new HashSet<PeptideAnalysis>();
        private readonly LinkedList<PeptideAnalysis> _dirtyPeptideAnalyses = new LinkedList<PeptideAnalysis>();
        private readonly EventWaitHandle _eventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread _resultCalculatorThread;
        private bool _isRunning;

        public ResultCalculator(Workspace workspace)
        {
            _workspace = workspace;
            _workspace.EntitiesChangedEvent += _workspace_EntitiesChangedEvent;
        }

        void _workspace_EntitiesChangedEvent(EntitiesChangedEventArgs obj)
        {
            foreach (var entity in obj.GetAllEntities())
            {
                var peptideAnalysis = GetPeptideAnalysis(entity);
                if (peptideAnalysis != null)
                {
                    AddDirtyPeptideAnalysis(peptideAnalysis);
                }
            }
        }

        PeptideAnalysis GetPeptideAnalysis(EntityModel entityModel)
        {
            if (entityModel is PeptideAnalysis)
            {
                return (PeptideAnalysis) entityModel;
            }
            if (entityModel is PeptideFileAnalysis)
            {
                return ((PeptideFileAnalysis) entityModel).PeptideAnalysis;
            }
            var peptideRates = entityModel as PeptideRates;
            if (peptideRates != null)
            {
                if (peptideRates.GetChildCount() == 0)
                {
                    return peptideRates.PeptideAnalysis;
                }
            }
            return null;
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
                foreach (var peptideAnalysis in _workspace.PeptideAnalyses.ListChildren())
                {
                    AddDirtyPeptideAnalysis(peptideAnalysis);
                }
                if (_resultCalculatorThread == null)
                {
                    _resultCalculatorThread = new Thread(WorkerMethod) { Name = "Result Calculator", Priority = ThreadPriority.BelowNormal};
                    _resultCalculatorThread.Start();
                }
                _eventWaitHandle.Set();
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

        public bool IsRunning()
        {
            lock(this)
            {
                return _isRunning;
            }
        }

        void WorkerMethod()
        {
            _workspace.PeptideAnalyses.LoadPeptideRates();
            _workspace.PeptideAnalyses.LoadPeptideFileAnalyses();
            var fileAnalysesWithoutChromatograms = new List<PeptideFileAnalysis>();
            foreach (var peptideAnalysis in _workspace.PeptideAnalyses.ListChildren())
            {
                AddDirtyPeptideAnalysis(peptideAnalysis);
                foreach (var peptideFileAnalysis in peptideAnalysis.FileAnalyses.ListChildren())
                {
                    if (!peptideFileAnalysis.HasChromatograms)
                    {
                        fileAnalysesWithoutChromatograms.Add(peptideFileAnalysis);
                    }
                }
            }
            _workspace.GenerateChromatograms(fileAnalysesWithoutChromatograms);
            while (true)
            {
                PeptideAnalysis peptideAnalysis = null;
                lock(this)
                {
                    if (!_isRunning)
                    {
                        break;
                    }
                    if (_dirtyPeptideAnalyses.Count > 0)
                    {
                        peptideAnalysis = _dirtyPeptideAnalyses.First.Value;
                        _dirtyPeptideAnalyses.RemoveFirst();
                        _dirtyPeptideAnalysesSet.Remove(peptideAnalysis);
                    }
                    if (peptideAnalysis == null)
                    {
                        _eventWaitHandle.Reset();
                    }
                }
                if (peptideAnalysis == null)
                {
                    _eventWaitHandle.WaitOne();
                    continue;
                }
                CalculateAnalysisResults(peptideAnalysis);
            }
        }
        void CalculateAnalysisResults(PeptideAnalysis peptideAnalysis)
        {
            bool changed = false;
            foreach (var fileAnalysis in peptideAnalysis.FileAnalyses.ListPeptideFileAnalyses(false))
            {
                if (fileAnalysis.PeptideDistributions.ChildCount != 0)
                {
                    continue;
                }
                if (!fileAnalysis.EnsureCalculated())
                {
                    _workspace.EnsureChromatograms(fileAnalysis);
                    continue;
                }
                changed = true;
                Debug.Assert(fileAnalysis.HasChromatograms);
            }
            if (!changed)
            {
                peptideAnalysis.PeptideRates.EnsureChildrenLoaded();
            }
            if (changed || peptideAnalysis.PeptideRates.ChildCount == 0)
            {
                using (_workspace.OpenSession())
                {
                    peptideAnalysis.PeptideRates.Clear();
                    peptideAnalysis.PeptideRates.EnsureCalculated();
                }
            }
        }
        private void AddDirtyPeptideAnalysis(PeptideAnalysis peptideAnalysis)
        {
            lock(this)
            {
                if (_dirtyPeptideAnalysesSet.Contains(peptideAnalysis))
                {
                    return;
                }
                _dirtyPeptideAnalysesSet.Add(peptideAnalysis);
                _dirtyPeptideAnalyses.AddLast(peptideAnalysis);
                _eventWaitHandle.Set();
            }
        }
    }
}
