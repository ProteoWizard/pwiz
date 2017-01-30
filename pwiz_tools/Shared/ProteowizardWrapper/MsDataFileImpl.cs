﻿/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;
using System.Threading;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using pwiz.Common.SystemUtil;

namespace pwiz.ProteowizardWrapper
{
    /// <summary>
    /// This is our wrapper class for ProteoWizard's MSData file reader interface.
    /// 
    /// Performance measurements can be made here, see notes below on enabling that.   
    /// 
    /// When performance measurement is enabled, the GetLog() method can be called
    /// after read operations have been completed. This returns a handy CSV-formatted
    /// report on file read performance.
    /// </summary>
    public class MsDataFileImpl : IDisposable
    {
        private static readonly ReaderList FULL_READER_LIST = ReaderList.FullReaderList;

        // By default this creates dummy non-functional performance timers.
        // Place "MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = false;" in 
        // the calling code to enable performance measurement.
        public static PerfUtilFactory PerfUtilFactory { get; private set; }

        static MsDataFileImpl()
        {
            PerfUtilFactory = new PerfUtilFactory();
        }

        // Cached disposable objects
        private MSData _msDataFile;
        private readonly ReaderConfig _config;
        private SpectrumList _spectrumList;
        private ChromatogramList _chromatogramList;
        private MsDataScanCache _scanCache;
        private readonly IPerfUtil _perf; // for performance measurement, dummied by default
        private readonly LockMassParameters _lockmassParameters; // For Waters lockmass correction
        private int? _lockmassFunction;  // For Waters lockmass correction

        private readonly bool _requireVendorCentroidedMS1;
        private readonly bool _requireVendorCentroidedMS2;

        private DetailLevel _detailMsLevel = DetailLevel.InstantMetadata;

        private DetailLevel _detailStartTime = DetailLevel.InstantMetadata;

        private DetailLevel _detailDriftTime = DetailLevel.InstantMetadata;

        private static double[] ToArray(BinaryDataArray binaryDataArray)
        {
            return binaryDataArray.data.ToArray();
        }

        private static float[] ToFloatArray(IList<double> list)
        {
            float[] result = new float[list.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = (float) list[i];
            return result;
        }

        public static string[] ReadIds(string path)
        {
            return FULL_READER_LIST.readIds(path);
        }

        public const string PREFIX_TOTAL = "SRM TIC "; // Not L10N
        public const string PREFIX_SINGLE = "SRM SIC "; // Not L10N
        public const string PREFIX_PRECURSOR = "SIM SIC "; // Not L10N


        public static bool? IsNegativeChargeIdNullable(string id)
        {
            if (id.StartsWith("+ ")) // Not L10N
                return false;
            if (id.StartsWith("- ")) // Not L10N
                return true;
            return null;
        }

        public static bool IsSingleIonCurrentId(string id)
        {
            if (IsNegativeChargeIdNullable(id).HasValue)
                id = id.Substring(2);
            return id.StartsWith(PREFIX_SINGLE) || id.StartsWith(PREFIX_PRECURSOR);
        }

        public MsDataFileImpl(string path, int sampleIndex = 0, LockMassParameters lockmassParameters = null,
            bool simAsSpectra = false, bool srmAsSpectra = false, bool acceptZeroLengthSpectra = true,
            bool requireVendorCentroidedMS1 = false, bool requireVendorCentroidedMS2 = false,
            bool ignoreZeroIntensityPoints = false)
        {
            // see note above on enabling performance measurement
            _perf = PerfUtilFactory.CreatePerfUtil("MsDataFileImpl " + // Not L10N 
                string.Format("{0},sampleIndex:{1},lockmassCorrection:{2},simAsSpectra:{3},srmAsSpectra:{4},acceptZeroLengthSpectra:{5},requireVendorCentroidedMS1:{6},requireVendorCentroidedMS2:{7}",  // Not L10N
                path, sampleIndex, !(lockmassParameters == null || lockmassParameters.IsEmpty), simAsSpectra, srmAsSpectra, acceptZeroLengthSpectra, requireVendorCentroidedMS1, requireVendorCentroidedMS2));
            using (_perf.CreateTimer("open")) // Not L10N
            {
                FilePath = path;
                _msDataFile = new MSData();
                _config = new ReaderConfig
                {
                    simAsSpectra = simAsSpectra,
                    srmAsSpectra = srmAsSpectra,
                    acceptZeroLengthSpectra = acceptZeroLengthSpectra,
                    ignoreZeroIntensityPoints = ignoreZeroIntensityPoints
                };
                _lockmassParameters = lockmassParameters;
                FULL_READER_LIST.read(path, _msDataFile, sampleIndex, _config);
                _requireVendorCentroidedMS1 = requireVendorCentroidedMS1;
                _requireVendorCentroidedMS2 = requireVendorCentroidedMS2;
            }
        }

        /// <summary>
        /// get the accumulated performance log, if any (see note above on enabling this)
        /// </summary>
        /// <returns>CSV-formatted multiline string with performance information, if any</returns>
        public string GetLog()
        {
            if (_perf != null)
                return _perf.GetLog();
            return null;
        }

        public void EnableCaching(int? cacheSize)
        {
            if (cacheSize == null || cacheSize.Value <= 0)
            {
                _scanCache = new MsDataScanCache();
            }
            else
            {
                _scanCache = new MsDataScanCache(cacheSize.Value);
            }
        }

        public void DisableCaching()
        {
            _scanCache.Clear();
            _scanCache = null;
        }

        public string RunId { get { return _msDataFile.run.id; } }

        public DateTime? RunStartTime
        {
            get
            {
                string stampText = _msDataFile.run.startTimeStamp;
                DateTime runStartTime;
                if (!DateTime.TryParse(stampText, CultureInfo.InvariantCulture, DateTimeStyles.None, out runStartTime) &&
                    !DateTime.TryParse(stampText, out runStartTime))
                    return null;
                return runStartTime;
            }
        }

        public MsDataConfigInfo ConfigInfo
        {
            get
            {
                int spectra = SpectrumList.size();
                string ionSource = string.Empty;
                string analyzer = string.Empty;
                string detector = string.Empty;
                foreach (InstrumentConfiguration ic in _msDataFile.instrumentConfigurationList)
                {
                    string instrumentIonSource;
                    string instrumentAnalyzer;
                    string instrumentDetector;
                    GetInstrumentConfig(ic, out instrumentIonSource, out instrumentAnalyzer, out instrumentDetector);

                    if (ionSource.Length > 0)
                        ionSource += ", "; // Not L10N
                    ionSource += instrumentIonSource;

                    if (analyzer.Length > 0)
                        analyzer += ", "; // Not L10N
                    analyzer += instrumentAnalyzer;

                    if (detector.Length > 0)
                        detector += ", "; // Not L10N
                    detector += instrumentDetector;
                }

                HashSet<string> contentTypeSet = new HashSet<string>();
                foreach (CVParam term in _msDataFile.fileDescription.fileContent.cvParams)
                    contentTypeSet.Add(term.name);
                var contentTypes = contentTypeSet.ToArray();
                Array.Sort(contentTypes);
                string contentType = String.Join(", ", contentTypes); // Not L10N

                return new MsDataConfigInfo
                           {
                               Analyzer = analyzer,
                               ContentType = contentType,
                               Detector = detector,
                               IonSource = ionSource,
                               Spectra = spectra
                           };
            }
        }

        private static void GetInstrumentConfig(InstrumentConfiguration ic, out string ionSource, out string analyzer, out string detector)
        {
            // ReSharper disable CollectionNeverQueried.Local  (why does ReSharper warn on this?)
            SortedDictionary<int, string> ionSources = new SortedDictionary<int, string>();
            SortedDictionary<int, string> analyzers = new SortedDictionary<int, string>();
            SortedDictionary<int, string> detectors = new SortedDictionary<int, string>();
            // ReSharper restore CollectionNeverQueried.Local

            foreach (Component c in ic.componentList)
            {
                CVParam term;
                switch (c.type)
                {
                    case ComponentType.ComponentType_Source:
                        term = c.cvParamChild(CVID.MS_ionization_type);
                        if (!term.empty())
                            ionSources.Add(c.order, term.name);
                        else
                        {
                            // If we did not find the ion source in a CVParam it may be in a UserParam
                            UserParam uParam = c.userParam("msIonisation"); // Not L10N
                            if (HasInfo(uParam))
                            {
                                ionSources.Add(c.order, uParam.value);
                            }
                        }
                        break;
                    case ComponentType.ComponentType_Analyzer:
                        term = c.cvParamChild(CVID.MS_mass_analyzer_type);
                        if (!term.empty())
                            analyzers.Add(c.order, term.name);
                        else
                        {
                            // If we did not find the analyzer in a CVParam it may be in a UserParam
                            UserParam uParam = c.userParam("msMassAnalyzer"); // Not L10N
                            if (HasInfo(uParam))
                            {
                                analyzers.Add(c.order, uParam.value);
                            }
                        }
                        break;
                    case ComponentType.ComponentType_Detector:
                        term = c.cvParamChild(CVID.MS_detector_type);
                        if (!term.empty())
                            detectors.Add(c.order, term.name);
                        else
                        {
                            // If we did not find the detector in a CVParam it may be in a UserParam
                            UserParam uParam = c.userParam("msDetector"); // Not L10N
                            if (HasInfo(uParam))
                            {
                                detectors.Add(c.order, uParam.value);
                            }
                        }
                        break;
                }
            }

            ionSource = String.Join("/", new List<string>(ionSources.Values).ToArray()); // Not L10N

            analyzer = String.Join("/", new List<string>(analyzers.Values).ToArray()); // Not L10N

            detector = String.Join("/", new List<string>(detectors.Values).ToArray()); // Not L10N
        }

        public bool IsProcessedBy(string softwareName)
        {
            foreach (var softwareApp in _msDataFile.softwareList)
            {
                if (softwareApp.id.Contains(softwareName))
                    return true;
            }
            return false;
        }

        public bool IsWatersLockmassSpectrum(MsDataSpectrum s)
        {
            return _lockmassFunction.HasValue && (s.WatersFunctionNumber >= _lockmassFunction.Value);
        }

        /// <summary>
        /// Record any instrument info found in the file, along with any Waters lockmass info we have
        /// </summary>
        public IEnumerable<MsInstrumentConfigInfo> GetInstrumentConfigInfoList()
        {
            using (_perf.CreateTimer("GetInstrumentConfigList")) // Not L10N
            {
                IList<MsInstrumentConfigInfo> configList = new List<MsInstrumentConfigInfo>();

                foreach (InstrumentConfiguration ic in _msDataFile.instrumentConfigurationList)
                {
                    string instrumentModel = null;
                    string ionization;
                    string analyzer;
                    string detector;

                    CVParam param = ic.cvParamChild(CVID.MS_instrument_model);
                    if (!param.empty() && param.cvid != CVID.MS_instrument_model)
                    {
                        instrumentModel = param.name;
                    }
                    if(instrumentModel == null)
                    {
                        // If we did not find the instrument model in a CVParam it may be in a UserParam
                        UserParam uParam = ic.userParam("msModel"); // Not L10N
                        if (HasInfo(uParam))
                        {
                            instrumentModel = uParam.value;
                        }
                    }

                    // get the ionization type, analyzer and detector
                    GetInstrumentConfig(ic, out ionization, out analyzer, out detector);

                    if (instrumentModel != null || ionization != null || analyzer != null || detector != null)
                    {
                        configList.Add(new MsInstrumentConfigInfo(instrumentModel, ionization, analyzer, detector));
                    }
                }
                return configList;
            }
        }

        private static bool HasInfo(UserParam uParam)
        {
            return !uParam.empty() && !String.IsNullOrEmpty(uParam.value) &&
                   !String.Equals("unknown", uParam.value.ToString().ToLowerInvariant()); // Not L10N
        }

        public bool IsABFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_ABI_WIFF_format)); }
        }

        public bool IsMzWiffXml
        {
            get { return IsProcessedBy("mzWiff"); } // Not L10N
        }

        public bool IsAgilentFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Agilent_MassHunter_format)); }
        }

        public bool IsThermoFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Thermo_RAW_format)); }
        }

        public bool IsWatersFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Waters_raw_format)); }
        }

        public bool IsWatersLockmassCorrectionCandidate
        {
            get
            {
                try
                {
                    // Has to be a .raw file, not just an mzML translation of one
                    return (FilePath.ToLowerInvariant().EndsWith(".raw")) && // Not L10N
                        IsWatersFile &&
                        _msDataFile.run.spectrumList != null &&
                        !_msDataFile.run.spectrumList.empty() &&
                        !HasChromatogramData && 
                        !HasSrmSpectra;
                }
                catch (Exception)
                {
                    // Whatever that was, it wasn't a Waters file
                    return false;
                }
            }
        }

        public bool IsShimadzuFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Shimadzu_Biotech_nativeID_format)); }
        }

        private ChromatogramList ChromatogramList
        {
            get
            {
                return _chromatogramList = _chromatogramList ??
                    _msDataFile.run.chromatogramList;
            }
        }

        private SpectrumList SpectrumList
        {
            get
            {
                if (_spectrumList == null)
                {
                    // CONSIDER(bspratt): there is no acceptable wrapping order when both centroiding and lockmass are needed at the same time 
                    // (For now, this can't happen in practice, as Waters offers no centroiding, but someday this may force pwiz API rework)
                    var centroidLevel = new List<int>();
                    _spectrumList = _msDataFile.run.spectrumList;
                    bool hasSrmSpectra = HasSrmSpectraInList(_spectrumList);
                    if (!hasSrmSpectra)
                    {
                        if (_requireVendorCentroidedMS1)
                            centroidLevel.Add(1);
                        if (_requireVendorCentroidedMS2)
                            centroidLevel.Add(2);
                    }
                    if (centroidLevel.Any())
                    {
                        _spectrumList = new SpectrumList_PeakPicker(_spectrumList,
                            new VendorOnlyPeakDetector(), // Throws an exception when no vendor centroiding available
                            true, centroidLevel.ToArray());
                    }

                    _lockmassFunction = null;
                    if (_lockmassParameters != null && !_lockmassParameters.IsEmpty)
                    {
                        _spectrumList = new SpectrumList_LockmassRefiner(_spectrumList,
                            _lockmassParameters.LockmassPositive ?? 0,
                            _lockmassParameters.LockmassNegative ?? 0,
                            _lockmassParameters.LockmassTolerance ?? LockMassParameters.LOCKMASS_TOLERANCE_DEFAULT);
                    }
                    if (IsWatersFile)
                    {
                        if (_spectrumList.size() > 0 && !hasSrmSpectra)
                        {
                            // If the first seen spectrum has MS1 data and function > 1 assume it's the lockspray function, 
                            // and thus to be omitted from chromatogram extraction.
                            // N.B. for msE data we will always assume function 3 and greater are to be omitted
                            // CONSIDER(bspratt) I really wish there was some way to communicate decisions like this to the user
                            using (var spectrum = _spectrumList.spectrum(0, DetailLevel.FullMetadata))
                            {
                                if (GetMsLevel(spectrum) == 1)
                                {
                                    var function = MsDataSpectrum.WatersFunctionNumberFromId(id.abbreviate(spectrum.id));
                                    if (function > 1)
                                        _lockmassFunction = function; // Ignore all scans in this function for chromatogram extraction purposes
                                }
                            }
                        }
                    }
                }
                return _spectrumList;
            }
        }

        public double? GetMaxDriftTime()
        {
            return GetMaxDriftTimeInList(SpectrumList);
        }

        public int ChromatogramCount
        {
            get { return ChromatogramList != null ? ChromatogramList.size() : 0; }
        }

        public string GetChromatogramId(int index, out int indexId)
        {
            using (var cid = ChromatogramList.chromatogramIdentity(index))
            {
                indexId = cid.index;
                return cid.id;                
            }
        }

        public void GetChromatogram(int chromIndex, out string id,
            out float[] timeArray, out float[] intensityArray)
        {
            using (Chromatogram chrom = ChromatogramList.chromatogram(chromIndex, true))
            {
                id = chrom.id;
                timeArray = ToFloatArray(chrom.binaryDataArrays[0].data);
                intensityArray = ToFloatArray(chrom.binaryDataArrays[1].data);
            }            
        }

        /// <summary>
        /// Gets the retention times from the first chromatogram in the data file.
        /// Returns null if there are no chromatograms in the file.
        /// </summary>
        public double[] GetScanTimes()
        {
            using (_perf.CreateTimer("GetScanTimes"))   // Not L10N
            {
                if (ChromatogramList == null || ChromatogramList.empty())
                {
                    return null;
                }
                using (var chromatogram = ChromatogramList.chromatogram(0, true))
                {
                    if (chromatogram == null)
                    {
                        return null;
                    }
                    TimeIntensityPairList timeIntensityPairList = new TimeIntensityPairList();
                    chromatogram.getTimeIntensityPairs(ref timeIntensityPairList);
                    double[] times = new double[timeIntensityPairList.Count];
                    for (int i = 0; i < times.Length; i++)
                    {
                        times[i] = timeIntensityPairList[i].time;
                    }
                    return times;
                }
            }
        }

        public double[] GetTotalIonCurrent()
        {
            if (ChromatogramList == null)
            {
                return null;
            }
            using (var chromatogram = ChromatogramList.chromatogram(0, true))
            {
                if (chromatogram == null)
                {
                    return null;
                }
                TimeIntensityPairList timeIntensityPairList = new TimeIntensityPairList();
                chromatogram.getTimeIntensityPairs(ref timeIntensityPairList);
                double[] intensities = new double[timeIntensityPairList.Count];
                for (int i = 0; i < intensities.Length; i++)
                {
                    intensities[i] = timeIntensityPairList[i].intensity;
                }
                return intensities;
            }
        }

        /// <summary>
        /// Walks the spectrum list, and fills in the retention time and MS level of each scan.
        /// Some data files do not have any chromatograms in them, so GetScanTimes
        /// cannot be used.
        /// </summary>
        public void GetScanTimesAndMsLevels(CancellationToken cancellationToken, out double[] times, out byte[] msLevels)
        {
            times = new double[SpectrumCount];
            msLevels = new byte[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var spectrum = SpectrumList.spectrum(i))
                {
                    times[i] = spectrum.scanList.scans[0].cvParam(CVID.MS_scan_start_time).timeInSeconds();
                    msLevels[i] = (byte) (int) spectrum.cvParam(CVID.MS_ms_level).value;
                }
            }
        }

        public int SpectrumCount
        {
            get { return SpectrumList != null ? SpectrumList.size() : 0; }
        }

        [Obsolete("Use the SpectrumCount property instead")]
        public int GetSpectrumCount()
        {
            return SpectrumCount;
        }

        public int GetSpectrumIndex(string id)
        {
            int index = SpectrumList.findAbbreviated(id);
            if (0 > index || index >= SpectrumList.size())
                return -1;
            return index;
        }

        public void GetSpectrum(int spectrumIndex, out double[] mzArray, out double[] intensityArray)
        {
            var spectrum = GetSpectrum(spectrumIndex);
            mzArray = spectrum.Mzs;
            intensityArray = spectrum.Intensities;
        }

        public MsDataSpectrum GetSpectrum(int spectrumIndex)
        {
            using (_perf.CreateTimer("GetSpectrum(index)")) // Not L10N
            {
                if (_scanCache != null)
                {
                    MsDataSpectrum returnSpectrum;
                    // check the scan for this cache
                    if (!_scanCache.TryGetSpectrum(spectrumIndex, out returnSpectrum))
                    {
                        // spectrum not in the cache, pull it from the file
                        returnSpectrum = GetSpectrum(SpectrumList.spectrum(spectrumIndex, true), spectrumIndex);
                        // add it to the cache
                        _scanCache.Add(spectrumIndex, returnSpectrum);
                    }
                    return returnSpectrum;
                }
                using (var spectrum = SpectrumList.spectrum(spectrumIndex, true))
                {
                    return GetSpectrum(spectrum, spectrumIndex);
                }
            }
        }

        private MsDataSpectrum GetSpectrum(Spectrum spectrum, int spectrumIndex)
        {
            if (spectrum == null)
            {
                return new MsDataSpectrum
                {
                    Centroided = true,
                    Mzs = new double[0],
                    Intensities = new double[0]
                };
            }
            string idText = spectrum.id;
            if (idText.Trim().Length == 0)
            {
                throw new ArgumentException(string.Format("Empty spectrum ID (and index = {0}) for scan {1}", // Not L10N
                    spectrum.index, spectrumIndex)); 
            }

            var msDataSpectrum = new MsDataSpectrum
            {
                Id = id.abbreviate(idText),
                Level = GetMsLevel(spectrum) ?? 0,
                Index = spectrum.index,
                RetentionTime = GetStartTime(spectrum),
                DriftTimeMsec = GetDriftTimeMsec(spectrum),
                Precursors = GetPrecursors(spectrum),
                Centroided = IsCentroided(spectrum),
                NegativeCharge = NegativePolarity(spectrum)
            };
            if (spectrum.binaryDataArrays.Count <= 1)
            {
                msDataSpectrum.Mzs = new double[0];
                msDataSpectrum.Intensities = new double[0];
            }
            else
            {
                try
                {
                    msDataSpectrum.Mzs = ToArray(spectrum.getMZArray());
                    msDataSpectrum.Intensities = ToArray(spectrum.getIntensityArray());

                    if (msDataSpectrum.Level == 1 && _config.simAsSpectra &&
                            spectrum.scanList.scans[0].scanWindows.Count > 0)
                    {
                        msDataSpectrum.Precursors = GetMs1Precursors(spectrum);
                    }

                    return msDataSpectrum;
                }
                catch (NullReferenceException)
                {
                }
            }
            return msDataSpectrum;
        }

        public bool HasSrmSpectra
        {
            get { return HasSrmSpectraInList(SpectrumList); }
        }

        public bool HasDriftTimeSpectra
        {
            get { return HasDriftTimeSpectraInList(SpectrumList); }
        }

        public bool HasChromatogramData
        {
            get
            {
                var len = ChromatogramCount;

                // Many files have just one TIC chromatogram
                if (len < 2)
                    return false;

                for (var i = 0; i < len; i++)
                {
                    int index;
                    var id = GetChromatogramId(i, out index);

                    if (IsSingleIonCurrentId(id))
                        return true;
                }
                return false;
            }
        }

        private static bool HasSrmSpectraInList(SpectrumList spectrumList)
        {
            if (spectrumList == null || spectrumList.size() == 0)
                return false;

            // If the first spectrum is not SRM, the others will not be either
            using (var spectrum = spectrumList.spectrum(0, false))
            {
                return IsSrmSpectrum(spectrum);
            }
        }

        private static bool HasDriftTimeSpectraInList(SpectrumList spectrumList)
        {
            if (spectrumList == null || spectrumList.size() == 0)
                return false;

            // Assume that if any spectra have drift times, all do
            using (var spectrum = spectrumList.spectrum(0, false))
            {
                return GetDriftTimeMsec(spectrum).HasValue;
            }
        }

        private double? GetMaxDriftTimeInList(SpectrumList spectrumList)
        {
            if (spectrumList == null || spectrumList.size() == 0)
                return null;

            // Assume that if any spectra have drift times, all do, and all are same range
            double? maxDrift = null;
            for (var i = 0; i < spectrumList.size(); i++)
            {
                using (var spectrum = spectrumList.spectrum(i, false))
                {
                    var dt = GetDriftTimeMsec(spectrum);
                    if (!dt.HasValue)
                    {
                        if (IsWatersLockmassSpectrum(GetSpectrum(spectrum, i)))
                            continue;  // In SONAR data, lockmass scan without drift info doesn't mean there's no drift info
                        if (!maxDrift.HasValue)
                            return null; // Assume that if first regular scan is without drift info, they are all
                    }
                    if (!maxDrift.HasValue)
                    {
                        maxDrift = dt;
                    }
                    else if (dt.Value < maxDrift.Value)
                    {
                        break;  // We've cycled 
                    }
                    else
                    {
                        maxDrift = dt;
                    }
                }
            }
            return maxDrift;
        }

        public MsDataSpectrum GetSrmSpectrum(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, true))
            {
                return GetSpectrum(IsSrmSpectrum(spectrum) ? spectrum : null, scanIndex);
            }
        }

        public string GetSpectrumId(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex))
            {
                return spectrum.id;
            }
        }

        public bool IsCentroided(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, false))
            {
                return IsCentroided(spectrum);
            }
        }

        private static bool IsCentroided(Spectrum spectrum)
        {
            return spectrum.hasCVParam(CVID.MS_centroid_spectrum);
        }

        private static bool NegativePolarity(Spectrum spectrum)
        {
            var param = spectrum.cvParamChild(CVID.MS_scan_polarity);
            if (param.empty())
                return false;  // Assume positive if undeclared
            return (param.cvid == CVID.MS_negative_scan);
        }

        public bool IsSrmSpectrum(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, false))
            {
                return IsSrmSpectrum(spectrum);
            }
        }

        private static bool IsSrmSpectrum(Spectrum spectrum)
        {
            return spectrum.hasCVParam(CVID.MS_SRM_spectrum);
        }

        public int GetMsLevel(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, _detailMsLevel))
            {
                int? level = GetMsLevel(spectrum);
                if (level.HasValue || _detailMsLevel == DetailLevel.FullMetadata)
                    return level ?? 0;

                // If level is not found with faster metadata methods, try the slower ones.
                if (_detailMsLevel == DetailLevel.InstantMetadata)
                    _detailMsLevel = DetailLevel.FastMetadata;
                else if (_detailMsLevel == DetailLevel.FastMetadata)
                    _detailMsLevel = DetailLevel.FullMetadata;
                return GetMsLevel(scanIndex);
            }
        }

        private static int? GetMsLevel(Spectrum spectrum)
        {
            CVParam param = spectrum.cvParam(CVID.MS_ms_level);
            if (param.empty())
                return null;
            return (int) param.value;
        }

        public double? GetDriftTimeMsec(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, _detailDriftTime))
            {
                double? driftTime = GetDriftTimeMsec(spectrum);
                if (driftTime.HasValue || _detailDriftTime >= DetailLevel.FullMetadata)
                    return driftTime ?? 0;

                // If level is not found with faster metadata methods, try the slower ones.
                if (_detailDriftTime == DetailLevel.InstantMetadata)
                    _detailDriftTime = DetailLevel.FastMetadata;
                else if (_detailDriftTime == DetailLevel.FastMetadata)
                    _detailDriftTime = DetailLevel.FullMetadata;
                return GetDriftTimeMsec(scanIndex);
            }
        }

        private static double? GetDriftTimeMsec(Spectrum spectrum)
        {
            if (spectrum.scanList.scans.Count == 0)
                return null;
            var scan = spectrum.scanList.scans[0];
            CVParam driftTime = scan.cvParam(CVID.MS_ion_mobility_drift_time);
            if (driftTime.empty())
            {
                UserParam param = scan.userParam(USERPARAM_DRIFT_TIME); // support files with the original drift time UserParam
                if (param.empty())
                    return null;
                return param.timeInSeconds() * 1000.0;
            }
            else
                return driftTime.timeInSeconds() * 1000.0;
        }

        public double? GetStartTime(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, _detailStartTime))
            {
                double? startTime = GetStartTime(spectrum);
                if (startTime.HasValue || _detailStartTime >= DetailLevel.FullMetadata)
                    return startTime ?? 0;

                // If level is not found with faster metadata methods, try the slower ones.
                if (_detailStartTime == DetailLevel.InstantMetadata)
                    _detailStartTime = DetailLevel.FastMetadata;
                else if (_detailStartTime == DetailLevel.FastMetadata)
                    _detailStartTime = DetailLevel.FullMetadata;
                return GetStartTime(scanIndex);
            }
        }

        private static double? GetStartTime(Spectrum spectrum)
        {
            if (spectrum.scanList.scans.Count == 0)
                return null;
            var scan = spectrum.scanList.scans[0];
            CVParam param = scan.cvParam(CVID.MS_scan_start_time);
            if (param.empty())
                return null;
            return param.timeInSeconds() / 60;
        }

        public MsTimeAndPrecursors GetInstantTimeAndPrecursors(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, DetailLevel.InstantMetadata))
            {
                return new MsTimeAndPrecursors
                {
                    Precursors = GetPrecursors(spectrum),
                    RetentionTime = GetStartTime(spectrum)
                };
            }
        }

        public MsPrecursor[] GetPrecursors(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, false))
            {
                return GetPrecursors(spectrum);
            }
        }

        private static MsPrecursor[] GetPrecursors(Spectrum spectrum)
        {
            bool negativePolarity = NegativePolarity(spectrum);
            return spectrum.precursors.Select(p =>
                new MsPrecursor
                    {
                        PrecursorMz = GetPrecursorMz(p, negativePolarity),
                        PrecursorDriftTimeMsec = GetPrecursorDriftTimeMsec(p),
                        PrecursorCollisionEnergy = GetPrecursorCollisionEnergy(p),
                        IsolationWindowTargetMz = new SignedMz(GetIsolationWindowValue(p, CVID.MS_isolation_window_target_m_z), negativePolarity),
                        IsolationWindowLower = GetIsolationWindowValue(p, CVID.MS_isolation_window_lower_offset),
                        IsolationWindowUpper = GetIsolationWindowValue(p, CVID.MS_isolation_window_upper_offset),
                    }).ToArray();
        }

        private static MsPrecursor[] GetMs1Precursors(Spectrum spectrum)
        {
            bool negativePolarity = NegativePolarity(spectrum);
            return spectrum.scanList.scans[0].scanWindows.Select(s =>
                {
                    double windowStart = s.cvParam(CVID.MS_scan_window_lower_limit).value;
                    double windowEnd = s.cvParam(CVID.MS_scan_window_upper_limit).value;
                    double isolationWidth = (windowEnd - windowStart) / 2;
                    return new MsPrecursor
                        {
                            IsolationWindowTargetMz = new SignedMz(windowStart + isolationWidth, negativePolarity),
                            IsolationWindowLower = isolationWidth,
                            IsolationWindowUpper = isolationWidth
                        };
                }).ToArray();
        }

        private static SignedMz GetPrecursorMz(Precursor precursor, bool negativePolarity)
        {
            // CONSIDER: Only the first selected ion m/z is considered for the precursor m/z
            var selectedIon = precursor.selectedIons.FirstOrDefault();
            if (selectedIon == null)
                return SignedMz.EMPTY;
            return new SignedMz(selectedIon.cvParam(CVID.MS_selected_ion_m_z).value, negativePolarity);
        }

        private const string USERPARAM_DRIFT_TIME = "drift time"; // Not L10N

        private static double? GetPrecursorDriftTimeMsec(Precursor precursor)
        {
            UserParam param = precursor.userParam(USERPARAM_DRIFT_TIME);  //   CONSIDER: this will eventually be a proper CVParam
            if (param.empty())
                return null;
            return param.timeInSeconds() * 1000.0;
        }

        private static double? GetPrecursorCollisionEnergy(Precursor precursor)
        {
            var param = precursor.activation.cvParam(CVID.MS_collision_energy);
            if (param.empty())
                return null;
            return (double)param.value;
        }

        private static double? GetIsolationWindowValue(Precursor precursor, CVID cvid)
        {
            var term = precursor.isolationWindow.cvParam(cvid);
            if (!term.empty())
                return term.value;
            return null;
        }

        public void Write(string path)
        {
            MSDataFile.write(_msDataFile, path);
        }

        public void Dispose()
        {
            if (_spectrumList != null)
                _spectrumList.Dispose();
            _spectrumList = null;
            if (_chromatogramList != null)
                _chromatogramList.Dispose();
            _chromatogramList = null;
            if (_msDataFile != null)
                _msDataFile.Dispose();
            _msDataFile = null;
        }

        public string FilePath { get; private set; }
    }

    public sealed class MsDataConfigInfo
    {
        public int Spectra { get; set; }
        public string ContentType { get; set; }
        public string IonSource { get; set; }
        public string Analyzer { get; set; }
        public string Detector { get; set; }
    }

    /// <summary>
    /// For Waters lockmass correction
    /// </summary>
    public sealed class LockMassParameters : IComparable
    {
        public LockMassParameters(double? lockmassPositve, double? lockmassNegative, double? lockmassTolerance)
        {
            LockmassPositive = lockmassPositve;
            LockmassNegative = lockmassNegative;
            if (LockmassPositive.HasValue || LockmassNegative.HasValue)
            {
                LockmassTolerance = lockmassTolerance ?? LOCKMASS_TOLERANCE_DEFAULT;
            }
            else
            {
                LockmassTolerance = null;  // Means nothing when no mz is given
            }
        }

        public double? LockmassPositive { get; private set; }
        public double? LockmassNegative { get; private set; }
        public double? LockmassTolerance { get; private set; }

        public static readonly double LOCKMASS_TOLERANCE_DEFAULT = 0.1; // Per Will T
        public static readonly double LOCKMASS_TOLERANCE_MAX = 10.0;
        public static readonly double LOCKMASS_TOLERANCE_MIN = 0;

        public static readonly LockMassParameters EMPTY = new LockMassParameters(null, null, null);

        public bool IsEmpty
        {
            get
            {
                return (0 == (LockmassNegative ?? 0)) &&
                       (0 == (LockmassPositive ?? 0));
                // Ignoring tolerance here, which means nothing when no mz is given
            }
        }

        private bool Equals(LockMassParameters other)
        {
            return LockmassPositive.Equals(other.LockmassPositive) && 
                   LockmassNegative.Equals(other.LockmassNegative) &&
                   LockmassTolerance.Equals(other.LockmassTolerance);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is LockMassParameters && Equals((LockMassParameters) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = LockmassPositive.GetHashCode();
                result = (result * 397) ^ LockmassNegative.GetHashCode();
                result = (result * 397) ^ LockmassTolerance.GetHashCode();
                return result;
            }
        }

        public int CompareTo(LockMassParameters other)
        {
            if (ReferenceEquals(null, other)) 
                return -1;
            var result = Nullable.Compare(LockmassPositive, other.LockmassPositive);
            if (result != 0)
                return result;
            result = Nullable.Compare(LockmassNegative, other.LockmassNegative);
            if (result != 0)
                return result;
            return Nullable.Compare(LockmassTolerance, other.LockmassTolerance);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return -1;
            if (ReferenceEquals(this, obj)) return 0;
            if (obj.GetType() != GetType()) return -1;
            return CompareTo((LockMassParameters)obj);
        }
    }

    
    /// <summary>
    /// We need a way to distinguish chromatograms for negative ion modes from those for positive.
    /// The idea of m/z is inherently "positive", in the sense of sorting etc, so we carry around 
    /// the m/z value, and a sign flag, and when we sort it's by sign then by (normally positive) m/z.  
    /// The m/z value *could* be negative as a result of an arithmetic operator, that has a special
    /// meaning for comparisons but doesn't happen in normal use.
    /// There's a lot of operator magic here to minimize code changes where we used to implement mz 
    /// values as simple doubles.
    /// </summary>
    public struct SignedMz : IComparable, IEquatable<SignedMz>, IFormattable
    {
        private readonly double? _mz;
        private readonly bool _isNegative;

        public SignedMz(double? mz, bool isNegative)
        {
            _mz = mz;
            _isNegative = isNegative;
        }

        public SignedMz(double? mz)  // For deserialization - a negative value is taken to mean negative polarity
        {
            _isNegative = (mz??0) < 0;
            _mz = mz.HasValue ? Math.Abs(mz.Value) : (double?) null;
        }

        public static readonly SignedMz EMPTY = new SignedMz(null, false);
        public static readonly SignedMz ZERO = new SignedMz(0);

        public double Value
        {
            get { return _mz.Value; }
        }

        public double GetValueOrDefault()
        {
            return HasValue? Value : 0.0;
        }

        /// <summary>
        /// For serialization etc - returns a negative number if IsNegative is true 
        /// </summary>
        public double? RawValue
        {
            get { return _mz.HasValue ? (_isNegative ? -_mz.Value : _mz.Value) : _mz;  }
        }

        public bool IsNegative
        {
            get { return _isNegative; }
        }

        public bool HasValue
        {
            get { return _mz.HasValue; }
        }

        public static implicit operator double(SignedMz mz)
        {
            return mz.Value;
        }

        public static implicit operator double?(SignedMz mz)
        {
            return mz._mz;
        }

        public static SignedMz operator +(SignedMz mz, double step)
        {
            return new SignedMz(mz.Value + step, mz.IsNegative);
        }

        public static SignedMz operator -(SignedMz mz, double step)
        {
            return new SignedMz(mz.Value - step, mz.IsNegative);
        }

        public static SignedMz operator +(SignedMz mz, SignedMz step)
        {
            if (mz.IsNegative != step.IsNegative)
                throw new InvalidOperationException("polarity mismatch"); // Not L10N
            return new SignedMz(mz.Value + step.Value, mz.IsNegative);
        }

        public static SignedMz operator -(SignedMz mz, SignedMz step)
        {
            if (mz.IsNegative != step.IsNegative)
                throw new InvalidOperationException("polarity mismatch"); // Not L10N
            return new SignedMz(mz.Value - step.Value, mz.IsNegative);
        }

        public static bool operator <(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) < 0;
        }

        public static bool operator <=(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) <= 0;
        }

        public static bool operator >=(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) >= 0;
        }

        public static bool operator >(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) > 0;
        }

        public static bool operator ==(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) == 0;
        }

        public static bool operator !=(SignedMz mzA, SignedMz mzB)
        {
            return !(mzA == mzB);
        }

        public bool Equals(SignedMz other)
        {
            return CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SignedMz && Equals((SignedMz)obj);
        }

        public override int GetHashCode()
        {
            return RawValue.GetHashCode();
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return Value.ToString(format, formatProvider);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return -1;
            if (obj.GetType() != GetType()) return -1;
            return CompareTo((SignedMz)obj);
        }

        public int CompareTo(SignedMz other)
        {
            if (_mz.HasValue != other.HasValue)
            {
                return _mz.HasValue ? 1 : -1;
            }
            if (IsNegative != other.IsNegative)
            {
                return IsNegative ? -1 : 1;
            }
            // Same sign
            if (_mz.HasValue)
                return Value.CompareTo(other.Value);
            return 0; // Both empty
        }

        public int CompareTolerant(SignedMz other, float tolerance)
        {
            if (_mz.HasValue != other.HasValue)
            {
                return _mz.HasValue ? 1 : -1;
            }
            if (IsNegative != other.IsNegative)
            {
                return IsNegative ? -1 : 1; // Not interested in tolerance when signs disagree 
            }
            // Same sign
            if (Math.Abs(Value - other.Value) <= tolerance)
                return 0;
            return Value.CompareTo(other.Value);
        }

        public SignedMz ChangeMz(double mz)
        {
            return new SignedMz(mz, IsNegative);  // New mz, same polarity
        }

        public override string ToString()
        {
            return RawValue.ToString();
        }
    }

    public struct MsPrecursor
    {
        public SignedMz PrecursorMz { get; set; }
        public double? PrecursorDriftTimeMsec { get; set; }
        public double? PrecursorCollisionEnergy  { get; set; }
        public SignedMz IsolationWindowTargetMz { get; set; }
        public double? IsolationWindowUpper { get; set; }
        public double? IsolationWindowLower { get; set; }
        public SignedMz IsolationMz
        {
            get
            {
                var targetMz = IsolationWindowTargetMz.HasValue ? IsolationWindowTargetMz : PrecursorMz;
                // If the isolation window is not centered around the target m/z, then return a
                // m/z value that is centered in the isolation window.
                if (targetMz.HasValue && IsolationWindowUpper.HasValue && IsolationWindowLower.HasValue &&
                        IsolationWindowUpper.Value != IsolationWindowLower.Value)
                    return new SignedMz((targetMz * 2 + IsolationWindowUpper.Value - IsolationWindowLower.Value) / 2.0, targetMz.IsNegative);
                return targetMz;
            }
        }
        public double? IsolationWidth
        {
            get
            {
                if (IsolationWindowUpper.HasValue && IsolationWindowLower.HasValue)
                {
                    double width = IsolationWindowUpper.Value + IsolationWindowLower.Value;
                    if (width > 0)
                        return width;
                }
                return null;
            }
        }
    }

    public sealed class MsTimeAndPrecursors
    {
        public double? RetentionTime { get; set; }
        public MsPrecursor[] Precursors { get; set; }
    }

    public sealed class MsDataSpectrum
    {
        public string Id { get; set; }
        public int Level { get; set; }
        public int Index { get; set; } // index into parent file, if any
        public double? RetentionTime { get; set; }
        public double? DriftTimeMsec { get; set; }
        public MsPrecursor[] Precursors { get; set; }
        public bool Centroided { get; set; }
        public bool NegativeCharge { get; set; } // True if negative ion mode
        public double[] Mzs { get; set; }
        public double[] Intensities { get; set; }

        public static int WatersFunctionNumberFromId(string id)
        {
            return int.Parse(id.Split('.')[0]); // Yes, this will throw if it's not in dotted format - and that's good
        }

        public int WatersFunctionNumber
        {
            get 
            {
                return WatersFunctionNumberFromId(Id);
            }
        }
    }

    public sealed class MsInstrumentConfigInfo
    {
        public string Model { get; private set; }
        public string Ionization { get; private set; }
        public string Analyzer { get; private set; }
        public string Detector { get; private set; }

        public static readonly MsInstrumentConfigInfo EMPTY = new MsInstrumentConfigInfo(null, null, null, null);

        public MsInstrumentConfigInfo(string model, string ionization,
                                      string analyzer, string detector)
        {
            Model = model != null ? model.Trim() : null;
            Ionization = ionization != null ? ionization.Replace('\n',' ').Trim() : null; // Not L10N
            Analyzer = analyzer != null ? analyzer.Replace('\n', ' ').Trim() : null; // Not L10N
            Detector = detector != null ? detector.Replace('\n', ' ').Trim() : null; // Not L10N
        }

        public bool IsEmpty
        {
            get
            {
                return (string.IsNullOrEmpty(Model)) &&
                       (string.IsNullOrEmpty(Ionization)) &&
                       (string.IsNullOrEmpty(Analyzer)) &&
                       (string.IsNullOrEmpty(Detector));
            }
        }

        #region object overrides

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(MsInstrumentConfigInfo)) return false;
            return Equals((MsInstrumentConfigInfo)obj);
        }

        public bool Equals(MsInstrumentConfigInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Model, Model) &&
                Equals(other.Ionization, Ionization) &&
                Equals(other.Analyzer, Analyzer) &&
                Equals(other.Detector, Detector);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = 0;
                result = (result * 397) ^ (Model != null ? Model.GetHashCode() : 0);
                result = (result * 397) ^ (Ionization != null ? Ionization.GetHashCode() : 0);
                result = (result * 397) ^ (Analyzer != null ? Analyzer.GetHashCode() : 0);
                result = (result * 397) ^ (Detector != null ? Detector.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }
    /// <summary>
    /// A class to cache scans recently read from the file
    /// </summary>
    public class MsDataScanCache
    {
        private readonly int _cacheSize;
        private readonly Dictionary<int, MsDataSpectrum> _cache;
        /// <summary>
        /// queue to keep track of order in which scans were added
        /// </summary>
        private readonly Queue<int> _scanStack;
        public int Capacity { get { return _cacheSize; } }
        public int Size { get { return _scanStack.Count; } }

        public MsDataScanCache()
            : this(100)
        {
        }

        public MsDataScanCache(int cacheSize)
        {
            _cacheSize = cacheSize;
            _cache = new Dictionary<int, MsDataSpectrum>(_cacheSize);
            _scanStack = new Queue<int>();
        }

        public bool HasScan(int scanNum)
        {
            return _cache.ContainsKey(scanNum);
        }

        public void Add(int scanNum, MsDataSpectrum s)
        {
            if (_scanStack.Count() >= _cacheSize)
            {
                _cache.Remove(_scanStack.Dequeue());
            }
            _cache.Add(scanNum, s);
            _scanStack.Enqueue(scanNum);
        }

        public bool TryGetSpectrum(int scanNum, out MsDataSpectrum spectrum)
        {
            return _cache.TryGetValue(scanNum, out spectrum);
        }

        public void Clear()
        {
            _cache.Clear();
            _scanStack.Clear();
        }
    }
}
