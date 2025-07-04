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
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using ComponentType = pwiz.CLI.msdata.ComponentType;
using Version = pwiz.CLI.msdata.Version;


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

        public static string InstalledVersion
        {
            get
            {
                // Forces pwiz_data_cli.dll to be loaded with all its dependencies
                // Throws and exception if the DLL load fails
                // ReSharper disable UnusedVariable
                var test = new MSData();
                // ReSharper restore UnusedVariable
                // Return the version string once the load succeeds
                return Version.ToString();
            }
        }

        public static IEnumerable<KeyValuePair<string, IList<string>>> GetFileExtensionsByType()
        {
            foreach (var typeExtsPair in FULL_READER_LIST.getFileExtensionsByType())
                yield return typeExtsPair;
        }

        public static bool SupportsVendorPeakPicking(string path)
        {
            return SpectrumList_PeakPicker.supportsVendorPeakPicking(path);
        }

        // By default this creates dummy non-functional performance timers.
        // Place "MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = false;" in 
        // the calling code to enable performance measurement.
        public static PerfUtilFactory PerfUtilFactory { get; private set; }

        static MsDataFileImpl()
        {
            PerfUtilFactory = new PerfUtilFactory();
        }

        // Cached disposable objects
        protected MSData _msDataFile;
        protected ReaderConfig _config;
        protected SpectrumList _spectrumList;
        protected ChromatogramList _chromatogramList;
        private bool _providesConversionCCStoIonMobility;
        private SpectrumList_IonMobility.IonMobilityUnits _ionMobilityUnits;
        private SpectrumList_IonMobility _ionMobilitySpectrumList; // For Agilent and Bruker (and others, eventually?) conversion from CCS to ion mobility
        private MsDataScanCache _scanCache;
        private readonly IPerfUtil _perf; // for performance measurement, dummied by default
        private readonly LockMassParameters _lockmassParameters; // For Waters lockmass correction
        private int? _lockmassFunction;  // For Waters lockmass correction

        private readonly bool _requireVendorCentroidedMS1;
        private readonly bool _requireVendorCentroidedMS2;

        private readonly bool _trimNativeID;

        private DetailLevel _detailMsLevel = DetailLevel.InstantMetadata;

        private DetailLevel _detailStartTime = DetailLevel.InstantMetadata;

        private DetailLevel _detailIonMobility = DetailLevel.InstantMetadata;

        private DetailLevel _detailLevelPrecursors = DetailLevel.InstantMetadata;

        private DetailLevel _detailScanDescription = DetailLevel.FastMetadata;

        private CVID? _cvidIonMobility;

        private static double[] ToArray(BinaryDataArray binaryDataArray)
        {
            return binaryDataArray.data.Storage();
        }

        public static float[] ToFloatArray(IList<double> list)
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

        public static bool SupportsMultipleSamples(string path)
        {
            path = path.ToLowerInvariant();
            return path.EndsWith(@".wiff") || path.EndsWith(@".wiff2");
        }

        public const string PREFIX_TOTAL = "SRM TIC ";
        public const string PREFIX_SINGLE = "SRM SIC ";
        public const string PREFIX_PRECURSOR = "SIM SIC ";
        public const string TIC = "TIC";
        public const string BPC = "BPC";

        public static bool? IsNegativeChargeIdNullable(string id)
        {
            if (id.StartsWith(@"+ "))
                return false;
            if (id.StartsWith(@"- "))
                return true;
            return null;
        }

        public static bool IsSingleIonCurrentId(string id)
        {
            if (IsNegativeChargeIdNullable(id).HasValue)
                id = id.Substring(2);
            return id.StartsWith(PREFIX_SINGLE) || id.StartsWith(PREFIX_PRECURSOR);
        }

        public static bool ForceUncombinedIonMobility { get { return false; } }

        public MsDataFileImpl(string path, int sampleIndex = 0, LockMassParameters lockmassParameters = null,
            bool simAsSpectra = false, bool srmAsSpectra = false, bool acceptZeroLengthSpectra = true,
            bool requireVendorCentroidedMS1 = false, bool requireVendorCentroidedMS2 = false,
            bool ignoreZeroIntensityPoints = false, 
            int preferOnlyMsLevel = 0,
            bool combineIonMobilitySpectra = true, // Ask for IMS data in 3-array format by default (not guaranteed)
            bool trimNativeId = true)
        {

            // see note above on enabling performance measurement
            _perf = PerfUtilFactory.CreatePerfUtil(@"MsDataFileImpl " +
                string.Format(@"{0},sampleIndex:{1},lockmassCorrection:{2},simAsSpectra:{3},srmAsSpectra:{4},acceptZeroLengthSpectra:{5},requireVendorCentroidedMS1:{6},requireVendorCentroidedMS2:{7},preferOnlyMsLevel:{8},combineIonMobilitySpectra:{9}",
                path, sampleIndex, !(lockmassParameters == null || lockmassParameters.IsEmpty), simAsSpectra, srmAsSpectra, acceptZeroLengthSpectra, requireVendorCentroidedMS1, requireVendorCentroidedMS2, preferOnlyMsLevel, combineIonMobilitySpectra));
//            if (!combineIonMobilitySpectra)
//                Console.Write(string.Empty);
            using (_perf.CreateTimer(@"open"))
            {
                FilePath = path;
                SampleIndex = sampleIndex;
                _msDataFile = new MSData();
                _config = new ReaderConfig
                {
                    simAsSpectra = simAsSpectra,
                    srmAsSpectra = srmAsSpectra,
                    acceptZeroLengthSpectra = acceptZeroLengthSpectra,
                    ignoreZeroIntensityPoints = ignoreZeroIntensityPoints,
                    preferOnlyMsLevel = !ForceUncombinedIonMobility && combineIonMobilitySpectra ? 0 : preferOnlyMsLevel,
                    allowMsMsWithoutPrecursor = false,
                    combineIonMobilitySpectra = !ForceUncombinedIonMobility && combineIonMobilitySpectra,
                    ignoreCalibrationScans = true, // For Waters, we don't need to hear about lockmass values
                    reportSonarBins = true, // For Waters SONAR data, report bin number instead of false drift time
                    globalChromatogramsAreMs1Only = true
                };
                _lockmassParameters = lockmassParameters;
                FULL_READER_LIST.read(path, _msDataFile, sampleIndex, _config);
                _requireVendorCentroidedMS1 = requireVendorCentroidedMS1;
                _requireVendorCentroidedMS2 = requireVendorCentroidedMS2;
                _trimNativeID = trimNativeId;
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

        public bool RequireVendorCentoridedMs1 => _requireVendorCentroidedMS1;
        public bool RequireVendorCentoridedMs2 => _requireVendorCentroidedMS2;

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
                        ionSource += @", ";
                    ionSource += instrumentIonSource;

                    if (analyzer.Length > 0)
                        analyzer += @", ";
                    analyzer += instrumentAnalyzer;

                    if (detector.Length > 0)
                        detector += @", ";
                    detector += instrumentDetector;
                }

                HashSet<string> contentTypeSet = new HashSet<string>();
                using var fileDescriptionFileContent = _msDataFile.fileDescription.fileContent;
                foreach (CVParam term in fileDescriptionFileContent.cvParams)
                    contentTypeSet.Add(term.name);
                var contentTypes = contentTypeSet.ToArray();
                Array.Sort(contentTypes);
                string contentType = String.Join(@", ", contentTypes);

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
                CVParam term = null;
                switch (c.type)
                {
                    case ComponentType.ComponentType_Source:
                        term = c.cvParamChild(CVID.MS_ionization_type);
                        if (!term.empty())
                            ionSources.Add(c.order, term.name);
                        else
                        {
                            // If we did not find the ion source in a CVParam it may be in a UserParam
                            using UserParam uParam = c.userParam(@"msIonisation");
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
                            using UserParam uParam = c.userParam(@"msMassAnalyzer");
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
                            using UserParam uParam = c.userParam(@"msDetector");
                            if (HasInfo(uParam))
                            {
                                detectors.Add(c.order, uParam.value);
                            }
                        }
                        break;
                }
                term?.Dispose();
            }

            ionSource = String.Join(@"/", new List<string>(ionSources.Values).ToArray());

            analyzer = String.Join(@"/", new List<string>(analyzers.Values).ToArray());

            detector = String.Join(@"/", new List<string>(detectors.Values).ToArray());
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
            return _lockmassFunction.HasValue && 
                   MsDataSpectrum.WatersFunctionNumberFromId(s.Id, s.IonMobilities != null) >= _lockmassFunction.Value;
        }

        public IEnumerable<string> GetFileContentList()
        {
            return _msDataFile.fileDescription.fileContent.cvParams.Select(cv => cv.name);
        }

        /// <summary>
        /// Record any instrument info found in the file, along with any Waters lockmass info we have
        /// </summary>
        public IEnumerable<MsInstrumentConfigInfo> GetInstrumentConfigInfoList()
        {
            using (_perf.CreateTimer(@"GetInstrumentConfigList"))
            {
                IList<MsInstrumentConfigInfo> configList = new List<MsInstrumentConfigInfo>();

                foreach (InstrumentConfiguration ic in _msDataFile.instrumentConfigurationList)
                {
                    var config = CreateMsInstrumentConfigInfo(ic);
                    if (config != null)
                    {
                        configList.Add(config);
                    }
                }
                return configList;
            }
        }

        public static MsInstrumentConfigInfo CreateMsInstrumentConfigInfo(InstrumentConfiguration ic)
        {
            if (ic == null)
                return null;
            string instrumentModel = null;
            string ionization;
            string analyzer;
            string detector;

            using CVParam param = ic.cvParamChild(CVID.MS_instrument_model);
            if (!param.empty() && param.cvid != CVID.MS_instrument_model)
            {
                instrumentModel = param.name;

                // if instrument model free string is present, it is probably more specific than CVID model (which may only indicate manufacturer)
                using UserParam uParam = ic.userParam(@"instrument model");
                if (HasInfo(uParam))
                {
                    instrumentModel = uParam.value;
                }
            }

            if (instrumentModel == null)
            {
                // If we did not find the instrument model in a CVParam it may be in a UserParam
                using UserParam uParam = ic.userParam(@"msModel");
                if (HasInfo(uParam))
                {
                    instrumentModel = uParam.value;
                }
                else
                {
                    using UserParam uParam2 = ic.userParam(@"instrument model");
                    if (HasInfo(uParam2))
                    {
                        instrumentModel = uParam2.value;
                    }
                }
            }

            // get the ionization type, analyzer and detector
            GetInstrumentConfig(ic, out ionization, out analyzer, out detector);

            if (instrumentModel != null || ionization != null || analyzer != null || detector != null)
            {
                return new MsInstrumentConfigInfo(instrumentModel, ionization, analyzer, detector);
            }
            else
                return null;
        }

        public string GetInstrumentSerialNumber()
        {
            return _msDataFile.instrumentConfigurationList.FirstOrDefault(o => o.hasCVParam(CVID.MS_instrument_serial_number))
                                                          ?.cvParam(CVID.MS_instrument_serial_number).value.ToString();
        }

        private static bool HasInfo(UserParam uParam)
        {
            return !uParam.empty() && !String.IsNullOrEmpty(uParam.value) &&
                   !String.Equals(@"unknown", uParam.value.ToString().ToLowerInvariant());
        }

        public static string GetCvParamName(string cvParamAccession)
        {
            return CV.cvTermInfo(cvParamAccession).shortName();
        }

        public void GetNativeIdAndFileFormat(out string nativeIdFormatAccession, out string fileFormatAccession)
        {
            var firstSource = _msDataFile.fileDescription.sourceFiles.First(source =>
                source.hasCVParamChild(CVID.MS_nativeID_format) &&
                source.hasCVParamChild(CVID.MS_file_format));
            nativeIdFormatAccession = CV.cvTermInfo(firstSource.cvParamChild(CVID.MS_nativeID_format).cvid).id;
            fileFormatAccession = CV.cvTermInfo(firstSource.cvParamChild(CVID.MS_file_format).cvid).id;
        }

        public bool IsABFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_ABI_WIFF_format)); }
        }

        public bool IsMzWiffXml
        {
            get { return IsProcessedBy(@"mzWiff"); }
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

        public bool HasDeclaredMSnSpectra
        {
            get { return _msDataFile.fileDescription.fileContent.hasCVParam(CVID.MS_MSn_spectrum); }
        }

        public bool IsWatersLockmassCorrectionCandidate
        {
            get
            {
                try
                {
                    // Has to be a .raw file, not just an mzML translation of one
                    return (FilePath.ToLowerInvariant().EndsWith(@".raw")) &&
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
            get { return _msDataFile.softwareList.Any(software => software.hasCVParamChild(CVID.MS_Shimadzu_Corporation_software)); }
        }

        private string InstrumentVendorName
        {
            get
            {
                if (IsABFile)
                    return @"Sciex";
                if (IsAgilentFile)
                    return @"Agilent";
                if (IsShimadzuFile)
                    return @"Shimadzu";
                if (IsThermoFile)
                    return @"Thermo";
                if (IsWatersFile)
                    return @"Waters";
                return null;
            }
        }

        public bool ProvidesCollisionalCrossSectionConverter
        {
            get { return SpectrumList != null && _providesConversionCCStoIonMobility; } // Checking SpectrumList provokes initialization of ionMobility info
        }

        private SpectrumList_IonMobility IonMobilitySpectrumList
        {
            get { return SpectrumList == null ? null : _ionMobilitySpectrumList; }  // Checking SpectrumList provokes initialization of ionMobility info
        }

        public IonMobilityValue IonMobilityFromCCS(double ccs, double mz, int charge)
        {
            return IonMobilityValue.GetIonMobilityValue(IonMobilitySpectrumList.ccsToIonMobility(ccs, mz, charge), IonMobilityUnits);
        }

        public double CCSFromIonMobilityValue(IonMobilityValue ionMobilityValue, double mz, int charge)
        {
            return ionMobilityValue.Mobility.HasValue ? IonMobilitySpectrumList.ionMobilityToCCS(ionMobilityValue.Mobility.Value, mz, charge) : 0;
        }

        public double CCSFromIonMobility(double ionMobility, double mz, int charge)
        {
            return IonMobilitySpectrumList.ionMobilityToCCS(ionMobility, mz, charge);
        }

        public eIonMobilityUnits IonMobilityUnits
        {
            get
            {
                switch (_ionMobilityUnits)
                {
                    case SpectrumList_IonMobility.IonMobilityUnits.none:
                        return eIonMobilityUnits.none;
                    case SpectrumList_IonMobility.IonMobilityUnits.drift_time_msec:
                        return eIonMobilityUnits.drift_time_msec;
                    case SpectrumList_IonMobility.IonMobilityUnits.inverse_reduced_ion_mobility_Vsec_per_cm2:
                        return eIonMobilityUnits.inverse_K0_Vsec_per_cm2;
                    case SpectrumList_IonMobility.IonMobilityUnits.compensation_V:
                        return eIonMobilityUnits.compensation_V;
                    case SpectrumList_IonMobility.IonMobilityUnits.waters_sonar: // Not really ion mobility, but uses IMS hardware to filter precursor m/z
                        return eIonMobilityUnits.waters_sonar;
                    default:
                        throw new InvalidDataException(string.Format(@"unknown ion mobility type {0}", _ionMobilityUnits));
                }
            }
        }

        protected virtual ChromatogramList ChromatogramList
        {
            get
            {
                return _chromatogramList = _chromatogramList ??
                    _msDataFile.run.chromatogramList;
            }
        }

        protected virtual SpectrumList SpectrumList
        {
            get
            {
                if (_spectrumList == null)
                {
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
                    if (centroidLevel.Any() && _spectrumList != null)
                    {
                        _spectrumList = new SpectrumList_PeakPicker(_spectrumList,
                            new VendorOnlyPeakDetector(), // Throws an exception when no vendor centroiding available
                            true, centroidLevel.ToArray());
                    }

                    _lockmassFunction = null;
                    if (_lockmassParameters != null && !_lockmassParameters.IsEmpty  && _spectrumList != null)
                    {
                        // N.B. it's OK for lockmass wrapper to wrap centroiding wrapper, but not vice versa.
                        _spectrumList = new SpectrumList_LockmassRefiner(_spectrumList,
                            _lockmassParameters.LockmassPositive ?? 0,
                            _lockmassParameters.LockmassNegative ?? 0,
                            _lockmassParameters.LockmassTolerance ?? LockMassParameters.LOCKMASS_TOLERANCE_DEFAULT);
                    }
                    // Ion mobility info
                    if (_spectrumList != null) // No ion mobility for chromatogram-only files
                    {
                        _ionMobilitySpectrumList = new SpectrumList_IonMobility(_spectrumList);
                        _ionMobilityUnits = _ionMobilitySpectrumList.getIonMobilityUnits();
                        _providesConversionCCStoIonMobility = _ionMobilitySpectrumList.canConvertIonMobilityAndCCS(_ionMobilityUnits);
                    }
                    if (IsWatersFile  && _spectrumList != null && !_spectrumList.calibrationSpectraAreOmitted() && !hasSrmSpectra)
                    {
                        for (var index = 0; index < _spectrumList.size(); index++)
                        {
                            // If lockmass scans aren't already being omitted at the top level, try to filter them out here.
                            // If the first seen MS spectrum has MS1 data and function > 1 assume it's the lockspray function, 
                            // and thus to be omitted from chromatogram extraction. We've seen files where first spectrum is
                            // "electromagnetic radiation spectrum", for example, which has no MS level value.
                            // N.B. for msE data we will always assume function 3 and greater are to be omitted
                            // N.B. in all cases this assumes that any functions greater than the lockmass function are to be ignored
                            // (e.g. "electromagnetic radiation spectrum") 
                            // CONSIDER(bspratt) I really wish there was some way to communicate decisions like this to the user
                            using var spectrum = _spectrumList.spectrum(index, DetailLevel.FullMetadata);
                            var msLevel = GetMsLevel(spectrum);
                            if (msLevel == 1)
                            {
                                var function = MsDataSpectrum.WatersFunctionNumberFromId(id.abbreviate(spectrum.id), 
                                    HasCombinedIonMobilitySpectra && spectrum.id.Contains(MERGED_TAG));
                                if (function > 1)
                                    _lockmassFunction = function; // Ignore all scans in this function for chromatogram extraction purposes
                            }
                            if (msLevel.HasValue)
                            {
                                break; // This was first-seen MS spectrum
                            }
                        }
                    }

                }
                return _spectrumList;
            }
        }

        public SpectrumMetadata GetSpectrumMetadata(int spectrumIndex)
        {
            return GetSpectrumMetadata(_msDataFile.run.spectrumList.spectrum(spectrumIndex, DetailLevel.FullMetadata));
        }

        public double? GetMaxIonMobility()
        {
            return GetMaxIonMobilityInList();
        }

        public bool HasCombinedIonMobilitySpectra => SpectrumList != null && IonMobilityUnits != eIonMobilityUnits.none &&  _ionMobilitySpectrumList != null && _ionMobilitySpectrumList.hasCombinedIonMobility();

        /// <summary>
        /// Gets the value of the MS_sample_name CV param of first sample in the MSData object, or null if there is no sample information.
        /// </summary>
        public string GetSampleId()
        {
            using var samples = _msDataFile.samples;
            if (samples.Count > 0)
            {
                using var cvParam = samples[0].cvParam(CVID.MS_sample_name);
                var sampleId = (string) cvParam.value;
                if (sampleId.Length > 0)
                    return sampleId;
            }
            return null;
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

        private static readonly string[] msLevelOrFunctionArrayNames = { "ms level", "function" };

        public double? GetChromatogramCollisionEnergy(int chromIndex)
        {
            using var chrom = ChromatogramList.chromatogram(chromIndex, DetailLevel.FullMetadata);
            return chrom.precursor?.activation?.cvParam(CVID.MS_collision_energy)?.value;
        }
        
        public void GetChromatogramMetadata(int chromIndex, out string id, out bool? isNegativePolarity, out double precursorMz, out double productMz)
        {
            using Chromatogram chrom = ChromatogramList.chromatogram(chromIndex, DetailLevel.FullMetadata);
            id = chrom.id;
            isNegativePolarity = chrom.cvParamChild(CVID.MS_scan_polarity).cvid switch
            {
                CVID.MS_positive_scan => false,
                CVID.MS_negative_scan => true,
                _ => null
            };
            precursorMz = chrom.precursor.isolationWindow.cvParam(CVID.MS_isolation_window_target_m_z).value;
            productMz = chrom.product.isolationWindow.cvParam(CVID.MS_isolation_window_target_m_z).value;
        }

        public void GetChromatogram(int chromIndex, out string id,
            out float[] timeArray, out float[] intensityArray, bool onlyMs1OrFunction1 = false)
        {
            using (Chromatogram chrom = ChromatogramList.chromatogram(chromIndex, true))
            {
                id = chrom.id;
                using var timeArrayData = chrom.getTimeArray().data;

                // convert time to minutes
                using var timeArrayParam = chrom.getTimeArray().cvParamChild(CVID.MS_binary_data_array);
                float timeUnitMultiple;
                switch (timeArrayParam.units)
                {
                    case CVID.UO_nanosecond: timeUnitMultiple = 60 * 1e9f; break;
                    case CVID.UO_microsecond: timeUnitMultiple = 60 * 1e6f; break;
                    case CVID.UO_millisecond: timeUnitMultiple = 60 * 1e3f; break;
                    case CVID.UO_second: timeUnitMultiple = 60; break;
                    case CVID.UO_minute: timeUnitMultiple = 1; break;
                    case CVID.UO_hour: timeUnitMultiple = 1f / 60; break;

                    default:
                        throw new InvalidDataException($"unsupported time unit in chromatogram: {timeArrayParam.unitsName}");
                }
                timeUnitMultiple = 1 / timeUnitMultiple;

                if (!onlyMs1OrFunction1)
                {
                    timeArray = new float[timeArrayData.Count];
                    for (int i = 0; i < timeArray.Length; ++i)
                        timeArray[i] = (float) timeArrayData[i] * timeUnitMultiple;
                    intensityArray = ToFloatArray(chrom.getIntensityArray().data);
                }
                else
                {
                    // get array of ms level or function for each chromatogram point
                    using var msLevelOrFunctionArray = chrom.integerDataArrays.FirstOrDefault(o =>
                        msLevelOrFunctionArrayNames.Contains(o.cvParam(CVID.MS_non_standard_data_array).value.ToString()));

                    // if array is missing or empty, return no chromatogram data points (because they could be from any ms level or function)
                    if (msLevelOrFunctionArray == null || msLevelOrFunctionArray.data.Count != chrom.binaryDataArrays[0].data.Count)
                    {
                        timeArray = intensityArray = null;
                        return;
                    }

                    var timeList = new List<float>();
                    var intensityList = new List<float>();
                    using var intensityArrayData = chrom.getIntensityArray().data;
                    using var msLevelOrFunctionArrayData = msLevelOrFunctionArray.data;

                    for (int i = 0; i < msLevelOrFunctionArrayData.Count; ++i)
                    {
                        if (msLevelOrFunctionArrayData[i] != 1)
                            continue;

                        timeList.Add((float) timeArrayData[i] * timeUnitMultiple);
                        intensityList.Add((float) intensityArrayData[i]);
                    }

                    // if there were no MS1 TIC points, add a placeholder so the TIC graph displays an appropriate message
                    if (timeList.Count == 0)
                    {
                        timeList.Add(0);
                        intensityList.Add(1);
                    }

                    timeArray = timeList.ToArray();
                    intensityArray = intensityList.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets the retention times from the first chromatogram in the data file.
        /// Returns null if there are no chromatograms in the file.
        /// </summary>
        public double[] GetScanTimes()
        {
            using (_perf.CreateTimer(@"GetScanTimes"))
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

        private const string MERGED_TAG = @"merged="; // Our cue that the scan in question represents 3-array IMS data

        public double[] GetTotalIonCurrent()
        {
            if (ChromatogramList == null || ChromatogramList.size() == 0)
            {
                return null;
            }
            using (var chromatogram = ChromatogramList.chromatogram(0, true))
            {
                return chromatogram?.getIntensityArray()?.data.Storage();
            }
        }

        public abstract class QcTraceQuality
        {
            public const string Pressure = @"pressure";
            public const string FlowRate = @"volumetric flow rate";
        }

        public abstract class QcTraceUnits
        {
            public const string PoundsPerSquareInch = @"psi";
            public const string MicrolitersPerMinute = @"uL/min";
        }

        public class QcTrace
        {
            public QcTrace(Chromatogram c, CVID chromatogramType)
            {
                Name = c.id;
                Index = c.index;
                if (chromatogramType == CVID.MS_pressure_chromatogram)
                {
                    MeasuredQuality = QcTraceQuality.Pressure;
                    IntensityUnits = QcTraceUnits.PoundsPerSquareInch;
                }
                else if (chromatogramType == CVID.MS_flow_rate_chromatogram)
                {
                    MeasuredQuality = QcTraceQuality.FlowRate;
                    IntensityUnits = QcTraceUnits.MicrolitersPerMinute;
                }
                else
                    throw new InvalidDataException($"unsupported chromatogram type (not pressure or flow rate): {c.id}");
                Times = c.getTimeArray().data.Storage();
                Intensities = c.binaryDataArrays[1].data.Storage();
            }

            public string Name { get; private set; }
            public int Index { get; private set; }
            public double[] Times { get; private set; }
            public double[] Intensities { get; private set; }
            public string MeasuredQuality { get; private set; }
            public string IntensityUnits { get; private set; }
        }

        public List<QcTrace> GetQcTraces()
        {
            if (ChromatogramList == null || ChromatogramList.size() == 0)
                return null;

            // some readers may return empty chromatograms at detail levels below FullMetadata
            DetailLevel minDetailLevel = DetailLevel.InstantMetadata;
            if (ChromatogramList.chromatogram(0, minDetailLevel).empty())
                minDetailLevel = DetailLevel.FullMetadata;

            var result = new List<QcTrace>();
            for (int i = 0; i < ChromatogramList.size(); ++i)
            {
                CVID chromatogramType;
                using (var chromMetaData = ChromatogramList.chromatogram(i, minDetailLevel))
                {
                    using var cvParamChild = chromMetaData.cvParamChild(CVID.MS_chromatogram_type);
                    chromatogramType = cvParamChild.cvid;
                    if (chromatogramType != CVID.MS_pressure_chromatogram &&
                        chromatogramType != CVID.MS_flow_rate_chromatogram)
                        continue;
                }

                using (var chromatogram = ChromatogramList.chromatogram(i, true))
                {
                    if (chromatogram == null)
                        return null;

                    result.Add(new QcTrace(chromatogram, chromatogramType));
                }
            }
            return result;
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
                    using var scanTime = spectrum.scanList.scans[0].cvParam(CVID.MS_scan_start_time);
                    using var msLevel = spectrum.cvParam(CVID.MS_ms_level);
                    times[i] = scanTime.timeInSeconds();
                    msLevels[i] = (byte) (int) msLevel.value;
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
            using (_perf.CreateTimer(@"GetSpectrum(index)"))
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
                var spectrum = GetCachedSpectrum(spectrumIndex, true);
                {
                    // Avoid wrapping the same cached Spectrum twice
                    if (_lastSpectrumInfo == null)
                        _lastSpectrumInfo = GetSpectrum(spectrum, spectrumIndex);
                    return _lastSpectrumInfo;
                }
            }
        }

        private double[] GetIonMobilityArray(Spectrum s)
        {
            double[] data = null;
            // Remember where the ion mobility value came from and continue getting it from the
            // same place throughout the file. Trying to get an ion mobility value from a CVID
            // where there is none can be slow.
            if (_cvidIonMobility.HasValue)
            {
                if (_cvidIonMobility.Value != CVID.CVID_Unknown)
                    data = s.getArrayByCVID(_cvidIonMobility.Value)?.data?.Storage();
            }
            else
            {
                switch (IonMobilityUnits)
                {
                    case eIonMobilityUnits.waters_sonar:
                    case eIonMobilityUnits.drift_time_msec:
                        data = TryGetIonMobilityData(s, CVID.MS_raw_ion_mobility_array, ref _cvidIonMobility);
                        if (data == null)
                        {
                            data = TryGetIonMobilityData(s, CVID.MS_scanning_quadrupole_position_lower_bound_m_z_array, ref _cvidIonMobility);
                            if (data == null)
                            {
                                data = TryGetIonMobilityData(s, CVID.MS_mean_ion_mobility_drift_time_array, ref _cvidIonMobility);
                                if (data == null && HasCombinedIonMobilitySpectra && !s.id.Contains(MERGED_TAG))
                                {
                                    _cvidIonMobility = null; // We can't learn anything from a lockmass spectrum that has no IMS
                                    return null;
                                }
                            }
                        }
                        break;
                    case eIonMobilityUnits.inverse_K0_Vsec_per_cm2:
                        data = TryGetIonMobilityData(s, CVID.MS_mean_inverse_reduced_ion_mobility_array, ref _cvidIonMobility);
                        if (data == null)
                            data = TryGetIonMobilityData(s, CVID.MS_raw_inverse_reduced_ion_mobility_array, ref _cvidIonMobility);
                        break;
//                    default:
//                        throw new InvalidDataException(string.Format(@"mobility type {0} does not support ion mobility arrays", IonMobilityUnits));
                }

                if (data == null)
                    _cvidIonMobility = CVID.CVID_Unknown;
            }

            return data;
        }

        private double[] TryGetIonMobilityData(Spectrum s, CVID cvid, ref CVID? cvidIonMobility)
        {
            using var data = s.getArrayByCVID(cvid)?.data;
            if (data != null)
                cvidIonMobility = cvid;

            return data?.Storage();
        }

        private MsDataSpectrum GetSpectrum(Spectrum spectrum, int spectrumIndex)
        {
            if (spectrum == null)
            {
                return new MsDataSpectrum
                {
                    Centroided = true,
                    Mzs = new double[0],
                    Intensities = new double[0],
                    IonMobilities = null
                };
            }
            string idText = spectrum.id;
            if (idText.Trim().Length == 0)
            {
                throw new ArgumentException(string.Format(@"Empty spectrum ID (and index = {0}) for scan {1}",
                    spectrum.index, spectrumIndex)); 
            }
            // Start building properties object here.
            bool expectIonMobilityValue = IonMobilityUnits != eIonMobilityUnits.none;
            var msDataSpectrum = new MsDataSpectrum
            {
                Id = _trimNativeID ? id.abbreviate(idText) : idText,
                Level = GetMsLevel(spectrum) ?? 0,
                Index = spectrum.index,
                RetentionTime = GetStartTime(spectrum),
                PrecursorsByMsLevel = GetPrecursorsByMsLevel(spectrum),
                Centroided = IsCentroided(spectrum),
                NegativeCharge = NegativePolarity(spectrum),
                ScanDescription = GetScanDescription(spectrum),
                Metadata = GetSpectrumMetadata(spectrum)
            };
            using var spectrumScanList = spectrum.scanList;
            using var scans = spectrumScanList.scans;
            if (IonMobilityUnits == eIonMobilityUnits.inverse_K0_Vsec_per_cm2)
            {
                var param = scans[0].userParam(@"windowGroup"); // For Bruker diaPASEF
                msDataSpectrum.WindowGroup = param.empty() ? 0 : int.Parse(param.value);
            }

            if (expectIonMobilityValue)
            {
                // Note the range actually measured (for zero vs missing value determination)
                var param = spectrum.userParam(@"ion mobility lower limit");
                if (!param.empty())
                {
                    msDataSpectrum.IonMobilityMeasurementRangeLow = param.value;
                    param = spectrum.userParam(@"ion mobility upper limit");
                    msDataSpectrum.IonMobilityMeasurementRangeHigh = param.value;
                }
            }

            if (spectrum.binaryDataArrays.Count <= 1)
            {
                msDataSpectrum.Mzs = new double[0];
                msDataSpectrum.Intensities = new double[0];
                msDataSpectrum.IonMobilities = null;
                if (expectIonMobilityValue)
                {
                    msDataSpectrum.IonMobility = GetIonMobility(spectrum);
                }
            }
            else
            {
                try
                {
                    msDataSpectrum.Mzs = ToArray(spectrum.getMZArray());
                    msDataSpectrum.Intensities = ToArray(spectrum.getIntensityArray());
                    msDataSpectrum.IonMobilities = GetIonMobilityArray(spectrum);
                    if (msDataSpectrum.IonMobilities != null)
                    {
                        // One more linear walk should be fine, given how much copying and walking gets done
                        double min = double.MaxValue, max = double.MinValue;
                        foreach (var ionMobility in msDataSpectrum.IonMobilities)
                        {
                            min = Math.Min(min, ionMobility);
                            max = Math.Max(max, ionMobility);
                        }
                        msDataSpectrum.MinIonMobility = min;
                        msDataSpectrum.MaxIonMobility = max;
                    }
                    else if (expectIonMobilityValue)
                    {
                        msDataSpectrum.IonMobility = GetIonMobility(spectrum);
                    }

                    if (msDataSpectrum.Level == 1 && _config.simAsSpectra &&
                            scans[0].scanWindows.Count > 0)
                    {
                        msDataSpectrum.Precursors = ImmutableList.ValueOf(GetMs1Precursors(spectrum));
                    }

                    msDataSpectrum.SourceFilePath = FilePath;
                    if(spectrum.scanList.scans.Count > 0)
                        msDataSpectrum.InstrumentInfo = CreateMsInstrumentConfigInfo(spectrum.scanList.scans[0].instrumentConfiguration); 
                    msDataSpectrum.InstrumentSerialNumber = GetInstrumentSerialNumber();
                    msDataSpectrum.InstrumentVendor = InstrumentVendorName;

                    return msDataSpectrum;
                }
                catch (NullReferenceException)
                {
                }
            }
            return msDataSpectrum;
        }

        // UserParam name that indicates that scan window bounds were obtained
        // from centroided data and therefore do not represent the entire
        // range that was monitored
        private const string CENTROIDED_MIN_MAX = @"centroided min/max";
        private SpectrumMetadata GetSpectrumMetadata(Spectrum spectrum)
        {
            if (spectrum == null)
            {
                return null;
            }

            var retentionTime = GetStartTime(spectrum);
            if (!retentionTime.HasValue)
            {
                return null;
            }
            var metadata = new SpectrumMetadata(id.abbreviate(spectrum.id), retentionTime.Value);
            var precursorsByMsLevel = new List<IEnumerable<SpectrumPrecursor>>();
            foreach (var level in GetPrecursorsByMsLevel(spectrum))
            {
                List<SpectrumPrecursor> spectrumPrecursors = new List<SpectrumPrecursor>();
                foreach (var msPrecursor in level)
                {
                    if (msPrecursor.IsolationMz.HasValue)
                    {
                        var spectrumPrecursor =
                            new SpectrumPrecursor(msPrecursor.IsolationMz.Value).ChangeCollisionEnergy(msPrecursor
                                .PrecursorCollisionEnergy);
                        if (msPrecursor.IsolationWindowLower.HasValue && msPrecursor.IsolationWindowUpper.HasValue)
                        {
                            spectrumPrecursor = spectrumPrecursor.ChangeIsolationWindowWidth(
                                msPrecursor.IsolationWindowLower.Value,
                                msPrecursor.IsolationWindowUpper.Value);
                        }
                        spectrumPrecursors.Add(spectrumPrecursor);
                    }
                }
                precursorsByMsLevel.Add(spectrumPrecursors);
            }
            metadata = metadata.ChangePrecursors(precursorsByMsLevel);
            metadata = metadata.ChangeScanDescription(GetScanDescription(spectrum));
            metadata = metadata.ChangePresetScanConfiguration(GetPresetScanConfiguration(spectrum));
            var instrumentConfig = spectrum.scanList.scans.FirstOrDefault()?.instrumentConfiguration;
            if (instrumentConfig != null)
            {
                GetInstrumentConfig(instrumentConfig, out string ionSource, out string analyzer, out string detector);
                if (analyzer != null)
                {
                    metadata = metadata.ChangeAnalyzer(analyzer);
                }
            }
            IonMobilityValue ionMobilityValue = GetIonMobility(spectrum);
            if (ionMobilityValue != null)
            {
                if (ionMobilityValue.Units == eIonMobilityUnits.compensation_V)
                {
                    metadata = metadata.ChangeCompensationVoltage(ionMobilityValue.Mobility);
                }
            }
            double? scanWindowLowerLimit = null;
            double? scanWindowUpperLimit = null;
            foreach (var scan in spectrum.scanList.scans)
            {
                foreach (var window in scan.scanWindows)
                {
                    if (!window.userParam(CENTROIDED_MIN_MAX).empty())
                    {
                        // min/max values obtained from centroided data are unreliable
                        continue;
                    }
                    var cvParamLowerLimit = window.cvParam(CVID.MS_scan_window_lower_limit);
                    if (cvParamLowerLimit != null)
                    {
                        double windowStart = cvParamLowerLimit.value;
                        if (scanWindowLowerLimit == null || windowStart < scanWindowLowerLimit)
                        {
                            scanWindowLowerLimit = windowStart;
                        }
                    }

                    var cvParamUpperLimit = window.cvParam(CVID.MS_scan_window_upper_limit);
                    if (cvParamUpperLimit != null)
                    {
                        double windowEnd = cvParamUpperLimit.value;
                        if (scanWindowUpperLimit == null || windowEnd > scanWindowUpperLimit)
                        {
                            scanWindowUpperLimit = windowEnd;
                        }
                    }
                }
            }

            if (scanWindowLowerLimit.HasValue && scanWindowUpperLimit.HasValue)
            {
                metadata = metadata.ChangeScanWindow(scanWindowLowerLimit.Value, scanWindowUpperLimit.Value);
            }

            metadata = metadata.ChangeTotalIonCurrent(GetTotalIonCurrent(spectrum));
            metadata = metadata.ChangeInjectionTime(GetInjectionTime(spectrum));
            return metadata;
        }

        public bool HasSrmSpectra
        {
            get { return HasSrmSpectraInList(SpectrumList); }
        }

        public bool HasIonMobilitySpectra
        {
            get { return HasIonMobilitySpectraInList(); }
        }

        public bool HasChromatogramData
        {
            get
            {
                var len = ChromatogramCount;
                
                for (var i = 0; i < len; i++)
                {
                    var id = GetChromatogramId(i, out _);

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

        private bool HasIonMobilitySpectraInList()
        {
            if (IonMobilitySpectrumList == null || IonMobilitySpectrumList.size() == 0)
                return false;

            // Assume that if any spectra have ion mobility info, all do
            using (var spectrum = IonMobilitySpectrumList.spectrum(0, false))
            {
                return GetIonMobility(spectrum).HasValue;
            }
        }

        public bool IsWatersSonarData()
        {
            if (IonMobilitySpectrumList == null || IonMobilitySpectrumList.size() == 0)
                return false;
            return IonMobilitySpectrumList.isWatersSonarData();
        }

        // Waters SONAR mode uses ion mobility hardware to filter on m/z and reports the results as bins
        public Tuple<int, int> SonarMzToBinRange(double mz, double tolerance)
        {
            int low = -1, high = -1;
            if (IonMobilitySpectrumList != null)
            {
                IonMobilitySpectrumList.sonarMzToBinRange(mz, tolerance, ref low, ref high);
            }
            return new Tuple<int, int>(low, high);
        }

        public double SonarBinToPrecursorMz(int bin)
        {
            double result = 0;
            IonMobilitySpectrumList?.sonarBinToPrecursorMz(bin, ref result); // Returns average of m/z range associated with bin, really only useful for display
            return result;
        }

        private double? GetMaxIonMobilityInList()
        {
            if (IonMobilitySpectrumList == null || IonMobilitySpectrumList.size() == 0)
                return null;

            // Assume that if any spectra have ion mobility values, all do, and all are same range
            double? maxIonMobility = null;
            for (var i = 0; i < IonMobilitySpectrumList.size(); i++)
            {
                using (var spectrum = IonMobilitySpectrumList.spectrum(i, true))
                {
                    var ionMobilities = GetIonMobilityArray(spectrum);
                    var ionMobility = 
                        ionMobilities != null ? IonMobilityValue.GetIonMobilityValue(ionMobilities.Max(), IonMobilityUnits) : GetIonMobility(spectrum);
                    if (!ionMobility.HasValue)
                    {
                        // Assume that if first few regular scans are without IM info, they are all without IM info
                        if (i < 20 || IsWatersLockmassSpectrum(GetSpectrum(spectrum, i)))
                            continue;  // In SONAR data, lockmass scan without IM info doesn't mean there's no IM info
                        if (!maxIonMobility.HasValue)
                            return null; 
                    }
                    if (!maxIonMobility.HasValue)
                    {
                        maxIonMobility = ionMobility.Mobility;
                        if (ionMobilities != null)
                        {
                            break; // 3-array representation, we've seen the range in one go
                        }
                    }
                    else if (Math.Abs(ionMobility.Mobility??0) < Math.Abs(maxIonMobility.Value))
                    {
                        break;  // We've cycled 
                    }
                    else
                    {
                        maxIonMobility = ionMobility.Mobility;
                    }
                }
            }
            return maxIonMobility;
        }

        /// <summary>
        /// Highly probable that we'll look at the same scan several times for different metadata
        /// </summary>
        private int _lastScanIndex = -1;
        private DetailLevel _lastDetailLevel;
        private Spectrum _lastSpectrum;
        private MsDataSpectrum _lastSpectrumInfo;
        private Spectrum GetCachedSpectrum(int scanIndex, DetailLevel detailLevel)
        {
            if (scanIndex != _lastScanIndex || detailLevel > _lastDetailLevel || _lastSpectrum == null)
            {
                _lastScanIndex = scanIndex;
                _lastDetailLevel = detailLevel;
                _lastSpectrum?.Dispose();
                _lastSpectrum = SpectrumList.spectrum(_lastScanIndex, _lastDetailLevel);
                _lastSpectrumInfo = null;
            }
            return _lastSpectrum;
        }
        private Spectrum GetCachedSpectrum(int scanIndex, bool getBinaryData)
        {
            return GetCachedSpectrum(scanIndex, getBinaryData ? DetailLevel.FullData : DetailLevel.FullMetadata);
        }

        public MsDataSpectrum GetSrmSpectrum(int scanIndex)
        {
            var spectrum = GetCachedSpectrum(scanIndex, true);
            {
                return GetSpectrum(IsSrmSpectrum(spectrum) ? spectrum : null, scanIndex);
            }
        }

        public string GetSpectrumId(int scanIndex)
        {
            return SpectrumList.spectrumIdentity(scanIndex).id;
        }

        public bool IsCentroided(int scanIndex)
        {
            var spectrum = GetCachedSpectrum(scanIndex, false);
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
            using var param = spectrum.cvParamChild(CVID.MS_scan_polarity);
            if (param.empty())
                return false;  // Assume positive if undeclared
            return (param.cvid == CVID.MS_negative_scan);
        }

        public bool IsSrmSpectrum(int scanIndex)
        {
            var spectrum = GetCachedSpectrum(scanIndex, false);
            {
                return IsSrmSpectrum(spectrum);
            }
        }

        private static bool IsSrmSpectrum(Spectrum spectrum)
        {
            return spectrum.hasCVParam(CVID.MS_SRM_spectrum);
        }

        public TVal GetMetaDataValue<TVal>(int scanIndex, Func<Spectrum, TVal> getValue, Func<TVal, bool> isUsableValue,
            Func<TVal, TVal> returnValue, ref DetailLevel detailLevel, DetailLevel maxDetailLevel = DetailLevel.FullMetadata)
        {
            var spectrum = GetCachedSpectrum(scanIndex, detailLevel);
            TVal val = getValue(spectrum);
            if (isUsableValue(val) || detailLevel >= maxDetailLevel)
                return returnValue(val);
            // If level is not found with faster metadata methods, try the slower ones.
            if (detailLevel < maxDetailLevel)
                detailLevel++;
            return GetMetaDataValue(scanIndex, getValue, isUsableValue, returnValue, ref detailLevel);
        }

        public int GetMsLevel(int scanIndex)
        {
            return (int) GetMetaDataValue(scanIndex, GetMsLevel, v => v.HasValue, v => v ?? 0, ref _detailMsLevel);
        }

        private static int? GetMsLevel(Spectrum spectrum)
        {
            using CVParam param = spectrum.cvParam(CVID.MS_ms_level);
            if (param.empty())
                return null;
            return (int) param.value;
        }

        public string GetScanDescription(int scanIndex)
        {
            return GetMetaDataValue(scanIndex, GetScanDescription, v => v.IsNullOrEmpty(), v => v, ref _detailScanDescription, DetailLevel.FastMetadata);
        }

        private static string GetScanDescription(Spectrum spectrum)
        {
            const string USERPARAM_SCAN_DESCRIPTION = "scan description";
            using UserParam param = spectrum.userParam(USERPARAM_SCAN_DESCRIPTION);
            if (param.empty())
                return null;
            return param.value.ToString().Trim();
        }

        private double? GetTotalIonCurrent(Spectrum spectrum)
        {
            var param = spectrum.cvParam(CVID.MS_total_ion_current);
            if (param.empty())
            {
                return null;
            }
            return param.value;
        }

        private double? GetInjectionTime(Spectrum spectrum)
        {
            int count = 0;
            double total = 0;
            foreach (var scan in spectrum.scanList.scans)
            {
                var param = scan.cvParam(CVID.MS_ion_injection_time);
                if (!param.empty())
                {
                    count++;
                    total += param.value;
                }
            }
            return count == 0 ? (double?) null : total;
        }

        private static int GetPresetScanConfiguration(Spectrum spectrum)
        {
            try
            {
                if (spectrum.scanList.empty())
                {
                    return 0;
                }

                CVParam param = spectrum.scanList.scans[0].cvParam(CVID.MS_preset_scan_configuration);
                if (param.empty())
                {
                    return 0;
                }

                return (int) param.value;
            }
            catch (InvalidCastException)
            {
                return 0;
            }
        }

        public IonMobilityValue GetIonMobility(int scanIndex) // for non-combined-mode IMS
        {
            return GetMetaDataValue(scanIndex, GetIonMobility, v => v != null && v.HasValue, v => v, ref _detailIonMobility);
        }

        private IonMobilityValue GetIonMobility(Spectrum spectrum) // for non-combined-mode IMS
        {
            using var spectrumScanList = spectrum.scanList;
            if (IonMobilityUnits == eIonMobilityUnits.none || spectrumScanList.scans.Count == 0)
                return IonMobilityValue.EMPTY;
            using var scan = spectrumScanList.scans[0];
            double value;
            var expectedUnits = IonMobilityUnits;
            switch (expectedUnits)
            {
                case eIonMobilityUnits.drift_time_msec:
                {
                    using CVParam driftTime = scan.cvParam(CVID.MS_ion_mobility_drift_time);
                    if (driftTime.empty())
                    {
                        const string USERPARAM_DRIFT_TIME = "drift time";
                        using UserParam param = scan.userParam(USERPARAM_DRIFT_TIME); // support files with the original drift time UserParam
                        if (param.empty())
                            return IonMobilityValue.EMPTY;
                        value =  param.timeInSeconds() * 1000.0;
                    }
                    else
                        value = driftTime.timeInSeconds() * 1000.0;
                    return IonMobilityValue.GetIonMobilityValue(value, expectedUnits);
                }

                case eIonMobilityUnits.inverse_K0_Vsec_per_cm2:
                {
                    using var irim = scan.cvParam(CVID.MS_inverse_reduced_ion_mobility);
                    if (irim.empty())
                    {
                        return IonMobilityValue.EMPTY;
                    }
                    value = irim.value;
                    return IonMobilityValue.GetIonMobilityValue(value, expectedUnits);
                }

                case eIonMobilityUnits.compensation_V:
                {
                    using var faims = spectrum.cvParam(CVID.MS_FAIMS_compensation_voltage);
                    if (faims.empty())
                    {
                        return IonMobilityValue.EMPTY;
                    }
                    value = faims.value;
                    return IonMobilityValue.GetIonMobilityValue(value, expectedUnits);
                }

                default:
                    return IonMobilityValue.EMPTY;
            }
        }

        public double? GetStartTime(int scanIndex)
        {
            return GetMetaDataValue(scanIndex, GetStartTime, v => v.HasValue, v => v ?? 0, ref _detailStartTime);
        }

        private static double? GetStartTime(Spectrum spectrum)
        {
            using var scans = spectrum.scanList.scans;
            if (scans.Count == 0)
                return null;
            using CVParam param = scans[0].cvParam(CVID.MS_scan_start_time);
            if (param.empty())
                return null;
            return param.timeInSeconds() / 60;
        }

        public IList<MsPrecursor> GetPrecursors(int scanIndex, int level)
        {
            if (GetMsLevel(scanIndex) < 2)
                return ImmutableList.Empty<MsPrecursor>();
            return GetMetaDataValue(scanIndex, s => GetPrecursors(s, level), v => v.Count > 0, v => v, ref _detailLevelPrecursors);
        }

        private IList<MsPrecursor> GetPrecursors(Spectrum spectrum, int level)
        {
            // return precursors with highest ms level
            var precursorsByMsLevel = GetPrecursorsByMsLevel(spectrum);
            if (level > precursorsByMsLevel.Count)
                return ImmutableList.Empty<MsPrecursor>();
            return precursorsByMsLevel[level - 1];
        }

        private static ImmutableList<ImmutableList<MsPrecursor>> GetPrecursorsByMsLevel(Spectrum spectrum)
        {
            bool negativePolarity = NegativePolarity(spectrum);
            using var spectrumPrecursors = spectrum.precursors;
            int count = spectrumPrecursors.Count;
            if (count == 0)
                return ImmutableList<ImmutableList<MsPrecursor>>.EMPTY;
            // Most MS/MS spectra will have a single MS1 precursor
            else if (spectrumPrecursors.Count == 1 && GetMsLevel(spectrumPrecursors[0]) == 1)
            {
                var msPrecursor = CreatePrecursor(spectrumPrecursors[0], negativePolarity);
                return ImmutableList.Singleton(ImmutableList.Singleton(msPrecursor));
            }
            return ImmutableList.ValueOf(GetPrecursorsByMsLevel(spectrumPrecursors, negativePolarity));
        }

        private static IEnumerable<ImmutableList<MsPrecursor>> GetPrecursorsByMsLevel(PrecursorList precursors, bool negativePolarity)
        {
            int level = 0;
            foreach (var group in precursors.GroupBy(GetMsLevel).OrderBy(g => g.Key))
            {
                int msLevel = group.Key;
                while (++level < msLevel)
                    yield return ImmutableList<MsPrecursor>.EMPTY;

                yield return ImmutableList.ValueOf(group.Select(p =>
                    CreatePrecursor(p, negativePolarity)));
            }
        }

        private static MsPrecursor CreatePrecursor(Precursor p, bool negativePolarity)
        {
            return new MsPrecursor
            {
                PrecursorMz = GetPrecursorMz(p, negativePolarity),
                PrecursorCollisionEnergy = GetPrecursorCollisionEnergy(p),
                IsolationWindowTargetMz =
                    GetSignedMz(GetIsolationWindowValue(p, CVID.MS_isolation_window_target_m_z),
                        negativePolarity),
                IsolationWindowLower = GetIsolationWindowValue(p, CVID.MS_isolation_window_lower_offset),
                IsolationWindowUpper = GetIsolationWindowValue(p, CVID.MS_isolation_window_upper_offset),
            };
        }

        private static int GetMsLevel(Precursor precursor)
        {
            UserParam msLevelParam = null;
            try
            {
                msLevelParam = precursor.isolationWindow.userParam("ms level");
                if (msLevelParam.empty())
                    msLevelParam = precursor.userParam("ms level");
                return msLevelParam.empty() ? 1 : (int)msLevelParam.value;
            }
            finally
            {
                msLevelParam?.Dispose();
            }

        }

        private static int? GetChargeStateValue(Precursor precursor)
        {
            if (precursor.selectedIons == null || precursor.selectedIons.Count == 0)
                return null;
            using var param = precursor.selectedIons[0].cvParam(CVID.MS_charge_state);
            if (param.empty())
                return null;
            return (int)param.value;
        }

        private static IEnumerable<MsPrecursor> GetMs1Precursors(Spectrum spectrum)
        {
            bool negativePolarity = NegativePolarity(spectrum);
            foreach (var scanWindow in spectrum.scanList.scans[0].scanWindows)
            {
                if (!scanWindow.userParam(CENTROIDED_MIN_MAX).empty())
                {
                    // The upper and lower window limits cannot be trusted.
                    // Just return a MsPrecursor whose IsolationWindowTargetMz can be used
                    // to determine the polarity of the spectrum.
                    yield return new MsPrecursor
                    {
                        IsolationWindowTargetMz = new SignedMz(1, negativePolarity)
                    };
                }
                else
                {
                    double windowStart = scanWindow.cvParam(CVID.MS_scan_window_lower_limit).value;
                    double windowEnd = scanWindow.cvParam(CVID.MS_scan_window_upper_limit).value;
                    double isolationWidth = (windowEnd - windowStart) / 2;
                    yield return new MsPrecursor
                    {
                        IsolationWindowTargetMz = new SignedMz(windowStart + isolationWidth, negativePolarity),
                        IsolationWindowLower = isolationWidth,
                        IsolationWindowUpper = isolationWidth
                    };
                }
            }
        }

        private static SignedMz? GetPrecursorMz(Precursor precursor, bool negativePolarity)
        {
            // CONSIDER: Only the first selected ion m/z is considered for the precursor m/z
            using var selectedIon = precursor.selectedIons.FirstOrDefault();
            if (selectedIon == null)
                return null;
            return GetSignedMz(selectedIon.cvParam(CVID.MS_selected_ion_m_z).value, negativePolarity);
        }

        private static SignedMz? GetSignedMz(double? mz, bool negativePolarity)
        {
            if (mz.HasValue)
                return new SignedMz(mz.Value, negativePolarity);
            return null;
        }

        private static double? GetPrecursorCollisionEnergy(Precursor precursor)
        {
            using var param = precursor.activation.cvParam(CVID.MS_collision_energy);
            if (param.empty())
                return null;
            return (double)param.value;
        }

        private static double? GetIsolationWindowValue(Precursor precursor, CVID cvid)
        {
            using var term = precursor.isolationWindow.cvParam(cvid);
            if (!term.empty())
                return term.value;
            return null;
        }

        public void Write(string path)
        {
            MSDataFile.write(_msDataFile, path);
        }

        public virtual void Dispose()
        {
            _lastSpectrum?.Dispose();
            _lastScanIndex = -1;
            if (_spectrumList != null)
                _spectrumList.Dispose();
            _spectrumList = null;
            if (_chromatogramList != null)
                _chromatogramList.Dispose();
            _chromatogramList = null;
            if (_ionMobilitySpectrumList != null)
                _ionMobilitySpectrumList.Dispose();
            _ionMobilitySpectrumList = null;
            if (_msDataFile != null)
                _msDataFile.Dispose();
            _msDataFile = null;
        }

        public string FilePath { get; private set; }
        public int SampleIndex { get; private set; }

        /// <summary>
        /// Returns true iff MsDataFileImpl's reader list can read the filepath.
        /// </summary>
        public static bool IsValidFile(string filepath)
        {
            if (!File.Exists(filepath) && !Directory.Exists(filepath))
                return false;

            try
            {
                using var msd = new MSData();
                FULL_READER_LIST.read(filepath, msd);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public sealed class MsDataConfigInfo
    {
        public int Spectra { get; set; }
        public string ContentType { get; set; }
        public string IonSource { get; set; }
        public string Analyzer { get; set; }
        public string Detector { get; set; }
    }


    public struct MsPrecursor
    {
        public SignedMz? PrecursorMz { get; set; }
        public int? ChargeState { get; set; }
        public double? PrecursorCollisionEnergy  { get; set; }
        public SignedMz? IsolationWindowTargetMz { get; set; }
        public double? IsolationWindowUpper { get; set; } // Add this to IsolationWindowTargetMz to get window upper bound
        public double? IsolationWindowLower { get; set; } // Subtract this from IsolationWindowTargetMz to get window lower bound
        public SignedMz? IsolationMz
        {
            get
            {
                SignedMz? targetMz = IsolationWindowTargetMz ?? PrecursorMz;
                // If the isolation window is not centered around the target m/z, then return a
                // m/z value that is centered in the isolation window.
                if (targetMz.HasValue && IsolationWindowUpper.HasValue && IsolationWindowLower.HasValue &&
                        IsolationWindowUpper.Value != IsolationWindowLower.Value)
                    return new SignedMz((targetMz.Value * 2 + IsolationWindowUpper.Value - IsolationWindowLower.Value) / 2.0, targetMz.Value.IsNegative);
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

    public sealed class MsDataSpectrum
    {

        private IonMobilityValue _ionMobility;
        public SpectrumMetadata Metadata { get; set; }
        public string SourceFilePath { get; set; }
        public string Id { get; set; }
        public int Level { get; set; }
        public int Index { get; set; } // index into parent file, if any
        public double? RetentionTime { get; set; }

        /// <summary>
        /// For non-combined-mode IMS
        /// </summary>
        public IonMobilityValue IonMobility
        {
            get { return _ionMobility ?? IonMobilityValue.EMPTY; }
            set { _ionMobility = value; }
        }

        /// <summary>
        /// The range of ion mobilities that were scanned (for zero vs missing value determination)
        /// </summary>
        public double? IonMobilityMeasurementRangeLow { get; set; }
        public double? IonMobilityMeasurementRangeHigh { get; set; }

        public ImmutableList<MsPrecursor> GetPrecursorsByMsLevel(int level)
        {
            if (PrecursorsByMsLevel == null || level > PrecursorsByMsLevel.Count)
                return ImmutableList<MsPrecursor>.EMPTY;
            return PrecursorsByMsLevel[level - 1];
        }

        public ImmutableList<ImmutableList<MsPrecursor>> PrecursorsByMsLevel { get; set; }

        public ImmutableList<MsPrecursor> Precursors
        {
            get
            {
                if (PrecursorsByMsLevel == null || PrecursorsByMsLevel.Count == 0)
                    return ImmutableList<MsPrecursor>.EMPTY;

                return GetPrecursorsByMsLevel(PrecursorsByMsLevel.Count);
            }
            set
            {
                PrecursorsByMsLevel = ImmutableList.Singleton(ImmutableList.ValueOf(value));
            }
        }

        public bool Centroided { get; set; }
        public bool NegativeCharge { get; set; } // True if negative ion mode
        public double[] Mzs { get; set; }
        public double[] Intensities { get; set; }
        public double[] IonMobilities { get; set; } // for combined-mode IMS (may be null)
        public double? MinIonMobility { get; set; }
        public double? MaxIonMobility { get; set; }
        public int WindowGroup { get; set; } // For Bruker diaPASEF
        public string ScanDescription { get; set; }

        public MsInstrumentConfigInfo InstrumentInfo { get; set; }
        public string InstrumentSerialNumber { get; set; }
        public string InstrumentVendor { get; set; }

        public static int WatersFunctionNumberFromId(string id, bool isCombinedIonMobility)
        {
            return int.Parse(id.Split('.')[isCombinedIonMobility ? 1 :0]); // Yes, this will throw if it's not in dotted format - and that's good
        }

        public override string ToString() // For debugging convenience, not user-facing
        {
            return $@"id={Id} idx={Index} mslevel={Level} rt={RetentionTime} im={MinIonMobility??_ionMobility?.Mobility}:{MaxIonMobility??_ionMobility?.Mobility}";
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
            Model = model != null ? model.Trim() : string.Empty;
            Ionization = ionization != null ? ionization.Replace('\n', ' ').Trim() : string.Empty;
            Analyzer = analyzer != null ? analyzer.Replace('\n', ' ').Trim() : string.Empty;
            Detector = detector != null ? detector.Replace('\n', ' ').Trim() : string.Empty;
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
                int result = Model != null ? Model.GetHashCode() : 0; // N.B. generated code starts with result = 0, which causes an inspection warning
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
            if (_scanStack.Count >= _cacheSize)
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
