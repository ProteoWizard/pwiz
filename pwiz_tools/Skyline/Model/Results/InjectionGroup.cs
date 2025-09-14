/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// A group of files (or just one file) that are having chromatograms extracted at the same time.
    /// These files all have to wait for each other to finish their first pass <see cref="CreateConversion"/> gets called.
    /// </summary>
    public class InjectionGroup
    {
        private Dictionary<MsDataFileUri, FileBuilderStatus> _fileBuilderStatuses =
            new Dictionary<MsDataFileUri, FileBuilderStatus>();

        private ChromCacheBuilder.RetentionTimePredictor _retentionTimePredictor;
        private bool? _createConversionResult;
        private Exception _createConversionException;
        

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
                    var peptideDocNode = group.First().PeptideDocNode;
                    var bestGroup = group.GroupBy(prt => prt.TransitionCount).OrderByDescending(g => g.Key).First();
                    var retentionTime = bestGroup.Average(prt => prt.RetentionTime);
                    _retentionTimePredictor.AddPeptideTime(peptideDocNode, retentionTime);
                }

                try
                {
                    _createConversionResult = _retentionTimePredictor.CreateConversion();
                }
                catch (Exception exception)
                {
                    _createConversionException = exception;
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
                    if (_createConversionException != null)
                    {
                        if (_createConversionException is CalculatorException)
                        {
                            throw new CalculatorException(_createConversionException.Message,
                                _createConversionException);
                        }
                        else
                        {
                            Helpers.WrapAndThrowException(_createConversionException);
                        }
                    }
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
            var paths = _fileBuilderStatuses.Keys.ToList();
            if (paths.Count == 0)
            {
                return;
            }
            // Build everything except the first file on a background thread
            foreach (var path in paths.Skip(1))
            {
                ActionUtil.RunAsync(() => BuildFile(path), @"InjectionGroup Load " + path.GetFileName());
            }
            // Build the first file on this thread
            BuildFile(paths[0]);
            // Wait for everything to finish.
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

        private void BuildFile(MsDataFileUri msDataFileUri)
        {
            var fileBuilderStatus = _fileBuilderStatuses[msDataFileUri];
            try
            {
                var loader = new SingleFileLoadMonitor(MultiFileLoadMonitor, msDataFileUri);
                var status = fileBuilderStatus.LoadingStatus;
                try
                {
                    if (Program.MultiProcImport && Program.ImportProgressPipe == null && ChromatogramSet == null)
                    {
                        // Import using a child process.
                        ChromatogramCache.BuildInSeparateProcess(msDataFileUri, DocumentFilePath, fileBuilderStatus.PartPath, status, loader);
                        var cacheNew = ChromatogramCache.Load(fileBuilderStatus.PartPath, status, loader, Document);
                        CompleteAction(cacheNew, fileBuilderStatus.LoadingStatus);
                    }
                    else
                    {
                        // Import using threads in this process.
                        status = status.ChangeFilePath(msDataFileUri);
                        var builder = new ChromCacheBuilder(this, fileBuilderStatus.PartPath, msDataFileUri, loader, status);
                        builder.BuildCache();
                    }
                }
                catch (Exception x)
                {
                    CompleteAction(null, status.ChangeErrorException(x));
                }
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
