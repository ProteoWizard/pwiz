/*
 * Original author: Dario Amodei <jegertso .at .u.washington.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results
{
    public class OverlapDemultiplexer : AbstractDemultiplexer
    {
        public OverlapDemultiplexer(MsDataFileImpl file, SpectrumFilter filter)
            : base(file, filter)
        {
            _isoMapper = new OverlapIsolationWindowMapper();
            _overlapRegionsInApprox = 7;
        }

        public int _overlapRegionsInApprox { get; private set; }

        public override int NumScansBlock
        {
            get { return NumScansCycle * 3; }
        }

        public int NumScansCycle
        {
            get { return NumDeconvRegions - 1; }
        }

        protected override void InitializeSolver()
        {
            int maxOverlapComponents = _isoMapper.MaxDeconvInIsoWindow();
            int maxTransInSpectrum = _spectrumProcessor.MaxTransitions(maxOverlapComponents);
            _deconvHandler = new OverlapDeconvSolverHandler(NumDeconvRegions, 
                                                            NumScansBlock,
                                                            maxTransInSpectrum,
                                                            _overlapRegionsInApprox,
                                                            NumScansCycle);
            _solver = new OverlapLsSolver(_overlapRegionsInApprox,_overlapRegionsInApprox, maxTransInSpectrum);
        }
    }

    public class OverlapIsolationWindowMapper : AbstractIsoWindowMapper
    {
        protected override void FindDeconvRegions()
        {
            _deconvRegions.Clear();
            // Set the minimum size of an overlap region
            const double minDeconvWindow = 0.02;
            long minDeconvHash = IsoWindowHasher.Hash(minDeconvWindow);
            var isolationWindows = _isolationWindows;
            // Generate the set of overlap regions
            List<long> isoBoundaries = new List<long>(isolationWindows.Count * 2);
            foreach (var isoWindow in isolationWindows)
            {
                isoBoundaries.Add(isoWindow.Start);
                isoBoundaries.Add(isoWindow.Stop);
            }
            isoBoundaries = isoBoundaries.Distinct().ToList();

            isoBoundaries.Sort();
            for (int i = 0; i < isoBoundaries.Count - 1; ++i)
            {
                long widthRegion = isoBoundaries[i + 1] - isoBoundaries[i];
                long centerRegion = (isoBoundaries[i + 1] + isoBoundaries[i]) / 2;
                int windowIndex;
                if (widthRegion >= minDeconvHash && TryGetWindowFromMz(IsoWindowHasher.UnHash(centerRegion), out windowIndex))
                {
                    _deconvRegions.Add(new DeconvolutionRegion(isoBoundaries[i], isoBoundaries[i + 1], _deconvRegions.Count));
                }
            }

            // Now map each isolation window to its overlap regions
            foreach (var isoWin in isolationWindows) isoWin.DeconvRegions.Clear();
            var isoWinArray = isolationWindows.ToArray();
            // Sort the isolation windows by the start value
            Array.Sort(isoWinArray, (win, isoWin) => Comparer<long>.Default.Compare(win.Start, isoWin.Start));
            int overlapStartSearch = 0;
            foreach (IsoWin currentIso in isoWinArray)
            {
                for (int overlapIndex = overlapStartSearch; overlapIndex < _deconvRegions.Count(); ++overlapIndex)
                {
                    var currentOverlap = _deconvRegions[overlapIndex];
                    if (currentIso.Start >= currentOverlap.Stop)
                    {
                        overlapStartSearch = overlapIndex;
                        continue;
                    }
                    if (currentIso.Contains(currentOverlap)) currentIso.DeconvRegions.Add(currentOverlap);
                    if (currentOverlap.Start >= currentIso.Stop) break;
                }
            }
        }
    }

    public class OverlapDeconvSolverHandler : AbstractDeconvSolverHandler
    {
        private IEnumerable<int> _scansInDeconv;
        private int _centerIndex;
        private int _leftBoundary;
        private readonly int _cycleLength;
        private readonly int _overlapRegionsInApprox;

        public OverlapDeconvSolverHandler(int numDeconvWindows, int maxScans, int maxTransitions, int overlapRegions, int cycleLength):
            base(numDeconvWindows, maxScans, maxTransitions)
        {
            _overlapRegionsInApprox = overlapRegions;
            _cycleLength = cycleLength;
            _deconvBlock = new DeconvBlock(_overlapRegionsInApprox, _overlapRegionsInApprox, maxTransitions);
        }

        protected override void BuildDeconvBlock()
        {
            _deconvBlock.Clear();
            _deconvBlock.Solution.Resize(_overlapRegionsInApprox, NumTransitions);
            _centerIndex = Convert.ToInt32(DeconvIndices.Average());
            _leftBoundary = Math.Min(Math.Max(_centerIndex - _overlapRegionsInApprox / 2, 0), NumDeconvWindows - _overlapRegionsInApprox);
            // Find the scans that are closest to the deconvolution windows we are interested in 
            var maskAverages = new List<KeyValuePair<double, int>>(NumScans);
            for (int scanIndex = 0; scanIndex < _cycleLength; ++scanIndex)
            {
                // Average over the indices of the nonzero elements to find the "center of mass" of the deconvolution windows of this scan
                double currentAverage = Masks.Matrix.Row(scanIndex).Select((maskVal, index) => maskVal > 0 ? index : 0)
                                        .Where(index => index > 0).Average();
                double deviationFromCenter = currentAverage - _centerIndex;
                maskAverages.Add(new KeyValuePair<double, int>(deviationFromCenter, scanIndex));
            }
            maskAverages.Sort(CompareKvpAbs);
            // Pick the best few scans
            var bestMaskAverages = maskAverages.Where((kvp, index) => index < _overlapRegionsInApprox).ToList();
            bestMaskAverages.Sort(CompareKvp);
            _scansInDeconv = bestMaskAverages.Select(kvp => kvp.Value);
            foreach (var deconvScan in _scansInDeconv)
            {
                var scansOfWindow = new List<int>();
                var scanTimes = new List<double>();
                int currentScan = deconvScan;
                while (currentScan < NumScans)
                {
                    scansOfWindow.Add(currentScan);
                    if (!ScanTimes[currentScan].HasValue)
                    {
                        throw new InvalidDataException(string.Format(Resources.OverlapDeconvSolverHandler_BuildDeconvBlock_Missing_scan_time_value_on_scan__0___Scan_times_are_required_for_overlap_based_demultiplexing_, ScanNumbers[currentScan]));
                    }
                    scanTimes.Add(ScanTimes[currentScan].Value);
                    currentScan = currentScan + _cycleLength;
                }
                var interpolatedValues = new List<double>();
                for (int j = 0; j < NumTransitions; ++j)
                {
                    var scanIntensities = scansOfWindow.Select(row => BinnedData.Matrix[row, j]).ToList();
                    var interpolator = MathNet.Numerics.Interpolate.CubicSpline(scanTimes, scanIntensities);
                    if (CurrentScan >= NumScans || CurrentScan < 0)
                    {
                        throw new InvalidDataException(string.Format("Current scan does not fall within bounds on scan {0}", ScanNumbers[currentScan]));  // Not L10N
                    }
                    if (!ScanTimes[CurrentScan].HasValue)
                    {
                        throw new InvalidDataException(string.Format(Resources.OverlapDeconvSolverHandler_BuildDeconvBlock_Missing_scan_time_value_on_scan__0___Scan_times_are_required_for_overlap_based_demultiplexing_, ScanNumbers[currentScan]));
                    }
                    // ReSharper disable PossibleInvalidOperationException
                    double interpolatedValue = interpolator.Interpolate(ScanTimes[CurrentScan].Value);
                    // ReSharper restore PossibleInvalidOperationException
                    interpolatedValues.Add(interpolatedValue);
                }
                _deconvBlock.Add(Masks.Matrix.Row(deconvScan, _leftBoundary, _overlapRegionsInApprox).ToArray(),
                                 interpolatedValues);
            }
            if (_deconvBlock.Masks.Matrix.Rank() < _overlapRegionsInApprox)
            {
                throw new InvalidDataException(string.Format(Resources.OverlapDeconvSolverHandler_BuildDeconvBlock_Overlap_deconvolution_window_scheme_is_rank_deficient_at_scan__2___Rank_is__0__while_matrix_has_dimension__1____A_non_degenerate_overlapping_window_scheme_is_required_,
                                               _deconvBlock.Masks.Matrix.Rank(),  _overlapRegionsInApprox, ScanNumbers[NumScans/2]));
            }
        }

        protected int CompareKvp(KeyValuePair<double, int> a, KeyValuePair<double, int> b)
        {
            return a.Key.CompareTo(b.Key);
        }

        protected int CompareKvpAbs(KeyValuePair<double, int> a, KeyValuePair<double, int> b)
        {
            return Math.Abs(a.Key).CompareTo(Math.Abs(b.Key));
        }

        protected override void DeconvBlockToSolution()
        {
            // Not using LINQ due to speed concerns.
            int i = 0;
            Solution.Resize(DeconvIndices.Count,_deconvBlock.Solution.NumCols);
            foreach (int deconvIndex in DeconvIndices)
            {
                var deconvRow = _deconvBlock.Solution.Matrix.Row(deconvIndex - _leftBoundary); 
                Solution.Matrix.SetRow(i, deconvRow);
                ++i;
            }
        }
    }
}