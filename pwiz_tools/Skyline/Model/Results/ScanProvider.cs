/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Drawing;
using System.IO;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class TransitionFullScanInfo
    {
        public string Name;
        public ChromSource Source;
        public TimeIntensities TimeIntensities;
        public Color Color;
        public SignedMz PrecursorMz;
        public SignedMz ProductMz;
        public double? ExtractionWidth;
        public IonMobilityFilter _ionMobilityInfo;
        public Identity Id;  // ID of the associated TransitionDocNode
        public bool MatchMz(double mz)
        {
            return mz >= ProductMz.Value - ExtractionWidth / 2 &&
                   mz < ProductMz.Value + ExtractionWidth / 2;
        }

        public override string ToString() // Not user facing, for debug convenience only
        {
            return $@"name={Name} src={Source} Q1={PrecursorMz} Q2={ProductMz} w={ExtractionWidth} im={_ionMobilityInfo} Id={Id}";
        }
    }

    public interface IScanProvider : IDisposable
    {
        string DocFilePath { get; }
        MsDataFileUri DataFilePath { get; }
        ChromSource Source { get; }
        IList<float> Times { get; }
        TransitionFullScanInfo[] Transitions { get; }
        // Return a collection of consecutive scans with common retention time and changing ion mobility (or a single scan if no drift info in file)
        MsDataSpectrum[] GetMsDataFileSpectraWithCommonRetentionTime(int dataFileSpectrumStartIndex, bool ignoreZeroIntensityPoints, bool? centroidedMs1 = null, bool? centroidedMs2 = null); 
        bool ProvidesCollisionalCrossSectionConverter { get; }
        bool IsWatersSonarData { get; } // Returns true if data presents as ion mobility but is actually filtered on precursor m/z
        Tuple<int, int> SonarMzToBinRange(double mz, double tolerance); // Maps an mz value into the Waters SONAR bin space
        double? SonarBinToPrecursorMz(int bin); // Maps a Waters SONAR bin into precursor mz space - returns average of the m/z range for the bin
        double? CCSFromIonMobility(IonMobilityValue ionMobilityValue, double mz, int charge); // Return a collisional cross section for this ion mobility value at this mz and charge, if reader supports this
        eIonMobilityUnits IonMobilityUnits { get; } 
        bool Adopt(IScanProvider scanProvider);
    }

    public class ScanProvider : IScanProvider
    {
        private MsDataFileImpl _dataFile;
        private Dictionary<int, MsDataFileImpl> _dataFileCentroidedMap = new Dictionary<int, MsDataFileImpl>(4);
        private MsDataFileScanIds _msDataFileScanIds; // Indexed container of MsDataFileImpl ids
        private ChromCachedFile _cachedFile;    // Cached file for the ids
        // Hold a strong reference to the measured results until the scan IDs are read
        private MeasuredResults _measuredResults;
        // And afterward only a weak reference to ensure we are caching values for the same results
        private WeakReference<MeasuredResults> _measuredResultsReference;

        public ScanProvider(string docFilePath, MsDataFileUri dataFilePath, ChromSource source,
            IList<float> times, TransitionFullScanInfo[] transitions, MeasuredResults measuredResults) :
            this(docFilePath, dataFilePath, source, times, transitions, measuredResults, null)
        {
        }

        public ScanProvider(string docFilePath, MsDataFileUri dataFilePath, ChromSource source,
            IList<float> times, TransitionFullScanInfo[] transitions, MeasuredResults measuredResults, MsDataFileScanIds msDataFileScanIds)
        {
            DocFilePath = docFilePath;
            DataFilePath = dataFilePath;
            Source = source;
            Times = times;
            Transitions = transitions;
            _msDataFileScanIds = msDataFileScanIds;
            _measuredResults = measuredResults;
            _measuredResultsReference = new WeakReference<MeasuredResults>(measuredResults);
        }

        public bool ProvidesCollisionalCrossSectionConverter { get { return _dataFile != null && _dataFile.ProvidesCollisionalCrossSectionConverter; } }

        // Returns true if data presents as ion mobility but is actually filtered on precursor m/z
        public bool IsWatersSonarData { get { return _dataFile?.IsWatersSonarData() ?? false; } }

        public Tuple<int,int> SonarMzToBinRange(double mz, double tolerance) {  return _dataFile?.SonarMzToBinRange(mz, tolerance); }
        public double? SonarBinToPrecursorMz(int bin) { return _dataFile?.SonarBinToPrecursorMz(bin); } // Maps a Waters SONAR bin into precursor mz space - returns average of the m/z range for the bin

        public double? CCSFromIonMobility(IonMobilityValue ionMobilityValue, double mz, int charge)
        {
            if (_dataFile == null)
                return null;
            return _dataFile.CCSFromIonMobilityValue(ionMobilityValue, mz, charge);
        }

        public eIonMobilityUnits IonMobilityUnits
        {
            get
            {
                if (_dataFile == null)
                    return eIonMobilityUnits.none;
                return _dataFile.IonMobilityUnits;
            }
        }

        /// <summary>
        /// Checks for file existence using ScanProvider search rules (as stated, in doc dir, in doc dir parent)
        /// </summary>
        /// <param name="docFilePath">Full path of the Skyline document that uses the data file</param>
        /// <param name="dataFilePath">Full path of file to be verified as existing</param>
        /// <returns>true if file exists as described or in any of the standard search locations</returns>
        public static bool FileExists(string docFilePath, MsDataFileUri dataFilePath)
        {
            var tester = new ScanProvider(docFilePath, dataFilePath, ChromSource.unknown, null, null, null);
            return tester.FindDataFilePath() != null;
        }

        public bool Adopt(IScanProvider other)
        {
            if (!Equals(DocFilePath, other.DocFilePath) || !Equals(DataFilePath, other.DataFilePath))
                return false;
            var scanProvider = other as ScanProvider;
            if (scanProvider == null)
                return false;
            MeasuredResults thisMeasuredResults, otherMeasuredResults;
            if (!_measuredResultsReference.TryGetTarget(out thisMeasuredResults) ||
                !scanProvider._measuredResultsReference.TryGetTarget(out otherMeasuredResults))
            {
                return false;
            }
            if (!ReferenceEquals(thisMeasuredResults, otherMeasuredResults))
            {
                return false;
            }
            _dataFile = scanProvider._dataFile;
            foreach (var key in scanProvider._dataFileCentroidedMap.Keys.ToList())
                _dataFileCentroidedMap.Add(key, scanProvider._dataFileCentroidedMap[key]);
            _msDataFileScanIds = scanProvider._msDataFileScanIds;
            _cachedFile = scanProvider._cachedFile;
            _measuredResults = scanProvider._measuredResults;
            scanProvider._dataFile = null;
            foreach (var key in scanProvider._dataFileCentroidedMap.Keys.ToList())
                scanProvider._dataFileCentroidedMap[key] = null;
            return true;
        }

        public string DocFilePath { get; private set; }
        public MsDataFileUri DataFilePath { get; private set; }
        public ChromSource Source { get; private set; }
        public IList<float> Times { get; private set; }
        public TransitionFullScanInfo[] Transitions { get; private set; }

        /// <summary>
        /// Retrieve a run of raw spectra with common retention time and changing ion mobility, or a single raw spectrum if no drift info
        /// </summary>
        /// <param name="internalScanIndex">an index in pwiz.Skyline.Model.Results space</param>
        /// <param name="ignoreZeroIntensityPoints">display uses want zero intensity points, data processing uses typically do not</param>
        /// <param name="centroidedMs1">explicitly specifies the type of the MS1 spectrum to retrieve (profile or centroided).
        ///     If null the chromatogram extraction type is used.</param>
        /// <param name="centroidedMs2">explicitly specifies the type of the MS2 spectrum to retrieve (profile or centroided)</param>
        /// <returns>Array of spectra with the same retention time (potentially different ion mobility values for IMS, or just one spectrum)</returns>
        public MsDataSpectrum[] GetMsDataFileSpectraWithCommonRetentionTime(int internalScanIndex, bool ignoreZeroIntensityPoints, bool? centroidedMs1 = null, bool? centroidedMs2 = null)
        {
            var spectra = new List<MsDataSpectrum>();
            if (_measuredResults != null)
            {
                _msDataFileScanIds = _measuredResults.LoadMSDataFileScanIds(DataFilePath, out _cachedFile);
                _measuredResults = null;
            }
            int dataFileSpectrumStartIndex = internalScanIndex;
            GetDataFile(ignoreZeroIntensityPoints); //Make sure we always have the default file.
            var dataFile = GetDataFile(ignoreZeroIntensityPoints, centroidedMs1, centroidedMs2);
            // For backward compatibility support SKYD files that did not store scan ID bytes
            if (_msDataFileScanIds != null)
            {
                var scanIdText = _msDataFileScanIds.GetMsDataFileSpectrumId(internalScanIndex);
                dataFileSpectrumStartIndex = GetDataFile(ignoreZeroIntensityPoints).GetSpectrumIndex(scanIdText);
                // TODO(brendanx): Improve this error message post-UI freeze
//                if (dataFileSpectrumStartIndex == -1)
//                    throw new ArgumentException(string.Format("The stored scan ID {0} was not found in the file {1}.", scanIdText, DataFilePath));
            }

            MsDataSpectrum currentSpectrum;
            try
            {
                currentSpectrum = dataFile.GetSpectrum(dataFileSpectrumStartIndex);
            }
            catch (Exception ex)
            {
                //get default spectrum type if the requested type is not available
                if (ex.Message.Contains(@"PeakDetector::NoVendorPeakPickingException"))
                    currentSpectrum = GetDataFile(ignoreZeroIntensityPoints).GetSpectrum(dataFileSpectrumStartIndex);
                else
                    throw;

            }

            spectra.Add(currentSpectrum);
            if (currentSpectrum.IonMobilities != null)  // Sort combined IMS spectra by m/z order
            {
                ArrayUtil.Sort(currentSpectrum.Mzs, currentSpectrum.Intensities, currentSpectrum.IonMobilities);
            }
            else if (currentSpectrum.IonMobility.HasValue) // Look ahead for uncombined IMS spectra
            {
                // Look for spectra with identical retention time and changing ion mobility values
                while (true)
                {
                    dataFileSpectrumStartIndex++;
                    //ignore the centroided options for ion mobility spectra.
                    var nextSpectrum = GetDataFile(ignoreZeroIntensityPoints).GetSpectrum(dataFileSpectrumStartIndex);  
                    if (!nextSpectrum.IonMobility.HasValue ||
                        nextSpectrum.RetentionTime != currentSpectrum.RetentionTime)
                    {
                        break;
                    }
                    spectra.Add(nextSpectrum);
                    currentSpectrum = nextSpectrum;
                }
            }
            return spectra.ToArray();
        }

        private MsDataFileImpl GetDataFile(bool ignoreZeroIntensityPoints, bool? centroidedMs1 = null, bool? centroidedMs2 = null)
        {
            var centroidedMapKey = ((centroidedMs1 ?? _cachedFile?.UsedMs1Centroids ?? false) ? 1 : 0) << 1 |
                                   ((centroidedMs2 ?? _cachedFile?.UsedMs2Centroids ?? false) ? 1 : 0);
            if (!_dataFileCentroidedMap.ContainsKey(centroidedMapKey))
            {
                const bool simAsSpectra = true; // SIM always as spectra here
                const bool preferOnlyMs1 = false; // Open with all available spectra indexed
                MsDataFileImpl dataFile;

                if (DataFilePath is MsDataFilePath)
                {
                    string dataFilePath = FindDataFilePath();
                    var lockMassParameters = DataFilePath.GetLockMassParameters();
                    if (dataFilePath == null)
                        throw new FileNotFoundException(string.Format(
                            Resources
                                .ScanProvider_GetScans_The_data_file__0__could_not_be_found__either_at_its_original_location_or_in_the_document_or_document_parent_folder_,
                            DataFilePath));
                    int sampleIndex = SampleHelp.GetPathSampleIndexPart(dataFilePath);
                    if (sampleIndex == -1)
                        sampleIndex = 0;
                    // Full-scan extraction always uses SIM as spectra
                    dataFile = new MsDataFileImpl(dataFilePath, sampleIndex,
                        lockMassParameters,
                        simAsSpectra,
                        combineIonMobilitySpectra: _cachedFile?.HasCombinedIonMobility ?? false,
                        requireVendorCentroidedMS1: centroidedMs1 ?? _cachedFile?.UsedMs1Centroids ?? false,
                        requireVendorCentroidedMS2: centroidedMs2 ?? _cachedFile?.UsedMs2Centroids ?? false,
                        ignoreZeroIntensityPoints: ignoreZeroIntensityPoints);
                }
                else
                {
                    dataFile = DataFilePath.OpenMsDataFile(simAsSpectra, preferOnlyMs1,
                        centroidedMs1 ?? _cachedFile?.UsedMs1Centroids ?? false, 
                        centroidedMs2 ?? _cachedFile?.UsedMs2Centroids ?? false, ignoreZeroIntensityPoints);
                }
                if (centroidedMs1 == null && centroidedMs2 == null)
                    _dataFile = dataFile;
                _dataFileCentroidedMap.Add(centroidedMapKey, dataFile);
            }
            return _dataFileCentroidedMap[centroidedMapKey];
        }

        public string FindDataFilePath()
        {
            var msDataFilePath = DataFilePath as MsDataFilePath;
            if (null == msDataFilePath)
            {
                return null;
            }
            string dataFilePath = msDataFilePath.FilePath;
            
            if (File.Exists(dataFilePath) || Directory.Exists(dataFilePath))
                return dataFilePath;
            // ReSharper disable ConstantNullCoalescingCondition
            string fileName = Path.GetFileName(dataFilePath) ?? string.Empty;
            string docDir = Path.GetDirectoryName(DocFilePath) ?? Directory.GetCurrentDirectory();
            dataFilePath = Path.Combine(docDir,  fileName);
            if (File.Exists(dataFilePath) || Directory.Exists(dataFilePath))
                return dataFilePath;
            string docParentDir = Path.GetDirectoryName(docDir) ?? Directory.GetCurrentDirectory();
            dataFilePath = Path.Combine(docParentDir, fileName);
            // ReSharper restore ConstantNullCoalescingCondition
            if (File.Exists(dataFilePath) || Directory.Exists(dataFilePath))
                return dataFilePath;
            if (!string.IsNullOrEmpty(Program.ExtraRawFileSearchFolder))
            {
                // For testing, we may keep raw files in a semi permanent location other than the testdir
                dataFilePath = Path.Combine(Program.ExtraRawFileSearchFolder, fileName);
                if (File.Exists(dataFilePath) || Directory.Exists(dataFilePath))
                    return dataFilePath;
            }
                    
            return null;
        }

        public void Dispose()
        {
            lock (this)
            {
                foreach (var key in _dataFileCentroidedMap.Keys)
                    _dataFileCentroidedMap[key]?.Dispose();
                _dataFile = null;
                _dataFileCentroidedMap.Clear();
            }
        }
    }
}
