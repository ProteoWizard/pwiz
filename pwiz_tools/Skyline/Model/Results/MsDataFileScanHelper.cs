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
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class MsDataFileScanHelper : IDisposable
    {
        public MsDataFileScanHelper(Action<MsDataSpectrum[]> successAction, Action<Exception> failureAction)
        {
            ScanProvider = new BackgroundScanProvider(successAction, failureAction);
            SourceNames = new string[Helpers.CountEnumValues<ChromSource>()];
            SourceNames[(int)ChromSource.ms1] = Resources.GraphFullScan_GraphFullScan_MS1;
            SourceNames[(int)ChromSource.fragment] = Resources.GraphFullScan_GraphFullScan_MS_MS;
            SourceNames[(int)ChromSource.sim] = Resources.GraphFullScan_GraphFullScan_SIM;
        }

        public BackgroundScanProvider ScanProvider { get; private set; }

        public MsDataSpectrum[] MsDataSpectra { get; set; }

        public string FileName { get; private set; }

        public int TransitionIndex { get; set; }

        public int ScanIndex { get; set; }

        public string[] SourceNames { get; set; }

        public ChromSource Source { get; set; }

        public ChromSource SourceFromName(string name)
        {
            return (ChromSource) SourceNames.IndexOf(e => e == name);
        }

        public string NameFromSource(ChromSource source)
        {
            return SourceNames[(int) source];
        }

        public MsDataSpectrum[] GetFilteredScans()
        {
            var fullScans = MsDataSpectra;
            double minIonMobility, maxIonMobility;
            if (Settings.Default.FilterIonMobilityFullScan && GetIonMobilityRange(out minIonMobility, out maxIonMobility, Source))
                fullScans = fullScans.Where(s => minIonMobility <= s.IonMobility.Mobility && s.IonMobility.Mobility <= maxIonMobility).ToArray();
            return fullScans;
        }

        public bool GetIonMobilityRange(out double minIonMobility, out double maxIonMobility, ChromSource sourceType)
        {
            minIonMobility = double.MaxValue;
            maxIonMobility = double.MinValue;
            var hasIonMobilityInfo = false;
            foreach (var transition in ScanProvider.Transitions)
            {
                if (!transition._ionMobilityInfo.HasIonMobilityValue || !transition._ionMobilityInfo.IonMobilityExtractionWindowWidth.HasValue)
                {
                    // Accept all values
                    minIonMobility = double.MinValue;
                    maxIonMobility = double.MaxValue;
                }
                else if (sourceType == ChromSource.unknown || transition.Source == sourceType)
                {
                    // Products and precursors may have different expected ion mobility values in Waters MsE
                    double startIM = transition._ionMobilityInfo.IonMobility.Mobility.Value -
                                        transition._ionMobilityInfo.IonMobilityExtractionWindowWidth.Value / 2;
                    double endIM = startIM + transition._ionMobilityInfo.IonMobilityExtractionWindowWidth.Value;
                    minIonMobility = Math.Min(minIonMobility, startIM);
                    maxIonMobility = Math.Max(maxIonMobility, endIM);
                    hasIonMobilityInfo = true;
                }
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

        public bool ProvidesCollisionalCrossSectionConverter
        {
            get { return ScanProvider != null && ScanProvider.ProvidesCollisionalCrossSectionConverter; }
        }

        public MsDataFileImpl.eIonMobilityUnits IonMobilityUnits
        {
            get
            {
                return ScanProvider.IonMobilityUnits;
            }
        }

        public IList<int> GetScanIndexes(ChromSource source)
        {
            if (ScanProvider != null)
            {
                foreach (var transition in ScanProvider.Transitions)
                {
                    if (transition.Source == source)
                        return transition.ScanIndexes;
                }
            }
            return null;
        }

        public int GetScanIndex()
        {
            var scanIndexes = GetScanIndexes(Source);
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

        public void UpdateScanProvider(IScanProvider scanProvider, int transitionIndex, int scanIndex)
        {
            ScanProvider.SetScanProvider(scanProvider);
            if (scanProvider != null)
            {
                Source = scanProvider.Transitions[transitionIndex].Source;
                Assume.IsTrue(Source == ScanProvider.Source);
                TransitionIndex = transitionIndex;
                ScanIndex = scanIndex;
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
            private IScanProvider _scanProvider;
            private readonly List<IScanProvider> _cachedScanProviders;
            private readonly List<IScanProvider> _oldScanProviders;
            private readonly Thread _backgroundThread;

            private readonly Action<MsDataSpectrum[]> _successAction;
            private readonly Action<Exception> _failureAction;

            public BackgroundScanProvider(Action<MsDataSpectrum[]> successAction, Action<Exception> failureAction)
            {
                _scanIndexNext = -1;

                _oldScanProviders = new List<IScanProvider>();
                _cachedScanProviders = new List<IScanProvider>();
                _backgroundThread = new Thread(Work) { Name = GetType().Name, Priority = ThreadPriority.BelowNormal, IsBackground = true };
                _backgroundThread.Start();

                _successAction = successAction;
                _failureAction = failureAction;
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

            public MsDataFileImpl.eIonMobilityUnits IonMobilityUnits
            {
                get
                {
                    return _scanProvider != null
                        ? _scanProvider.IonMobilityUnits
                        : MsDataFileImpl.eIonMobilityUnits.none;
                } }

            public bool ProvidesCollisionalCrossSectionConverter { get { return _scanProvider != null && _scanProvider.ProvidesCollisionalCrossSectionConverter; } }

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
                                var msDataSpectra = scanProvider.GetMsDataFileSpectraWithCommonRetentionTime(internalScanIndex); // Get a collection of scans with changing ion mobility but same retention time, or single scan if no ion mobility info
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

            public void SetScanForBackgroundLoad(int scanIndex)
            {
                lock (this)
                {
                    _scanIndexNext = scanIndex;

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