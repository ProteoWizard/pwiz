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
using System.Threading;
using NHibernate;
using NHibernate.Criterion;
using pwiz.ProteowizardWrapper;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.MsData
{
    public class ChromatogramGenerator : IDisposable
    {
        private int _maxConcurrentAnalyses;
        private readonly Workspace _workspace;
        private Thread _chromatogramGeneratorThread;
        private readonly EventWaitHandle _eventWaitHandle = new EventWaitHandle(true, EventResetMode.ManualReset);
        private bool _isRunning;
        private bool _isSuspended;
        private int _progress;
        private String _statusMessage;
        private ISession _session;
        private int _missingChromatograms;

        public ChromatogramGenerator(Workspace workspace)
        {
            _workspace = workspace;
            _workspace.Change += WorkspaceOnChange;
            if (Environment.Is64BitProcess)
            {
                _maxConcurrentAnalyses = 1000;
            }
            else
            {
                _maxConcurrentAnalyses = 100;
            }
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
                if (_chromatogramGeneratorThread == null)
                {
                    _chromatogramGeneratorThread = new Thread(GenerateSavedChromatograms)
                                                      {
                                                          Name = "Chromatogram Generator",
                                                          Priority = ThreadPriority.BelowNormal
                                                      };
                    _chromatogramGeneratorThread.Start();
                }
                _eventWaitHandle.Set();
            }
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
                    session.Close();
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
            return _isRunning;
        }

        public void GetProgress(out String statusMessage, out int progress)
        {
            statusMessage = _statusMessage;
            progress = _progress;
        }

        private bool IsLowOnMemory()
        {
            try
            {
                var bytes = new byte[20000000];
            }
            catch (OutOfMemoryException outOfMemoryException)
            {
                return true;
            }
            return false;
        }

        private ChromatogramTask GetTaskList()
        {
            long? msDataFileId = null;
            var analysisChromatograms = new List<AnalysisChromatograms>();
            bool openAnalyses = false;
            string dataFilePath = null;
            
            if (string.IsNullOrEmpty(_workspace.GetDataDirectory()))
            {
                return null;
            }
            var workspaceCopy = _workspace.Clone();
            foreach (var peptideAnalysis in workspaceCopy.PeptideAnalyses)
            {
                if (0 == peptideAnalysis.GetChromatogramRefCount() || !peptideAnalysis.ChromatogramsWereLoaded)
                {
                    continue;
                }
                foreach (var peptideFileAnalysis in peptideAnalysis.FileAnalyses)
                {
                    if (_workspace.IsRejected(peptideFileAnalysis.MsDataFile))
                    {
                        continue;
                    }
                    if (!peptideFileAnalysis.PeptideAnalysis.ChromatogramsWereLoaded)
                    {
                        continue;
                    }
                    if (peptideFileAnalysis.ChromatogramSet == null || !peptideFileAnalysis.IsMzKeySetComplete(peptideFileAnalysis.ChromatogramSet.Chromatograms.Keys))
                    {
                        if (msDataFileId == null)
                        {
                            dataFilePath = _workspace.GetDataFilePath(peptideFileAnalysis.MsDataFile.Name);
                            if (dataFilePath == null)
                            {
                                continue;
                            }
                            msDataFileId = peptideFileAnalysis.MsDataFile.Id;
                            openAnalyses = true;
                        }
                        else
                        {
                            if (!Equals(msDataFileId, peptideFileAnalysis.MsDataFile.Id))
                            {
                                continue;
                            }
                        }
                        analysisChromatograms.Add(new AnalysisChromatograms(peptideFileAnalysis));
                    }
                }
            }
            int missingChromatograms = 0;
            if (!openAnalyses)
            {
                if (!_workspace.SavedWorkspaceChange.HasChromatogramMassChange)
                {
                    ICollection<long> lockedMsDataFileIds;
                    using (_session = _workspace.OpenSession())
                    {
                        lockedMsDataFileIds = ListLockedMsDataFileIds(_session);
                    }
                    foreach (var peptideAnalysis in workspaceCopy.PeptideAnalyses)
                    {
                        foreach (var fileAnalysis in peptideAnalysis.FileAnalyses)
                        {
                            if (fileAnalysis.ChromatogramSetId != null)
                            {
                                continue;
                            }
                            missingChromatograms++;
                            if (lockedMsDataFileIds.Contains(fileAnalysis.MsDataFile.Id))
                            {
                                continue;
                            }
                            if (_workspace.IsRejected(fileAnalysis.MsDataFile))
                            {
                                continue;
                            }
                            if (null == msDataFileId)
                            {
                                msDataFileId = fileAnalysis.MsDataFile.Id;
                            }
                            else
                            {
                                if (msDataFileId != fileAnalysis.MsDataFile.Id)
                                {
                                    continue;
                                }
                            }
                            analysisChromatograms.Add(new AnalysisChromatograms(fileAnalysis));
                        }
                    }
                }
            }
            _missingChromatograms = missingChromatograms;
            if (!msDataFileId.HasValue)
            {
                return null;
            }
            DbLock dbLock = null;
            var msDataFile = _workspace.MsDataFiles.FindByKey(msDataFileId.Value);
            if (!openAnalyses)
            {
                dbLock = new DbLock
                             {
                                 InstanceIdGuid = _workspace.InstanceId,
                                 MsDataFileId = msDataFileId,
                                 LockType = LockType.chromatograms
                             };
            }
            return new ChromatogramTask(workspaceCopy, dbLock)
                       {
                           AnalysisChromatograms = analysisChromatograms,
                           MsDataFile = msDataFile,
                           DataFilePath = dataFilePath,
                       };
        }

        private void GenerateSavedChromatograms()
        {
            while (true)
            {
                lock (this)
                {
                    if (!_isRunning)
                    {
                        break;
                    }
                    if (_isSuspended)
                    {
                        _statusMessage = "Suspended";
                        _eventWaitHandle.Reset();
                    }
                    if (!_workspace.IsLoaded)
                    {
                        _statusMessage = "Waiting for workspace to load";
                        _eventWaitHandle.Reset();
                    }
                }
                if (_isSuspended || !_workspace.IsLoaded)
                {
                    _eventWaitHandle.WaitOne();
                    continue;
                }
                ChromatogramTask chromatogramTask;
                try
                {
                    _eventWaitHandle.Reset();
                    chromatogramTask = GetTaskList();
                    if (chromatogramTask != null)
                    {
                        _eventWaitHandle.Set();
                    }
                }
                catch(Exception exception)
                {
                    _eventWaitHandle.Set();
                    ErrorHandler.LogException("Chromatogram generator", "Error finding next task", exception);
                    _eventWaitHandle.WaitOne();
                    continue;
                }
                
                if (chromatogramTask != null)
                {
                    if (chromatogramTask.AnalysisChromatograms.Count == 1)
                    {
                        _statusMessage = "Generating chromatograms for " +
                                         chromatogramTask.AnalysisChromatograms[0].Sequence + " in " +
                                         chromatogramTask.MsDataFile.Label;
                    }
                    else
                    {
                        _statusMessage = "Generating chromatograms for " + chromatogramTask.AnalysisChromatograms.Count +
                                         " peptides in " +
                                         chromatogramTask.MsDataFile.Label;
                    }
                }
                else if (_isSuspended)
                {
                    _statusMessage = "Suspended";
                }
                else if (!_workspace.IsLoaded)
                {
                    _statusMessage = "Waiting for workspace to load";
                }
                else
                {
                    _statusMessage = "Idle";
                }
                _progress = 0;
                if (chromatogramTask == null)
                {
                    _eventWaitHandle.WaitOne();
                    continue;
                }
                using (chromatogramTask)
                {
                    foreach (var analysis in chromatogramTask.AnalysisChromatograms)
                    {
                        if (!_isRunning)
                        {
                            return;
                        }
                        analysis.Init(_workspace);
                    }
                    try
                    {
                        chromatogramTask.BeginLock();
                        GenerateChromatograms(chromatogramTask);
                    }
                    catch (Exception exception)
                    {
                        if (_workspace.SessionFactory != null)
                        {
                            if (exception is OutOfMemoryException && _maxConcurrentAnalyses > 1)
                            {
                                ErrorHandler.LogException("Chromatogram Generator", string.Format("Ran out of memory trying to generate {0} chromatograms at once, reducing that number by half.", _maxConcurrentAnalyses), exception);
                                _maxConcurrentAnalyses /= 2;
                                _maxConcurrentAnalyses = Math.Max(_maxConcurrentAnalyses, 1);
                            }
                            else
                            {
                                ErrorHandler.LogException("Chromatogram Generator", "Exception generating chromatograms", exception);
                            }
                        }
                    }
                }
            }
        }

        private ICollection<long> ListLockedMsDataFileIds(ISession session)
        {
            var lockedFiles = new List<long>();
            session.CreateQuery("SELECT T.MsDataFileId FROM " + typeof(DbLock) +
                                " T WHERE T.MsDataFileId IS NOT NULL").List(lockedFiles);
            return new HashSet<long>(lockedFiles);
        }

        private bool UpdateProgress(ChromatogramTask chromatogramTask, int progress)
        {
            // TODO(nicksh)
//            if (chromatogramTask.WorkspaceChange.MassVersion != _workspace.WorkspaceChange.MassVersion)
//            {
//                return false;
//            }
            lock(this)
            {
                _progress = progress;
                return _isRunning && !_isSuspended;
            }
        }

        private void GenerateChromatograms(ChromatogramTask chromatogramTask)
        {
            int totalAnalyses = chromatogramTask.AnalysisChromatograms.Count;
            if (totalAnalyses == 0)
            {
                return;
            }
            if (!UpdateProgress(chromatogramTask, 0))
            {
                return;
            }
            var msDataFile = chromatogramTask.MsDataFile;
            MsDataFileUtil.InitMsDataFile(chromatogramTask.Workspace, msDataFile);
            var analyses = new List<AnalysisChromatograms>(chromatogramTask.AnalysisChromatograms);
            MsDataFileImpl pwizMsDataFileImpl;
            var path = _workspace.GetDataFilePath(msDataFile.Name);
            try
            {
                pwizMsDataFileImpl = new MsDataFileImpl(path);
            }
            catch (Exception exception)
            {
                ErrorHandler.LogException("Chromatogram Generator", "Error opening " + path, exception);
                _workspace.RejectMsDataFile(msDataFile);
                return;
            }
            using (pwizMsDataFileImpl)
            {
                var completeAnalyses = new List<AnalysisChromatograms>();
                int totalScanCount = pwizMsDataFileImpl.SpectrumCount;
                var lastTimeInDataFile =
                    chromatogramTask.MsDataFile.GetTime(chromatogramTask.MsDataFile.GetSpectrumCount() - 1);
                double minTime = lastTimeInDataFile;
                double maxTime = msDataFile.GetTime(0);
                foreach (var analysis in analyses)
                {
                    minTime = Math.Min(minTime, analysis.FirstTime);
                    maxTime = Math.Max(maxTime, analysis.LastTime);
                }
                int firstScan = msDataFile.FindScanIndex(minTime);
                for (int iScan = firstScan; analyses.Count > 0 && iScan < totalScanCount; iScan++)
                {
                    double time = msDataFile.GetTime(iScan);
                    int progress = (int)(100 * (time - minTime) / (maxTime - minTime));
                    progress = Math.Min(progress, 100);
                    progress = Math.Max(progress, 0);
                    if (!UpdateProgress(chromatogramTask, progress))
                    {
                        return;
                    }
                    List<AnalysisChromatograms> activeAnalyses = new List<AnalysisChromatograms>();
                    double nextTime = Double.MaxValue;
                    if (msDataFile.GetMsLevel(iScan, pwizMsDataFileImpl) != 1)
                    {
                        continue;
                    }
                    foreach (var analysis in analyses)
                    {
                        nextTime = Math.Min(nextTime, analysis.FirstTime);
                        if (analysis.FirstTime <= time)
                        {
                            activeAnalyses.Add(analysis);
                        }
                    }
                    if (activeAnalyses.Count == 0)
                    {
                        int nextScan = msDataFile.FindScanIndex(nextTime);
                        iScan = Math.Max(iScan, nextScan - 1);
                        continue;
                    }
                    bool lowMemory = IsLowOnMemory();
                    // If we have exceeded the number of analyses we should be working on at once,
                    // throw out any that we haven't started.
                    for (int iAnalysis = activeAnalyses.Count - 1; iAnalysis >= 0 
                        && (activeAnalyses.Count > _maxConcurrentAnalyses || lowMemory); iAnalysis--)
                    {
                        var analysis = activeAnalyses[iAnalysis];
                        if (analysis.Times.Count > 0)
                        {
                            continue;
                        }
                        activeAnalyses.RemoveAt(iAnalysis);
                        analyses.Remove(analysis);
                    }
                    double[] mzArray, intensityArray;
                    pwizMsDataFileImpl.GetSpectrum(iScan, out mzArray, out intensityArray);
                    if (!pwizMsDataFileImpl.IsCentroided(iScan))
                    {
                        var centroider = new Centroider(mzArray, intensityArray);
                        centroider.GetCentroidedData(out mzArray, out intensityArray);
                    }
                    foreach (var analysis in activeAnalyses)
                    {
                        var points = new List<ChromatogramPoint>();
                        foreach (var chromatogram in analysis.Chromatograms)
                        {
                            points.Add(MsDataFileUtil.GetPoint(chromatogram.MzRange, mzArray, intensityArray));
                        }
                        analysis.AddPoints(iScan, time, points);
                    }
                    var incompleteAnalyses = new List<AnalysisChromatograms>();
                    foreach (var analysis in analyses)
                    {
                        if (analysis.LastTime <= time)
                        {
                            completeAnalyses.Add(analysis);
                        }
                        else
                        {
                            incompleteAnalyses.Add(analysis);
                        }
                    }
                    SaveChromatograms(chromatogramTask, completeAnalyses, false);
                    completeAnalyses.Clear();
                    analyses = incompleteAnalyses;
                }
                completeAnalyses.AddRange(analyses);
                SaveChromatograms(chromatogramTask, completeAnalyses, true);
            }
        }

        private void SaveChromatograms(ChromatogramTask chromatogramTask, ICollection<AnalysisChromatograms> analyses, bool finished)
        {
            if (analyses.Count == 0)
            {
                return;
            }
            if (!IsRunning())
            {
                return;
            }
            _workspace.RunOnEventQueue(() =>
                                    {
                                        if (
                                            chromatogramTask.Workspace.CompareSettings(_workspace)
                                                            .HasChromatogramMassChange)
                                        {
                                            return;
                                        }
                                        foreach (var analysisChromatograms in analyses)
                                        {
                                            PeptideAnalysis peptideAnalysis;
                                            _workspace.PeptideAnalyses.TryGetValue(
                                                analysisChromatograms.PeptideAnalysisId, out peptideAnalysis);
                                            if (peptideAnalysis == null ||
                                                0 == peptideAnalysis.GetChromatogramRefCount())
                                            {
                                                continue;
                                            }
                                            var peptideFileAnalysis =
                                                peptideAnalysis.GetFileAnalysis(
                                                    analysisChromatograms.PeptideFileAnalysisId);
                                            if (peptideFileAnalysis == null)
                                            {
                                                continue;
                                            }
                                            peptideFileAnalysis.SetChromatograms(analysisChromatograms);
                                        }
                                    });
            if (!chromatogramTask.CanSave())
            {
                return;
            }
            using (_session = _workspace.OpenWriteSession())
            {
                // TODO(nicksh)
//                if (chromatogramTask.WorkspaceChange.MassVersion != _workspace.SavedWorkspaceChange.MassVersion)
//                {
//                    return;
//                }
                _session.BeginTransaction();
                foreach (AnalysisChromatograms analysis in analyses)
                {
                    if (!IsRunning())
                    {
                        return;
                    }
                    var dbPeptideFileAnalysis = _session.Get<DbPeptideFileAnalysis>(analysis.PeptideFileAnalysisId);
                    if (dbPeptideFileAnalysis == null)
                    {
                        continue;
                    }
                    if (analysis.MinCharge != dbPeptideFileAnalysis.PeptideAnalysis.MinCharge
                        || analysis.MaxCharge != dbPeptideFileAnalysis.PeptideAnalysis.MaxCharge)
                    {
                        continue;
                    }
                    foreach (DbChromatogramSet dbChromatogramSet in _session.CreateCriteria(typeof(DbChromatogramSet))
                        .Add(Restrictions.Eq("PeptideFileAnalysis", dbPeptideFileAnalysis)).List())
                    {
                        foreach (DbChromatogram dbChromatogram in _session.CreateCriteria(typeof(DbChromatogram))
                            .Add(Restrictions.Eq("ChromatogramSet", dbChromatogramSet)).List())
                        {
                            _session.Delete(dbChromatogram);
                        }
                        _session.Delete(dbChromatogramSet);
                    }
                    dbPeptideFileAnalysis.ChromatogramSet = null;
                    _session.Update(dbPeptideFileAnalysis);
                    _session.Flush();
                    dbPeptideFileAnalysis.ChromatogramSet = new DbChromatogramSet
                                          {
                                              PeptideFileAnalysis = dbPeptideFileAnalysis,
                                              Times = analysis.Times.ToArray(),
                                              ScanIndexes = analysis.ScanIndexes.ToArray(),
                                              ChromatogramCount = analysis.Chromatograms.Count,
                                          };
                    bool noPoints = false;
                    if (dbPeptideFileAnalysis.ChromatogramSet.Times.Length == 0)
                    {
                        noPoints = true;
                        dbPeptideFileAnalysis.ChromatogramSet.Times = new double[]{0};
                        dbPeptideFileAnalysis.ChromatogramSet.ScanIndexes = new int[]{0};
                    }
                    _session.Save(dbPeptideFileAnalysis.ChromatogramSet);
                    foreach (Chromatogram chromatogram in analysis.Chromatograms)
                    {
                        DbChromatogram dbChromatogram = new DbChromatogram
                                                 {
                                                     ChromatogramSet = dbPeptideFileAnalysis.ChromatogramSet,
                                                     MzKey = chromatogram.MzKey,
                                                     ChromatogramPoints = chromatogram.Points,
                                                     MzRange = chromatogram.MzRange,
                                                 };
                        if (noPoints)
                        {
                            dbChromatogram.ChromatogramPoints = new []{new ChromatogramPoint(), };
                        }
                        _session.Save(dbChromatogram);
                    }
                    _session.Update(dbPeptideFileAnalysis);
                    _session.Save(new DbChangeLog(_workspace, dbPeptideFileAnalysis.PeptideAnalysis));
                }
                if (chromatogramTask.MsDataFile.IsBinaryDirty)
                {
                    chromatogramTask.MsDataFile.SaveBinary(_session);
                }
                chromatogramTask.UpdateLock(_session);
                _session.Transaction.Commit();
            }
        }

        public void WorkspaceOnChange(object sender, WorkspaceChangeArgs args)
        {
            _eventWaitHandle.Set();
        }

        public class Chromatogram
        {
            public Chromatogram (MzKey mzKey, MzRange mzRange)
            {
                MzKey = mzKey;
                MzRange = mzRange;
                Points = new List<ChromatogramPoint>();
            }
            public MzKey MzKey { get; private set; }
            public MzRange MzRange { get; private set; }
            public List<ChromatogramPoint> Points { get; private set; }
        }
        public int PendingAnalysisCount { get { return _missingChromatograms; } }
        public void SetRequeryPending()
        {
            _eventWaitHandle.Set();
        }
        public void AddPeptideFileAnalysisIds(IEnumerable<long> ids)
        {
            _eventWaitHandle.Set();
        }
        public bool IsRequeryPending()
        {
            return true;
        }

        public bool IsSuspended
        {
            get
            {
                return _isSuspended;
            }
            set
            {
                lock(this)
                {
                    if (_isSuspended == value)
                    {
                        return;
                    }
                    _isSuspended = value;
                    _eventWaitHandle.Set();
                }
            }
        }
        public bool IsThreadAlive
        {
            get
            {
                return _chromatogramGeneratorThread.IsAlive;
            }
        }

        private class ChromatogramTask : TaskItem
        {
            public ChromatogramTask(Workspace workspace, DbLock dbLock)
                : base(workspace, dbLock)
            {
            }

            public List<AnalysisChromatograms> AnalysisChromatograms;
            public MsDataFile MsDataFile;
            public String DataFilePath;
        }

        public void Dispose()
        {
            Stop();
            var session = _session;
            _session = null;

            if (session != null)
            {
                session.Dispose();
            }
        }
    }

    public class AnalysisChromatograms
    {
        public AnalysisChromatograms(PeptideFileAnalysis peptideFileAnalysis)
        {
            PeptideFileAnalysisId = peptideFileAnalysis.Id;
            PeptideAnalysisId = peptideFileAnalysis.PeptideAnalysis.Id;
            FirstTime = peptideFileAnalysis.ChromatogramStartTime;
            LastTime = peptideFileAnalysis.ChromatogramEndTime;
            MinCharge = peptideFileAnalysis.PeptideAnalysis.MinCharge;
            MaxCharge = peptideFileAnalysis.PeptideAnalysis.MaxCharge;
            Sequence = peptideFileAnalysis.PeptideAnalysis.Peptide.Sequence;
        }

        public void Init(Workspace workspace)
        {
            Chromatograms = new List<ChromatogramGenerator.Chromatogram>();
            var turnoverCalculator = new TurnoverCalculator(workspace, Sequence);
            for (int charge = MinCharge; charge <= MaxCharge; charge++)
            {
                var mzs = turnoverCalculator.GetMzs(charge);
                for (int massIndex = 0; massIndex < mzs.Count; massIndex++)
                {
                    Chromatograms.Add(new ChromatogramGenerator.Chromatogram(new MzKey(charge, massIndex), mzs[massIndex]));
                }
            }
            ScanIndexes = new List<int>();
            Times = new List<double>();
        }

        public AnalysisChromatograms(DbPeptideFileAnalysis dbPeptideFileAnalysis)
        {
            PeptideAnalysisId = dbPeptideFileAnalysis.PeptideAnalysis.Id.Value;
            PeptideFileAnalysisId = dbPeptideFileAnalysis.Id.Value;
            FirstTime = dbPeptideFileAnalysis.ChromatogramStartTime;
            LastTime = dbPeptideFileAnalysis.ChromatogramEndTime;
            MinCharge = dbPeptideFileAnalysis.PeptideAnalysis.MinCharge;
            MaxCharge = dbPeptideFileAnalysis.PeptideAnalysis.MaxCharge;
            Sequence = dbPeptideFileAnalysis.PeptideAnalysis.Peptide.Sequence;
        }
        public long PeptideFileAnalysisId { get; private set; }
        public long PeptideAnalysisId { get; private set; }
        public String Sequence { get; private set; }
        public double FirstTime { get; private set; }
        public double LastTime { get; private set; }
        public List<ChromatogramGenerator.Chromatogram> Chromatograms { get; private set; }
        public List<int> ScanIndexes { get; private set; }
        public List<double> Times { get; private set;}
        public int MinCharge { get; private set; }
        public int MaxCharge { get; private set; }
        public void AddPoints(int scanIndex, double time, List<ChromatogramPoint> points)
        {
            if (ScanIndexes.Count > 0)
            {
                Debug.Assert(scanIndex > ScanIndexes[ScanIndexes.Count - 1]);
                Debug.Assert(time >= Times[Times.Count - 1]);
            }
            ScanIndexes.Add(scanIndex);
            Times.Add(time);
            for (int i = 0; i < Chromatograms.Count; i ++)
            {
                var chromatogram = Chromatograms[i];
                chromatogram.Points.Add(points[i]);
            }
        }
    }
}
