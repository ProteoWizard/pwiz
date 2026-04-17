/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Chromatography
{
    /// <summary>
    /// Continuous Wavelet Transform (CWT) consensus peak detection for DIA proteomics.
    ///
    /// Uses the Mexican Hat (Ricker) wavelet as a matched filter for Gaussian-like
    /// chromatographic peaks. Multi-transition consensus -- the pointwise median of
    /// CWT coefficients across all fragment XICs -- is only high where the majority
    /// of transitions simultaneously exhibit peak-like shapes, naturally rejecting
    /// single-fragment interference.
    ///
    /// Ported from osprey-chromatography/src/cwt.rs.
    /// </summary>
    public static class CwtPeakDetector
    {
        /// <summary>Minimum number of kernel points.</summary>
        public const int MIN_KERNEL_POINTS = 11;

        /// <summary>
        /// Boundary expansion factor. Zero-crossings are at +/-sigma (~68% coverage);
        /// multiply by this factor to get ~95% coverage (+/-2 sigma for Gaussian peaks).
        /// </summary>
        public const double COVERAGE_FACTOR = 2.0;

        /// <summary>
        /// Valley guard threshold: stop extending boundaries when the raw reference
        /// signal rises more than 5% of apex above a running minimum.
        /// </summary>
        public const double VALLEY_THRESHOLD = 0.05;

        /// <summary>Minimum allowed CWT scale (sigma) in scan-index units.</summary>
        public const double MIN_SCALE = 2.0;

        /// <summary>Maximum allowed CWT scale (sigma) in scan-index units.</summary>
        public const double MAX_SCALE = 20.0;

        /// <summary>Default fallback scale when no FWHM can be estimated.</summary>
        public const double DEFAULT_SCALE = 4.0;

        /// <summary>Gaussian FWHM-to-sigma conversion factor (FWHM = 2.355 * sigma).</summary>
        private const double FWHM_TO_SIGMA = 2.355;

        /// <summary>
        /// Generate a discrete Mexican Hat (Ricker) wavelet kernel.
        ///
        /// The Mexican Hat wavelet is the negative normalized second derivative of a
        /// Gaussian, acting as a matched filter for Gaussian-like chromatographic peaks:
        ///
        ///   psi(t) = (2 / sqrt(3*sigma) * pi^(1/4)) * (1 - (t/sigma)^2) * exp(-t^2 / (2*sigma^2))
        ///
        /// The kernel is zero-mean corrected to ensure pure wavelet behavior (zero
        /// response to constant signals).
        /// </summary>
        /// <param name="sigma">Scale parameter in scan-index units.</param>
        /// <param name="kernelRadius">Points on each side of center. Total size = 2*radius + 1.</param>
        /// <returns>Zero-mean corrected Mexican Hat wavelet kernel.</returns>
        public static double[] MexicanHatKernel(double sigma, int kernelRadius)
        {
            int len = 2 * kernelRadius + 1;
            double[] kernel = new double[len];
            double center = kernelRadius;

            double norm = 2.0 / (Math.Sqrt(3.0 * sigma) * Math.Pow(Math.PI, 0.25));

            for (int i = 0; i < len; i++)
            {
                double t = (i - center) / sigma;
                kernel[i] = norm * (1.0 - t * t) * Math.Exp(-0.5 * t * t);
            }

            // Zero-mean correction: numerical discretization can leave a tiny DC offset
            double mean = 0.0;
            for (int i = 0; i < len; i++)
                mean += kernel[i];
            mean /= len;

            for (int i = 0; i < len; i++)
                kernel[i] -= mean;

            return kernel;
        }

        /// <summary>
        /// Convolve a signal with a kernel using "same" output size.
        ///
        /// Output length equals input length. Edges are zero-padded: points beyond
        /// the signal boundary contribute 0. Direct convolution is used because
        /// typical XIC lengths (30-200 scans) and kernel sizes (11-51 points) make
        /// O(N*K) trivially fast.
        /// </summary>
        /// <param name="signal">Input signal array.</param>
        /// <param name="kernel">Convolution kernel.</param>
        /// <returns>Convolved output with the same length as the input signal.</returns>
        public static double[] Convolve(double[] signal, double[] kernel)
        {
            int n = signal.Length;
            int k = kernel.Length;
            if (n == 0 || k == 0)
                return new double[n];

            int halfK = k / 2;
            double[] output = new double[n];

            for (int i = 0; i < n; i++)
            {
                double sum = 0.0;
                for (int j = 0; j < k; j++)
                {
                    int signalIdx = i + j - halfK;
                    if (signalIdx >= 0 && signalIdx < n)
                        sum += signal[signalIdx] * kernel[j];
                }
                output[i] = sum;
            }

            return output;
        }

        /// <summary>
        /// Estimate the CWT scale parameter (sigma) from fragment XICs.
        ///
        /// Estimates sigma from the median FWHM across fragment XICs that have
        /// detectable signal. The FWHM is measured in scan-index units and converted
        /// via sigma = fwhm / 2.355 (Gaussian FWHM = 2.355 * sigma).
        ///
        /// Falls back to 4.0 scans if no FWHM can be estimated.
        /// </summary>
        /// <param name="xics">
        /// Fragment XICs. Each element is (fragmentIndex, rts, intensities) where
        /// rts and intensities are parallel arrays.
        /// </param>
        /// <returns>Estimated scale parameter clamped to [MIN_SCALE, MAX_SCALE].</returns>
        public static double EstimateScale(List<XicData> xics)
        {
            List<double> fwhmValues = new List<double>();

            foreach (XicData xic in xics)
            {
                double[] intensities = xic.Intensities;
                if (intensities.Length < 5)
                    continue;

                // Find apex
                int apexIdx = 0;
                double apexVal = intensities[0];
                for (int i = 1; i < intensities.Length; i++)
                {
                    if (intensities[i] > apexVal)
                    {
                        apexIdx = i;
                        apexVal = intensities[i];
                    }
                }

                if (apexVal <= 0.0)
                    continue;

                double halfMax = apexVal / 2.0;

                // Walk left to find half-height crossing
                double leftIdxF = double.NaN;
                for (int i = apexIdx; i >= 1; i--)
                {
                    if (intensities[i] >= halfMax && intensities[i - 1] < halfMax)
                    {
                        double denom = intensities[i] - intensities[i - 1];
                        if (denom > 1e-30)
                        {
                            double frac = (intensities[i] - halfMax) / denom;
                            leftIdxF = i - frac;
                        }
                        else
                        {
                            leftIdxF = i;
                        }
                        break;
                    }
                }

                // Walk right to find half-height crossing
                double rightIdxF = double.NaN;
                for (int i = apexIdx; i < intensities.Length - 1; i++)
                {
                    if (intensities[i] >= halfMax && intensities[i + 1] < halfMax)
                    {
                        double denom = intensities[i] - intensities[i + 1];
                        if (denom > 1e-30)
                        {
                            double frac = (intensities[i] - halfMax) / denom;
                            rightIdxF = i + frac;
                        }
                        else
                        {
                            rightIdxF = i;
                        }
                        break;
                    }
                }

                if (!double.IsNaN(leftIdxF) && !double.IsNaN(rightIdxF))
                {
                    double fwhm = rightIdxF - leftIdxF;
                    if (fwhm > 1.0)
                        fwhmValues.Add(fwhm);
                }
            }

            if (fwhmValues.Count == 0)
                return DEFAULT_SCALE;

            // Median FWHM
            double medianFwhm = SmallMedian(fwhmValues);

            // sigma = FWHM / 2.355 (Gaussian relationship)
            double sigma = medianFwhm / FWHM_TO_SIGMA;
            return Math.Max(MIN_SCALE, Math.Min(MAX_SCALE, sigma));
        }

        /// <summary>
        /// Compute the pointwise median of CWT coefficients across transitions.
        ///
        /// At each scan index, takes the median across all fragment CWT signals.
        /// The consensus is only high where the majority of transitions simultaneously
        /// exhibit peak-like shapes; single-fragment interference is naturally rejected.
        /// </summary>
        /// <param name="cwtCoeffs">CWT coefficient arrays, one per fragment.</param>
        /// <param name="nScans">Number of scans (all arrays must have this length).</param>
        /// <returns>Pointwise median consensus signal.</returns>
        public static double[] ConsensusMedianCwt(double[][] cwtCoeffs, int nScans)
        {
            int nFrags = cwtCoeffs.Length;
            double[] consensus = new double[nScans];
            double[] buf = new double[nFrags];

            for (int s = 0; s < nScans; s++)
            {
                for (int f = 0; f < nFrags; f++)
                    buf[f] = cwtCoeffs[f][s];
                consensus[s] = SmallMedianInPlace(buf);
            }

            return consensus;
        }

        /// <summary>
        /// Find peaks in a consensus CWT signal using zero-crossing boundary detection
        /// with valley guard.
        ///
        /// Steps:
        /// 1. Find local maxima in the consensus signal above minConsensusHeight.
        /// 2. For each apex, walk outward to zero-crossings (at +/-sigma in CWT space).
        /// 3. Extend boundaries by COVERAGE_FACTOR for ~95% Gaussian coverage.
        /// 4. Apply valley guard: stop extending if the reference signal rises more
        ///    than VALLEY_THRESHOLD * apex above a running minimum.
        /// 5. Compute area and SNR from the summed raw fragment signal.
        /// </summary>
        /// <param name="consensus">Consensus CWT signal.</param>
        /// <param name="refSignal">Reference (summed raw) signal for quantitation.</param>
        /// <param name="rts">Retention time axis.</param>
        /// <param name="minConsensusHeight">Minimum consensus CWT coefficient for peak candidates.</param>
        /// <returns>Peaks sorted by consensus CWT coefficient descending.</returns>
        public static List<XICPeakBounds> FindPeaks(
            double[] consensus,
            double[] refSignal,
            double[] rts,
            double minConsensusHeight)
        {
            int nScans = consensus.Length;

            // Find local maxima in consensus signal
            List<int> apexIndices = new List<int>();
            List<double> apexCoeffs = new List<double>();

            for (int i = 1; i < nScans - 1; i++)
            {
                if (consensus[i] > minConsensusHeight
                    && consensus[i] > consensus[i - 1]
                    && consensus[i] > consensus[i + 1])
                {
                    apexIndices.Add(i);
                    apexCoeffs.Add(consensus[i]);
                }
            }

            // Check endpoints
            if (nScans >= 2 && consensus[0] > minConsensusHeight && consensus[0] > consensus[1])
            {
                apexIndices.Add(0);
                apexCoeffs.Add(consensus[0]);
            }
            if (nScans >= 2 && consensus[nScans - 1] > minConsensusHeight
                && consensus[nScans - 1] > consensus[nScans - 2])
            {
                apexIndices.Add(nScans - 1);
                apexCoeffs.Add(consensus[nScans - 1]);
            }

            // Sort by consensus coefficient descending
            int[] sortOrder = new int[apexIndices.Count];
            for (int i = 0; i < sortOrder.Length; i++)
                sortOrder[i] = i;
            Array.Sort(sortOrder, (a, b) => apexCoeffs[b].CompareTo(apexCoeffs[a]));

            List<XICPeakBounds> peaks = new List<XICPeakBounds>();

            foreach (int orderIdx in sortOrder)
            {
                int apexIdx = apexIndices[orderIdx];

                // Step 1: Find zero-crossings
                int leftZc = apexIdx;
                while (leftZc > 0 && consensus[leftZc - 1] > 0.0)
                    leftZc--;

                int rightZc = apexIdx;
                while (rightZc < nScans - 1 && consensus[rightZc + 1] > 0.0)
                    rightZc++;

                // Step 2: Compute asymmetric sigma estimates from zero-crossing distances
                int leftSigma = apexIdx - leftZc;
                int rightSigma = rightZc - apexIdx;

                // Step 3: Extend to +/-2 sigma for ~95% coverage
                int targetStart = apexIdx - (int)COVERAGE_FACTOR * Math.Max(leftSigma, 1);
                if (targetStart < 0) targetStart = 0;

                int targetEnd = apexIdx + (int)COVERAGE_FACTOR * Math.Max(rightSigma, 1);
                if (targetEnd > nScans - 1) targetEnd = nScans - 1;

                // Step 4: Valley guard
                double refApexVal = refSignal[apexIdx];
                double riseThreshold = refApexVal * VALLEY_THRESHOLD;

                // Extend left with valley guard
                int startIdx = leftZc;
                double runningMin = refSignal[leftZc];
                int minPos = leftZc;
                for (int i = leftZc - 1; i >= targetStart; i--)
                {
                    if (refSignal[i] < runningMin)
                    {
                        runningMin = refSignal[i];
                        minPos = i;
                    }
                    else if (refSignal[i] - runningMin > riseThreshold)
                    {
                        startIdx = minPos;
                        goto leftDone;
                    }
                    startIdx = i;
                }
                leftDone:

                // Extend right with valley guard
                int endIdx = rightZc;
                runningMin = refSignal[rightZc];
                minPos = rightZc;
                for (int i = rightZc + 1; i <= targetEnd; i++)
                {
                    if (refSignal[i] < runningMin)
                    {
                        runningMin = refSignal[i];
                        minPos = i;
                    }
                    else if (refSignal[i] - runningMin > riseThreshold)
                    {
                        endIdx = minPos;
                        goto rightDone;
                    }
                    endIdx = i;
                }
                rightDone:

                // Require at least 3 scans
                if (endIdx - startIdx + 1 < 3)
                    continue;

                // Find the actual apex in the reference signal within boundaries
                int refApexIdx = startIdx;
                double refApexIntensity = refSignal[startIdx];
                for (int i = startIdx + 1; i <= endIdx; i++)
                {
                    if (refSignal[i] > refApexIntensity)
                    {
                        refApexIdx = i;
                        refApexIntensity = refSignal[i];
                    }
                }

                // Compute area from reference signal
                double area = PeakDetector.TrapezoidalArea(rts, refSignal, startIdx, endIdx);

                // Compute SNR from reference signal
                double snr = PeakDetector.ComputeSnr(refSignal, refApexIdx, startIdx, endIdx);

                peaks.Add(new XICPeakBounds
                {
                    ApexRt = rts[refApexIdx],
                    ApexIntensity = refApexIntensity,
                    ApexIndex = refApexIdx,
                    StartRt = rts[startIdx],
                    EndRt = rts[endIdx],
                    StartIndex = startIdx,
                    EndIndex = endIdx,
                    Area = area,
                    SignalToNoise = snr
                });
            }

            return peaks;
        }

        /// <summary>
        /// Main entry point: detect consensus peaks across multiple fragment XICs
        /// using the Continuous Wavelet Transform.
        ///
        /// 1. Convolves each fragment XIC with the Mexican Hat wavelet
        /// 2. Computes pointwise median consensus across all transitions
        /// 3. Finds peaks via zero-crossing boundaries with valley guard
        /// 4. Computes area/SNR from summed raw fragment signal
        ///
        /// Requires at least 2 XICs with at least 5 scans each, all the same length.
        /// </summary>
        /// <param name="xics">Fragment XICs (all must share the same time axis).</param>
        /// <param name="minConsensusHeight">Minimum consensus CWT coefficient. Use 0.0 to accept any positive peak.</param>
        /// <returns>Peaks sorted by consensus CWT coefficient descending.</returns>
        public static List<XICPeakBounds> DetectConsensusPeaks(
            List<XicData> xics,
            double minConsensusHeight)
        {
            // Validate input
            if (xics.Count < 2)
                return new List<XICPeakBounds>();

            int nScans = xics[0].Intensities.Length;
            if (nScans < 5)
                return new List<XICPeakBounds>();

            // Verify all XICs have the same length
            for (int i = 1; i < xics.Count; i++)
            {
                if (xics[i].Intensities.Length != nScans)
                    return new List<XICPeakBounds>();
            }

            int nFrags = xics.Count;

            // Check for any signal
            bool hasSignal = false;
            for (int f = 0; f < nFrags && !hasSignal; f++)
            {
                double[] ints = xics[f].Intensities;
                for (int s = 0; s < nScans; s++)
                {
                    if (ints[s] > 0.0)
                    {
                        hasSignal = true;
                        break;
                    }
                }
            }
            if (!hasSignal)
                return new List<XICPeakBounds>();

            // Estimate scale and generate kernel
            double sigma = EstimateScale(xics);
            int kernelRadius = Math.Min((int)Math.Ceiling(5.0 * sigma), nScans / 2);
            double[] kernel = MexicanHatKernel(sigma, kernelRadius);

            // Convolve each fragment XIC with the Mexican Hat kernel
            double[][] cwtCoeffs = new double[nFrags][];
            for (int f = 0; f < nFrags; f++)
                cwtCoeffs[f] = Convolve(xics[f].Intensities, kernel);

            // Compute pointwise median consensus
            double[] consensus = ConsensusMedianCwt(cwtCoeffs, nScans);

            // Build reference signal (sum of raw fragment intensities)
            double[] refSignal = new double[nScans];
            for (int s = 0; s < nScans; s++)
            {
                for (int f = 0; f < nFrags; f++)
                    refSignal[s] += xics[f].Intensities[s];
            }

            double[] rts = xics[0].RetentionTimes;

            return FindPeaks(consensus, refSignal, rts, minConsensusHeight);
        }

        /// <summary>
        /// Compute the median of a list of values. Creates a sorted copy.
        /// </summary>
        private static double SmallMedian(List<double> values)
        {
            if (values.Count == 0)
                return 0.0;

            double[] sorted = values.ToArray();
            Array.Sort(sorted);
            int n = sorted.Length;
            if (n % 2 == 0)
                return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
            return sorted[n / 2];
        }

        /// <summary>
        /// Compute the median of a buffer in place (sorts the buffer).
        /// </summary>
        private static double SmallMedianInPlace(double[] values)
        {
            if (values.Length == 0)
                return 0.0;

            Array.Sort(values);
            int n = values.Length;
            if (n % 2 == 0)
                return (values[n / 2 - 1] + values[n / 2]) / 2.0;
            return values[n / 2];
        }
    }

    /// <summary>
    /// Holds a single fragment XIC with parallel retention time and intensity arrays.
    /// </summary>
    public class XicData
    {
        /// <summary>Fragment index (identifier).</summary>
        public int FragmentIndex { get; set; }

        /// <summary>Retention time values (shared time axis across all fragments).</summary>
        public double[] RetentionTimes { get; set; }

        /// <summary>Intensity values corresponding to each retention time.</summary>
        public double[] Intensities { get; set; }

        public XicData(int fragmentIndex, double[] retentionTimes, double[] intensities)
        {
            FragmentIndex = fragmentIndex;
            RetentionTimes = retentionTimes;
            Intensities = intensities;
        }
    }
}
