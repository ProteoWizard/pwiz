/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Threading;
using pwiz.Common.Chemistry;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class MsDataFileScanHelper : IDisposable
    {
        private ChromSource _chromSource;

        public enum PeakType : byte
        {
            chromDefault,
            centroided,
            profile
        };

        public MsDataFileScanHelper(Action<MsDataSpectrum[]> successAction, Action<Exception> failureAction, bool ignoreZeroIntensityPoints)
        {
            ScanProvider = new BackgroundScanProvider(successAction, failureAction, ignoreZeroIntensityPoints);
            SourceNames = new string[Helpers.CountEnumValues<ChromSource>()];
            SourceNames[(int) ChromSource.ms1] = Resources.GraphFullScan_GraphFullScan_MS1;
            SourceNames[(int) ChromSource.fragment] = Resources.GraphFullScan_GraphFullScan_MS_MS;
            SourceNames[(int) ChromSource.sim] = Resources.GraphFullScan_GraphFullScan_SIM;

            PeakTypeNames = new string[Helpers.CountEnumValues<PeakType>()];
            PeakTypeNames[(int) PeakType.chromDefault] = Resources.GraphFullScan_PeakType_ChromDefault;
            PeakTypeNames[(int)PeakType.centroided] = Resources.GraphFullScan_PeakType_Centroided;
            PeakTypeNames[(int)PeakType.profile] = Resources.GraphFullScan_PeakType_Profile;
        }

        public BackgroundScanProvider ScanProvider { get; private set; }

        public MsDataSpectrum[] MsDataSpectra { get; set; }

        public string FileName { get; private set; }

        public int TransitionIndex { get; set; }

        public TransitionFullScanInfo CurrentTransition
        {
            get
            {
                if (ScanProvider.Transitions.Length > TransitionIndex)
                    return ScanProvider.Transitions[TransitionIndex];
                return null;
            }
        }

        public int ScanIndex { get; set; }

        public int? OptStep { get; private set; }

        public string[] SourceNames { get; set; }

        public string[] PeakTypeNames { get; set; }

        public ChromSource Source
        {
            get { return _chromSource; }
            set
            {
                if (Source == value)
                {
                    return;
                }

                var oldTimeIntensities = TimeIntensities;
                _chromSource = value;
                var newTimeIntensities = TimeIntensities;
                if (newTimeIntensities != null)
                {
                    if (oldTimeIntensities != null && ScanIndex >= 0 && ScanIndex < oldTimeIntensities.Times.Count)
                    {
                        var oldTime = oldTimeIntensities.Times[ScanIndex];
                        ScanIndex = newTimeIntensities.IndexOfNearestTime(oldTime);
                    }
                    else
                    {
                        ScanIndex = Math.Min(ScanIndex, newTimeIntensities.NumPoints - 1);
                    }
                }
            }
        }

        public ChromSource SourceFromName(string name)
        {
            return (ChromSource) SourceNames.IndexOf(e => e == name);
        }

        public PeakType PeakTypeFromLocalizedName(string name)
        {
            return (PeakType) PeakTypeNames.IndexOf(e => e == name);
        }

        public PeakType ParsePeakTypeEnumName(string enumName)
        {
            if (Enum.TryParse<PeakType>(enumName, out var peakType))
                return peakType;
            else
                return PeakType.chromDefault;
        }

        public string GetPeakTypeLocalizedName(PeakType peakType)
        {
            return PeakTypeNames[(int) peakType];
        }

        public string NameFromSource(ChromSource source)
        {
            return SourceNames[(int) source];
        }

        public MsDataSpectrum[] GetFilteredScans(out double minIonMobilityVal, out double maxIonMobilityVal)
        {
            var fullScans = MsDataSpectra;
            double minIonMobility, maxIonMobility;
            if (Settings.Default.FilterIonMobilityFullScan &&
                GetIonMobilityFilterRange(out minIonMobility, out maxIonMobility, Source))
            {
                if (IsWatersSonarData)
                {
                    // "ion mobility" range is actually SONAR mz filtering range, caller will want that expressed as bin numbers
                    minIonMobility = ScanProvider.SonarMzToBinRange(minIonMobility, 0).Item1;
                    maxIonMobility = ScanProvider.SonarMzToBinRange(maxIonMobility, 0).Item2;
                }
                fullScans = fullScans.Where(s =>
                        minIonMobility <= s.IonMobility.Mobility && s.IonMobility.Mobility <= maxIonMobility // im-per-scan case
                        || minIonMobility <= s.MaxIonMobility && maxIonMobility >= s.MinIonMobility // 3-array case
                ).ToArray();
            }
            else
            {
                minIonMobility = double.MinValue;
                maxIonMobility = double.MaxValue;
            }

            minIonMobilityVal = minIonMobility;
            maxIonMobilityVal = maxIonMobility;
            return fullScans;
        }

        // Determine the lower and upper bound of any ion mobility filtering
        // In the case of Waters SONAR data, it's actually precursor m/z filtering so we return bin values (which need to be converted to SONAR bins for filtering)
        public bool GetIonMobilityFilterRange(out double minIonMobility, out double maxIonMobility, ChromSource sourceType)
        {
            minIonMobility = double.MaxValue;
            maxIonMobility = double.MinValue;
            var hasIonMobilityInfo = false;
            int i = 0;
            foreach (var transition in ScanProvider.Transitions)
            {
                if (IsWatersSonarData)
                {
                    // Waters SONAR uses IM hardware to filter on precursor m/z and presents those data bins as if they were drift bins
                    // So actual filter range is the same m/z window used in the m/z dimension.
                    var mz = transition.PrecursorMz.Value;
                    var halfWin = (transition.ExtractionWidth ?? 0) / 2;
                    var mzLow = mz - halfWin;
                    var mzHigh = mz + halfWin;
                    minIonMobility = Math.Min(minIonMobility, mzLow); // Yes, this is a misnomer
                    maxIonMobility = Math.Max(maxIonMobility, mzHigh); 
                    hasIonMobilityInfo = true; // Well, not really ion mobility info - the drift time dimension is really precursor m/z space
                }
                else if (!transition.IonMobilityInfo.HasIonMobilityValue || !transition.IonMobilityInfo.IonMobilityExtractionWindowWidth.HasValue)
                {
                    // Accept all values
                    minIonMobility = double.MinValue;
                    maxIonMobility = double.MaxValue;
                }
                else if (sourceType == ChromSource.unknown || (transition.Source == sourceType && i == TransitionIndex))
                {
                    // Products and precursors may have different expected ion mobility values in Waters MsE
                    double startIM = transition.IonMobilityInfo.IonMobility.Mobility.Value -
                                        transition.IonMobilityInfo.IonMobilityExtractionWindowWidth.Value / 2;
                    double endIM = startIM + transition.IonMobilityInfo.IonMobilityExtractionWindowWidth.Value;
                    minIonMobility = Math.Min(minIonMobility, startIM);
                    maxIonMobility = Math.Max(maxIonMobility, endIM);
                    hasIonMobilityInfo = true;
                }
                i++;
            }
            return hasIonMobilityInfo;
        }

        // Determine the lower and upper bound of any ion mobility filtering, and for Waters SONAR display purposes include the effect
        // of m/z values potentially covering more than one bin
        public bool GetIonMobilityFilterDisplayRange(out double minIonMobility, out double maxIonMobility, ChromSource sourceType)
        {
            var hasIonMobilityInfo = GetIonMobilityFilterRange(out minIonMobility, out maxIonMobility, sourceType);

            if (hasIonMobilityInfo && IsWatersSonarData)
            {
                // "Ion mobility" range is actually SONAR mz filtering range, i.e. the m/z extraction window from Settings.Transitions.FullScan
                // But a single m/z value can cover multiple bins, so the actual quadrupole m/z selection range is probably wider than that and
                // a wider m/z band was actually admitted into the collision cell to ensure sampling the precursor actually wanted.
                // So take that into account when determining the edges of the purple "ion mobility" band for display
                var mzAvg = 0.5 * (maxIonMobility + minIonMobility);
                var mzTol = 0.5 * (maxIonMobility - minIonMobility);
                var binRange = ScanProvider.SonarMzToBinRange(mzAvg, mzTol);
                minIonMobility = Math.Min(minIonMobility, ScanProvider.SonarBinToPrecursorMz(binRange.Item1) ?? 0);
                maxIonMobility = Math.Max(maxIonMobility, ScanProvider.SonarBinToPrecursorMz(binRange.Item2) ?? 0);
            }
            return hasIonMobilityInfo;
        }

        /// <summary>
        /// Return a collisional cross section for this ion mobility at this mz, if reader supports this
        /// </summary>
        public double? CCSFromIonMobility(IonMobilityValue ionMobility, double mz, int charge)
        {
            if (ScanProvider == null)
            {
                return null;
            }
            return ScanProvider.CCSFromIonMobility(ionMobility, mz, charge);
        }

        public bool IsWatersSonarData { get {  return ScanProvider?. IsWatersSonarData ?? false; } } // For SONAR the drift dimension is actually precursor m/z filter dimension

        public bool ProvidesCollisionalCrossSectionConverter
        {
            get { return ScanProvider != null && ScanProvider.ProvidesCollisionalCrossSectionConverter; }
        }

        public eIonMobilityUnits IonMobilityUnits
        {
            get
            {
                return ScanProvider.IonMobilityUnits;
            }
        }

        public TimeIntensities TimeIntensities => ScanProvider?.Transitions
            .FirstOrDefault(transition => transition.Source == Source)?.TimeIntensities;

        public int GetScanIndex()
        {
            var scanIndexes = TimeIntensities?.ScanIds;
            var result = scanIndexes != null ? scanIndexes[ScanIndex] : -1;
            if (result < 0)
                MsDataSpectra = null;
            return result;
        }

        public static int FindScanIndex(ChromatogramInfo chromatogramInfo, double retentionTime)
        {
            if (chromatogramInfo.TimeIntensities.ScanIds == null)
                return -1;
            return FindScanIndex(chromatogramInfo.Times, retentionTime, 0, chromatogramInfo.Times.Count);
        }

        public static int FindScanIndex(IList<float> times, double retentionTime)
        {
            return FindScanIndex(times, retentionTime, 0, times.Count);
        }

        private static int FindScanIndex(IList<float> times, double retentionTime, int startIndex, int endIndex)
        {
            if (endIndex - startIndex <= 1)
                return startIndex;

            int index = (startIndex + endIndex) / 2;
            return (retentionTime < times[index])
                ? FindScanIndex(times, retentionTime, startIndex, index)
                : FindScanIndex(times, retentionTime, index, endIndex);
        }

        public void UpdateScanProvider(IScanProvider scanProvider, int transitionIndex, int scanIndex, int? optStep)
        {
            ScanProvider.SetScanProvider(scanProvider);
            if (scanProvider != null)
            {
                Source = scanProvider.Transitions[transitionIndex].Source;
                if (Source != ScanProvider.Source)
                    Assume.Fail($@"unexpected ChromSource '{ScanProvider.Source}' in transition {transitionIndex} ({scanProvider.Transitions[transitionIndex]})");
                TransitionIndex = transitionIndex;
                ScanIndex = scanIndex;
                OptStep = optStep;
                FileName = scanProvider.DataFilePath.GetFileName();
            }
            else
            {
                MsDataSpectra = null;
                FileName = null;
            }
        }
        /// <summary>
        /// Provides a constant background thread with responsibility for all interactions
        /// with <see cref="IScanProvider"/>, necessary because <see cref="MsDataFileImpl"/> objects
        /// must be accessed on the same thread.
        /// </summary>
        public class BackgroundScanProvider : IDisposable
        {
            private const int MAX_CACHE_COUNT = 2;

            private bool _disposing;
            private int _scanIndexNext;
            private bool? _centroidedMs1, _centroidedMs2;
            private IScanProvider _scanProvider;
            private readonly List<IScanProvider> _cachedScanProviders;
            private readonly List<IScanProvider> _oldScanProviders;
            private readonly Thread _backgroundThread;
            private bool _ignoreZeroIntensityPoints;

            private readonly Action<MsDataSpectrum[]> _successAction;
            private readonly Action<Exception> _failureAction;

            public BackgroundScanProvider(Action<MsDataSpectrum[]> successAction, Action<Exception> failureAction, bool ignoreZeroIntensityPoints)
            {
                _scanIndexNext = -1;

                _oldScanProviders = new List<IScanProvider>();
                _cachedScanProviders = new List<IScanProvider>();
                _backgroundThread = new Thread(Work) { Name = GetType().Name, Priority = ThreadPriority.BelowNormal, IsBackground = true };
                _backgroundThread.Start();

                _successAction = successAction;
                _failureAction = failureAction;
                _ignoreZeroIntensityPoints = ignoreZeroIntensityPoints;
            }

            public MsDataFileUri DataFilePath
            {
                get { return GetProviderProperty(p => p.DataFilePath, new MsDataFilePath(string.Empty)); }
            }

            public ChromSource Source
            {
                get { return GetProviderProperty(p => p.Source, ChromSource.unknown); }
            }

            public TransitionFullScanInfo[] Transitions
            {
                get { return GetProviderProperty(p => p.Transitions, new TransitionFullScanInfo[0]); }
            }

            public IList<float> Times
            {
                get { return GetProviderProperty(p => p.Times, new float[0]); }
            }

            private TProp GetProviderProperty<TProp>(Func<IScanProvider, TProp> getProp, TProp defaultValue)
            {
                lock (this)
                {
                    return _scanProvider != null ? getProp(_scanProvider) : defaultValue;
                }
            }

            /// <summary>
            /// Return a collisional cross section for this ion mobility at this mz, if reader supports this
            /// </summary>
            public double? CCSFromIonMobility(IonMobilityValue ionMobility, double mz, int charge)
            {
                if (_scanProvider == null)
                {
                    return null;
                }
                return _scanProvider.CCSFromIonMobility(ionMobility, mz, charge);
            }

            public eIonMobilityUnits IonMobilityUnits
            {
                get
                {
                    return _scanProvider != null
                        ? _scanProvider.IonMobilityUnits
                        : eIonMobilityUnits.none;
                } }

            public bool ProvidesCollisionalCrossSectionConverter { get { return _scanProvider != null && _scanProvider.ProvidesCollisionalCrossSectionConverter; } }

            public bool IsWatersSonarData { get { return _scanProvider != null && _scanProvider.IsWatersSonarData; } }
            public Tuple<int, int> SonarMzToBinRange(double mz, double tolerance)
            {
                return _scanProvider?.SonarMzToBinRange(mz, tolerance);
            }
            public double? SonarBinToPrecursorMz(int bin)
            {
                return _scanProvider?.SonarBinToPrecursorMz(bin);
            }

            /// <summary>
            /// Always run on a specific background thread to avoid changing threads when dealing
            /// with a scan provider, which can mess up data readers used by ProteoWizard.
            /// </summary>
            private void Work()
            {
                try
                {
                    while (!_disposing)
                    {
                        IScanProvider scanProvider;
                        int internalScanIndex;

                        lock (this)
                        {
                            while (!_disposing && (_scanProvider == null || _scanIndexNext < 0) && _oldScanProviders.Count == 0)
                                Monitor.Wait(this);
                            if (_disposing)
                                break;

                            scanProvider = _scanProvider;
                            internalScanIndex = _scanIndexNext;
                            _scanIndexNext = -1;
                        }

                        if (scanProvider != null && internalScanIndex != -1)
                        {
                            try
                            {
                                // Get a collection of scans with changing ion mobility but same retention time, or single scan if no ion mobility info
                                var msDataSpectra = scanProvider.GetMsDataFileSpectraWithCommonRetentionTime(internalScanIndex, _ignoreZeroIntensityPoints, _centroidedMs1, _centroidedMs2); 
                                _successAction(msDataSpectra);
                            }
                            catch (Exception ex)
                            {
                                try
                                {
                                    _failureAction(ex);
                                }
                                catch (Exception exFailure)
                                {
                                    Program.ReportException(exFailure);
                                }
                            }
                        }

                        DisposeAllProviders();
                    }
                }
                finally
                {
                    lock (this)
                    {
                        SetScanProvider(null);
                        DisposeAllProviders();

                        Monitor.PulseAll(this);
                    }
                }
            }

            public void SetScanProvider(IScanProvider newScanProvider)
            {
                lock (this)
                {
                    if (_scanProvider != null && !ReferenceEquals(_scanProvider, newScanProvider)) 
                    {
                        _cachedScanProviders.Insert(0, _scanProvider);

                        if (newScanProvider != null)
                        {
                            AdoptCachedProvider(newScanProvider);
                        }

                        // Queue for disposal
                        if (_cachedScanProviders.Count > MAX_CACHE_COUNT)
                        {
                            _oldScanProviders.Add(_cachedScanProviders[MAX_CACHE_COUNT]);
                            _cachedScanProviders.RemoveAt(MAX_CACHE_COUNT);
                        }
                    }
                    _scanProvider = newScanProvider;
                    if (newScanProvider == null) // Called with null when we're disposing
                    {
                        _oldScanProviders.AddRange(_cachedScanProviders);
                        _cachedScanProviders.Clear();
                    }
                    Monitor.PulseAll(this);
                }
            }

            private void AdoptCachedProvider(IScanProvider scanProvider)
            {
                lock (this)
                {
                    for (int i = 0; i < _cachedScanProviders.Count; i++)
                    {
                        if (scanProvider.Adopt(_cachedScanProviders[i]))
                        {
                            _oldScanProviders.Add(_cachedScanProviders[i]);
                            _cachedScanProviders.RemoveAt(i);
                            return;
                        }
                    }
                }
            }

            public void SetScanForBackgroundLoad(int scanIndex, bool? centroidedMs1 = null, bool? centroidedMs2 = null)
            {
                lock (this)
                {
                    _scanIndexNext = scanIndex;
                    _centroidedMs1 = _centroidedMs2 = null;
                    _centroidedMs2 = centroidedMs2;
                    _centroidedMs1 = centroidedMs1;

                    if (_scanIndexNext != -1)
                        Monitor.PulseAll(this);
                }
            }

            private void DisposeAllProviders()
            {
                IScanProvider[] disposeScanProviders;
                lock (this)
                {
                    disposeScanProviders = _oldScanProviders.ToArray();
                    _oldScanProviders.Clear();
                }
                foreach (var provider in disposeScanProviders)
                    provider.Dispose();
            }

            public void Dispose()
            {
                // Wait for dispose to happen on the background thread
                lock (this)
                {
                    _disposing = true;
                    SetScanProvider(null);
                }
                // Make sure the background thread goes away
                _backgroundThread.Join();
            }
        }

        public void Dispose()
        {
            if (ScanProvider != null)
                ScanProvider.Dispose();
            ScanProvider = null;
        }
    }
}