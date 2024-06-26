/*
 * Original author: Brian Pratt <bspratt .at. proteinms dot net >
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Model.DdaSearch;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public class HardklorSearchControl : SearchControl
    {
        private ImportPeptideSearch ImportPeptideSearch;
        private ImportPeptideSearch.HardklorSettings _settings;
        private int _totalSteps;
        private int _currentStep;
        HardklorSearchEngine FeatureFinder => ImportPeptideSearch.SearchEngine as HardklorSearchEngine;


        public HardklorSearchControl(ImportPeptideSearch importPeptideSearch)
        {
            ImportPeptideSearch = importPeptideSearch;
        }

        // Steps, in parallel, for n files:
        //    Threads 0 through (n-1): per file, convert to mzML, then do HardKlor then Bullseye
        //    Thread n: Perform alignments on all mzMLs as they become available
        // Finally combine the results across replicates

        private bool Search(ImportPeptideSearch.HardklorSettings settings, CancellationTokenSource token)
        {
            _settings = settings;
            ParallelRunnerProgressControl multiProgressControl = null;
            try
            {
                Invoke(new MethodInvoker(() =>
                {
                    multiProgressControl = new ParallelRunnerProgressControl(this);
                    multiProgressControl.Dock = DockStyle.Fill;
                    progressSplitContainer.Panel1.Controls.Add(multiProgressControl);
                }));

                Generate(FeatureFinder, multiProgressControl, token.Token);
            }
            catch (OperationCanceledException e)
            {
                UpdateProgress(_status.ChangeWarningMessage(e.InnerException?.Message ?? e.Message));
                return false;
            }
            catch (Exception e)
            {
                UpdateProgress(_status.ChangeErrorException(e));
                return false;
            }
            finally
            {
                Invoke(new MethodInvoker(() =>
                {
                    progressSplitContainer.Panel1Collapsed = true;
                    progressSplitContainer.Panel1.Controls.Clear();
                    multiProgressControl?.Dispose();
                }));
            }

            return !token.IsCancellationRequested;
        }

        internal void AnnounceStep(string message)
        {
            lock (_status)
            {
                _status = _status.ChangeMessage(message).ChangePercentComplete((100 * _currentStep++) / _totalSteps);
                Invoke(new MethodInvoker(() => UpdateSearchEngineProgress(_status)));
            }
        }

        private void Generate(HardklorSearchEngine config,
            ParallelRunnerProgressControl parallelProgressMonitor,
            CancellationToken cancelToken)
        {
            // parallel converter that starts all files converting
            var runner = new ParallelFeatureFinder(config, this, parallelProgressMonitor, Settings.Default.ActiveDirectory, cancelToken);

            runner.Generate();
        }


        public override void RunSearch()
        {
            // ImportPeptideSearch.SearchEngine.SearchProgressChanged += SearchEngine_MessageNotificationEvent;
            txtSearchProgress.Text = string.Empty;
            _progressTextItems.Clear();
            btnCancel.Enabled = progressBar.Visible = true;

            _cancelToken = new CancellationTokenSource();
            FeatureFinder.SetCancelToken(_cancelToken);

            ActionUtil.RunAsync(RunSearchAsync, @"Feature Finding Search thread");
        }

        private IProgressStatus _status;
        private void RunSearchAsync()
        {
            _totalSteps = (ImportPeptideSearch.SearchEngine.SpectrumFileNames.Length * 4) + 2; //  Per-file: msconvert, Hardklor, Bullseye, RTAlign prep.  All-files: RT alignment, combine features
            _currentStep = 0;

            _status = new ProgressStatus();

            bool success = true;

            if (!_cancelToken.IsCancellationRequested)
            {
                AnnounceStep(PeptideSearchResources.DDASearchControl_SearchProgress_Starting_search);

                success = Search(_settings, _cancelToken);

                Invoke(new MethodInvoker(() => UpdateSearchEngineProgressMilestone(_status, success, _status.SegmentCount,
                    Resources.DDASearchControl_SearchProgress_Search_canceled,
                    PeptideSearchResources.DDASearchControl_SearchProgress_Search_failed,
                    Resources.DDASearchControl_SearchProgress_Search_done)));
            }


            Invoke(new MethodInvoker(() =>
            {
                UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.NoProgress, 0);
                btnCancel.Enabled = false;
                OnSearchFinished(success);
                // ImportPeptideSearch.SearchEngine.SearchProgressChanged -= SearchEngine_MessageNotificationEvent;
            }));
        }

        public class ParallelFeatureFinder
        {
            private readonly CancellationToken _cancelToken;
            private readonly HardklorSearchEngine _featureFinder;
            private HardklorSearchControl _hardklorSearchControl;
            private string _workingDirectory;
            private IProgressMonitor _masterProgressMonitor { get; }
            private IProgressMonitor _parallelProgressMonitor { get; }
            public IList<MsDataFileUri> RawDataFiles { get; }

            public ParallelFeatureFinder(HardklorSearchEngine featureFinder,
                HardklorSearchControl hardklorSearchControl,
                ParallelRunnerProgressControl parallelProgressMonitor,
                string workingDirectory,
                CancellationToken cancelToken)
            {
                _featureFinder = featureFinder;
                _hardklorSearchControl = hardklorSearchControl;
                _cancelToken = cancelToken;
                _parallelProgressMonitor = parallelProgressMonitor;
                _masterProgressMonitor = featureFinder;
                 _workingDirectory = workingDirectory;
                RawDataFiles = _featureFinder.SpectrumFileNames.ToList();
            }

            private bool IsCanceled =>  _parallelProgressMonitor.IsCanceled || _masterProgressMonitor.IsCanceled;

            public bool KeepIntermediateResults
            {
                get => _featureFinder?._keepIntermediateFiles ?? false;
                set
                {
                    if (_featureFinder != null) _featureFinder._keepIntermediateFiles = value;
                }
            }

            public void Generate()
            {
                var rawFileQueue = new ConcurrentQueue<MsDataFileUri>(RawDataFiles);

                // QueueWorkers convert input raw files (in parallel) to feed them to hardklor (in parallel), and to the RT alignment (serial)
                var searchedFiles = 0;
                var allFilesSearched = new ManualResetEventSlim(false);
                var allFilesAligned = new ManualResetEventSlim(false);
                var rawFileThreads = Math.Min(ParallelEx.GetThreadCount() - 1, RawDataFiles.Count);
                var progressMonitorForAlignment = new ProgressMonitorForFile(PeptideSearchResources.ParallelFeatureFinder_Generate_Align_replicates, _parallelProgressMonitor);
                var totalAlignmentSteps = _featureFinder.TotalAlignmentSteps(RawDataFiles.Count); // Each file load is one step, then it's n by n comparison, and final combination step

                void ConsumeAlignmentFile(MsDataFileUri rawFile, int i)
                {
                    if (IsCanceled)
                    {
                        return;
                    }

                    var consumeStatus = new ProgressStatus();

                    var mzmlFile = new MsDataFilePath(HardklorSearchEngine.GetMzmlFilePath(rawFile).GetFilePath());
                    _hardklorSearchControl.AnnounceStep(string.Format(PeptideSearchResources.ParallelFeatureFinder_Generate_Preparing__0__for_RT_alignment, mzmlFile.GetFileNameWithoutExtension())); // Update the master progress leb

                    // Load for alignment
                    progressMonitorForAlignment.UpdateProgress(consumeStatus=(ProgressStatus)consumeStatus.ChangePercentComplete((_featureFinder.AlignmentSpectrumSummaryLists.Count * 100) / totalAlignmentSteps).ChangeMessage(string.Format(PeptideSearchResources.ParallelFeatureFinder_Generate_Reading__0_, mzmlFile.GetFileName())));

                    var summary = HardklorSearchEngine.LoadSpectrumSummaries(mzmlFile);

                    lock (_featureFinder.AlignmentSpectrumSummaryLists)
                    {
                        _featureFinder.AlignmentSpectrumSummaryLists.Add(mzmlFile, summary);
                        progressMonitorForAlignment.UpdateProgress(consumeStatus = (ProgressStatus)consumeStatus.ChangePercentComplete((_featureFinder.AlignmentSpectrumSummaryLists.Count * 100) / totalAlignmentSteps));

                        if (_featureFinder.AlignmentSpectrumSummaryLists.Count == RawDataFiles.Count)
                        {
                            // That was the last one to load, do the alignment amongst them all
                            _hardklorSearchControl.AnnounceStep(PeptideSearchResources.ParallelFeatureFinder_Generate_Performing_RT_alignments);
                            _featureFinder.AlignReplicates(progressMonitorForAlignment);
                            allFilesAligned.Set();
                            progressMonitorForAlignment.UpdateProgress(consumeStatus = (ProgressStatus)consumeStatus.ChangeMessage(PeptideSearchResources.ParallelFeatureFinder_Generate_Waiting_for_Hardklor_Bullseye_completion));
                        }
                    }
                }

                // Start alignments as soon as all mzML are available
                using var aligner = new QueueWorker<MsDataFileUri>(null, ConsumeAlignmentFile);
                aligner.RunAsync(1, @"FeatureFindingAlignmentConsumer", 0, null);
                
                void ConsumeRawFile(MsDataFileUri rawFile, int i)
                {
                    ProcessRawDataFileAsync(rawFile, aligner);
                    lock (rawFileQueue)
                    {
                        ++searchedFiles;
                        if (searchedFiles == RawDataFiles.Count)
                            allFilesSearched.Set();
                    }
                }

                MsDataFileUri ProduceRawFile(int i)
                {
                    if (!rawFileQueue.TryDequeue(out var rawFile))
                    {
                        return null;
                    }
                    return rawFile;
                }

                using var rawFileProcessor = new QueueWorker<MsDataFileUri>(ProduceRawFile, ConsumeRawFile);
                rawFileProcessor.RunAsync(rawFileThreads, @"FeatureFindingConsume", 1, @"FeatureFindingProduce");

                
                // Wait for all Hardklor/Bullseye jobs to finish
                while (!allFilesSearched.Wait(1000, _cancelToken) && !IsCanceled)
                {
                    lock (rawFileProcessor)
                    {
                        var exception = rawFileProcessor.Exception;
                        if (exception != null)
                            throw new OperationCanceledException(exception.Message, exception);
                    }
                }

                if (IsCanceled)
                {
                    return;
                }

                // Wait for all RT alignments to complete
                while (!allFilesAligned.Wait(1000, _cancelToken) && !IsCanceled)
                {
                    lock (aligner)
                    {
                        var exception = aligner.Exception;
                        if (exception != null)
                            throw new OperationCanceledException(exception.Message, exception);
                    }
                }

                if (IsCanceled)
                {
                    return;
                }

                // Now look for common features
                var alignmentStatus = new ProgressStatus();
                _hardklorSearchControl.AnnounceStep(PeptideSearchResources.ParallelFeatureFinder_Generate_Searching_for_common_features_across_replicates);
                progressMonitorForAlignment.UpdateProgress(alignmentStatus = (ProgressStatus)alignmentStatus.ChangePercentComplete((_featureFinder.AlignmentSpectrumSummaryLists.Count * 100) / totalAlignmentSteps).ChangeMessage((DdaSearchResources.HardklorSearchEngine_FindSimilarFeatures_Looking_for_features_occurring_in_multiple_replicates)));

                _featureFinder.FindSimilarFeatures();

                _masterProgressMonitor.UpdateProgress(alignmentStatus.ChangePercentComplete(100));
            }

            private static string MsconvertOutputExtension => @".mzML";


            private void ProcessRawDataFileAsync(MsDataFileUri rawFile, QueueWorker<MsDataFileUri> aligner)
            {
                var progressMonitorForFile = new ProgressMonitorForFile(rawFile.GetFileName(), _parallelProgressMonitor);
                var mzmlFilePath = HardklorSearchEngine.GetMzmlFilePath(rawFile).GetFilePath();
                IProgressStatus status = new ProgressStatus();

                string convertMessage;
                if ((string.Compare(mzmlFilePath, rawFile.GetFilePath(), StringComparison.OrdinalIgnoreCase) == 0) ||
                    (File.Exists(mzmlFilePath) &&
                     File.GetLastWriteTime(mzmlFilePath) > File.GetLastWriteTime(rawFile.GetFilePath()) &&
                     MsDataFileImpl.IsValidFile(mzmlFilePath)))
                {
                    // No need for mzML conversion
                    convertMessage = string.Format(
                        Resources.MsconvertDdaConverter_Run_Re_using_existing_converted__0__file_for__1__,
                        MsconvertOutputExtension, rawFile.GetSampleOrFileName());
                    status = status.ChangeMessage(convertMessage);
                    _hardklorSearchControl.AnnounceStep(convertMessage); // Update main window log
                }
                else
                {
                    status = status.ChangeSegments(0, 1);
                    const string MSCONVERT_EXE = @"msconvert";
                    status = status.ChangeSegmentName(@"msconvert");
                    convertMessage = string.Format(DdaSearchResources.MsconvertDdaConverter_Run_Converting_file___0___to__1_, rawFile.GetSampleOrFileName(), @"mzML");
                    status = status.ChangeMessage(convertMessage);
                    progressMonitorForFile.UpdateProgress(status);
                    var pr = new ProcessRunner();
                    var psi = new ProcessStartInfo(MSCONVERT_EXE)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        Arguments =
                            "-v -z --mzML " +
                            $"-o {Path.GetDirectoryName(mzmlFilePath).Quote()} " +
                            $"--outfile {Path.GetFileName(mzmlFilePath).Quote()} " +
                            " --acceptZeroLengthSpectra --simAsSpectra --combineIonMobilitySpectra" +
                            " --filter \"peakPicking true 1-\" " +
                            " --filter \"msLevel 1\" " +
                            rawFile.GetFilePath().Quote()
                    };

                    try
                    {
                        var cmd = $@"{psi.FileName} {psi.Arguments}";
                        _hardklorSearchControl.AnnounceStep($@"{convertMessage}: {cmd}"); // Update main window log
                        progressMonitorForFile.UpdateProgress(status = status.ChangeMessage(cmd)); // Update local progress bar
                        pr.Run(psi, null, progressMonitorForFile, ref status, null, ProcessPriorityClass.BelowNormal);
                    }
                    catch (Exception e)
                    {
                        progressMonitorForFile.UpdateProgress(status.ChangeMessage(e.Message));
                    }
                }
                if (progressMonitorForFile.IsCanceled)
                {
                    _featureFinder.DeleteIntermediateFiles(); // Delete .conf etc
                    FileEx.SafeDelete(mzmlFilePath, true);
                    return;
                }

                lock (aligner)
                {
                    aligner.Add(rawFile); // Let aligner thread know this mzML file is ready to be loaded for alignment
                }

                var mzml = new MsDataFilePath(HardklorSearchEngine.GetMzmlFilePath(rawFile).GetFilePath());

                // Run Hardklor
                status = _featureFinder.RunFeatureFinderStep(progressMonitorForFile, _workingDirectory, _hardklorSearchControl,  status, mzml, false);
                if (progressMonitorForFile.IsCanceled)
                {
                    _featureFinder.DeleteIntermediateFiles(); // Delete .conf etc
                    FileEx.SafeDelete(mzmlFilePath, true);
                    return;
                }

                // Run Bullseye
                status = _featureFinder.RunFeatureFinderStep(progressMonitorForFile, _workingDirectory, _hardklorSearchControl, status, mzml, true);
                if (progressMonitorForFile.IsCanceled)
                {
                    _featureFinder.DeleteIntermediateFiles(); // Delete .conf etc
                    FileEx.SafeDelete(mzmlFilePath, true);
                }

            }

            public class ProgressMonitorForFile : IProgressMonitor
            {
                private readonly string _filename;
                private readonly IProgressMonitor _multiProgressMonitor;
                private int _maxPercentComplete;
                private StringBuilder _logText = new StringBuilder();

                public string LogText => _logText.ToString();

                public ProgressMonitorForFile(string filename, IProgressMonitor multiProgressMonitor)
                {
                    _filename = filename;
                    _multiProgressMonitor = multiProgressMonitor;
                }

                public bool IsCanceled => _multiProgressMonitor.IsCanceled;

                private Regex _msconvert = new Regex(@"writing spectra: (\d+)/(\d+)",
                    RegexOptions.Compiled | RegexOptions.CultureInvariant); // e.g. "Orbi3_SA_IP_SMC1_01.RAW::msconvert: writing spectra: 2202/4528"
                private Regex _hardklor = new Regex(@"(\d+)%",
                    RegexOptions.Compiled | RegexOptions.CultureInvariant);

                public UpdateProgressResponse UpdateProgress(IProgressStatus status)
                {
                    var message = status.Message.Trim();
                    var displayMessage = $@"{_filename}::{status.SegmentName}: {message}";

                    if (status.IsCanceled || status.ErrorException != null)
                    {
                        _logText.AppendLine(message);
                        status = status.ChangePercentComplete(100);
                        _multiProgressMonitor.UpdateProgress(status.ChangeMessage(displayMessage));
                        return UpdateProgressResponse.cancel;
                    }

                    if (string.IsNullOrEmpty(message))
                    {
                        return UpdateProgressResponse.normal; // Don't update 
                    }

                    var match = _msconvert.Match(status.Message); // MSConvert output?
                    if (match.Success && match.Groups.Count == 3)
                    {
                        // e.g. "Orbi3_SA_IP_SMC1_01.RAW::msconvert: writing spectra: 2202/4528"
                        _maxPercentComplete = Math.Max(_maxPercentComplete,
                            Convert.ToInt32(match.Groups[1].Value) * 100 /
                            Convert.ToInt32(match.Groups[2].Value));
                        status = status.ChangePercentComplete(_maxPercentComplete);
                    }
                    else
                    {
                        match = _hardklor.Match(status.Message); // Hardklor output?
                        if (match.Success)
                        {
                            _maxPercentComplete = Math.Max(_maxPercentComplete, Convert.ToInt32(match.Groups[1].Value));
                            status = status.ChangePercentComplete(_maxPercentComplete);
                        }
                    }

                    _logText.AppendLine(message);

                    return _multiProgressMonitor.UpdateProgress(status.ChangeMessage(displayMessage));
                }
                
                public bool HasUI => _multiProgressMonitor.HasUI;
            }
        }


    }
}
