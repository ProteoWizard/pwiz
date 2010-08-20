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
using System.Linq;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.analysis;
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

        private const bool PREFER_VENDOR_PEAK_PICKING = true;
        private SpectrumList SpectrumListCentroided
        {
            get
            {
                return _spectrumListCentroided 
                    = _spectrumListCentroided 
                    ?? new SpectrumList_PeakPicker(
                        SpectrumList, new LocalMaximumPeakDetector(3), PREFER_VENDOR_PEAK_PICKING, new[] {1, 2});
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
            using (var spectrum = SpectrumList.spectrum(scanIndex, true))
            {
                mzArray = ToArray(spectrum.getMZArray().data);
                intensityArray = ToArray(spectrum.getIntensityArray().data);
            }
        }

        public bool IsCentroided(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, false))
            {
                return spectrum.hasCVParam(CVID.MS_centroid_spectrum);
            }
        }

        public void GetCentroidedSpectrum(int scanIndex, out double[] mzArray, out double[] intensityArray)
        {
            using (var spectrum = SpectrumListCentroided.spectrum(scanIndex, true))
            {
                var mzData = spectrum.getMZArray().data;
                var intensityData = spectrum.getIntensityArray().data;
                var mzs = new List<double>();
                var intensities = new List<double>();
                for (int i = 0; i < mzData.Count(); i++)
                {
                    if (intensityData[i] == 0)
                    {
                        continue;
                    }
                    mzs.Add(mzData[i]);
                    intensities.Add(intensityData[i]);
                }
                mzArray = mzs.ToArray();
                intensityArray = intensities.ToArray();
            }
        }

        public bool GetSrmSpectrum(int scanIndex, out double? time, out double? precursorMz,
            out double[] mzArray, out double[] intensityArray)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, true))
            {
                if (!spectrum.hasCVParam(CVID.MS_SRM_spectrum))
                {
                    time = null;
                    precursorMz = null;
                    mzArray = null;
                    intensityArray = null;
                    return false;
                }
                else
                {
                    time = GetStartTime(spectrum);
                    precursorMz = GetPrecursorMz(spectrum);
                    mzArray = ToArray(spectrum.getMZArray().data);
                    intensityArray = ToArray(spectrum.getIntensityArray().data);
                    return true;
                }
            }
        }

        public string GetSpectrumId(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex))
            {
                return spectrum.id;
            }            
        }

        public int GetMsLevel(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex))
            {
                return (int) spectrum.cvParam(CVID.MS_ms_level).value;
            }
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

        public double? GetPrecursorMz(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex))
            {
                return GetPrecursorMz(spectrum);
            }            
        }

        private static double? GetPrecursorMz(Spectrum spectrum)
        {
            foreach (Precursor p in spectrum.precursors)
            {
                foreach (SelectedIon si in p.selectedIons)
                    return si.cvParam(CVID.MS_selected_ion_m_z).value;
            }
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
}
