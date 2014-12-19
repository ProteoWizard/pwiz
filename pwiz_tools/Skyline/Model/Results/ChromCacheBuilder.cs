/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009-2010 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class ChromCacheBuilder : ChromCacheWriter
    {
        // Lock on this to access these variables
        private readonly SrmDocument _document;
        private LibraryRetentionTimes _libraryRetentionTimes;
        private int _currentFileIndex = -1;
        private FileBuildInfo _currentFileInfo;

        private readonly ChromatogramCache _cacheRecalc;
        
        // Temporary file used as substitute for the current file for vendor-specific
        // issues in file reading
        private string _tempFileSubsitute;
        // True if write thread was started before temporary file substitution was
        // made
        private bool _tempFileWriteStarted;

        // Lock on _chromDataSets to access these variables
        private readonly List<PeptideChromDataSets> _chromDataSets = new List<PeptideChromDataSets>();
        private bool _writerStarted;
        private bool _writerCompleted;
        private bool _readCompleted;
        private bool _writerIsWaitingForChromDataSets;
        private Exception _writeException;

        // Accessed only on the write thread
        private readonly RetentionTimePredictor _retentionTimePredictor;
        private readonly Dictionary<string, int> _dictSequenceToByteIndex = new Dictionary<string, int>();

        public ChromCacheBuilder(SrmDocument document, ChromatogramCache cacheRecalc,
            string cachePath, IList<MsDataFileUri> msDataFilePaths, ILoadMonitor loader, ProgressStatus status,
            Action<ChromatogramCache, Exception> complete)
            : base(cachePath, loader, status, complete)
        {
            _document = document;
            _cacheRecalc = cacheRecalc;
            MSDataFilePaths = msDataFilePaths;

            // Reserve an array for caching retention time alignment information, if needed
            FileAlignmentIndices = new RetentionTimeAlignmentIndices[msDataFilePaths.Count];

            // Initialize retention time prediction
            _retentionTimePredictor = new RetentionTimePredictor(document.Settings.PeptideSettings.Prediction.RetentionTime);

            // Get peak scoring calculators
            IEnumerable<IPeakFeatureCalculator> calcEnum;
            if (document.Settings.HasResults && document.Settings.MeasuredResults.CachePaths.Any())
            {
                // Once a document has scores continue using those scores, until
                // the document is re-scored.
                calcEnum = document.Settings.MeasuredResults.CachedScoreTypes
                    .Select(PeakFeatureCalculator.GetCalculator);
            }
            else
            {
                calcEnum = PeakFeatureCalculator.Calculators
                    .Where(c => c is DetailedPeakFeatureCalculator);
            }
            DetailedPeakFeatureCalculators = calcEnum.Cast<DetailedPeakFeatureCalculator>().ToArray();
            _listScoreTypes.AddRange(DetailedPeakFeatureCalculators.Select(c => c.GetType()));
        }

        private IList<MsDataFileUri> MSDataFilePaths { get; set; }
        private IList<RetentionTimeAlignmentIndices> FileAlignmentIndices { get; set; }

        private IList<DetailedPeakFeatureCalculator> DetailedPeakFeatureCalculators { get; set; }

        private bool IsTimeNormalArea
        {
            get
            {
                return !_document.Settings.HasResults ||
                       _document.Settings.MeasuredResults.IsTimeNormalArea;
            }
        }

        public override void Dispose()
        {
            RemoveTempFile();

            base.Dispose();
        }

        private void RemoveTempFile()
        {
            if (_tempFileSubsitute != null)
            {
                FileEx.SafeDelete(_tempFileSubsitute, true);
                _tempFileSubsitute = null;
                _tempFileWriteStarted = false;
            }
        }

        public void BuildCache()
        {
            lock (this)
            {
                if (_currentFileIndex != -1)
                    return;
                _currentFileIndex = 0;
                BuildNextFile();
            }
        }

        private void BuildNextFile()
        {
            // Called on a new UI thread.
            LocalizationHelper.InitThread();
            lock (this)
            {
                try
                {
                    BuildNextFileInner();
                }
                finally
                {
                    lock (_chromDataSets)
                    {
                        // Release any writer thread.
                        if (!_writerStarted)
                            Monitor.Pulse(_chromDataSets);
                    }
                }
            }
        }

        private void BuildNextFileInner()
        {
            // If there is a temp file, rewind and retry last file
            if (_tempFileWriteStarted)
            {
                _listCachedFiles.RemoveAt(--_currentFileIndex);
                if (_fs.Stream != null)
                {
                    try { _loader.StreamManager.Finish(_fs.Stream); }
                    catch (IOException) { }

                    _fs.Stream = null;
                }
            }

            if (_currentFileIndex >= MSDataFilePaths.Count)
            {
                ExitRead(null);
                return;
            }

            // Check for cancellation on every chromatogram, because there
            // have been some files that load VERY slowly, and appear to hang
            // on a single file.
            if (_loader.IsCanceled)
            {
                _loader.UpdateProgress(_status = _status.Cancel());
                ExitRead(null);
                return;
            }

            // If not cancelled, update progress.
            var dataFilePath = MSDataFilePaths[_currentFileIndex];
            ChromFileInfo fileInfo = _document.Settings.MeasuredResults.GetChromFileInfo(dataFilePath);
            Assume.IsNotNull(fileInfo);
            string dataFilePathPart;
            MsDataFileUri dataFilePathRecalc = GetRecalcDataFilePath(dataFilePath, out dataFilePathPart);

            if (_tempFileSubsitute == null)
            {
                string format = dataFilePathRecalc == null
                                    ? Resources.ChromCacheBuilder_BuildNextFileInner_Importing__0__
                                    : Resources.ChromCacheBuilder_BuildNextFileInner_Recalculating_scores_for__0_;
                string message = string.Format(format, dataFilePath.GetSampleOrFileName());
                int percent = _currentFileIndex * 100 / MSDataFilePaths.Count;
                _status = _status.ChangeMessage(message).ChangePercentComplete(percent);
                _status = LoadingStatus.ChangeFilePath(dataFilePath).ChangeImporting(true);
                var allChromData = LoadingStatus.Transitions;
                allChromData.FromCache = false;
                allChromData.MaxIntensity = 0;
                allChromData.MaxRetentionTime = 0;
                allChromData.MaxRetentionTimeKnown = false;
                _loader.UpdateProgress(_status);
            }

            try
            {
                var msDataFilePath = dataFilePath as MsDataFilePath;
                if (msDataFilePath != null)
                {
                    dataFilePath = dataFilePathRecalc ?? ChromatogramSet.GetExistingDataFilePath(CachePath, msDataFilePath, out dataFilePathPart);
                    if (dataFilePath == null)
                        throw new FileNotFoundException(string.Format(Resources.ChromCacheBuilder_BuildNextFileInner_The_file__0__does_not_exist, dataFilePathPart), dataFilePathPart);
                }
                MSDataFilePaths[_currentFileIndex] = dataFilePath;

                if (_tempFileSubsitute != null)
                    dataFilePath = MsDataFileUri.Parse(_tempFileSubsitute);

                // HACK: Force the thread that the writer will use into existence
                // This allowed teh DACServer Reader_Waters to function normally the first time through.
                // It is no longer necessary for the MassLynxRaw version of Reader_Waters,
                // but is kept to avoid destabilizing code changes.
                //
                // This does not actually start the loop, but calling the function once,
                // seems to reserve a thread somehow, so that the next call works.
                ActionUtil.RunAsync(() => WriteLoop(_currentFileIndex, true));

                // Read the instrument data indexes
                int sampleIndex = dataFilePath.GetSampleIndex();
                if (sampleIndex == -1)
                    sampleIndex = 0;

                // Once a ChromDataProvider is created, it owns disposing of the MSDataFileImpl.
                MsDataFileImpl inFile = null;
                ChromDataProvider provider = null;
                try
                {
                    
                    if (dataFilePathRecalc == null && !RemoteChromDataProvider.IsRemoteChromFile(dataFilePath))
                    {
                        // Always use SIM as spectra, if any full-scan chromatogram extraction is enabled
                        var enableSimSpectrum = _document.Settings.TransitionSettings.FullScan.IsEnabled;
                        inFile = GetMsDataFile(dataFilePathPart, sampleIndex, enableSimSpectrum);
                    }

                    // Check for cancelation
                    if (_loader.IsCanceled)
                    {
                        _loader.UpdateProgress(_status = _status.Cancel());
                        ExitRead(null);
                        return;
                    }
                    if (_fs.Stream == null)
                        _fs.Stream = _loader.StreamManager.CreateStream(_fs.SafeName, FileMode.Create, true);

                    _currentFileInfo = GetRecalcFileBuildInfo(dataFilePathRecalc);
                    if (null == _currentFileInfo)
                    {
                        if (null == inFile)
                        {
                            _currentFileInfo = new FileBuildInfo(fileInfo.RunStartTime, fileInfo.FileWriteTime ?? DateTime.Now, new MsInstrumentConfigInfo[0], null);
                        }
                        else
                        {
                            _currentFileInfo = new FileBuildInfo(inFile);
                        }
                    }
                    _libraryRetentionTimes = null;

                    // Read and write the mass spec data)
                    if (dataFilePathRecalc != null)
                    {
                        provider = CreateChromatogramRecalcProvider(dataFilePathRecalc, fileInfo);
                        var allChromData = LoadingStatus.Transitions;
                        allChromData.FromCache = true;
                        allChromData.MaxIntensity = (float)(provider.MaxIntensity ?? 0);
                        allChromData.MaxRetentionTime = (float)(provider.MaxRetentionTime ?? 0);
                        allChromData.MaxRetentionTimeKnown = provider.MaxRetentionTime.HasValue;
                    }
                    else if (ChorusResponseChromDataProvider.IsChorusResponse(dataFilePath))
                    {
                        provider = new ChorusResponseChromDataProvider(_document, fileInfo, _status, StartPercent, EndPercent, _loader);
                    }
                    else if (RemoteChromDataProvider.IsRemoteChromFile(dataFilePath))
                    {
                        provider = new RemoteChromDataProvider(_document, _retentionTimePredictor, fileInfo, _status, StartPercent, EndPercent, _loader);
                    }
                    else if (ChromatogramDataProvider.HasChromatogramData(inFile))
                        provider = CreateChromatogramProvider(inFile, fileInfo, _tempFileSubsitute == null);
                    else if (SpectraChromDataProvider.HasSpectrumData(inFile))
                    {                            
                        if (_document.Settings.TransitionSettings.FullScan.IsEnabled && _libraryRetentionTimes == null)
                            _libraryRetentionTimes = _document.Settings.GetRetentionTimes(dataFilePath);

                        provider = CreateSpectraChromProvider(inFile, fileInfo);
                    }
                    else
                    {
                        throw new InvalidDataException(String.Format(Resources.ChromCacheBuilder_BuildNextFileInner_The_sample__0__contains_no_usable_data,
                                                                     dataFilePath.GetSampleOrFileName()));
                    }

                    _currentFileInfo.IsSingleMatchMz = provider.IsSingleMzMatch;

                    Read(provider);

                    _status = provider.Status;

                    if (_status.IsCanceled)
                        ExitRead(null);

                    if ((inFile!=null) && (inFile.GetLog() != null)) // in case perf logging is enabled
                        DebugLog.Info(inFile.GetLog());

                    RemoveTempFile();
                }
                catch (LoadingTooSlowlyException x)
                {
                    _status = x.Status;
                    _tempFileSubsitute = VendorIssueHelper.CreateTempFileSubstitute(dataFilePathPart,
                        sampleIndex, x, _loader, ref _status);
                    _tempFileWriteStarted = _writerStarted;
                    if (_tempFileWriteStarted)
                    {
                        // Trigger next call to BuildNextFile from the write thread
                        PostChromDataSet(null, true);
                    }
                    else
                    {
                        // Just call this function again with the temp file subsitute
                        BuildNextFileInner();
                    }
                }
                finally
                {
                    if (provider != null)
                        provider.Dispose();
                    else if (inFile != null)
                        inFile.Dispose();
                }
            }
            catch (LoadCanceledException x)
            {
                _status = x.Status;
                ExitRead(null);
            }
            catch (MissingDataException x)
            {
                ExitRead(new MissingDataException(x.MessageFormat,
                                                  MSDataFilePaths[_currentFileIndex].GetSampleOrFileName(), x));
            }
            catch (Exception x)
            {
                // Add a more generic message to an exception message that may
                // be fairly unintelligible to the user, but keep the exception
                // message, because ProteoWizard "Unsupported file format" comes
                // in on this channel.
                ExitRead(x);
            }
        }

        private MsDataFileUri GetRecalcDataFilePath(MsDataFileUri dataFilePath, out string dataFilePathPart)
        {
            if (_cacheRecalc == null || !_cacheRecalc.CachedFilePaths.Contains(dataFilePath))
            {
                dataFilePathPart = null;
                return null;
            }
            var msDataFilePath = dataFilePath as MsDataFilePath;
            dataFilePathPart = msDataFilePath != null ? msDataFilePath.FilePath : null;
            return dataFilePath;
        }

        private FileBuildInfo GetRecalcFileBuildInfo(MsDataFileUri dataFilePathRecalc)
        {
            if (_cacheRecalc == null || dataFilePathRecalc == null)
                return null;
            int i = _cacheRecalc.CachedFiles.IndexOf(f => Equals(f.FilePath, dataFilePathRecalc));
            if (i == -1)
                throw new ArgumentException(string.Format(Resources.ChromCacheBuilder_GetRecalcFileBuildInfo_The_path___0___was_not_found_among_previously_imported_results_, dataFilePathRecalc));
            var cachedFile = _cacheRecalc.CachedFiles[i];
            return new FileBuildInfo(cachedFile.RunStartTime,
                                     cachedFile.FileWriteTime,
                                     cachedFile.InstrumentInfoList,
                                     cachedFile.IsSingleMatchMz);
        }

        private MsDataFileImpl GetMsDataFile(string dataFilePathPart, int sampleIndex, bool enableSimSpectrum)
        {
//            if (Directory.Exists(dataFilePathPart))
//            {
//                DirectoryInfo directoryInfo = new DirectoryInfo(dataFilePathPart);
//                var type = DataSourceUtil.GetSourceType(directoryInfo);
//                if (type == DataSourceUtil.TYPE_BRUKER)
//                    throw new LoadingTooSlowlyException(LoadingTooSlowlyException.Solution.bruker_conversion, _status, 0, 0);
//            }
            return new MsDataFileImpl(dataFilePathPart, sampleIndex, enableSimSpectrum);
        }

        private void ExitRead(Exception x)
        {
            Complete(x);
            lock (_chromDataSets)
            {
                _writerStarted = false;
            }
        }

        private void Read(ChromDataProvider provider)
        {
            lock (_chromDataSets)
            {
                _readCompleted = false;
            }

            var dictPeptideChromData = new Dictionary<PeptideSequenceModKey, PeptideChromDataSets>();
            var listChromData = new List<PeptideChromDataSets>();

            var listMzPrecursors = new List<PeptidePrecursorMz>(Precursors);
            listMzPrecursors.Sort((p1, p2) => p1.PrecursorMz.CompareTo(p2.PrecursorMz));

            bool singleMatch = provider.IsSingleMzMatch;
            var setInternalStandards = new HashSet<IsotopeLabelType>(
                _document.Settings.PeptideSettings.Modifications.InternalStandardTypes);

            foreach (var chromDataSet in GetChromDataSets(provider))
            {
                foreach (var matchingGroup in GetMatchingGroups(chromDataSet, listMzPrecursors, singleMatch))
                {
                    var peptidePercursor = matchingGroup.Key;
                    var chromDataSetMatch = matchingGroup.Value;
                    chromDataSetMatch.IsStandard = peptidePercursor != null &&
                        setInternalStandards.Contains(peptidePercursor.NodeGroup.TransitionGroup.LabelType);

                    AddChromDataSet(provider.IsProcessedScans,
                                    chromDataSetMatch,
                                    peptidePercursor,
                                    dictPeptideChromData,
                                    listChromData,
                                    provider.FileInfo);
                }
            }

            listChromData.AddRange(dictPeptideChromData.Values);
            listChromData.Sort(ComparePeptideChromDataSets);

            // Avoid holding onto chromatogram data sets for entire read
            dictPeptideChromData.Clear();
            bool wasFirstPass = true;
            for (int i = 0; i < listChromData.Count; i++)
            {
                var pepChromData = listChromData[i];
                bool firstPass = null != pepChromData.NodePep && _retentionTimePredictor.IsFirstPassPeptide(pepChromData.NodePep);
                if (wasFirstPass && !firstPass)
                {
                    // When we finished with the standard peptides, we need to wait until the writer thread is
                    // finished processing _chromDataSets so that _retentionTimePredictor can provide
                    // times for the rest of the peptides.
                    lock (_chromDataSets)
                    {
                        while (_chromDataSets.Any() || (_writerStarted && !_writerIsWaitingForChromDataSets))
                        {
                            Monitor.Wait(_chromDataSets, 100);
                        }
                    }
                }
                wasFirstPass = firstPass;
                if (!pepChromData.Load(provider))
                    continue;

                PostChromDataSet(pepChromData, false);

                // Release the reference to the chromatogram data set so that
                // it can be garbage collected after it has been written
                listChromData[i] = null;
            }
            // Write scan ids
            var scanIdBytes = provider.ScanIdBytes;
            if (scanIdBytes.Length > 0)
            {
                _currentFileInfo.LocationScanIds = _fsScans.Stream.Position;
                _currentFileInfo.SizeScanIds = scanIdBytes.Length;
                _fsScans.Stream.Write(scanIdBytes, 0, scanIdBytes.Length);
            }

            // Release all provider memory before waiting for write completion
            provider.ReleaseMemory();
            PostChromDataSet(null, true);
        }

        private int ComparePeptideChromDataSets(PeptideChromDataSets p1, PeptideChromDataSets p2)
        {
            if (p1.NodePep != null && p2.NodePep != null)
            {
                bool s1 = string.Equals(p1.NodePep.GlobalStandardType, PeptideDocNode.STANDARD_TYPE_IRT);
                bool s2 = string.Equals(p2.NodePep.GlobalStandardType, PeptideDocNode.STANDARD_TYPE_IRT);
                // Import iRT standards before everything else
                if (s1 != s2)
                {
                    return s1 ? -1 : 1;
                }
            }
            else if (p1.NodePep == null)
                return 1;
            else if (p2.NodePep == null)
                return -1;
            // Otherwise, import in precursor m/z order
            return Comparer.Default.Compare(p1.DataSets[0].PrecursorMz, p2.DataSets[0].PrecursorMz);
        }

        private sealed class PeptidePrecursorMz
        {
            public PeptidePrecursorMz(PeptideDocNode nodePeptide,
                                      TransitionGroupDocNode nodeGroup,
                                      double precursorMz)
            {
                NodePeptide = nodePeptide;
                NodeGroup = nodeGroup;
                PrecursorMz = precursorMz;
            }

            public PeptideDocNode NodePeptide { get; private set; }
            public TransitionGroupDocNode NodeGroup { get; private set; }
            public double PrecursorMz { get; private set; }
        }

        private IEnumerable<PeptidePrecursorMz> Precursors
        {
            get
            {
                return from nodePep in _document.Molecules
                       from nodeGroup in nodePep.Children.Cast<TransitionGroupDocNode>()
                       select new PeptidePrecursorMz(nodePep, nodeGroup, nodeGroup.PrecursorMz);
            }
        }

        private IEnumerable<ChromDataSet> GetChromDataSets(ChromDataProvider provider)
        {
            return GetChromDataSets(provider, IsTimeNormalArea);
        }

        public static IEnumerable<ChromDataSet> GetChromDataSets(ChromDataProvider provider, bool isTimeNormalArea)
        {
            var listKeyIndex = new List<KeyValuePair<ChromKey, int>>(provider.ChromIds);
            listKeyIndex.Sort((p1, p2) => p1.Key.CompareTo(p2.Key));

            ChromKey lastKey = ChromKey.EMPTY;
            ChromDataSet chromDataSet = null;
            foreach (var keyIndex in listKeyIndex)
            {
                var key = keyIndex.Key;
                var chromData = new ChromData(key, keyIndex.Value);

                if (chromDataSet != null && key.ComparePrecursors(lastKey) == 0)
                {
                    chromDataSet.Add(chromData);
                }
                else
                {
                    if (chromDataSet != null)
                        yield return chromDataSet;

                    chromDataSet = new ChromDataSet(isTimeNormalArea, chromData);
                }
                lastKey = key;
            }

            yield return chromDataSet;
        }

        private void AddChromDataSet(bool isProcessedScans,
                                            ChromDataSet chromDataSet,
                                            PeptidePrecursorMz peptidePrecursorMz,
                                            IDictionary<PeptideSequenceModKey, PeptideChromDataSets> dictPeptideChromData,
                                            ICollection<PeptideChromDataSets> listChromData,
                                            ChromFileInfo fileInfo)
        {
            // If there was no matching precursor, just add this as a stand-alone set
            PeptideChromDataSets pepDataSets;
            if (peptidePrecursorMz == null)
            {
                pepDataSets = new PeptideChromDataSets(null,
                    _document, fileInfo, DetailedPeakFeatureCalculators, isProcessedScans);
                pepDataSets.DataSets.Add(chromDataSet);
                listChromData.Add(pepDataSets);
                return;
            }

            // Otherwise, add it to the dictionary by its peptide GlobalIndex to make
            // sure precursors are grouped by peptide
            var nodePep = peptidePrecursorMz.NodePeptide;
            var key = nodePep.SequenceKey;
            if (!dictPeptideChromData.TryGetValue(key, out pepDataSets))
            {
                pepDataSets = new PeptideChromDataSets(nodePep,
                    _document, fileInfo, DetailedPeakFeatureCalculators, isProcessedScans);
                dictPeptideChromData.Add(key, pepDataSets);
            }
            chromDataSet.NodeGroup = peptidePrecursorMz.NodeGroup;
            pepDataSets.Add(nodePep, chromDataSet);
        }

        private void GetPeptideRetentionTimes(PeptideChromDataSets peptideChromDataSets)
        {
            var nodePep = peptideChromDataSets.NodePep;
            if (nodePep == null)
                return;

            string lookupSequence = nodePep.SourceUnmodifiedTextId;
            var lookupMods = nodePep.SourceExplicitMods;
            double[] retentionTimes = _document.Settings.GetRetentionTimes(MSDataFilePaths[_currentFileIndex],
                                                                           lookupSequence,
                                                                           lookupMods);
            bool isAlignedTimes = (retentionTimes.Length == 0);
            if (isAlignedTimes)
            {
                RetentionTimeAlignmentIndices alignmentIndices = FileAlignmentIndices[_currentFileIndex];
                if (alignmentIndices == null)
                {
                    string basename = MSDataFilePaths[_currentFileIndex].GetFileNameWithoutExtension();
                    var fileAlignments = _document.Settings.DocumentRetentionTimes.FileAlignments.Find(basename);
                    alignmentIndices = new RetentionTimeAlignmentIndices(fileAlignments);
                    FileAlignmentIndices[_currentFileIndex] = alignmentIndices;
                }

                retentionTimes = _document.Settings.GetAlignedRetentionTimes(alignmentIndices,
                                                                             lookupSequence,
                                                                             lookupMods);
            }
            peptideChromDataSets.RetentionTimes = retentionTimes;
            peptideChromDataSets.IsAlignedTimes = isAlignedTimes;

            RetentionTimePrediction prediction = null;
            if (_retentionTimePredictor.HasCalculator)
            {
                var predictedRetentionTime = _retentionTimePredictor.GetPredictedRetentionTime(nodePep);
                TruncateChromatograms(peptideChromDataSets, predictedRetentionTime);
                prediction = new RetentionTimePrediction(predictedRetentionTime,
                    _retentionTimePredictor.TimeWindow);
            }

            var fullScan = _document.Settings.TransitionSettings.FullScan;
            if (prediction == null && fullScan.IsEnabled && fullScan.RetentionTimeFilterType == RetentionTimeFilterType.ms2_ids)
            {
                if (retentionTimes.Length == 0)
                    retentionTimes = _document.Settings.GetUnalignedRetentionTimes(lookupSequence, lookupMods);
                if (retentionTimes.Length > 0)
                {
                    var statTimes = new Statistics(retentionTimes);
                    double predictedRT = statTimes.Median();
                    double window = statTimes.Range() + fullScan.RetentionTimeFilterLength * 2;
                    prediction = new RetentionTimePrediction(predictedRT, window);
                }
            }
            if (prediction == null)
                prediction = new RetentionTimePrediction(null, 0);

            peptideChromDataSets.PredictedRetentionTime = prediction;
        }

        private void TruncateChromatograms(PeptideChromDataSets peptideChromDataSets, double? predictedRetentionTime)
        {
            if (!predictedRetentionTime.HasValue)
            {
                return;
            }
            var fullScan = _document.Settings.TransitionSettings.FullScan;
            if (fullScan.RetentionTimeFilterType != RetentionTimeFilterType.scheduling_windows)
            {
                return;
            }
            if (!_document.Settings.PeptideSettings.Prediction.RetentionTime.IsAutoCalculated)
            {
                return;
            }
            double startTime = predictedRetentionTime.Value - fullScan.RetentionTimeFilterLength;
            double endTime = predictedRetentionTime.Value + fullScan.RetentionTimeFilterLength;
            foreach (var chromDataSet in peptideChromDataSets.DataSets)
            {
                chromDataSet.Truncate(startTime, endTime);
            }
        }

        /// <summary>
        /// Stores the best chromatogram peak for only iRT peptides in the retention time predictor
        /// </summary>
        private void StorePeptideRetentionTime(PeptideChromDataSets peptideChromDataSets)
        {
            var nodePep = peptideChromDataSets.NodePep;
            if (nodePep == null || !string.Equals(nodePep.GlobalStandardType, PeptideDocNode.STANDARD_TYPE_IRT))
                return;

            var dataSet = peptideChromDataSets.DataSets.FirstOrDefault();
            if (dataSet != null && dataSet.MaxPeakIndex >= 0)
            {
                var bestPeak = dataSet.BestChromatogram.Peaks[dataSet.MaxPeakIndex];
                _retentionTimePredictor.AddPeptideTime(nodePep, bestPeak.RetentionTime);
            }
        }

        /// <summary>
        /// Used for retentiont time prediction during import
        /// </summary>
        private class RetentionTimePredictor : IRetentionTimePredictor
        {
            private readonly RetentionScoreCalculatorSpec _calculator;
            private readonly double _timeWindow;
            private RegressionLineElement _conversion;
            private Dictionary<string, double> _dictSeqToTime;

            public RetentionTimePredictor(RetentionTimeRegression rtSettings)
            {
                if (rtSettings != null)
                {
                    _timeWindow = rtSettings.TimeWindow;
                    _calculator = rtSettings.Calculator;
                    if (!rtSettings.IsAutoCalculated)
                        _conversion = rtSettings.Conversion;
                    else
                        _dictSeqToTime = new Dictionary<string, double>();
                }
            }

            public bool HasCalculator
            {
                get { return _calculator != null; }
            }

            public double TimeWindow
            {
                get { return _timeWindow; }
            }

            /// <summary>
            /// If the predictor uses an iRT calculator, then this will store the
            /// measured retention times for the iRT standard peptides.
            /// </summary>
            /// <param name="nodePep">Any peptide</param>
            /// <param name="time">The measured time of its best peak</param>
            public void AddPeptideTime(PeptideDocNode nodePep, double time)
            {
                if (_conversion == null && _dictSeqToTime != null)
                    _dictSeqToTime.Add(nodePep.ModifiedSequence, time);
            }

            /// <summary>
            /// Attempts to get a retention time prediction for a given peptide
            /// </summary>
            public double? GetPredictedRetentionTime(PeptideDocNode nodePep)
            {
                if (_calculator == null)
                    return null;

                if (_conversion != null)
                {
                    double? score = _calculator.ScoreSequence(nodePep.SourceTextId);
                    if (!score.HasValue)
                        return null;
                    return _conversion.GetY(score.Value);
                }

                if (string.Equals(nodePep.GlobalStandardType, PeptideDocNode.STANDARD_TYPE_IRT))
                    return null;

                if (_dictSeqToTime == null)
                    return null;

                // Calculate the conversion, ensuring that this is only done once, even if
                // something below throws unexpectedly.
                var dictSeqToTime = _dictSeqToTime;
                _dictSeqToTime = null;

                try
                {
                    var listTimes = new List<double>();
                    var listScores = new List<double>();
                    foreach (string sequence in _calculator.GetStandardPeptides(dictSeqToTime.Keys))
                    {
                        listTimes.Add(dictSeqToTime[sequence]);
                        listScores.Add(_calculator.ScoreSequence(sequence).Value);
                    }
                    var statTime = new Statistics(listTimes);
                    var statScore = new Statistics(listScores);

                    _conversion = new RegressionLineElement(new RegressionLine(statTime.Slope(statScore), statTime.Intercept(statScore)));
                }
                catch (IncompleteStandardException)
                {
                    return null;
                }

                return GetPredictedRetentionTime(nodePep);
            }

            public bool IsFirstPassPeptide(PeptideDocNode nodePep)
            {
                return string.Equals(nodePep.GlobalStandardType, PeptideDocNode.STANDARD_TYPE_IRT);
            }
        }

        private static IEnumerable<KeyValuePair<PeptidePrecursorMz, ChromDataSet>> GetMatchingGroups(
            ChromDataSet chromDataSet, List<PeptidePrecursorMz> listMzPrecursors, bool singleMatch)
        {
            // Find the first precursor m/z that is greater than or equal to the
            // minimum possible match value
            string modSeq = chromDataSet.ModifiedSequence;
            double minMzMatch = chromDataSet.PrecursorMz - TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
            double maxMzMatch = chromDataSet.PrecursorMz + TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
            var lookup = new PeptidePrecursorMz(null, null, minMzMatch);
            int i = listMzPrecursors.BinarySearch(lookup, MZ_COMPARER);
            if (i < 0)
                i = ~i;
            // Enumerate all possible matching precursor values, collecting the ones
            // with potentially matching product ions
            var listMatchingGroups = new List<Tuple<PeptidePrecursorMz, ChromDataSet, IList<ChromData>>>();
            for (; i < listMzPrecursors.Count && listMzPrecursors[i].PrecursorMz <= maxMzMatch; i++)
            {
                var peptidePrecursorMz = listMzPrecursors[i];
                if (modSeq != null && !string.Equals(modSeq, peptidePrecursorMz.NodePeptide.RawTextId)) // ModifiedSequence for peptides, other id for customIons
                    continue;

                var nodeGroup = peptidePrecursorMz.NodeGroup;
                if (listMatchingGroups.Count > 0)
                {
                    // If the current chromDataSet has already been used, make a copy.
                    chromDataSet = new ChromDataSet(chromDataSet.IsTimeNormalArea,
                        chromDataSet.Chromatograms.Select(c => c.CloneForWrite()).ToArray());
                }
                var groupData = GetMatchingData(nodeGroup, chromDataSet);
                if (groupData != null)
                {
                    listMatchingGroups.Add(new Tuple<PeptidePrecursorMz, ChromDataSet, IList<ChromData>>(peptidePrecursorMz, chromDataSet, groupData));
                }
            }

            // Only use this method of finding potential matching groups, if multiple
            // matches are allowed.  Otherwise, it may discard the right match.
            if (!singleMatch)
                FilterMatchingGroups(listMatchingGroups);

            if (listMatchingGroups.Count == 0)
            {
                // No matches found
                yield return new KeyValuePair<PeptidePrecursorMz, ChromDataSet>(
                    null, chromDataSet);                
            }
            else if (listMatchingGroups.Count == 1)
            {
                // If only one match is found, return product ions for the precursor, whether they
                // all match or not.
                yield return new KeyValuePair<PeptidePrecursorMz, ChromDataSet>(
                    listMatchingGroups[0].Item1, listMatchingGroups[0].Item2);
            }
            else
            {
                // Otherwise, split up the product ions among the precursors they matched
                bool isTimeNormalArea = chromDataSet.IsTimeNormalArea;

                // If this is single matching, as in full-scan filtering, return only nodes
                // matching a single precursor m/z value.  The one closest to the data.
                float? bestMz = null;
                if (singleMatch)
                {
                    double matchMz = chromDataSet.PrecursorMz;
                    foreach (var match in listMatchingGroups)
                    {
                        float currentMz = (float) match.Item1.PrecursorMz;
                        if (!bestMz.HasValue || Math.Abs(matchMz - currentMz) < Math.Abs(matchMz - bestMz.Value))
                            bestMz = currentMz;
                    }
                }

                // Make sure the same chrom data object is not added twice, or two threads
                // may end up processing it at the same time.
                var setChromData = new HashSet<ChromData>();
                foreach (var match in listMatchingGroups.Where(match =>
                    !bestMz.HasValue || bestMz.Value == (float) match.Item1.PrecursorMz))
                {
                    var arrayChromData = match.Item3.ToArray();
                    for (int j = 0; j < arrayChromData.Length; j++)
                    {
                        var chromData = arrayChromData[j];
                        if (setChromData.Contains(chromData))
                            arrayChromData[j] = chromData.CloneForWrite();
                        setChromData.Add(chromData);
                    }
                    var chromDataPart = new ChromDataSet(isTimeNormalArea, arrayChromData);
                    yield return new KeyValuePair<PeptidePrecursorMz, ChromDataSet>(
                        match.Item1, chromDataPart);
                }
            }
        }

        private static void FilterMatchingGroups(
                List<Tuple<PeptidePrecursorMz, ChromDataSet, IList<ChromData>>> listMatchingGroups)
        {
            if (listMatchingGroups.Count < 2)
                return;
            // Filter for only matches that do not match a strict subset of another match.
            // That is, if there is a precursor that matches 4 product ions, and another that
            // matches 2 of those same 4, then we want to discard the one with only 2.
            var listFiltered = new List<Tuple<PeptidePrecursorMz, ChromDataSet, IList<ChromData>>>();
            foreach (var match in listMatchingGroups)
            {
                var subset = match;
                if (!listMatchingGroups.Contains(superset => IsMatchSubSet(subset, superset)))
                    listFiltered.Add(match);
            }
            listMatchingGroups.Clear();
            listMatchingGroups.AddRange(listFiltered);
        }

        private static bool IsMatchSubSet(Tuple<PeptidePrecursorMz, ChromDataSet, IList<ChromData>> subset,
            Tuple<PeptidePrecursorMz, ChromDataSet, IList<ChromData>> superset)
        {
            var subList = subset.Item3;
            var superList = superset.Item3;
            // Can't be a subset, if it doesn't have fewer element in its list
            if (subList.Count >= superList.Count)
                return false;
            foreach (var chromData in subList)
            {
                // Not a subset, if it contains something that is not in the superset list
                if (!superList.Contains(chromData))
                    return false;
            }
            // Must be a subset
            return true;
        }

// ReSharper disable SuggestBaseTypeForParameter
        private static IList<ChromData> GetMatchingData(TransitionGroupDocNode nodeGroup, ChromDataSet chromDataSet)
// ReSharper restore SuggestBaseTypeForParameter
        {
            // Look for potential product ion matches
            var listMatchingData = new List<Tuple<ChromData, TransitionDocNode>>();
            const float tolerance = (float) TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
            foreach (var chromData in chromDataSet.Chromatograms)
            {
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    if (ChromKey.CompareTolerant(chromData.Key.Product,
                            (float) nodeTran.Mz, tolerance) == 0)
                    {
                        listMatchingData.Add(new Tuple<ChromData, TransitionDocNode>(chromData, nodeTran));
                        break;
                    }
                }
            }
            // Only return a match, if at least two product ions match, or the precursor
            // has only a single product ion, and it matches
            int countChildren = nodeGroup.Children.Count;
            if (countChildren == 0 || listMatchingData.Count < Math.Min(2, countChildren))
                return null;
            // Assign all the doc nodes and return this list
            chromDataSet.ClearDataDocNodes();
            var result = new ChromData[listMatchingData.Count];
            for (int i = 0; i < listMatchingData.Count; i++)
            {
                var match = listMatchingData[i];
                match.Item1.DocNode = match.Item2;
                result[i] = match.Item1;
            }
            return result;
        }

        private static readonly MzComparer MZ_COMPARER = new MzComparer();

        private class MzComparer : IComparer<PeptidePrecursorMz>
        {
            public int Compare(PeptidePrecursorMz p1, PeptidePrecursorMz p2)
            {
                return Comparer.Default.Compare(p1.PrecursorMz, p2.PrecursorMz);
            }
        }

        private int StartPercent { get { return _currentFileIndex*100/MSDataFilePaths.Count; } }
        private int EndPercent { get { return (_currentFileIndex + 1)*100/MSDataFilePaths.Count; } }

        private ChromDataProvider CreateChromatogramRecalcProvider(MsDataFileUri dataFilePathRecalc, ChromFileInfo fileInfo)
        {
            return new CachedChromatogramDataProvider(_cacheRecalc,
                                                      _document,
                                                      dataFilePathRecalc,
                                                      fileInfo,
                                                      IsSingleMatchMzFile,
                                                      _status,
                                                      StartPercent,
                                                      EndPercent,
                                                      _loader);
        }

        private bool? IsSingleMatchMzFile
        {
            get
            {
                if (_currentFileInfo == null)
                    return null;
                return ChromCachedFile.IsSingleMatchMzFlags(_currentFileInfo.Flags);
            }
        }

        private ChromDataProvider CreateChromatogramProvider(MsDataFileImpl dataFile, ChromFileInfo fileInfo, bool throwIfSlow)
        {
            return new ChromatogramDataProvider(dataFile, fileInfo, throwIfSlow, _status, StartPercent, EndPercent, _loader);
        }

        private SpectraChromDataProvider CreateSpectraChromProvider(MsDataFileImpl dataFile, ChromFileInfo fileInfo)
        {
            // New WIFF reader library no longer needs this, and mzWiff.exe has been removed from the installation
            // The old WiffFileDataReader messed up the precursor m/z values for targeted
            // spectra.  The mzWiff mzXML converter must be used instead.
//            if (dataFile.IsABFile && !dataFile.IsMzWiffXml)
//            {
                // This will show an error about the import taking 10 hours, which is not really true, if the computer running Skyline does not have Analyst installed
//                throw new LoadingTooSlowlyException(LoadingTooSlowlyException.Solution.mzwiff_conversion, _status,
//                    10*60, 4);
//            }
            // If this is a performance work-around, then make sure the progress indicator
            // does not jump backward perceptibly.
            int startPercent = (_tempFileSubsitute != null ? (StartPercent + EndPercent)/2 : StartPercent);
            return new SpectraChromDataProvider(dataFile, fileInfo, _document, CachePath, _status, startPercent, EndPercent, _loader);
        }

        private const int MAX_CHROM_READ_AHEAD = 20;
        
        private void PostChromDataSet(PeptideChromDataSets chromDataSet, bool complete)
        {
            lock (_chromDataSets)
            {
                // First check for any errors on the writer thread
                if (_writeException != null)
                    throw _writeException;

                // Add new chromatogram data set, if not empty
                if (chromDataSet != null)
                {
                    _chromDataSets.Add(chromDataSet);
                }
                // Update completion status
                _readCompleted = _readCompleted || complete;
                // Notify the writer thread, if necessary
                if (_readCompleted || _chromDataSets.Count > 0)
                {
                    if (_writerStarted)
                    {
                        Monitor.Pulse(_chromDataSets);
                        while (!_writerCompleted && _chromDataSets.Count >= MAX_CHROM_READ_AHEAD)
                            Monitor.Wait(_chromDataSets);
                    }
                    else
                    {
                        // Start the writer thread
                        _writerStarted = true;
                        ActionUtil.RunAsync(() => WriteLoop(_currentFileIndex, false));
                    }

                    // If this is the last read, then wait for the
                    // writer to complete, in case of an exception.
                    if (_readCompleted)
                    {
                        if (!_writerCompleted)
                        {
                            // Wait while work is being accomplished by the writer, but not
                            // if it is hung.
                            bool completed;
                            int countSets;
                            do
                            {
                                countSets = _chromDataSets.Count;
                                // Wait 10 seconds for some work to complete.  In debug mode,
                                // a shorter time may not be enough to load DLLs necessary
                                // for the first iteration.
                                completed = Monitor.Wait(_chromDataSets, 10*1000);
                            }
                            while (!completed && countSets != _chromDataSets.Count);

                            // Try calling the write loop directly on this thread.
                            if (!completed && _chromDataSets.Count > 0)
                                WriteLoop(_currentFileIndex, false);                                
                        }

                        if (_writeException != null)
                            throw _writeException;
                    }
                }
            }
        }

        /// <summary>
        /// List of write threads for debugging write thread leaks.
        /// </summary>
        private static readonly List<Thread> WRITE_THREADS = new List<Thread>() ;

        private void WriteLoop(int currentFileIndex, bool primeThread)
        {
            // Called in a new thread
            LocalizationHelper.InitThread();
            // HACK: This is a huge hack, for a temporary work-around to the problem
            // of Reader_Waters (or DACServer.dll) killing the ThreadPool.  WriteLoop
            // is called once as a no-op to force the thread it will use during
            // processing into existence before the file is opened.
            if (primeThread)
                return;

            try
            {
                for (;;)
                {
                    PeptideChromDataSets chromDataSetNext;
                    lock (_chromDataSets)
                    {
                        try
                        {
                            if (WRITE_THREADS.Count > 0 && !_readCompleted)
// ReSharper disable LocalizableElement
                                Console.WriteLine("Existing write threads: {0}", string.Join(", ", WRITE_THREADS.Select(t => t.ManagedThreadId))); // Not L10N: Debugging purposes
// ReSharper restore LocalizableElement
                            WRITE_THREADS.Add(Thread.CurrentThread);
                            while (_writerStarted && !_readCompleted && _chromDataSets.Count == 0)
                            {
                                try
                                {
                                    _writerIsWaitingForChromDataSets = true;
                                    Monitor.Wait(_chromDataSets);
                                }
                                finally
                                {
                                    _writerIsWaitingForChromDataSets = false;
                                }
                            }

                            if (!_writerStarted)
                                return;

                            // If reading is complete, and there are no more sets to process,
                            // begin next file.
                            if (_readCompleted && _chromDataSets.Count == 0)
                            {
                                // Once inside here, the thread is going to exit.
                                _writerStarted = false;

                                // Write loop completion may have already been executed
                                if (_currentFileIndex != currentFileIndex)
                                    return;

                                _listCachedFiles.Add(new ChromCachedFile(MSDataFilePaths[_currentFileIndex],
                                                                         _currentFileInfo.Flags,
                                                                         _currentFileInfo.LastWriteTime,
                                                                         _currentFileInfo.StartTime,
                                                                         LoadingStatus.Transitions.MaxRetentionTime,
                                                                         LoadingStatus.Transitions.MaxIntensity,
                                                                         _currentFileInfo.SizeScanIds,
                                                                         _currentFileInfo.LocationScanIds,
                                                                         _currentFileInfo.InstrumentInfoList));
                                _currentFileIndex++;

                                // Allow the reader thread to exit
                                Monitor.Pulse(_chromDataSets);

                                ActionUtil.RunAsync(BuildNextFile);
                                return;
                            }

                            chromDataSetNext = _chromDataSets[0];
                            _chromDataSets.RemoveAt(0);

                            // Wake up read thread, if it is waiting because it is too far ahead
                            Monitor.Pulse(_chromDataSets);
                        }
                        finally
                        {
                            WRITE_THREADS.Remove(Thread.CurrentThread);
                        }
                    }

                    GetPeptideRetentionTimes(chromDataSetNext);
                    chromDataSetNext.PickChromatogramPeaks();
                    StorePeptideRetentionTime(chromDataSetNext);

                    var dictScoresToIndex = new Dictionary<IList<float>, int>();
                    foreach (var chromDataSet in chromDataSetNext.DataSets)
                    {
                        if (_fs.Stream == null)
                            throw new InvalidDataException(
                                Resources.ChromCacheBuilder_WriteLoop_Failure_writing_cache_file);

                        long location = _fs.Stream.Position;

                        float[] times = chromDataSet.Times;
                        float[][] intensities = chromDataSet.Intensities;
                        // Assign mass errors only it the cache is allowed to store them
                        short[][] massErrors = null;
                        if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_4)
                            massErrors = chromDataSet.MassErrors10X;
                        int[][] scanIds = chromDataSet.ScanIds;
                        // Write the raw chromatogram points
                        byte[] points = ChromatogramCache.TimeIntensitiesToBytes(times, intensities, massErrors, scanIds);
                        // Compress the data (can be huge for AB data with lots of zeros)
                        byte[] pointsCompressed = points.Compress(3);
                        int lenCompressed = pointsCompressed.Length;
                        _fs.Stream.Write(pointsCompressed, 0, lenCompressed);

                        // Use existing scores, if they have already been added
                        int scoresIndex;
                        var startPeak = chromDataSet.PeakSets.FirstOrDefault();
                        if (startPeak == null || startPeak.DetailScores == null)
                        {
                            // CONSIDER: Should unscored peak sets be kept?
                            scoresIndex = -1;
                        }
                        else if (!dictScoresToIndex.TryGetValue(startPeak.DetailScores, out scoresIndex))
                        {
                            scoresIndex = _listScores.Count;
                            dictScoresToIndex.Add(startPeak.DetailScores, scoresIndex);

                            // Add scores to the scores list
                            foreach (var peakSet in chromDataSet.PeakSets)
                                _listScores.AddRange(peakSet.DetailScores);
                        }

                        // Add to header list
                        ChromGroupHeaderInfo5.FlagValues flags = 0;
                        if (massErrors != null)
                            flags |= ChromGroupHeaderInfo5.FlagValues.has_mass_errors;
                        if (chromDataSet.HasCalculatedMzs)
                            flags |= ChromGroupHeaderInfo5.FlagValues.has_calculated_mzs;
                        if (chromDataSet.Extractor == ChromExtractor.base_peak)
                            flags |= ChromGroupHeaderInfo5.FlagValues.extracted_base_peak;
                        if (scanIds != null)
                        {
                            if (scanIds[(int)ChromSource.ms1] != null)
                                flags |= ChromGroupHeaderInfo5.FlagValues.has_ms1_scan_ids;
                            if (scanIds[(int)ChromSource.fragment] != null)
                                flags |= ChromGroupHeaderInfo5.FlagValues.has_frag_scan_ids;
                            if (scanIds[(int)ChromSource.sim] != null)
                                flags |= ChromGroupHeaderInfo5.FlagValues.has_sim_scan_ids;
                        }
                        var header = new ChromGroupHeaderInfo5(chromDataSet.PrecursorMz,
                                                               currentFileIndex,
                                                               chromDataSet.Count,
                                                               _listTransitions.Count,
                                                               chromDataSet.CountPeaks,
                                                               _peakCount,
                                                               scoresIndex,
                                                               chromDataSet.MaxPeakIndex,
                                                               times.Length,
                                                               lenCompressed,
                                                               location,
                                                               flags,
                                                               chromDataSet.StatusId,
                                                               chromDataSet.StatusRank);

                        header.CalcTextIdIndex(chromDataSet.ModifiedSequence, _dictSequenceToByteIndex, _listTextIdBytes);

                        int? transitionPeakCount = null;
                        foreach (var chromData in chromDataSet.Chromatograms)
                        {
                            var chromTran = new ChromTransition(chromData.Key.Product,
                                                                chromData.Key.ExtractionWidth,
                                                                chromData.Key.IonMobilityValue,
                                                                chromData.Key.IonMobilityExtractionWidth,
                                                                chromData.Key.Source);
                            _listTransitions.Add(chromTran);

                            // Make sure all transitions have the same number of peaks, as this is a cache requirement
                            if (!transitionPeakCount.HasValue)
                                transitionPeakCount = chromData.Peaks.Count;
                            else if (transitionPeakCount.Value != chromData.Peaks.Count)
                            {
                                throw new InvalidDataException(
                                    string.Format(
                                        Resources
                                            .ChromCacheBuilder_WriteLoop_Transitions_of_the_same_precursor_found_with_different_peak_counts__0__and__1__,
                                        transitionPeakCount, chromData.Peaks.Count));
                            }

                            // Add to peaks list
                            _peakCount += chromData.Peaks.Count;
                            if (ChromatogramCache.FORMAT_VERSION_CACHE <= ChromatogramCache.FORMAT_VERSION_CACHE_4)
                            {
                                // Zero out the mass error bits in the peaks to make sure mass errors
                                // do not show up until after they are officially released
                                for (int i = 0; i < chromData.Peaks.Count; i++)
                                    chromData.Peaks[i] = chromData.Peaks[i].RemoveMassError();
                            }
                            ChromPeak.WriteArray(_fsPeaks.FileStream.SafeFileHandle, chromData.Peaks.ToArray());
                        }

                        _listGroups.Add(header);
                    }
                }
            }
            catch (Exception x)
            {
                lock (_chromDataSets)
                {
                    _writerCompleted = true;
                    _writeException = x;
                    // Make sure the reader thread can exit
                    Monitor.Pulse(_chromDataSets);
                }
            }
            finally
            {
                lock (_chromDataSets)
                {
                    if (!_writerCompleted)
                    {
                        _writerCompleted = true;
                        Monitor.Pulse(_chromDataSets);
                    }
                }
            }
        }
    }

    internal sealed class FileBuildInfo
    {
        public FileBuildInfo(MsDataFileImpl file)
            : this(file.RunStartTime,
                   ChromCachedFile.GetLastWriteTime(new MsDataFilePath(file.FilePath)),
                   file.GetInstrumentConfigInfoList(),
                   null)
        {
        }

        public FileBuildInfo(DateTime? startTime,
            DateTime lastWriteTime,
            IEnumerable<MsInstrumentConfigInfo> instrumentInfoList,
            bool? isSingleMatchMz)
        {
            StartTime = startTime;
            LastWriteTime = lastWriteTime;
            InstrumentInfoList = instrumentInfoList;
            IsSingleMatchMz = isSingleMatchMz;
        }

        public DateTime? StartTime { get; private set; }
        public DateTime LastWriteTime { get; private set; }
        public IEnumerable<MsInstrumentConfigInfo> InstrumentInfoList { get; private set; }
        public ChromCachedFile.FlagValues Flags { get; private set; }

        public bool? IsSingleMatchMz
        {
            get { return ChromCachedFile.IsSingleMatchMzFlags(Flags); }
            set
            {
                Flags = 0;
                if (value.HasValue)
                {
                    Flags |= ChromCachedFile.FlagValues.single_match_mz_known;
                    if (value.Value)
                        Flags |= ChromCachedFile.FlagValues.single_match_mz;
                }
            }
        }

        public int SizeScanIds { get; set; }
        public long LocationScanIds { get; set; }
    }
}