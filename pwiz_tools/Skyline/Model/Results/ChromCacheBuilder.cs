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
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class ChromCacheBuilder : ChromCacheWriter
    {
        // Lock on this to access these variables
        private readonly SrmDocument _document;
        private LibraryRetentionTimes _libraryRetentionTimes;
        private FileBuildInfo _currentFileInfo;

        private readonly ChromatogramCache _cacheRecalc;
        
        // Lock on _chromDataSets to access these variables
        private QueueWorker<PeptideChromDataSets> _chromDataSets;
        private readonly object _writeLock = new object();

        // Accessed only on the write thread
        private readonly RetentionTimePredictor _retentionTimePredictor;
        private readonly Dictionary<string, int> _dictSequenceToByteIndex = new Dictionary<string, int>();

        private const int SCORING_THREADS = 4;
        //private static readonly Log LOG = new Log<ChromCacheBuilder>();

        public ChromCacheBuilder(SrmDocument document, ChromatogramCache cacheRecalc,
            string cachePath, MsDataFileUri msDataFilePath, ILoadMonitor loader, ProgressStatus status,
            Action<ChromatogramCache, Exception> complete)
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
                calcEnum = PeakFeatureCalculator.Calculators
                    .Where(c => c is DetailedPeakFeatureCalculator);
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
            //LOG.InfoFormat("Start file import: {0}", MSDataFilePath.GetFileName());  // Not L10N

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
            LoadingStatus.Transitions.Flush();
            _status = _status.ChangeMessage(message).ChangePercentComplete(0);
            _status = LoadingStatus.ChangeFilePath(MSDataFilePath).ChangeImporting(true);
            var allChromData = LoadingStatus.Transitions;
            allChromData.MaxIntensity = 0;
            allChromData.MaxRetentionTime = 0;
            allChromData.MaxRetentionTimeKnown = false;
            _loader.UpdateProgress(_status);

            try
            {
                var dataFilePath = MSDataFilePath;
                var lockMassCorrection = MSDataFilePath.GetLockMassParameters();
                var centroidMS1 = MSDataFilePath.GetCentroidMs1();
                var centroidMS2 = MSDataFilePath.GetCentroidMs2();
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
                        var configInfo = fileInfo.InstrumentInfoList.FirstOrDefault();
                        var fullScan = _document.Settings.TransitionSettings.FullScan;
                        var enableSimSpectrum = fullScan.IsEnabled;
                        inFile = GetMsDataFile(dataFilePathPart, sampleIndex, lockMassCorrection, configInfo,
                            enableSimSpectrum, centroidMS1, centroidMS2);
                        // Preserve centroiding info as part of MsDataFileUri string in chromdata only if it will be used
                        // CONSIDER: Dangerously high knowledge of future control flow required for making this decision
                        if (!ChromatogramDataProvider.HasChromatogramData(inFile) && !inFile.HasSrmSpectra)
                            MSDataFilePath = dataFilePath = MSDataFilePath.ChangeCentroiding(centroidMS1, centroidMS2);
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
                            _currentFileInfo = new FileBuildInfo(fileInfo.RunStartTime,
                                fileInfo.FileWriteTime ?? DateTime.Now, new MsInstrumentConfigInfo[0], null);
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
                        allChromData.MaxIntensity = (float) (provider.MaxIntensity ?? 0);
                        allChromData.MaxRetentionTime = (float) (provider.MaxRetentionTime ?? 0);
                        allChromData.MaxRetentionTimeKnown = provider.MaxRetentionTime.HasValue;
                    }
                    else if (ChorusResponseChromDataProvider.IsChorusResponse(dataFilePath))
                    {
                        provider = new ChorusResponseChromDataProvider(_document, fileInfo, _status, 0, 100, _loader);
                    }
                    else if (RemoteChromDataProvider.IsRemoteChromFile(dataFilePath))
                    {
                        provider = new RemoteChromDataProvider(_document, _retentionTimePredictor, fileInfo, _status, 0,
                            100, _loader);
                    }
                    else if (ChromatogramDataProvider.HasChromatogramData(inFile))
                    {
                        provider = CreateChromatogramProvider(inFile, fileInfo);
                    }
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

                    // Start multiple threads to perform peak scoring.
                    _chromDataSets = new QueueWorker<PeptideChromDataSets>(null, ScoreWriteChromDataSets);
                    _chromDataSets.RunAsync(SCORING_THREADS, "Scoring/writing", MAX_CHROM_READ_AHEAD); // Not L10N

                    Read(provider);

                    _status = provider.Status;

                    if (_status.IsCanceled)
                        ExitRead(null);

                    if ((inFile != null) && (inFile.GetLog() != null)) // in case perf logging is enabled
                        DebugLog.Info(inFile.GetLog());
                }
                finally
                {
                    if (_chromDataSets != null)
                        _chromDataSets.Abort();
                    if (provider != null)
                        provider.Dispose();
                    else if (inFile != null)
                        inFile.Dispose();
                }

                ActionUtil.RunAsync(() => ExitRead(null), "Exit read"); // Not L10N
            }
            catch (LoadCanceledException x)
            {
                _status = x.Status;
                ExitRead(null);
            }
            catch (MissingDataException x)
            {
                ExitRead(new MissingDataException(x.MessageFormat, MSDataFilePath.GetSampleOrFileName(), x));
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
                    ExitRead(new MissingDataException(missingDataX.MessageFormat, MSDataFilePath.GetSampleOrFileName(), missingDataX));
                }
                else if (x.Message.Contains("PeakDetector::NoVendorPeakPickingException")) // Not L10N
                {
                    ExitRead(new NoCentroidedDataException(MSDataFilePath.GetFileName(), x));
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

        private MsDataFileImpl GetMsDataFile(string dataFilePathPart, int sampleIndex, LockMassParameters lockMassParameters, MsInstrumentConfigInfo msInstrumentConfigInfo, bool enableSimSpectrum, bool requireCentroidedMS1, bool requireCentroidedMS2)
        {
            return new MsDataFileImpl(dataFilePathPart, sampleIndex, lockMassParameters, enableSimSpectrum, requireVendorCentroidedMS1:requireCentroidedMS1, requireVendorCentroidedMS2:requireCentroidedMS2);
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

            var listProviderIds = new List<IList<int>>(listChromData.Where(IsFirstPassPeptide).Select(c => c.ProviderIds.ToArray()));
            listProviderIds.AddRange(listChromData.Where(c => !IsFirstPassPeptide(c)).Select(c => c.ProviderIds.ToArray()));

            provider.SetRequestOrder(listProviderIds);

            // Create IRT prediction if we don't already have it.
            var predict = _document.Settings.PeptideSettings.Prediction;
            if (predict.RetentionTime != null && predict.RetentionTime.IsAutoCalculated)
            {
                for (int i = 0; i < listChromData.Count; i++)
                {
                    var pepChromData = listChromData[i];
                    if (IsFirstPassPeptide(pepChromData))
                    {
                        if (pepChromData.Load(provider))
                            PostChromDataSet(pepChromData);

                        // Release the reference to the chromatogram data set so that
                        // it can be garbage collected after it has been written
                        listChromData[i] = null;
                    }
                }

                // All threads must complete scoring before we complete the first pass.
                _chromDataSets.Wait();
                _retentionTimePredictor.CreateConversion();

                // Let the provider know that it is now safe to use retention time prediction
                if (provider.CompleteFirstPass())
                {
                    // Then refresh the chrom data list if indicated by provider, as it should now contain more than first-pass peptides
                    listChromData = CalcPeptideChromDataSets(provider, listMzPrecursors, setInternalStandards);
                    listProviderIds = new List<IList<int>>(listChromData.Select(c => c.ProviderIds.ToArray()));
                    provider.SetRequestOrder(listProviderIds);
                }
            }

            // Load scan data.
            for (int i = 0; i < listChromData.Count; i++)
            {
                var pepChromData = listChromData[i];
                if (pepChromData == null)
                    continue;
                if (!IsFirstPassPeptide(pepChromData))
                {
                    if (pepChromData.Load(provider))
                        PostChromDataSet(pepChromData);
                }

                // Release the reference to the chromatogram data set so that
                // it can be garbage collected after it has been written
                listChromData[i] = null;
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

            //LOG.InfoFormat("Scans read: {0}", MSDataFilePath.GetFileName());  // Not L10N
            _chromDataSets.DoneAdding(true);
            //LOG.InfoFormat("Peak scoring/writing finished: {0}", MSDataFilePath.GetFileName());  // Not L10N

            _listCachedFiles.Add(new ChromCachedFile(MSDataFilePath,
                                     _currentFileInfo.Flags,
                                     _currentFileInfo.LastWriteTime,
                                     _currentFileInfo.StartTime,
                                     LoadingStatus.Transitions.MaxRetentionTime,
                                     LoadingStatus.Transitions.MaxIntensity,
                                     _currentFileInfo.SizeScanIds,
                                     _currentFileInfo.LocationScanIds,
                                     _currentFileInfo.InstrumentInfoList));
        }

        private bool IsFirstPassPeptide(PeptideChromDataSets pepChromData)
        {
            return pepChromData.NodePep != null && _retentionTimePredictor.IsFirstPassPeptide(pepChromData.NodePep);
        }

        private List<PeptideChromDataSets> CalcPeptideChromDataSets(ChromDataProvider provider,
            List<PeptidePrecursorMz> listMzPrecursors, HashSet<IsotopeLabelType> setInternalStandards)
        {
            bool singleMatch = provider.IsSingleMzMatch;

            var dictPeptideChromData = new Dictionary<PeptideSequenceModKey, PeptideChromDataSets>();
            var listChromData = new List<PeptideChromDataSets>();

            foreach (var chromDataSet in GetChromDataSets(provider))
            {
                if (chromDataSet == null)
                    continue;
                foreach (var matchingGroup in GetMatchingGroups(chromDataSet, listMzPrecursors, singleMatch))
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
            listChromData.Sort(CompareKeys);

            // Avoid holding onto chromatogram data sets for entire read
            dictPeptideChromData.Clear();
            return listChromData;
        }

        /// <summary>
        /// Compare data sets by maximum retention time.
        /// </summary>
        private int CompareKeys(PeptideChromDataSets p1, PeptideChromDataSets p2)
        {
            var key1 = p1.FirstKey;
            var key2 = p2.FirstKey;
            return key1.CompareTo(key2);
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
//        public static IEnumerable<ChromDataSet> GetChromDataSets(ChromDataProvider provider, bool isTimeNormalArea)
//        {
//            var listKeyIndex = new List<KeyValuePair<ChromKey, int>>(provider.ChromIds);
//            listKeyIndex.Sort((p1, p2) => p1.Key.CompareTo(p2.Key));
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
            if (nodePep.ExplicitRetentionTime != null)
            {
                // Explicitly set retention time overrides any predictor
                peptideChromDataSets.PredictedRetentionTime =
                    new RetentionTimePrediction(nodePep.ExplicitRetentionTime.RetentionTime, nodePep.ExplicitRetentionTime.RetentionTimeWindow ?? 0);
                peptideChromDataSets.RetentionTimes = new[] { nodePep.ExplicitRetentionTime.RetentionTime }; // Feed this information to the peak picker
                return;
            }
            if (!nodePep.IsProteomic)  // No retention time prediction for small molecules
                return;
            string lookupSequence = nodePep.SourceUnmodifiedTextId;
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
            private readonly Dictionary<string, double> _dictSeqToTime;

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
                {
                    lock (_dictSeqToTime)
                    {
                        _dictSeqToTime.Add(nodePep.ModifiedSequence, time);
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

                double? score = _calculator.ScoreSequence(nodePep.SourceTextId);
                if (!score.HasValue)
                    return null;
                return _conversion.GetY(score.Value);
            }

            public void CreateConversion()
            {
                lock (_dictSeqToTime)   // not necessary, but make Resharper happy
                {
                    if (_dictSeqToTime == null)
                        return;

                    var listTimes = new List<double>();
                    var listIrts = new List<double>();
                    foreach (string sequence in _calculator.GetStandardPeptides(_dictSeqToTime.Keys))
                    {
                        listTimes.Add(_dictSeqToTime[sequence]);
                        listIrts.Add(_calculator.ScoreSequence(sequence).Value);
                    }
                    RegressionLine line;
                    if (RCalcIrt.TryGetRegressionLine(listIrts, listTimes, out line))
                        _conversion = new RegressionLineElement(line);
                }
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
                double? bestMz = null;
                if (singleMatch)
                {
                    double matchMz = chromDataSet.PrecursorMz;
                    foreach (var match in listMatchingGroups)
                    {
                        double currentMz = match.Item1.PrecursorMz;
                        if (!bestMz.HasValue || Math.Abs(matchMz - currentMz) < Math.Abs(matchMz - bestMz.Value))
                            bestMz = currentMz;
                    }
                }

                // Make sure the same chrom data object is not added twice, or two threads
                // may end up processing it at the same time.
                var setChromData = new HashSet<ChromData>();
                foreach (var match in listMatchingGroups.Where(match =>
                    !bestMz.HasValue || bestMz.Value == match.Item1.PrecursorMz))
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
            var listMatchingData = GetBestMatching(chromDataSet.Chromatograms, nodeGroup.Transitions);

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

        private static List<ChromDataTrans> GetBestMatching(
            IEnumerable<ChromData> chromatograms, IEnumerable<TransitionDocNode> transitions)
        {
            var listMatchingData = new List<IndexChromDataTrans>();
            const float tolerance = (float)TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
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
                if (ChromKey.CompareTolerant(tc.Mz, tt.Mz, tolerance) != 0)
                {
                    // Current transition and current chromatogram are not a match
                    // Advance in the list with the smaller m/z
                    if (tc.Mz > tt.Mz)
                        it = itNext;
                    else
                        ic = NextChrom(ic, tc, null, null, listMatchingData);
                }
                else
                {
                    // Current transition and current chromatogram are an mz match
                    // If next chromatogram matches better, just advance in chromatogram list and continue
                    double delta = Math.Abs(tc.Mz - tt.Mz);
                    MzIndexChromData tc2 = null;
                    // Handle the case where there are both ms1 and sim chromatograms
                    // Or where Q1-Q3 pair is selected more than once, probably with different RT windows
                    if (icNext < cc)
                    {
                        var tcNext = listMzIndexChromatograms[icNext];
                        if (AreMatchingPrecursors(tc, tcNext) || AreMatchingFragments(tc, tcNext))
                        {
                            tc2 = tcNext;
                            icNext++;
                        }
                    }
                    if (icNext < cc && delta > Math.Abs(listMzIndexChromatograms[icNext].Mz - tt.Mz))
                    {
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
            return listMatchingData.Select(t => new ChromDataTrans(t.Chrom, t.Trans)).ToList();
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

        private static int NextChrom(int ic, MzIndexChromData tc, MzIndexChromData tc2, MzTrans tt,
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
                if (tc2 != null)
                {
                    listMatchingData.Add(new IndexChromDataTrans(tc2.Index, tc2.Chrom, nodeTran));
                }
            }
            return ic + (tc2 != null ? 2 : 1);
        }

        private class MzIndexChromData
        {
            public MzIndexChromData(double mz, int index, ChromData chrom)
            {
                Mz = mz;
                Index = index;
                Chrom = chrom;
            }

            public double Mz { get; private set; }
            public int Index { get; private set; }
            public ChromData Chrom { get; private set; }
        }

        private class MzTrans
        {
            public MzTrans(double mz, TransitionDocNode trans)
            {
                Mz = mz;
                Trans = trans;
            }

            public double Mz { get; private set; }
            public TransitionDocNode Trans { get; private set; }
        }

        private class ChromDataTrans
        {
            public ChromDataTrans(ChromData chrom, TransitionDocNode trans)
            {
                Chrom = chrom;
                Trans = trans;
            }

            public ChromData Chrom { get; private set; }
            public TransitionDocNode Trans { get; private set; }
        }

        private class IndexChromDataTrans : ChromDataTrans
        {
            public IndexChromDataTrans(int index, ChromData chrom, TransitionDocNode trans)
                : base(chrom, trans)
            {
                Index = index;
            }

            public int Index { get; private set; }
        }

        private static readonly MzComparer MZ_COMPARER = new MzComparer();

        private class MzComparer : IComparer<PeptidePrecursorMz>
        {
            public int Compare(PeptidePrecursorMz p1, PeptidePrecursorMz p2)
            {
                return Comparer.Default.Compare(p1.PrecursorMz, p2.PrecursorMz);
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
                _chromDataSets.Add(chromDataSet);
        }


        private void WriteChromDataSets(PeptideChromDataSets chromDataSets)
        {
            var dictScoresToIndex = new Dictionary<IList<float>, int>();
            foreach (var chromDataSet in chromDataSets.DataSets)
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
                int[][] scanIds = chromDataSet.ScanIndexes;
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
                ChromGroupHeaderInfo5.FlagValues flags = 0;
                if (massErrors != null)
                    flags |= ChromGroupHeaderInfo5.FlagValues.has_mass_errors;
                if (chromDataSet.HasCalculatedMzs)
                    flags |= ChromGroupHeaderInfo5.FlagValues.has_calculated_mzs;
                if (chromDataSet.Extractor == ChromExtractor.base_peak)
                    flags |= ChromGroupHeaderInfo5.FlagValues.extracted_base_peak;
                if (scanIds != null)
                {
                    if (scanIds[(int) ChromSource.ms1] != null)
                        flags |= ChromGroupHeaderInfo5.FlagValues.has_ms1_scan_ids;
                    if (scanIds[(int) ChromSource.fragment] != null)
                        flags |= ChromGroupHeaderInfo5.FlagValues.has_frag_scan_ids;
                    if (scanIds[(int) ChromSource.sim] != null)
                        flags |= ChromGroupHeaderInfo5.FlagValues.has_sim_scan_ids;
                }
                var header = new ChromGroupHeaderInfo5(chromDataSet.PrecursorMz,
                    0,
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