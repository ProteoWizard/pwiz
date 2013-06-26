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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results
{
    public sealed class SpectrumFilter
    {
        private readonly TransitionFullScan _fullScan;
        private readonly TransitionInstrument _instrument;
        private readonly FullScanAcquisitionMethod _acquisitionMethod;
        private readonly bool _isHighAccMsFilter;
        private readonly bool _isHighAccProductFilter;
        private readonly bool _isSharedTime;
        private readonly double? _minTime;
        private readonly double? _maxTime;
        private readonly SpectrumFilterPair[] _filterMzValues;
        private readonly bool _isWatersMse;
        private int _mseLevel;
        private MsDataSpectrum _mseLastSpectrum;

        public IEnumerable<SpectrumFilterPair> FilterPairs { get { return _filterMzValues; } }

        public SpectrumFilter(SrmDocument document, MsDataFileImpl dataFile)
        {
            _fullScan = document.Settings.TransitionSettings.FullScan;
            _instrument = document.Settings.TransitionSettings.Instrument;
            _acquisitionMethod = _fullScan.AcquisitionMethod;

            var comparer = PrecursorModSeq.PrecursorModSeqComparerInstance;
            var dictPrecursorMzToFilter = new SortedDictionary<PrecursorModSeq, SpectrumFilterPair>(comparer);

            if (EnabledMs || EnabledMsMs)
            {
                if (EnabledMs)
                {
                    _isHighAccMsFilter = !Equals(_fullScan.PrecursorMassAnalyzer,
                        FullScanMassAnalyzerType.qit);

                    var key = new PrecursorModSeq(0, null, ChromExtractor.summed);  // TIC
                    dictPrecursorMzToFilter.Add(key, new SpectrumFilterPair(key, dictPrecursorMzToFilter.Count,
                        _instrument.MinTime, _instrument.MaxTime, _isHighAccMsFilter, _isHighAccProductFilter));
                    key = new PrecursorModSeq(0, null, ChromExtractor.base_peak);   // BPC
                    dictPrecursorMzToFilter.Add(key, new SpectrumFilterPair(key, dictPrecursorMzToFilter.Count,
                        _instrument.MinTime, _instrument.MaxTime, _isHighAccMsFilter, _isHighAccProductFilter));
                }
                if (EnabledMsMs)
                {
                    _isHighAccProductFilter = !Equals(_fullScan.ProductMassAnalyzer,
                        FullScanMassAnalyzerType.qit);

                    if (_fullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA &&
                        _fullScan.IsolationScheme.IsAllIons)
                    {
                        _isWatersMse = dataFile.IsWatersFile;
                        _mseLevel = 1;
                    }
                }

                Func<double, double> calcWindowsQ1 = _fullScan.GetPrecursorFilterWindow;
                Func<double, double> calcWindowsQ3 = _fullScan.GetProductFilterWindow;
                _minTime = _instrument.MinTime;
                _maxTime = _instrument.MaxTime;
                bool canSchedule;
                if (RetentionTimeFilterType.scheduling_windows == _fullScan.RetentionTimeFilterType)
                {
                    canSchedule = document.Settings.PeptideSettings.Prediction.CanSchedule(document, PeptidePrediction.SchedulingStrategy.any);
                }
                else if (RetentionTimeFilterType.ms2_ids == _fullScan.RetentionTimeFilterType)
                {
                    canSchedule = true;
                }
                else
                {
                    canSchedule = false;
                }
                // TODO: Figure out a way to turn off time sharing on first SIM scan so that
                //       times can be shared for MS1 without SIM scans
                _isSharedTime = !canSchedule;
                int? replicateNum = null;
                if (document.Settings.HasResults)
                    replicateNum = document.Settings.MeasuredResults.Chromatograms.Count - 1;
                var schedulingAlgorithm = replicateNum.HasValue
                    ? ExportSchedulingAlgorithm.Single
                    : ExportSchedulingAlgorithm.Average;
                foreach (var nodePep in document.Peptides)
                {
                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                    {
                        if (nodeGroup.Children.Count == 0)
                            continue;

                        double? minTime = _minTime, maxTime = _maxTime;
                        if (canSchedule)
                        {
                            if (RetentionTimeFilterType.scheduling_windows == _fullScan.RetentionTimeFilterType)
                            {
                                double windowRT;
                                double? centerTime = document.Settings.PeptideSettings.Prediction.PredictRetentionTime(
                                    document, nodePep, nodeGroup, replicateNum, schedulingAlgorithm, false, out windowRT);
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
                                var times = document.Settings.GetRetentionTimes(dataFile.FilePath,
                                    nodePep.Peptide.Sequence,
                                    nodePep.ExplicitMods);
                                if (times.Length == 0)
                                {
                                    times = document.Settings.GetAllRetentionTimes(dataFile.FilePath,
                                        nodePep.Peptide.Sequence,
                                        nodePep.ExplicitMods);
                                }
                                if (times.Length > 0)
                                {
                                    minTime = Math.Max(minTime ?? 0, times.Min() - _fullScan.RetentionTimeFilterLength);
                                    maxTime = Math.Min(maxTime ?? double.MaxValue, times.Max() + _fullScan.RetentionTimeFilterLength);
                                }
                            }
                        }

                        SpectrumFilterPair filter;
                        string seq = nodePep.ModifiedSequence;
                        double mz = nodeGroup.PrecursorMz;
                        var key = new PrecursorModSeq(mz, seq, ChromExtractor.summed);
                        if (!dictPrecursorMzToFilter.TryGetValue(key, out filter))
                        {
                            filter = new SpectrumFilterPair(key, dictPrecursorMzToFilter.Count, minTime, maxTime,
                                _isHighAccMsFilter, _isHighAccProductFilter);
                            dictPrecursorMzToFilter.Add(key, filter);
                        }

                        if (!EnabledMs)
                        {
                            filter.AddQ3FilterValues(from TransitionDocNode nodeTran in nodeGroup.Children
                                                     select nodeTran.Mz, calcWindowsQ3);
                        }
                        else if (!EnabledMsMs)
                        {
                            filter.AddQ1FilterValues(GetMS1MzValues(nodeGroup), calcWindowsQ1);
                        }
                        else
                        {
                            filter.AddQ1FilterValues(GetMS1MzValues(nodeGroup), calcWindowsQ1);
                            filter.AddQ3FilterValues(from TransitionDocNode nodeTran in nodeGroup.Children
                                                     where !IsMS1Precursor(nodeTran)
                                                     select nodeTran.Mz, calcWindowsQ3);
                        }
                    }
                }
                _filterMzValues = dictPrecursorMzToFilter.Values.ToArray();
            }
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

        private IEnumerable<double> GetMS1MzValues(TransitionGroupDocNode nodeGroup)
        {
            var isotopePeaks = nodeGroup.IsotopeDist;
            if (isotopePeaks == null)
            {
                // Return the MS1 transition m/z values, if the precursor has no isotope peaks
                foreach (var nodeTran in nodeGroup.Children.Cast<TransitionDocNode>().Where(IsMS1Precursor))
                    yield return nodeTran.Mz;
            }
            else
            {
                // Otherwise, return all possible isotope peaks
                for (int i = 0; i < isotopePeaks.CountPeaks; i++)
                    yield return isotopePeaks.GetMZI(isotopePeaks.PeakIndexToMassIndex(i));
            }
        }

        private bool IsMS1Precursor(TransitionDocNode nodeTran)
        {
            return Transition.IsPrecursor(nodeTran.Transition.IonType) && !nodeTran.HasLoss;
        }

        public bool EnabledMs { get { return _fullScan.PrecursorIsotopes != FullScanPrecursorIsotopes.None; } }
        public bool IsHighAccMsFilter { get { return _isHighAccMsFilter; } }
        public bool EnabledMsMs { get { return _acquisitionMethod != FullScanAcquisitionMethod.None; } }
        public bool IsHighAccProductFilter { get { return _isHighAccProductFilter; } }
        public bool IsSharedTime { get { return _isSharedTime; } }

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

        public bool IsSimSpectrum(MsDataSpectrum dataSpectrum)
        {
            if (!EnabledMs || _mseLevel > 0)
                return false;
            return dataSpectrum.Level == 1 &&
                IsSimIsolation(GetIsolationWindows(dataSpectrum.Precursors).FirstOrDefault());
        }

        private static bool IsSimIsolation(IsolationWindowFilter isoWin)
        {
            return isoWin.IsolationMz.HasValue && isoWin.IsolationWidth.HasValue &&
                // TODO: Introduce a variable cut-off in the document settings
                isoWin.IsolationWidth.Value < 200;
        }

        public bool IsMsMsSpectrum(MsDataSpectrum dataSpectrum)
        {
            if (!EnabledMsMs)
                return false;
            if (_mseLevel > 0)
                return UpdateMseLevel(dataSpectrum) == 2;
            return dataSpectrum.Level == 2;
        }

        private int UpdateMseLevel(MsDataSpectrum dataSpectrum)
        {
            if (_mseLastSpectrum != null && !ReferenceEquals(dataSpectrum, _mseLastSpectrum))
            {
                // Waters MSe is enumerated in two separate runs, first MS1 and then MS/MS
                // Bruker MSe is enumerated in interleaved MS1 and MS/MS scans
                if (!_isWatersMse)
                {
                    // Alternate between 1 and 2
                    _mseLevel = (_mseLevel % 2) + 1;
                }
                else if ((dataSpectrum.RetentionTime ?? 0) < (_mseLastSpectrum.RetentionTime ?? 0))
                {
                    // level 1 followed by level 2, followed by data that should be ignored
                    _mseLevel++;
                }
            }
            _mseLastSpectrum = dataSpectrum;
            return _mseLevel;
        }

        public IEnumerable<ExtractedSpectrum> SrmSpectraFromMs1Scan(double? time,
                                                                    IList<MsPrecursor> precursors, double[] mzArray, double[] intensityArray)
        {
            if (!EnabledMs || !time.HasValue || mzArray == null || intensityArray == null)
                yield break;

            // All filter pairs have a shot at filtering the MS1 scans
            foreach (var filterPair in FindMs1FilterPairs(precursors))
            {
                if (!filterPair.ContainsTime(time.Value))
                    continue;
                var filteredSrmSpectrum = filterPair.FilterQ1Spectrum(mzArray, intensityArray);
                if (filteredSrmSpectrum != null)
                    yield return filteredSrmSpectrum;
            }
        }

        public IEnumerable<ExtractedSpectrum> Extract(double? time, MsDataSpectrum dataSpectrum)
        {
            double[] mzArray = dataSpectrum.Mzs;
            double[] intensityArray = dataSpectrum.Intensities;
            if (!EnabledMsMs || !time.HasValue || mzArray == null || intensityArray == null)
                yield break;

            foreach (var isoWin in GetIsolationWindows(dataSpectrum.Precursors))
            {
                foreach (var filterPair in FindFilterPairs(isoWin, _acquisitionMethod))
                {
                    if (!filterPair.ContainsTime(time.Value))
                        continue;
                    var filteredSrmSpectrum = filterPair.FilterQ3Spectrum(mzArray, intensityArray);
                    if (filteredSrmSpectrum != null)
                        yield return filteredSrmSpectrum;
                }
            }
        }

        private IEnumerable<IsolationWindowFilter> GetIsolationWindows(IList<MsPrecursor> precursors)
        {
            // Waters MSe high-energy scans actually appear to be MS1 scans without
            // any isolation m/z.  So, use the instrument range.
            if (_mseLevel > 0)
            {
                double isolationWidth = _instrument.MaxMz - _instrument.MinMz;
                double isolationMz = _instrument.MinMz + isolationWidth / 2;
                yield return new IsolationWindowFilter(isolationMz, isolationWidth);
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
            public IsolationWindowFilter(double? isolationMz, double? isolationWidth) : this()
            {
                IsolationMz = isolationMz;
                IsolationWidth = isolationWidth;
            }

            public double? IsolationMz { get; private set; }
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
                    return ((IsolationMz.HasValue ? IsolationMz.Value.GetHashCode() : 0) * 397) ^
                        (IsolationWidth.HasValue ? IsolationWidth.Value.GetHashCode() : 0);
                }
            }

            #endregion
        }

        private readonly Dictionary<IsolationWindowFilter, IList<SpectrumFilterPair>> _filterPairDictionary =
            new Dictionary<IsolationWindowFilter, IList<SpectrumFilterPair>>();

        private IEnumerable<SpectrumFilterPair> FindFilterPairs(IsolationWindowFilter isoWin,
                                                                FullScanAcquisitionMethod acquisitionMethod, bool ignoreIsolationScheme = false)
        {
            List<SpectrumFilterPair> filterPairs = new List<SpectrumFilterPair>();
            
            if (!isoWin.IsolationMz.HasValue)
                return filterPairs; // empty

            // Return cached value from dictionary if we've seen this target previously.
            IList<SpectrumFilterPair> filterPairsCached;
            if (_filterPairDictionary.TryGetValue(isoWin, out filterPairsCached))
            {
                return filterPairsCached;
            }

            if (acquisitionMethod == FullScanAcquisitionMethod.DIA)
            {
                double isoTargMz = isoWin.IsolationMz.Value;
                double? isoTargWidth = isoWin.IsolationWidth;
                if (!ignoreIsolationScheme)
                {
                    CalcDiaIsolationValues(ref isoTargMz, ref isoTargWidth);
                    isoWin = new IsolationWindowFilter(isoTargMz, isoTargWidth);
                }
                if (!isoTargWidth.HasValue)
                {
                    return filterPairs; // empty
                }

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
            else
            {
                // For single case, review all possible matches for the one closest to the
                // desired precursor m/z value.
                SpectrumFilterPair filterPairBest = null;
                double minMzDelta = double.MaxValue;

                // Isolation width for single is based on the instrument m/z match tolerance
                double isoTargMz = isoWin.IsolationMz.Value;
                isoWin = new IsolationWindowFilter(isoTargMz, _instrument.MzMatchTolerance*2);

                foreach (var filterPair in FindFilterPairs(isoWin, FullScanAcquisitionMethod.DIA, true))
                {
                    double mzDelta = Math.Abs(isoTargMz - filterPair.Q1);
                    if (mzDelta < minMzDelta)
                    {
                        minMzDelta = mzDelta;
                        filterPairBest = filterPair;
                    }
                }

                if (filterPairBest != null)
                    filterPairs.Add(filterPairBest);
            }

            _filterPairDictionary[isoWin] = filterPairs;
            return filterPairs;
        }

        private void CalcDiaIsolationValues(ref double isolationTargetMz,
                                            ref double? isolationWidth)
        {
            double isolationWidthValue;
            var isolationScheme = _fullScan.IsolationScheme;
            if (isolationScheme == null)
            {                
                throw new InvalidOperationException("Unexpected attempt to calculate DIA isolation window without an isolation scheme");
            }

                // Calculate window for a simple isolation scheme.
            else if (isolationScheme.PrecursorFilter.HasValue)
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
                IsolationWindow isolationWindow = null;

                // Match pre-specified targets.
                if (isolationScheme.PrespecifiedIsolationWindows[0].Target.HasValue)
                {
                    foreach (var window in isolationScheme.PrespecifiedIsolationWindows)
                    {
                        if (!window.TargetMatches(isolationTargetMz, _instrument.MzMatchTolerance)) continue;
                        if (isolationWindow != null)
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SpectrumFilter_FindFilterPairs_Two_isolation_windows_contain_targets_which_match_the_isolation_target__0__,
                                    isolationTargetMz));
                        }
                        isolationWindow = window;
                    }
                }

                    // Find containing window.
                else
                {
                    foreach (var window in isolationScheme.PrespecifiedIsolationWindows)
                    {
                        if (!window.Contains(isolationTargetMz)) continue;
                        if (isolationWindow != null)
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SpectrumFilter_FindFilterPairs_Two_isolation_windows_contain_the_isolation_target__0__,
                                    isolationTargetMz));
                        }
                        isolationWindow = window;
                    }
                }

                if (isolationWindow == null)
                {
                    _filterPairDictionary[new IsolationWindowFilter(isolationTargetMz, isolationWidth)] = new List<SpectrumFilterPair>();
                    isolationWidth = null;
                    return;
                }

                isolationWidthValue = isolationWindow.End - isolationWindow.Start;
                isolationTargetMz = isolationWindow.Start + isolationWidthValue/2;
            }

                // MSe just uses the instrument isolation window
            else if (isolationWidth.HasValue && isolationScheme.IsAllIons)
            {
                isolationWidthValue = isolationWidth.Value;
            }

                // No defined isolation scheme?
            else
            {
                throw new InvalidDataException(Resources.SpectrumFilter_FindFilterPairs_Isolation_scheme_does_not_contain_any_isolation_windows);
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

        private int IndexOfFilter(double precursorMz, double window)
        {
            return IndexOfFilter(precursorMz, window, 0, _filterMzValues.Length - 1);
        }

        private int IndexOfFilter(double precursorMz, double window, int left, int right)
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

        private static int CompareMz(double mz1, double mz2, double window)
        {
            double startMz = mz1 - window/2;
            if (startMz < mz2 && mz2 < startMz + window)
                return 0;
            return (mz1 > mz2 ? 1 : -1);
        }
    }
}