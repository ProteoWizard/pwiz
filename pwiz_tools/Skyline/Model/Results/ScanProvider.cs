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
using pwiz.Common.Chemistry;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results
{
    public class TransitionFullScanInfo
    {
        public string Name;
        public ChromSource Source;
        public IList<int> ScanIndexes;
        public Color Color;
        public SignedMz PrecursorMz;
        public SignedMz ProductMz;
        public double? ExtractionWidth;
        public IonMobilityFilter _ionMobilityInfo;
        public Identity Id;  // ID of the associated TransitionDocNode
    }

    public interface IScanProvider : IDisposable
    {
        string DocFilePath { get; }
        MsDataFileUri DataFilePath { get; }
        ChromSource Source { get; }
        IList<float> Times { get; }
        TransitionFullScanInfo[] Transitions { get; }
        MsDataSpectrum[] GetMsDataFileSpectraWithCommonRetentionTime(int dataFileSpectrumStartIndex); // Return a collection of consecutive scans with common retention time and changing ion mobility (or a single scan if no drift info in file)
        bool ProvidesCollisionalCrossSectionConverter { get; }
        double? CCSFromIonMobility(IonMobilityValue ionMobilityValue, double mz, int charge); // Return a collisional cross section for this ion mobility value at this mz and charge, if reader supports this
        MsDataFileImpl.eIonMobilityUnits IonMobilityUnits { get; } 
        bool Adopt(IScanProvider scanProvider);
    }

    public class ScanProvider : IScanProvider
    {
        private MsDataFileImpl _dataFile;
        private MsDataFileScanIds _msDataFileScanIds; // Indexed container of MsDataFileImpl ids
        private Func<MsDataFileScanIds> _getMsDataFileScanIds;
        private WeakReference<MeasuredResults> _measuredResultsReference;

        public ScanProvider(string docFilePath, MsDataFileUri dataFilePath, ChromSource source,
            IList<float> times, TransitionFullScanInfo[] transitions, MeasuredResults measuredResults, Func<MsDataFileScanIds> getMsDataFileScanIds)
        {
            DocFilePath = docFilePath;
            DataFilePath = dataFilePath;
            Source = source;
            Times = times;
            Transitions = transitions;
            _measuredResultsReference = new WeakReference<MeasuredResults>(measuredResults);
            _getMsDataFileScanIds = getMsDataFileScanIds;
        }

        public bool ProvidesCollisionalCrossSectionConverter { get { return _dataFile != null && _dataFile.ProvidesCollisionalCrossSectionConverter; } }

        public double? CCSFromIonMobility(IonMobilityValue ionMobilityValue, double mz, int charge)
        {
            if (_dataFile == null)
                return null;
            return _dataFile.CCSFromIonMobilityValue(ionMobilityValue, mz, charge);
        }

        public MsDataFileImpl.eIonMobilityUnits IonMobilityUnits
        {
            get
            {
                if (_dataFile == null)
                    return MsDataFileImpl.eIonMobilityUnits.none;
                return _dataFile.IonMobilityUnits;
            }
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
            _msDataFileScanIds = scanProvider._msDataFileScanIds;
            _getMsDataFileScanIds = scanProvider._getMsDataFileScanIds;
            scanProvider._dataFile = null;
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
        /// <returns>Array of spectra with the same retention time (potentially different ion mobility values for IMS, or just one spectrum)</returns>
        public MsDataSpectrum[] GetMsDataFileSpectraWithCommonRetentionTime(int internalScanIndex)
        {
            var spectra = new List<MsDataSpectrum>();
            if (_getMsDataFileScanIds != null)
            {
                _msDataFileScanIds = _getMsDataFileScanIds();
                _getMsDataFileScanIds = null;
            }
            int dataFileSpectrumStartIndex = internalScanIndex;
            // For backward compatibility support SKYD files that did not store scan ID bytes
            if (_msDataFileScanIds != null)
            {
                var scanIdText = _msDataFileScanIds.GetMsDataFileSpectrumId(internalScanIndex);
                dataFileSpectrumStartIndex = GetDataFile().GetSpectrumIndex(scanIdText);
                if (dataFileSpectrumStartIndex == -1)
                    throw new IOException(string.Format(Resources.ScanProvider_GetScans_The_scan_ID__0__was_not_found_in_the_file__1__, scanIdText, DataFilePath.GetFileName()));
            }
            var currentSpectrum = GetDataFile().GetSpectrum(dataFileSpectrumStartIndex);
            spectra.Add(currentSpectrum);
            if (currentSpectrum.IonMobility.HasValue)
            {
                // Look for spectra with identical retention time and changing ion mobility values
                while (true)
                {
                    dataFileSpectrumStartIndex++;
                    var nextSpectrum = GetDataFile().GetSpectrum(dataFileSpectrumStartIndex);
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

        private MsDataFileImpl GetDataFile()
        {
            if (_dataFile == null)
            {
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
                    _dataFile = new MsDataFileImpl(dataFilePath, sampleIndex, lockMassParameters, true,
                        requireVendorCentroidedMS1: DataFilePath.GetCentroidMs1(),
                        requireVendorCentroidedMS2: DataFilePath.GetCentroidMs2());
                }
                else
                {
                    _dataFile = DataFilePath.OpenMsDataFile(true, 0);
                }
            }
            return _dataFile;
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
                if (_dataFile != null)
                {
                    _dataFile.Dispose();
                    _dataFile = null;
                }
            }
        }
    }
}
