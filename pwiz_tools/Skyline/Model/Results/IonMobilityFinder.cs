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
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
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
        private readonly IonMobilityWindowWidthCalculator _filterWindowWidthCalculator;
        private TransitionGroupDocNode _currentDisplayedTransitionGroupDocNode;
        private Dictionary<LibKey, IonMobilityFitList> _ms1IonMobilitiesPerLibKey;
        private Dictionary<LibKey, IonMobilityFitList> _ms2IonMobilitiesPerLibKey;
        private int _totalSteps;
        private int _currentStep;
        private readonly IProgressMonitor _progressMonitor;
        private IProgressStatus _progressStatus;
        private Exception _dataFileScanHelperException;
        private double _maxHighEnergyDriftOffsetMsec;
        private bool _useHighEnergyOffset;
        private double? _ms1IonMobilityBest;
        private eIonMobilityUnits _ionMobilityUnits = eIonMobilityUnits.none;
        private double _ms2IonMobilityFilterLow, _ms2IonMobilityFilterHigh; // For rejecting extreme MS2 high energy offset values

        private class IonMobilityFitList
        {
            public List<IonMobilityFit> IonMobilityFits { get; private set; } = new List<IonMobilityFit>();
            public IonMobilityFit BestIonMobilityFit { get; private set; }

            public void Add(IonMobilityFit fit)
            {
                if (fit != null)
                {
                    IonMobilityFits.Add(fit);
                    if (fit.BetterThan(BestIonMobilityFit))
                    {
                        BestIonMobilityFit = fit;
                    }
                }
            }
        }

        private class IonMobilityFit
        {
            public IonMobilityFit(int fileIndex, double rt, double im, double? ccs, double iDotP, double intensityM0, double intensityMminus1)
            {
                FileIndex = fileIndex;
                RetentionTime = rt;
                IonMobility = im;
                CCS = ccs;
                IdotP = iDotP;
                IntensityM0 = intensityM0;
                IntensityMminus1 = intensityMminus1;
            }

            public int FileIndex { get; }
            public double RetentionTime { get; } // RT at which this mobility was measured
            public double IonMobility { get; } 
            public double? CCS { get; set; }
            public double IdotP { get; }
            public double IntensityM0 { get; } // M0 intensity
            public double IntensityMminus1 { get; } //M-1 intensity

            private double RatioMm1ToM0 => IntensityM0 == 0 ? double.MaxValue : (IntensityMminus1/IntensityM0);

            // Return true if this is a better fit than other
            // First by is best idotp then best intensity
            // For very similar idotp, intensity is the tie breaker
            // If one has a suspiciously strong M-1 signal, prefer the other
            // N.B. retention time is not part of the sort
            public bool BetterThan(IonMobilityFit other)
            {
                if (other == null)
                {
                    return true; // Anything beats nothing
                }
                const double sketchyRatio = .25; // If M-1/M0 greater than this, we're probably actually in the middle of a different isotope envelope
                var sketchy = RatioMm1ToM0 > sketchyRatio;
                var sketchyOther = other.RatioMm1ToM0 > sketchyRatio; 
                if (sketchyOther != sketchy) 
                {
                    return !sketchy; // Take the one without the suspicious M-1 peak
                }

                if (SimilarIdotP(IdotP, other.IdotP))
                {
                    return IntensityM0 > other.IntensityM0;
                }

                return IdotP > other.IdotP;
            }

            public static bool SimilarIdotP(double iDotP, double otherIdotP)
            {
                return Math.Abs(iDotP - otherIdotP) <= .02; // iDotP within 2%
            }

            public override string ToString()
            {
                return $@"rt {RetentionTime} idotp {IdotP} iM0 {IntensityM0} iM01 {IntensityMminus1} mob {IonMobility}";
            }
        }

        /// <summary>
        /// Finds ion mobilities by examining loaded results in a document.
        /// </summary>
        /// <param name="document">The document to be inspected</param>
        /// <param name="documentFilePath">Aids in locating the raw files</param>
        /// <param name="filterWindowWidthCalculator">IM filter width calculator - not necessarily same as in document.Settings</param>
        /// <param name="progressMonitor">Optional progress monitor for this potentially long operation</param>
        public IonMobilityFinder(SrmDocument document, string documentFilePath, IonMobilityWindowWidthCalculator filterWindowWidthCalculator, IProgressMonitor progressMonitor)
        {
            _document = document;
            _documentFilePath = documentFilePath;
            _filterWindowWidthCalculator = filterWindowWidthCalculator;
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

            // Before we do anything else, make sure the raw files are present
            foreach (var f in fileInfos)
            {
                if (!ScanProvider.FileExists(_documentFilePath, f.FilePath))
                {
                    throw new FileNotFoundException(TextUtil.LineSeparate(Resources.IonMobilityFinder_ProcessMSLevel_Failed_using_results_to_populate_ion_mobility_library_,
                        string.Format(ResultsResources.ScanProvider_GetScans_The_data_file__0__could_not_be_found__either_at_its_original_location_or_in_the_document_or_document_parent_folder_,
                            f.FilePath)));
                }
            }

            using (_msDataFileScanHelper = new MsDataFileScanHelper(SetScans, HandleLoadScanException, true))
            {
                //
                // Avoid opening and re-opening raw files - make these the outer loop
                //

                _ms1IonMobilitiesPerLibKey = new Dictionary<LibKey, IonMobilityFitList>();
                _ms2IonMobilitiesPerLibKey = new Dictionary<LibKey, IonMobilityFitList>();
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
                    if (_ionMobilityUnits == eIonMobilityUnits.none)
                    {
                        _ionMobilityUnits = fileInfo.IonMobilityUnits;
                    }
                    else if (_ionMobilityUnits != fileInfo.IonMobilityUnits)
                    {
                        throw new IOException(TextUtil.LineSeparate(Resources.IonMobilityFinder_ProcessMSLevel_Failed_using_results_to_populate_ion_mobility_library_, ResultsResources.IonMobilityFinder_FindIonMobilityPeaks_mixed_ion_mobility_types_are_not_supported), _dataFileScanHelperException);
                    }
                    if (!ProcessFile(fileInfo))
                        return null; // User cancelled
                }
                // Find ion mobilities based on MS1 data
                foreach (var fitMS1 in _ms1IonMobilitiesPerLibKey)
                {
                    // Choose the ion mobility which gave the best fit to expected isotope envelope
                    // CONSIDER: average IM and CCS values that fall "near" the IM of best fit? Or consider them multiple conformers? (Only if at same RT, though)
                    var ms1IonMobilityFit = fitMS1.Value.BestIonMobilityFit;
                    double highEnergyIonMobilityValueOffset = 0;
                    if (_useHighEnergyOffset)
                    {
                        // Check for MS2 data to use for high energy offset
                        // At same RT in same file
                        IonMobilityFit ms2IonMobilityFit = null;
                        _ms2IonMobilitiesPerLibKey.TryGetValue(fitMS1.Key, out var ionMobilityMS2Fits);
                        if (ionMobilityMS2Fits != null)
                        {
                            ms2IonMobilityFit = ionMobilityMS2Fits.IonMobilityFits.Where(f => f.FileIndex == ms1IonMobilityFit.FileIndex).
                                OrderBy(f => f.RetentionTime).
                                FirstOrDefault(f => f.RetentionTime >= ms1IonMobilityFit.RetentionTime);
                        }
                        highEnergyIonMobilityValueOffset = ms2IonMobilityFit == null ? 0 : Math.Round(ms2IonMobilityFit.IonMobility - ms1IonMobilityFit.IonMobility, 6); // Excessive precision is just distracting noise TODO(bspratt) ask vendors what "excessive" means here
                    }
                    var value =  IonMobilityAndCCS.GetIonMobilityAndCCS(ms1IonMobilityFit.IonMobility, _ionMobilityUnits, ms1IonMobilityFit.CCS, highEnergyIonMobilityValueOffset);
                    measured[fitMS1.Key] = value;
                }
                // Check for data for which we have only MS2 to go on
                foreach (var im in _ms2IonMobilitiesPerLibKey)
                {
                    if (!_ms1IonMobilitiesPerLibKey.ContainsKey(im.Key))
                    {
                        // Only MS2 ion mobility values found, use that as a reasonable inference of MS1 ion mobility
                        var bestFit = im.Value.IonMobilityFits.OrderByDescending(p => p.IntensityM0).FirstOrDefault();
                        if (bestFit != null)
                        {
                            var ccs = bestFit.CCS;
                            if (!ccs.HasValue && _msDataFileScanHelper.ProvidesCollisionalCrossSectionConverter)
                            {
                                var mz = im.Key.PrecursorMz ?? GetMzFromDocument(im.Key);
                                ccs = _msDataFileScanHelper.CCSFromIonMobility(bestFit.IonMobility,
                                    mz, im.Key.Charge);
                            }
                            measured[im.Key] = IonMobilityAndCCS.GetIonMobilityAndCCS(bestFit.IonMobility, _ionMobilityUnits, ccs, null);
                        }
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
            var mzTolerance = (float)_document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var pair in _document.MoleculePrecursorPairs)
            {
                var nodePep = pair.NodePep;
                var nodeGroup = pair.NodeGroup;
                var libKey = nodeGroup.GetLibKey(_document.Settings, nodePep);
                // Across all replicates for this precursor, note the ion mobility at best isotope envelope fit
                for (var i = 0; i < results.Chromatograms.Count; i++)
                {
                    if (_progressMonitor != null && _progressMonitor.IsCanceled)
                        return false;

                    ChromatogramGroupInfo[] chromGroupInfos;
                    results.TryLoadChromatogram(i, nodePep, nodeGroup, mzTolerance, out chromGroupInfos);
                    foreach (var chromInfo in chromGroupInfos.Where(c => Equals(filePath, c.FilePath)))
                    {
                        if (!ProcessChromInfo(fileInfo, chromInfo, pair, nodeGroup, mzTolerance, libKey)) 
                            return false; // User cancelled
                    }
                }
            }
            return true;
        }

        private struct ChromatogramInfoAndExpectedProportion
        {
            public ChromatogramInfo ChromatogramInfo { get; }
            public double Proportion { get; }

            public ChromatogramInfoAndExpectedProportion(ChromatogramInfo chromatogramInfo, double proportion)
            {
                this.ChromatogramInfo = chromatogramInfo;
                this.Proportion = proportion;
            }
        }

        private bool ProcessChromInfo(ChromFileInfo fileInfo, ChromatogramGroupInfo chromGroupInfo, PeptidePrecursorPair pair,
            TransitionGroupDocNode nodeGroup, float mzTolerance, LibKey libKey)
        {
            if (chromGroupInfo.NumPeaks == 0)  // Due to data polarity mismatch, probably
                return true;
            Assume.IsTrue(chromGroupInfo.BestPeakIndex != -1);

            // Determine retention times for trying idotp for DT measurement, preferably using all MS1 M0 peaks - failing that
            // use MS2 and just find max signal
            var apexRTs = GetApexRTs(chromGroupInfo, true) ??
                GetApexRTs(chromGroupInfo, false);
            if (apexRTs == null || !apexRTs.Any())
            {
                return true;
            }

            bool WithinTolerance(double mz, ChromatogramInfo tp)
            {
                var halfWidth = (tp.ExtractionWidth ?? mzTolerance) / 2;
                return (mz - halfWidth) <= tp.ProductMz && (mz + halfWidth) >= tp.ProductMz;
            }

            Assume.IsTrue(chromGroupInfo.PrecursorMz.CompareTolerant(pair.NodeGroup.PrecursorMz, 1.0E-9f) == 0 , @"mismatch in precursor values");
            // Only use the fragment transitions currently enabled, but use all calculated precursor isotopes - map them to the chromInfo, note isotope envelope
            var transitionPointSets = new List<ChromatogramInfoAndExpectedProportion>(chromGroupInfo.TransitionPointSets.Count());
            if (pair.NodeGroup.HasIsotopeDist)
            {
                var nodeGroupIsotopeDist = pair.NodeGroup.IsotopeDist;
                foreach (var mzp in nodeGroupIsotopeDist.ExpectedPeaks)
                {
                    // Find the extracted MS1 chromatogram whose mz most closely matches
                    var tp = chromGroupInfo.TransitionPointSets.OrderBy(tps => Math.Abs(mzp.Mz - tps.ProductMz)).First();
                    if (WithinTolerance(mzp.Mz, tp))
                    {
                        // N.B. this might be the M-1 isotope, which is useful  - its expected proportion 0 helps avoid matching
                        // the middle of another isotope envelope where what we thought was our M0 is actually somebody else's M1
                        transitionPointSets.Add(new ChromatogramInfoAndExpectedProportion(tp, mzp.Proportion)); 
                    }
                }
            }
            foreach (var tp in chromGroupInfo.TransitionPointSets)
            {
                transitionPointSets.AddRange(nodeGroup.Transitions.Where(
                        t => !t.HasDistInfo && // We already added any nodes with isotope distribution info
                             WithinTolerance(t.Mz, tp))
                    .Select(t => new ChromatogramInfoAndExpectedProportion(tp,  0))); // No idotp contribution if no expected distribution
            }

            for (var msLevel = 1; msLevel <= 2; msLevel++)
            {
                if (!ProcessMSLevel(fileInfo, msLevel, transitionPointSets, chromGroupInfo, apexRTs, nodeGroup, libKey, mzTolerance))
                    return false; // User cancelled
            }
            return true;
        }

        private static List<double> GetApexRTs(ChromatogramGroupInfo chromGroupInfo, bool ms1Trans)
        {
            double? apexRT = null;
            List<double> m0RTs = null;
            float ms2Max = 0;
            var transPointSets = (ms1Trans
                ? chromGroupInfo.TransitionPointSets.Where(tp => tp.Source == ChromSource.ms1 && tp.PrecursorMz.Equals(tp.ProductMz)) // Get the chromatographic peaks for M0 isotope
                : chromGroupInfo.TransitionPointSets.Where(tp => tp.Source == ChromSource.fragment)).ToArray();
            if (ms1Trans && !transPointSets.Any())
            {
                // No exact precursor/fragment mz match for finding M0 peak
                var minDiff = double.MaxValue;
                ChromatogramInfo tpM0 = null;
                foreach (var tp in chromGroupInfo.TransitionPointSets.Where(tp => tp.Source == ChromSource.ms1))
                {
                    var diff = Math.Abs(tp.PrecursorMz - tp.ProductMz);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        tpM0 = tp;
                    }
                }
                if (tpM0 != null)
                {
                    transPointSets = new[] { tpM0 };
                }
            }
            foreach (var tp in transPointSets)
            {
                foreach (var peakInfo in tp.Peaks)
                {
                    if (ms1Trans)
                    {
                        m0RTs ??= new List<double>();
                        m0RTs.Add(peakInfo.RetentionTime); // Time of a chromatographic peak for M0 isotope
                    }
                    else if (peakInfo.Area > ms2Max)
                    {
                        apexRT = peakInfo.RetentionTime; // If no MS1 available, just look for max MS2
                        ms2Max = peakInfo.Area;
                    }
                }
            }

            if (ms1Trans)
                return m0RTs;

            return apexRT.HasValue ? new List<double>{apexRT.Value} : null;
        }

        private bool ProcessMSLevel(ChromFileInfo fileInfo, int msLevel, IEnumerable<ChromatogramInfoAndExpectedProportion> transitionPointSets,
            ChromatogramGroupInfo chromInfo, List<double> apexRTs, TransitionGroupDocNode nodeGroup, LibKey libKey, float mzTolerance)
        {
            var transitions = new List<TransitionFullScanInfo>();
            var chromSource = (msLevel == 1) ? ChromSource.ms1 : ChromSource.fragment;
            IList<float> times = null;
            var isotopeDist = new List<double>();
            foreach (var t in transitionPointSets.Where(t => t.ChromatogramInfo.Source == chromSource))
            {
                var chromatogramInfo = t.ChromatogramInfo;
                transitions.Add(new TransitionFullScanInfo
                {
                    Source = chromSource,
                    TimeIntensities =  chromatogramInfo.TimeIntensities,
                    PrecursorMz = chromInfo.PrecursorMz,
                    ProductMz = chromatogramInfo.ProductMz,
                    ExtractionWidth = chromatogramInfo.ExtractionWidth,
                });
                isotopeDist.Add(t.Proportion);
                times = chromatogramInfo.Times;
            }

            if (!transitions.Any())
            {
                return true; // Nothing to do at this ms level
            }

            var filePath = fileInfo.FilePath;
            IScanProvider scanProvider = new ScanProvider(_documentFilePath, filePath,
                chromSource, times, transitions.ToArray(), _document.Settings.MeasuredResults);

            // Across all IM spectra at each chromatogram peak, find the one with best iDotP
            // for the mz's of interest (ie the isotopic distribution) and note its ion mobility.
            // Then keep ion mobility of all those candidate peaks.
            // (N.B. Skyline 23.1.x and earlier just looked for max intensity summed across the
            // isotopic peaks, but that was easily mislead by unrelated isotope envelopes in other
            // IM bands)
            foreach (var rt in apexRTs)
            {
                var scanIndex = MsDataFileScanHelper.FindScanIndex(times, rt);  // Index into the scanProvider's table of scan times
                _msDataFileScanHelper.UpdateScanProvider(scanProvider, 0, scanIndex, null);
                _msDataFileScanHelper.MsDataSpectra = null; // Reset
                scanIndex = _msDataFileScanHelper.GetScanIndex(); // Index into the file itself
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
                EvaluateBestIonMobilityValue(fileInfo.FileIndex, msLevel, rt, libKey, mzTolerance, transitions, isotopeDist);
            }
            return true;
        }

        private void EvaluateBestIonMobilityValue(int fileIndex, int msLevel, double rt, LibKey libKey, float mzTolerance, List<TransitionFullScanInfo> transitions, List<double> expectedIsotopeProportions)
        {
            var isNegative = transitions[0].ProductMz.IsNegative;

            // Avoid picking MS2 ion mobility values wildly different from MS1 values
            if ((msLevel == 2) && _ms1IonMobilitiesPerLibKey.TryGetValue(libKey, out var ionMobilityFitsMS1))
            {
                var bestFitMS1 = ionMobilityFitsMS1.BestIonMobilityFit;
                _ms1IonMobilityBest = bestFitMS1.IonMobility; 
                var imMS1 = _ms1IonMobilityBest ?? 0;
                switch (_ionMobilityUnits)
                {
                    case eIonMobilityUnits.drift_time_msec:
                        _ms2IonMobilityFilterHigh = imMS1; // Fragments go faster, not slower
                        _ms2IonMobilityFilterLow = imMS1 - _maxHighEnergyDriftOffsetMsec; // But not a lot faster
                        break;
                    case eIonMobilityUnits.inverse_K0_Vsec_per_cm2:
                        // Within 5% - arbitrary value, normally not used with Bruker data, anyway
                        _ms2IonMobilityFilterHigh = imMS1*1.05;
                        _ms2IonMobilityFilterLow = imMS1*.95;
                        break;
                    // case eIonMobilityUnits.compensation_V: Not a meaningful concept for FAIMS
                }
            }
            else
            {
                _ms1IonMobilityBest = null;
            }

            var indexM0 = expectedIsotopeProportions[0] == 0 && expectedIsotopeProportions.Count > 1 ? 1 : 0;

            var isotopeIntensitiesPerIM = new Dictionary<double, double[]>();
            foreach (var scan in _msDataFileScanHelper.MsDataSpectra.Where(scan => scan != null))
            {
                var isThreeArrayFormat = scan.IonMobilities != null;
                Assume.IsTrue(isNegative == scan.NegativeCharge);  // It would be strange if associated scan did not have same polarity as transitions

                if (!isThreeArrayFormat)
                {
                    if (!scan.IonMobility.HasValue || !scan.Mzs.Any())
                        continue;
                    if (IsExtremeMs2Value(scan.IonMobility.Mobility.Value))
                        continue;
                    var ionMobility = scan.IonMobility.Mobility ?? 0;
                    var seenIM = isotopeIntensitiesPerIM.TryGetValue(ionMobility, out var isotopeIntensitiesThisIM);
                    if (!seenIM)
                    {
                        isotopeIntensitiesThisIM = new double[transitions.Count];
                    }
                    for (var t = 0; t < transitions.Count; t++)
                    {
                        var mzHigh = FindRangeMz(mzTolerance, transitions[t], scan, out var first);
                        for (var i = first; i < scan.Mzs.Length; i++)
                        {
                            if (scan.Mzs[i] > mzHigh)
                                break;
                            isotopeIntensitiesThisIM[t] += scan.Intensities[i];
                        }
                    }
                    if (!seenIM && isotopeIntensitiesThisIM.Any(intensity => intensity != 0))
                    {
                        isotopeIntensitiesPerIM.Add(ionMobility, isotopeIntensitiesThisIM);
                    }
                }
                else // 3-array IMS format
                {
                    // Get the intensity for all transitions of current msLevel
                    for (var t = 0; t < transitions.Count; t++)
                    {
                        var mzHigh = FindRangeMz(mzTolerance, transitions[t], scan, out var first);
                        for (var i = first; i < scan.Mzs.Length; i++)
                        {
                            if (scan.Mzs[i] > mzHigh)
                                break;
                            var im = scan.IonMobilities[i];
                            if (im == 0 || IsExtremeMs2Value(im))
                                continue;

                            var intensityThisMzAndIM = scan.Intensities[i];
                            if (intensityThisMzAndIM == 0)
                            {
                                continue;
                            }
                            if (!isotopeIntensitiesPerIM.TryGetValue(im, out var isotopeIntensitiesThisIM))
                            {
                                isotopeIntensitiesThisIM = new double[transitions.Count];
                                isotopeIntensitiesPerIM.Add(im, isotopeIntensitiesThisIM);
                            }
                            isotopeIntensitiesThisIM[t] += intensityThisMzAndIM;
                        }
                    }
                }
            }

            if (!isotopeIntensitiesPerIM.Any())
            {
                return; // No signal
            }

            // Now check idotp, use total intensity as a tie breaker
            // Note that we don't include the M-1 value if it's zero, because this
            // would tend to reject good fits that happen to overlap the tail of some other
            // isotope envelope. Note that M-1 can be nonzero for heavy labeled ions
            IonMobilityFit bestFit = null;
            var statExpectedIsotopeProportions = new Statistics(expectedIsotopeProportions.Skip(indexM0));

            // Use the same IM window size as in chromatogram extraction
            var windowedIsotopeIntensitiesPerIM = new List<KeyValuePair<double, double[]>>();
            var observedIMs = isotopeIntensitiesPerIM.Keys.ToArray();
            observedIMs.Sort();
            var imMax = observedIMs.LastOrDefault();
            for (var indexIM = 0; indexIM < observedIMs.Length; indexIM++)
            {
                var observedIM = observedIMs[indexIM];
                var ionMobilityHalfWindow = _filterWindowWidthCalculator.WidthAt(observedIM, imMax)/2;
                if (ionMobilityHalfWindow <= 0)
                {
                    throw new IOException(TextUtil.LineSeparate(Resources.IonMobilityFinder_ProcessMSLevel_Failed_using_results_to_populate_ion_mobility_library_, 
                        ResultsResources.IonMobilityFinder_EvaluateBestIonMobilityValue_need_a_value_for_Transition_Settings___ion_mobility_filtering___Window_type), null);
                }
                var isotopeIntensities = isotopeIntensitiesPerIM[observedIM];
                var windowedIsotopeIntensities = new double[isotopeIntensities.Length];
                isotopeIntensities.CopyTo(windowedIsotopeIntensities,0);
                int indexLeft, indexRight;
                for (indexLeft = indexIM - 1; indexLeft >= 0 && Math.Abs(observedIM - observedIMs[indexLeft]) <= ionMobilityHalfWindow;)
                {
                    indexLeft--;
                }

                for (indexRight = indexIM + 1; indexRight < observedIMs.Length && Math.Abs(observedIM - observedIMs[indexRight]) <= ionMobilityHalfWindow;)
                {
                    indexRight++;
                }

                // Note the IM of the most intense point within the window (these peaks are not symmetrical)
                // TODO:(bspratt) preserve that asymmetry information
                var peakIM = observedIM;
                var intensityAtPeakIM = isotopeIntensities.Skip(indexM0).Sum();

                for (var indexNeighborIM = indexLeft + 1; indexNeighborIM < indexRight; indexNeighborIM++)
                {
                    if (indexNeighborIM != indexIM)
                    {
                        var neighborIM = observedIMs[indexNeighborIM];
                        var isotopeIntensitiesNeighbor = isotopeIntensitiesPerIM[neighborIM];
                        for (var isotopeIndex = 0; isotopeIndex < isotopeIntensities.Length; isotopeIndex++)
                        {
                            windowedIsotopeIntensities[isotopeIndex] += isotopeIntensitiesNeighbor[isotopeIndex];
                        }
                        var intensityAtNeighborIM = isotopeIntensitiesNeighbor.Skip(indexM0).Sum();
                        if (intensityAtNeighborIM > intensityAtPeakIM)
                        {
                            intensityAtPeakIM = intensityAtNeighborIM;
                            peakIM = neighborIM;
                        }
                    }
                }

                windowedIsotopeIntensitiesPerIM.Add(new KeyValuePair<double, double[]>(peakIM, windowedIsotopeIntensities));
            }


            if (statExpectedIsotopeProportions.Sum() != 0)
            {
                foreach (var isotopeIntensitiesThisIM in windowedIsotopeIntensitiesPerIM)
                {
                    // Reject anything with no signal where at least 10% of the distribution is expected
                    // (It's hard to pick apart excessive signal from interference, but lack of signal is an obvious hint)
                    if (!expectedIsotopeProportions.Where((proportion, i) => proportion >= 0.10 &&
                                                                             isotopeIntensitiesThisIM.Value[i] == 0).Any())
                    {
                        // Now compare fit to isotopic envelope
                        var statDistributionThisIM = new Statistics(isotopeIntensitiesThisIM.Value.Skip(indexM0));
                        var isotopeDotProduct = statExpectedIsotopeProportions.Angle(statDistributionThisIM.NormalizeUnit()); // statExpectedIsotopeProportions is already normalized to unit vector
                        if (!double.IsNaN(isotopeDotProduct) && isotopeDotProduct >= 0)
                        {
                            var fit = new IonMobilityFit(fileIndex, rt, isotopeIntensitiesThisIM.Key, null, // We'll calculate CCS later
                                isotopeDotProduct, isotopeIntensitiesThisIM.Value[indexM0], indexM0 > 0 ? isotopeIntensitiesThisIM.Value[indexM0-1] : 0);
                            if (fit.BetterThan(bestFit)) // IonMobilityFit.Compare sorts best->worst
                            {
                                bestFit = fit; // This is a better fit than whatever we had, if any
                            }
                        }
                    }
                }
            }

            if (bestFit == null)
            {
                // No isotope envelope matches, fall back to just looking for IM with max total signal
                double bestMatchIntensityM0 = 0;
                double? bestMatchIM = null;
                foreach (var isotopeIntensitiesThisIM in isotopeIntensitiesPerIM)
                {
                    var intensityM0 = isotopeIntensitiesThisIM.Value[indexM0];
                    if (intensityM0 > bestMatchIntensityM0)
                    {
                        bestMatchIM = isotopeIntensitiesThisIM.Key;
                        bestMatchIntensityM0 = intensityM0;
                    }
                }

                if (bestMatchIM.HasValue)
                {
                    bestFit = new IonMobilityFit(fileIndex, rt, bestMatchIM.Value, null, // CCS calculated later
                        0, bestMatchIntensityM0, 0);
                }
            }

            if (bestFit != null)
            {
                var dict = (msLevel == 1) ? _ms1IonMobilitiesPerLibKey : _ms2IonMobilitiesPerLibKey;
                if (msLevel == 1 && _msDataFileScanHelper.ProvidesCollisionalCrossSectionConverter)
                {
                    bestFit.CCS  = _msDataFileScanHelper.CCSFromIonMobility(bestFit.IonMobility, transitions.First().PrecursorMz, libKey.Charge);
                }

                if (!dict.TryGetValue(libKey, out var ionMobilityFitList))
                {
                    ionMobilityFitList = new IonMobilityFitList();
                    dict.Add(libKey, ionMobilityFitList);
                }
                ionMobilityFitList.Add(bestFit);
            }
        }

        private static SignedMz FindRangeMz(float tolerance, TransitionFullScanInfo t, MsDataSpectrum scan, out int first)
        {
            var mzPeak = t.ProductMz;
            var halfwin = (t.ExtractionWidth ?? tolerance) / 2;
            var mzLow = mzPeak - halfwin;
            var mzHigh = mzPeak + halfwin;
            first = Array.BinarySearch(scan.Mzs, mzLow);
            if (first < 0)
                first = ~first;
            return mzHigh;
        }

        // Ignore proposed fragment IM values that are too far away from the parent IM
        private bool IsExtremeMs2Value(double im)
        {
            if ((_ms1IonMobilityBest ?? 0) == 0)
            {
                return false; // No MS1 to compare with
            }

            if (_ionMobilityUnits == eIonMobilityUnits.compensation_V)
            {
                return false;  // Makes no sense in FAIMS
            }

            // These limits are set in EvaluateBestIonMobilityValue()
            return im <_ms2IonMobilityFilterLow || im > _ms2IonMobilityFilterHigh;
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
