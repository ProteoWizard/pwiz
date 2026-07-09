/*
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
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;


using Pwiz.Analysis;
using Pwiz.Analysis.DiaUmpire;
using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util;

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
    public partial class MsDataFileImpl : IDisposable
    {
        private static readonly ReaderList FULL_READER_LIST = ReaderList.Default;

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
                return ProteoWizardVersion.ToString();
            }
        }

        public static IEnumerable<KeyValuePair<string, IList<string>>> GetFileExtensionsByType()
        {
            foreach (var typeExtsPair in FULL_READER_LIST.FileExtensionsByType())
                yield return typeExtsPair;
        }

        public static bool SupportsVendorPeakPicking(string path)
        {
            return SpectrumList_PeakPicker.SupportsVendorPeakPicking(path);
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
        protected ISpectrumList _spectrumList;
        protected IChromatogramList _chromatogramList;
        private bool _providesConversionCCStoIonMobility;
        private IonMobilityUnits _ionMobilityUnits;
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
        
        private DetailLevel _detailWindowGroup = DetailLevel.InstantMetadata;

        private CVID? _cvidIonMobility;

        private static double[] ToArray(BinaryDataArray binaryDataArray)
        {
            return binaryDataArray.Data.ToArray();
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
            return FULL_READER_LIST.ReadIds(path);
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
            bool trimNativeId = true,
            bool passEntireDiaPasefFrame = false // Ask for Bruker DiaPASEF frames as a single chunk
            )
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
                    SimAsSpectra = simAsSpectra,
                    SrmAsSpectra = srmAsSpectra,
                    AcceptZeroLengthSpectra = acceptZeroLengthSpectra,
                    IgnoreZeroIntensityPoints = ignoreZeroIntensityPoints,
                    PreferOnlyMsLevel = !ForceUncombinedIonMobility && combineIonMobilitySpectra ? 0 : preferOnlyMsLevel,
                    AllowMsMsWithoutPrecursor = false,
                    CombineIonMobilitySpectra = !ForceUncombinedIonMobility && combineIonMobilitySpectra,
                    IgnoreCalibrationScans = true, // For Waters, we don't need to hear about lockmass values
                    ReportSonarBins = true, // For Waters SONAR data, report bin number instead of false drift time
                    IncludeIsolationArrays = false, // For Bruker TIMS data, don't pass the isolation arrays (we infer from WindowGroup and IM)
                    PassEntireDiaPasefFrame = passEntireDiaPasefFrame && combineIonMobilitySpectra && path.EndsWith(@".d"), // For Bruker TIMS data, pass the entire frame at once if we have window group table (ie not mzML)
                    GlobalChromatogramsAreMs1Only = true
                };
                _lockmassParameters = lockmassParameters;
                FULL_READER_LIST.Read(path, _msDataFile, sampleIndex, _config);
                _requireVendorCentroidedMS1 = requireVendorCentroidedMS1;
                _requireVendorCentroidedMS2 = requireVendorCentroidedMS2;
                _trimNativeID = trimNativeId;
            }
        }

        // Uncomment to run leak check for C++/CLI objects
        /*~MsDataFileImpl()
        {
            FULL_READER_LIST.Dispose();
            pwiz.CLI.util.ObjectStructorLog.LeakCheck();
        }*/

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

        public string RunId { get { return _msDataFile.Run.Id; } }

        public bool RequireVendorCentoridedMs1 => _requireVendorCentroidedMS1;
        public bool RequireVendorCentoridedMs2 => _requireVendorCentroidedMS2;

        public DateTime? RunStartTime
        {
            get
            {
                string stampText = _msDataFile.Run.StartTimeStamp;
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
                int spectra = SpectrumList.Count;
                string ionSource = string.Empty;
                string analyzer = string.Empty;
                string detector = string.Empty;
                Dictionary<int,List<DiaFrameMsMsWindowItem>> diaFrameMsMsWindowInfoList = null; // Bruker diaPASEF WindowGroup details

                foreach (InstrumentConfiguration ic in _msDataFile.InstrumentConfigurations)
                {
                    string instrumentIonSource;
                    string instrumentAnalyzer;
                    string instrumentDetector;
                    GetInstrumentConfig(ic, out instrumentIonSource, out instrumentAnalyzer, out instrumentDetector, out var newDiaFrameMsMsWindowList);

                    if (ionSource.Length > 0)
                        ionSource += @", ";
                    ionSource += instrumentIonSource;

                    if (analyzer.Length > 0)
                        analyzer += @", ";
                    analyzer += instrumentAnalyzer;

                    if (detector.Length > 0)
                        detector += @", ";
                    detector += instrumentDetector;

                    if (newDiaFrameMsMsWindowList != null)
                        diaFrameMsMsWindowInfoList = newDiaFrameMsMsWindowList;
                }

                HashSet<string> contentTypeSet = new HashSet<string>();
                var fileDescriptionFileContent = _msDataFile.FileDescription.FileContent;
                foreach (CVParam term in fileDescriptionFileContent.CVParams)
                    contentTypeSet.Add(term.Name);
                var contentTypes = contentTypeSet.ToArray();
                Array.Sort(contentTypes);
                string contentType = String.Join(@", ", contentTypes);

                return new MsDataConfigInfo
                           {
                               Analyzer = analyzer,
                               ContentType = contentType,
                               Detector = detector,
                               IonSource = ionSource,
                               Spectra = spectra,
                               DiaFrameMsMsWindowsTable = diaFrameMsMsWindowInfoList
                           };
            }
        }

        public bool IsValidDiaPasefPoint(int windowGroup, double IM, double IsoMzLow, double isoMzHigh)
        {
            return ConfigInfo.IsValidDiaPasefPoint(windowGroup, IM, IsoMzLow, isoMzHigh);

        }

        public class DiaFrameMsMsWindowItem
        {
            public DiaFrameMsMsWindowItem(int windowGroup, double imLow, double imHigh, double isoMzLow, double isoMzHigh, double collisionEnergy)
            {
                WindowGroup = windowGroup;
                ImLow = imLow;
                ImHigh = imHigh;
                IsoMzLow = isoMzLow;
                IsoMzHigh = isoMzHigh;
                CollisionEnergy = collisionEnergy;
            }

            public int WindowGroup { get; internal set;}
            public double ImLow { get; private set; }
            public double ImHigh { get; private set; }
            public double IsoMzLow { get; private set; }
            public double IsoMzHigh { get; private set; }
            public double CollisionEnergy { get; private set; }

            public bool IsValidPoint(int windowGroup, double im, double isoMzLow, double isoMzHigh)
            {
                return windowGroup == WindowGroup &&
                       im <= ImHigh && im >= ImLow &&
                       isoMzLow <= IsoMzHigh && IsoMzHigh >= isoMzLow;
            }
        }

        /// <summary>
        /// Attempt to get a non-unicode path for use with launched processes that have trouble with Unicode paths
        ///
        /// N.B should give same result as PathEx.GetNonUnicodePath, primary use of this method is to test that. Prefer PathEx.GetNonUnicodePath when possible.
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns>path with Unicode-containing segments replaced with Windows 8.3 equivalent, if possible</returns>
        public static string GetNonUnicodePath(string path)
        {
            return Pwiz.Util.Misc.Filesystem.GetNonUnicodePath(path);
        }

        private class InstrumentConfigurationCacheValues
        {
            public InstrumentConfigurationCacheValues(string ionSource, string analyzer, string detector, Dictionary<int, List<DiaFrameMsMsWindowItem>> diaFrameMsMsWindowInfo)
            {
                this.ionSource = ionSource;
                this.analyzer = analyzer;
                this.detector = detector;
                this.diaFrameMsMsWindowInfo = diaFrameMsMsWindowInfo;
            }

            public string ionSource { get; private set; }
            public string analyzer { get; private set; }
            public string detector { get; private set; }
            public Dictionary<int, List<DiaFrameMsMsWindowItem>> diaFrameMsMsWindowInfo { get; private set; }
        }

        private Dictionary<string, InstrumentConfigurationCacheValues> InstrumentConfigurationCache = new Dictionary<string, InstrumentConfigurationCacheValues>();

        private void GetInstrumentConfig(InstrumentConfiguration ic, out string ionSource, out string analyzer, out string detector, out Dictionary<int, List<DiaFrameMsMsWindowItem>> diaFrameMsMsWindowInfo)
        {
            if (InstrumentConfigurationCache.TryGetValue(ic.Id, out var values))
            {
                ionSource = values.ionSource;
                analyzer = values.analyzer;
                detector = values.detector;
                diaFrameMsMsWindowInfo = values.diaFrameMsMsWindowInfo;
                return;
            }

            // ReSharper disable CollectionNeverQueried.Local  (why does ReSharper warn on this?)
            SortedDictionary<int, string> ionSources = new SortedDictionary<int, string>();
            SortedDictionary<int, string> analyzers = new SortedDictionary<int, string>();
            SortedDictionary<int, string> detectors = new SortedDictionary<int, string>();
            // ReSharper restore CollectionNeverQueried.Local

            foreach (Component c in ic.ComponentList)
            {
                CVParam term = null;
                switch (c.Type)
                {
                    case ComponentType.Source:
                        term = c.CvParamChild(CVID.MS_ionization_type);
                        if (!term.IsEmpty)
                            ionSources.Add(c.Order, term.Name);
                        else
                        {
                            // If we did not find the ion source in a CVParam it may be in a UserParam
                            UserParam uParam = c.UserParam(@"msIonisation");
                            if (HasInfo(uParam))
                            {
                                ionSources.Add(c.Order, uParam);
                            }
                        }
                        break;
                    case ComponentType.Analyzer:
                        term = c.CvParamChild(CVID.MS_mass_analyzer_type);
                        if (!term.IsEmpty)
                            analyzers.Add(c.Order, term.Name);
                        else
                        {
                            // If we did not find the analyzer in a CVParam it may be in a UserParam
                            UserParam uParam = c.UserParam(@"msMassAnalyzer");
                            if (HasInfo(uParam))
                            {
                                analyzers.Add(c.Order, uParam);
                            }
                        }
                        break;
                    case ComponentType.Detector:
                        term = c.CvParamChild(CVID.MS_detector_type);
                        if (!term.IsEmpty)
                            detectors.Add(c.Order, term.Name);
                        else
                        {
                            // If we did not find the detector in a CVParam it may be in a UserParam
                            UserParam uParam = c.UserParam(@"msDetector");
                            if (HasInfo(uParam))
                            {
                                detectors.Add(c.Order, uParam);
                            }
                        }
                        break;
                }
            }

            ionSource = String.Join(@"/", new List<string>(ionSources.Values).ToArray());

            analyzer = String.Join(@"/", new List<string>(analyzers.Values).ToArray());

            detector = String.Join(@"/", new List<string>(detectors.Values).ToArray());

            // Parse the Bruker DiaFrameMsMsWindowsTable if available
            diaFrameMsMsWindowInfo = null;
            foreach (var u in ic.UserParams)
            {
                if (u.Name.Equals(@"WindowGroup"))
                {
                    diaFrameMsMsWindowInfo ??= new Dictionary<int, List<DiaFrameMsMsWindowItem>>();
                    var line = u.Value.ToString();
                    if (string.IsNullOrEmpty(line))
                        continue;
                    var columns = line.Split(',');
                    if (int.TryParse(columns[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var windowGroup) &&
                        double.TryParse(columns[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var imHigh) &&
                        double.TryParse(columns[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var imLow) &&
                        double.TryParse(columns[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var isolationMz) &&
                        double.TryParse(columns[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var isolationWidth) &&
                        double.TryParse(columns[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var collisionEnergy))
                    {
                        if (!diaFrameMsMsWindowInfo.TryGetValue(windowGroup, out var list))
                        {
                            diaFrameMsMsWindowInfo.Add(windowGroup, list = new List<DiaFrameMsMsWindowItem>());
                        }
                        list.Add(new DiaFrameMsMsWindowItem(windowGroup, imLow, imHigh, isolationMz - isolationWidth / 2, isolationMz + isolationWidth / 2, collisionEnergy));
                    }
                    else
                    {
                        diaFrameMsMsWindowInfo = null;
                        throw new ArgumentException(@"unexpected format in DiaFrameMsMsWindowsTable");
                    }
                }
            }

            InstrumentConfigurationCache.Add(ic.Id, new InstrumentConfigurationCacheValues(ionSource, analyzer, detector, diaFrameMsMsWindowInfo));
        }

        public bool IsProcessedBy(string softwareName)
        {
            foreach (var softwareApp in _msDataFile.Software)
            {
                if (softwareApp.Id.Contains(softwareName))
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
            return _msDataFile.FileDescription.FileContent.CVParams.Select(cv => cv.Name);
        }

        /// <summary>
        /// Record any instrument info found in the file, along with any Waters lockmass info we have
        /// </summary>
        public IEnumerable<MsInstrumentConfigInfo> GetInstrumentConfigInfoList()
        {
            using (_perf.CreateTimer(@"GetInstrumentConfigList"))
            {
                IList<MsInstrumentConfigInfo> configList = new List<MsInstrumentConfigInfo>();

                foreach (InstrumentConfiguration ic in _msDataFile.InstrumentConfigurations)
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

        private Dictionary<string, MsInstrumentConfigInfo> MsInstrumentConfigInfoCache = new Dictionary<string, MsInstrumentConfigInfo>();

        public MsInstrumentConfigInfo CreateMsInstrumentConfigInfo(InstrumentConfiguration ic)
        {
            if (ic == null)
                return null;
            if (MsInstrumentConfigInfoCache.TryGetValue(ic.Id, out var cached))
                return cached;
            string instrumentModel = null;
            string ionization;
            string analyzer;
            string detector;

            CVParam param = ic.CvParamChild(CVID.MS_instrument_model);
            if (!param.IsEmpty && param.Cvid != CVID.MS_instrument_model)
            {
                instrumentModel = param.Name;

                // if instrument model free string is present, it is probably more specific than CVID model (which may only indicate manufacturer)
                UserParam uParam = ic.UserParam(@"instrument model");
                if (HasInfo(uParam))
                {
                    instrumentModel = uParam;
                }
            }

            if (instrumentModel == null)
            {
                // If we did not find the instrument model in a CVParam it may be in a UserParam
                UserParam uParam = ic.UserParam(@"msModel");
                if (HasInfo(uParam))
                {
                    instrumentModel = uParam;
                }
                else
                {
                    UserParam uParam2 = ic.UserParam(@"instrument model");
                    if (HasInfo(uParam2))
                    {
                        instrumentModel = uParam2;
                    }
                }
            }

            // get the ionization type, analyzer and detector
            GetInstrumentConfig(ic, out ionization, out analyzer, out detector, out var diaFrameMsMsWindowDict);

            if (instrumentModel != null || ionization != null || analyzer != null || detector != null)
            {
                var result = new MsInstrumentConfigInfo(instrumentModel, ionization, analyzer, detector, diaFrameMsMsWindowDict);
                MsInstrumentConfigInfoCache.Add(ic.Id, result);
                return result;
            }
            else
                return null;
        }

        public string GetInstrumentSerialNumber()
        {
            return _msDataFile.InstrumentConfigurations.FirstOrDefault(o => o.HasCVParam(CVID.MS_instrument_serial_number))
                                                          ?.CvParam(CVID.MS_instrument_serial_number).Value.ToString();
        }

        private static bool HasInfo(UserParam uParam)
        {
            return !uParam.IsEmpty && !String.IsNullOrEmpty(uParam) &&
                   !String.Equals(@"unknown", uParam.Value.ToString().ToLowerInvariant());
        }

        public static string GetCvParamName(string cvParamAccession)
        {
            return CvLookup.CvTermInfo(cvParamAccession).ShortName;
        }

        public void GetNativeIdAndFileFormat(out string nativeIdFormatAccession, out string fileFormatAccession)
        {
            var firstSource = _msDataFile.FileDescription.SourceFiles.First(source =>
                source.HasCVParamChild(CVID.MS_nativeID_format) &&
                source.HasCVParamChild(CVID.MS_file_format));
            nativeIdFormatAccession = CvLookup.CvTermInfo(firstSource.CvParamChild(CVID.MS_nativeID_format).Cvid).Id;
            fileFormatAccession = CvLookup.CvTermInfo(firstSource.CvParamChild(CVID.MS_file_format).Cvid).Id;
        }

        public bool IsABFile
        {
            get { return _msDataFile.FileDescription.SourceFiles.Any(source => source.HasCVParam(CVID.MS_ABI_WIFF_format)); }
        }

        public bool IsMzWiffXml
        {
            get { return IsProcessedBy(@"mzWiff"); }
        }

        public bool IsAgilentFile
        {
            get { return _msDataFile.FileDescription.SourceFiles.Any(source => source.HasCVParam(CVID.MS_Agilent_MassHunter_format)); }
        }

        public bool IsThermoFile
        {
            get { return _msDataFile.FileDescription.SourceFiles.Any(source => source.HasCVParam(CVID.MS_Thermo_RAW_format)); }
        }

        public bool IsWatersFile
        {
            get { return _msDataFile.FileDescription.SourceFiles.Any(source => source.HasCVParam(CVID.MS_Waters_raw_format)); }
        }

        public bool PassEntireDiaPasefFrame
        {
            get { return _config.PassEntireDiaPasefFrame; }
        }

        public bool HasDeclaredMSnSpectra
        {
            get { return _msDataFile.FileDescription.FileContent.HasCVParam(CVID.MS_MSn_spectrum); }
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
                        _msDataFile.Run.SpectrumList != null &&
                        !_msDataFile.Run.SpectrumList.IsEmpty &&
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
            get { return _msDataFile.Software.Any(software => software.HasCVParamChild(CVID.MS_Shimadzu_Corporation_software)); }
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
            get { return SpectrumList != null && _providesConversionCCStoIonMobility; } // Checking ISpectrumList provokes initialization of ionMobility info
        }

        private SpectrumList_IonMobility IonMobilitySpectrumList
        {
            get { return SpectrumList == null ? null : _ionMobilitySpectrumList; }  // Checking ISpectrumList provokes initialization of ionMobility info
        }

        public IonMobilityValue IonMobilityFromCCS(double ccs, double mz, int charge)
        {
            return IonMobilityValue.GetIonMobilityValue(IonMobilitySpectrumList.CcsToIonMobility(ccs, mz, charge), IonMobilityUnits);
        }

        public double CCSFromIonMobilityValue(IonMobilityValue ionMobilityValue, double mz, int charge)
        {
            return ionMobilityValue.Mobility.HasValue ? IonMobilitySpectrumList.IonMobilityToCcs(ionMobilityValue.Mobility.Value, mz, charge) : 0;
        }

        public double CCSFromIonMobility(double ionMobility, double mz, int charge)
        {
            return IonMobilitySpectrumList.IonMobilityToCcs(ionMobility, mz, charge);
        }

        public eIonMobilityUnits IonMobilityUnits
        {
            get
            {
                switch (_ionMobilityUnits)
                {
                    case Pwiz.Data.MsData.Spectra.IonMobilityUnits.None:
                        return eIonMobilityUnits.none;
                    case Pwiz.Data.MsData.Spectra.IonMobilityUnits.DriftTimeMsec:
                        return eIonMobilityUnits.drift_time_msec;
                    case Pwiz.Data.MsData.Spectra.IonMobilityUnits.InverseReducedIonMobilityVsecPerCm2:
                        return eIonMobilityUnits.inverse_K0_Vsec_per_cm2;
                    case Pwiz.Data.MsData.Spectra.IonMobilityUnits.CompensationV:
                        return eIonMobilityUnits.compensation_V;
                    case Pwiz.Data.MsData.Spectra.IonMobilityUnits.WatersSonar: // Not really ion mobility, but uses IMS hardware to filter precursor m/z
                        return eIonMobilityUnits.waters_sonar;
                    default:
                        throw new InvalidDataException(string.Format(@"unknown ion mobility type {0}", _ionMobilityUnits));
                }
            }
        }

        protected virtual IChromatogramList ChromatogramList
        {
            get
            {
                return _chromatogramList = _chromatogramList ??
                    _msDataFile.Run.ChromatogramList;
            }
        }

        protected virtual ISpectrumList SpectrumList
        {
            get
            {
                if (_spectrumList == null)
                {
                    string centroidLevels = null;
                    _spectrumList = _msDataFile.Run.SpectrumList;
                    bool hasSrmSpectra = HasSrmSpectraInList();
                    if (!hasSrmSpectra)
                    {
                        if (_requireVendorCentroidedMS1 && _requireVendorCentroidedMS2)
                            centroidLevels = @"1-";
                        else if (_requireVendorCentroidedMS1)
                            centroidLevels = @"1";
                        else if (_requireVendorCentroidedMS2)
                            centroidLevels = @"2-";
                    }
                    if (centroidLevels != null && _spectrumList != null)
                    {
                        _spectrumList = new SpectrumList_PeakPicker(_spectrumList,
                            new VendorOnlyPeakDetector(), // Throws an exception when no vendor centroiding available
                            true, centroidLevels);
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
                        _ionMobilityUnits = _ionMobilitySpectrumList.IonMobilityUnits;
                        _providesConversionCCStoIonMobility = _ionMobilitySpectrumList.CanConvertIonMobilityAndCcs(_ionMobilityUnits);
                    }
                    if (IsWatersFile  && _spectrumList != null && !_spectrumList.CalibrationSpectraAreOmitted && !hasSrmSpectra)
                    {
                        for (var index = 0; index < _spectrumList.Count; index++)
                        {
                            // If lockmass scans aren't already being omitted at the top level, try to filter them out here.
                            // If the first seen MS spectrum has MS1 data and function > 1 assume it's the lockspray function, 
                            // and thus to be omitted from chromatogram extraction. We've seen files where first spectrum is
                            // "electromagnetic radiation spectrum", for example, which has no MS level value.
                            // N.B. for msE data we will always assume function 3 and greater are to be omitted
                            // N.B. in all cases this assumes that any functions greater than the lockmass function are to be ignored
                            // (e.g. "electromagnetic radiation spectrum") 
                            // CONSIDER(bspratt) I really wish there was some way to communicate decisions like this to the user
                            var spectrum = _spectrumList.GetSpectrum(index, DetailLevel.FullMetadata);
                            var msLevel = GetMsLevel(spectrum);
                            if (msLevel == 1)
                            {
                                var function = MsDataSpectrum.WatersFunctionNumberFromId(Id.Abbreviate(spectrum.Id), 
                                    HasCombinedIonMobilitySpectra && spectrum.Id.Contains(MERGED_TAG));
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
            return GetSpectrumMetadata(_msDataFile.Run.SpectrumList.GetSpectrum(spectrumIndex, DetailLevel.FullMetadata));
        }

        public double? GetMaxIonMobility()
        {
            return GetMaxIonMobilityInList();
        }

        public bool HasCombinedIonMobilitySpectra => SpectrumList != null && IonMobilityUnits != eIonMobilityUnits.none &&  _ionMobilitySpectrumList != null && _ionMobilitySpectrumList.HasCombinedIonMobility;

        /// <summary>
        /// Gets the value of the MS_sample_name CV param of first sample in the MSData object, or null if there is no sample information.
        /// </summary>
        public string GetSampleId()
        {
            var samples = _msDataFile.Samples;
            if (samples.Count > 0)
            {
                var cvParam = samples[0].CvParam(CVID.MS_sample_name);
                var sampleId = (string) cvParam;
                if (sampleId.Length > 0)
                    return sampleId;
            }
            return null;
        }

        public int ChromatogramCount
        {
            get { return ChromatogramList != null ? ChromatogramList.Count : 0; }
        }

        public string GetChromatogramId(int index, out int indexId)
        {
            var cid = ChromatogramList.ChromatogramIdentity(index);            {
                indexId = cid.Index;
                return cid.Id;                
            }
        }

        private static readonly string[] msLevelOrFunctionArrayNames = { "ms level", "function" };

        public double? GetChromatogramCollisionEnergy(int chromIndex)
        {
            var chrom = ChromatogramList.GetChromatogram(chromIndex, DetailLevel.FullMetadata);
            return chrom.Precursor?.Activation?.CvParam(CVID.MS_collision_energy);
        }
        
        public void GetChromatogramMetadata(int chromIndex, out string id, out bool? isNegativePolarity, out double precursorMz, out double productMz)
        {
            Chromatogram chrom = ChromatogramList.GetChromatogram(chromIndex, DetailLevel.FullMetadata);
            id = chrom.Id;
            isNegativePolarity = chrom.CvParamChild(CVID.MS_scan_polarity).Cvid switch
            {
                CVID.MS_positive_scan => false,
                CVID.MS_negative_scan => true,
                _ => null
            };
            precursorMz = chrom.Precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z);
            productMz = chrom.Product.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z);
        }

        public void GetChromatogram(int chromIndex, out string id,
            out float[] timeArray, out float[] intensityArray, bool onlyMs1OrFunction1 = false)
        {
            Chromatogram chrom = ChromatogramList.GetChromatogram(chromIndex, true);            {
                id = chrom.Id;
                var timeArrayData = chrom.GetTimeArray().Data;

                // convert time to minutes
                var timeArrayParam = chrom.GetTimeArray().CvParamChild(CVID.MS_binary_data_array);
                float timeUnitMultiple;
                switch (timeArrayParam.Units)
                {
                    case CVID.UO_nanosecond: timeUnitMultiple = 60 * 1e9f; break;
                    case CVID.UO_microsecond: timeUnitMultiple = 60 * 1e6f; break;
                    case CVID.UO_millisecond: timeUnitMultiple = 60 * 1e3f; break;
                    case CVID.UO_second: timeUnitMultiple = 60; break;
                    case CVID.UO_minute: timeUnitMultiple = 1; break;
                    case CVID.UO_hour: timeUnitMultiple = 1f / 60; break;

                    default:
                        throw new InvalidDataException($"unsupported time unit in chromatogram: {timeArrayParam.UnitsName}");
                }
                timeUnitMultiple = 1 / timeUnitMultiple;

                if (!onlyMs1OrFunction1)
                {
                    timeArray = new float[timeArrayData.Count];
                    for (int i = 0; i < timeArray.Length; ++i)
                        timeArray[i] = (float) timeArrayData[i] * timeUnitMultiple;
                    intensityArray = ToFloatArray(chrom.GetIntensityArray().Data);
                }
                else
                {
                    // get array of ms level or function for each chromatogram point
                    var msLevelOrFunctionArray = chrom.IntegerDataArrays.FirstOrDefault(o =>
                        msLevelOrFunctionArrayNames.Contains(o.CvParam(CVID.MS_non_standard_data_array).Value.ToString()));

                    // if array is missing or empty, return no chromatogram data points (because they could be from any ms level or function)
                    if (msLevelOrFunctionArray == null || msLevelOrFunctionArray.Data.Count != chrom.BinaryDataArrays[0].Data.Count)
                    {
                        timeArray = intensityArray = null;
                        return;
                    }

                    var timeList = new List<float>();
                    var intensityList = new List<float>();
                    var intensityArrayData = chrom.GetIntensityArray().Data;
                    var msLevelOrFunctionArrayData = msLevelOrFunctionArray.Data;

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
                if (ChromatogramList == null || ChromatogramList.IsEmpty)
                {
                    return null;
                }
                var chromatogram = ChromatogramList.GetChromatogram(0, true);                {
                    if (chromatogram == null)
                    {
                        return null;
                    }
                    TimeIntensityPairList timeIntensityPairList = new TimeIntensityPairList();
                    chromatogram.GetTimeIntensityPairs(ref timeIntensityPairList);
                    double[] times = new double[timeIntensityPairList.Count];
                    for (int i = 0; i < times.Length; i++)
                    {
                        times[i] = timeIntensityPairList[i].Time;
                    }
                    return times;
                }
            }
        }

        private const string MERGED_TAG = @"merged="; // Our cue that the scan in question represents 3-array IMS data

        public double[] GetTotalIonCurrent()
        {
            if (ChromatogramList == null || ChromatogramList.Count == 0)
            {
                return null;
            }
            var chromatogram = ChromatogramList.GetChromatogram(0, true);            {
                return chromatogram?.GetIntensityArray()?.Data.ToArray();
            }
        }

        public abstract class QcTraceQuality
        {
            public const string Pressure = @"pressure";
            public const string FlowRate = @"volumetric flow rate";
            public static string Temperature = @"temperature";
        }

        public abstract class QcTraceUnits
        {
            public const string Intensity = @"intensity";
            public const string Pascal = @"Pa";
            public const string PoundsPerSquareInch = @"psi";
            public const string MicrolitersPerMinute = @"uL/min";
            public static string DegreeC = @"°C";
            public static string DegreeF = @"°F";
            public static string Percent = @"%";
            public static string Unknown = @"unknown";
        }

        public class QcTrace
        {
            public QcTrace(Chromatogram c)
            {
                Name = c.Id;
                Index = c.Index;
                var param = c.CvParamChild(CVID.MS_chromatogram_type);
                var chromatogramType = param.Cvid;
                var intensityArray = c.GetIntensityArray();
                var unitsCVID = CVID.CVID_Unknown;
                string unitsString = null;
                if (intensityArray != null)
                {
                    var typeParam = intensityArray.CvParamChild(CVID.MS_intensity_array);
                    unitsCVID = typeParam.Units;
                    unitsString = typeParam.UnitsName; // Default to the raw units name from MS OBO
                }

                switch (unitsCVID)
                {
                    case CVID.MS_number_of_detector_counts:
                    case CVID.CVID_Unknown:
                    {
                        var userParam = c.UserParam(@"units");
                        if (!userParam.IsEmpty)
                        {
                            unitsString = userParam; // Show custom units if provided
                        }
                        if (string.IsNullOrEmpty(unitsString) ||
                            unitsCVID == CVID.MS_number_of_detector_counts)
                        {
                            unitsString = QcTraceUnits.Intensity; // Show "intensity" instead of "" or "number of detector counts"
                        }
                        break;
                    }
                    case CVID.UO_percent:
                        unitsString = QcTraceUnits.Percent; // Show "%" instead of "percent"
                        break;
                    case CVID.UO_pounds_per_square_inch:
                        unitsString = QcTraceUnits.PoundsPerSquareInch; // Show "psi" instead of "pounds per square inch"
                        break;
                    case CVID.UO_pascal:
                        unitsString = QcTraceUnits.Pascal; // Show "Pa" instead of "pascal"
                        break;
                    case CVID.UO_microliters_per_minute:
                        unitsString = QcTraceUnits.MicrolitersPerMinute; // Show "uL/min" instead of "microliters per minute"
                        break;
                    case CVID.UO_degree_Celsius:
                        unitsString = QcTraceUnits.DegreeC; // Show "°C" instead of "degree Celsius"
                        break;
                    case CVID.UO_degree_Fahrenheit:
                        unitsString = QcTraceUnits.DegreeF; // Show "°F" instead of "degree Fahrenheit"
                        break;
                }

                if (chromatogramType == CVID.MS_pressure_chromatogram)
                {
                    MeasuredQuality = QcTraceQuality.Pressure;
                }
                else if (chromatogramType == CVID.MS_flow_rate_chromatogram)
                {
                    MeasuredQuality = QcTraceQuality.FlowRate;
                }
                else if (chromatogramType == CVID.MS_temperature_chromatogram)
                {
                    MeasuredQuality = QcTraceQuality.Temperature;
                }
                else // Generalized chromatogram, or absorption chromatogram, or emission chromatogram, etc - probably best to use the name directly
                {
                    MeasuredQuality = Name;
                }

                IntensityUnits = string.IsNullOrEmpty(unitsString) ? QcTraceUnits.Unknown : unitsString;
                Times = c.GetTimeArray().Data.ToArray();
                Intensities = c.BinaryDataArrays[1].Data.ToArray();
            }

            public string Name { get; private set; }
            public int Index { get; private set; }
            public double[] Times { get; private set; }
            public double[] Intensities { get; private set; }
            public string MeasuredQuality { get; private set; }
            public string IntensityUnits { get; private set; }

            public string TypeWithUnits()
            {
                string CapitalizeFirst(string str)
                {
                    if (string.IsNullOrEmpty(str))
                        return str;
                    if (str.Length == 1)
                        return str.ToUpper();
                    return char.ToUpper(str[0]) + str.Substring(1);
                }

                var type = MeasuredQuality;
                var units = IntensityUnits;

                // if units are not unknown, prefer those to any potentially buried in a custom MeasuredQuality
                if (!string.IsNullOrEmpty(units) && !units.Equals(@"unknown", StringComparison.OrdinalIgnoreCase))
                {
                    // Strip any existing units from MeasuredQuality
                    // e.g. "Pressure (psi)" or "Pressure [bar]" becomes just "Pressure"
                    // Only strip if the parentheses/brackets are at the end
                    if (type.EndsWith(")"))
                    {
                        int openParen = type.LastIndexOf('(');
                        if (openParen > 0)
                        {
                            type = type.Substring(0, openParen).TrimEnd();
                        }
                    }
                    else if (type.EndsWith("]"))
                    {
                        int openBracket = type.LastIndexOf('[');
                        if (openBracket > 0)
                        {
                            type = type.Substring(0, openBracket).TrimEnd();
                        }
                    }

                    // Now format with proper units
                    if (units == QcTraceUnits.Intensity)
                    {
                        return CapitalizeFirst(units);
                    }
                    else
                    {
                        return $"{CapitalizeFirst(type)} ({units})";
                    }
                }

                // If units are empty, unknown, or null, return just the type
                return CapitalizeFirst(type);
            }
        }

        public List<QcTrace> GetQcTraces()
        {
            if (ChromatogramList == null || ChromatogramList.Count == 0)
                return null;

            // some readers may return empty chromatograms at detail levels below FullMetadata
            DetailLevel minDetailLevel = DetailLevel.InstantMetadata;
            if (ChromatogramList.GetChromatogram(0, minDetailLevel).IsEmpty)
                minDetailLevel = DetailLevel.FullMetadata;

            var result = new List<QcTrace>();
            for (int i = 0; i < ChromatogramList.Count; ++i)
            {
                var chromMetaData = ChromatogramList.GetChromatogram(i, minDetailLevel);                {
                    // Skip over TIC, BPC, SIC, SIM, SRM, etc as they are not QC traces
                    var cvParamChild = chromMetaData.CvParamChild(CVID.MS_ion_current_chromatogram);
                    if (cvParamChild.Cvid != CVID.CVID_Unknown)
                        continue;
                }

                var chromatogram = ChromatogramList.GetChromatogram(i, true);                {
                    if (chromatogram == null)
                        return null;

                    result.Add(new QcTrace(chromatogram));
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
                var spectrum = SpectrumList.GetSpectrum(i);                {
                    var scanTime = spectrum.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
                    var msLevel = spectrum.CvParam(CVID.MS_ms_level);
                    times[i] = scanTime.TimeInSeconds();
                    msLevels[i] = (byte) (int) msLevel;
                }
            }
        }

        public int SpectrumCount
        {
            get { return SpectrumList != null ? SpectrumList.Count : 0; }
        }

        [Obsolete("Use the SpectrumCount property instead")]
        public int GetSpectrumCount()
        {
            return SpectrumCount;
        }

        public int GetSpectrumIndex(string id)
        {
            int index = SpectrumList.FindAbbreviated(id);
            if (0 > index || index >= SpectrumList.Count)
                return -1;
            return index;
        }
/* obsolete?
        public void GetSpectrum(int spectrumIndex, out double[] mzArray, out double[] intensityArray)
        {
            var spectrum = GetSpectrum(spectrumIndex);
            mzArray = spectrum.Mzs;
            intensityArray = spectrum.Intensities;
        }
*/
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
                        returnSpectrum = GetSpectrum(SpectrumList.GetSpectrum(spectrumIndex, true), spectrumIndex);
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
                    data = s.GetArrayByCvid(_cvidIonMobility.Value)?.Data?.ToArray();
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
                                if (data == null)
                                {
                                    data = TryGetIonMobilityData(s, CVID.MS_raw_ion_mobility_drift_time_array, ref _cvidIonMobility);
                                    if (data == null && HasCombinedIonMobilitySpectra && !s.Id.Contains(MERGED_TAG))
                                    {
                                        _cvidIonMobility = null; // We can't learn anything from a lockmass spectrum that has no IMS
                                        return null;
                                    }
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
            var data = s.GetArrayByCvid(cvid)?.Data;
            if (data != null)
                cvidIonMobility = cvid;

            return data?.ToArray();
        }

        private MsDataSpectrum GetSpectrum(Spectrum spectrum, int spectrumIndex)
        {
            if (spectrum == null)
            {
                return new MsDataSpectrum();
            }
            string idText = spectrum.Id;
            if (idText.Trim().Length == 0)
            {
                throw new ArgumentException(string.Format(@"Empty spectrum ID (and index = {0}) for scan {1}",
                    spectrum.Index, spectrumIndex)); 
            }
            // Start building properties object here.
            bool expectIonMobilityValue = IonMobilityUnits != eIonMobilityUnits.none;
            var msDataSpectrum = new MsDataSpectrum
            {
                Id = _trimNativeID ? Id.Abbreviate(idText) : idText,
                Level = GetMsLevel(spectrum) ?? 0,
                Index = spectrum.Index,
                RetentionTime = GetStartTime(spectrum),
                PrecursorsByMsLevel = GetPrecursorsByMsLevel(spectrum),
                Centroided = IsCentroided(spectrum),
                NegativeCharge = NegativePolarity(spectrum),
                ScanDescription = GetScanDescription(spectrum),
                Metadata = GetSpectrumMetadata(spectrum)
            };
            var spectrumScanList = spectrum.ScanList;
            var scans = spectrumScanList.Scans;
            if (IonMobilityUnits == eIonMobilityUnits.inverse_K0_Vsec_per_cm2)
            {
                msDataSpectrum.WindowGroup = GetWindowGroup(spectrum) ?? 0; // For Bruker diaPASEF
                msDataSpectrum.IsFullFrameDiaPasef = _config.PassEntireDiaPasefFrame && msDataSpectrum.WindowGroup > 0;
            }

            if (expectIonMobilityValue)
            {
                // Note the range actually measured (for zero vs missing value determination)
                var param = spectrum.UserParam(@"ion mobility lower limit");
                if (!param.IsEmpty)
                {
                    msDataSpectrum.IonMobilityMeasurementRangeLow = param;
                    param = spectrum.UserParam(@"ion mobility upper limit");
                    msDataSpectrum.IonMobilityMeasurementRangeHigh = param;
                }
            }

            if (spectrum.BinaryDataArrays.Count <= 1)
            {
                msDataSpectrum.SetArrays(Array.Empty<double>(), Array.Empty<double>());
                if (expectIonMobilityValue)
                {
                    msDataSpectrum.IonMobility = GetIonMobility(spectrum);
                }
            }
            else
            {
                try
                {
                    msDataSpectrum.SetArrays(ToArray(spectrum.GetMZArray()),
                        ToArray(spectrum.GetIntensityArray()),
                        GetIonMobilityArray(spectrum));

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

                    if (msDataSpectrum.Level == 1 && _config.SimAsSpectra &&
                            scans[0].ScanWindows.Count > 0)
                    {
                        msDataSpectrum.Precursors = ImmutableList.ValueOf(GetMs1Precursors(spectrum));
                    }

                    msDataSpectrum.SourceFilePath = FilePath;
                    if(spectrum.ScanList.Scans.Count > 0)
                        msDataSpectrum.InstrumentInfo = CreateMsInstrumentConfigInfo(spectrum.ScanList.Scans[0].InstrumentConfiguration); 
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
            var metadata = new SpectrumMetadata(Id.Abbreviate(spectrum.Id), retentionTime.Value);
            var precursorsByMsLevel = new List<IEnumerable<SpectrumPrecursor>>();
            foreach (var level in GetPrecursorsByMsLevel(spectrum))
            {
                List<SpectrumPrecursor> spectrumPrecursors = new List<SpectrumPrecursor>();
                foreach (var msPrecursor in level)
                {
                    if (msPrecursor.IsolationMz.HasValue)
                    {
                        var spectrumPrecursor = new SpectrumPrecursor(msPrecursor.IsolationMz.Value)
                            .ChangeCollisionEnergy(msPrecursor.PrecursorCollisionEnergy)
                            .ChangeDissociationMethod(msPrecursor.DissociationMethod);
                        
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
            var instrumentConfig = spectrum.ScanList.Scans.FirstOrDefault()?.InstrumentConfiguration;
            if (instrumentConfig != null)
            {
                GetInstrumentConfig(instrumentConfig, out _, out string analyzer, out _, out _);
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
            foreach (var scan in spectrum.ScanList.Scans)
            {
                foreach (var window in scan.ScanWindows)
                {
                    if (!window.UserParam(CENTROIDED_MIN_MAX).IsEmpty)
                    {
                        // min/max values obtained from centroided data are unreliable
                        continue;
                    }
                    var cvParamLowerLimit = window.CvParam(CVID.MS_scan_window_lower_limit);
                    if (cvParamLowerLimit != null)
                    {
                        double windowStart = cvParamLowerLimit;
                        if (scanWindowLowerLimit == null || windowStart < scanWindowLowerLimit)
                        {
                            scanWindowLowerLimit = windowStart;
                        }
                    }

                    var cvParamUpperLimit = window.CvParam(CVID.MS_scan_window_upper_limit);
                    if (cvParamUpperLimit != null)
                    {
                        double windowEnd = cvParamUpperLimit;
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
            metadata = metadata.ChangeSourceOffsetVoltage(GetSourceOffsetVoltage(spectrum));
            metadata = metadata.ChangeConstantNeutralLoss(GetConstantNeutralLoss(spectrum));
            return metadata;
        }

        public bool HasSrmSpectra
        {
            get { return HasSrmSpectraInList(); }
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

        private bool HasSrmSpectraInList()
        {
            if (_spectrumList == null || _spectrumList.Count == 0)
                return false;

            if (_msDataFile.FileDescription.FileContent.HasCVParam(CVID.MS_SRM_spectrum))
                return true;

            // If the first spectrum is not SRM, the others will not be either
            var spectrum = _spectrumList.GetSpectrum(0, false);            {
                return IsSrmSpectrum(spectrum);
            }
        }

        private bool HasIonMobilitySpectraInList()
        {
            if (IonMobilitySpectrumList == null || IonMobilitySpectrumList.Count == 0)
                return false;

            // Assume that if any spectra have ion mobility info, all do
            var spectrum = IonMobilitySpectrumList.GetSpectrum(0, false);            {
                return GetIonMobility(spectrum).HasValue;
            }
        }

        public bool IsWatersSonarData()
        {
            if (IonMobilitySpectrumList == null || IonMobilitySpectrumList.Count == 0)
                return false;
            return IonMobilitySpectrumList.IsWatersSonarData;
        }

        // Waters SONAR mode uses ion mobility hardware to filter on m/z and reports the results as bins
        public Tuple<int, int> SonarMzToBinRange(double mz, double tolerance)
        {
            int low = -1, high = -1;
            if (IonMobilitySpectrumList != null)
            {
                IonMobilitySpectrumList.SonarMzToBinRange(mz, tolerance, out low, out high);
            }
            return new Tuple<int, int>(low, high);
        }

        public double SonarBinToPrecursorMz(int bin)
        {
            double result = 0;
            IonMobilitySpectrumList?.SonarBinToPrecursorMz(bin, out result); // Returns average of m/z range associated with bin, really only useful for display
            return result;
        }

        private double? GetMaxIonMobilityInList()
        {
            if (IonMobilitySpectrumList == null || IonMobilitySpectrumList.Count == 0)
                return null;

            // Assume that if any spectra have ion mobility values, all do, and all are same range
            double? maxIonMobility = null;
            for (var i = 0; i < IonMobilitySpectrumList.Count; i++)
            {
                var spectrum = IonMobilitySpectrumList.GetSpectrum(i, true);                {
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
                _lastSpectrum = SpectrumList.GetSpectrum(_lastScanIndex, _lastDetailLevel);
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
            return SpectrumList.SpectrumIdentity(scanIndex).Id;
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
            return spectrum.HasCVParam(CVID.MS_centroid_spectrum);
        }

        private static bool NegativePolarity(Spectrum spectrum)
        {
            var param = spectrum.CvParamChild(CVID.MS_scan_polarity);
            if (param.IsEmpty)
                return false;  // Assume positive if undeclared
            return (param.Cvid == CVID.MS_negative_scan);
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
            return spectrum.HasCVParam(CVID.MS_SRM_spectrum);
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
            CVParam param = spectrum.CvParam(CVID.MS_ms_level);
            if (param.IsEmpty)
                return null;
            return (int) param;
        }

        public string GetScanDescription(int scanIndex)
        {
            return GetMetaDataValue(scanIndex, GetScanDescription, v => v.IsNullOrEmpty(), v => v, ref _detailScanDescription, DetailLevel.FastMetadata);
        }

        private static string GetScanDescription(Spectrum spectrum)
        {
            const string USERPARAM_SCAN_DESCRIPTION = "scan description";
            UserParam param = spectrum.UserParam(USERPARAM_SCAN_DESCRIPTION);
            if (param.IsEmpty)
                return null;
            return param.Value.ToString().Trim();
        }

        private double? GetTotalIonCurrent(Spectrum spectrum)
        {
            var param = spectrum.CvParam(CVID.MS_total_ion_current);
            if (param.IsEmpty)
            {
                return null;
            }
            return param;
        }

        private double? GetInjectionTime(Spectrum spectrum)
        {
            int count = 0;
            double total = 0;
            foreach (var scan in spectrum.ScanList.Scans)
            {
                var param = scan.CvParam(CVID.MS_ion_injection_time);
                if (!param.IsEmpty)
                {
                    count++;
                    total += param;
                }
            }
            return count == 0 ? (double?) null : total;
        }

        private double GetSourceOffsetVoltage(Spectrum spectrum)
        {
            foreach (var scan in spectrum.ScanList.Scans)
            {
                var param = scan.CvParam(CVID.MS_offset_voltage);
                if (!param.IsEmpty)
                {
                    return param;
                }
            }

            return 0;
        }

        private double? GetConstantNeutralLoss(Spectrum spectrum) // If return value < 0, it's actually a neutral gain
        {
            try
            {
                if (spectrum.ScanList.IsEmpty)
                {
                    return null;
                }

                CVParam paramOffset = spectrum.ScanList.Scans[0].CvParam(CVID.MS_analyzer_scan_offset);
                if (paramOffset.IsEmpty)
                {
                    return null;
                }
                
                CVParam paramScanType = spectrum.ScanList.Scans[0].CvParam(CVID.MS_constant_neutral_gain_spectrum);
                if (paramScanType.IsEmpty)
                {
                    return (double)paramOffset; // ConstantNeutralLoss is positive for loss, negative for gain;
                }

                return  -1.0 * (double)paramOffset; // ConstantNeutralLoss is positive for loss, negative for gain
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }

        private static int GetPresetScanConfiguration(Spectrum spectrum)
        {
            try
            {
                if (spectrum.ScanList.IsEmpty)
                {
                    return 0;
                }

                CVParam param = spectrum.ScanList.Scans[0].CvParam(CVID.MS_preset_scan_configuration);
                if (param.IsEmpty)
                {
                    return 0;
                }

                return (int) param;
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
            var spectrumScanList = spectrum.ScanList;
            if (IonMobilityUnits == eIonMobilityUnits.none || spectrumScanList.Scans.Count == 0)
                return IonMobilityValue.EMPTY;
            var scan = spectrumScanList.Scans[0];
            double value;
            var expectedUnits = IonMobilityUnits;
            switch (expectedUnits)
            {
                case eIonMobilityUnits.drift_time_msec:
                {
                    CVParam driftTime = scan.CvParam(CVID.MS_ion_mobility_drift_time);
                    if (driftTime.IsEmpty)
                    {
                        const string USERPARAM_DRIFT_TIME = "drift time";
                        UserParam param = scan.UserParam(USERPARAM_DRIFT_TIME); // support files with the original drift time UserParam
                        if (param.IsEmpty)
                            return IonMobilityValue.EMPTY;
                        value =  param.TimeInSeconds() * 1000.0;
                    }
                    else
                        value = driftTime.TimeInSeconds() * 1000.0;
                    return IonMobilityValue.GetIonMobilityValue(value, expectedUnits);
                }

                case eIonMobilityUnits.inverse_K0_Vsec_per_cm2:
                {
                    var irim = scan.CvParam(CVID.MS_inverse_reduced_ion_mobility);
                    if (irim.IsEmpty)
                    {
                        return IonMobilityValue.EMPTY;
                    }
                    value = irim;
                    return IonMobilityValue.GetIonMobilityValue(value, expectedUnits);
                }

                case eIonMobilityUnits.compensation_V:
                {
                    var faims = spectrum.CvParam(CVID.MS_FAIMS_compensation_voltage);
                    if (faims.IsEmpty)
                    {
                        return IonMobilityValue.EMPTY;
                    }
                    value = faims;
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
            var scans = spectrum.ScanList.Scans;
            if (scans.Count == 0)
                return null;
            CVParam param = scans[0].CvParam(CVID.MS_scan_start_time);
            if (param.IsEmpty)
                return null;
            return param.TimeInSeconds() / 60;
        }

        public int? GetWindowGroup(int scanIndex)
        {
            if (IonMobilityUnits != eIonMobilityUnits.inverse_K0_Vsec_per_cm2)
            {
                return null;
            }
            return GetMetaDataValue(scanIndex, GetWindowGroup, v => v.HasValue, v => v ?? 0, ref _detailWindowGroup);
        }

        private static int? GetWindowGroup(Spectrum spectrum)
        {
            var scans = spectrum.ScanList.Scans;
            if (scans.Count == 0)
                return null;
            var param = scans[0].UserParam(@"windowGroup"); // For Bruker diaPASEF
            if (param.IsEmpty)
                return null;
            return int.Parse(param);
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
            var spectrumPrecursors = spectrum.Precursors;
            int count = spectrumPrecursors.Count;
            if (count == 0)
                return ImmutableList<ImmutableList<MsPrecursor>>.EMPTY;
            // Most MS/MS spectra will have a single MS1 precursor
            else if (spectrumPrecursors.Count == 1)
            {
                var precursor = spectrumPrecursors[0];
                if (GetMsLevel(precursor) == 1)
                {
                    var msPrecursor = CreatePrecursor(precursor, negativePolarity);
                    return ImmutableList.Singleton(ImmutableList.Singleton(msPrecursor));
                }
            }
            return ImmutableList.ValueOf(GetPrecursorsByMsLevel(spectrumPrecursors, negativePolarity));
        }

        private static IEnumerable<ImmutableList<MsPrecursor>> GetPrecursorsByMsLevel(List<Precursor> precursors, bool negativePolarity)
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
            var msPrecursor = new MsPrecursor
            {
                PrecursorMz = GetPrecursorMz(p, negativePolarity),
                PrecursorCollisionEnergy = GetPrecursorCollisionEnergy(p),
                IsolationWindowTargetMz =
                    GetSignedMz(GetIsolationWindowValue(p, CVID.MS_isolation_window_target_m_z),
                        negativePolarity),
                IsolationWindowLower = GetIsolationWindowValue(p, CVID.MS_isolation_window_lower_offset),
                IsolationWindowUpper = GetIsolationWindowValue(p, CVID.MS_isolation_window_upper_offset),
            };
            var cvidDissociationMethod = GetPrecursorDissociationMethods(p);
            if (cvidDissociationMethod.Count > 0)
            {
                msPrecursor.DissociationMethod = string.Join(" ", cvidDissociationMethod.Select(cvid => CvLookup.CvTermInfo(cvid).ShortName));
            }
            return msPrecursor;
        }

        private static int GetMsLevel(Precursor precursor)
        {
            UserParam msLevelParam = null;
            try
            {
                msLevelParam = precursor.IsolationWindow.UserParam("ms level");
                if (msLevelParam.IsEmpty)
                    msLevelParam = precursor.UserParam("ms level");
                return msLevelParam.IsEmpty ? 1 : (int)msLevelParam;
            }
            finally
            {
            }

        }

        private static int? GetChargeStateValue(Precursor precursor)
        {
            if (precursor.SelectedIons == null || precursor.SelectedIons.Count == 0)
                return null;
            var param = precursor.SelectedIons[0].CvParam(CVID.MS_charge_state);
            if (param.IsEmpty)
                return null;
            return (int)param;
        }

        private static IEnumerable<MsPrecursor> GetMs1Precursors(Spectrum spectrum)
        {
            bool negativePolarity = NegativePolarity(spectrum);
            foreach (var scanWindow in spectrum.ScanList.Scans[0].ScanWindows)
            {
                if (!scanWindow.UserParam(CENTROIDED_MIN_MAX).IsEmpty)
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
                    double windowStart = scanWindow.CvParam(CVID.MS_scan_window_lower_limit);
                    double windowEnd = scanWindow.CvParam(CVID.MS_scan_window_upper_limit);
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
            var selectedIon = precursor.SelectedIons.FirstOrDefault();
            if (selectedIon == null)
                return null;
            return GetSignedMz(selectedIon.CvParam(CVID.MS_selected_ion_m_z), negativePolarity);
        }

        private static SignedMz? GetSignedMz(double? mz, bool negativePolarity)
        {
            if (mz.HasValue)
                return new SignedMz(mz.Value, negativePolarity);
            return null;
        }

        private static double? GetPrecursorCollisionEnergy(Precursor precursor)
        {
            var param = precursor.Activation.CvParam(CVID.MS_collision_energy);
            if (param.IsEmpty)
                return null;
            return (double)param;
        }

        private static double? GetIsolationWindowValue(Precursor precursor, CVID cvid)
        {
            var term = precursor.IsolationWindow.CvParam(cvid);
            if (!term.IsEmpty)
                return term;
            return null;
        }

        private static IList<CVID> GetPrecursorDissociationMethods(Precursor precursor)
        {
            var list = new List<CVID>();
            foreach (var cvParam in precursor.Activation.CvParamChildren(CVID.MS_dissociation_method))
            {
                
                {
                    list.Add(cvParam.Cvid);
                }
            }
            return list;
        }

        public void Write(string path)
        {
            MSDataFile.Write(_msDataFile, path);
        }

        public virtual void Dispose()
        {
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
                var msd = new MSData();
                FULL_READER_LIST.Read(filepath, msd);
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
        public string Id {get; set; }
        public int Spectra { get; set; }
        public string ContentType { get; set; }
        public string IonSource { get; set; }
        public string Analyzer { get; set; }
        public string Detector { get; set; }
        public Dictionary<int, List<MsDataFileImpl.DiaFrameMsMsWindowItem>> DiaFrameMsMsWindowsTable { get; set; } // For Bruker DiaPasef

        public bool IsValidDiaPasefPoint(int windowGroup, double im, double isoMzLow, double isoMzHigh)
        {
            return DiaFrameMsMsWindowsTable==null || DiaFrameMsMsWindowsTable[windowGroup].Any(item => item.IsValidPoint(windowGroup, im, isoMzLow, isoMzHigh));
        }
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
        public string DissociationMethod { get; set; }
    }

    public sealed class MsDataSpectrum
    {
        public MsDataSpectrum()
        {
            Centroided = true;
            SetArrays(Array.Empty<double>(), Array.Empty<double>());
        }

        public MsDataSpectrum(double[] mzs, double[] intensities)
        {
            SetArrays(mzs, intensities);
        }

        public void SetArrays(double[] mzs, double[] intensities, double[] ionMobilities = null,
            double[] scanningQuadMzLows = null, double[] scanningQuadMzHighs = null)
        {
            Mzs = mzs;
            Intensities = intensities;
            IonMobilities = ionMobilities;
        }

        public void SetEmptyArrays()
        {
            SetArrays(Array.Empty<double>(), Array.Empty<double>());
        }

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
        public bool IsFullFrameDiaPasef { get; set; } // For Bruker diaPASEF -  when true the entire frame is sent instead of individdal isolation ranges (e.g. Parallel diaPAEF)
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
        public Dictionary<int, List<MsDataFileImpl.DiaFrameMsMsWindowItem>> DiaFrameMsMsWindows { get; private set; } // For Bruker diaPASEF

        public static readonly MsInstrumentConfigInfo EMPTY = new MsInstrumentConfigInfo(null, null, null, null);

        public MsInstrumentConfigInfo(string model, string ionization,
                                      string analyzer, string detector,
                                      Dictionary<int, List<MsDataFileImpl.DiaFrameMsMsWindowItem>> diaFrameMsMsWindows = null)
        {
            Model = model != null ? model.Trim() : string.Empty;
            Ionization = ionization != null ? ionization.Replace('\n', ' ').Trim() : string.Empty;
            Analyzer = analyzer != null ? analyzer.Replace('\n', ' ').Trim() : string.Empty;
            Detector = detector != null ? detector.Replace('\n', ' ').Trim() : string.Empty;
            DiaFrameMsMsWindows = diaFrameMsMsWindows;
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
