using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using NHibernate;
using NHibernate.Criterion;
using NHibernate.Mapping;
using ProteowizardWrapper;
using turnover.Data;
using turnover.Model;
using Array=System.Array;

namespace turnover
{
    public class ChromatogramGenerator
    {
        private readonly Workspace workspace;
        private readonly List<PeptideFileAnalysis> requiredPeptideAnalyses = new List<PeptideFileAnalysis>();
        private readonly HashSet<PeptideFileAnalysis> _requiredPeptideAnalysesSet = new HashSet<PeptideFileAnalysis>();
        private Thread chromatogramGeneratorThread;
        private readonly EventWaitHandle eventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private bool isRunning;
        private int _progress;
        private String _statusMessage;
        private HashSet<long> _activePeptideFileAnalysisIds = new HashSet<long>();

        public ChromatogramGenerator(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public void Start()
        {
            lock(this)
            {
                if (isRunning)
                {
                    return;
                }
                isRunning = true;
                if (chromatogramGeneratorThread == null)
                {
                    chromatogramGeneratorThread = new Thread(GenerateChromatograms)
                                                      {
                                                          Name = "Chromatogram Generator",
                                                          Priority = ThreadPriority.BelowNormal
                                                      };
                    chromatogramGeneratorThread.Start();
                }
                eventWaitHandle.Set();
            }
        }

        public void Stop()
        {
            lock(this)
            {
                if (!isRunning)
                {
                    return;
                }
                isRunning = false;
                eventWaitHandle.Set();
            }
        }

        public bool IsRunning()
        {
            lock(this)
            {
                return isRunning;
            }
        }

        public void GetProgress(out String statusMessage, out int progress)
        {
            lock(this)
            {
                statusMessage = _statusMessage;
                progress = _progress;
            }
        }

        private void GenerateChromatograms()
        {
            while (true)
            {
                var peptideFileAnalyses = new HashSet<PeptideFileAnalysis>();
                MsDataFile msDataFile = null;
                lock(this)
                {
                    if (!isRunning)
                    {
                        break;
                    }
                    if (requiredPeptideAnalyses.Count > 0)
                    {
                        var peptideAnalysis = requiredPeptideAnalyses[0];
                        peptideFileAnalyses.Add(requiredPeptideAnalyses[0]);
                        requiredPeptideAnalyses.RemoveAt(0);
                        msDataFile = peptideAnalysis.MsDataFile;
                        for (int i = requiredPeptideAnalyses.Count - 1; i >= 0; i--)
                        {
                            peptideAnalysis = requiredPeptideAnalyses[i];
                            if (Equals(msDataFile, peptideAnalysis.MsDataFile))
                            {
                                peptideFileAnalyses.Add(peptideAnalysis);
                                requiredPeptideAnalyses.RemoveAt(i);
                            }
                        }
                    }
                    _requiredPeptideAnalysesSet.ExceptWith(peptideFileAnalyses);
                    if (peptideFileAnalyses.Count == 0)
                    {
                        eventWaitHandle.Reset();
                        _statusMessage = "Finished generating chromatograms";
                    }
                    else if (peptideFileAnalyses.Count == 1)
                    {
                        var peptideFileAnalysis = peptideFileAnalyses.ToArray()[0];
                        _statusMessage = "Generating chromatograms for " + peptideFileAnalysis.Peptide.Sequence + " in " +
                                         msDataFile.Label;
                    }
                    else
                    {
                        _statusMessage = "Generating chromatograms for " + peptideFileAnalyses.Count + " peptides in " +
                                         msDataFile.Label;
                    }
                    _progress = 0;
                    _activePeptideFileAnalysisIds.Clear();
                    foreach (PeptideFileAnalysis peptideFileAnalysis in peptideFileAnalyses)
                    {
                        if (peptideFileAnalysis.HasChromatograms)
                        {
                            continue;
                        }
                        _activePeptideFileAnalysisIds.Add(peptideFileAnalysis.Id.Value);
                    }
                }
                if (peptideFileAnalyses.Count == 0)
                {
                    eventWaitHandle.WaitOne();
                    continue;
                }
                var analysisChromatograms = new List<AnalysisChromatograms>();
                foreach (PeptideFileAnalysis peptideFileAnalysis in peptideFileAnalyses)
                {
                    analysisChromatograms.Add(new AnalysisChromatograms(peptideFileAnalysis));
                }
                GenerateChromatograms(msDataFile, analysisChromatograms);
            }
        }

        private bool UpdateProgress(int progress)
        {
            lock(this)
            {
                _progress = progress;
                return isRunning;
            }
        }

        private void GenerateChromatograms(MsDataFile msDataFile, List<AnalysisChromatograms> analyses)
        {
            int totalAnalyses = analyses.Count;
            if (totalAnalyses == 0)
            {
                return;
            }
            if (!UpdateProgress(0))
            {
                return;
            }
            analyses = new List<AnalysisChromatograms>(analyses);
            using (var pwizMsDataFileImpl = new MsDataFileImpl(msDataFile.Path))
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
                    if (!UpdateProgress(progress))
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
                    double[] mzArray, intensityArray;
                    pwizMsDataFileImpl.GetSpectrum(iScan, out mzArray, out intensityArray);
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
                    if (completeAnalyses.Count > 10)
                    {
                        SaveChromatograms(completeAnalyses, null);
                        completeAnalyses.Clear();
                    }
                    analyses = incompleteAnalyses;
                }
                completeAnalyses.AddRange(analyses);
                SaveChromatograms(completeAnalyses, msDataFile);
                lock(this)
                {
                    _activePeptideFileAnalysisIds.Clear();
                }
            }
        }

        private void SaveChromatograms(ICollection<AnalysisChromatograms> analyses, MsDataFile msDataFile)
        {
            using (ISession session = workspace.OpenWriteSession())
            {
                ITransaction transaction = session.BeginTransaction();
                foreach (AnalysisChromatograms analysis in analyses)
                {
                    var dbPeptideAnalysis = session.Get<DbPeptideFileAnalysis>(analysis.PeptideFileAnalysis.Id);
                    if (dbPeptideAnalysis == null)
                    {
                        continue;
                    }
                    dbPeptideAnalysis.Times = analysis.Times.ToArray();
                    dbPeptideAnalysis.ScanIndexes = analysis.ScanIndexes.ToArray();
                    dbPeptideAnalysis.HasChromatograms = true;
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
                if (msDataFile != null)
                {
                    msDataFile.MsDataFileData.Save(session);
                }
                transaction.Commit();
            }
            foreach (var analysis in analyses)
            {
                Debug.Assert(_activePeptideFileAnalysisIds.Contains(analysis.PeptideFileAnalysis.Id.Value));
                analysis.PeptideFileAnalysis.HasChromatograms = true;
            }
        }

        public void AddPeptideAnalysis(PeptideFileAnalysis peptideFileAnalysis)
        {
            AddPeptideFileAnalyses(new[] {peptideFileAnalysis});
        }

        public void AddPeptideFileAnalyses(ICollection<PeptideFileAnalysis> peptideFileAnalyses)
        {
            lock(this)
            {
                Start();
                foreach (var peptideFileAnalysis in peptideFileAnalyses)
                {
                    if (_activePeptideFileAnalysisIds.Contains(peptideFileAnalysis.Id.Value))
                    {
                        continue;
                    }
                    if (_requiredPeptideAnalysesSet.Contains(peptideFileAnalysis))
                    {
                        continue;
                    }
                    requiredPeptideAnalyses.Add(peptideFileAnalysis);
                    _requiredPeptideAnalysesSet.Add(peptideFileAnalysis);
                }
                eventWaitHandle.Set();
            }
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
        public interface IProgressMonitor
        {
            bool UpdateStatus(String text);
        }
    }

    public class AnalysisChromatograms
    {
        public AnalysisChromatograms(PeptideFileAnalysis peptideFileAnalysis)
        {
            PeptideFileAnalysis = peptideFileAnalysis;
            FirstTime = peptideFileAnalysis.FirstTime;
            LastTime = peptideFileAnalysis.LastTime;
            Chromatograms = new List<ChromatogramGenerator.Chromatogram>();
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge ++)
            {
                var mzs = PeptideAnalysis.TurnoverCalculator.GetMzs(charge);
                for (int massIndex = 0; massIndex < mzs.Count; massIndex ++)
                {
                    Chromatograms.Add(new ChromatogramGenerator.Chromatogram(new MzKey(charge, massIndex), mzs[massIndex]));
                }
            }
            ScanIndexes = new List<int>();
            Times = new List<double>();
        }
        public PeptideFileAnalysis PeptideFileAnalysis { get; private set; }
        public PeptideAnalysis PeptideAnalysis { get { return PeptideFileAnalysis.PeptideAnalysis; } }
        public double FirstTime { get; private set; }
        public double LastTime { get; private set; }
        public List<ChromatogramGenerator.Chromatogram> Chromatograms { get; private set; }
        public List<int> ScanIndexes { get; private set; }
        public List<double> Times { get; private set;}
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
