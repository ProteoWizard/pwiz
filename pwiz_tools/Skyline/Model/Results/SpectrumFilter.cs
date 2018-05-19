/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.RemoteApi.GeneratedCode;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public interface IFilterInstrumentInfo : IIonMobilityFunctionsProvider
    {
        bool IsWatersFile { get; }
        bool IsAgilentFile { get; }
    }

    public interface IIonMobilityFunctionsProvider
    {
        bool ProvidesCollisionalCrossSectionConverter { get; }
        MsDataFileImpl.eIonMobilityUnits IonMobilityUnits { get; } // Reports ion mobility units in use by the mass spec
        IonMobilityValue IonMobilityFromCCS(double ccs, double mz, int charge); // Convert from Collisional Cross Section to ion mobility
        double CCSFromIonMobility(IonMobilityValue im, double mz, int charge); // Convert from ion mobility to Collisional Cross Section
    }

    public sealed class SpectrumFilter : IFilterInstrumentInfo
    {
        private readonly TransitionFullScan _fullScan;
        private readonly TransitionInstrument _instrument;
        private readonly FullScanAcquisitionMethod _acquisitionMethod;
        private readonly bool _isHighAccMsFilter;
        private readonly bool _isHighAccProductFilter;
        private readonly bool _isSharedTime;
        private readonly double? _minTime;  // Copied from _instrument.MinTime
        private readonly double? _maxTime;  // Copied from _instrument.MaxTime
        private double _minFilterPairsRT; // Min of range of RT filter values across FilterPairs 
        private double _maxFilterPairsRT; // Max of range of RT filter values across FilterPairs
        private readonly SpectrumFilterPair[] _filterMzValues;
        private readonly SpectrumFilterPair[] _filterRTValues;
        private readonly ChromKey[] _productChromKeys;
        private int _retentionTimeIndex;
        private readonly bool _isWatersFile;
        private readonly bool _isWatersMse;
        private readonly bool _isAgilentMse;
        private readonly IIonMobilityFunctionsProvider _ionMobilityFunctionsProvider;
        private int _mseLevel;
        private MsDataSpectrum _mseLastSpectrum;
        private int _mseLastSpectrumLevel; // for averaging Agilent stepped CE spectra

        public IEnumerable<SpectrumFilterPair> FilterPairs { get { return _filterMzValues; } }

        public SpectrumFilter(SrmDocument document, MsDataFileUri msDataFileUri, IFilterInstrumentInfo instrumentInfo,
            double? maxObservedIonMobilityValue = null,
            IRetentionTimePredictor retentionTimePredictor = null, bool firstPass = false)
        {
            _fullScan = document.Settings.TransitionSettings.FullScan;
            _instrument = document.Settings.TransitionSettings.Instrument;
            _acquisitionMethod = _fullScan.AcquisitionMethod;
            _ionMobilityFunctionsProvider = instrumentInfo;
            if (instrumentInfo != null)
            {
                _isWatersFile = instrumentInfo.IsWatersFile;
            }
            IsFirstPass = firstPass;

            var comparer = PrecursorTextId.PrecursorTextIdComparerInstance;
            var dictPrecursorMzToFilter = new SortedDictionary<PrecursorTextId, SpectrumFilterPair>(comparer);

            if (EnabledMs || EnabledMsMs)
            {
                if (EnabledMs)
                {
                    _isHighAccMsFilter = !Equals(_fullScan.PrecursorMassAnalyzer,
                        FullScanMassAnalyzerType.qit);

                    if (!firstPass)
                    {
                        var key = new PrecursorTextId(SignedMz.ZERO, null, ChromExtractor.summed);  // TIC
                        dictPrecursorMzToFilter.Add(key, new SpectrumFilterPair(key, PeptideDocNode.UNKNOWN_COLOR, dictPrecursorMzToFilter.Count,
                            _instrument.MinTime, _instrument.MaxTime, null, null, IonMobilityAndCCS.EMPTY, _isHighAccMsFilter, _isHighAccProductFilter));
                        key = new PrecursorTextId(SignedMz.ZERO, null, ChromExtractor.base_peak);   // BPC
                        dictPrecursorMzToFilter.Add(key, new SpectrumFilterPair(key, PeptideDocNode.UNKNOWN_COLOR, dictPrecursorMzToFilter.Count,
                            _instrument.MinTime, _instrument.MaxTime, null, null, IonMobilityAndCCS.EMPTY, _isHighAccMsFilter, _isHighAccProductFilter));
                    }
                }
                if (EnabledMsMs)
                {
                    _isHighAccProductFilter = !Equals(_fullScan.ProductMassAnalyzer,
                        FullScanMassAnalyzerType.qit);

                    if (_fullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA &&
                        _fullScan.IsolationScheme.IsAllIons)
                    {
                        if (instrumentInfo != null)
                        {
                            _isWatersMse = _isWatersFile;
                            _isAgilentMse = instrumentInfo.IsAgilentFile;
                        }
                        _mseLevel = 1;
                    }
                }

                Func<double, double> calcWindowsQ1 = _fullScan.GetPrecursorFilterWindow;
                Func<double, double> calcWindowsQ3 = _fullScan.GetProductFilterWindow;
                _minTime = _instrument.MinTime;
                _maxTime = _instrument.MaxTime;
                bool canSchedule = !firstPass && CanSchedule(document, retentionTimePredictor);
                // TODO: Figure out a way to turn off time sharing on first SIM scan so that
                //       times can be shared for MS1 without SIM scans
                _isSharedTime = !canSchedule;

                // If we're using bare measured ion mobility values from spectral libraries, go get those now
                var libraryIonMobilityInfo = document.Settings.PeptideSettings.Prediction.UseLibraryIonMobilityValues
                    ? document.Settings.GetIonMobilities(msDataFileUri)
                    : null;
                var ionMobilityMax = maxObservedIonMobilityValue??0;

                int filterCount = 0;
                foreach (var nodePep in document.Molecules)
                {
                    if (firstPass && !retentionTimePredictor.IsFirstPassPeptide(nodePep))
                        continue;

                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                    {
                        if (nodeGroup.Children.Count == 0)
                            continue;

                        double? minTime = _minTime, maxTime = _maxTime;
                        double? startIM = null, endIM = null;
                        double windowIM;
                        var ionMobility = document.Settings.PeptideSettings.Prediction.GetIonMobility(
                            nodePep, nodeGroup, libraryIonMobilityInfo, _ionMobilityFunctionsProvider, ionMobilityMax, out windowIM);
                        if (ionMobility.IonMobility.HasValue)
                        {
                            startIM = ionMobility.IonMobility.Mobility - windowIM / 2; // Get the low energy ion mobility
                            endIM = startIM + windowIM;
                        }
                        else
                        {
                            ionMobility = IonMobilityAndCCS.EMPTY;
                        }

                        
                        if (_fullScan.RetentionTimeFilterType != RetentionTimeFilterType.none)
                        {
                            var peakBoundaries = document.Settings.GetExplicitPeakBounds(nodePep, msDataFileUri);
                            if (peakBoundaries != null)
                            {
                                minTime = peakBoundaries.StartTime - _fullScan.RetentionTimeFilterLength;
                                maxTime = peakBoundaries.EndTime + _fullScan.RetentionTimeFilterLength;
                                _isSharedTime = false;
                            }
                        }
                        if (canSchedule)
                        {
                            if (RetentionTimeFilterType.scheduling_windows == _fullScan.RetentionTimeFilterType)
                            {
                                double? centerTime = null;
                                double windowRT = 0;
                                    if (retentionTimePredictor != null)
                                    {
                                        centerTime = retentionTimePredictor.GetPredictedRetentionTime(nodePep);
                                    }
                                    else
                                    {
                                        var prediction = document.Settings.PeptideSettings.Prediction;
                                        if (prediction.RetentionTime == null || !prediction.RetentionTime.IsAutoCalculated)
                                        {
                                            centerTime = document.Settings.PeptideSettings.Prediction.PredictRetentionTimeForChromImport(
                                                document, nodePep, nodeGroup, out windowRT);
                                        }
                                    }
                                // Force the center time to be at least zero
                                if (centerTime.HasValue && centerTime.Value < 0)
                                    centerTime = 0;
                                if (_fullScan.RetentionTimeFilterLength != 0)
                                {
                                    windowRT = _fullScan.RetentionTimeFilterLength * 2;
                                }
                                if (centerTime != null)
                                {
                                    double startTime = centerTime.Value - windowRT / 2;
                                    double endTime = startTime + windowRT;
                                    minTime = Math.Max(minTime ?? 0, startTime);
                                    maxTime = Math.Min(maxTime ?? double.MaxValue, endTime);
                                }
                            }
                            else if (RetentionTimeFilterType.ms2_ids == _fullScan.RetentionTimeFilterType)
                            {
                                var times = document.Settings.GetBestRetentionTimes(nodePep, msDataFileUri);
                                if (times.Length > 0)
                                {
                                    minTime = Math.Max(minTime ?? 0, times.Min() - _fullScan.RetentionTimeFilterLength);
                                    maxTime = Math.Min(maxTime ?? double.MaxValue, times.Max() + _fullScan.RetentionTimeFilterLength);
                                }
                            }
                        }

                        SpectrumFilterPair filter;
                        var textId = nodePep.ModifiedTarget; // Modified Sequence for peptides, or some other string for custom ions
                        var mz = new SignedMz(nodeGroup.PrecursorMz, nodeGroup.PrecursorCharge < 0);
                        var key = new PrecursorTextId(mz, textId, ChromExtractor.summed);
                        if (!dictPrecursorMzToFilter.TryGetValue(key, out filter))
                        {
                            filter = new SpectrumFilterPair(key, nodePep.Color, dictPrecursorMzToFilter.Count, minTime, maxTime,
                                startIM, endIM, ionMobility,
                                _isHighAccMsFilter, _isHighAccProductFilter);
                            dictPrecursorMzToFilter.Add(key, filter);
                        }

                        if (!EnabledMs)
                        {
                            filterCount += filter.AddQ3FilterValues(from TransitionDocNode nodeTran in nodeGroup.Children
                                                     select nodeTran.Mz, calcWindowsQ3);
                        }
                        else if (!EnabledMsMs)
                        {
                            filterCount += filter.AddQ1FilterValues(GetMS1MzValues(nodeGroup), calcWindowsQ1);
                        }
                        else
                        {
                            filterCount += filter.AddQ1FilterValues(GetMS1MzValues(nodeGroup), calcWindowsQ1);
                            filterCount += filter.AddQ3FilterValues(from TransitionDocNode nodeTran in nodeGroup.Children
                                                     where !nodeTran.IsMs1
                                                     select nodeTran.Mz, calcWindowsQ3);
                        }
                    }
                }
                _filterMzValues = dictPrecursorMzToFilter.Values.ToArray();

                var listChromKeyFilterIds = new List<ChromKey>(filterCount);
                foreach (var spectrumFilterPair in _filterMzValues)
                {
                    spectrumFilterPair.AddChromKeys(listChromKeyFilterIds);
                }
                _productChromKeys = listChromKeyFilterIds.ToArray();

                // Sort a copy of the filter pairs by maximum retention time so that we can detect when
                // filters are no longer active.
                _filterRTValues = new SpectrumFilterPair[_filterMzValues.Length];
                Array.Copy(_filterMzValues, _filterRTValues, _filterMzValues.Length);
                Array.Sort(_filterRTValues, CompareByRT);

            }

            InitRTLimits();
        }

        public bool ProvidesCollisionalCrossSectionConverter { get { return _ionMobilityFunctionsProvider != null;  } }

        public MsDataFileImpl.eIonMobilityUnits IonMobilityUnits
        {
            get
            {
                return ProvidesCollisionalCrossSectionConverter
                    ? _ionMobilityFunctionsProvider.IonMobilityUnits
                    : MsDataFileImpl.eIonMobilityUnits.none;
            } }

        public IonMobilityValue IonMobilityFromCCS(double ccs, double mz, int charge)
        {
            if (ProvidesCollisionalCrossSectionConverter)
            {
                return _ionMobilityFunctionsProvider.IonMobilityFromCCS(ccs, mz, charge);
            }
            Assume.IsNotNull(_ionMobilityFunctionsProvider, "No CCS to ion mobility translation is possible for this data set"); // Not L10N
            return IonMobilityValue.EMPTY;
        }

        public double CCSFromIonMobility(IonMobilityValue im, double mz, int charge)
        {
            if (ProvidesCollisionalCrossSectionConverter)
            {
                return _ionMobilityFunctionsProvider.CCSFromIonMobility(im, mz, charge);
            }
            Assume.IsNotNull(_ionMobilityFunctionsProvider, "No ion mobility to CCS translation is possible for this data set"); // Not L10N
            return 0;
        }

        private bool CanSchedule(SrmDocument document, IRetentionTimePredictor retentionTimePredictor)
        {
            bool canSchedule;
            if (RetentionTimeFilterType.scheduling_windows == _fullScan.RetentionTimeFilterType)
            {
                canSchedule =
                    document.Settings.PeptideSettings.Prediction.CanSchedule(document, PeptidePrediction.SchedulingStrategy.any) ||
                    null != retentionTimePredictor;
            }
            else if (RetentionTimeFilterType.ms2_ids == _fullScan.RetentionTimeFilterType)
            {
                canSchedule = true;
            }
            else
            {
                canSchedule = false;
            }
            return canSchedule;
        }

        /// <summary>
        /// Determine min and max range across all retention time filters, if any
        /// </summary>
        private void InitRTLimits()
        {
            _maxFilterPairsRT = double.MaxValue;
            _minFilterPairsRT = double.MinValue;
            if (FilterPairs != null)
            {
                double? maxRT = null;
                double? minRT = null;
                foreach (var fp in FilterPairs)
                {
                    if (!fp.MaxTime.HasValue || !fp.MinTime.HasValue)
                    {
                        maxRT = null;
                        minRT = null;
                        break;
                    }
                    if (fp.MaxTime.Value > (maxRT ?? double.MinValue))
                        maxRT = fp.MaxTime.Value;
                    if (fp.MinTime.Value < (minRT ?? double.MaxValue))
                        minRT = fp.MinTime.Value;
                }
                if (maxRT.HasValue)
                    _maxFilterPairsRT = maxRT.Value;
                if (minRT.HasValue && !IsMseData()) // For MSe data, just start from the beginning lest we drop in mid-cycle
                    _minFilterPairsRT = minRT.Value;
            }
        }

        /// <summary>
        /// Compare filter pairs by maximum retention time.
        /// </summary>
        private int CompareByRT(SpectrumFilterPair x, SpectrumFilterPair y)
        {
            return 
                !x.MaxTime.HasValue ? 1 :
                !y.MaxTime.HasValue ? -1 :
                x.MaxTime.Value.CompareTo(y.MaxTime.Value);
        }

        public bool IsFirstPass { get; private set; }

        public bool IsWatersFile
        {
            get { return _isWatersFile; }
        }

        public bool IsWatersMse
        {
            get { return _isWatersMse; }
        }

        public bool IsAgilentFile
        {
            get { return _isAgilentMse; }
        }

        /// <summary>
        /// Returns true if ProteoWizard implementation of spectrum list does not support time ordered 
        /// extraction.  This used to be the case, for example, with Waters where MS1 then MS/MS scans were 
        /// traversed due to the nature of the underlying raw data.  For now no vendor data is presented by
        /// ProteoWizard in anything other than time order.
        /// </summary>
        public bool IsTimeOrderedExtraction
        {
            get { return true; }
        }

        public IList<ChromKey> ProductChromKeys
        {
            get { return _productChromKeys; }
        }

        public bool IsOutsideRetentionTimeRange(double? rtCheck)
        {
            if ((!rtCheck.HasValue) || (rtCheck.Value == 0)) // Consider these both as meaning "unknown"
                return false; // Can't reject an as-yet-unknown value
            return ((rtCheck.Value < _minFilterPairsRT || _maxFilterPairsRT < rtCheck.Value));
        }

        public bool IsMseData()
        {
            return (EnabledMsMs && _fullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA &&
                    _fullScan.IsolationScheme.IsAllIons);
        }

        /*
        public int Count
        {
            get
            {
                return _filterMzValues != null
                           ? _filterMzValues.SelectMany(pair => pair.ArrayQ3 ?? pair.ArrayQ1).Count()
                           : 0;
            }
        }
        */

        private IEnumerable<SignedMz> GetMS1MzValues(TransitionGroupDocNode nodeGroup)
        {
            var isotopePeaks = nodeGroup.IsotopeDist;
            if (isotopePeaks == null)
            {
                // Return the MS1 transition m/z values, if the precursor has no isotope peaks
                foreach (var nodeTran in nodeGroup.Children.Cast<TransitionDocNode>().Where(t => t.IsMs1))
                    yield return nodeTran.Mz;
            }
            else
            {
                // Otherwise, return all possible isotope peaks
                for (int i = 0; i < isotopePeaks.CountPeaks; i++)
                    yield return new SignedMz(isotopePeaks.GetMZI(isotopePeaks.PeakIndexToMassIndex(i)), nodeGroup.PrecursorCharge < 0);
            }
        }

        public bool EnabledMs { get { return _fullScan.PrecursorIsotopes != FullScanPrecursorIsotopes.None; } }
        public bool IsHighAccMsFilter { get { return _isHighAccMsFilter; } }
        public bool EnabledMsMs { get { return _acquisitionMethod != FullScanAcquisitionMethod.None; } }
        public bool IsHighAccProductFilter { get { return _isHighAccProductFilter; } }
        public bool IsSharedTime { get { return _isSharedTime; } }
        public bool IsAgilentMse { get { return _isAgilentMse; } }


        public bool ContainsTime(double time)
        {
            return (!_minTime.HasValue || _minTime.Value <= time) &&
                (!_maxTime.HasValue || _maxTime.Value >= time);
        }

        public double? MaxTime { get { return _maxTime; } }

        public bool IsMsSpectrum(MsDataSpectrum dataSpectrum)
        {
            if (!EnabledMs)
                return false;
            if (_mseLevel > 0)
                return UpdateMseLevel(dataSpectrum) == 1;
            return dataSpectrum.Level == 1;
        }

        public bool IsSimSpectrum(MsDataSpectrum dataSpectrum, MsDataSpectrum[] spectra)
        {
            if (!EnabledMs || _mseLevel > 0)
                return false;
            bool isSimSpectrum = dataSpectrum.Level == 1 &&
                IsSimIsolation(GetIsolationWindows(dataSpectrum.Precursors).FirstOrDefault());
            if (isSimSpectrum && spectra.Length > 1)
            {
                // If this is actually a run of IMS bins, look at the aggregate isolation window
                var rt = spectra[0].RetentionTime;
                var win = GetIsolationWindows(spectra[0].Precursors).FirstOrDefault();
                double mzLow = win.IsolationMz.Value - win.IsolationWidth.Value/2;
                double mzHigh = win.IsolationMz.Value + win.IsolationWidth.Value/2;
                for (var i = 1; i < spectra.Length; i++)
                {
                    var spec = spectra[i];
                    if (!Equals(spec.RetentionTime, rt) || !IonMobilityValue.IsExpectedValueOrdering(spectra[i - 1].IonMobility, spec.IonMobility))
                        return true;  // Not a run of IMS bins, must have been actual SIM scan
                    win = GetIsolationWindows(spec.Precursors).FirstOrDefault();
                    var halfIsolationWidth = (win.IsolationWidth??0) / 2; // Isolation width may not be declared in IMS scans
                    mzLow = Math.Min(mzLow, win.IsolationMz.Value - halfIsolationWidth);
                    mzHigh = Math.Max(mzHigh, win.IsolationMz.Value + halfIsolationWidth);
                }
                var width = mzHigh - mzLow;
                isSimSpectrum = IsSimIsolation(new IsolationWindowFilter(new SignedMz(mzLow + width/2), width));
            }
            return isSimSpectrum;
        }

        private static bool IsSimIsolation(IsolationWindowFilter isoWin)
        {
            return isoWin.IsolationMz.HasValue && isoWin.IsolationWidth.HasValue &&
                // TODO: Introduce a variable cut-off in the document settings
                isoWin.IsolationWidth.Value <= 200;
        }

        public bool IsMsMsSpectrum(MsDataSpectrum dataSpectrum)
        {
            if (!EnabledMsMs)
                return false;
            if (_mseLevel > 0)
                return UpdateMseLevel(dataSpectrum) == 2;
            return dataSpectrum.Level == 2;
        }

        public int GetMseLevel()
        {
            return _mseLevel;
        }

        private int UpdateMseLevel(MsDataSpectrum dataSpectrum)
        {
            int returnval; 
            if ((_mseLastSpectrum == null) || !ReferenceEquals(dataSpectrum, _mseLastSpectrum)) // is this the same one we were just asked about?
            {
                // Waters MSe is enumerated in interleaved scans ("functions" in the raw data) 1==MS 2==MSMS 3=ignore
                // Bruker MSe is enumerated in interleaved MS1 and MS/MS scans
                // Agilent MSe is a series of MS1 scans with ramped CE (SpectrumList_Agilent returns these as MS1,MS2,MS2,...) 
                //    but with ion mobility, as of June 2014, it's just a series of MS2 scans with a single nonzero CE, or MS1 scans with 0 CE
                if (_isAgilentMse)
                {
                    if (1 == dataSpectrum.Level)
                    {
                        _mseLevel = 1; // Expecting a series of MS2 scans to follow after this
                        returnval = 1; // Report as MS1
                    }
                    else if ((2 == dataSpectrum.Level) && 
                        (_mseLastSpectrum != null)) // Sometimes the file doesn't contain that leading MS1 scan
                    {
                        _mseLevel = 2; 
                        returnval = 2; 
                    }
                    else
                    {
                        returnval = 0; // Not useful - probably the file started off mid-cycle, with MS2 CE>0
                    }
                }
                else if (!_isWatersMse)
                {
                    // Bruker - Alternate between 1 and 2
                    _mseLevel = (_mseLevel % 2) + 1;
                    returnval = _mseLevel;
                }
                else
                {
                    // Waters - mse level 1 in raw data "function 1", mse level 2 in raw data "function 2", and "function 3" which we ignore (lockmass?)
                    // All are declared mslevel=1, but we can tell these apart by inspecting the Id which is formatted as <function>.<process>.<scan>
                    _mseLevel = int.Parse(dataSpectrum.Id.Split('.')[0]);
                    returnval = _mseLevel; 
                }
                _mseLastSpectrumLevel = returnval;
            }
            else
            {
                returnval = _mseLastSpectrumLevel; // we were just asked about this spectrum, no update this time
            }
            _mseLastSpectrum = dataSpectrum;
            return returnval;
        }

        public IEnumerable<ExtractedSpectrum> SrmSpectraFromMs1Scan(double? retentionTime,
                                                                    IList<MsPrecursor> precursors, MsDataSpectrum[] spectra)
        {
            if (!EnabledMs || !retentionTime.HasValue || spectra == null)
                yield break;

            // All filter pairs have a shot at filtering the MS1 scans
            bool isSimSpectra = IsSimSpectrum(spectra.First(), spectra);
            foreach (var filterPair in isSimSpectra ? FindMs1FilterPairs(precursors) : _filterMzValues)
            {
                if (!filterPair.ContainsRetentionTime(retentionTime.Value))
                    continue;
                var filteredSrmSpectrum = filterPair.FilterQ1SpectrumList(spectra, isSimSpectra);
                if (filteredSrmSpectrum != null)
                    yield return filteredSrmSpectrum;
            }
        }

        public IEnumerable<ExtractedSpectrum> Extract(double? retentionTime, MsDataSpectrum[] spectra)
        {
            if (!EnabledMsMs || !retentionTime.HasValue || !spectra.Any())
                yield break;

            var handlingType = _fullScan.IsolationScheme == null || _fullScan.IsolationScheme.SpecialHandling == null
                ? IsolationScheme.SpecialHandlingType.NONE
                : _fullScan.IsolationScheme.SpecialHandling;
            bool ignoreIso = handlingType == IsolationScheme.SpecialHandlingType.OVERLAP ||
                             handlingType == IsolationScheme.SpecialHandlingType.OVERLAP_MULTIPLEXED ||
                             handlingType == IsolationScheme.SpecialHandlingType.FAST_OVERLAP;
            
            foreach (var isoWin in GetIsolationWindows(spectra[0].Precursors))
            {
                foreach (var filterPair in FindFilterPairs(isoWin, _acquisitionMethod, ignoreIso))
                {
                    if (!filterPair.ContainsRetentionTime(retentionTime.Value))
                        continue;
                    var filteredSrmSpectrum = filterPair.FilterQ3SpectrumList(spectra, _isWatersMse && GetMseLevel() > 1);
                    if (filteredSrmSpectrum != null)
                        yield return filteredSrmSpectrum;
                }
            }
        }

        public bool HasProductFilterPairs(double? retentionTime, MsPrecursor[] precursors)
        {
            if (!EnabledMsMs || !retentionTime.HasValue || !precursors.Any())
                return false;

            var handlingType = _fullScan.IsolationScheme == null || _fullScan.IsolationScheme.SpecialHandling == null
                ? IsolationScheme.SpecialHandlingType.NONE
                : _fullScan.IsolationScheme.SpecialHandling;
            bool ignoreIso = handlingType == IsolationScheme.SpecialHandlingType.OVERLAP ||
                             handlingType == IsolationScheme.SpecialHandlingType.OVERLAP_MULTIPLEXED ||
                             handlingType == IsolationScheme.SpecialHandlingType.FAST_OVERLAP;

            foreach (var isoWin in GetIsolationWindows(precursors))
            {
                foreach (var filterPair in FindFilterPairs(isoWin, _acquisitionMethod, ignoreIso))
                {
                    if (!filterPair.ContainsRetentionTime(retentionTime.Value))
                        continue;
                    return true;
                }
            }
            return false;
        }

        private IEnumerable<IsolationWindowFilter> GetIsolationWindows(IList<MsPrecursor> precursors)
        {
            // Waters MSe high-energy scans actually appear to be MS1 scans without
            // any isolation m/z.  So, use the instrument range.
            if (_mseLevel > 0)
            {
                double isolationWidth = _instrument.MaxMz - _instrument.MinMz;
                double isolationMz = _instrument.MinMz + isolationWidth / 2;
                if (precursors.Any(p => p.PrecursorMz.HasValue && p.PrecursorMz.Value.IsNegative))
                {
                    yield return new IsolationWindowFilter(new SignedMz(isolationMz, true), isolationWidth);
                }
                if (precursors.Any(p => !p.PrecursorMz.HasValue || !p.PrecursorMz.Value.IsNegative))
                {
                    yield return new IsolationWindowFilter(new SignedMz(isolationMz, false), isolationWidth);
                }
            }
            else if (precursors.Count > 0)
            {
                foreach (var precursor in precursors)
                    yield return new IsolationWindowFilter(precursor.IsolationMz, precursor.IsolationWidth);
            }
            else
            {
                yield return default(IsolationWindowFilter);
            }
        }

        private struct IsolationWindowFilter
        {
            public IsolationWindowFilter(SignedMz? isolationMz, double? isolationWidth) : this()
            {
                IsolationMz = isolationMz;
                IsolationWidth = isolationWidth;
            }

            public SignedMz? IsolationMz { get; private set; }
            public double? IsolationWidth { get; private set; }

            #region object overrides

            private bool Equals(IsolationWindowFilter other)
            {
                return other.IsolationMz.Equals(IsolationMz) &&
                    other.IsolationWidth.Equals(IsolationWidth);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (obj.GetType() != typeof(IsolationWindowFilter)) return false;
                return Equals((IsolationWindowFilter)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (IsolationMz.GetHashCode() * 397) ^
                        (IsolationWidth.HasValue ? IsolationWidth.Value.GetHashCode() : 0);
                }
            }

            #endregion
        }

        // Has to be concurrent, because both the spectrum reader thread and chromatogram extraction thread use this
        private readonly IDictionary<IsolationWindowFilter, IList<SpectrumFilterPair>> _filterPairDictionary =
            new ConcurrentDictionary<IsolationWindowFilter, IList<SpectrumFilterPair>>();

        private IEnumerable<SpectrumFilterPair> FindFilterPairs(IsolationWindowFilter isoWin,
                                                                FullScanAcquisitionMethod acquisitionMethod, bool ignoreIsolationScheme = false)
        {
            if (!isoWin.IsolationMz.HasValue)
                return new SpectrumFilterPair[0]; // empty

            // Return cached value from dictionary if we've seen this target previously.
            var isoWinKey = isoWin;
            IList<SpectrumFilterPair> filterPairsCached;
            if (_filterPairDictionary.TryGetValue(isoWinKey, out filterPairsCached))
            {
                return filterPairsCached;
            }

            var filterPairs = new List<SpectrumFilterPair>();
            if (acquisitionMethod == FullScanAcquisitionMethod.DIA)
            {
                var isoTargMz = isoWin.IsolationMz.Value;
                double? isoTargWidth = isoWin.IsolationWidth;
                if (!ignoreIsolationScheme)
                {
                    CalcDiaIsolationValues(ref isoTargMz, ref isoTargWidth);
                }
                if (isoTargWidth.HasValue)
                {
                    // For multiple case, find the first possible value, and iterate until
                    // no longer matching or the end of the array is encountered
                    int iFilter = IndexOfFilter(isoTargMz, isoTargWidth.Value);
                    if (iFilter != -1)
                    {
                        while (iFilter < _filterMzValues.Length && CompareMz(isoTargMz,
                            _filterMzValues[iFilter].Q1, isoTargWidth.Value) == 0)
                            filterPairs.Add(_filterMzValues[iFilter++]);
                    }
                }
            }
            else
            {
                // For single (Targeted) case, review all possible matches for the one closest to the
                // desired precursor m/z value.
                // per "Issue 263: Too strict about choosing only one precursor for every MS/MS scan in Targeted MS/MS",
                // if more than one match at this m/z value return a list

                double minMzDelta = double.MaxValue;
                double mzDeltaEpsilon = Math.Min(_instrument.MzMatchTolerance, .0001);

                // Isolation width for single is based on the instrument m/z match tolerance
                var isoTargMz = isoWin.IsolationMz.Value;
                var isoWinSingle = new IsolationWindowFilter(isoTargMz, _instrument.MzMatchTolerance * 2);

                foreach (var filterPair in FindFilterPairs(isoWinSingle, FullScanAcquisitionMethod.DIA, true))
                {
                    double mzDelta = Math.Abs(isoTargMz - filterPair.Q1);
                    if (mzDelta < minMzDelta) // new best match
                    {
                        minMzDelta = mzDelta;
                        // are any existing matches no longer within epsilion of new best match?
                        for (int n = filterPairs.Count; n-- > 0; )
                        {
                            if ((Math.Abs(isoTargMz - filterPairs[n].Q1) - minMzDelta) > mzDeltaEpsilon)
                            {
                                filterPairs.RemoveAt(n);  // no longer a match by our new standard
                            }
                        }
                        filterPairs.Add(filterPair);
                    }
                    else if ((mzDelta - minMzDelta) <= mzDeltaEpsilon)
                    {
                        filterPairs.Add(filterPair);  // not the best, but close to it
                    }
                }
            }

            _filterPairDictionary[isoWinKey] = filterPairs;
            return filterPairs;
        }

        public void CalcDiaIsolationValues(ref SignedMz isolationTargetMz,
                                            ref double? isolationWidth)
        {
            double isolationWidthValue;
            var isolationScheme = _fullScan.IsolationScheme;
            if (isolationScheme == null)
            {                
                throw new InvalidOperationException("Unexpected attempt to calculate DIA isolation window without an isolation scheme"); // Not L10N - for developers
            }

                // Calculate window for a simple isolation scheme.
            else if (isolationScheme.PrecursorFilter.HasValue && !isolationScheme.UseMargin)
            {
                // Use the user specified isolation width, unless it is larger than
                // the acquisition isolation width.  In this case the chromatograms
                // may be very confusing (spikey), because of incorrectly included
                // data points.
                isolationWidthValue = isolationScheme.PrecursorFilter.Value +
                    (isolationScheme.PrecursorRightFilter ?? 0);
                if (isolationWidth.HasValue && isolationWidth.Value < isolationWidthValue)
                    isolationWidthValue = isolationWidth.Value;

                // Make sure the isolation target is centered in the desired window, even
                // if the window was specified as being asymetric
                if (isolationScheme.PrecursorRightFilter.HasValue)
                    isolationTargetMz += isolationScheme.PrecursorRightFilter.Value - isolationWidthValue/2;
            }

                // Find isolation window.
            else if (isolationScheme.PrespecifiedIsolationWindows.Count > 0)
            {
                var isolationWindow = isolationScheme.GetIsolationWindow(isolationTargetMz, _instrument.MzMatchTolerance);
                if (isolationWindow == null)
                {
                    _filterPairDictionary[new IsolationWindowFilter(isolationTargetMz, isolationWidth)] = new List<SpectrumFilterPair>();
                    isolationWidth = null;
                    return;
                }

                isolationWidthValue = isolationWindow.End - isolationWindow.Start;
                isolationTargetMz = isolationTargetMz.ChangeMz(isolationWindow.Start + isolationWidthValue / 2);
            }

                // Use the instrument isolation window
            else if (isolationWidth.HasValue)
            {
                isolationWidthValue = isolationWidth.Value - (isolationScheme.PrecursorFilter ?? 0)*2;
            }

                // No defined isolation scheme?
            else
            {
                throw new InvalidDataException(string.Format(Resources.SpectrumFilter_CalcDiaIsolationValues_Unable_to_determine_isolation_width_for_the_scan_targeted_at__0_, isolationTargetMz));
            }
            isolationWidth = isolationWidthValue;
        }

        private IEnumerable<SpectrumFilterPair> FindMs1FilterPairs(IList<MsPrecursor> precursors)
        {
            if (precursors.Count > 1)
                return FindSimFilterPairs(precursors);  // SIM scans
            var isoWin = GetIsolationWindows(precursors).FirstOrDefault();
            if (!IsSimIsolation(isoWin))
                return _filterMzValues; // survey scan
            return FindFilterPairs(isoWin, FullScanAcquisitionMethod.DIA, true);  // SIM scan
        }

        private IEnumerable<SpectrumFilterPair> FindSimFilterPairs(IList<MsPrecursor> precursors)
        {
            return GetIsolationWindows(precursors).SelectMany(isoWin =>
                FindFilterPairs(isoWin, FullScanAcquisitionMethod.DIA, true));  // SIM scan
        }


        public SpectrumFilterPair[] RemoveFinishedFilterPairs(float retentionTime)
        {
            int startIndex = _retentionTimeIndex;
            if (retentionTime < 0)
                _retentionTimeIndex = _filterRTValues.Length;
            else if (!IsTimeOrderedExtraction)
            {
                // Wait until the end to release chromatograms, when we can't be sure
                // that all chromatograms for a peptide have been seen just because a
                // spectrum with a later time has been seen.
                _retentionTimeIndex = 0;
            }
            else
            {
                while (_retentionTimeIndex < _filterRTValues.Length)
                {
                    var maxTime = _filterRTValues[_retentionTimeIndex].MaxTime;
                    if (!maxTime.HasValue || maxTime >= retentionTime)  // Subsequent spectra are allowed to have the same time
                        break;
                    _retentionTimeIndex++;
                }
            }
            var donePairs = new SpectrumFilterPair[_retentionTimeIndex - startIndex];
            for (int i = 0; i < donePairs.Length; i++)
                donePairs[i] = _filterRTValues[startIndex++];
            return donePairs;
        }

        private int IndexOfFilter(SignedMz precursorMz, double window)
        {
            return IndexOfFilter(precursorMz, window, 0, _filterMzValues.Length - 1);
        }

        private int IndexOfFilter(SignedMz precursorMz, double window, int left, int right)
        {
            // Binary search for the right precursorMz
            if (left > right)
                return -1;
            int mid = (left + right) / 2;
            int compare = CompareMz(precursorMz, _filterMzValues[mid].Q1, window);
            if (compare < 0)
                return IndexOfFilter(precursorMz, window, left, mid - 1);
            if (compare > 0)
                return IndexOfFilter(precursorMz, window, mid + 1, right);
            
            // Scan backward until the first matching element is found.
            while (mid > 0 && CompareMz(precursorMz, _filterMzValues[mid - 1].Q1, window) == 0)
                mid--;

            return mid;
        }

        private static int CompareMz(SignedMz mz1, SignedMz mz2, double window)
        {
            return mz1.CompareTolerant(mz2, 0.5 * window);
        }

        public ChromatogramRequestDocument ToChromatogramRequestDocument()
        {
            var document = new ChromatogramRequestDocument
            {
                MaxMz = _instrument.MaxMz,
                MinMz = _instrument.MinMz,
                MzMatchTolerance = _instrument.MzMatchTolerance,
            };
            if (_minTime.HasValue)
            {
                document.MinTime = _minTime.Value;
                document.MinTimeSpecified = true;
            }
            if (_maxTime.HasValue)
            {
                document.MaxTime = _maxTime.Value;
                document.MaxTimeSpecified = true;
            }
            switch (_acquisitionMethod)
            {
                case FullScanAcquisitionMethod.DIA:
                    document.Ms2FullScanAcquisitionMethod = Ms2FullScanAcquisitionMethod.DIA;
                    break;
                case FullScanAcquisitionMethod.None:
                    document.Ms2FullScanAcquisitionMethod = Ms2FullScanAcquisitionMethod.None;
                    break;
                case FullScanAcquisitionMethod.Targeted:
                    document.Ms2FullScanAcquisitionMethod = Ms2FullScanAcquisitionMethod.Targeted;
                    break;
            }

            if (null != _filterMzValues)
            {
                var chromatogramGroups = new List<ChromatogramRequestDocumentChromatogramGroup>();
                var sources = new HashSet<RemoteApi.GeneratedCode.ChromSource>();
                if (EnabledMs)
                {
                    sources.Add(RemoteApi.GeneratedCode.ChromSource.Ms1);
                }
                if (EnabledMsMs)
                {
                    sources.Add(RemoteApi.GeneratedCode.ChromSource.Ms2);
                }
                foreach (var filterPair in _filterMzValues)
                {
                    foreach (var chromatogramGroup in filterPair.ToChromatogramRequestDocumentChromatogramGroups())
                    {
                        if (chromatogramGroup.PrecursorMz == 0 || sources.Contains(chromatogramGroup.Source))
                        {
                            chromatogramGroups.Add(chromatogramGroup);
                        }
                    }
                }
                document.ChromatogramGroup = chromatogramGroups.ToArray();
            }
            var isolationScheme = _fullScan.IsolationScheme;
            if (null != _fullScan.IsolationScheme)
            {
                document.IsolationScheme = new ChromatogramRequestDocumentIsolationScheme();
                if (_fullScan.IsolationScheme.PrecursorFilter.HasValue)
                {
                    document.IsolationScheme.PrecursorFilter = _fullScan.IsolationScheme.PrecursorFilter.Value;
                    document.IsolationScheme.PrecursorFilterSpecified = true;
                }
                if (isolationScheme.PrecursorRightFilter.HasValue)
                {
                    document.IsolationScheme.PrecursorRightFilter = isolationScheme.PrecursorRightFilter.Value;
                    document.IsolationScheme.PrecursorRightFilterSpecified = true;
                }
                if (null != isolationScheme.SpecialHandling)
                {
                    document.IsolationScheme.SpecialHandling = isolationScheme.SpecialHandling;
                }
                if (isolationScheme.WindowsPerScan.HasValue)
                {
                    document.IsolationScheme.WindowsPerScan = isolationScheme.WindowsPerScan.Value;
                    document.IsolationScheme.WindowsPerScanSpecified = true;
                }
                document.IsolationScheme.IsolationWindow =
                    isolationScheme.PrespecifiedIsolationWindows.Select(
                        isolationWindow =>
                        {
                            var result = new ChromatogramRequestDocumentIsolationSchemeIsolationWindow
                            {
                                Start = isolationWindow.Start,
                                End = isolationWindow.End,
                            };
                            if (isolationWindow.Target.HasValue)
                            {
                                result.Target = isolationWindow.Target.Value;
                            }
                            if (isolationWindow.StartMargin.HasValue)
                            {
                                result.StartMargin = isolationWindow.StartMargin.Value;
                            }
                            if (isolationWindow.EndMargin.HasValue)
                            {
                                result.EndMargin = isolationWindow.EndMargin.Value;
                            }
                            return result;
                        }).ToArray();
            }
            return document;
        }

        public string ToChromatogramRequestDocumentXml()
        {
            var xmlSerializer = new XmlSerializer(typeof(ChromatogramRequestDocument));
            StringWriter stringWriter = new StringWriter();
            xmlSerializer.Serialize(stringWriter, ToChromatogramRequestDocument());
            return stringWriter.ToString();
        }

    }
}