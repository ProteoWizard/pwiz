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
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Finds drift times by examining loaded results in a document.
    /// </summary>
    public class DriftTimeFinder : IDisposable
    {
        private MsDataFileScanHelper _msDataFileScanHelper;
        private readonly DriftTimePredictor _existing;
        private readonly string _documentFilePath;
        private readonly SrmDocument _document;
        private TransitionGroupDocNode _currentDisplayedTransitionGroupDocNode;
        private Dictionary<LibKey, List<DriftTimeIntensityPair>> _ms1DriftTimes;
        private Dictionary<LibKey, List<DriftTimeIntensityPair>> _ms2DriftTimes;
        private int _totalSteps;
        private int _currentStep;
        private readonly IProgressMonitor _progressMonitor;
        private ProgressStatus _progressStatus;
        private Exception _dataFileScanHelperException;

        private struct DriftTimeIntensityPair
        {
            public double? DriftTime { get; set; }
            public double Intensity { get; set; }
        }

        /// <summary>
        /// Finds drift times by examining loaded results in a document.
        /// </summary>
        /// <param name="document">The document to be inspected</param>
        /// <param name="documentFilePath">Aids in locating the raw files</param>
        /// <param name="existing">If non-null, will be examined for any existing drift time measurements (which may be overwritten) </param>
        /// <param name="progressMonitor">Optional progress monitor for this potentially long operation</param>
        public DriftTimeFinder(SrmDocument document, string documentFilePath, DriftTimePredictor existing, IProgressMonitor progressMonitor)
        {
            _document = document;
            _documentFilePath = documentFilePath;
            _existing = existing;
            _currentDisplayedTransitionGroupDocNode = null;
            _progressMonitor = progressMonitor;
        }

        public Dictionary<LibKey, DriftTimeInfo> FindDriftTimePeaks()
        {
            // Overwrite any existing measurements with newly derived ones
            var measured = new Dictionary<LibKey, DriftTimeInfo>();
            if (_existing != null && _existing.MeasuredDriftTimePeptides != null)
            {
                foreach (var existingPair in _existing.MeasuredDriftTimePeptides)
                    measured.Add(existingPair.Key, existingPair.Value);
            }

            var filepaths = _document.Settings.MeasuredResults.MSDataFilePaths.ToArray();
            _totalSteps = filepaths.Length * _document.MoleculeTransitionGroupCount;
            if (_totalSteps == 0)
                return measured;

            using (_msDataFileScanHelper = new MsDataFileScanHelper(SetScans, HandleLoadScanException))
            {
                //
                // Avoid opening and re-opening raw files - make these the outer loop
                //

                _ms1DriftTimes = new Dictionary<LibKey, List<DriftTimeIntensityPair>>();
                _ms2DriftTimes = new Dictionary<LibKey, List<DriftTimeIntensityPair>>();
                var twopercent = (int) Math.Ceiling(_totalSteps*0.02);
                _totalSteps += twopercent;
                _currentStep = twopercent;
                if (_progressMonitor != null)
                {
                    _progressStatus = new ProgressStatus(filepaths.First().GetFileName());
                    _progressStatus = _progressStatus.UpdatePercentCompleteProgress(_progressMonitor, _currentStep, _totalSteps); // Make that inital lag seem less dismal to the user
                }
                foreach (var fp in filepaths)
                {
                    if (!ProcessFile(fp))
                        return null; // User cancelled
                }
                // Find drift times based on MS1 data
                foreach (var dt in _ms1DriftTimes)
                {
                    // Choose the drift time which gave the largest signal
                    var ms1DriftTime = dt.Value.OrderByDescending(p => p.Intensity).First().DriftTime;
                    // Check for MS2 data to use for high energy offset
                    List<DriftTimeIntensityPair> listDt;
                    var ms2DriftTime = _ms2DriftTimes.TryGetValue(dt.Key, out listDt)
                        ? listDt.OrderByDescending(p => p.Intensity).First().DriftTime
                        : (ms1DriftTime ?? 0);
                    var value = new DriftTimeInfo(ms1DriftTime, ms2DriftTime - ms1DriftTime ?? 0);
                    if (!measured.ContainsKey(dt.Key))
                        measured.Add(dt.Key, value);
                    else
                        measured[dt.Key] = value;
                }
                // Check for data for which we have only MS2 to go on
                foreach (var dt in _ms2DriftTimes)
                {
                    if (!_ms1DriftTimes.ContainsKey(dt.Key))
                    {
                        // Only MS2 drift times found, use that
                        var value = new DriftTimeInfo(dt.Value.OrderByDescending(p => p.Intensity).First().DriftTime, 0);
                        if (!measured.ContainsKey(dt.Key))
                            measured.Add(dt.Key, value);
                        else
                            measured[dt.Key] = value;
                    }
                }
            }
            return measured;
        }

        // Returns false on cancellation
        private bool ProcessFile(MsDataFileUri filePath)
        {
            var results = _document.Settings.MeasuredResults;
            if (!results.MSDataFilePaths.Contains(filePath))
                return true; // Nothing to do
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
                var libKey = new LibKey(nodePep.RawTextId, nodeGroup.PrecursorCharge);
                // Across all replicates for this precursor, note the drift time at max intensity for this mz
                for (var i = 0; i < results.Chromatograms.Count; i++)
                {
                    if (_progressMonitor != null && _progressMonitor.IsCanceled)
                        return false;

                    ChromatogramGroupInfo[] chromGroupInfos;
                    results.TryLoadChromatogram(i, nodePep, nodeGroup, tolerance, true, out chromGroupInfos);
                    foreach (var chromInfo in chromGroupInfos.Where(c => filePath == c.FilePath))
                    {
                        if (!ProcessChromInfo(filePath, chromInfo, pair, nodeGroup, tolerance, libKey)) 
                            return false; // User cancelled
                    }
                }
            }
            return true;
        }

        private bool ProcessChromInfo(MsDataFileUri filePath, ChromatogramGroupInfo chromInfo, PeptidePrecursorPair pair,
            TransitionGroupDocNode nodeGroup, float tolerance, LibKey libKey)
        {
            Assume.IsTrue(chromInfo.BestPeakIndex != -1);
            var resultIndex = _document.Settings.MeasuredResults.Chromatograms.IndexOf(c => c.GetFileInfo(filePath) != null);
            if (resultIndex == -1)
                return true;
            var chromFileInfo = _document.Settings.MeasuredResults.Chromatograms[resultIndex].GetFileInfo(filePath);
            Assume.IsTrue(Equals(chromFileInfo.FilePath.GetLockMassParameters(), filePath.GetLockMassParameters()));

            // Determine apex RT for DT measurement using most intense MS1 peak
            var apexRT = GetApexRT(nodeGroup, resultIndex, chromFileInfo, true) ??
                GetApexRT(nodeGroup, resultIndex, chromFileInfo, false);

            Assume.IsTrue(chromInfo.PrecursorMz == pair.NodeGroup.PrecursorMz);
            // Only use the transitions currently enabled
            var transitionPointSets = chromInfo.TransitionPointSets.Where(
                tp => nodeGroup.Transitions.Any(
                    t => (t.Mz - (tp.ExtractionWidth ?? tolerance)/2) <= tp.ProductMz &&
                         (t.Mz + (tp.ExtractionWidth ?? tolerance)/2) >= tp.ProductMz))
                .ToArray();

            for (var msLevel = 1; msLevel <= 2; msLevel++)
            {
                if (!ProcessMSLevel(filePath, msLevel, transitionPointSets, chromInfo, apexRT, nodeGroup, libKey, tolerance))
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

        private bool ProcessMSLevel(MsDataFileUri filePath, int msLevel, IEnumerable<ChromatogramInfo> transitionPointSets,
            ChromatogramGroupInfo chromInfo, double? apexRT, TransitionGroupDocNode nodeGroup, LibKey libKey, float tolerance)
        {
            var transitions = new List<TransitionFullScanInfo>();
            var chromSource = (msLevel == 1) ? ChromSource.ms1 : ChromSource.fragment;
            foreach (var tranPointSet in transitionPointSets.Where(t => t.Source == chromSource))
            {
                transitions.Add(new TransitionFullScanInfo
                {
                    //Name = tranPointSet.Header.,
                    Source = chromSource,
                    ScanIndexes = chromInfo.ScanIndexes,
                    PrecursorMz = chromInfo.PrecursorMz,
                    ProductMz = tranPointSet.ProductMz,
                    ExtractionWidth = tranPointSet.ExtractionWidth,
                    //Id = nodeTran.Id
                });
            }
            var chorusUrl = filePath as ChorusUrl;
            IScanProvider scanProvider;
            if (null == chorusUrl)
            {
                scanProvider = new ScanProvider(_documentFilePath,
                    filePath,
                    chromSource, chromInfo.Times, transitions.ToArray(),
                    () => _document.Settings.MeasuredResults.LoadMSDataFileScanIds(filePath));
            }
            else
            {
                scanProvider = new ChorusScanProvider(_documentFilePath,
                    chorusUrl,
                    chromSource, chromInfo.Times, transitions.ToArray());
            }

            // Across all spectra at the peak retention time, find the one with max total 
            // intensity for the mz's of interest (ie the isotopic distribution) and note its drift time.
            var scanIndex = chromInfo.ScanIndexes != null
                ? MsDataFileScanHelper.FindScanIndex(chromInfo, apexRT.Value)
                : -1;
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
                throw new IOException(TextUtil.LineSeparate(Resources.DriftTimeFinder_HandleLoadScanException_Problem_using_results_to_populate_drift_time_library__, _dataFileScanHelperException.Message), _dataFileScanHelperException);
            }
            if (_progressMonitor != null && !ReferenceEquals(nodeGroup, _currentDisplayedTransitionGroupDocNode))
            {
                // Do this after scan load so first group after file switch doesn't seem laggy
                _progressStatus = _progressStatus.ChangeMessage(TextUtil.LineSeparate(filePath.GetFileName(), nodeGroup.ToString())).
                                     UpdatePercentCompleteProgress(_progressMonitor, _currentStep++, _totalSteps);
                _currentDisplayedTransitionGroupDocNode = nodeGroup;
            }
            EvaluateBestDriftTime(msLevel, libKey, tolerance, transitions);
            return true;
        }

        private void EvaluateBestDriftTime(int msLevel, LibKey libKey, float tolerance, List<TransitionFullScanInfo> transitions)
        {
            double? driftTime = null;
            double maxIntensity = 0;

            // Avoid picking MS2 drift times wildly different from MS1 times
            double? ms1DriftTimeBest;
            if ((msLevel == 2) && _ms1DriftTimes.ContainsKey(libKey))
            {
                ms1DriftTimeBest =
                    _ms1DriftTimes[libKey].OrderByDescending(p => p.Intensity)
                        .FirstOrDefault()
                        .DriftTime;
            }
            else
            {
                ms1DriftTimeBest = null;
            }

            const int maxHighEnergyDriftOffsetMsec = 2; // CONSIDER(bspratt): user definable? or dynamically set by looking at scan to scan drift delta? Or resolving power?
            foreach (var scan in _msDataFileScanHelper.MsDataSpectra.Where(scan => scan != null))
            {
                if (!scan.DriftTimeMsec.HasValue || !scan.Mzs.Any())
                    continue;
                if (ms1DriftTimeBest.HasValue &&
                    (scan.DriftTimeMsec.Value <
                     ms1DriftTimeBest.Value - maxHighEnergyDriftOffsetMsec ||
                     scan.DriftTimeMsec.Value >
                     ms1DriftTimeBest.Value + maxHighEnergyDriftOffsetMsec))
                    continue;

                // Get the total intensity for all transitions of current msLevel
                double totalIntensity = 0;
                foreach (var t in transitions)
                {
                    var mzPeak = t.ProductMz;
                    var halfwin = (t.ExtractionWidth ?? tolerance)/2;
                    var mzLow = mzPeak - halfwin;
                    var mzHigh = mzPeak + halfwin;
                    var first = Array.BinarySearch(scan.Mzs, mzLow);
                    if (first < 0)
                        first = ~first;
                    for (var i = first; i < scan.Mzs.Length; i++)
                    {
                        if (scan.Mzs[i] > mzHigh)
                            break;
                        totalIntensity += scan.Intensities[i];
                    }
                }
                if (maxIntensity < totalIntensity)
                {
                    driftTime = scan.DriftTimeMsec;
                    maxIntensity = totalIntensity;
                }
            }
            if (driftTime.HasValue)
            {
                var dict = (msLevel == 1) ? _ms1DriftTimes : _ms2DriftTimes;
                var result = new DriftTimeIntensityPair
                {
                    DriftTime = driftTime.Value,
                    Intensity = maxIntensity
                };
                List<DriftTimeIntensityPair> listPairs;
                if (!dict.TryGetValue(libKey, out listPairs))
                {
                    listPairs = new List<DriftTimeIntensityPair>();
                    dict.Add(libKey, listPairs);
                }
                listPairs.Add(result);
            }
        }

        private void HandleLoadScanException(Exception ex)
        {
            lock (this)
            {
                _dataFileScanHelperException = ex;
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