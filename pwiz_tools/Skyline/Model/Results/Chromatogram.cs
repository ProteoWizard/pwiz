/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public sealed class ChromatogramManager : BackgroundLoader, IDisposable
    {
        private readonly MultiFileLoader _multiFileLoader;

        public bool SupportAllGraphs { get; set; }
        public int? LoadingThreads { get; set; }
        public MultiProgressStatus Status { get { return _multiFileLoader.Status; } }

        public ChromatogramManager(bool synchronousMode)
        {
            IsMultiThreadAware = true;
            _multiFileLoader = new MultiFileLoader(synchronousMode);
        }

        public MultiProgressStatus ChangeStatus(ChromatogramLoadingStatus loadingStatus)
        {
            return _multiFileLoader.ChangeStatus(loadingStatus);
        }

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            // If using full-scan filtering, then completion of library load
            // is a state change event, since peak picking cannot occur until
            // libraries are loaded.
            if (!IsReadyToLoad(previous) && IsReadyToLoad(document))
            {
                return true;
            }
            return !ReferenceEquals(document.Settings.MeasuredResults, previous.Settings.MeasuredResults);
        }

        public override void ClearCache()
        {
        }

        protected override string IsNotLoadedExplained(SrmDocument document)
        {
            SrmSettings settings = document.Settings;
            
            // If using full-scan filtering, then the chromatograms may not be loaded
            // until the libraries are loaded, since they are used for peak picking.
            if (!IsReadyToLoad(document))
            {
                return null;
            } 
            if (!settings.HasResults)
            {
                return null;
            } 
            return settings.MeasuredResults.IsNotLoadedExplained;
        }

        public override void ResetProgress(SrmDocument document)
        {
            _multiFileLoader.ResetStatus();
            _multiFileLoader.ClearDocument(document);
        }

        public void CancelProgress()
        {
            if (!_multiFileLoader.Status.IsEmpty)
                UpdateProgress(_multiFileLoader.CancelStatus());
        }

        public void RemoveFile(MsDataFileUri filePath)
        {
            _multiFileLoader.ClearFile(filePath);
        }

        public override bool AnyProcessing()
        {
            return _multiFileLoader.AnyLoading();
        }

        public void Dispose()
        {
            _multiFileLoader.DoneAddingFiles();
            _multiFileLoader.Dispose();
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            if (document == null || !document.Settings.HasResults)
                return new IPooledStream[0];
            return document.Settings.MeasuredResults.ReadStreams;
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            SrmSettings settings = container.Document.Settings;
            // If the document no longer contains any measured results, or the
            // measured results for the document are completely loaded.
            if (!settings.HasResults || settings.MeasuredResults.IsLoaded)
                return true;

            var dataFilePath = tag as MsDataFileUri;
            if (dataFilePath != null)
            {
                // Cancelled if file is no longer part of the document, or it is
                // already loaded.
                var res = settings.MeasuredResults;
                return !res.IsDataFilePath(dataFilePath) || res.IsCachedFile(dataFilePath);
            }
            return false;
        }

        private bool IsReadyToLoad(SrmDocument document)
        {
            if (null == document)
            {
                return false;
            }
            if (document.Settings.TransitionSettings.FullScan.IsEnabled)
            {
                if (!document.Settings.PeptideSettings.Libraries.IsLoaded)
                {
                    return false;
                }
                if (DocumentRetentionTimes.IsNotLoadedExplained(document) != null)
                {
                    return false;
                }
                if (IonMobilityLibraryManager.IsNotLoadedDocumentExplained(document) != null)
                {
                    return false; // Need to wait for imsdb file to load into memory
                }
            }
            // Make sure any iRT calculater gets loaded before starting to import
            var rtPrediction = document.Settings.PeptideSettings.Prediction.RetentionTime;
            if (rtPrediction != null && !rtPrediction.Calculator.IsUsable)
                return false;
            return true;
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            lock (this)
            {
                // Now that we're in the lock, we have to refresh the current document.
                // If during the time this thread was waiting for the lock, the document
                // has changed state in a way that means another thread will be following
                // behind this one, or the document has become loaded, then this thread
                // has nothing to do.
                var docInLock = container.Document;
                if (IsStateChanged(docCurrent, docInLock) || IsLoaded(docInLock))
                    return false;
                docCurrent = docInLock;

                _multiFileLoader.InitializeThreadCount(LoadingThreads);

                // The Thread.Sleep below caused many issues loading code, which were fixed
                // This may still be good to keep around for periodic testing of synchronization logic
//                Thread.Sleep(1000);

                var loader = new Loader(this, container, document, docCurrent, _multiFileLoader);
                loader.Load();
            }

            return false;
        }

        private sealed class Loader
        {
            private readonly ChromatogramManager _manager;
            private readonly IDocumentContainer _container;
            private readonly SrmDocument _document;
            private readonly SrmDocument _docCurrent;
            private readonly MultiFileLoader _multiFileLoader;
            private MultiFileLoadMonitor _loadMonitor;

            public Loader(ChromatogramManager manager, IDocumentContainer container, SrmDocument document, SrmDocument docCurrent, MultiFileLoader multiFileLoader)
            {
                _manager = manager;
                _container = container;
                _document = document;
                _docCurrent = docCurrent;
                _multiFileLoader = multiFileLoader;
            }

            public void Load()
            {
                string documentFilePath = _container.DocumentFilePath;
                if (documentFilePath == null)
                    return;

                var results = _docCurrent.Settings.MeasuredResults;
                if (results.IsLoaded)
                    return;

                Assume.IsNull(_loadMonitor);
                _loadMonitor = new MultiFileLoadMonitor(_manager, _container, results) {HasUI = _manager.SupportAllGraphs};
                results.Load(_docCurrent, documentFilePath, _loadMonitor, _multiFileLoader, FinishLoad);
            }

            private void CancelLoad(MeasuredResults results)
            {
                _manager.CancelProgress();
                _manager.EndProcessing(_document);
            }

            private static readonly object _finishLock = new object();

            private void FinishLoad(string documentPath, MeasuredResults resultsLoad, MeasuredResults resultsPrevious)
            {
                // Only one finisher at a time, otherwise guaranteed wasted work
                // CONSIDER: In theory this should be a lock per document container, but in
                //           practice we have only one document container per process
                lock (_finishLock)
                {
                    try
                    {
                        FinishLoadSynch(documentPath, resultsLoad, resultsPrevious);
                    }
                    catch (Exception ex)
                    {
                        if (ExceptionUtil.IsProgrammingDefect(ex))
                        {
                            throw;
                        }
                        foreach (var chromatogramStatus in _multiFileLoader.Status.ProgressList)
                        {
                            _multiFileLoader.ChangeStatus((ChromatogramLoadingStatus)chromatogramStatus.ChangeErrorException(ex));
                        }

                        _loadMonitor.UpdateProgress(_multiFileLoader.Status);
                    }
                }
            }

            private void FinishLoadSynch(string documentPath, MeasuredResults resultsLoad, MeasuredResults resultsPrevious)
            {
                if (resultsLoad == null)
                {
                    // Loading was cancelled
                    _manager.EndProcessing(_document);
                    return;
                }

                SrmDocument docNew, docCurrent;
                do
                {
                    docCurrent = _container.Document;
                    var results = docCurrent.Settings.MeasuredResults;
                    // If current document has no results, then cancel
                    if (results == null)
                    {
                        CancelLoad(resultsLoad);
                        return;
                    }
                    else if (results.IsLoaded)
                    {
                        // No need to continue
                        _manager.EndProcessing(_document);
                        return;
                    }
                    // Full precision XML serialization started on r9730 (see Xml.cs) and 3.53 was added at r9743
                    else if (resultsLoad.IsJoiningDisabled ||   // Always skip settings update if joining is disabled
                        (docCurrent.FormatVersion.CompareTo(DocumentFormat.VERSION_3_53) >= 0 &&
                        resultsLoad.CacheVersion.HasValue &&
                        resultsPrevious != null && resultsPrevious.IsDeserialized))
                    {
                        // Skip settings change for deserialized document when it first becomes connected with its cache
                        results = results.UpdateCaches(documentPath, resultsLoad);
                        docNew = docCurrent.ChangeSettingsNoDiff(docCurrent.Settings.ChangeMeasuredResults(results));
                        docNew = _manager.ApplyMetadataRules(docNew);
                    }
                    else
                    {
                        try
                        {
                            using (var settingsChangeMonitor = new SrmSettingsChangeMonitor(new LoadMonitor(_manager, _container, null),
                                                                                            ResultsResources.Loader_FinishLoad_Updating_peak_statistics,
                                                                                            _container, docCurrent))
                            {
                                // First remove any chromatogram sets that were removed during processing
                                results = results.ApplyChromatogramSetRemovals(resultsLoad, resultsPrevious);
                                // Then update caches
                                if (results != null)
                                    results = results.UpdateCaches(documentPath, resultsLoad);
                                docNew = docCurrent.ChangeMeasuredResults(results, settingsChangeMonitor);
                                docNew = _manager.ApplyMetadataRules(docNew);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // Restart the processing form the top
                            docNew = null;
                        }
                    }
                }
                while (docNew == null || !_manager.CompleteProcessing(_container, docNew, docCurrent));
            }
        }

        public SrmDocument ApplyMetadataRules(SrmDocument document)
        {
            var progressDictionary = Status.ProgressList.ToDictionary(status => status.FilePath);
            document = MetadataExtractor.ApplyRules(document, progressDictionary.Keys.ToHashSet(),
                out CommonException<MetadataExtractor.RuleError> error);
            if (error != null)
            {
                ChangeStatus(
                    (ChromatogramLoadingStatus)
                    progressDictionary[error.ExceptionDetail.MsDataFileUri].ChangeWarningMessage(error.Message));
            }

            return document;
        }
    }

    [XmlRoot("replicate")]
    [XmlRootAlias("chromatogram_group")]
    public sealed class ChromatogramSet : XmlNamedIdElement
    {
        /// <summary>
        /// Info for all files contained in this replicate
        /// </summary>
        private ImmutableList<ChromFileInfo> _msDataFileInfo;

        /// <summary>
        /// Ids used in XML to refer to the files in this replicate
        /// </summary>
        private string[] _fileLoadIds;

        /// <summary>
        /// Counter to differentiate rescored versions
        /// </summary>
        private int _rescoreCount;

        private const double DEFAULT_DILUTION_FACTOR = 1;

        public ChromatogramSet(string name,
            IEnumerable<MsDataFileUri> msDataFileNames)
            : this(name, msDataFileNames, Annotations.EMPTY, null)
        {
        }

        public ChromatogramSet(string name,
            IEnumerable<string> msDataFileNames)
            : this(name, msDataFileNames.Select(file => new MsDataFilePath(file)))
        {
            
        }

        public ChromatogramSet(string name, 
                IEnumerable<MsDataFileUri> msDataFileNames,
                Annotations annotations,
                OptimizableRegression optimizationFunction)
            : base(new ChromatogramSetId(), name)
        {
            MSDataFileInfos = msDataFileNames.ToList().ConvertAll(path => new ChromFileInfo(path));

            OptimizationFunction = optimizationFunction;
            Annotations = annotations;
            SampleType = SampleType.DEFAULT;
            SampleDilutionFactor = DEFAULT_DILUTION_FACTOR;
        }

        public IList<ChromFileInfo> MSDataFileInfos
        {
            get { return _msDataFileInfo; }
            private set
            {
                // Make sure paths are unique
                _msDataFileInfo = MakeReadOnly(value.Distinct(new PathComparer<ChromFileInfo>()).ToArray());
            }
        }

        public bool ContainsFile(MsDataFileUri filePath)
        {
            for (int i = 0; i < _msDataFileInfo.Count; i++)
            {
                // Compare ignoring centroiding, combineIMS etc
                if (Equals(_msDataFileInfo[i].FilePath.GetLocation(), filePath.GetLocation()))
                    return true;
            }
            return false;
        }

        public int FileCount { get { return _msDataFileInfo.Count; } }

        public IEnumerable<MsDataFileUri> MSDataFilePaths { get { return MSDataFileInfos.Select(info => info.FilePath); } }

        public bool IsLoaded { get { return !MSDataFileInfos.Contains(info => !info.FileWriteTime.HasValue); } }

        public bool IsLoadedAndAvailable(MeasuredResults measuredResults)
        {
            if (!IsLoaded)
            {
                return false;
            }
            return MSDataFilePaths.All(measuredResults.IsCachedFile);
        }

        public string IsLoadedExplained() // For test and debug purposes, gives a descriptive string for IsLoaded failure
        {
            return IsLoaded ? string.Empty : @"No ChromFileInfo.FileWriteTime for " + string.Join(@",", MSDataFileInfos.Where(info => !info.FileWriteTime.HasValue).Select(f => f.FilePath.GetFilePath()));
        }

        public Annotations Annotations { get; private set; }

        public OptimizableRegression OptimizationFunction { get; private set; }

        public bool UseForRetentionTimeFilter { get; private set; }

        public ChromFileInfo GetFileInfo(ChromFileInfoId fileId)
        {
            return GetFileInfo(IndexOfId(fileId));
        }

        public ChromFileInfo GetFileInfo(ChromatogramGroupInfo chromGroupInfo)
        {
            return GetFileInfo(chromGroupInfo.FilePath);
        }

        public ChromFileInfo GetFileInfo(MsDataFileUri filePath)
        {
            return GetFileInfo(IndexOfPath(filePath));
        }

        public int IndexOfId(ChromFileInfoId fileId)
        {
            // Faster than IndexOf and shows up in a profiler for large document loading
            if (MSDataFileInfos.Count == 1)
                return ReferenceEquals(MSDataFileInfos[0].Id, fileId) ? 0 : -1;

            return MSDataFileInfos.IndexOf(info => ReferenceEquals(info.Id, fileId));
        }

        public int IndexOfPath(MsDataFileUri filePath)
        {
            return MSDataFileInfos.IndexOf(info => Equals(filePath, info.FilePath));
        }

        public ChromFileInfoId FindFile(ChromatogramGroupInfo chromGroupInfo)
        {
            return FindFile(chromGroupInfo.FilePath);
        }

        public ChromFileInfoId FindFile(MsDataFileUri filePath)
        {
            return GetFileId(IndexOfPath(filePath));
        }

        public ChromFileInfoId FindFileById(string id)
        {
            return GetFileId(Array.IndexOf(_fileLoadIds, id));
        }

        private ChromFileInfo GetFileInfo(int ordinalIndex)
        {
            return ordinalIndex != -1 ? MSDataFileInfos[ordinalIndex] : null;
        }

        private ChromFileInfoId GetFileId(int ordinalIndex)
        {
            return (ChromFileInfoId) (ordinalIndex != -1 ? MSDataFileInfos[ordinalIndex].Id : null);
        }

        public double? AnalyteConcentration { get; private set; }

        public double SampleDilutionFactor { get; private set; }

        public SampleType SampleType { get; private set; }

        public string BatchName { get; private set; }

        #region Property change methods

        public ChromatogramSet ChangeMSDataFileInfos(IList<ChromFileInfo> prop)
        {
            return ChangeProp(ImClone(this), im => im.MSDataFileInfos = prop);
        }

        public ChromatogramSet ChangeMSDataFilePaths(IList<MsDataFileUri> prop)
        {
            var set = ImClone(this);

            // Be sure to preserve existing file info objects
            var dictPathToFileInfo = MSDataFileInfos.ToLookup(info => info.FilePath.GetLocation());
            var listFileInfos = new List<ChromFileInfo>();
            foreach (var filePath in prop)
            {
                ChromFileInfo chromFileInfo = dictPathToFileInfo[filePath.GetLocation()].FirstOrDefault();
                if (chromFileInfo == null)
                {
                    chromFileInfo = new ChromFileInfo(filePath);
                }
                else
                {
                    if (!Equals(chromFileInfo.FilePath, filePath))
                    {
                        chromFileInfo = chromFileInfo.ChangeFilePath(filePath);
                    }
                }
                listFileInfos.Add(chromFileInfo);
            }

            set.MSDataFileInfos = listFileInfos;

            if (ReferenceEquals(MSDataFileInfos, set.MSDataFileInfos))
                return this;

            return set;
        }

        public ChromatogramSet ChangeFileCacheFlags(IDictionary<MsDataFileUri, ChromCachedFile> cachedPaths,
            HashSet<string> cachedFileNames, string cachePath)
        {
            var set = ImClone(this);

            set.CalcCachedFlags(cachedPaths, cachedFileNames, cachePath);

            if (ReferenceEquals(MSDataFileInfos, set.MSDataFileInfos))
                return this;

            return set;
        }

        /// <summary>
        /// Calculates the fileCacheFlags from a collection of cached paths.
        /// This function modifies 'this', and should only be called on a clone
        /// before it is returned from a change operation.
        /// <para>
        /// If a path is not found in the cached set, its name is checked in
        /// the names set, and if found, then disk is checked to see if the
        /// file location has been moved.</para>
        /// </summary>
        /// <param name="cachedPaths">Set of known cached paths</param>
        /// <param name="cachedFileNames">Set of known cached file names</param>
        /// <param name="cachePath">Final cache path</param>
        private void CalcCachedFlags(IDictionary<MsDataFileUri, ChromCachedFile> cachedPaths,
            ICollection<string> cachedFileNames, string cachePath)
        {
            ChromFileInfo[] fileInfos = new ChromFileInfo[FileCount];
            for (int i = 0; i < fileInfos.Length; i++)
            {
                fileInfos[i] = MSDataFileInfos[i];
                
                var path = fileInfos[i].FilePath.GetLocation(); // Strips any decoration like "?centroid_ms1=true" etc

                ChromCachedFile fileInfo;
                if (cachedPaths.TryGetValue(path, out fileInfo))
                    fileInfos[i] = fileInfos[i].ChangeInfo(fileInfo);
                else if (cachedFileNames == null || cachedFileNames.Contains(path.GetFileName()))
                {
                    // If the name but not the file was found, check for an
                    // existing file in the cache file's directory.
                    var dataFilePath = GetExistingDataFilePath(cachePath, path);
                    if (dataFilePath != null && cachedPaths.TryGetValue(dataFilePath, out fileInfo))
                        fileInfos[i] = fileInfos[i].ChangeInfo(fileInfo);
                }
            }

            if (!ArrayUtil.EqualsDeep(MSDataFileInfos, fileInfos))
                MSDataFileInfos = fileInfos;
        }

        public ChromatogramSet ChangeOptimizationFunction(OptimizableRegression prop)
        {
            return ChangeProp(ImClone(this), im => im.OptimizationFunction = prop);
        }

        public ChromatogramSet ChangeAnnotations(Annotations prop)
        {
            return ChangeProp(ImClone(this), im => im.Annotations = prop);
        }

        public ChromatogramSet ChangeUseForRetentionTimeFilter(bool value)
        {
            return ChangeProp(ImClone(this), im => im.UseForRetentionTimeFilter = value);
        }

        public ChromatogramSet ChangeRescoreCount()
        {
            return ChangeProp(ImClone(this), im => im._rescoreCount++);
        }

        public ChromatogramSet ChangeAnalyteConcentration(double? concentration)
        {
            return ChangeProp(ImClone(this), im => im.AnalyteConcentration = concentration);
        }

        public ChromatogramSet ChangeSampleType(SampleType sampleType)
        {
            return ChangeProp(ImClone(this), im => im.SampleType = sampleType ?? SampleType.DEFAULT);
        }

        public ChromatogramSet ChangeDilutionFactor(double dilutionFactor)
        {
            return ChangeProp(ImClone(this), im => im.SampleDilutionFactor = dilutionFactor);
        }

        public ChromatogramSet ChangeBatchName(string batchName)
        {
            return ChangeProp(ImClone(this), im => im.BatchName = string.IsNullOrEmpty(batchName) ? null : batchName);
        }

        #endregion

        public static MsDataFileUri GetExistingDataFilePath(string cachePath, MsDataFileUri msDataFileUri)
        {
            MsDataFilePath msDataFilePath = msDataFileUri as MsDataFilePath;
            if (null == msDataFilePath)
            {
                return msDataFileUri;
            }

            return GetExistingDataFilePath(cachePath, msDataFilePath, out _);
        }
        
        /// <summary>
        /// Gets a full MSDataFile path to an existing file.  If the original path is not found to
        /// exist, the folder containing the chromatogram cache is tried with the filename part.
        /// </summary>
        /// <param name="cachePath">The path to the cache file</param>
        /// <param name="dataFilePath">A full MSDataFile path, potentially including a sample part</param>
        /// <param name="dataFilePathPart">A file path only to an existing file</param>
        /// <returns>A full MSDataFile path, potentially including a sample part, to an existing file, or null if no file is found</returns>
        public static MsDataFilePath GetExistingDataFilePath(string cachePath, MsDataFilePath dataFilePath, out string dataFilePathPart)
        {
            // Check file (directory for Waters) existence, because ProteoWizard can hang on this
            if (File.Exists(dataFilePath.FilePath) || Directory.Exists(dataFilePath.FilePath))
            {
                dataFilePathPart = dataFilePath.FilePath;
                return dataFilePath;
            }

            string dataFileName = Path.GetFileName(dataFilePath.FilePath);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (null != dataFileName)
            {
                // Check the most common case where the file is in the same directory
                // where the cache is being written.
                // Also, for tests, check Program.ExtraRawFileSearchFolder
                foreach (string folder in new[] { 
                    Path.GetDirectoryName(cachePath), 
                    Program.ExtraRawFileSearchFolder })
                {
                    if (null == folder)
                    {
                        continue;
                    }
                    var pathToCheck = Path.Combine(folder, dataFileName);
                    if (File.Exists(pathToCheck) || Directory.Exists(pathToCheck))
                    {
                        dataFilePathPart = pathToCheck;
                        return dataFilePath.SetFilePath(pathToCheck);
                    }
                }
            }
            dataFilePathPart = dataFilePath.FilePath;
            return null;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private ChromatogramSet()
            : base(new ChromatogramSetId())
        {
        }

        public static ChromatogramSet Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ChromatogramSet());
        }

        public enum EL
        {
            sample_file,
            replicate_file,
            chromatogram_file,
            instrument_info_list,
            instrument_info,
            model,
            ionsource,
            analyzer,
            detector,
            alignment,
        }

        public enum ATTR
        {
            id,
            file_path,
            sample_name,
            modified_time,
            acquired_time,
            import_time,
            cvid,
            name,
            value,
            use_for_retention_time_prediction,
            analyte_concentration,
            sample_type,
            has_midas_spectra,
            explicit_global_standard_area,
            tic_area,
            ion_mobility_type,
            sample_dilution_factor,
            batch_name,
            sample_id,
            instrument_serial_number,
        }

        private static readonly IXmlElementHelper<OptimizableRegression>[] OPTIMIZATION_HELPERS =
        {
            new XmlElementHelperSuper<CollisionEnergyRegression, OptimizableRegression>(),
            new XmlElementHelperSuper<DeclusteringPotentialRegression, OptimizableRegression>(),
            new XmlElementHelperSuper<CompensationVoltageRegressionRough, OptimizableRegression>(),
            new XmlElementHelperSuper<CompensationVoltageRegressionMedium, OptimizableRegression>(),
            new XmlElementHelperSuper<CompensationVoltageRegressionFine, OptimizableRegression>(),
        };

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            UseForRetentionTimeFilter = reader.GetBoolAttribute(ATTR.use_for_retention_time_prediction, false);
            AnalyteConcentration = reader.GetNullableDoubleAttribute(ATTR.analyte_concentration);
            SampleType = SampleType.FromName(reader.GetAttribute(ATTR.sample_type));
            SampleDilutionFactor = reader.GetDoubleAttribute(ATTR.sample_dilution_factor, DEFAULT_DILUTION_FACTOR);
            BatchName = reader.GetAttribute(ATTR.batch_name);
            // Consume tag
            reader.Read();

            // Check if there is an optimization function element, and read
            // if if there is.
            IXmlElementHelper<OptimizableRegression> helper =
                reader.FindHelper(OPTIMIZATION_HELPERS);
            if (helper != null)
                OptimizationFunction = helper.Deserialize(reader);

            var chromFileInfos = new List<ChromFileInfo>();
            var fileLoadIds = new List<string>();
            while (reader.IsStartElement(EL.sample_file) ||
                    reader.IsStartElement(EL.replicate_file) ||
                    reader.IsStartElement(EL.chromatogram_file))
            {
                // Note that the file path may actually be a URI that encodes things like lockmass correction as well as filename
                var fileUri = MsDataFileUri.Parse(reader.GetAttribute(ATTR.file_path));
                // BACKWARD COMPATIBILITY: Deal with legacy parameters which got stored on the file_path URI
                var filePath = fileUri as MsDataFilePath;
                if (filePath != null)
                    fileUri = filePath.RemoveLegacyParameters();
                ChromFileInfo chromFileInfo = new ChromFileInfo(fileUri);
                chromFileInfo = chromFileInfo.ChangeHasMidasSpectra(reader.GetBoolAttribute(ATTR.has_midas_spectra, false));
                var imUnitsStr = reader.GetAttribute(ATTR.ion_mobility_type);
                var imUnits = SmallMoleculeTransitionListReader.IonMobilityUnitsFromAttributeValue(imUnitsStr);
                chromFileInfo = chromFileInfo.ChangeIonMobilityUnits(imUnits);
                chromFileInfo = chromFileInfo.ChangeExplicitGlobalStandardArea(
                    reader.GetNullableDoubleAttribute(ATTR.explicit_global_standard_area));
                chromFileInfo = chromFileInfo.ChangeTicArea(reader.GetNullableDoubleAttribute(ATTR.tic_area));
                chromFileInfo = chromFileInfo.ChangeSampleId(reader.GetAttribute(ATTR.sample_id));
                chromFileInfo = chromFileInfo.ChangeSerialNumber(reader.GetAttribute(ATTR.instrument_serial_number));
                var strImportTime = reader.GetAttribute(ATTR.import_time);
                if (strImportTime != null)
                {
                    chromFileInfo = chromFileInfo.ChangeImportTime(XmlConvert.ToDateTime(strImportTime,
                        XmlDateTimeSerializationMode.RoundtripKind));
                }
                chromFileInfos.Add(chromFileInfo);
                
                string id = reader.GetAttribute(ATTR.id) ?? GetOrdinalSaveId(fileLoadIds.Count);
                fileLoadIds.Add(id);
                reader.Read();
                if (reader.IsStartElement(EL.instrument_info_list))
                {
                    reader.Skip();
                    reader.Read();
                } 
            }
            Annotations = DocumentReader.ReadAnnotations(reader);

            MSDataFileInfos = chromFileInfos;
            _fileLoadIds = fileLoadIds.ToArray();

            // Consume end tag
            reader.ReadEndElement();
        }

        public ChromatogramSet RestoreLegacyUriParameters()
        {
            return ChangeMSDataFileInfos(MSDataFileInfos.Select(fi => fi.RestoreLegacyUriParameters()).ToArray());
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.use_for_retention_time_prediction, false);
            writer.WriteAttributeNullable(ATTR.analyte_concentration, AnalyteConcentration);
            if (null != SampleType && !Equals(SampleType, SampleType.DEFAULT))
            {
                writer.WriteAttribute(ATTR.sample_type, SampleType.Name);
            }
            writer.WriteAttribute(ATTR.sample_dilution_factor, SampleDilutionFactor, DEFAULT_DILUTION_FACTOR);
            writer.WriteAttributeIfString(ATTR.batch_name, BatchName);

            // Write optimization element, if present
            if (OptimizationFunction != null)
            {
                IXmlElementHelper<OptimizableRegression> helper = XmlUtil.FindHelper(
                    OptimizationFunction, OPTIMIZATION_HELPERS);
                if (helper == null)
                    throw new InvalidOperationException(ResultsResources.ChromatogramSet_WriteXml_Attempt_to_serialize_list_containing_invalid_type);
                writer.WriteElement(helper.ElementNames[0], OptimizationFunction);                
            }

            int i = 0;
            foreach (var fileInfo in MSDataFileInfos)
            {
                writer.WriteStartElement(EL.sample_file);
                writer.WriteAttribute(ATTR.id, GetOrdinalSaveId(i++));
                writer.WriteAttribute(ATTR.file_path, fileInfo.FilePath);
                writer.WriteAttribute(ATTR.sample_name, fileInfo.FilePath.GetSampleOrFileName());
                writer.WriteAttributeIfString(ATTR.sample_id, fileInfo.SampleId);
                writer.WriteAttributeIfString(ATTR.instrument_serial_number, fileInfo.InstrumentSerialNumber);
                if (fileInfo.RunStartTime != null)
                {
                    writer.WriteAttribute(ATTR.acquired_time, XmlConvert.ToString((DateTime) fileInfo.RunStartTime, @"yyyy-MM-ddTHH:mm:ss"));
                }
                if(fileInfo.FileWriteTime != null)
                {
                    writer.WriteAttribute(ATTR.modified_time, XmlConvert.ToString((DateTime)fileInfo.FileWriteTime, @"yyyy-MM-ddTHH:mm:ss"));
                }

                if (fileInfo.ImportTime.HasValue)
                {
                    writer.WriteAttribute(ATTR.import_time, XmlConvert.ToString(fileInfo.ImportTime.Value, XmlDateTimeSerializationMode.RoundtripKind));
                }
                writer.WriteAttribute(ATTR.has_midas_spectra, fileInfo.HasMidasSpectra, false);
                writer.WriteAttributeNullable(ATTR.explicit_global_standard_area, fileInfo.ExplicitGlobalStandardArea);
                writer.WriteAttributeNullable(ATTR.tic_area, fileInfo.TicArea);
                if (fileInfo.IonMobilityUnits != eIonMobilityUnits.none)
                    writer.WriteAttribute(ATTR.ion_mobility_type, fileInfo.IonMobilityUnits.ToString());

                // instrument information
                WriteInstrumentConfigList(writer, fileInfo.InstrumentInfoList);

                writer.WriteEndElement();
            }
            DocumentWriter.WriteAnnotations(writer, Annotations);
        }

        private void WriteInstrumentConfigList(XmlWriter writer, IList<MsInstrumentConfigInfo> instrumentInfoList)
        {
            if (instrumentInfoList == null || instrumentInfoList.Count == 0)
                return;

            writer.WriteStartElement(EL.instrument_info_list);

            foreach (var instrumentInfo in instrumentInfoList)
            {
                writer.WriteStartElement(EL.instrument_info);

                if(!string.IsNullOrWhiteSpace(instrumentInfo.Model))
                    writer.WriteElementString(EL.model, instrumentInfo.Model);

                if(!string.IsNullOrWhiteSpace(instrumentInfo.Ionization))
                    writer.WriteElementString(EL.ionsource, instrumentInfo.Ionization);

                if (!string.IsNullOrWhiteSpace(instrumentInfo.Analyzer))
                    writer.WriteElementString(EL.analyzer, instrumentInfo.Analyzer);

                if (!string.IsNullOrWhiteSpace(instrumentInfo.Detector))
                    writer.WriteElementString(EL.detector, instrumentInfo.Detector);

                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private string GetOrdinalSaveId(int ordinalIndex)
        {
            if (ordinalIndex == -1)
                throw new ArgumentOutOfRangeException(nameof(ordinalIndex),
                                                      ResultsResources.ChromatogramSet_GetOrdinalSaveId_Attempting_to_save_results_info_for_a_file_that_cannot_be_found);

            return string.Format(@"{0}_f{1}", Helpers.MakeXmlId(Name), ordinalIndex);
        }

        public string GetFileSaveId(ChromFileInfoId fileId)
        {
            return GetOrdinalSaveId(IndexOfId(fileId));
        }

        #endregion

        #region object overrides

        public bool Equals(ChromatogramSet obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            // Why isn't "OptimizationFunction" included in "Equals"?
            if (!base.Equals(obj))
                return false;
            if (!ArrayUtil.EqualsDeep(obj.MSDataFileInfos, MSDataFileInfos))
                return false; 
            if (!Equals(obj.Annotations, Annotations))
                return false;
            if (!obj.UseForRetentionTimeFilter == UseForRetentionTimeFilter)
                return false;
            if (!Equals(obj.OptimizationFunction, OptimizationFunction))
                return false;
            if (obj._rescoreCount != _rescoreCount)
                return false;
            if (!Equals(obj.AnalyteConcentration, AnalyteConcentration))
                return false;
            if (!Equals(obj.SampleType, SampleType))
                return false;
            if (!Equals(obj.SampleDilutionFactor, SampleDilutionFactor))
                return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as ChromatogramSet);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ MSDataFileInfos.GetHashCodeDeep();
                result = (result*397) ^ Annotations.GetHashCode();
                result = (result*397) ^ UseForRetentionTimeFilter.GetHashCode();
                result = (result*397) ^ (OptimizationFunction != null ? OptimizationFunction.GetHashCode() : 0);
                result = (result*397) ^ _rescoreCount;
                result = (result*397) ^ AnalyteConcentration.GetHashCode();
                result = (result*397) ^ SampleType.GetHashCode();
                result = (result*397) ^ SampleDilutionFactor.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// Identity class to allow identity equality on <see cref="ChromatogramSetId"/>.
    /// </summary>
    public sealed class ChromatogramSetId : Identity
    {        
    }

    public sealed class ChromFileInfo : DocNode, IPathContainer
    {
        public ChromFileInfo(MsDataFileUri filePath)
            : base(new ChromFileInfoId())
        {
            RemoteUrl remoteUrl = filePath as RemoteUrl;
            if (null != remoteUrl)
            {
                FileWriteTime = remoteUrl.ModifiedTime;
                filePath = remoteUrl.ChangeModifiedTime(null);
            }
            FilePath = filePath;
            InstrumentInfoList = new MsInstrumentConfigInfo[0];
        }

        private ImmutableList<MsInstrumentConfigInfo> _instrumentInfoList;

        public ChromFileInfoId FileId { get { return (ChromFileInfoId) Id; }}
        public int FileIndex { get { return Id.GlobalIndex; } }
        public MsDataFileUri FilePath { get; private set; }
        public DateTime? FileWriteTime { get; private set; }
        public DateTime? RunStartTime { get; private set; }
        public DateTime? ImportTime { get; private set; }
        public double MaxRetentionTime { get; private set; }
        public double MaxIntensity { get; private set; }
        public bool HasMidasSpectra { get; private set; }
        // Only used for File > Share to older versions, use ChromCachedFile versions instead in other cases
        public bool? UsedMs1Centroids { get; private set; }
        // Only used for File > Share to older versions, use ChromCachedFile versions instead in other cases
        public bool? UsedMs2Centroids { get; private set; }
        public double? ExplicitGlobalStandardArea { get; private set; }
        public double? TicArea { get; private set; }
        public eIonMobilityUnits IonMobilityUnits { get; private set; }
        public string SampleId { get; private set; }
        public string InstrumentSerialNumber { get; private set; }
        public bool IsSrm { get; private set; }

        public IList<MsInstrumentConfigInfo> InstrumentInfoList
        {
            get { return _instrumentInfoList; }
            private set { _instrumentInfoList = MakeReadOnly(value); }
        }

        public override AnnotationDef.AnnotationTarget AnnotationTarget
        {
            get { throw new InvalidOperationException(); }
        }

        public IList<KeyValuePair<ChromFileInfoId, RegressionLineElement>> RetentionTimeAlignments { get; private set; }

        #region Property change methods

        public ChromFileInfo ChangeFilePath(MsDataFileUri prop)
        {
            return ChangeProp(ImClone(this), im => im.FilePath = prop);
        }

        public ChromFileInfo ChangeHasMidasSpectra(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.HasMidasSpectra = prop);
        }

        public ChromFileInfo ChangeIonMobilityUnits(eIonMobilityUnits prop)
        {
            return ChangeProp(ImClone(this), im => im.IonMobilityUnits = prop);
        }

        public ChromFileInfo ChangeInfo(ChromCachedFile fileInfo)
        {
            return ChangeProp(ImClone(this), im =>
                                                 {
                                                     im.FilePath = fileInfo.FilePath;
                                                     im.FileWriteTime = fileInfo.FileWriteTime;
                                                     im.RunStartTime = fileInfo.RunStartTime;
                                                     im.InstrumentInfoList = fileInfo.InstrumentInfoList.ToArray();
                                                     im.MaxRetentionTime = fileInfo.MaxRetentionTime;
                                                     im.MaxIntensity = fileInfo.MaxIntensity;
                                                     im.HasMidasSpectra = fileInfo.HasMidasSpectra;
                                                     im.UsedMs1Centroids = fileInfo.UsedMs1Centroids;
                                                     im.UsedMs2Centroids = fileInfo.UsedMs2Centroids;
                                                     im.TicArea = fileInfo.TicArea;
                                                     im.IonMobilityUnits = fileInfo.IonMobilityUnits;
                                                     im.SampleId = fileInfo.SampleId;
                                                     im.InstrumentSerialNumber = fileInfo.InstrumentSerialNumber;
                                                     im.IsSrm = fileInfo.IsSrm;
                                                 });
        }

        public ChromFileInfo ChangeTicArea(double? ticArea)
        {
            return ChangeProp(ImClone(this), im => im.TicArea = ticArea);
        }

        public ChromFileInfo ChangeImportTime(DateTime? importTime)
        {
            return ChangeProp(ImClone(this), im => im.ImportTime = importTime);
        }

        public ChromFileInfo ChangeSampleId(string sampleId)
        {
            return ChangeProp(ImClone(this), im => im.SampleId = sampleId);
        }

        public ChromFileInfo ChangeSerialNumber(string serialNumber)
        {
            return ChangeProp(ImClone(this), im => im.InstrumentSerialNumber = serialNumber);
        }

        public ChromFileInfo ChangeRetentionTimeAlignments(IEnumerable<KeyValuePair<ChromFileInfoId, RegressionLineElement>> retentionTimeAlignments)
        {
            return ChangeProp(ImClone(this),
                              im =>
                                  {
                                      im.RetentionTimeAlignments = Array.AsReadOnly(retentionTimeAlignments.ToArray());
                                  });
        }

        public ChromFileInfo ChangeExplicitGlobalStandardArea(double? globalStandardArea)
        {
            return ChangeProp(ImClone(this), im => im.ExplicitGlobalStandardArea = globalStandardArea);
        }

        #endregion

        #region object overrides

        public bool Equals(ChromFileInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (!Equals(other.Id, Id))
                return false;
            if (!Equals(other.FilePath, FilePath))
                return false;
            if (!other.FileWriteTime.Equals(FileWriteTime))
                return false;
            if (!other.RunStartTime.Equals(RunStartTime))
                return false;
            if (!other.ImportTime.Equals(ImportTime))
                return false;
            if (!other.MaxIntensity.Equals(MaxIntensity))
                return false;
            if (!other.MaxRetentionTime.Equals(MaxRetentionTime))
                return false;
            if (HasMidasSpectra != other.HasMidasSpectra)
                return false;
            if (!Equals(UsedMs1Centroids, other.UsedMs1Centroids))
                return false;
            if (!Equals(UsedMs2Centroids, other.UsedMs2Centroids))
                return false;
            if (!Equals(ExplicitGlobalStandardArea, other.ExplicitGlobalStandardArea))
                return false;
            if (!Equals(TicArea, other.TicArea))
                return false;
            if (!Equals(IonMobilityUnits, other.IonMobilityUnits))
                return false;
            if (!Equals(SampleId, other.SampleId))
                return false;
            if (!Equals(InstrumentSerialNumber, other.InstrumentSerialNumber))
                return false;
            if (!ArrayUtil.EqualsDeep(other.InstrumentInfoList, InstrumentInfoList))
                return false;
            if (!ArrayUtil.EqualsDeep(other.RetentionTimeAlignments, RetentionTimeAlignments))
                return false;
            if (!IsSrm.Equals(other.IsSrm))
                return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ChromFileInfo)) return false;
            return Equals((ChromFileInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Id.GetHashCode();
                result = (result*397) ^ FilePath.GetHashCode();
                result = (result*397) ^ FileWriteTime.GetHashCode();
                result = (result*397) ^ RunStartTime.GetHashCode();
                result = (result*397) ^ ImportTime.GetHashCode();
                result = (result*397) ^
                         (InstrumentInfoList != null ? InstrumentInfoList.GetHashCodeDeep() : 0);
                result = (result*397) ^
                         (RetentionTimeAlignments == null ? 0 : RetentionTimeAlignments.GetHashCodeDeep());
                result = (result*397) ^ MaxIntensity.GetHashCode();
                result = (result*397) ^ MaxRetentionTime.GetHashCode();
                result = (result*397) ^ HasMidasSpectra.GetHashCode();
                result = (result*397) ^ UsedMs1Centroids.GetHashCode();
                result = (result*397) ^ UsedMs2Centroids.GetHashCode();
                result = (result*397) ^ ExplicitGlobalStandardArea.GetHashCode();
                result = (result*397) ^ TicArea.GetHashCode();
                result = (result*397) ^ IonMobilityUnits.GetHashCode();
                result = (result*397) ^ SampleId?.GetHashCode() ?? 0;
                result = (result*397) ^ InstrumentSerialNumber?.GetHashCode() ?? 0;
                result = (result*397) ^ IsSrm.GetHashCode();
                return result;
            }
        }

        #endregion

        public ChromFileInfo RestoreLegacyUriParameters()
        {
            var filePath = FilePath.RestoreLegacyParameters(UsedMs1Centroids ?? false, UsedMs2Centroids ?? false);
            if (!ReferenceEquals(filePath, FilePath))
                return ChangeFilePath(filePath);
            return this;
        }
    }

    /// <summary>
    /// Identity class to allow identity equality on <see cref="ChromFileInfo"/>.
    /// This ID is used in peak integration objects.  Since a change in this ID will
    /// cause the loss of all manual integration and annotations associated with the
    /// file, there is no suitable content information for this ID, since it is desirable
    /// to allow the path to a result file to change without losing manually entered
    /// information.
    /// </summary>
    public sealed class ChromFileInfoId : Identity
    {        
    }

    /// <summary>
    /// Helper functions for specifying a single sample injected into a mass
    /// spectrometer.
    /// 
    /// Ideally this would be represented with a complete object, but that would
    /// require both XML and cache format changes.  Implemented late in v0.5,
    /// the simplest solution is to encode the necessary information into the
    /// existing path string used to identify a single sample file.
    /// 
    /// It's now (v3.5) being expanded to include other information needed to reproducibly 
    /// read raw data - lockmass settings, for example.  Probably ought to be moved out to 
    /// MSDataFileUri, really
    /// 
    /// </summary>
    public static class SampleHelp
    {
        private const string TAG_LOCKMASS_POS = "lockmass_pos";
        private const string TAG_LOCKMASS_NEG = "lockmass_neg";
        private const string TAG_LOCKMASS_TOL = "lockmass_tol";
        private const string TAG_CENTROID_MS1 = "centroid_ms1";
        private const string TAG_CENTROID_MS2 = "centroid_ms2";
        private const string TAG_COMBINE_IMS = "combine_ims";   // LEGACY: Introduced temporarily in 19.1.9.338 and 350
        private const string VAL_TRUE = "true";

        public static string EncodePath(string filePath, string sampleName, int sampleIndex, LockMassParameters lockMassParameters)
        {
            return LegacyEncodePath(filePath, sampleName, sampleIndex, lockMassParameters, false, false, false);
        }

        /// <summary>
        /// Use directly only when access to combineIonMobilitySpectra is required for legacy testing
        /// </summary>
        public static string LegacyEncodePath(string filePath, string sampleName, int sampleIndex, LockMassParameters lockMassParameters,
            bool centroidMS1, bool centroidMS2, bool combineIonMobilitySpectra)
        {
            var parameters = new List<string>();
            const string pairFormat = "{0}={1}";
            string filePart;
            if (!(string.IsNullOrEmpty(sampleName) && -1 == sampleIndex))
            {
                // Info for distinguishing a single sample within a WIFF file.
                filePart = string.Format(@"{0}|{1}|{2}", filePath, sampleName ?? string.Empty, sampleIndex);
            }
            else
            {
                filePart = filePath;
            }

            if (lockMassParameters != null && !lockMassParameters.IsEmpty)
            {
                if (lockMassParameters.LockmassPositive.HasValue)
                    parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_LOCKMASS_POS, lockMassParameters.LockmassPositive.Value));
                if (lockMassParameters.LockmassNegative.HasValue)
                    parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_LOCKMASS_NEG, lockMassParameters.LockmassNegative.Value));
                if (lockMassParameters.LockmassTolerance.HasValue)
                    parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_LOCKMASS_TOL, lockMassParameters.LockmassTolerance.Value));
            }
            if (centroidMS1)
            {
                parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_CENTROID_MS1, VAL_TRUE));
            }
            if (centroidMS2)
            {
                parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_CENTROID_MS2, VAL_TRUE));
            }
            if (combineIonMobilitySpectra)
            {
                parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_COMBINE_IMS, VAL_TRUE));
            }

            return parameters.Any() ? string.Format(@"{0}?{1}", filePart, string.Join(@"&", parameters)) : filePart;
        }

        public static string EscapeSampleId(string sampleId)
        {
            var invalidFileChars = Path.GetInvalidFileNameChars();
            var invalidNameChars = new[] {',', '.', ';'};
            if (sampleId.IndexOfAny(invalidFileChars) == -1 &&
                    sampleId.IndexOfAny(invalidNameChars) == -1)
                return sampleId;
            var sb = new StringBuilder();
            foreach (char c in sampleId)
                sb.Append(invalidFileChars.Contains(c) || invalidNameChars.Contains(c) ? '_' : c);
            return sb.ToString();
        }

        public static string GetLocationPart(string path)
        {
            return path.Split('?')[0];
        }

        public static string GetPathFilePart(string path)
        {
            path = GetLocationPart(path); // Just in case the url args contain '|'
            if (path.IndexOf('|') == -1)
                return path;
            return path.Split('|')[0];
        }

        public static bool HasSamplePart(string path)
        {
            path = GetLocationPart(path); // Just in case the url args contain '|'
            string[] parts = path.Split('|');

            return parts.Length == 3 && int.TryParse(parts[2], out _);
        }

        public static string GetPathSampleNamePart(string path)
        {
            path = GetLocationPart(path); // Just in case the url args contain '|'
            if (path.IndexOf('|') == -1)
                return null;
            return path.Split('|')[1];
        }

        public static string GetPathSampleNamePart(MsDataFileUri msDataFileUri)
        {
            return msDataFileUri.GetSampleName();
        }

        public static int GetPathSampleIndexPart(string path)
        {
            path = GetLocationPart(path); // Just in case the url args contain '|'
            int sampleIndex = -1;
            if (path.IndexOf('|') != -1)
            {
                string[] parts = path.Split('|');
                int index;
                if (parts.Length == 3 && int.TryParse(parts[2], out index))
                    sampleIndex = index;
            }
            return sampleIndex;
        }        

        /// <summary>
        /// Returns just the file name from a path that may contain sample information.
        /// </summary>
        /// <param name="path">The full path with any sample information</param>
        /// <returns>The file name part</returns>
        public static string GetFileName(string path)
        {
            return Path.GetFileName(GetPathFilePart(path));
        }

        public static string GetFileName(MsDataFileUri msDataFileUri)
        {
            return msDataFileUri.GetFileName();
        }

        public static bool GetCentroidMs1(string path)
        {
            return ParseParameterBool(TAG_CENTROID_MS1, path) ?? false;
        }

        public static bool GetCentroidMs2(string path)
        {
            return ParseParameterBool(TAG_CENTROID_MS2, path) ?? false;
        }

        public static bool GetCombineIonMobilitySpectra(string path)
        {
            return ParseParameterBool(TAG_COMBINE_IMS, path) ?? false;
        }

        /// <summary>
        /// Returns a sample name for any file path, using either the available sample
        /// information on the path, or the file basename, if no sample information is present.
        /// </summary>
        /// <param name="path">The full path with any sample information</param>
        /// <returns>The sample name part or file basename</returns>
        public static string GetFileSampleName(string path)
        {
            return GetPathSampleNamePart(path) ?? Path.GetFileNameWithoutExtension(path);
        }

        private static string ParseParameter(string name, string url)
        {
            var parts = url.Split('?');
            if (parts.Length > 1)
            {
                var parameters = parts[1].Split('&');
                var parameter = parameters.FirstOrDefault(p => p.StartsWith(name));
                if (parameter != null)
                {
                    return parameter.Split('=')[1];
                }
            }
            return null;
        }

        private static bool? ParseParameterBool(string name, string url)
        {
            var valStr = ParseParameter(name, url);
            if (valStr != null)
            {
                return valStr.Equals(VAL_TRUE);
            }
            return null;
        }

        private static double? ParseParameterDouble(string name, string url)
        {
            var valStr = ParseParameter(name, url);
            if (valStr != null)
            {
                double dval;
                if (double.TryParse(valStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out dval))
                    return dval;
            }
            return null;
        }

        private static int? ParseParameterInt(string name, string url) 
        {
            var valStr = ParseParameter(name, url);
            if (valStr != null)
            {
                int ival;
                if (int.TryParse(valStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out ival))
                    return ival;
            }
            return null;
        }

        public static LockMassParameters GetLockmassParameters(string url)
        {
            if (url == null || string.IsNullOrEmpty(url))
                return LockMassParameters.EMPTY;
            return new LockMassParameters(ParseParameterDouble(TAG_LOCKMASS_POS, url), ParseParameterDouble(TAG_LOCKMASS_NEG, url), ParseParameterDouble(TAG_LOCKMASS_TOL, url));
        }
    }
}
