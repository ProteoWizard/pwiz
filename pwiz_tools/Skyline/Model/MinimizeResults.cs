using System;
using System.IO;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class MinimizeResults
    {
        private ChromCacheMinimizer.Settings _settings;
        private ChromCacheMinimizer _chromCacheMinimizer;
        private BackgroundWorker _statisticsCollector;
        private bool _doStatisticsCollection;

        private SrmDocument _document;


        public MinimizeResults(SrmDocument document, ProgressCallback doOnProgress)
        {
            _doStatisticsCollection = false; // Block statistics collection during initialization
            _doOnProgress = doOnProgress;
            Settings = new ChromCacheMinimizer.Settings()
                .ChangeDiscardUnmatchedChromatograms(false)
                .ChangeDiscardAllIonsChromatograms(false)
                .ChangeNoiseTimeRange(null);
            _document = document;
            SetDocument(_document);
        }

        public delegate void ProgressCallback(ChromCacheMinimizer.MinStatistics minStatistics, BackgroundWorker worker);

        public ProgressCallback _doOnProgress { get; private set; }
        public bool BlockStatisticsCollection => !_doStatisticsCollection;

        public void SetDocument(SrmDocument document)
        {
            _document = document;
            ChromCacheMinimizer = document.Settings.HasResults
                ? document.Settings.MeasuredResults.GetChromCacheMinimizer(document)
                : null;
        }

        public ChromCacheMinimizer.Settings Settings
        {
            get
            {
                return _settings;
            }
            set
            {
                if (Equals(value, Settings))
                {
                    return;
                }
                
                _settings = value;
                if (_doStatisticsCollection && ChromCacheMinimizer != null)
                {
                    StatisticsCollector = new BackgroundWorker(this, null);
                }
            }
        }

        public ChromCacheMinimizer ChromCacheMinimizer
        {
            get { return _chromCacheMinimizer; }
            set
            {
                if (ReferenceEquals(value, _chromCacheMinimizer))
                {
                    return;
                }
                if (ChromCacheMinimizer != null)
                {
                    StatisticsCollector = null;
                }
                _chromCacheMinimizer = value;
                if (_doStatisticsCollection && ChromCacheMinimizer != null)
                {
                    StatisticsCollector = new BackgroundWorker(this, null);
                }
            }
        }

        public BackgroundWorker StatisticsCollector
        {
            get { return _statisticsCollector; }
            private set
            {
                if (ReferenceEquals(value, _statisticsCollector) || !_doStatisticsCollection)
                {
                    return;
                }
                if (StatisticsCollector != null)
                {
                    StatisticsCollector.Dispose();
                }
                _statisticsCollector = value;
                if (StatisticsCollector != null)
                {
                    // ReSharper disable once LocalizableElement - threadName does not need to be localized
                    ActionUtil.RunAsync(StatisticsCollector.CollectStatistics, @"Collect statistics");
                }
            }
        }
        
        public void PauseStatisticsCollection()
        {
            _doStatisticsCollection = false;
            StatisticsCollector = null;
        }

        public void StartStatisticsCollection()
        {
            _doStatisticsCollection = true;
            StatisticsCollector = new BackgroundWorker(this, null);
        }


        public MeasuredResults MinimizeToFile(string targetFile, ILongWaitBroker longWaitBroker)
        {
            var targetSkydFile = ChromatogramCache.FinalPathForName(targetFile, null);
            using (var skydSaver = new FileSaver(targetSkydFile))
            using (var scansSaver = new FileSaver(targetSkydFile + ChromatogramCache.SCANS_EXT, true))
            using (var peaksSaver = new FileSaver(targetSkydFile + ChromatogramCache.PEAKS_EXT, true))
            using (var scoreSaver = new FileSaver(targetSkydFile + ChromatogramCache.SCORES_EXT, true))
            {
                skydSaver.Stream = File.OpenWrite(skydSaver.SafeName);

                using (var backgroundWorker =
                    new BackgroundWorker(this, longWaitBroker))
                {
                    backgroundWorker.RunBackground(skydSaver.Stream,
                        scansSaver.FileStream, peaksSaver.FileStream, scoreSaver.FileStream);
                }
                
                return _document.Settings.MeasuredResults.CommitCacheFile(skydSaver);
            }
        }


        /// <summary>
        /// Handles the task of either estimating the space savings the user will achieve
        /// when they minimize the cache file, and actually doing to the work to minimize
        /// the file.
        /// The work that this class is doing can be cancelled by calling <see cref="IDisposable.Dispose"/>.
        /// </summary>
        public class BackgroundWorker : MustDispose
        {
            private readonly MinimizeResults _minimizeResults;
            public readonly ILongWaitBroker LongWaitBroker;

            public BackgroundWorker(MinimizeResults minimizeResults, ILongWaitBroker longWaitBroker)
            {
                _minimizeResults = minimizeResults;
                LongWaitBroker = longWaitBroker;
            }

            void OnProgress(ChromCacheMinimizer.MinStatistics minStatistics)
            {
                if (_minimizeResults._doOnProgress != null)
                {
                    _minimizeResults._doOnProgress(minStatistics, this);
                }
            }

            public void RunBackground(Stream outputStream, FileStream outputStreamScans, FileStream outputStreamPeaks,
                FileStream outputStreamScores)
            {
                _minimizeResults.ChromCacheMinimizer.Minimize(_minimizeResults.Settings, OnProgress, outputStream,
                    outputStreamScans, outputStreamPeaks, outputStreamScores);
            }

            public void CollectStatistics()
            {
                try
                {
                    RunBackground(null, null, null, null);
                }
                catch (ObjectDisposedException)
                {
                    
                }
                catch (Exception e)
                {
                    Program.ReportException(e);
                }
            }
        }
    }
}
