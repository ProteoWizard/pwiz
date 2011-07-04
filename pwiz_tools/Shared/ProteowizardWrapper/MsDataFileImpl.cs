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
        // Cached disposable objects
        private MSData _msDataFile;
        private SpectrumList _spectrumList;
        private SpectrumList _spectrumListCentroided;
        private ChromatogramList _chromatogramList;

        private DetailLevel _detailMsLevel = DetailLevel.InstantMetadata;

        private static double[] ToArray(IList<double> list)
        {
            double[] result = new double[list.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = list[i];
            return result;
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
            return ReaderList.FullReaderList.readIds(path);
        }

        public static MsDataFileImpl[] ReadAll(string path)
        {
            var listAll = new MSDataList();
            ReaderList.FullReaderList.read(path, listAll);
            var listAllImpl = new List<MsDataFileImpl>();
            foreach (var msData in listAll)
                listAllImpl.Add(new MsDataFileImpl(msData));
            return listAllImpl.ToArray();
        }

        private MsDataFileImpl(MSData msDataFile)
        {
            _msDataFile = msDataFile;
        }

        public MsDataFileImpl(string path)
        {
            _msDataFile = new MSDataFile(path);
        }

        public MsDataFileImpl(string path, int sampleIndex)
        {
            _msDataFile = new MSData();
            ReaderList.FullReaderList.read(path, _msDataFile, sampleIndex);            
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
                                break;
                            case ComponentType.ComponentType_Analyzer:
                                term = c.cvParamChild(CVID.MS_mass_analyzer_type);
                                if (!term.empty())
                                    analyzers.Add(c.order, term.name);
                                break;
                            case ComponentType.ComponentType_Detector:
                                term = c.cvParamChild(CVID.MS_detector_type);
                                if (!term.empty())
                                    detectors.Add(c.order, term.name);
                                break;
                        }
                    }

                    if (ionSource.Length > 0)
                        ionSource += ", ";
                    ionSource += String.Join("/", new List<string>(ionSources.Values).ToArray());

                    if (analyzer.Length > 0)
                        analyzer += ", ";
                    analyzer += String.Join("/", new List<string>(analyzers.Values).ToArray());

                    if (detector.Length > 0)
                        detector += ", ";
                    detector += String.Join("/", new List<string>(detectors.Values).ToArray());
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

        public bool IsProcessedBy(string softwareName)
        {
            foreach (var softwareApp in _msDataFile.softwareList)
            {
                if (softwareApp.id.Contains(softwareName))
                    return true;
            }
            return false;
        }

        public bool IsABFile
        {
            get { return IsProcessedBy("Analyst"); }
        }

        public bool IsMzWiffXml
        {
            get { return IsProcessedBy("mzWiff"); }
        }

        public bool IsAgilentFile
        {
            get { return IsProcessedBy("MassHunter"); }
        }

        public bool IsThermoFile
        {
            get { return IsProcessedBy("Xcalibur"); }
        }

        public bool IsWatersFile
        {
            get { return IsProcessedBy("MassLynx"); }
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
            using (var spectrum = SpectrumList.spectrum(scanIndex, true))
            {
                return GetSpectrum(spectrum);
            }
        }

        private static MsDataSpectrum GetSpectrum(Spectrum spectrum)
        {
            if (spectrum != null)
            {
                try
                {
                    return new MsDataSpectrum
                               {
                                   Level = GetMsLevel(spectrum) ?? 0,
                                   RetentionTime = GetStartTime(spectrum),
                                   Precursors = GetPrecursors(spectrum),
                                   Centroided = IsCentroided(spectrum),
                                   Mzs = ToArray(spectrum.getMZArray().data),
                                   Intensities = ToArray(spectrum.getIntensityArray().data)
                               };
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
            return (selectedIon != null ? selectedIon.cvParam(CVID.MS_selected_ion_m_z).value : null);
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
}
