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
using pwiz.ProteowizardWrapper;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.MsData
{
    public class ChromatogramGenerator
    {
        private const int maxConcurrentAnalyses = 200;
        private readonly Workspace _workspace;
        private Thread _chromatogramGeneratorThread;
        private readonly EventWaitHandle _eventWaitHandle = new EventWaitHandle(true, EventResetMode.ManualReset);
        private bool _isRunning;
        private bool _isSuspended;
        private int _progress;
        private String _statusMessage;

        public ChromatogramGenerator(Workspace workspace)
        {
            _workspace = workspace;
            _workspace.WorkspaceDirty += WorkspaceSave;
            _workspace.EntitiesChange += WorkspaceEntitiesChanged;
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

        public void GetProgress(out String statusMessage, out int progress)
        {
            statusMessage = _statusMessage;
            progress = _progress;
        }

        private void GenerateSavedChromatograms()
        {
            while (true)
            {
                var analysisChromatograms = new List<AnalysisChromatograms>();
                MsDataFile msDataFile = null;
                long? msDataFileId = null;
                WorkspaceVersion workspaceVersion = null;
                lock (this)
                {
                    if (!_isRunning)
                    {
                        break;
                    }
                    if (_isSuspended)
                    {
                        _eventWaitHandle.Reset();
                    }
                    else
                    {
                        var initedMsDataFileIds = ListInitedMsDataFileIds();
                        using(_workspace.GetReadLock())
                        {
                            workspaceVersion = _workspace.WorkspaceVersion;
                            foreach (var peptideAnalysis in _workspace.PeptideAnalyses.ListOpenPeptideAnalyses())
                            {
                                foreach (var peptideFileAnalysis in peptideAnalysis.FileAnalyses.ListChildren())
                                {
                                    if (!initedMsDataFileIds.Contains(peptideFileAnalysis.MsDataFile.Id.Value))
                                    {
                                        continue;
                                    }
                                    if (
                                        !peptideFileAnalysis.IsMzKeySetComplete(
                                             peptideFileAnalysis.Chromatograms.GetKeys()))
                                    {
                                        if (msDataFileId == null)
                                        {
                                            msDataFileId = peptideFileAnalysis.MsDataFile.Id;
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
                        }
                        if (analysisChromatograms.Count == 0)
                        {
                            if (workspaceVersion.MassVersion == _workspace.SavedWorkspaceVersion.MassVersion)
                            {
                                using (var session = _workspace.OpenSessionWithoutLock())
                                {
                                    var criteria = session.CreateCriteria(typeof (DbPeptideFileAnalysis))
                                        .Add(Restrictions.Eq("ChromatogramCount", 0));
                                    var list = criteria.List();
                                    PendingAnalysisCount = list.Count;
                                    foreach (DbPeptideFileAnalysis dbPeptideFileAnalysis in list)
                                    {
                                        if (!initedMsDataFileIds.Contains(dbPeptideFileAnalysis.MsDataFile.Id.Value))
                                        {
                                            continue;
                                        }
                                        if (msDataFileId != null && dbPeptideFileAnalysis.MsDataFile.Id != msDataFileId)
                                        {
                                            continue;
                                        }
                                        if (msDataFileId == null)
                                        {
                                            msDataFileId = dbPeptideFileAnalysis.MsDataFile.Id;
                                        }
                                        analysisChromatograms.Add(new AnalysisChromatograms(dbPeptideFileAnalysis));
                                    }
                                }
                            }
                        }
                        if (msDataFileId.HasValue)
                        {
                            msDataFile = _workspace.MsDataFiles.GetChild(msDataFileId.Value);
                        }
                        if (_isSuspended)
                        {
                            _statusMessage = "Suspended";
                        }
                        else if (analysisChromatograms.Count == 0)
                        {
                            _eventWaitHandle.Reset();
                            _statusMessage = "Idle";
                        }
                        else if (analysisChromatograms.Count == 1)
                        {
                            _statusMessage = "Generating chromatograms for " +
                                             analysisChromatograms[0].Sequence + " in " +
                                             msDataFile.Label;
                        }
                        else
                        {
                            _statusMessage = "Generating chromatograms for " + analysisChromatograms.Count +
                                             " peptides in " +
                                             msDataFile.Label;
                        }
                        _progress = 0;
                    }
                }
                if (analysisChromatograms.Count == 0 || _isSuspended)
                {
                    _eventWaitHandle.WaitOne();
                    continue;
                }
                foreach (var analysis in analysisChromatograms)
                {
                    analysis.Init(_workspace);
                }
                GenerateChromatograms(msDataFile, analysisChromatograms, workspaceVersion);
            }
        }

        private ICollection<long> ListInitedMsDataFileIds()
        {
            var result = new HashSet<long>();
            foreach (var msDataFile in _workspace.MsDataFiles.ListChildren())
            {
                if (msDataFile.HasTimes() && msDataFile.ValidationStatus != ValidationStatus.reject)
                {
                    result.Add(msDataFile.Id.Value);
                }
            }
            return result;
        }

        private bool UpdateProgress(WorkspaceVersion workspaceVersion, int progress)
        {
            if (workspaceVersion.MassVersion != _workspace.WorkspaceVersion.MassVersion)
            {
                return false;
            }
            lock(this)
            {
                _progress = progress;
                return _isRunning && !_isSuspended;
            }
        }

        private void GenerateChromatograms(MsDataFile msDataFile, List<AnalysisChromatograms> analyses, WorkspaceVersion workspaceVersion)
        {
            int totalAnalyses = analyses.Count;
            if (totalAnalyses == 0)
            {
                return;
            }
            if (!UpdateProgress(workspaceVersion, 0))
            {
                return;
            }
            analyses = new List<AnalysisChromatograms>(analyses);
            MsDataFileImpl pwizMsDataFileImpl;
            try
            {
                pwizMsDataFileImpl = new MsDataFileImpl(msDataFile.Path);
            }
            catch (Exception)
            {
                msDataFile.ValidationStatus = ValidationStatus.reject;
                return;
            }
            using (pwizMsDataFileImpl)
            {
                var completeAnalyses = new List<AnalysisChromatograms>();
                int totalScanCount = pwizMsDataFileImpl.SpectrumCount;
                double minTime = msDataFile.GetTime(msDataFile.GetSpectrumCount() - 1);
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
                    if (!UpdateProgress(workspaceVersion, progress))
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
                    // If we have exceeded the number of analyses we should be working on at once,
                    // throw out any that we haven't started.
                    for (int iAnalysis = activeAnalyses.Count - 1; iAnalysis >= 0 && activeAnalyses.Count > maxConcurrentAnalyses; iAnalysis--)
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
                    pwizMsDataFileImpl.GetCentroidedSpectrum(iScan, out mzArray, out intensityArray);
                    foreach (var analysis in activeAnalyses)
                    {
                        var points = new List<MsDataFileUtil.ChromatogramPoint>();
                        foreach (var chromatogram in analysis.Chromatograms)
                        {
                            points.Add(MsDataFileUtil.GetIntensity(chromatogram.Mz, mzArray, intensityArray));
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
                    SaveChromatograms(workspaceVersion, completeAnalyses);
                    completeAnalyses.Clear();
                    analyses = incompleteAnalyses;
                }
                completeAnalyses.AddRange(analyses);
                SaveChromatograms(workspaceVersion, completeAnalyses);
            }
        }

        private void SaveChromatograms(WorkspaceVersion workspaceVersion, ICollection<AnalysisChromatograms> analyses)
        {
            var chromatogramsToSkip = new HashSet<AnalysisChromatograms>();
            using (ISession session = _workspace.OpenWriteSession())
            {
                if (!workspaceVersion.Equals(_workspace.WorkspaceVersion))
                {
                    return;
                }
                foreach (var analysisChromatograms in analyses)
                {
                    var peptideAnalysis = _workspace.PeptideAnalyses.GetChild(analysisChromatograms.PeptideAnalysisId);
                    if (peptideAnalysis == null)
                    {
                        continue;
                    }
                    var peptideFileAnalysis =
                        peptideAnalysis.GetFileAnalysis(analysisChromatograms.PeptideFileAnalysisId);
                    if (peptideFileAnalysis == null)
                    {
                        continue;
                    }
                    peptideFileAnalysis.SetChromatograms(workspaceVersion, analysisChromatograms);

                }
                if (workspaceVersion.MassVersion != _workspace.SavedWorkspaceVersion.MassVersion)
                {
                    return;
                }
                session.BeginTransaction();
                foreach (AnalysisChromatograms analysis in analyses)
                {
                    var dbPeptideAnalysis = session.Get<DbPeptideFileAnalysis>(analysis.PeptideFileAnalysisId);
                    if (dbPeptideAnalysis == null)
                    {
                        continue;
                    }
                    if (analysis.MinCharge != dbPeptideAnalysis.PeptideAnalysis.MinCharge
                        || analysis.MaxCharge != dbPeptideAnalysis.PeptideAnalysis.MaxCharge)
                    {
                        continue;
                    }
                    dbPeptideAnalysis.Times = analysis.Times.ToArray();
                    dbPeptideAnalysis.ScanIndexes = analysis.ScanIndexes.ToArray();
                    dbPeptideAnalysis.ChromatogramCount = analysis.Chromatograms.Count;
                    session.Update(dbPeptideAnalysis);
                    var dbChromatogramDict = dbPeptideAnalysis.GetChromatogramDict();
                    foreach (Chromatogram chromatogram in analysis.Chromatograms)
                    {
                        DbChromatogram dbChromatogram;
                        if (!dbChromatogramDict.TryGetValue(chromatogram.MzKey, out dbChromatogram))
                        {
                            dbChromatogram = new DbChromatogram
                                                 {
                                                     PeptideFileAnalysis = dbPeptideAnalysis,
                                                     MzKey = chromatogram.MzKey,
                                                 };
                        }
                        dbChromatogram.IntensitiesBytes = ArrayConverter.ToBytes(chromatogram.Intensities.ToArray());
                        dbChromatogram.PeakMzsBytes = ArrayConverter.ToBytes(chromatogram.PeakMzs.ToArray());
                        dbChromatogram.Mz = chromatogram.Mz;
                        session.SaveOrUpdate(dbChromatogram);
                    }
                }
                session.Transaction.Commit();
            }
            _workspace.ResultCalculator.Wake();
        }

        public void WorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            _eventWaitHandle.Set();
        }

        public void WorkspaceSave(Workspace workspace)
        {
            _eventWaitHandle.Set();
        }

        public class Chromatogram
        {
            public Chromatogram (MzKey mzKey, double mz)
            {
                MzKey = mzKey;
                Mz = mz;
                Intensities = new List<float>();
                PeakMzs = new List<float>();
            }
            public MzKey MzKey { get; private set; }
            public double Mz { get; private set; }
            public List<float> Intensities { get; private set; }
            public List<float> PeakMzs { get; private set; }
        }
        public int PendingAnalysisCount { get; private set; }
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
    }

    public class AnalysisChromatograms
    {
        public AnalysisChromatograms(PeptideFileAnalysis peptideFileAnalysis)
        {
            PeptideFileAnalysisId = peptideFileAnalysis.Id.Value;
            PeptideAnalysisId = peptideFileAnalysis.PeptideAnalysis.Id.Value;
            FirstTime = peptideFileAnalysis.FirstTime;
            LastTime = peptideFileAnalysis.LastTime;
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
        public void AddPoints(int scanIndex, double time, List<MsDataFileUtil.ChromatogramPoint> points)
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
                var point = points[i];
                chromatogram.Intensities.Add((float) point.Intensity);
                chromatogram.PeakMzs.Add((float) point.PeakMz);
            }
        }
    }
}
