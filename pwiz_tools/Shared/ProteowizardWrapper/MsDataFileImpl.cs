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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;

namespace pwiz.ProteowizardWrapper
{
    public class MsDataFileImpl : IDisposable
    {
        private static readonly ReaderList FULL_READER_LIST = ReaderList.FullReaderList;

        // Cached disposable objects
        private MSData _msDataFile;
        private ReaderConfig _config;
        private SpectrumList _spectrumList;
        private SpectrumList _spectrumListCentroided;
        private ChromatogramList _chromatogramList;
        private MsDataScanCache _scanCache;

        /// <summary>
        /// is the file multiplexed DIA?
        /// </summary>
        private readonly bool _isMsx;

        private DetailLevel _detailMsLevel = DetailLevel.InstantMetadata;

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

        public static MsDataFileImpl[] ReadAll(string path)
        {
            var listAll = new MSDataList();
            FULL_READER_LIST.read(path, listAll);
            var listAllImpl = new List<MsDataFileImpl>();
            foreach (var msData in listAll)
                listAllImpl.Add(new MsDataFileImpl(msData));
            return listAllImpl.ToArray();
        }

        private MsDataFileImpl(MSData msDataFile)
        {
            _msDataFile = msDataFile;
            _config = new ReaderConfig();
            _isMsx = CheckMsx();
        }

        public MsDataFileImpl(string path)
        {
            FilePath = path;
            _msDataFile = new MSDataFile(path);
            _config = new ReaderConfig();
            _isMsx = CheckMsx();
        }

        public MsDataFileImpl(string path, int sampleIndex, bool simAsSpectra = false, bool srmAsSpectra = false)
        {
            FilePath = path;
            _msDataFile = new MSData();
            _config = new ReaderConfig {simAsSpectra = simAsSpectra, srmAsSpectra = srmAsSpectra};
            FULL_READER_LIST.read(path, _msDataFile, sampleIndex, _config);
            _isMsx = CheckMsx();
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
                string ionSource = "";
                string analyzer = "";
                string detector = "";
                foreach (InstrumentConfiguration ic in _msDataFile.instrumentConfigurationList)
                {
                    string instrumentIonSource;
                    string instrumentAnalyzer;
                    string instrumentDetector;
                    GetInstrumentConfig(ic, out instrumentIonSource, out instrumentAnalyzer, out instrumentDetector);

                    if (ionSource.Length > 0)
                        ionSource += ", ";
                    ionSource += instrumentIonSource;

                    if (analyzer.Length > 0)
                        analyzer += ", ";
                    analyzer += instrumentAnalyzer;

                    if (detector.Length > 0)
                        detector += ", ";
                    detector += instrumentDetector;
                }

                HashSet<string> contentTypeSet = new HashSet<string>();
                foreach (CVParam term in _msDataFile.fileDescription.fileContent.cvParams)
                    contentTypeSet.Add(term.name);
                var contentTypes = contentTypeSet.ToArray();
                Array.Sort(contentTypes);
                string contentType = String.Join(", ", contentTypes);

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
            SortedDictionary<int, string> ionSources = new SortedDictionary<int, string>();
            SortedDictionary<int, string> analyzers = new SortedDictionary<int, string>();
            SortedDictionary<int, string> detectors = new SortedDictionary<int, string>();

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
                            UserParam uParam = c.userParam("msIonisation");
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
                            UserParam uParam = c.userParam("msMassAnalyzer");
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
                            UserParam uParam = c.userParam("msDetector");
                            if (HasInfo(uParam))
                            {
                                detectors.Add(c.order, uParam.value);
                            }
                        }
                        break;
                }
            }

            ionSource = String.Join("/", new List<string>(ionSources.Values).ToArray());

            analyzer = String.Join("/", new List<string>(analyzers.Values).ToArray());

            detector = String.Join("/", new List<string>(detectors.Values).ToArray());
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

        public IEnumerable<MsInstrumentConfigInfo> GetInstrumentConfigInfoList()
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
                    UserParam uParam = ic.userParam("msModel");
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

        private static bool HasInfo(UserParam uParam)
        {
            return !uParam.empty() && !String.IsNullOrEmpty(uParam.value) &&
                   !String.Equals("unknown", uParam.value.ToString().ToLowerInvariant());
        }

        public bool IsABFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_ABI_WIFF_file)); }
        }

        public bool IsMzWiffXml
        {
            get { return IsProcessedBy("mzWiff"); }
        }

        public bool IsAgilentFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Agilent_MassHunter_file)); }
        }

        public bool IsThermoFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Thermo_RAW_file)); }
        }

        public bool IsWatersFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Waters_raw_file)); }
        }

        public bool IsMsx
        {
            get { return _isMsx; }
        }

        /// <summary>
        /// Checks the file to determine if it is MSX by analyzing the
        /// placement of DIA windows for now the criteria is fairly loose:
        /// MSX has: MS/MS scans w/ > 1 precursor isolation window in
        /// one of the first 500 scans MS/MS scans after this one all have
        /// the same number of precursors
        /// </summary>
        private bool CheckMsx()
        {
            if (!IsThermoFile)
                return false;

            // is there MS/MS w/ > 1 precursor in the first 500 scans?
            int i;
            int maxIndex = Math.Min(500, SpectrumCount);
            int precursorsPerScan = 0;
            for (i = 1; i < maxIndex; ++i )
            {
                if (GetMsLevel(i) != 2)
                    continue;
                var precursors = GetPrecursors(i);
                if (precursors.Length < 1 || !precursors[0].PrecursorMz.HasValue)
                    continue;
                // if MS/MS spectrum only has a single precursor, this data
                // is assumed to not be multiplexed
                if (precursors.Length < 2)
                    return false;
                precursorsPerScan = precursors.Length;
                break;
            }

            // there was an MS/MS in the first 500 scans w/ multiple precursors
            // check that the next 20 MS/MS scans have the same number of precursors
            
            int msMsChecked = 1;
            for (; i < SpectrumCount && msMsChecked <=20; ++i )
            {
                if (GetMsLevel(i) != 2)
                    continue;
                var precursors = GetPrecursors(i);
                if (precursors.Length < 1 || !precursors[0].PrecursorMz.HasValue)
                    continue;
                int numPrecursors = precursors.Length;
                if (numPrecursors!=precursorsPerScan)
                    return false;
                ++msMsChecked;
            }

            // return false; // for testing
            return true;
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
                return _spectrumList = _spectrumList ??
                    _msDataFile.run.spectrumList;
            }
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
                double[] times = new double[timeIntensityPairList.Count];
                for (int i = 0; i < times.Length; i++)
                {
                    times[i] = timeIntensityPairList[i].time;
                }
                return times;
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
        public void GetScanTimesAndMsLevels(out double[] times, out byte[] msLevels)
        {
            times = new double[SpectrumCount];
            msLevels = new byte[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
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

        public void GetSpectrum(int scanIndex, out double[] mzArray, out double[] intensityArray)
        {
            var spectrum = GetSpectrum(scanIndex);
            mzArray = spectrum.Mzs;
            intensityArray = spectrum.Intensities;
        }

        public MsDataSpectrum GetSpectrum(int scanIndex)
        {
            if (_scanCache != null)
            {
                MsDataSpectrum returnSpectrum;
                // check the scan for this cache
                if (!_scanCache.TryGetSpectrum(scanIndex, out returnSpectrum))
                {
                    // spectrum not in the cache, pull it from the file
                    returnSpectrum = GetSpectrum(SpectrumList.spectrum(scanIndex, true));
                    // add it to the cache
                    _scanCache.Add(scanIndex, returnSpectrum);
                }
                return returnSpectrum;
            }
            using (var spectrum = SpectrumList.spectrum(scanIndex, true))
            {
                return GetSpectrum(spectrum);
            }
        }

        private MsDataSpectrum GetSpectrum(Spectrum spectrum)
        {
            if (spectrum != null && spectrum.binaryDataArrays.Count > 1)
            {
                try
                {
                    var msDataSpectrum = new MsDataSpectrum
                               {
                                   Level = GetMsLevel(spectrum) ?? 0,
                                   RetentionTime = GetStartTime(spectrum),
                                   Precursors = GetPrecursors(spectrum),
                                   Centroided = IsCentroided(spectrum),
                                   Mzs = ToArray(spectrum.getMZArray()),
                                   Intensities = ToArray(spectrum.getIntensityArray())
                               };

                    if (msDataSpectrum.Level == 1 && _config.simAsSpectra)
                    {
                        var scanWindow = spectrum.scanList.scans[0].scanWindows.FirstOrDefault();
                        if (scanWindow != null)
                        {
                            double windowStart = scanWindow.cvParam(CVID.MS_scan_window_lower_limit).value;
                            double windowEnd = scanWindow.cvParam(CVID.MS_scan_window_upper_limit).value;
                            double isolationWidth = (windowEnd - windowStart) / 2;
                            msDataSpectrum.Precursors = new[]
                                                        {
                                                            new MsPrecursor
                                                                {
                                                                    IsolationWindowTargetMz = windowStart + isolationWidth,
                                                                    IsolationWindowLower = isolationWidth,
                                                                    IsolationWindowUpper = isolationWidth
                                                                }
                                                        };
                        }
                    }

                    return msDataSpectrum;
                }
                catch (NullReferenceException)
                {
                }
            }

            return new MsDataSpectrum
            {
                Centroided = true,
                Mzs = new double[0],
                Intensities = new double[0]
            };
        }

        public MsDataSpectrum GetCentroidedSpectrum(int scanIndex)
        {
            var msDataSpectrum = GetSpectrum(scanIndex);
            if (!msDataSpectrum.Centroided && msDataSpectrum.Mzs.Length > 0)
            {
                // Spectra from mzWiff files lack zero intensity m/z values necessary for
                // correct centroiding.
                if (IsMzWiffXml)
                    InsertZeros(msDataSpectrum);

                var centroider = new Centroider(msDataSpectrum.Mzs, msDataSpectrum.Intensities);
                double[] mzArray, intensityArray;
                centroider.GetCentroidedData(out mzArray, out intensityArray);
                msDataSpectrum.Mzs = mzArray;
                msDataSpectrum.Intensities = intensityArray;
            }
            return msDataSpectrum;
        }

        private static void InsertZeros(MsDataSpectrum msDataSpectrum)
        {
            double[] mzs = msDataSpectrum.Mzs;
            double[] intensities = msDataSpectrum.Intensities;
            int len = mzs.Length;
            double minDelta = double.MaxValue;
            for (int i = 0; i < len - 1; i++)
            {
                minDelta = Math.Min(minDelta, mzs[i + 1] - mzs[i]);
            }
            double maxGap = minDelta*2;
            var newMzs = new List<double>(len);
            var newIntensities = new List<double>(len);
            for (int i = 0; i < len - 1; i++)
            {
                double mz = mzs[i];
                double mzNext = mzs[i + 1];
                if (i == 0)
                {
                    newMzs.Add(mz - minDelta);
                    newIntensities.Add(0);
                }
                newMzs.Add(mz);
                newIntensities.Add(intensities[i]);
                // If the distance to the next m/z value is greater than the
                // maximum gap allowed, insert a flanking zero after this peak.
                if (mzNext - mz > maxGap)
                {
                    mz += minDelta;
                    newMzs.Add(mz);
                    newIntensities.Add(0);

                    // If the distance is still greater than the maximum gap,
                    // insert a flanking zero before the next peak.
                    if (mzNext - mz > maxGap)
                    {
                        mz = mzNext - minDelta;
                        newMzs.Add(mz);
                        newIntensities.Add(0);
                    }
                }
            }
            newMzs.Add(mzs[len - 1]);
            newIntensities.Add(intensities[len - 1]);
            newMzs.Add(mzs[len - 1] + minDelta);
            newIntensities.Add(0);
            msDataSpectrum.Mzs = newMzs.ToArray();
            msDataSpectrum.Intensities = newIntensities.ToArray();
        }

        public bool HasSrmSpectra
        {
            get
            {
                if (SpectrumList.size() == 0)
                    return false;

                // If the first spectrum is not SRM, the others will not be either
                using (var spectrum = SpectrumList.spectrum(0, false))
                {
                    return IsSrmSpectrum(spectrum);
                }
            }
        }

        public MsDataSpectrum GetSrmSpectrum(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, true))
            {
                return GetSpectrum(IsSrmSpectrum(spectrum) ? spectrum : null);
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

        public double? GetStartTime(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex))
            {
                return GetStartTime(spectrum);
            }
        }

        private static double? GetStartTime(Spectrum spectrum)
        {
            var scan = spectrum.scanList.scans[0];
            CVParam param = scan.cvParam(CVID.MS_scan_start_time);
            if (param.empty())
                return null;
            return param.timeInSeconds() / 60;
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
            return spectrum.precursors.Select(p =>
                new MsPrecursor
                    {
                        PrecursorMz = GetPrecursorMz(p),
                        IsolationWindowTargetMz = GetIsolationWindowValue(p, CVID.MS_isolation_window_target_m_z),
                        IsolationWindowLower = GetIsolationWindowValue(p, CVID.MS_isolation_window_lower_offset),
                        IsolationWindowUpper = GetIsolationWindowValue(p, CVID.MS_isolation_window_upper_offset),
                    }).ToArray();
        }

        private static double? GetPrecursorMz(Precursor precursor)
        {
            // CONSIDER: Only the first selected ion m/z is considered for the precursor m/z
            var selectedIon = precursor.selectedIons.FirstOrDefault();
            if (selectedIon == null)
                return null;
            return selectedIon.cvParam(CVID.MS_selected_ion_m_z).value;
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
            if (_spectrumListCentroided != null)
                _spectrumListCentroided.Dispose();
            _spectrumListCentroided = null;
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

    public struct MsPrecursor
    {
        public double? PrecursorMz { get; set; }
        public double? IsolationWindowTargetMz { get; set; }
        public double? IsolationWindowUpper { get; set; }
        public double? IsolationWindowLower { get; set; }
        public double? IsolationMz
        {
            get
            {
                double? targetMz = IsolationWindowTargetMz ?? PrecursorMz;
                // If the isolation window is not centered around the target m/z, then return a
                // m/z value that is centered in the isolation window.
                if (targetMz.HasValue && IsolationWindowUpper.HasValue && IsolationWindowLower.HasValue &&
                        IsolationWindowUpper.Value != IsolationWindowLower.Value)
                    return (targetMz.Value * 2 + IsolationWindowUpper.Value - IsolationWindowLower.Value) / 2.0;
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
        public int Level { get; set; }
        public double? RetentionTime { get; set; }
        public MsPrecursor[] Precursors { get; set; }
        public bool Centroided { get; set; }
        public double[] Mzs { get; set; }
        public double[] Intensities { get; set; }
    }

    public sealed class MsInstrumentConfigInfo
    {
        public string Model { get; private set; }
        public string Ionization { get; private set; }
        public string Analyzer { get; private set; }
        public string Detector { get; private set; }

        public MsInstrumentConfigInfo(string model, string ionization,
                                      string analyzer, string detector)
        {
            Model = model != null ? model.Trim() : null;
            Ionization = ionization != null ? ionization.Replace('\n',' ').Trim() : null;
            Analyzer = analyzer != null ? analyzer.Replace('\n', ' ').Trim() : null;
            Detector = detector != null ? detector.Replace('\n', ' ').Trim() : null;
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
