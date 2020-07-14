﻿/*
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
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Finds ion mobilities by examining loaded results in a document.
    /// N.B. does not attempt to find multiple conformers
    /// </summary>
    public class IonMobilityFinder : IDisposable
    {
        private MsDataFileScanHelper _msDataFileScanHelper;
        private readonly string _documentFilePath;
        private readonly SrmDocument _document;
        private TransitionGroupDocNode _currentDisplayedTransitionGroupDocNode;
        private Dictionary<LibKey, List<IonMobilityIntensityPair>> _ms1IonMobilities;
        private Dictionary<LibKey, List<IonMobilityIntensityPair>> _ms2IonMobilities;
        private int _totalSteps;
        private int _currentStep;
        private readonly IProgressMonitor _progressMonitor;
        private IProgressStatus _progressStatus;
        private Exception _dataFileScanHelperException;
        private double _maxHighEnergyDriftOffsetMsec;
        private bool _useHighEnergyOffset;
        private IonMobilityValue _ms1IonMobilityBest;

        private struct IonMobilityIntensityPair
        {
            public IonMobilityAndCCS IonMobility { get; set; }
            public double Intensity { get; set; }
        }

        /// <summary>
        /// Finds ion mobilities by examining loaded results in a document.
        /// </summary>
        /// <param name="document">The document to be inspected</param>
        /// <param name="documentFilePath">Aids in locating the raw files</param>
        /// <param name="progressMonitor">Optional progress monitor for this potentially long operation</param>
        public IonMobilityFinder(SrmDocument document, string documentFilePath, IProgressMonitor progressMonitor)
        {
            _document = document;
            _documentFilePath = documentFilePath;
            _currentDisplayedTransitionGroupDocNode = null;
            _progressMonitor = progressMonitor;
        }

        public bool UseHighEnergyOffset
        {
            get => _useHighEnergyOffset;
            set
            {
                _useHighEnergyOffset = value;
                _maxHighEnergyDriftOffsetMsec =
                    _useHighEnergyOffset ? 2 : 0; // CONSIDER(bspratt): user definable? or dynamically set by looking at scan to scan drift delta? Or resolving power?
            }
        }

        /// <summary>
        /// Looks through the result and finds ion mobility values.
        /// Note that this method only returns new values that were found in results.
        /// The returned dictionary should be merged with the existing values in
        /// order to preserve those existing values.
        /// </summary>
        public Dictionary<LibKey, IonMobilityAndCCS> FindIonMobilityPeaks()
        {
            // Overwrite any existing measurements with newly derived ones
            var measured = new Dictionary<LibKey, IonMobilityAndCCS>();
            if (_document.Settings.MeasuredResults == null)
                return measured;

            var fileInfos = _document.Settings.MeasuredResults.MSDataFileInfos.ToArray();
            _totalSteps = fileInfos.Length * _document.MoleculeTransitionGroupCount;
            if (_totalSteps == 0)
                return measured;

            using (_msDataFileScanHelper = new MsDataFileScanHelper(SetScans, HandleLoadScanException, true))
            {
                //
                // Avoid opening and re-opening raw files - make these the outer loop
                //

                _ms1IonMobilities = new Dictionary<LibKey, List<IonMobilityIntensityPair>>();
                _ms2IonMobilities = new Dictionary<LibKey, List<IonMobilityIntensityPair>>();
                var twopercent = (int) Math.Ceiling(_totalSteps*0.02);
                _totalSteps += twopercent;
                _currentStep = twopercent;
                if (_progressMonitor != null)
                {
                    _progressStatus = new ProgressStatus(fileInfos.First().FilePath.GetFileName());
                    _progressStatus = _progressStatus.UpdatePercentCompleteProgress(_progressMonitor, _currentStep, _totalSteps); // Make that initial lag seem less dismal to the user
                }
                foreach (var fileInfo in fileInfos)
                {
                    if (!ProcessFile(fileInfo))
                        return null; // User cancelled
                }
                // Find ion mobilities based on MS1 data
                foreach (var dt in _ms1IonMobilities)
                {
                    // Choose the ion mobility which gave the largest signal
                    // CONSIDER: average IM and CCS values that fall "near" the IM of largest signal? Or consider them multiple conformers?
                    var ms1IonMobility = dt.Value.OrderByDescending(p => p.Intensity).First().IonMobility;
                    // Check for MS2 data to use for high energy offset
                    List<IonMobilityIntensityPair> listDt;
                    var ms2IonMobility = _ms2IonMobilities.TryGetValue(dt.Key, out listDt)
                        ? listDt.OrderByDescending(p => p.Intensity).First().IonMobility
                        : ms1IonMobility;
                    var highEnergyIonMobilityValueOffset = Math.Round(ms2IonMobility.IonMobility.Mobility.Value - ms1IonMobility.IonMobility.Mobility.Value, 6); // Excessive precision is just distracting noise TODO(bspratt) ask vendors what "excessive" means here
                    var value =  IonMobilityAndCCS.GetIonMobilityAndCCS(ms1IonMobility.IonMobility, ms1IonMobility.CollisionalCrossSectionSqA, highEnergyIonMobilityValueOffset);
                    if (!measured.ContainsKey(dt.Key))
                        measured.Add(dt.Key, value);
                    else
                        measured[dt.Key] = value;
                }
                // Check for data for which we have only MS2 to go on
                foreach (var im in _ms2IonMobilities)
                {
                    if (!_ms1IonMobilities.ContainsKey(im.Key))
                    {
                        // Only MS2 ion mobility values found, use that as a reasonable inference of MS1 ion mobility
                        var driftTimeIntensityPair = im.Value.OrderByDescending(p => p.Intensity).First();
                        var value = driftTimeIntensityPair.IonMobility;
                        // Note collisional cross section
                        if (_msDataFileScanHelper.ProvidesCollisionalCrossSectionConverter)
                        {
                            var mz = im.Key.PrecursorMz ?? GetMzFromDocument(im.Key);
                            var ccs = _msDataFileScanHelper.CCSFromIonMobility(value.IonMobility,
                                mz, im.Key.Charge);
                            if (ccs.HasValue)
                            {
                                value =  IonMobilityAndCCS.GetIonMobilityAndCCS(value.IonMobility, ccs, value.HighEnergyIonMobilityValueOffset);
                            }
                        }

                        if (!measured.ContainsKey(im.Key))
                            measured.Add(im.Key, value);
                        else
                            measured[im.Key] = value;
                    }
                }
            }
            return measured;
        }

        double GetMzFromDocument(LibKey key)
        {
            foreach (var pair in _document.MoleculePrecursorPairs)
            {
                var nodePep = pair.NodePep;
                var nodeGroup = pair.NodeGroup;
                var libKey = nodeGroup.GetLibKey(_document.Settings, nodePep);
                if (key.Equals(libKey))
                {
                    return nodeGroup.PrecursorMz;
                }
            }
            return 0.0;
        }

        // Returns false on cancellation
        private bool ProcessFile(ChromFileInfo fileInfo)
        {
            var results = _document.Settings.MeasuredResults;
            if (!results.MSDataFileInfos.Contains(fileInfo))
                return true; // Nothing to do
            var filePath = fileInfo.FilePath;
            if (_progressStatus != null)
            {
                _progressStatus = _progressStatus.ChangeMessage(filePath.GetFileName());
            }
            _currentDisplayedTransitionGroupDocNode = null;
            var tolerance = (float)_document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var pair in _document.MoleculePrecursorPairs)
            {
                var nodePep = pair.NodePep;
                var nodeGroup = pair.NodeGroup;
                var libKey = nodeGroup.GetLibKey(_document.Settings, nodePep);
                // Across all replicates for this precursor, note the ion mobility at max intensity for this mz
                for (var i = 0; i < results.Chromatograms.Count; i++)
                {
                    if (_progressMonitor != null && _progressMonitor.IsCanceled)
                        return false;

                    ChromatogramGroupInfo[] chromGroupInfos;
                    results.TryLoadChromatogram(i, nodePep, nodeGroup, tolerance, true, out chromGroupInfos);
                    foreach (var chromInfo in chromGroupInfos.Where(c => Equals(filePath, c.FilePath)))
                    {
                        if (!ProcessChromInfo(fileInfo, chromInfo, pair, nodeGroup, tolerance, libKey)) 
                            return false; // User cancelled
                    }
                }
            }
            return true;
        }

        private bool ProcessChromInfo(ChromFileInfo fileInfo, ChromatogramGroupInfo chromInfo, PeptidePrecursorPair pair,
            TransitionGroupDocNode nodeGroup, float tolerance, LibKey libKey)
        {
            if (chromInfo.NumPeaks == 0)  // Due to data polarity mismatch, probably
                return true;
            Assume.IsTrue(chromInfo.BestPeakIndex != -1);
            var filePath = fileInfo.FilePath;
            var resultIndex = _document.Settings.MeasuredResults.Chromatograms.IndexOf(c => c.GetFileInfo(filePath) != null);
            if (resultIndex == -1)
                return true;
            var chromFileInfo = _document.Settings.MeasuredResults.Chromatograms[resultIndex].GetFileInfo(filePath);
            Assume.IsTrue(Equals(chromFileInfo.FilePath.GetLockMassParameters(), filePath.GetLockMassParameters()));

            // Determine apex RT for DT measurement using most intense MS1 peak
            var apexRT = GetApexRT(nodeGroup, resultIndex, chromFileInfo, true) ??
                GetApexRT(nodeGroup, resultIndex, chromFileInfo, false);
            if (!apexRT.HasValue)
            {
                return true;
            }
            Assume.IsTrue(chromInfo.PrecursorMz.CompareTolerant(pair.NodeGroup.PrecursorMz, 1.0E-9f) == 0 , @"mismatch in precursor values");
            // Only use the transitions currently enabled
            var transitionPointSets = chromInfo.TransitionPointSets.Where(
                tp => nodeGroup.Transitions.Any(
                    t => (t.Mz - (tp.ExtractionWidth ?? tolerance)/2) <= tp.ProductMz &&
                         (t.Mz + (tp.ExtractionWidth ?? tolerance)/2) >= tp.ProductMz))
                .ToArray();

            for (var msLevel = 1; msLevel <= 2; msLevel++)
            {
                if (!ProcessMSLevel(fileInfo, msLevel, transitionPointSets, chromInfo, apexRT, nodeGroup, libKey, tolerance))
                    return false; // User cancelled
            }
            return true;
        }

        private static double? GetApexRT(TransitionGroupDocNode nodeGroup, int resultIndex, ChromFileInfo chromFileInfo, bool ms1Trans)
        {
            double? apexRT = null;
            float ms1Max = 0;
            var trans = ms1Trans
                ? nodeGroup.GetMsTransitions(true)
                : nodeGroup.GetMsMsTransitions(true);
            foreach (var nodeTran in trans)
            {
                foreach (var peakInfo in nodeTran.GetChromInfos(resultIndex).Where(c =>
                    ReferenceEquals(c.FileId, chromFileInfo.FileId)))
                {
                    if (peakInfo.Area > ms1Max)
                    {
                        apexRT = peakInfo.RetentionTime;
                        ms1Max = peakInfo.Area;
                    }
                }
            }
            return apexRT;
        }

        private bool ProcessMSLevel(ChromFileInfo fileInfo, int msLevel, IEnumerable<ChromatogramInfo> transitionPointSets,
            ChromatogramGroupInfo chromInfo, double? apexRT, TransitionGroupDocNode nodeGroup, LibKey libKey, float tolerance)
        {
            var transitions = new List<TransitionFullScanInfo>();
            var chromSource = (msLevel == 1) ? ChromSource.ms1 : ChromSource.fragment;
            IList<float> times = null;
            foreach (var tranPointSet in transitionPointSets.Where(t => t.Source == chromSource))
            {
                transitions.Add(new TransitionFullScanInfo
                {
                    //Name = tranPointSet.Header.,
                    Source = chromSource,
                    TimeIntensities =  tranPointSet.TimeIntensities,
                    PrecursorMz = chromInfo.PrecursorMz,
                    ProductMz = tranPointSet.ProductMz,
                    ExtractionWidth = tranPointSet.ExtractionWidth,
                    //Id = nodeTran.Id
                });
                times = tranPointSet.Times;
            }

            if (!transitions.Any())
            {
                return true; // Nothing to do at this ms level
            }

            var filePath = fileInfo.FilePath;
            IScanProvider scanProvider = new ScanProvider(_documentFilePath, filePath,
                chromSource, times, transitions.ToArray(), _document.Settings.MeasuredResults);

            // Across all spectra at the peak retention time, find the one with max total 
            // intensity for the mz's of interest (ie the isotopic distribution) and note its ion mobility.
            var scanIndex = MsDataFileScanHelper.FindScanIndex(times, apexRT.Value);
            _msDataFileScanHelper.UpdateScanProvider(scanProvider, 0, scanIndex);
            _msDataFileScanHelper.MsDataSpectra = null; // Reset
            scanIndex = _msDataFileScanHelper.GetScanIndex();
            _msDataFileScanHelper.ScanProvider.SetScanForBackgroundLoad(scanIndex);
            lock (this)
            {
                while (_msDataFileScanHelper.MsDataSpectra == null && _dataFileScanHelperException == null)
                {
                    if (_progressMonitor != null && _progressMonitor.IsCanceled)
                        return false;
                    Monitor.Wait(this, 500); // Let background loader do its thing
                }
            }
            if (_dataFileScanHelperException != null)
            {
                throw new IOException(TextUtil.LineSeparate(Resources.IonMobilityFinder_ProcessMSLevel_Failed_using_results_to_populate_ion_mobility_library_, _dataFileScanHelperException.Message), _dataFileScanHelperException);
            }
            if (_progressMonitor != null && !ReferenceEquals(nodeGroup, _currentDisplayedTransitionGroupDocNode))
            {
                // Do this after scan load so first group after file switch doesn't seem laggy
                _progressStatus = _progressStatus.ChangeMessage(TextUtil.LineSeparate(filePath.GetFileName(), nodeGroup.ToString())).
                                     UpdatePercentCompleteProgress(_progressMonitor, _currentStep++, _totalSteps);
                _currentDisplayedTransitionGroupDocNode = nodeGroup;
            }
            EvaluateBestIonMobilityValue(msLevel, libKey, tolerance, transitions);
            return true;
        }

        private void EvaluateBestIonMobilityValue(int msLevel, LibKey libKey, float tolerance, List<TransitionFullScanInfo> transitions)
        {
            IonMobilityValue ionMobilityValue = IonMobilityValue.EMPTY;
            double maxIntensity = 0;

            // Avoid picking MS2 ion mobility values wildly different from MS1 values
            if ((msLevel == 2) && _ms1IonMobilities.ContainsKey(libKey))
            {
                _ms1IonMobilityBest =
                    _ms1IonMobilities[libKey].OrderByDescending(p => p.Intensity)
                        .FirstOrDefault()
                        .IonMobility.IonMobility;
            }
            else
            {
                _ms1IonMobilityBest = IonMobilityValue.EMPTY;
            }

            var totalIntensitiesPerIM = new Dictionary<double, double>();
            double ionMobilityAtMaxIntensity = 0;
            var isThreeArrayFormat = false;
            foreach (var scan in _msDataFileScanHelper.MsDataSpectra.Where(scan => scan != null))
            {
                isThreeArrayFormat = scan.IonMobilities != null;
                if (!isThreeArrayFormat)
                {
                    if (!scan.IonMobility.HasValue || !scan.Mzs.Any())
                        continue;
                    if (IsExtremeMs2Value(scan.IonMobility.Mobility.Value))
                        continue;

                    // Get the total intensity for all transitions of current msLevel
                    double totalIntensity = 0;
                    foreach (var t in transitions)
                    {
                        var mzHigh = FindRangeMz(tolerance, t, scan, out var first);
                        for (var i = first; i < scan.Mzs.Length; i++)
                        {
                            if (scan.Mzs[i] > mzHigh)
                                break;
                            totalIntensity += scan.Intensities[i];
                        }
                    }
                    if (maxIntensity < totalIntensity)
                    {
                        ionMobilityValue = scan.IonMobility;
                        maxIntensity = totalIntensity;
                    }
                }
                else // 3-array IMS format
                {
                    // Get the total intensity for all transitions of current msLevel
                    foreach (var t in transitions)
                    {
                        var mzHigh = FindRangeMz(tolerance, t, scan, out var first);
                        for (var i = first; i < scan.Mzs.Length; i++)
                        {
                            if (scan.Mzs[i] > mzHigh)
                                break;
                            var im = scan.IonMobilities[i];
                            if (IsExtremeMs2Value(im))
                                continue;

                            var intensityThisMzAndIM = scan.Intensities[i];
                            if (!totalIntensitiesPerIM.TryGetValue(im, out var totalIntensityThisIM))
                            {
                                totalIntensityThisIM = intensityThisMzAndIM;
                                totalIntensitiesPerIM.Add(im, totalIntensityThisIM);
                            }
                            else
                            {
                                totalIntensityThisIM += intensityThisMzAndIM;
                                totalIntensitiesPerIM[im] = totalIntensityThisIM;
                            }
                            if (maxIntensity < totalIntensityThisIM)
                            {
                                maxIntensity = totalIntensityThisIM;
                                ionMobilityAtMaxIntensity = im;
                            }
                        }
                    }
                }
            }

            if (isThreeArrayFormat)
            {
                ionMobilityValue = IonMobilityValue.GetIonMobilityValue(ionMobilityAtMaxIntensity, _msDataFileScanHelper.ScanProvider.IonMobilityUnits);
            }
            if (ionMobilityValue.HasValue)
            {
                var dict = (msLevel == 1) ? _ms1IonMobilities : _ms2IonMobilities;
                var ccs = msLevel == 1 && _msDataFileScanHelper.ProvidesCollisionalCrossSectionConverter ? _msDataFileScanHelper.CCSFromIonMobility(ionMobilityValue, transitions.First().PrecursorMz, libKey.Charge) : null;
                var result = new IonMobilityIntensityPair
                {
                    IonMobility =  IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobilityValue, ccs, 0),
                    Intensity = maxIntensity
                };
                List<IonMobilityIntensityPair> listPairs;
                if (!dict.TryGetValue(libKey, out listPairs))
                {
                    listPairs = new List<IonMobilityIntensityPair>();
                    dict.Add(libKey, listPairs);
                }
                listPairs.Add(result);
            }
        }

        private static SignedMz FindRangeMz(float tolerance, TransitionFullScanInfo t, MsDataSpectrum scan, out int first)
        {
            Assume.IsTrue(t.ProductMz.IsNegative == scan.NegativeCharge);  // It would be strange if associated scan did not have same polarity
            var mzPeak = t.ProductMz;
            var halfwin = (t.ExtractionWidth ?? tolerance) / 2;
            var mzLow = mzPeak - halfwin;
            var mzHigh = mzPeak + halfwin;
            first = Array.BinarySearch(scan.Mzs, mzLow);
            if (first < 0)
                first = ~first;
            return mzHigh;
        }

        private bool IsExtremeMs2Value(double im)
        {
            return _ms1IonMobilityBest.HasValue &&
                   (im < _ms1IonMobilityBest.Mobility - _maxHighEnergyDriftOffsetMsec ||
                    im > _ms1IonMobilityBest.Mobility + _maxHighEnergyDriftOffsetMsec);
        }

        private void HandleLoadScanException(Exception ex)
        {
            lock (this)
            {
                _dataFileScanHelperException = ex;
                if (_msDataFileScanHelper != null)
                    _msDataFileScanHelper.MsDataSpectra = null;
                Monitor.PulseAll(this);
            }
        }

        private void SetScans(MsDataSpectrum[] scans)
        {
            lock (this)
            {
                _msDataFileScanHelper.MsDataSpectra = scans;
                Monitor.PulseAll(this);
            }
        }

        public void Dispose()
        {
            if (_msDataFileScanHelper != null)
            {
                _msDataFileScanHelper.Dispose();
                _msDataFileScanHelper = null;
            }
        }
    }
}
