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
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class ChromCacheBuilder : ChromCacheWriter
    {
        /// <summary>
        /// This flag controls whether the peaks for iRT peptides get re-picked after the
        /// iRT predictor is trained. This is currently not ready for prime-time, but
        /// it tools some effort to get working at all. So, keeping the code around to
        /// make it possible until we decide when/if it might be useful.
        /// </summary>
        public static bool REPICK_IRTS_AFTER_TRAINING => false;

        // Lock on this to access these variables
        private readonly SrmDocument _document;
        private FileBuildInfo _currentFileInfo;

        private readonly ChromatogramCache _cacheRecalc;
        
        // Lock on _chromDataSets to access these variables
        private QueueWorker<PeptideChromDataSets> _chromDataSets;
        private readonly object _writeLock = new object();

        // Accessed only on the write thread
        private readonly RetentionTimePredictor _retentionTimePredictor;
        private readonly Dictionary<Target, int> _dictSequenceToByteIndex = new Dictionary<Target, int>();

        private readonly int SCORING_THREADS = ParallelEx.SINGLE_THREADED ? 1 : 4;
        //private static readonly Log LOG = new Log<ChromCacheBuilder>();

        public ChromCacheBuilder(SrmDocument document, ChromatogramCache cacheRecalc,
            string cachePath, MsDataFileUri msDataFilePath, ILoadMonitor loader, IProgressStatus status,
            Action<ChromatogramCache, IProgressStatus> complete)
            : base(cachePath, loader, status, complete)
        {
            _document = document;
            _cacheRecalc = cacheRecalc;
            MSDataFilePath = msDataFilePath;

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
                // Avoid storing reference dependent scores in large label-free documents
                bool includeReference = document.Settings.PeptideSettings.Modifications.HasHeavyImplicitModifications;
                calcEnum = PeakFeatureCalculator.Calculators
                    .Where(c => c is DetailedPeakFeatureCalculator && (includeReference || !c.IsReferenceScore));
            }
            DetailedPeakFeatureCalculators = calcEnum.Cast<DetailedPeakFeatureCalculator>().ToArray();
            _listScoreTypes.AddRange(DetailedPeakFeatureCalculators.Select(c => c.GetType()));

            string basename = MSDataFilePath.GetFileNameWithoutExtension();
            var fileAlignments = _document.Settings.DocumentRetentionTimes.FileAlignments.Find(basename);
            FileAlignmentIndices = new RetentionTimeAlignmentIndices(fileAlignments);
        }

        private void ScoreWriteChromDataSets(PeptideChromDataSets chromDataSets, int threadIndex)
        {
            // Score peaks.
            GetPeptideRetentionTimes(chromDataSets);
            chromDataSets.PickChromatogramPeaks();
            StorePeptideRetentionTime(chromDataSets);

            if (chromDataSets.IsDelayedWrite)
                return;

            // Only one writer at a time.
            lock (_writeLock)
            {
                WriteChromDataSets(chromDataSets);
            }
        }

        private MsDataFileUri MSDataFilePath { get; set; }
        private RetentionTimeAlignmentIndices FileAlignmentIndices { get; set; }

        private IList<DetailedPeakFeatureCalculator> DetailedPeakFeatureCalculators { get; set; }

        private bool IsTimeNormalArea
        {
            get
            {
                return !_document.Settings.HasResults ||
                       _document.Settings.MeasuredResults.IsTimeNormalArea;
            }
        }

        public void BuildCache()
        {
            //LOG.InfoFormat(@"Start file import: {0}", MSDataFilePath.GetFileName());

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
            ChromFileInfo fileInfo = _document.Settings.MeasuredResults.GetChromFileInfo(MSDataFilePath);
            Assume.IsNotNull(fileInfo);
            string dataFilePathPart;
            MsDataFileUri dataFilePathRecalc = GetRecalcDataFilePath(MSDataFilePath, out dataFilePathPart);

            string format = dataFilePathRecalc == null
                                ? Resources.ChromCacheBuilder_BuildNextFileInner_Importing__0__
                                : Resources.ChromCacheBuilder_BuildNextFileInner_Recalculating_scores_for__0_;
            string message = string.Format(format, MSDataFilePath.GetSampleName() ?? MSDataFilePath.GetFileName());
            _status = _status
                .ChangeMessage(message)
                .ChangePercentComplete(0);
            ChromatogramLoadingStatus.TransitionData allChromData = null;
            var loadingStatus = _status as ChromatogramLoadingStatus;
            if (loadingStatus != null)
            {
                allChromData = loadingStatus.Transitions;
                allChromData.MaxIntensity = 0;
                allChromData.MaxRetentionTime = 0;
                allChromData.MaxRetentionTimeKnown = false;
                _status = loadingStatus
                    .ChangeImporting(true)
                    .ChangeFilePath(MSDataFilePath);
            }
            _loader.UpdateProgress(_status);

            try
            {
                var dataFilePath = MSDataFilePath;
                var msDataFilePath = MSDataFilePath as MsDataFilePath;
                if (msDataFilePath != null)
                {
                    dataFilePath = dataFilePathRecalc ??
                                   ChromatogramSet.GetExistingDataFilePath(CachePath, msDataFilePath,
                                       out dataFilePathPart);
                    if (dataFilePath == null)
                        throw new FileNotFoundException(
                            string.Format(Resources.ChromCacheBuilder_BuildNextFileInner_The_file__0__does_not_exist,
                                dataFilePathPart), dataFilePathPart);
                }
                MSDataFilePath = dataFilePath;

                // Once a ChromDataProvider is created, it owns disposing of the MSDataFileImpl.
                MsDataFileImpl inFile = null;
                ChromDataProvider provider = null;
                try
                {
                    if (dataFilePathRecalc == null)
                    {
                        // Always use SIM as spectra, if any full-scan chromatogram extraction is enabled
                        var fullScan = _document.Settings.TransitionSettings.FullScan;
                        bool enableSimSpectrum = fullScan.IsEnabled; // And chromatogram extraction requires SIM as spectra
                        bool preferOnlyMs1 = fullScan.IsEnabledMs && !fullScan.IsEnabledMsMs; // If we don't want MS2, ask reader to totally skip it (not guaranteed)
                        bool centroidMs1 = fullScan.IsCentroidedMs;
                        bool centroidMs2 = fullScan.IsCentroidedMsMs;
                        const bool ignoreZeroIntensityPoints = true; // Omit zero intensity points during extraction
                        inFile = MSDataFilePath.OpenMsDataFile(enableSimSpectrum, preferOnlyMs1,
                            centroidMs1, centroidMs2, ignoreZeroIntensityPoints);
                    }

                    // Check for cancellation
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
                            _currentFileInfo = new FileBuildInfo(fileInfo.RunStartTime,
                                fileInfo.FileWriteTime ?? DateTime.Now);
                        }
                        else
                        {
                            _currentFileInfo = new FileBuildInfo(MSDataFilePath, inFile);
                        }
                    }

                    // Read and write the mass spec data)
                    if (dataFilePathRecalc != null)
                    {
                        provider = CreateChromatogramRecalcProvider(dataFilePathRecalc, fileInfo);
                        if (allChromData != null)
                        {
                            allChromData.MaxIntensity = (float) (provider.MaxIntensity ?? 0);
                            allChromData.MaxRetentionTime = (float) (provider.MaxRetentionTime ?? 0);
                            allChromData.MaxRetentionTimeKnown = provider.MaxRetentionTime.HasValue;
                        }
                    }
                    else if (ChromatogramDataProvider.HasChromatogramData(inFile))
                    {
                        provider = CreateChromatogramProvider(inFile, fileInfo);
                    }
                    else if (SpectraChromDataProvider.HasSpectrumData(inFile))
                    {
                        provider = CreateSpectraChromProvider(inFile, fileInfo);
                    }
                    else
                    {
                        throw new InvalidDataException(String.Format(Resources.ChromCacheBuilder_BuildNextFileInner_The_sample__0__contains_no_usable_data,
                                dataFilePath.GetSampleOrFileName()));
                    }

                    _currentFileInfo.IsSingleMatchMz = provider.IsSingleMzMatch;
                    _currentFileInfo.HasMidasSpectra = provider.HasMidasSpectra;

                    // Start multiple threads to perform peak scoring.
                    _chromDataSets = new QueueWorker<PeptideChromDataSets>(null, ScoreWriteChromDataSets);
                    _chromDataSets.RunAsync(SCORING_THREADS, @"Scoring/writing", MAX_CHROM_READ_AHEAD);

                    Read(provider);

                    _status = provider.Status;

                    if (_status.IsCanceled)
                        ExitRead(null);

                    if ((inFile != null) && (inFile.GetLog() != null)) // in case perf logging is enabled
                        DebugLog.Info(inFile.GetLog());

                    CheckForProviderErrors(provider);
                }
                finally
                {
                    if (_chromDataSets != null)
                        _chromDataSets.Dispose();
                    if (provider != null)
                        provider.Dispose();
                    else if (inFile != null)
                        inFile.Dispose();
                }

                ExitRead(null);
            }
            catch (LoadCanceledException x)
            {
                _status = x.Status;
                ExitRead(null);
            }
            catch (MissingDataException x)
            {
                ExitRead(new MissingDataException(x.MessageFormat, MSDataFilePath, x));
            }
            catch (Exception x)
            {
                // Because exceptions frequently get wrapped now
                var canceledX = x.InnerException as LoadCanceledException;
                var missingDataX = x.InnerException as MissingDataException;
                if (canceledX != null)
                {
                    _status = canceledX.Status;
                    ExitRead(null);
                }
                else if (missingDataX != null)
                {
                    ExitRead(new MissingDataException(missingDataX.MessageFormat, MSDataFilePath, missingDataX));
                }
                else if (x.Message.Contains(@"PeakDetector::NoVendorPeakPickingException"))
                {
                    ExitRead(new NoCentroidedDataException(MSDataFilePath, x));
                }
                else
                {
                    // Add a more generic message to an exception message that may
                    // be fairly unintelligible to the user, but keep the exception
                    // message, because ProteoWizard "Unsupported file format" comes
                    // in on this channel.
                    x = x as ChromCacheBuildException ?? new ChromCacheBuildException(MSDataFilePath, x);
                    ExitRead(x);
                }
            }
        }

        private void CheckForProviderErrors(ChromDataProvider provider)
        {
            // Check for attempts to load negative data into an all-postive document, and vice versa
            if (_document.MoleculeTransitions.Any())
            {
                if (_document.MoleculeTransitions.All(t => !t.Transition.IsNegative()) &&
                    provider.SourceHasNegativePolarityData && !provider.SourceHasPositivePolarityData)
                {
                    throw new InvalidDataException(
                        Resources.ChromCacheBuilder_BuildCache_This_document_contains_only_positive_ion_mode_transitions__and_the_imported_file_contains_only_negative_ion_mode_data_so_nothing_can_be_loaded___Negative_ion_mode_transitions_need_to_have_negative_charge_values_);
                }
                if (_document.MoleculeTransitions.All(t => t.Transition.IsNegative()) &&
                    !provider.SourceHasNegativePolarityData && provider.SourceHasPositivePolarityData)
                {
                    throw new InvalidDataException(
                        Resources.ChromCacheBuilder_BuildCache_This_document_contains_only_negative_ion_mode_transitions__and_the_imported_file_contains_only_positive_ion_mode_data_so_nothing_can_be_loaded_);
                }
            }
        }

        private MsDataFileUri GetRecalcDataFilePath(MsDataFileUri dataFilePath, out string dataFilePathPart)
        {
            if (_cacheRecalc == null || !_cacheRecalc.CachedFilePaths.Contains(dataFilePath.GetLocation()))
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
            return new FileBuildInfo(cachedFile);
        }

        private void ExitRead(Exception x)
        {
            Complete(x);
        }

        private void Read(ChromDataProvider provider)
        {
            var listMzPrecursors = new List<PeptidePrecursorMz>(Precursors);
            listMzPrecursors.Sort((p1, p2) => p1.PrecursorMz.CompareTo(p2.PrecursorMz));

            var setInternalStandards = new HashSet<IsotopeLabelType>(
                _document.Settings.PeptideSettings.Modifications.InternalStandardTypes);

            var listChromData = CalcPeptideChromDataSets(provider, listMzPrecursors, setInternalStandards);
            for (int i = 0; i < listChromData.Count; i++)
            {
                listChromData[i].IndexInFile = i;
            }

            var listProviderIds = new List<IList<int>>(listChromData.Where(IsFirstPassPeptide).Select(c => c.ProviderIds.ToArray()));
            var doSecondPass = true;
            listProviderIds.AddRange(listChromData.Where(c => !IsFirstPassPeptide(c)).Select(c => c.ProviderIds.ToArray()));

            provider.SetRequestOrder(listProviderIds);

            // Create IRT prediction if we don't already have it.
            var predict = _document.Settings.PeptideSettings.Prediction;
            bool doFirstPass = predict.RetentionTime != null && predict.RetentionTime.IsAutoCalculated;
            if (doFirstPass)
            {
                if (!CreateRetentionTimeEquation(provider, listChromData))
                {
                    var transitionFullScan = _document.Settings.TransitionSettings.FullScan;
                    if (transitionFullScan.IsEnabled && transitionFullScan.RetentionTimeFilterType ==
                        RetentionTimeFilterType.scheduling_windows)
                    {
                        doSecondPass = false;
                    }
                }
                if (!doSecondPass && listChromData.Any(data => null != data && !IsFirstPassPeptide(data)))
                {
                    _status = _status.ChangeWarningMessage(
                        Resources.ChromCacheBuilder_Read_Unable_to_finish_importing_chromatograms_because_the_retention_time_predictor_linear_regression_failed_);
                    _loader.UpdateProgress(_status);
                }
                // Let the provider know that it is now safe to use retention time prediction
                if (provider.CompleteFirstPass() && doSecondPass)
                {
                    // Then refresh the chrom data list if indicated by provider, as it should now contain more than first-pass peptides
                    listChromData = CalcPeptideChromDataSets(provider, listMzPrecursors, setInternalStandards);
                    listProviderIds = new List<IList<int>>(listChromData.Select(c => c.ProviderIds.ToArray()));
                    provider.SetRequestOrder(listProviderIds);
                }
            }

            if (doSecondPass)
            {
                // Load scan data.
                for (int i = 0; i < listChromData.Count; i++)
                {
                    var pepChromData = listChromData[i];
                    if (pepChromData == null)
                        continue;
                    if (!doFirstPass || !IsFirstPassPeptide(pepChromData))
                    {
                        if (pepChromData.Load(provider))
                            PostChromDataSet(pepChromData);
                    }

                    // Release the reference to the chromatogram data set so that
                    // it can be garbage collected after it has been written
                    listChromData[i] = null;
                }
            }

            // Write scan ids
            var scanIdBytes = provider.MSDataFileScanIdBytes;
            if (scanIdBytes.Length > 0)
            {
                _currentFileInfo.LocationScanIds = _fsScans.Stream.Position;
                _currentFileInfo.SizeScanIds = scanIdBytes.Length;
                _fsScans.Stream.Write(scanIdBytes, 0, scanIdBytes.Length);
            }

            // Release all provider memory before waiting for write completion
            provider.ReleaseMemory();

            //LOG.InfoFormat(@"Scans read: {0}", MSDataFilePath.GetFileName());
            _chromDataSets.DoneAdding(true);
            //LOG.InfoFormat(@"Peak scoring/writing finished: {0}", MSDataFilePath.GetFileName());
            if (_chromDataSets.Exception != null)
            {
                throw new ChromCacheBuildException(MSDataFilePath, _chromDataSets.Exception);
            }

            _listCachedFiles.Add(new ChromCachedFile(MSDataFilePath,
                                     _currentFileInfo.Flags,
                                     _currentFileInfo.LastWriteTime,
                                     _currentFileInfo.StartTime,
                                     DateTime.UtcNow,
                                     (float) (provider.MaxRetentionTime ?? 0),
                                     (float) (provider.MaxIntensity ?? 0),
                                     _currentFileInfo.SizeScanIds,
                                     _currentFileInfo.LocationScanIds,
                                     (float?) provider.TicArea,
                                     provider.IonMobilityUnits,
                                     _currentFileInfo.SampleId,
                                     _currentFileInfo.SerialNumber,
                                     _currentFileInfo.InstrumentInfoList));
        }

        private bool CreateRetentionTimeEquation(ChromDataProvider provider,
            IList<PeptideChromDataSets> listChromData)
        {
            // Train once without writing to disk, re-score and write the peaks using the trained
            // predictor, and then retrain the predictor for all subsequent use
            return CreateRetentionTimeEquationSub(provider, listChromData, true) &&
                   CreateRetentionTimeEquationSub(provider, listChromData, false);
        }

        private bool CreateRetentionTimeEquationSub(ChromDataProvider provider, IList<PeptideChromDataSets> listChromData, bool delayWrite)
        {
            for (int i = 0; i < listChromData.Count; i++)
            {
                var pepChromData = listChromData[i];
                if (IsFirstPassPeptide(pepChromData))
                {
                    pepChromData.IsDelayedWrite = delayWrite && REPICK_IRTS_AFTER_TRAINING;

                    if (delayWrite)
                    {
                        if (pepChromData.Load(provider))
                            PostChromDataSet(pepChromData);
                    }
                    else
                    {
                        if (REPICK_IRTS_AFTER_TRAINING)
                        {
                            // Remove original peak choices before re-scoring
                            foreach (var chromDataSet in pepChromData.DataSets)
                                chromDataSet.TruncatePeakSets(0);

                            PostChromDataSet(pepChromData);
                        }

                        // Release the reference to the chromatogram data set so that
                        // it can be garbage collected after it has been written
                        listChromData[i] = null;
                    }
                }
            }

            // All threads must complete scoring before calculating the regression
            _chromDataSets.Wait();

            return _retentionTimePredictor.CreateConversion();
        }

        private bool IsFirstPassPeptide(PeptideChromDataSets pepChromData)
        {
            return pepChromData.NodePep != null && _retentionTimePredictor.IsFirstPassPeptide(pepChromData.NodePep);
        }

        private List<PeptideChromDataSets> CalcPeptideChromDataSets(ChromDataProvider provider,
            List<PeptidePrecursorMz> listMzPrecursors, HashSet<IsotopeLabelType> setInternalStandards)
        {
            double tolerance = _document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            bool singleMatch = provider.IsSingleMzMatch;

            var dictPeptideChromData = new Dictionary<PeptideSequenceModKey, PeptideChromDataSets>();
            var listChromData = new List<PeptideChromDataSets>();

            foreach (var chromDataSet in GetChromDataSets(provider))
            {
                if (chromDataSet == null)
                    continue;
                foreach (var matchingGroup in GetMatchingGroups(chromDataSet, listMzPrecursors, singleMatch, tolerance))
                {
                    var peptidePercursor = matchingGroup.Key;
                    var chromDataSetMatch = matchingGroup.Value;
                    chromDataSetMatch.IsStandard = peptidePercursor != null &&
                                                   setInternalStandards.Contains(
                                                       peptidePercursor.NodeGroup.TransitionGroup.LabelType);

                    AddChromDataSet(provider.IsProcessedScans,
                        chromDataSetMatch,
                        peptidePercursor,
                        dictPeptideChromData,
                        listChromData,
                        provider.FileInfo);
                }
            }

            listChromData.AddRange(dictPeptideChromData.Values);
            listChromData.Sort(CompareMaxRetentionTime);

            // Avoid holding onto chromatogram data sets for entire read
            dictPeptideChromData.Clear();
            return listChromData;
        }

        /// <summary>
        /// Compare data sets by maximum retention time. If a dataset does not have a maximum
        /// retention time, that is treated as infinity.
        /// </summary>
        private int CompareMaxRetentionTime(PeptideChromDataSets p1, PeptideChromDataSets p2)
        {
            var time1 = p1.FirstKey.OptionalMaxTime;
            var time2 = p2.FirstKey.OptionalMaxTime;
            if (time1.HasValue)
            {
                if (time2.HasValue)
                {
                    return time1.Value.CompareTo(time2.Value);
                }
                return -1;
            }
            if (time2.HasValue)
            {
                return 1;
            }
            return 0;
        }

        private sealed class PeptidePrecursorMz
        {
            public PeptidePrecursorMz(PeptideDocNode nodePeptide,
                                      TransitionGroupDocNode nodeGroup,
                                      SignedMz precursorMz)
            {
                NodePeptide = nodePeptide;
                NodeGroup = nodeGroup;
                PrecursorMz = precursorMz;
            }

            public PeptideDocNode NodePeptide { get; private set; }
            public TransitionGroupDocNode NodeGroup { get; private set; }
            public SignedMz PrecursorMz { get; private set; }
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
            return GetChromDataSets(_document.Settings.TransitionSettings.FullScan.AcquisitionMethod, provider, IsTimeNormalArea);
        }

        public static IEnumerable<ChromDataSet> GetChromDataSets(FullScanAcquisitionMethod fullScanAcquisitionMethod, ChromDataProvider provider, bool isTimeNormalArea)
        {
            ChromKey lastKey = ChromKey.EMPTY;
            ChromDataSet chromDataSet = null;
            foreach (var keyIndex in provider.ChromIds.OrderBy(k => k))
            {
                var key = keyIndex.Key;
                var chromData = new ChromData(key, keyIndex.ProviderId);

                if (chromDataSet != null && key.ComparePrecursors(lastKey) == 0)
                {
                    chromDataSet.Add(chromData);
                }
                else
                {
                    if (chromDataSet != null)
                        yield return chromDataSet;

                    chromDataSet = new ChromDataSet(isTimeNormalArea, fullScanAcquisitionMethod, chromData);
                }
                lastKey = key;
            }
            // Caution: for SRM data, we may have just grouped chromatograms that will eventually
            // prove to have discontiguous RT spans once we load them and have time data.
            yield return chromDataSet;
        }
//        public static IEnumerable<ChromDataSet> GetChromDataSets(ChromDataProvider provider, bool isTimeNormalArea)
//        {
//            var listKeyIndex = new List<ChromKeyProviderIdPair>(provider.ChromIds);
//            listKeyIndex.Sort();
//           
//            List<ChromDataSet> chromDataSets = new List<ChromDataSet>();
//            ChromKey lastKey = ChromKey.EMPTY;
//            ChromDataSet chromDataSet = null;
//
//            foreach (var keyIndex in listKeyIndex)
//            {
//                var key = keyIndex.Key;
//                var chromData = new ChromData(key, keyIndex.Value);
//
//                if (chromDataSet != null && key.ComparePrecursors(lastKey) == 0)
//                {
//                    chromDataSet.Add(chromData);
//                }
//                else
//                {
//                    if (chromDataSet != null)
//                        chromDataSets.Add(chromDataSet);
//
//                    chromDataSet = new ChromDataSet(isTimeNormalArea, chromData);
//                }
//                lastKey = key;
//            }
//
//            Assume.IsNotNull(chromDataSet);
//            chromDataSets.Add(chromDataSet);
//            return chromDataSets;
//        }

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

            // Otherwise, add it to the dictionary by its PeptideSequenceModKey to make
            // sure precursors are grouped by peptide
            Assume.AreEqual(peptidePrecursorMz.PrecursorMz.IsNegative, chromDataSet.PrecursorMz.IsNegative);
            var nodePep = peptidePrecursorMz.NodePeptide;
            var key = nodePep.SequenceKey;
            if (!dictPeptideChromData.TryGetValue(key, out pepDataSets))
            {
                pepDataSets = new PeptideChromDataSets(nodePep,
                    _document, fileInfo, DetailedPeakFeatureCalculators, isProcessedScans);
                dictPeptideChromData.Add(key, pepDataSets);
            }

            if (peptidePrecursorMz.NodeGroup != null)
            {
                chromDataSet.NodeGroups = ImmutableList.ValueOf(chromDataSet.NodeGroups.Append(Tuple.Create(peptidePrecursorMz.NodePeptide, peptidePrecursorMz.NodeGroup)));
            }
            pepDataSets.Add(nodePep, chromDataSet);
        }

        private void GetPeptideRetentionTimes(PeptideChromDataSets peptideChromDataSets)
        {
            var nodePep = peptideChromDataSets.NodePep;
            if (nodePep == null)
                return;
            if (nodePep.ExplicitRetentionTime != null)
            {
                // Explicitly set retention time overrides any predictor
                peptideChromDataSets.PredictedRetentionTime =
                    new RetentionTimePrediction(nodePep.ExplicitRetentionTime.RetentionTime, nodePep.ExplicitRetentionTime.RetentionTimeWindow ?? 0);
                peptideChromDataSets.RetentionTimes = new[] { nodePep.ExplicitRetentionTime.RetentionTime }; // Feed this information to the peak picker
                return;
            }
            // N.B. No retention time prediction for small molecules (yet?), but may be able to pull from libraries
            var lookupSequence = nodePep.SourceUnmodifiedTarget;
            var lookupMods = nodePep.SourceExplicitMods;
            double[] retentionTimes = _document.Settings.GetRetentionTimes(MSDataFilePath, lookupSequence, lookupMods);
            bool isAlignedTimes = (retentionTimes.Length == 0);
            if (isAlignedTimes)
            {
                retentionTimes = _document.Settings.GetAlignedRetentionTimes(FileAlignmentIndices,
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
            if (!fullScan.IsEnabled)
            {
                return;
            }
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
            if (nodePep == null || !Equals(nodePep.GlobalStandardType, PeptideDocNode.STANDARD_TYPE_IRT))
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
        public class RetentionTimePredictor : IRetentionTimePredictor
        {
            private readonly RetentionScoreCalculatorSpec _calculator;
            private readonly double _timeWindow;
            private RegressionLineElement _conversion;
            private readonly Dictionary<Target, double> _dictSeqToTime;

            public RetentionTimePredictor(RetentionTimeRegression rtSettings)
            {
                if (rtSettings != null)
                {
                    _timeWindow = rtSettings.TimeWindow;
                    _calculator = rtSettings.Calculator;
                    if (!rtSettings.IsAutoCalculated)
                        _conversion = rtSettings.Conversion as RegressionLineElement;
                    else
                        _dictSeqToTime = new Dictionary<Target, double>();
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
                {
                    lock (_dictSeqToTime)
                    {
                        _dictSeqToTime.Add(nodePep.ModifiedTarget, time);
                    }
                }
            }

            /// <summary>
            /// Attempts to get a retention time prediction for a given peptide
            /// </summary>
            public double? GetPredictedRetentionTime(PeptideDocNode nodePep)
            {
                if (_calculator == null || _conversion == null)
                    return null;

                double? score = _calculator.ScoreSequence(nodePep.SourceModifiedTarget);
                if (!score.HasValue)
                    return null;
                return _conversion.GetY(score.Value);
            }

            public bool CreateConversion()
            {
                if (_dictSeqToTime == null)
                    return false;

                lock (_dictSeqToTime)   // not necessary, but make Resharper happy
                {
                    var listTimes = new List<double>();
                    var listIrts = new List<double>();
                    int minCount;
                    foreach (var sequence in _calculator.ChooseRegressionPeptides(_dictSeqToTime.Keys, out minCount))
                    {
                        listTimes.Add(_dictSeqToTime[sequence]);
                        listIrts.Add(_calculator.ScoreSequence(sequence).Value);
                    }
                    if (!IrtRegression.TryGet<RegressionLine>(listIrts, listTimes, minCount, out var line))
                        return false;

                    _conversion = new RegressionLineElement((RegressionLine) line);
                    return true;
                }
            }

            public bool IsFirstPassPeptide(PeptideDocNode nodePep)
            {
                return Equals(nodePep.GlobalStandardType, PeptideDocNode.STANDARD_TYPE_IRT);
            }
        }

        private static IEnumerable<KeyValuePair<PeptidePrecursorMz, ChromDataSet>> GetMatchingGroups(
            ChromDataSet chromDataSet, List<PeptidePrecursorMz> listMzPrecursors, bool singleMatch, double tolerance)
        {
            SignedMz maxMzMatch;
            var i = FindMatchMin(chromDataSet, listMzPrecursors, singleMatch, out maxMzMatch);

            // Enumerate all possible matching precursor values, collecting the ones
            // with potentially matching product ions
            var modSeq = chromDataSet.ModifiedSequence;
            var listMatchingGroups = new List<Tuple<PeptidePrecursorMz, ChromDataSet, IList<ChromData>>>();
            for (; i < listMzPrecursors.Count && listMzPrecursors[i].PrecursorMz <= maxMzMatch && listMzPrecursors[i].PrecursorMz.IsNegative == maxMzMatch.IsNegative; i++)
            {
                var peptidePrecursorMz = listMzPrecursors[i];
                if (modSeq != null && !Equals(modSeq, peptidePrecursorMz.NodePeptide.ModifiedTarget)) // ModifiedSequence for peptides, other id for customIons
                    continue;

                var nodeGroup = peptidePrecursorMz.NodeGroup;
                var explicitRetentionTimeInfo = peptidePrecursorMz.NodePeptide.ExplicitRetentionTime;
                if (listMatchingGroups.Count > 0)
                {
                    // If the current chromDataSet has already been used, make a copy.
                    chromDataSet = new ChromDataSet(chromDataSet.IsTimeNormalArea,
                        peptidePrecursorMz.NodePeptide,
                        peptidePrecursorMz.NodeGroup,
                        chromDataSet.FullScanAcquisitionMethod, 
                        chromDataSet.Chromatograms.Select(c => c.CloneForWrite()));
                }
                var groupData = GetMatchingData(nodeGroup, chromDataSet, explicitRetentionTimeInfo, tolerance);
                if (groupData != null)
                {
                    Assume.IsTrue(chromDataSet.PrecursorMz.IsNegative == peptidePrecursorMz.PrecursorMz.IsNegative);
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
                SignedMz? bestMz = null;
                if (singleMatch)
                {
                    var matchMz = chromDataSet.PrecursorMz;
                    foreach (var match in listMatchingGroups)
                    {
                        Assume.IsTrue(match.Item1.PrecursorMz.IsNegative == chromDataSet.PrecursorMz.IsNegative);
                        var currentMz = match.Item1.PrecursorMz;
                        if (!bestMz.HasValue || Math.Abs(matchMz - currentMz) < Math.Abs(matchMz - bestMz.Value))
                            bestMz = currentMz;
                    }
                }

                // Make sure the same chrom data object is not added twice, or two threads
                // may end up processing it at the same time.
                foreach (var match in listMatchingGroups.Where(match =>
                    !bestMz.HasValue || bestMz.Value == match.Item1.PrecursorMz))
                {
                    var chromDataPart = new ChromDataSet(isTimeNormalArea, match.Item1.NodePeptide,
                        match.Item1.NodeGroup, chromDataSet.FullScanAcquisitionMethod, match.Item3);
                    yield return new KeyValuePair<PeptidePrecursorMz, ChromDataSet>(
                        match.Item1, chromDataPart);
                }
            }
        }

        private static int FindMatchMin(ChromDataSet chromDataSet, List<PeptidePrecursorMz> listMzPrecursors, bool singleMatch,
            out SignedMz maxMzMatch)
        {
            // Find the first precursor m/z that is greater than or equal to the
            // minimum possible match value
            var minMzMatch = chromDataSet.PrecursorMz;
            maxMzMatch = chromDataSet.PrecursorMz;
            // Single match should exactly match the precursor m/z
            int i = -1;
            if (singleMatch)
            {
                i = listMzPrecursors.BinarySearch(new PeptidePrecursorMz(null, null, minMzMatch), MZ_COMPARER);
                if (i > 0)
                {
                    // If found a match, scan back for first matching precursor m/z
                    while (i > 0 && listMzPrecursors[i - 1].PrecursorMz == chromDataSet.PrecursorMz)
                        i--;
                }
                // Avoid tolerant match looking for zero m/z chromatograms
                else if (minMzMatch == SignedMz.ZERO)
                {
                    i = ~i;
                }
            }
            if (i < 0)
            {
                // Not found, so use tolerant matching generally from SRM, but some older
                // SKYD files also had precursor m/z written as a float
                minMzMatch -= TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
                maxMzMatch += TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
                i = listMzPrecursors.BinarySearch(new PeptidePrecursorMz(null, null, minMzMatch), MZ_COMPARER);
                if (i < 0)
                    i = ~i;
            }
            return i;
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
        private static IList<ChromData> GetMatchingData(TransitionGroupDocNode nodeGroup, ChromDataSet chromDataSet, ExplicitRetentionTimeInfo explicitRT, double tolerance)
// ReSharper restore SuggestBaseTypeForParameter
        {
            // Look for potential product ion matches
            var listMatchingData = GetBestMatching(chromDataSet.Chromatograms, nodeGroup.Transitions, explicitRT, tolerance);

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
                match.Chrom.DocNode = match.Trans;
                result[i] = match.Chrom;
            }
            return result;
        }

        private static IList<IndexChromDataTrans> GetBestMatching(
            IEnumerable<ChromData> chromatograms, IEnumerable<TransitionDocNode> transitions, ExplicitRetentionTimeInfo explicitRT, double tolerance)
        {
            var listMatchingData = new List<IndexChromDataTrans>();
            // Create lists of elements sorted by m/z
            var listMzIndexChromatograms =
                chromatograms.Select((c, i) => new MzIndexChromData(c.Key.Product, i, c)).ToList();
            listMzIndexChromatograms.Sort((t1, t2) => Comparer.Default.Compare(t1.Mz, t2.Mz));
            var listMzTrans = transitions.Select(t => new MzTrans(t.Mz, t)).ToList();
            listMzTrans.Sort((t1, t2) => Comparer.Default.Compare(t1.Mz, t2.Mz));
            // Find best matches between chromatograms and transitions by walking the ordered lists
            int ic = 0, it = 0;
            int cc = listMzIndexChromatograms.Count, ct = listMzTrans.Count;
            while (ic < cc && it < ct)
            {
                var tc = listMzIndexChromatograms[ic];
                var tt = listMzTrans[it];
                int icNext = ic + 1, itNext = it + 1;
                if (tc.Mz.CompareTolerant(tt.Mz, tolerance) != 0)
                {
                    // Current transition and current chromatogram are not a match
                    // Advance in the list with the smaller m/z
                    if (tc.Mz > tt.Mz)
                        it = itNext;
                    else
                        ic = NextChrom(ic, tc, null, null, listMatchingData);
                }
                else if (explicitRT != null && tc.Chrom.RawTimes != null &&
                         (explicitRT.RetentionTime < tc.Chrom.RawTimes.First() || tc.Chrom.RawTimes.Last() < explicitRT.RetentionTime))
                {
                    // Current transition mz matches, but retention time range does not contain explicitRT
                    it = itNext;  // Just advance in transition list and continue
                }
                else
                {
                    // Current transition and current chromatogram are an mz match
                    // If next chromatogram matches better, just advance in chromatogram list and continue
                    double delta = Math.Abs(tc.Mz - tt.Mz);
                    Assume.IsTrue(tc.Mz.IsNegative == tt.Mz.IsNegative);
                    List<MzIndexChromData> tc2 = null;
                    // Handle the case where there are both ms1 and sim chromatograms
                    // Or where Q1-Q3 pair is selected more than once, probably with different RT windows
                    while (icNext < cc)
                    {
                        var tcNext = listMzIndexChromatograms[icNext];
                        if (!AreMatchingPrecursors(tc, tcNext) && !AreMatchingFragments(tc, tcNext))
                            break;

                        if (tc2 == null)
                            tc2 = new List<MzIndexChromData> {tcNext};
                        else
                            tc2.Add(tcNext);
                        icNext++;
                    }
                    if (icNext < cc && delta > Math.Abs(listMzIndexChromatograms[icNext].Mz - tt.Mz))
                    {
                        Assume.IsTrue(listMzIndexChromatograms[icNext].Mz.IsNegative == tt.Mz.IsNegative);
                        ic = NextChrom(ic, tc, tc2, null, listMatchingData);
                    }
                    // or next transition matches better, just advance in transition list and continue
                    else if (itNext < ct && delta > Math.Abs(tc.Mz - listMzTrans[itNext].Mz))
                        it = itNext;
                    // otherwise, this is the best match, so add it
                    else
                    {
                        ic = NextChrom(ic, tc, tc2, tt, listMatchingData);
                        it = itNext;
                    }
                }
            }
            // Advance through remaining chromatograms
            while (ic < cc)
                ic = NextChrom(ic, listMzIndexChromatograms[ic], null, null, listMatchingData);

            // Sort back into original order
            listMatchingData.Sort((t1, t2) => Comparer.Default.Compare(t1.Index, t2.Index));
            return listMatchingData;
        }

        private static bool AreMatchingPrecursors(MzIndexChromData tc, MzIndexChromData tc2)
        {
            return tc.Mz == tc2.Mz &&
                   tc.Chrom.Key.Source != ChromSource.fragment &&
                   tc2.Chrom.Key.Source != ChromSource.fragment;
        }

        private static bool AreMatchingFragments(MzIndexChromData tc, MzIndexChromData tc2)
        {
            return tc.Mz == tc2.Mz &&
                   tc.Chrom.Key.Precursor == tc2.Chrom.Key.Precursor &&
                   tc.Chrom.Key.Source == ChromSource.fragment &&
                   tc2.Chrom.Key.Source == ChromSource.fragment;
        }

        private static int NextChrom(int ic, MzIndexChromData tc, ICollection<MzIndexChromData> tc2List, MzTrans tt,
                                     ICollection<IndexChromDataTrans> listMatchingData)
        {
            // Make sure all chromatograms extracted from MS1 stay with the group regardless of whether
            // they actually match transitions
            var nodeTran = tt != null ? tt.Trans : null;
            if (nodeTran != null || tc.Chrom.Key.Source != ChromSource.fragment)
            {
                listMatchingData.Add(new IndexChromDataTrans(tc.Index, tc.Chrom, nodeTran));

                // If there are two precursor chromatograms matching the same transition, add them
                // both. One will probably fail to load. At some point, we will make Transitions contain
                // a ChromSource, and then we will only match the right ChromSource.
                // If there are two fragment chromatograms matching the same transition, add them
                // both.  Likely they have different RT ranges and only one or the other is useful,
                // but we don't have enough RT information at this point to make that decision.
                if (tc2List != null)
                {
                    foreach (var tc2 in tc2List)
                        listMatchingData.Add(new IndexChromDataTrans(tc2.Index, tc2.Chrom, nodeTran));
                }
            }
            return ic + (tc2List != null ? tc2List.Count+1 : 1);
        }

        private class MzIndexChromData
        {
            public MzIndexChromData(SignedMz mz, int index, ChromData chrom)
            {
                Mz = mz;
                Index = index;
                Chrom = chrom;
            }

            public SignedMz Mz { get; private set; }
            public int Index { get; private set; }
            public ChromData Chrom { get; private set; }
        }

        private class MzTrans
        {
            public MzTrans(SignedMz mz, TransitionDocNode trans)
            {
                Mz = mz;
                Trans = trans;
                Assume.IsTrue(mz.IsNegative == (trans.Transition.Charge<0));
            }

            public SignedMz Mz { get; private set; }
            public TransitionDocNode Trans { get; private set; }
        }

        private class IndexChromDataTrans
        {
            public IndexChromDataTrans(int index, ChromData chrom, TransitionDocNode trans)
            {
                Index = index;
                Chrom = chrom;
                Trans = trans;
            }

            public int Index { get; private set; }
            public ChromData Chrom { get; private set; }
            public TransitionDocNode Trans { get; private set; }
        }

        private static readonly MzComparer MZ_COMPARER = new MzComparer();

        private class MzComparer : IComparer<PeptidePrecursorMz>
        {
            public int Compare(PeptidePrecursorMz p1, PeptidePrecursorMz p2)
            {
                // ReSharper disable PossibleNullReferenceException
                return Comparer.Default.Compare(p1.PrecursorMz, p2.PrecursorMz);
                // ReSharper restore PossibleNullReferenceException
            }
        }

        private ChromDataProvider CreateChromatogramRecalcProvider(MsDataFileUri dataFilePathRecalc, ChromFileInfo fileInfo)
        {
            return new CachedChromatogramDataProvider(_cacheRecalc,
                                                      _document,
                                                      dataFilePathRecalc,
                                                      fileInfo,
                                                      IsSingleMatchMzFile,
                                                      _status,
                                                      0,
                                                      100,
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

        private ChromDataProvider CreateChromatogramProvider(MsDataFileImpl dataFile, ChromFileInfo fileInfo)
        {
            return new ChromatogramDataProvider(dataFile, fileInfo, _status, 0, 100, _loader);
        }

        private SpectraChromDataProvider CreateSpectraChromProvider(MsDataFileImpl dataFile, ChromFileInfo fileInfo)
        {
            // Give retention time predictor to provider if it may be necessary for 2 phase extraction
            IRetentionTimePredictor retentionTimePredictor = null;
            var predict = _document.Settings.PeptideSettings.Prediction;
            if (predict.RetentionTime != null && predict.RetentionTime.IsAutoCalculated)
                retentionTimePredictor = _retentionTimePredictor;

            return new SpectraChromDataProvider(dataFile, fileInfo, _document, retentionTimePredictor,
                CachePath, _status, 0, 100, _loader);
        }

        private const int MAX_CHROM_READ_AHEAD = 20;

        private void PostChromDataSet(PeptideChromDataSets chromDataSet)
        {
            // First check for any errors on the writer thread
            if (_chromDataSets.Exception != null)
                throw new ChromCacheBuildException(MSDataFilePath, _chromDataSets.Exception);

            // Add new chromatogram data set, if not empty
            if (chromDataSet != null)
            {
                if (chromDataSet.FilterByRetentionTime())
                    _chromDataSets.Add(chromDataSet);
            }
        }

        private void WriteChromDataSets(PeptideChromDataSets chromDataSets)
        {
            var dictScoresToIndex = new Dictionary<IList<float>, int>();
            bool saveRawTimes = chromDataSets.IsSaveRawTimes && CacheFormat.FormatVersion >= CacheFormatVersion.Twelve;
            foreach (var chromDataSet in chromDataSets.DataSets)
            {
                if (_fs.Stream == null)
                    throw new InvalidDataException(
                        Resources.ChromCacheBuilder_WriteLoop_Failure_writing_cache_file);
                WriteChromDataSet(chromDataSets.IndexInFile, chromDataSet, dictScoresToIndex, saveRawTimes, chromDataSets.IsProcessedScans);
            }
        }

        private void WriteChromDataSet(int indexInFile, ChromDataSet chromDataSet, Dictionary<IList<float>, int> dictScoresToIndex, bool saveRawTimes, bool isProcessedScans)
        {
            long location = _fs.Stream.Position;
            var groupOfTimeIntensities = chromDataSet.ToGroupOfTimeIntensities(saveRawTimes);
                // Write the raw chromatogram points
            MemoryStream pointsMemoryStream = new MemoryStream();
            var scanIdsByChromSource = groupOfTimeIntensities is InterpolatedTimeIntensities
                ? ((InterpolatedTimeIntensities) groupOfTimeIntensities).ScanIdsByChromSource()
                : null;
            groupOfTimeIntensities.WriteToStream(pointsMemoryStream);
                // Compress the data (can be huge for AB data with lots of zeros)
            byte[] pointsCompressed = pointsMemoryStream.ToArray().Compress(3);
                int lenCompressed = pointsCompressed.Length;
            int lenUncompressed = (int) pointsMemoryStream.Length;
                if (_fs.Stream == null)
                    throw new InvalidDataException(
                        Resources.ChromCacheBuilder_WriteLoop_Failure_writing_cache_file);
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
                    scoresIndex = _scoreCount;
                    dictScoresToIndex.Add(startPeak.DetailScores, scoresIndex);

                    // Add scores to the scores list
                    foreach (var peakSet in chromDataSet.PeakSets)
                    {
                        PrimitiveArrays.Write(_fsScores.Stream, peakSet.DetailScores);
                        _scoreCount += peakSet.DetailScores.Length;
                    }
                }

                // Add to header list
                ChromGroupHeaderInfo.FlagValues flags = 0;
            if (groupOfTimeIntensities.HasMassErrors)
                    flags |= ChromGroupHeaderInfo.FlagValues.has_mass_errors;
                if (chromDataSet.HasCalculatedMzs)
                    flags |= ChromGroupHeaderInfo.FlagValues.has_calculated_mzs;
                if (chromDataSet.Extractor == ChromExtractor.base_peak)
                    flags |= ChromGroupHeaderInfo.FlagValues.extracted_base_peak;
                else if(chromDataSet.Extractor == ChromExtractor.qc)
                    flags |= ChromGroupHeaderInfo.FlagValues.extracted_qc_trace;
            if (scanIdsByChromSource != null && scanIdsByChromSource.ContainsKey(ChromSource.ms1))
                        flags |= ChromGroupHeaderInfo.FlagValues.has_ms1_scan_ids;
            if (scanIdsByChromSource != null && scanIdsByChromSource.ContainsKey(ChromSource.fragment))
                        flags |= ChromGroupHeaderInfo.FlagValues.has_frag_scan_ids;
            if (scanIdsByChromSource != null && scanIdsByChromSource.ContainsKey(ChromSource.sim))
                        flags |= ChromGroupHeaderInfo.FlagValues.has_sim_scan_ids;
            if (groupOfTimeIntensities is RawTimeIntensities)
                flags |= ChromGroupHeaderInfo.FlagValues.raw_chromatograms;
            if (_document.Settings.TransitionSettings.FullScan.AcquisitionMethod == FullScanAcquisitionMethod.DDA)
                flags |= ChromGroupHeaderInfo.FlagValues.dda_acquisition_method;

                var header = new ChromGroupHeaderInfo(chromDataSet.PrecursorMz,
                    0,
                    chromDataSet.Count,
                    _listTransitions.Count,
                    chromDataSet.CountPeaks,
                    _peakCount,
                    scoresIndex,
                    chromDataSet.MaxPeakIndex,
                groupOfTimeIntensities.NumInterpolatedPoints,
                    lenCompressed,
                    lenUncompressed,
                    location,
                    flags,
                    chromDataSet.StatusId,
                    chromDataSet.StatusRank,
                    chromDataSet.MinRawTime,
                chromDataSet.MaxRawTime,
                // TODO(version)
                chromDataSet.CollisionalCrossSectionSqA,
                chromDataSet.IonMobilityUnits);
                header.CalcTextIdIndex(chromDataSet.ModifiedSequence, _dictSequenceToByteIndex, _listTextIdBytes);

                int? transitionPeakCount = null;
                foreach (var chromData in chromDataSet.Chromatograms)
                {
                    var chromTran = new ChromTransition(chromData.Key.Product,
                        chromData.Key.ExtractionWidth,
                       (float)(chromData.Key.IonMobilityFilter.IonMobility.Mobility ?? 0),
                       (float)(chromData.Key.IonMobilityFilter.IonMobilityExtractionWindowWidth ?? 0),
                        chromData.Key.Source);

                    if (groupOfTimeIntensities.HasMassErrors && chromData.TimeIntensities.MassErrors == null)
                    {
                        chromTran.MissingMassErrors = true;
                    }
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
                CacheFormat.ChromPeakSerializer().WriteItems(_fsPeaks.FileStream, chromData.Peaks);
                }

                _listGroups.Add(new ChromGroupHeaderEntry(indexInFile, header));
            }
        }


    internal sealed class FileBuildInfo
    {
        public FileBuildInfo(MsDataFileUri msDataFileUri, MsDataFileImpl file)
        {
            StartTime = file.RunStartTime;
            LastWriteTime = msDataFileUri.GetFileLastWriteTime();
            InstrumentInfoList = file.GetInstrumentConfigInfoList();
            UsedMs1Centroids = file.RequireVendorCentoridedMs1;
            UsedMs2Centroids = file.RequireVendorCentoridedMs2;
            HasCombinedIonMobility = file.HasCombinedIonMobilitySpectra;
            SampleId = file.GetSampleId();
            SerialNumber = file.GetInstrumentSerialNumber();
        }

        public FileBuildInfo(ChromCachedFile cachedFile)
        {
            StartTime = cachedFile.RunStartTime;
            LastWriteTime = cachedFile.FileWriteTime;
            InstrumentInfoList = cachedFile.InstrumentInfoList;
            IsSingleMatchMz = cachedFile.IsSingleMatchMz;
            UsedMs1Centroids = cachedFile.UsedMs1Centroids;
            UsedMs2Centroids = cachedFile.UsedMs2Centroids;
            HasMidasSpectra = cachedFile.HasMidasSpectra;
            HasCombinedIonMobility = cachedFile.HasCombinedIonMobility;
            SampleId = cachedFile.SampleId;
            SerialNumber = cachedFile.InstrumentSerialNumber;
        }

        public FileBuildInfo(DateTime? startTime, DateTime lastWriteTime)
        {
            StartTime = startTime;
            LastWriteTime = lastWriteTime;
            InstrumentInfoList = new MsInstrumentConfigInfo[0];
        }

        public DateTime? StartTime { get; private set; }
        public DateTime LastWriteTime { get; private set; }
        public IEnumerable<MsInstrumentConfigInfo> InstrumentInfoList { get; private set; }
        public ChromCachedFile.FlagValues Flags { get; private set; }
        public string SampleId { get; private set; }
        public string SerialNumber { get; private set; }

        public bool? IsSingleMatchMz
        {
            get { return ChromCachedFile.IsSingleMatchMzFlags(Flags); }
            set
            {
                Flags &= ~ChromCachedFile.FlagValues.single_match_mz_known;
                Flags &= ~ChromCachedFile.FlagValues.single_match_mz;
                if (value.HasValue)
                {
                    Flags |= ChromCachedFile.FlagValues.single_match_mz_known;
                    if (value.Value)
                        Flags |= ChromCachedFile.FlagValues.single_match_mz;
                }
            }
        }

        public bool UsedMs1Centroids
        {
            get { return (Flags & ChromCachedFile.FlagValues.used_ms1_centroids) != 0; }
            set { SetFlag(value, ChromCachedFile.FlagValues.used_ms1_centroids); }
        }

        public bool UsedMs2Centroids
        {
            get { return (Flags & ChromCachedFile.FlagValues.used_ms2_centroids) != 0; }
            set { SetFlag(value, ChromCachedFile.FlagValues.used_ms2_centroids); }
        }

        public bool HasMidasSpectra
        {
            get { return (Flags & ChromCachedFile.FlagValues.has_midas_spectra) != 0; }
            set { SetFlag(value, ChromCachedFile.FlagValues.has_midas_spectra); }
        }

        public bool HasCombinedIonMobility
        {
            get { return (Flags & ChromCachedFile.FlagValues.has_combined_ion_mobility) != 0; }
            set { SetFlag(value, ChromCachedFile.FlagValues.has_combined_ion_mobility); }
        }

        private void SetFlag(bool set, ChromCachedFile.FlagValues flag)
        {
            if (set)
                Flags |= flag;
            else
                Flags &= ~flag;
        }

        public int SizeScanIds { get; set; }
        public long LocationScanIds { get; set; }
    }
}
