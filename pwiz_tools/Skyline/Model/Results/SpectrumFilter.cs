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
using System.Text;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public interface IFilterInstrumentInfo : IIonMobilityFunctionsProvider
    {
        bool IsWatersFile { get; }
        bool IsAgilentFile { get; }
        IEnumerable<MsInstrumentConfigInfo> ConfigInfoList { get; }
        bool HasDeclaredMSnSpectra { get; }
    }

    public interface IIonMobilityFunctionsProvider
    {
        bool ProvidesCollisionalCrossSectionConverter { get; }
        eIonMobilityUnits IonMobilityUnits { get; } // Reports ion mobility units in use by the mass spec
        bool HasCombinedIonMobility { get; } // When true, data source provides IMS data in 3-array format (mz, intensity, im), which affects spectrum ID format
        bool IsCombinedDiagonalPASEF { get; } // When true, data source provides IMS data in 6-array format (mz, intensity, im, isoLow, isoHigh, CE) which affects spectrum ID format
        IonMobilityValue IonMobilityFromCCS(double ccs, double mz, int charge, object obj); // Convert from Collisional Cross Section to ion mobility
        double CCSFromIonMobility(IonMobilityValue im, double mz, int charge, object obj); // Convert from ion mobility to Collisional Cross Section
        bool IsWatersSonarData { get; } // Returns true if this ion mobility data is actually Waters SONAR data, which filters on precursor mz 
        Tuple<int, int> SonarMzToBinRange(double mz, double tolerance); // Only useful for Waters SONAR data
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
        private List<Tuple<double,double>> _rangeFilterPairsIM; // List of active ranges of IonMobility filter values across FilterPairs 
        private readonly SpectrumFilterPair[] _filterMzValues;
        private readonly Dictionary<double,SpectrumFilterPair[]> _filterMzValuesFAIMSDict; // FAIMS chromatogram extraction is a special case for non-contiguous scans
//        private readonly Dictionary<double, Dictionary<double, bool>> _filterMobilityPrecursors; // TODO map showing which IM by Mz windows are useful and not useful
        private readonly SpectrumFilterPair[] _filterRTValues;
        private readonly ChromKey[] _productChromKeys;
        private int _retentionTimeIndex;
        private readonly bool _isWatersFile;
        private readonly bool _isWatersSonar;
        private readonly bool _isWatersMse;
        private readonly bool _isAgilentMse;
        private readonly bool _isIonMobilityFiltered;
        private readonly bool _isElectronIonizationMse; // All ions, data MS1 only, but produces just fragments
        private readonly IEnumerable<MsInstrumentConfigInfo> _configInfoList;
        private readonly IIonMobilityFunctionsProvider _ionMobilityFunctionsProvider;
        private int _mseLevel;
        private MsDataSpectrum _mseLastSpectrum;
        private int _mseLastSpectrumLevel; // for averaging Agilent stepped CE spectra
        private bool _sourceHasDeclaredMSnSpectra; // Used in all-ions mode to discern low and high energy scans for Bruker

        private static readonly PrecursorTextId TIC_KEY = new PrecursorTextId(SignedMz.ZERO, null, null, null, null, ChromExtractor.summed);
        private static readonly PrecursorTextId BPC_KEY = new PrecursorTextId(SignedMz.ZERO, null, null, null, null, ChromExtractor.base_peak);

        public IEnumerable<SpectrumFilterPair> FilterPairs { get { return _filterMzValues; } }
        public bool HasRangeRT { get; private set; }

        public SpectrumFilter(SrmDocument document, MsDataFileUri msDataFileUri, IFilterInstrumentInfo instrumentInfo,
            OptimizableRegression optimization = null, double? maxObservedIonMobilityValue = null,
            IRetentionTimePredictor retentionTimePredictor = null, bool firstPass = false, GlobalChromatogramExtractor gce = null)
        {
            _fullScan = document.Settings.TransitionSettings.FullScan;
            _instrument = document.Settings.TransitionSettings.Instrument;
            _acquisitionMethod = _fullScan.AcquisitionMethod;
            _ionMobilityFunctionsProvider = instrumentInfo;
            if (instrumentInfo != null)
            {
                _isWatersFile = instrumentInfo.IsWatersFile;
                _isWatersSonar = instrumentInfo.IsWatersSonarData;
                _configInfoList = instrumentInfo.ConfigInfoList;
                _sourceHasDeclaredMSnSpectra = instrumentInfo.HasDeclaredMSnSpectra;
            }
            IsFirstPass = firstPass;

            var comparer = PrecursorTextId.PrecursorTextIdComparerInstance;
            var dictPrecursorMzToFilter = new SortedDictionary<PrecursorTextId, SpectrumFilterPair>(comparer);

            var moleculesThisPass = (retentionTimePredictor == null || !firstPass
                ? document.Molecules
                : document.Molecules.Where(p => retentionTimePredictor.IsFirstPassPeptide(p))).ToArray();
            
            // If we're using bare measured ion mobility values from spectral libraries, go get those now
            // TODO(bspratt): Should be queried out of the libraries as needed, as with RT not bulk copied all at once
            var libraryIonMobilityInfo = _isWatersSonar ? null : document.Settings.GetIonMobilities(moleculesThisPass.SelectMany(
                    node => node.TransitionGroups.Select(nodeGroup => nodeGroup.GetLibKey(document.Settings, node))).ToArray(), msDataFileUri);
            var ionMobilityMax = maxObservedIonMobilityValue ?? 0;

            // TIC and Base peak are meaningless with FAIMS, where we can't know the actual overall ion counts -also can't reliably share times with any ion mobility scheme
            if (instrumentInfo != null && instrumentInfo.IonMobilityUnits != eIonMobilityUnits.none)
            {
                if ((libraryIonMobilityInfo != null && !libraryIonMobilityInfo.IsEmpty) || _isWatersSonar)
                {
                    _isIonMobilityFiltered = true;
                }
                else
                {
                    foreach (var pair in moleculesThisPass.SelectMany(
                        node => node.TransitionGroups.Select(nodeGroup => new PeptidePrecursorPair(node, nodeGroup))))
                    {
                        var ionMobility = document.Settings.GetIonMobilityFilter(
                            pair.NodePep, pair.NodeGroup, null, libraryIonMobilityInfo, _ionMobilityFunctionsProvider, ionMobilityMax);
                        _isIonMobilityFiltered = ionMobility.HasIonMobilityValue;
                        if (_isIonMobilityFiltered)
                        {
                            break;
                        }
                    }
                }
            }

            if (EnabledMs || EnabledMsMs)
            {
                if (EnabledMs)
                {
                    _isHighAccMsFilter = !Equals(_fullScan.PrecursorMassAnalyzer,
                        FullScanMassAnalyzerType.qit);

                    if (!firstPass && !_isIonMobilityFiltered)
                    {
                        if (gce?.TicChromatogramIndex == null)
                        {
                            var key = TIC_KEY;
                            dictPrecursorMzToFilter.Add(key, new SpectrumFilterPair(key, PeptideDocNode.UNKNOWN_COLOR, dictPrecursorMzToFilter.Count,
                                _instrument.MinTime, _instrument.MaxTime, _isHighAccMsFilter, _isHighAccProductFilter));
                            /*
                             Leaving this here in case we ever decide to fall back to our own BPC extraction in cases where data
                             file doesn't have them ready to go, as in mzXML
                            key = BPC_KEY;
                            dictPrecursorMzToFilter.Add(key, new SpectrumFilterPair(key, PeptideDocNode.UNKNOWN_COLOR, dictPrecursorMzToFilter.Count,
                                _instrument.MinTime, _instrument.MaxTime, _isHighAccMsFilter, _isHighAccProductFilter));
                            */
                        }
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
                            _isElectronIonizationMse = instrumentInfo.ConfigInfoList != null &&
                                   instrumentInfo.ConfigInfoList.Any(c => @"electron ionization".Equals(c.Ionization));
                        }
                        _mseLevel =  _isElectronIonizationMse ? 2 : 1; // Electron ionization produces fragments only
                    }
                }

                Func<double, double> calcWindowsQ1 = _fullScan.GetPrecursorFilterWindow;
                Func<double, double> calcWindowsQ3 = _fullScan.GetProductFilterWindow;
                _minTime = _instrument.MinTime;
                _maxTime = _instrument.MaxTime;
                bool canSchedule = !firstPass && CanSchedule(document, retentionTimePredictor);
                // TODO: Figure out a way to turn off time sharing on first SIM scan so that
                //       times can be shared for MS1 without SIM scans
                _isSharedTime = !canSchedule && !_isIonMobilityFiltered && 
                                document.MoleculeTransitionGroups.All(transitionGroup=>transitionGroup.SpectrumClassFilter.IsEmpty);

                var ceSteps = Equals(optimization?.OptType, OptimizationType.collision_energy)
                    ? Enumerable.Range(-optimization.StepCount, optimization.StepCount * 2 + 1).Cast<int?>().ToArray()
                    : new[] { (int?)null };

                int filterCount = 0;
                foreach (var nodePep in moleculesThisPass)
                {
                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                    {
                        if (nodeGroup.Children.Count == 0)
                            continue;

                        double? minTime = _minTime, maxTime = _maxTime;
                        var ionMobilityFilter = document.Settings.GetIonMobilityFilter(
                            nodePep, nodeGroup, null, libraryIonMobilityInfo, _ionMobilityFunctionsProvider, ionMobilityMax);

                        if (ionMobilityFilter.IonMobilityUnits == eIonMobilityUnits.waters_sonar &&
                            (ionMobilityFilter.IonMobility.Mobility??0) + .5*(ionMobilityFilter?.IonMobilityExtractionWindowWidth??0) < 0)
                        {
                            continue; // This precursor is outside the SONAR quadrupole range
                        }

                        ExplicitPeakBounds peakBoundaries = null;
                        if (_fullScan.RetentionTimeFilterType != RetentionTimeFilterType.none)
                        {
                            peakBoundaries = document.Settings.GetExplicitPeakBounds(nodePep, msDataFileUri);
                            if (peakBoundaries != null && !peakBoundaries.IsEmpty)
                            {
                                minTime = peakBoundaries.StartTime - _fullScan.RetentionTimeFilterLength;
                                maxTime = peakBoundaries.EndTime + _fullScan.RetentionTimeFilterLength;
                                _isSharedTime = false;
                            }
                        }
                        if (canSchedule && (peakBoundaries == null || peakBoundaries.IsEmpty))
                        {
                            if (RetentionTimeFilterType.scheduling_windows == _fullScan.RetentionTimeFilterType)
                            {
                                double? centerTime = null;
                                var windowRT = new PeptidePrediction.WindowRT(0, false);
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
                                    windowRT.Window = _fullScan.RetentionTimeFilterLength * 2;
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

                        var mz = new SignedMz(nodeGroup.PrecursorMz, nodeGroup.PrecursorCharge < 0);

                        foreach (var step in ceSteps)
                        {
                            var ce = step.HasValue
                                ? document.GetCollisionEnergy(nodePep, nodeGroup, null, step.Value)
                                : (double?)null;
                            var key = new PrecursorTextId(mz, step, ce, ionMobilityFilter,
                                ChromatogramGroupId.ForPeptide(nodePep, nodeGroup), ChromExtractor.summed);

                            if (!dictPrecursorMzToFilter.TryGetValue(key, out var filter))
                            {
                                filter = new SpectrumFilterPair(key, nodePep.Color, dictPrecursorMzToFilter.Count, minTime, maxTime,
                                    _isHighAccMsFilter, _isHighAccProductFilter);
                                if (_instrument.TriggeredAcquisition)
                                {
                                    filter.ScanDescriptionFilter = GetSureQuantScanDescription(document.Settings, nodePep, nodeGroup);
                                }
                                dictPrecursorMzToFilter.Add(key, filter);
                            }

                            if (EnabledMs)
                            {
                                filterCount += filter.AddQ1FilterValues(GetMS1MzValues(nodeGroup), calcWindowsQ1);
                            }

                            if (EnabledMsMs)
                            {
                                var transitions = EnabledMs
                                    ? nodeGroup.Transitions.Where(nodeTran => !nodeTran.IsMs1)
                                    : nodeGroup.Transitions;

                                var values = transitions.Select(nodeTran => new SpectrumFilterValues(nodeTran.Mz,
                                    nodeTran.ExplicitValues.IonMobilityHighEnergyOffset ?? ionMobilityFilter.HighEnergyIonMobilityOffset ?? 0));

                                filterCount += filter.AddQ3FilterValues(values, calcWindowsQ3);
                            }
                        }
                    }
                }

                HasFullGradientMs1Filters = dictPrecursorMzToFilter.ContainsKey(TIC_KEY) ||
                                            dictPrecursorMzToFilter.ContainsKey(BPC_KEY);

                _filterMzValues = dictPrecursorMzToFilter.Values.ToArray();
                HasIonMobilityFiltering = _filterMzValues.Any(f => f.MinIonMobilityValue.HasValue);

                // For FAIMS chromatogram extraction is a special case for non-contiguous scans, so create convenient subsets of filters
                foreach (var cv in _filterMzValues.Where(f => f.HasIonMobilityFAIMS())
                    .Select(f => f.GetIonMobilityWindow().IonMobility.Mobility.Value).Distinct())
                {
                    if (_filterMzValuesFAIMSDict == null)
                    {
                        _filterMzValuesFAIMSDict = new Dictionary<double, SpectrumFilterPair[]>();
                    }
                    var filterCV = _filterMzValues.Where(f =>
                        !f.HasIonMobilityFAIMS() || // TIC, base peak
                        Equals(cv, f.GetIonMobilityWindow().IonMobility.Mobility.Value))
                        .ToArray();
                    _filterMzValuesFAIMSDict.Add(cv, filterCV);
                }

                var listChromKeyFilterIds = new List<ChromKey>(filterCount);
                foreach (var spectrumFilterPair in _filterMzValues)
                {
                    spectrumFilterPair.AddChromKeys(listChromKeyFilterIds);
                }

                if (gce != null)
                {
                    listChromKeyFilterIds.AddRange(gce.ListChromKeys());
                }

                _productChromKeys = listChromKeyFilterIds.ToArray();

                // Sort a copy of the filter pairs by maximum retention time so that we can detect when
                // filters are no longer active.
                _filterRTValues = new SpectrumFilterPair[_filterMzValues.Length];
                Array.Copy(_filterMzValues, _filterRTValues, _filterMzValues.Length);
                Array.Sort(_filterRTValues, CompareByRT);

                // If we have DIA-PASEF window group table, use it to prepopulate BestWindowGroup values
                var table = _configInfoList?.FirstOrDefault(c => c.DiaFrameMsMsWindows != null)?.DiaFrameMsMsWindows;

                if (table != null)
                {
                    var imRangeNormalize =
                        table.Values.SelectMany(value => value.Select(diaFrameMsMsWindowInfo => diaFrameMsMsWindowInfo.ImHigh)).Max() -
                        table.Values.SelectMany(value => value.Select(diaFrameMsMsWindowInfo => diaFrameMsMsWindowInfo.ImLow)).Min();
                    if (imRangeNormalize == 0)
                    {
                        imRangeNormalize = 1;
                    }
                    else
                    {
                        imRangeNormalize = 1.0 / imRangeNormalize; // Normalize to 1.0 for distance calculations
                    }
                    var isoRangeNormalize =
                        table.Values.SelectMany(value => value.Select(diaFrameMsMsWindowInfo => diaFrameMsMsWindowInfo.IsoMzHigh)).Max() -
                        table.Values.SelectMany(value => value.Select(diaFrameMsMsWindowInfo => diaFrameMsMsWindowInfo.IsoMzLow)).Min();
                    if (isoRangeNormalize == 0)
                    {
                        isoRangeNormalize = 1;
                    }
                    else
                    {
                        isoRangeNormalize = 1.0 / isoRangeNormalize; // Normalize to 1.0 for distance calculations
                    }
                    foreach (var filter in _filterMzValues)
                    {
                        var halfWidth = 0.5 * (filter.Ms1ProductFilters.Length > 2 ?
                            filter.Ms1ProductFilters[1].FilterWidth :
                            _fullScan.GetPrecursorFilterWindow(filter.Q1));
                        if (halfWidth == 0)
                            halfWidth = _instrument.MzMatchTolerance;
                        var mzFilterLow = filter.Q1 - halfWidth;
                        var mzFilterHigh = filter.Q1 + halfWidth;
                        var imFilterLow = filter.MinIonMobilityValue ?? double.MinValue;
                        var imFilterHigh = filter.MaxIonMobilityValue ?? double.MaxValue;
                        var bestNoOverlapDistance = double.MaxValue;
                        var bestNoOverlapWindowGroup = -1;
                        foreach (var kvp in table) // For each window group
                        {
                            var windowGroup = kvp.Key;
                            filter.SetIsKnownWindowGroup(windowGroup);
                            var overlapAreaTotal = 0.0;
                            var bestDistance = double.MaxValue;
                            var windowInfos = kvp.Value;
                            for (var i = 0; i < windowInfos.Count; i++)
                            {
                                var imLow = windowInfos[i].ImLow;
                                var imHigh = windowInfos[i].ImHigh;
                                if (imLow == imHigh) // As in DiagonalPASEF - need a nonzero IM width for area calc
                                {
                                    var imWidth = i== 0 ? (imLow-windowInfos[i + 1].ImLow) : (windowInfos[i - 1].ImLow - imLow);
                                    imLow -= imWidth / 2;
                                    imHigh += imWidth / 2;
                                }

                                var overlapArea = DiaPasefAwareFilter.CalcOverlapArea(windowInfos[i].IsoMzLow, windowInfos[i].IsoMzHigh, imLow, imHigh,
                                    mzFilterLow, mzFilterHigh, imFilterLow, imFilterHigh);
                                var distanceIM = filter.MinIonMobilityValue.HasValue ?
                                    DiaPasefAwareFilter.CalcCenter(imFilterLow, imFilterHigh) - DiaPasefAwareFilter.CalcCenter(imLow, imHigh) :
                                    0;
                                var distanceMz =
                                    filter.Q1 - DiaPasefAwareFilter.CalcCenter(windowInfos[i].IsoMzLow, windowInfos[i].IsoMzHigh);
                                var distance = DiaPasefAwareFilter.CalcHypotenuse(distanceIM*imRangeNormalize, distanceMz*isoRangeNormalize);
                                if (overlapArea > 0)
                                {
                                    bestDistance = Math.Min(bestDistance, distance);
                                    overlapAreaTotal += overlapArea;
                                }
                                else if (distance < bestNoOverlapDistance &&
                                         mzFilterLow < windowInfos[i].IsoMzHigh && mzFilterHigh > windowInfos[i].IsoMzLow)
                                {
                                    // Poor IM match but at least the mz isolation matches
                                    bestNoOverlapDistance = distance;
                                    bestNoOverlapWindowGroup = windowGroup;
                                }
                            }

                            filter.ProposeBestWindowGroup(windowGroup, overlapAreaTotal, bestDistance);
                        }

                        if (!filter.HasBestWindowGroup)
                        {
                            // No actual overlap in isolation,IM space, pick the one that's closest while at least matching mz
                            if (bestNoOverlapDistance == 0)
                            {
                                Console.WriteLine($@"No windowGroup area found for mz={filter.Q1} im={filter.GetIonMobilityWindow()}");
                            }
                            filter.ProposeBestWindowGroup(bestNoOverlapWindowGroup,0.0, bestNoOverlapDistance);
                        }
                    }
                }

            }

            InitIonMobilityAndRTLimits();
        }

        public bool ProvidesCollisionalCrossSectionConverter { get { return _ionMobilityFunctionsProvider != null;  } }

        public eIonMobilityUnits IonMobilityUnits
        {
            get
            {
                return ProvidesCollisionalCrossSectionConverter
                    ? _ionMobilityFunctionsProvider.IonMobilityUnits
                    : eIonMobilityUnits.none;
            }
        }

        public bool HasCombinedIonMobility
        {
            get
            {
                return ProvidesCollisionalCrossSectionConverter && _ionMobilityFunctionsProvider.HasCombinedIonMobility;
            }
        }

        public bool IsCombinedDiagonalPASEF
        {
            get
            {
                return _ionMobilityFunctionsProvider is { IsCombinedDiagonalPASEF: true };
            }
        }

        public IonMobilityValue IonMobilityFromCCS(double ccs, double mz, int charge, object obj)
        {
            if (ProvidesCollisionalCrossSectionConverter)
            {
                return _ionMobilityFunctionsProvider.IonMobilityFromCCS(ccs, mz, charge, obj);
            }
            Assume.IsNotNull(_ionMobilityFunctionsProvider, @"No CCS to ion mobility translation is possible for this data set");
            return IonMobilityValue.EMPTY;
        }

        public double CCSFromIonMobility(IonMobilityValue im, double mz, int charge, object obj)
        {
            if (ProvidesCollisionalCrossSectionConverter)
            {
                return _ionMobilityFunctionsProvider.CCSFromIonMobility(im, mz, charge, obj);
            }
            Assume.IsNotNull(_ionMobilityFunctionsProvider, @"No ion mobility to CCS translation is possible for this data set");
            return 0;
        }

        public bool IsWatersSonarData
        {
            get { return _isWatersSonar; }
        }

        public Tuple<int, int> SonarMzToBinRange(double mz, double tolerance)
        {
            if (_ionMobilityFunctionsProvider?.IsWatersSonarData ?? false)
            {
                return _ionMobilityFunctionsProvider.SonarMzToBinRange(mz, tolerance);
            }
            Assume.Fail(@"This is not Waters SONAR data");
            return new Tuple<int, int>(-1, -1);
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
        private void InitIonMobilityAndRTLimits()
        {
            _maxFilterPairsRT = double.MaxValue;
            _minFilterPairsRT = double.MinValue;
            HasRangeRT = false;
            _rangeFilterPairsIM = new List<Tuple<double, double>>();
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
                {
                    _maxFilterPairsRT = maxRT.Value;
                    HasRangeRT = true;
                }

                if (minRT.HasValue && !IsMseData()) // For MSe data, just start from the beginning lest we drop in mid-cycle
                    _minFilterPairsRT = minRT.Value;

                // Create a sorted list of active ion mobility windows. When windows overlap, combine them into a single window.
                foreach (var fp in FilterPairs.Where(fp=>fp.MaxIonMobilityValue.HasValue && fp.MinIonMobilityValue.HasValue))
                {
                    var imLow = fp.MinIonMobilityValue.Value;
                    var imHigh = fp.MaxIonMobilityValue.Value;
                    foreach (var offset in fp.Ms2ProductFilters.Select(fp2 => fp2.HighEnergyIonMobilityValueOffset))
                    {
                        imLow = Math.Min(imLow, imLow + offset);
                        imHigh = Math.Max(imHigh, imHigh + offset);
                    }
                    _rangeFilterPairsIM.Add(Tuple.Create(imLow, imHigh));
                }
                _rangeFilterPairsIM.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                for (var i = 0; i < _rangeFilterPairsIM.Count-1;)
                {
                    if (_rangeFilterPairsIM[i].Item2 >= _rangeFilterPairsIM[i + 1].Item1)
                    {
                        // Ranges overlap, combine them
                        _rangeFilterPairsIM[i] = Tuple.Create(_rangeFilterPairsIM[i].Item1, _rangeFilterPairsIM[i + 1].Item2);
                        _rangeFilterPairsIM.RemoveAt(i+1);
                    }
                    else
                    {
                        i++;
                    }
                }

                HasIonMobilityFilters = _rangeFilterPairsIM.Count > 0;
                if (HasIonMobilityFilters)
                {
                    _lowEndFilterPairsIM = _rangeFilterPairsIM[0].Item1;
                    _highEndFilterPairsIM = _rangeFilterPairsIM.Last().Item2;
                }
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
        public bool HasFullGradientMs1Filters { get; private set; }

        public bool IsFilteringFullGradientMs1
        {
            get { return !IsFirstPass && HasFullGradientMs1Filters; }
        }

        public bool IsWatersFile
        {
            get { return _isWatersFile; }
        }

        public bool IsWatersMse
        {
            get { return _isWatersMse; }
        }

        public bool IsElectronIonizationMse
        {
            get { return _isElectronIonizationMse; }
        }

        public bool IsAgilentFile
        {
            get { return _isAgilentMse; }
        }

        public bool HasDeclaredMSnSpectra
        {
            get { return _sourceHasDeclaredMSnSpectra; }
        }

        public IEnumerable<MsInstrumentConfigInfo> ConfigInfoList
        {
            get { return _configInfoList; }
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

        public bool HasIonMobilityFilters { get; private set; }

        private int _indexFilterPairsIM;
        private double _lowEndFilterPairsIM, _highEndFilterPairsIM;

        public bool IsOutsideIonMobilityRange(IonMobilityValue imCheck)
        {
            if (!HasIonMobilityFilters)
                return false; // No range to be outside of
            var imCheckMobility = imCheck.Mobility ?? 0.0;  // Consider empty or zero as meaning "unknown"
            if (imCheckMobility == 0.0)
                return false; // Can't reject an as-yet-unknown value
            if (_indexFilterPairsIM  >= 0 && 
                imCheckMobility >= _rangeFilterPairsIM[_indexFilterPairsIM].Item1 && imCheckMobility <= _rangeFilterPairsIM[_indexFilterPairsIM].Item2)
                return false; // Found in same window as previous
            else if (_indexFilterPairsIM < 0 &&
                     imCheckMobility > _rangeFilterPairsIM[~_indexFilterPairsIM - 1].Item2 && imCheckMobility < _rangeFilterPairsIM[~_indexFilterPairsIM].Item1)
                return false; // Fell between same two windows as previous
            else if (imCheckMobility < _lowEndFilterPairsIM || imCheckMobility > _highEndFilterPairsIM)
                return true;
            // Locate position, if any, of window in sorted list which contains imCheckMobility.
            // Will return negative index iff imCheckMobility is between windows.
            _indexFilterPairsIM = CollectionUtil.BinarySearch(_rangeFilterPairsIM, r =>
            {
                var val = r.Item1.CompareTo(imCheckMobility);
                if (val >= 0)
                    return val; // imCheckMobility is less than or equal to lower bound of this window
                return imCheckMobility <= r.Item2 ? 0 : -1;   // imCheckMobility is within window (0), or greater than upper bound bound of this window (-1)
            }, true);
            return _indexFilterPairsIM < 0; // A value < 0 means not found in any window
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

        public bool IsAllIons
        {
            get
            {
                return _acquisitionMethod == FullScanAcquisitionMethod.DIA &&
                       _fullScan.IsolationScheme.IsAllIons;
            }
        }

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

        public const int SIM_ISOLATION_CUTOFF = 500;

        private static bool IsSimIsolation(IsolationWindowFilter isoWin)
        {
            // Consider: Introduce a variable cut-off in the document settings
            return isoWin.IsolationMz.HasValue && isoWin.IsolationWidth.HasValue &&
                   isoWin.IsolationWidth.Value <= SIM_ISOLATION_CUTOFF;
        }

        public bool IsMsMsSpectrum(MsDataSpectrum dataSpectrum)
        {
            if (!EnabledMsMs)
                return false;
            _sourceHasDeclaredMSnSpectra |= (dataSpectrum.Level == 2);
            if (_mseLevel > 0)
                return UpdateMseLevel(dataSpectrum) == 2;
            return dataSpectrum.Level > 1;
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
                // Electron Ionization "MSe" is all MS1 data, but contains only fragments
                if (_isElectronIonizationMse)
                {
                    _mseLevel = 2; // EI data is all fragments
                    returnval = 2; // Report as MS2 even though its recorded as MS1
                }
                else if (_isAgilentMse)
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
                    // Bruker - Alternate between 1 and 2 if everything is declared as MS1, assume first is low energy
                    _mseLevel = _mseLastSpectrum == null || _sourceHasDeclaredMSnSpectra
                        ? dataSpectrum.Level  
                        : (_mseLevel % 2) + 1;
                    returnval = _mseLevel;
                }
                else
                {
                    // Waters - mse level 1 in raw data "function 1", mse level 2 in raw data "function 2", and "function 3" which we ignore (lockmass?)
                    _mseLevel = MsDataSpectrum.WatersFunctionNumberFromId(dataSpectrum.Id, HasCombinedIonMobility);
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

        public bool PassesFilterFAIMS(MsDataSpectrum spectrum)
        {
            return _filterMzValuesFAIMSDict == null || // No FAIMS filtering, everything passes
                   !spectrum.IonMobility.Mobility.HasValue || // This spectrum is not ion mobility data
                   _filterMzValuesFAIMSDict.ContainsKey(spectrum.IonMobility.Mobility.Value);
        }

        public IEnumerable<ExtractedSpectrum> SrmSpectraFromMs1Scan(double? retentionTime,
                                                                    IList<MsPrecursor> precursors, MsDataSpectrum[] spectra)
        {
            if (!EnabledMs || !retentionTime.HasValue || spectra == null)
                yield break;

            // All filter pairs have a shot at filtering the MS1 scans
            var firstSpectrum = spectra.First();
            bool isSimSpectra = IsSimSpectrum(firstSpectrum, spectra);
            SpectrumFilterPair[] filterPairs;
            if (isSimSpectra)
            {
                if (_fullScan.IgnoreSimScans)
                    yield break;

                filterPairs = FindMs1FilterPairs(precursors).ToArray();
            }
            else
            {
                if (_filterMzValuesFAIMSDict != null && firstSpectrum.IonMobility.HasValue)
                {
                    // For FAIMS use only filters that match the CV
                    if (!_filterMzValuesFAIMSDict.TryGetValue(firstSpectrum.IonMobility.Mobility.Value, out filterPairs))
                    {
                        yield break;
                    }
                }
                else
                {
                    filterPairs = _filterMzValues;
                }
            }
            foreach (var filterPair in filterPairs)
            {
                if (!filterPair.ContainsRetentionTime(retentionTime.Value))
                    continue;
                var matchingSpectra = spectra;
                if (false == filterPair.SpectrumClassFilter.IsEmpty)
                {
                    matchingSpectra = spectra.Where(spectrum => filterPair.MatchesSpectrum(spectrum.Metadata))
                        .ToArray();
                    if (matchingSpectra.Length == 0)
                    {
                        continue;
                    }
                }

                var filteredSrmSpectrum = filterPair.FilterQ1SpectrumList(matchingSpectra, isSimSpectra);
                if (filteredSrmSpectrum != null)
                    yield return filteredSrmSpectrum;
            }
        }

        public IEnumerable<ExtractedSpectrum> Extract(double? retentionTime, MsDataSpectrum[] allSpectra)
        {
            if (!EnabledMsMs || !retentionTime.HasValue || !allSpectra.Any())
                yield break;

            // Note that for Agilent ramped CE the list allSpectra will have varying RT and isolation window, but we will
            // treat that as all having the same RT 
            var rampedCE = allSpectra.Any(s => s.RetentionTime != allSpectra[0].RetentionTime);

            var handlingType = _fullScan.IsolationScheme == null || _fullScan.IsolationScheme.SpecialHandling == null
                ? IsolationScheme.SpecialHandlingType.NONE
                : _fullScan.IsolationScheme.SpecialHandling;
            bool ignoreIso = handlingType == IsolationScheme.SpecialHandlingType.OVERLAP ||
                             handlingType == IsolationScheme.SpecialHandlingType.OVERLAP_MULTIPLEXED ||
                             handlingType == IsolationScheme.SpecialHandlingType.FAST_OVERLAP;

            var groupedByIsoWin = (allSpectra.Length <= 1) ? new[] { allSpectra }:
                IsCombinedDiagonalPASEF ?
                    allSpectra.Select(item => new [] { item }).ToArray() : // Diagonal PASEF - each scan has unique isolation window and single ion mobility
                    allSpectra.GroupBy(item => item.Precursors).Select(group => group.ToArray()).ToArray();

            var multiIso = groupedByIsoWin.Length > 1;
            var allFilters = multiIso ? new List<DiaPasefAwareFilter>() : null;

            foreach (var spectra in groupedByIsoWin)
            {
                var pasefAwareFilter = new DiaPasefAwareFilter(spectra, _acquisitionMethod, rampedCE);
                var firstSpectrum = spectra.First();
                var windowGroup = firstSpectrum.WindowGroup;
                foreach (var isoWin in GetIsolationWindows(firstSpectrum.GetPrecursorsByMsLevel(1)))
                {
                    foreach (var filterPair in FindFilterPairs(isoWin, _acquisitionMethod, ignoreIso, windowGroup))
                    {
                        if (!filterPair.ContainsRetentionTime(retentionTime.Value))
                            continue;
                        if (pasefAwareFilter.PreFilter(filterPair, isoWin, firstSpectrum, allSpectra, _instrument.MzMatchTolerance))
                            continue;
                        if (filterPair.ScanDescriptionFilter != null)
                        {
                            if (firstSpectrum.ScanDescription != null &&
                                firstSpectrum.ScanDescription.StartsWith(SUREQUANT_SCAN_DESCRIPTION_PREFIX))
                            {
                                if (firstSpectrum.ScanDescription != filterPair.ScanDescriptionFilter)
                                {
                                    continue;
                                }
                            }
                        }

                        var matchingSpectra = spectra;
                        if (!filterPair.SpectrumClassFilter.IsEmpty)
                        {
                            matchingSpectra = spectra.Where(spectrum => filterPair.MatchesSpectrum(spectrum.Metadata))
                                .ToArray();
                            if (matchingSpectra.Length == 0)
                                continue;
                        }

                        // This line does the bulk of the work of pulling chromatogram points from spectra
                        var filteredSrmSpectrum = filterPair.FilterQ3SpectrumList(matchingSpectra, UseDriftTimeHighEnergyOffset());

                        filteredSrmSpectrum = pasefAwareFilter.Filter(filteredSrmSpectrum, filterPair, isoWin);
                        if (filteredSrmSpectrum != null)
                            yield return filteredSrmSpectrum;
                    }
                }

                if (multiIso)
                {
                    allFilters.Add(pasefAwareFilter);
                }
                else
                {
                    foreach (var accumulatedSpectrum in pasefAwareFilter.AccumulatedSpectra)
                    {
                        yield return accumulatedSpectrum;
                    }
                }
            }

            if (allFilters != null)
            {
                foreach (var es in DiaPasefAwareFilter.AccumulateSpectra(allFilters))
                {
                    yield return es;
                }
            }
        }

        private class DiaPasefAwareFilter
        {
            private readonly bool _isDiaPasef;
            private readonly int _windowGroup;
            private readonly Dictionary<SpectrumFilterPair, List<ExtractedSpectrum>> _filteredSpectra;
            private double? _imWinCenter;   // IMS window center for the next group of spectra to process

            public DiaPasefAwareFilter(MsDataSpectrum[] spectra, FullScanAcquisitionMethod acquisitionMethod, bool rampedCE)
            {
                var firstSpectrum = spectra.First();
                _windowGroup = firstSpectrum.WindowGroup;
                _isDiaPasef = acquisitionMethod == FullScanAcquisitionMethod.DIA && _windowGroup > 0;
                // Older more flexible way of doing things was to accumulate spectra when they lack
                // an ion mobility array
                if (_isDiaPasef)
                {
                    _imWinCenter = CalcCenter(firstSpectrum.IonMobility.Mobility,
                        spectra.Last().IonMobility.Mobility);

                    if (firstSpectrum.IonMobilities == null)
                    {
                        // For diaPASEF we will see the isolation window shift periodically as we cycle through ion mobilities - and we may see overlaps
                        _filteredSpectra = new Dictionary<SpectrumFilterPair, List<ExtractedSpectrum>>();
                    }
                }
                else if (rampedCE)
                {
                    _filteredSpectra = new Dictionary<SpectrumFilterPair, List<ExtractedSpectrum>>();
                }
            }

            public bool IsEmpty => _filteredSpectra == null || _filteredSpectra.Count == 0;
            public IEnumerable<SpectrumFilterPair> SpectrumFilterPairs => _filteredSpectra.Keys;

            public List<ExtractedSpectrum> GetExtractedSpectra(SpectrumFilterPair f) =>
                _filteredSpectra.TryGetValue(f, out var val) ? val : null;

            /// <summary>
            /// Handles calculating a distance function used to decide on the best diaPASEF window
            /// for a particular SpectrumFilterPair and deciding whether the spectra under consideration
            /// are from that window group.
            /// <returns>true if filter pair can be skipped for current diaPASEF window</returns>
            /// </summary>
            public bool PreFilter(SpectrumFilterPair filterPair, IsolationWindowFilter isoWin, MsDataSpectrum spectrum, MsDataSpectrum[] allSpectra, double mzToler)
            {
                if (!_isDiaPasef)
                    return false; // Window group isn't a thing for non-diaPASEF, we have no opinion

                // If this window group has been tested before, then filter if it is not the best
                if (filterPair.GetIsKnownWindowGroup(_windowGroup))
                {
                    return !filterPair.IsBestWindowGroup(_windowGroup);
                }

                // This window group has not been considered before, is it the best for this FilterPair?
                var overlapArea = 0.0;
                var distanceToCenter = double.MaxValue;
                var filterTolerMz = filterPair.Ms1ProductFilters?.FirstOrDefault()?.FilterWidth/2 ?? mzToler;
                var filterTargetIMCenter = CalcCenter(filterPair.MinIonMobilityValue, filterPair.MaxIonMobilityValue);
                if (allSpectra.Length > 1) // Need to look at the entire frame to decide if its windowGroup is suitable
                {
                    var maxMz = 0.0;
                    var minMz = double.MaxValue;
                    var maxIm = 0.0;
                    var minIm = double.MaxValue;
                    var imHalfWidthAverage = 0.0;
                    foreach (var spec in allSpectra)
                    {
                        var specImLow = spec.IonMobilityMeasurementRangeLow ?? spec.IonMobility.Mobility ?? 0;
                        var specImHigh = spec.IonMobilityMeasurementRangeHigh ?? spec.IonMobility.Mobility ?? 0;
                        if (specImHigh < filterPair.MinIonMobilityValue || specImLow > filterPair.MaxIonMobilityValue)
                        {
                            continue; // Guard against very close mz match with poor IM match - distance might look nice but its meaningless
                        }
                        if (specImLow == specImHigh)
                        {
                            // Need a range for area calculation
                            if (imHalfWidthAverage == 0)
                            {
                                imHalfWidthAverage = Math.Abs(.5 * ((allSpectra[0].IonMobility.Mobility??0) - (allSpectra[allSpectra.Length-1].IonMobility.Mobility??0))/(allSpectra.Length-1));
                            }
                            specImLow -= imHalfWidthAverage;
                            specImHigh += imHalfWidthAverage;
                        }
                        foreach (var p in spec.Precursors)
                        {
                            if (filterPair.Q1 + filterTolerMz < p.IsolationWindowBoundsLow ||
                                filterPair.Q1 - filterTolerMz > p.IsolationWindowBoundsHigh)
                                continue; // Guard against very close IM match with poor mz match - distance might look nice but its meaningless
                            maxMz = Math.Max(maxMz, p.IsolationWindowBoundsHigh);
                            minMz = Math.Min(minMz, p.IsolationWindowBoundsLow);
                            maxIm = Math.Max(maxIm, specImHigh);
                            minIm = Math.Min(minIm, specImLow);
                            overlapArea += CalcOverlapArea(p.IsolationWindowBoundsLow, p.IsolationWindowBoundsHigh, specImLow, specImHigh,
                                filterPair.Q1-filterTolerMz, filterPair.Q1+filterTolerMz, filterPair.MinIonMobilityValue??0, filterPair.MaxIonMobilityValue ?? double.MaxValue);
                        }
                    }

                    var centerIM = CalcCenter(minIm, maxIm);
                    var distanceIM = CalcCenter(filterPair.MinIonMobilityValue, filterPair.MaxIonMobilityValue) - centerIM;
                    var centerMz = CalcCenter(minMz, maxMz)??0;
                    var distanceMz = filterPair.Q1 - centerMz;
                    distanceToCenter = CalcHypotenuse(distanceIM, distanceMz);
                }
                else
                {
                    // DIA-PASEF - multiple 1/K0 for single isolation window
                    // Find the WindowGroup with the best overlap of the target's mz,IM search bounds
                    // In case of tie, pick the one most centered on the target
                    var specImLow = spectrum.IonMobilityMeasurementRangeLow ?? spectrum.IonMobility.Mobility ?? 0.0;
                    var specImHigh = spectrum.IonMobilityMeasurementRangeHigh ?? spectrum.IonMobility.Mobility ?? double.MaxValue;
                    overlapArea = CalcOverlapArea(filterPair.Q1 - filterTolerMz, filterPair.Q1 + filterTolerMz, filterPair.MinIonMobilityValue??0, filterPair.MaxIonMobilityValue??double.MaxValue,
                        isoWin.BoundsLow, isoWin.BoundsHigh, specImLow, specImHigh);
                    if (overlapArea > 0)
                    {
                        // At least a partial overlap in m/z and 1/K0
                        var distanceMz = isoWin.IsolationMz.Value - filterPair.Q1;
                        var imWinCenter = _imWinCenter ?? CalcCenter(spectrum.IonMobilityMeasurementRangeHigh, spectrum.IonMobilityMeasurementRangeLow);
                        var distanceIM = CalcDelta(imWinCenter, filterTargetIMCenter) ?? 0;
                        distanceToCenter = CalcHypotenuse(distanceMz, distanceIM);
                    }

                }

                // Filter the spectrum if this is not the best window group for this FilterPair
                return !filterPair. ProposeBestWindowGroup(_windowGroup, overlapArea, distanceToCenter);
            }

            public ExtractedSpectrum Filter(ExtractedSpectrum filteredSrmSpectrum, SpectrumFilterPair filterPair, IsolationWindowFilter isoWin)
            {
                if (_filteredSpectra == null || filteredSrmSpectrum == null)
                    return filteredSrmSpectrum;

                if (!_filteredSpectra.TryGetValue(filterPair, out var list))
                {
                    list = new List<ExtractedSpectrum>();
                    _filteredSpectra.Add(filterPair, list);
                }
                list.Add(filteredSrmSpectrum); // Accumulate for later

                return null;
            }

            public static double? CalcCenter(double? v1, double? v2)
            {
                if (v1.HasValue && v2.HasValue)
                    return (v1.Value + v2.Value) / 2;
                return null;
            }

            private double? CalcDelta(double? v1, double? v2)
            {
                if (v1.HasValue && v2.HasValue)
                    return v1.Value - v2.Value;
                return null;
            }

            public static double CalcHypotenuse(double? v1, double? v2)
            {
                return Math.Sqrt(((v1 * v1) ?? 0) + ((v2 * v2) ?? 0));
            }

            public static double CalcOverlapArea(
                double mzLow1, double mzHigh1, double imLow1, double imHigh1,
                double mzLow2, double mzHigh2, double imLow2, double imHigh2)
            {
                var mzOverlap = Math.Max(0.0, Math.Min(mzHigh1, mzHigh2) - Math.Max(mzLow1, mzLow2));
                var imOverlap = Math.Max(0.0, Math.Min(imHigh1, imHigh2) - Math.Max(imLow1, imLow2));
                return mzOverlap * imOverlap;
            }

            public IEnumerable<ExtractedSpectrum> AccumulatedSpectra
            {
                get
                {
                    if (_filteredSpectra == null)
                        yield break;

                    // We may have worked through several isolation windows all at the same retention time - sum their intensities.
                    foreach (var kvp in _filteredSpectra)
                    {
                        var firstSpectrum = kvp.Value.First();
                        int countSpectra = 1;
                        foreach (var nextSpectrum in kvp.Value.Skip(1))
                        {
                            for (var i = 0; i < firstSpectrum.Intensities.Length; i++)
                            {
                                firstSpectrum.Intensities[i] += nextSpectrum.Intensities[i];
                                firstSpectrum.MassErrors[i] += (nextSpectrum.MassErrors[i] - firstSpectrum.MassErrors[i])/
                                                               (countSpectra+1); // Adjust the running mean.
                            }

                            countSpectra++;
                        }
                        yield return firstSpectrum;
                    }
                }
            }

            public static IEnumerable<ExtractedSpectrum> AccumulateSpectra(IList<DiaPasefAwareFilter> filters)
            {
                // Diagonal PASEF - combine results across entire frame
                // We may have worked through several isolation windows all at the same retention time - sum their intensities.
                var activeFilters = filters.Where(f => !f.IsEmpty).ToArray();
                var spectrumFilterPairs = activeFilters.SelectMany(f => f.SpectrumFilterPairs).Distinct();
                foreach (var filterPair in spectrumFilterPairs)
                {
                    var extractedSpectra = activeFilters.Select(f => f.GetExtractedSpectra(filterPair)).Where(es => es != null).ToList();
                    var all = extractedSpectra.SelectMany(es => es).ToList();
                    var firstSpectrum = all.First();
                    var countSpectra = 1;
                    foreach (var nextSpectrum in all.Skip(1))
                    {
                        for (var i = 0; i < firstSpectrum.Intensities.Length; i++)
                        {
                            firstSpectrum.Intensities[i] += nextSpectrum.Intensities[i];
                            firstSpectrum.MassErrors[i] +=
                                (nextSpectrum.MassErrors[i] - firstSpectrum.MassErrors[i]) /
                                (countSpectra + 1); // Adjust the running mean.
                        }

                        countSpectra++;
                    }
                    yield return firstSpectrum;
                }
            }
        }

        private bool UseDriftTimeHighEnergyOffset()
        {
            return (_isWatersMse || _isAgilentMse) && _mseLevel > 1;
        }

        public bool HasProductFilterPairs(double? retentionTime, IList<MsPrecursor> precursors)
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
            // Agilent MSe high energy scans present varying isolation windows, but always with the same low end - we've traditionally ignored them since it's "all ions"
            // Bruker all ions PASEF makes creative use of isolation windows so we do want to look at those when available
            if (_mseLevel > 0 && (_isWatersMse || _isAgilentMse || precursors.All(p => p.IsolationMz == null)))
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

            public double BoundsLow => (IsolationMz ?? 0.0) - (IsolationWidth ?? 0) / 2;
            public double BoundsHigh => (IsolationMz ?? double.MaxValue) + (IsolationWidth ?? 0) / 2;

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

            public override string ToString()
            {
                return string.Format(@"start = {0}, end = {1}", IsolationMz - IsolationWidth/2, IsolationMz + IsolationWidth/2);
            }

            #endregion
        }

        // Has to be concurrent, because both the spectrum reader thread and chromatogram extraction thread use this
        private readonly IDictionary<IsolationWindowFilter, IList<SpectrumFilterPair>> _filterPairDictionary =
            new ConcurrentDictionary<IsolationWindowFilter, IList<SpectrumFilterPair>>();

        public bool HasIonMobilityFiltering { get; private set; }

        private IEnumerable<SpectrumFilterPair> FindFilterPairs(IsolationWindowFilter isoWin,
                                                                FullScanAcquisitionMethod acquisitionMethod, bool ignoreIsolationScheme = false, int windowGroup = -1)
        {
            if (!isoWin.IsolationMz.HasValue)
                return new SpectrumFilterPair[0]; // empty

            // Return cached value from dictionary if we've seen this target previously.
            var isoWinKey = isoWin;
            IList<SpectrumFilterPair> filterPairsCached;
            if (_filterPairDictionary.TryGetValue(isoWinKey, out filterPairsCached))
            {
                return (windowGroup <= 0) ? filterPairsCached : filterPairsCached.Where(s => !s.HasBestWindowGroup || s.IsBestWindowGroup(windowGroup));
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
            else if (acquisitionMethod == FullScanAcquisitionMethod.Targeted)
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
                        for (int n = filterPairs.Count; n-- > 0;)
                        {
                            if ((Math.Abs(isoTargMz - filterPairs[n].Q1) - minMzDelta) > mzDeltaEpsilon)
                            {
                                filterPairs.RemoveAt(n); // no longer a match by our new standard
                            }
                        }
                        filterPairs.Add(filterPair);
                    }
                    else if ((mzDelta - minMzDelta) <= mzDeltaEpsilon)
                    {
                        filterPairs.Add(filterPair); // not the best, but close to it
                    }
                }
            }
            else if (acquisitionMethod == FullScanAcquisitionMethod.DDA)
            {
                foreach (var filterPair in _filterMzValues)
                {
                    if (filterPair.MatchesDdaPrecursor(isoWinKey.IsolationMz.Value))
                    {
                        filterPairs.Add(filterPair);
                    }
                }
            }
            else // PRM or SureQuant
            {
                return FindFilterPairs(new IsolationWindowFilter(isoWin.IsolationMz, 2 * _instrument.MzMatchTolerance),
                    FullScanAcquisitionMethod.DIA, true);
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
                throw new InvalidOperationException(@"Unexpected attempt to calculate DIA isolation window without an isolation scheme"); // - for developers
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
            else if (isolationScheme.IsAllIons)
            {
                isolationWidthValue = Double.MaxValue;
            }
                // No defined isolation scheme?
            else
            {
                throw new InvalidDataException(string.Format(ResultsResources.SpectrumFilter_CalcDiaIsolationValues_Unable_to_determine_isolation_width_for_the_scan_targeted_at__0_, isolationTargetMz));
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

        public const string SUREQUANT_SCAN_DESCRIPTION_PREFIX = @"SQ_";
        /// <summary>
        /// Thermo SureQuant acquisition methods use the "Scan Description" field to disambiguate which analytes particular MS2 scans
        /// are intended for. These scan description values always start with "SQ_". Then they are followed by either "ENDO_" or "IS_" depending
        /// on whether the precursor was light (endogenous) or heavy (internal standard).
        /// Then, they are followed by the labeled amino acid, and a plus sign, and labeled amino acid mass delta.
        /// Then, "_" and the charge state.
        /// Here are some example scan description values:
        /// SQ_IS_R+10_2
        /// SQ_IS_K+8_2
        /// SQ_ENDO_K+8_2
        /// SQ_ENDO_R+10_2
        /// </summary>
        public static string GetSureQuantScanDescription(SrmSettings settings, PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode)
        {
            char? labeledAminoAcid = null;
            int labelMassDelta = 0;

            var lightSequence = ModifiedSequence.GetModifiedSequence(settings, peptideDocNode, IsotopeLabelType.light);
            if (lightSequence == null)
            {
                return null;
            }
            var lightModificationNames = lightSequence.GetModifications().Select(mod => mod.Name).ToHashSet();
            foreach (var labelType in settings.PeptideSettings.Modifications.GetHeavyModificationTypes())
            {
                var modifiedSequence = ModifiedSequence.GetModifiedSequence(settings, peptideDocNode, labelType);
                var isotopeModification = modifiedSequence.GetModifications()
                    .FirstOrDefault(mod => !lightModificationNames.Contains(mod.Name));
                if (isotopeModification != null)
                {
                    labeledAminoAcid = modifiedSequence.GetUnmodifiedSequence()[isotopeModification.IndexAA];
                    labelMassDelta = (int) Math.Round(isotopeModification.MonoisotopicMass);
                }
            }

            if (!labeledAminoAcid.HasValue)
            {
                return null;
            }

            StringBuilder stringBuilder = new StringBuilder(SUREQUANT_SCAN_DESCRIPTION_PREFIX);
            if (transitionGroupDocNode.IsLight)
            {
                stringBuilder.Append(@"ENDO_");
            }
            else
            {
                stringBuilder.Append(@"IS_");
            }

            stringBuilder.Append(labeledAminoAcid);
            if (labelMassDelta >= 0)
            {
                stringBuilder.Append(@"+");
            }

            stringBuilder.Append(labelMassDelta);
            stringBuilder.Append(@"_");
            stringBuilder.Append(transitionGroupDocNode.PrecursorCharge);
            return stringBuilder.ToString();
        }
    }
}
