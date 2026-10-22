/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Centroid
{
    /// <summary>
    /// A linear ion trap scan rate (Thermo Stellar, Fusion and other LITs): the broadest peak it produces and the
    /// profile point spacing its firmware fixes, which identifies it. The faster the scan, the coarser the points and
    /// the wider the peaks. From Centrix's table (Centrix's README, <c>cwt.rs</c> <c>LIT_SCAN_RATES</c>), whose widths
    /// are the broadest peak for each rate: the peaks a run actually shows are narrower (on the Stellar, MS2 at
    /// 200 kTh/s measures about 0.72 Th FWHM against 2.0, MS1 at 67 kTh/s about 0.40 against 0.6). Centrix gives no
    /// spacing for 200 kTh/s; the 0.2 Th here (five points per Th, on the 1000 / rate pattern of the others) is what
    /// the Stellar's MS2 at that rate has.
    /// <para>The rate is identified from the spacing rather than from the filter string's scan rate code (Centrix's
    /// first choice: n, r, t, u after "NSI"). The Stellar's MS1 carry "r" (67 kTh/s), but its MS2 carry "Z", a Zoom
    /// annotation the Stellar, which performs no zoom scans, uses to mark Adaptive RT spectra, and which Lauren's
    /// DIA runs nonetheless carry on every MS2. Other LITs (a Fusion's) write no code at all. The spacing is in every
    /// profile spectrum.</para>
    /// </summary>
    public sealed class LinearIonTrapScanRate
    {
        /// <summary>The scan rates: 33, 67, 125 and 200 kTh/s.</summary>
        public static readonly LinearIonTrapScanRate[] ALL =
        {
            new LinearIonTrapScanRate(33, 0.5, 1.0 / 30),
            new LinearIonTrapScanRate(67, 0.6, 1.0 / 15),
            new LinearIonTrapScanRate(125, 0.8, 1.0 / 8),
            new LinearIonTrapScanRate(200, 2.0, 1.0 / 5),
        };

        /// <summary>Spacings within this fraction of a scan rate's are that scan rate's.</summary>
        private const double SPACING_TOLERANCE = 0.02;

        private LinearIonTrapScanRate(double kThPerSecond, double maxFwhmTh, double spacingTh)
        {
            KThPerSecond = kThPerSecond;
            MaxFwhmTh = maxFwhmTh;
            SpacingTh = spacingTh;
        }

        /// <summary>The scan rate, thousands of Th per second.</summary>
        public double KThPerSecond { get; }

        /// <summary>The broadest peak, Th: a single species' peak is this wide or narrower.</summary>
        public double MaxFwhmTh { get; }

        /// <summary>The profile point spacing, Th.</summary>
        public double SpacingTh { get; }

        /// <summary>The Gaussian sigma of the broadest peak, Th.</summary>
        public double MaxSigmaTh => MaxFwhmTh / CentrixCalibration.FWHM_PER_SIGMA;

        /// <summary>The scan rate whose point spacing this is, or null when it is none of them.</summary>
        public static LinearIonTrapScanRate FromSpacing(double spacingTh)
        {
            foreach (var rate in ALL)
            {
                if (Math.Abs(spacingTh - rate.SpacingTh) <= SPACING_TOLERANCE * rate.SpacingTh)
                    return rate;
            }
            return null;
        }
    }

    /// <summary>
    /// The peak sigma Centrix fits a run's profile spectra with, measured from the run: the width of one species' peak,
    /// which is the instrument's resolution at the scan rate it ran.
    /// <list type="bullet">
    /// <item>The scan rate bounds it: no wider than <see cref="LinearIonTrapScanRate.MaxFwhmTh"/>, the broadest peak
    /// that rate produces.</item>
    /// <item>Within that, a wider peak is most often two or more species the instrument did not resolve, which is
    /// what Centrix is there to separate. So the width is taken from the narrow side of the run's isolated peaks'
    /// widths, their lower quartile (<see cref="WIDTH_QUANTILE"/>), not their median or mode.</item>
    /// <item>Each peak's width is its full width at half maximum, the resolution's own measure, rather than the sigma
    /// of a Gaussian fitted to it: these peaks have a narrow core and broad wings, and a fit's sigma grows with the
    /// points it is given (on the Stellar MS2, 0.31 Th from three points, 0.36 from five, 0.61 from eleven, the last
    /// what Centrix's wavelet calibration reports).</item>
    /// <item>The sigma is not chosen by identifications: at unit resolution those depend on mass accuracy, a separate
    /// question from the peak width.</item>
    /// </list>
    /// <para>Measured on a Thermo Stellar's MS2 (200 kTh/s, 35,000 isolated peaks of 6,000 spectra): the widths' lower
    /// quartile is 0.71 to 0.74 Th at every m/z from 200 to 1200, as a linear ion trap's should be, and varies by 0.6%
    /// between samples of 3,000 peaks, where their mode, the top of a plateau from 0.70 to 0.85 Th, varies by 6%. Two
    /// effects move it, both left unmodeled: it rises with apex height, from 0.63 Th in the weakest fifth of peaks to
    /// 0.81 Th in the strongest (space charge, or more merged species among the larger peaks); and spectra of summed
    /// windows read wider (sigma 0.309 Th for the 2 Th windows, 0.331 and 0.340 for two and four of them summed), since
    /// more of their peaks are merged species. The run's first spectra read 4% wider than the whole, so callers sample
    /// across the run (<see cref="CentrixSigmaCalibrator.SampleIndices"/>). MS1 (67 kTh/s): sigma 0.170 Th.</para>
    /// </summary>
    public static class CentrixCalibration
    {
        /// <summary>A Gaussian's full width at half maximum over its sigma, 2 sqrt(2 ln 2).</summary>
        public const double FWHM_PER_SIGMA = 2.3548200450309493;

        /// <summary>A width is measured from at least this many peaks; with fewer, the scan rate's broadest stands.</summary>
        public const int MIN_PEAKS = 20;

        /// <summary>Peaks are local maxima among a spectrum's most intense points: those above this quantile.</summary>
        public const double PEAK_QUANTILE = 0.99;

        /// <summary>The width is this quantile of the isolated peaks' widths: the narrow side, single species.</summary>
        public const double WIDTH_QUANTILE = 0.25;

        /// <summary>Centrix's sigma when neither the peaks nor the scan rate give one (125 kTh/s, the common DIA rate).</summary>
        public const double FALLBACK_SIGMA_TH = 0.340;

        /// <summary>A peak's points must be no farther apart than this many point spacings (none missing between them).</summary>
        private const double MAX_STEP_SPACINGS = 1.5;

        /// <summary>
        /// The sigma, Th, for spectra of one MS level whose isolated peaks' FWHMs were collected: the widths'
        /// <see cref="WIDTH_QUANTILE"/> quantile over <see cref="FWHM_PER_SIGMA"/>, no wider than the scan rate's
        /// broadest. With fewer than <see cref="MIN_PEAKS"/> widths, the broadest itself, or
        /// <see cref="FALLBACK_SIGMA_TH"/> without a scan rate.
        /// </summary>
        public static double EstimateSigma(List<double> fwhms, LinearIonTrapScanRate scanRate)
        {
            double maxSigma = scanRate?.MaxSigmaTh ?? double.PositiveInfinity;
            if (fwhms.Count < MIN_PEAKS)
                return scanRate?.MaxSigmaTh ?? FALLBACK_SIGMA_TH;
            var sorted = fwhms.ToArray();
            Array.Sort(sorted); // Array.Sort OK: equal doubles are indistinguishable
            return Math.Min(sorted[(int)(WIDTH_QUANTILE * (sorted.Length - 1))] / FWHM_PER_SIGMA, maxSigma);
        }

        /// <summary>
        /// Adds the FWHM, Th, of each isolated intense peak of a profile spectrum (m/z ascending, zero points present or
        /// not): a local maximum above <see cref="PEAK_QUANTILE"/> of the spectrum's intensities whose intensity falls
        /// without rising again to half its apex on both sides, over points adjacent on the profile grid. The apex is the
        /// vertex of the log-parabola through the top three points, so a peak sampled off its center is not read as
        /// wider; the crossings are interpolated linearly.
        /// </summary>
        public static void AddPeakWidths(IReadOnlyList<double> mz, IReadOnlyList<double> intensity, List<double> fwhms)
        {
            int n = Math.Min(mz.Count, intensity.Count);
            if (n < 3)
                return;
            double maxStep = MAX_STEP_SPACINGS * CentrixCentroider.DetectGridSpacing(mz);
            var sorted = new double[n];
            for (int i = 0; i < n; i++)
                sorted[i] = intensity[i];
            Array.Sort(sorted); // Array.Sort OK: equal doubles are indistinguishable
            double threshold = Math.Max(sorted[(int)(PEAK_QUANTILE * (n - 1))], 0);

            for (int i = 1; i < n - 1; i++)
            {
                // An intense local maximum with positive neighbors (a plateau's first point counts once).
                double y = intensity[i];
                if (!(y > threshold) || !(y > intensity[i - 1]) || !(y >= intensity[i + 1]) ||
                    !(intensity[i - 1] > 0) || !(intensity[i + 1] > 0))
                {
                    continue;
                }
                // Walk down each side while the intensity keeps falling and is still above half the apex. A peak whose
                // intensity rises again first has a neighbor inside its half maximum, so its width is not one species'
                // and it is skipped; so is one whose walk crosses a missing point.
                double half = ApexHeight(intensity[i - 1], y, intensity[i + 1]) / 2;
                int left = i;
                while (left > 0 && intensity[left - 1] < intensity[left] && intensity[left] > half)
                    left--;
                int right = i;
                while (right < n - 1 && intensity[right + 1] < intensity[right] && intensity[right] > half)
                    right++;
                if (intensity[left] > half || intensity[right] > half || !Contiguous(mz, left, right, maxStep))
                    continue;
                double mzLeft = mz[left] + (half - intensity[left]) / (intensity[left + 1] - intensity[left]) *
                    (mz[left + 1] - mz[left]);
                double mzRight = mz[right - 1] + (intensity[right - 1] - half) / (intensity[right - 1] - intensity[right]) *
                    (mz[right] - mz[right - 1]);
                fwhms.Add(mzRight - mzLeft);
            }
        }

        /// <summary>Whether every step between points first and last is at most maxStep: no point missing between them.</summary>
        private static bool Contiguous(IReadOnlyList<double> mz, int first, int last, double maxStep)
        {
            for (int i = first; i < last; i++)
            {
                if (!(mz[i + 1] - mz[i] <= maxStep))
                    return false;
            }
            return true;
        }

        /// <summary>The apex height of the log-parabola through three points around a maximum, or the middle one's.</summary>
        private static double ApexHeight(double y0, double y1, double y2)
        {
            double l0 = Math.Log(y0), l1 = Math.Log(y1), l2 = Math.Log(y2);
            double a = l0 - 2 * l1 + l2;
            if (!(a < 0))
                return y1;
            double offset = 0.5 * (l0 - l2) / a;
            if (Math.Abs(offset) > 0.5)
                return y1;
            return Math.Exp(l1 - 0.25 * (l0 - l2) * offset);
        }
    }

    /// <summary>
    /// Collects the peak widths of a run's profile spectra, MS1 apart from MS2 and above, since a linear ion trap scans
    /// them at their own rates, and gives each level's sigma (<see cref="CentrixCalibration.EstimateSigma"/>) and scan
    /// rate, taken from the point spacing of the level's first spectrum. The caller should offer spectra from across the
    /// run: on the Stellar the run's first spectra read 4% wider than the whole.
    /// </summary>
    public sealed class CentrixSigmaCalibrator
    {
        /// <summary>Peaks enough for a level's width: their lower quartile varies by 0.6% between such samples (Stellar MS2).</summary>
        public const int TARGET_PEAKS = 3000;

        /// <summary>Spectra a run is sampled at, evenly spaced, for its widths.</summary>
        public const int SAMPLE_SPECTRA = 3000;

        private readonly List<double>[] _fwhms = { new List<double>(), new List<double>() };
        private readonly double[] _spacing = { double.NaN, double.NaN };

        /// <summary>Up to <see cref="SAMPLE_SPECTRA"/> spectrum indices spread evenly over a run, ascending.</summary>
        public static IEnumerable<int> SampleIndices(int spectrumCount)
        {
            int samples = Math.Min(SAMPLE_SPECTRA, spectrumCount);
            for (int s = 0; s < samples; s++)
                yield return (int)((long)s * spectrumCount / samples);
        }

        /// <summary>Adds a profile spectrum's isolated peak widths to its level's.</summary>
        public void Add(int msLevel, IReadOnlyList<double> mz, IReadOnlyList<double> intensity)
        {
            int level = Slot(msLevel);
            if (_fwhms[level].Count >= TARGET_PEAKS)
                return;
            if (double.IsNaN(_spacing[level]) && mz.Count >= 2)
                _spacing[level] = CentrixCentroider.DetectGridSpacing(mz);
            CentrixCalibration.AddPeakWidths(mz, intensity, _fwhms[level]);
        }

        /// <summary>Whether a level has <see cref="TARGET_PEAKS"/> widths.</summary>
        public bool IsComplete(int msLevel)
        {
            return _fwhms[Slot(msLevel)].Count >= TARGET_PEAKS;
        }

        /// <summary>The widths collected for a level.</summary>
        public int PeakCount(int msLevel)
        {
            return _fwhms[Slot(msLevel)].Count;
        }

        /// <summary>A level's scan rate, from its point spacing; null when unknown.</summary>
        public LinearIonTrapScanRate ScanRate(int msLevel)
        {
            double spacing = _spacing[Slot(msLevel)];
            return double.IsNaN(spacing) ? null : LinearIonTrapScanRate.FromSpacing(spacing);
        }

        /// <summary>A level's sigma, Th.</summary>
        public double Sigma(int msLevel)
        {
            return CentrixCalibration.EstimateSigma(_fwhms[Slot(msLevel)], ScanRate(msLevel));
        }

        private static int Slot(int msLevel)
        {
            return msLevel == 1 ? 0 : 1;
        }
    }
}
