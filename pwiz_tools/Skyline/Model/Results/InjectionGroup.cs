using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using static pwiz.Skyline.Model.ModificationMatcher;

namespace pwiz.Skyline.Model.Results
{
    public class InjectionGroup
    {
        private Dictionary<MsDataFileUri, FileBuilderStatus> _fileBuilderStatuses =
            new Dictionary<MsDataFileUri, FileBuilderStatus>();

        private ChromCacheBuilder.RetentionTimePredictor _retentionTimePredictor;
        private bool? _createConversionResult;
        

        public InjectionGroup(SrmDocument document, string documentFilePath, ChromatogramCache cacheRecalc, FileLoadCompletionAccumulator fileLoadCompletionAccumulator,
            ChromatogramSet chromatogramSet, MultiFileLoadMonitor multiFileLoadMonitor)
        {
            Document = document;
            DocumentFilePath = documentFilePath;
            CacheRecalc = cacheRecalc;
            FileLoadCompletionAccumulator = fileLoadCompletionAccumulator;
            ChromatogramSet = chromatogramSet;
            MultiFileLoadMonitor = multiFileLoadMonitor;
            _retentionTimePredictor = new ChromCacheBuilder.RetentionTimePredictor(document.Settings.PeptideSettings.Prediction.RetentionTime);
        }

        public SrmDocument Document { get; }
        public string DocumentFilePath { get; }
        public ChromatogramCache CacheRecalc { get; }
        public ChromatogramSet ChromatogramSet { get; }
        public FileLoadCompletionAccumulator FileLoadCompletionAccumulator { get; }
        public MultiFileLoadMonitor MultiFileLoadMonitor { get; }

        public bool IsFirstPassPeptide(PeptideDocNode peptideDocNode)
        {
            return peptideDocNode != null && _retentionTimePredictor.IsFirstPassPeptide(peptideDocNode);
        }


        internal RetentionTimePrediction GetRetentionTimePrediction(PeptideDocNode peptideDocNode)
        {
            if (!_retentionTimePredictor.HasCalculator)
            {
                return null;
            }
            return new RetentionTimePrediction(_retentionTimePredictor.GetPredictedRetentionTime(peptideDocNode), _retentionTimePredictor.TimeWindow);
        }

        public void CompletedFirstPass(MsDataFileUri msDataFileUri)
        {
            _fileBuilderStatuses[msDataFileUri].CompletedFirstPass = true;
            TryCreateConversion();
        }

        public void UseExistingResults(MsDataFileUri msDataFileUri)
        {
            StoreExistingResults(msDataFileUri);
            CompletedFirstPass(msDataFileUri);
        }

        private void StoreExistingResults(MsDataFileUri msDataFileUri)
        {
            if (!Document.MeasuredResults.TryGetChromatogramSet(ChromatogramSet.Name, out _, out int replicateIndex))
            {
                return;
            }
            var chromFileInfoId = Document.MeasuredResults.Chromatograms[replicateIndex].FindFile(msDataFileUri);
            if (chromFileInfoId == null)
            {
                return;
            }
            var fileBuildStatus = _fileBuilderStatuses[msDataFileUri];
            foreach (var molecule in Document.Molecules)
            {
                if (!IsFirstPassPeptide(molecule))
                {
                    continue;
                }

                var transitionGroupChromInfo = molecule.TransitionGroups
                    .SelectMany(tg => tg.GetSafeChromInfo(replicateIndex)).FirstOrDefault(chromInfo =>
                        0 == chromInfo.OptimizationStep && ReferenceEquals(chromFileInfoId, chromInfo.FileId) && chromInfo.RetentionTime.HasValue);
                if (transitionGroupChromInfo != null)
                {
                    var transitionCount = molecule.TransitionGroups
                        .SelectMany(tg => tg.Transitions.SelectMany(t => t.GetSafeChromInfo(replicateIndex))).Count(
                            transitionChromInfo => !transitionChromInfo.IsEmpty &&
                                                   0 == transitionChromInfo.OptimizationStep &&
                                                   ReferenceEquals(chromFileInfoId, transitionChromInfo.FileId));
                    fileBuildStatus.PeptideRetentionTimes.Add(new PeptideRetentionTime(molecule, transitionCount, transitionGroupChromInfo.RetentionTime.Value));
                }
            }

        }

        private void TryCreateConversion()
        {
            lock (this)
            {
                if (_fileBuilderStatuses.Values.Any(status => !status.CompletedFirstPass))
                {
                    return;
                }

                foreach (var group in _fileBuilderStatuses.Values.SelectMany(v => v.PeptideRetentionTimes)
                             .GroupBy(prt => prt.PeptideDocNode.ModifiedTarget))
                {
                    var bestPeptideRetentionTime = group.OrderByDescending(prt => prt.TransitionCount).First();
                    _retentionTimePredictor.AddPeptideTime(bestPeptideRetentionTime.PeptideDocNode, bestPeptideRetentionTime.RetentionTime);
                }

                try
                {
                    _createConversionResult = _retentionTimePredictor.CreateConversion();
                }
                catch
                {
                    _createConversionResult = false;
                }
                Monitor.PulseAll(this);
            }
        }

        public bool CreateConversion(MsDataFileUri msDataFileUri)
        {
            while (true)
            {
                lock (this)
                {
                    if (_createConversionResult.HasValue)
                    {
                        return _createConversionResult.Value;
                    }
                    Monitor.Wait(this);
                }
            }
        }

        public IRetentionTimePredictor GetRetentionTimePredictor()
        {
            return _retentionTimePredictor;
        }

        internal void StorePeptideRetentionTime(PeptideChromDataSets peptideChromDataSets)
        {
            var nodePep = peptideChromDataSets.NodePep;
            if (nodePep == null || !Equals(nodePep.GlobalStandardType, PeptideDocNode.STANDARD_TYPE_IRT))
                return;

            int transitionCount = peptideChromDataSets.DataSets.Sum(dataSet =>
                dataSet.Chromatograms.Count(chromatogram => null != chromatogram.DocNode && 0 == chromatogram.OptimizationStep));
            if (transitionCount == 0)
            {
                return;
            }
            var firstDataSet = peptideChromDataSets.DataSets.FirstOrDefault();
            if (firstDataSet == null || firstDataSet.MaxPeakIndex < 0)
            {
                return;
            }
            var bestPeak = firstDataSet.BestChromatogram.Peaks[firstDataSet.MaxPeakIndex];
            var peptideRetentionTime = new PeptideRetentionTime(nodePep, transitionCount, bestPeak.RetentionTime);
            _fileBuilderStatuses[peptideChromDataSets.FileInfo.FilePath].PeptideRetentionTimes.Add(peptideRetentionTime);
        }

        private class FileBuilderStatus
        {
            public FileBuilderStatus(string partPath, ChromatogramLoadingStatus loadingStatus)
            {
                PartPath = partPath;
                LoadingStatus = loadingStatus;
            }
            public string PartPath { get; }
            public ChromatogramLoadingStatus LoadingStatus { get; }
            public bool CompletedFirstPass { get; set; }
            public bool Completed { get; set; }
            public ConcurrentBag<PeptideRetentionTime> PeptideRetentionTimes { get;  } = new ConcurrentBag<PeptideRetentionTime>();
        }

        private class PeptideRetentionTime
        {
            public PeptideRetentionTime(PeptideDocNode peptideDocNode, int transitionCount, double retentionTime)
            {
                PeptideDocNode = peptideDocNode;
                TransitionCount = transitionCount;
                RetentionTime = retentionTime;
            }

            public PeptideDocNode PeptideDocNode { get; }
            public int TransitionCount { get; }
            public double RetentionTime { get; }
        }

        public void AddFileToLoad(MsDataFileUri path, string partPath, ChromatogramLoadingStatus loadingStatus)
        {
            _fileBuilderStatuses.Add(path, new FileBuilderStatus(partPath, loadingStatus));
        }

        public void Load()
        {
            foreach (var path in _fileBuilderStatuses.Keys)
            {
                ActionUtil.RunAsync(() => BuildFile(path), @"InjectionGroup Load " + path.GetFileName());
            }

            lock (this)
            {
                while (true)
                {
                    if (_fileBuilderStatuses.Values.All(status => status.Completed))
                    {
                        return;
                    }

                    Monitor.Wait(this);
                }
            }
        }

        private void BuildFile(MsDataFileUri path)
        {
            var fileBuilderStatus = _fileBuilderStatuses[path];
            try
            {
                ChromatogramCache.Build(this, fileBuilderStatus.PartPath, path, fileBuilderStatus.LoadingStatus);
                fileBuilderStatus.LoadingStatus.Transitions.Flush();
            }
            finally
            {
                lock (this)
                {
                    fileBuilderStatus.CompletedFirstPass = true;
                    fileBuilderStatus.Completed = true;
                    Monitor.PulseAll(this);
                }
            }
        }

        public void CompleteAction(ChromatogramCache cache, IProgressStatus status)
        {
            FileLoadCompletionAccumulator.Complete(cache, status);
        }
    }
}
