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
        private TransitionGroupDocNode _currentDisplayedTransitionGroupDocNode;
        private Dictionary<LibKey, List<IonMobilityFit>> _ms1IonMobilities;
        private Dictionary<LibKey, List<IonMobilityFit>> _ms2IonMobilities;
        private int _totalSteps;
        private int _currentStep;
        private readonly IProgressMonitor _progressMonitor;
        private IProgressStatus _progressStatus;
        private Exception _dataFileScanHelperException;
        private double _maxHighEnergyDriftOffsetMsec;
        private bool _useHighEnergyOffset;
        private IonMobilityValue _ms1IonMobilityBest;
        private double _ms2IonMobilityFilterLow, _ms2IonMobilityFilterHigh; // For rejecting extreme MS2 high energy offset values

        private readonly struct IonMobilityFit : IComparable<IonMobilityFit>
        {
            public IonMobilityFit(IonMobilityAndCCS ionMobility, double iDotP, double intensity)
            {
                IonMobility = ionMobility;
                IdotP = iDotP;
                Intensity = intensity;
            }

            public IonMobilityAndCCS IonMobility { get; }
            public double IdotP { get; }
            public double Intensity { get; }

            // Sort order is best idotp then best intensity
            // FOr very similar idotp, intensity is the tie breaker
            public int CompareTo(IonMobilityFit other)
            {
                var similar = SimilarIdotP(IdotP, other.IdotP);

                return similar ? Intensity.CompareTo(other.Intensity) : IdotP.CompareTo(other.IdotP);
            }

            public static bool SimilarIdotP(double iDotP, double otherIdotP)
            {
                return (iDotP == 0 && otherIdotP == 0) || // Avoids divide by zero
                       Math.Abs(iDotP - otherIdotP) / (iDotP + otherIdotP) <= .02; // iDotP within 1%
            }

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

                _ms1IonMobilities = new Dictionary<LibKey, List<IonMobilityFit>>();
                _ms2IonMobilities = new Dictionary<LibKey, List<IonMobilityFit>>();
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
                    // Choose the ion mobility which gave the best fit to expected isotope envelope
                    // CONSIDER: average IM and CCS values that fall "near" the IM of best fit? Or consider them multiple conformers?
                    var ms1IonMobility = dt.Value.Max().IonMobility;
                    double highEnergyIonMobilityValueOffset = 0;
                    if (_useHighEnergyOffset)
                    {
                    // Check for MS2 data to use for high energy offset
                    List<IonMobilityFit> listDt;
                    var ms2IonMobility = _ms2IonMobilities.TryGetValue(dt.Key, out listDt)
                        ? listDt.Max().IonMobility
                        : ms1IonMobility;
                        highEnergyIonMobilityValueOffset = Math.Round(ms2IonMobility.IonMobility.Mobility.Value - ms1IonMobility.IonMobility.Mobility.Value, 6); // Excessive precision is just distracting noise TODO(bspratt) ask vendors what "excessive" means here
                    }
                    var value =  IonMobilityAndCCS.GetIonMobilityAndCCS(ms1IonMobility.IonMobility, ms1IonMobility.CollisionalCrossSectionSqA, highEnergyIonMobilityValueOffset);
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
                    results.TryLoadChromatogram(i, nodePep, nodeGroup, tolerance, out chromGroupInfos);
                    foreach (var chromInfo in chromGroupInfos.Where(c => Equals(filePath, c.FilePath)))
                    {
                        if (!ProcessChromInfo(fileInfo, chromInfo, pair, nodeGroup, tolerance, libKey)) 
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

            bool WithinTolerance(double mz, ChromatogramInfo tp)
            {
                var halfWidth = (tp.ExtractionWidth ?? tolerance) / 2;
                return (mz - halfWidth) <= tp.ProductMz && (mz + halfWidth) >= tp.ProductMz;
            }

            Assume.IsTrue(chromInfo.PrecursorMz.CompareTolerant(pair.NodeGroup.PrecursorMz, 1.0E-9f) == 0 , @"mismatch in precursor values");
            // Only use the fragment transitions currently enabled, but use all calculated precursor isotopes - map them to the chromInfo, note isotope envelope
            var transitionPointSets = new List<ChromatogramInfoAndExpectedProportion>(chromInfo.TransitionPointSets.Count());
            if (pair.NodeGroup.HasIsotopeDist)
            {
                var nodeGroupIsotopeDist = pair.NodeGroup.IsotopeDist;
                foreach (var mzp in nodeGroupIsotopeDist.ExpectedPeaks)
                {
                    if (mzp.Proportion > 0) // Avoid the M-1 peak
                    {
                        // Find the extracted MS1 chromatogram whose mz most closely matches
                        var tp = chromInfo.TransitionPointSets.OrderBy(tps => Math.Abs(mzp.Mz - tps.ProductMz)).First();
                        if (WithinTolerance(mzp.Mz, tp))
                        {
                            transitionPointSets.Add(new ChromatogramInfoAndExpectedProportion(tp, mzp.Proportion));
                        }
                    }
                }
            }
            foreach (var tp in chromInfo.TransitionPointSets)
            {
                transitionPointSets.AddRange(nodeGroup.Transitions.Where(
                        t => !t.HasDistInfo && // We already added any nodes with isotope distribution info
                             WithinTolerance(t.Mz, tp))
                    .Select(t => new ChromatogramInfoAndExpectedProportion(tp,  0))); // No idotp contribution if no expected distribution
            }

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

        private bool ProcessMSLevel(ChromFileInfo fileInfo, int msLevel, IEnumerable<ChromatogramInfoAndExpectedProportion> transitionPointSets,
            ChromatogramGroupInfo chromInfo, double? apexRT, TransitionGroupDocNode nodeGroup, LibKey libKey, float tolerance)
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

            // Across all spectra at the peak retention time, find the one with best iDotP
            // for the mz's of interest (ie the isotopic distribution) and note its ion mobility.
            // (N.B. Skyline 23.1.x and earlier just looked for max intensity summed across the
            // isotopic peaks, but that was easily mislead by unrelated isotope envelopes in other
            // IM bands)
            var scanIndex = MsDataFileScanHelper.FindScanIndex(times, apexRT.Value);
            _msDataFileScanHelper.UpdateScanProvider(scanProvider, 0, scanIndex, null);
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
            EvaluateBestIonMobilityValue(msLevel, libKey, tolerance, transitions, isotopeDist);
            return true;
        }

        private void EvaluateBestIonMobilityValue(int msLevel, LibKey libKey, float tolerance, List<TransitionFullScanInfo> transitions, List<double> expectedIsotopeProportions)
        {
            var ionMobilityValue = IonMobilityValue.EMPTY;
            var isNegative = transitions[0].ProductMz.IsNegative;

            // Avoid picking MS2 ion mobility values wildly different from MS1 values
            if ((msLevel == 2) && _ms1IonMobilities.TryGetValue(libKey, out var mobility))
            {
                _ms1IonMobilityBest = mobility.Max().IonMobility.IonMobility;
                var imMS1 = _ms1IonMobilityBest.Mobility ?? 0;
                switch (_ms1IonMobilityBest.Units)
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
                _ms1IonMobilityBest = IonMobilityValue.EMPTY;
            }

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
                    if (!isotopeIntensitiesPerIM.TryGetValue(scan.IonMobility.Mobility??0, out var isotopeIntensitiesThisIM))
                    {
                        isotopeIntensitiesThisIM = new double[transitions.Count];
                        isotopeIntensitiesPerIM.Add(scan.IonMobility.Mobility??0, isotopeIntensitiesThisIM);
                    }
                    for (var t = 0; t < transitions.Count; t++)
                    {
                        var mzHigh = FindRangeMz(tolerance, transitions[t], scan, out var first);
                        for (var i = first; i < scan.Mzs.Length; i++)
                        {
                            if (scan.Mzs[i] > mzHigh)
                                break;
                            isotopeIntensitiesThisIM[t] += scan.Intensities[i];
                        }
                    }
                }
                else // 3-array IMS format
                {
                    // Get the intensity for all transitions of current msLevel
                    for (var t = 0; t < transitions.Count; t++)
                    {
                        var mzHigh = FindRangeMz(tolerance, transitions[t], scan, out var first);
                        for (var i = first; i < scan.Mzs.Length; i++)
                        {
                            if (scan.Mzs[i] > mzHigh)
                                break;
                            var im = scan.IonMobilities[i];
                            if (IsExtremeMs2Value(im))
                                continue;

                            var intensityThisMzAndIM = scan.Intensities[i];
                            if (!isotopeIntensitiesPerIM.TryGetValue(im, out var isotopeIntensitiesThisIM))
                            {
                                isotopeIntensitiesThisIM = new double[transitions.Count];
                                isotopeIntensitiesPerIM.Add(im, isotopeIntensitiesThisIM);
                            }
                            isotopeIntensitiesThisIM[t] += scan.Intensities[i];
                        }
                    }
                }
            }

            if (!isotopeIntensitiesPerIM.Any())
            {
                return; // No signal
            }

            // Now check idotp, use total intensity as a tie breaker
            var bestCorrelation = -1.0;
            double? bestIM = null;
            double bestIntensity = 0;
            var statExpectedIsotopeProportions = new Statistics(expectedIsotopeProportions);
            // We'll do a little smoothing by adding in the immediately adjacent IM channels
            // N.B. this assumes no completely empty  ion mobility channels next to channels of interest,
            // and that we don't expect to find the IM sweet spot at the edges of the overall IM range.
            // For MS2 the IMxMz space can be very sparse so we don't attempt smoothing
            var nPointAcrossPeak = (msLevel == 1) ? 5 : 1;
            var margin = nPointAcrossPeak / 2;
            var observedMobilities = isotopeIntensitiesPerIM.Keys.ToArray();
            observedMobilities.Sort();
            var localSummedDistributions = new double[transitions.Count];
            for (var imIndex = margin; imIndex < observedMobilities.Length- margin; imIndex++)
            {
                var edgeIndex = imIndex - margin;
                isotopeIntensitiesPerIM[observedMobilities[edgeIndex]].CopyTo(localSummedDistributions, 0);
                for (var localIndex = 1; localIndex < nPointAcrossPeak; localIndex++)
                {
                    var isotopeIntensities = isotopeIntensitiesPerIM[observedMobilities[edgeIndex+localIndex]];
                    for (var i = 0; i < transitions.Count; i++)
                    {
                        localSummedDistributions[i] += isotopeIntensities[i];
                    }
                }
                var statDistributionThisIM = new Statistics(localSummedDistributions);
                var isotopeDotProduct = statExpectedIsotopeProportions.NormalizedContrastAngleSqrt(statDistributionThisIM);
                if (!double.IsNaN(isotopeDotProduct))
                {
                    var intensity = statDistributionThisIM.Sum();
                    if (intensity > 0)
                    {
                        var similar = IonMobilityFit.SimilarIdotP(isotopeDotProduct,bestCorrelation); // iDotP within 1%
                        if (similar ? bestIntensity < intensity : isotopeDotProduct > bestCorrelation)
                        {
                            bestCorrelation = isotopeDotProduct;
                            bestIM = observedMobilities[imIndex];
                            bestIntensity = intensity;
                        }
                    }
                }
            }

            if (bestIM.HasValue)
            {
                ionMobilityValue = IonMobilityValue.GetIonMobilityValue(bestIM, _msDataFileScanHelper.ScanProvider.IonMobilityUnits);
                var dict = (msLevel == 1) ? _ms1IonMobilities : _ms2IonMobilities;
                var ccs = msLevel == 1 && _msDataFileScanHelper.ProvidesCollisionalCrossSectionConverter ? _msDataFileScanHelper.CCSFromIonMobility(ionMobilityValue, transitions.First().PrecursorMz, libKey.Charge) : null;
                var result = new IonMobilityFit(IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobilityValue, ccs, 0),
                    bestCorrelation,
                    bestIntensity);
                List<IonMobilityFit> listPairs;
                if (!dict.TryGetValue(libKey, out listPairs))
                {
                    listPairs = new List<IonMobilityFit>();
                    dict.Add(libKey, listPairs);
                }
                listPairs.Add(result);
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
            if ((_ms1IonMobilityBest.Mobility ?? 0) == 0)
            {
                return false; // No MS1 to compare with
            }

            if (_ms1IonMobilityBest.Units == eIonMobilityUnits.compensation_V)
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
