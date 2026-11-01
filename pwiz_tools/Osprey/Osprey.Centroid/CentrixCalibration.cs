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
using System.Globalization;

namespace pwiz.Osprey.Centroid
{
    /// <summary>
    /// A linear ion trap scan rate (Thermo Stellar, Fusion and other LITs) and the profile point spacing its firmware
    /// fixes, which identifies it: the faster the scan, the coarser the points (and the wider the peaks). From
    /// Centrix's table (Centrix's README, <c>cwt.rs</c> <c>LIT_SCAN_RATES</c>, which also lists each rate's broadest
    /// peak; docs/25-profile-centroiding.md). Centrix gives no spacing for 200 kTh/s; the 0.2 Th here (five points
    /// per Th, on the 1000 / rate pattern of the others) is what the Stellar's MS2 at that rate has.
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
            new LinearIonTrapScanRate(33, 1.0 / 30),
            new LinearIonTrapScanRate(67, 1.0 / 15),
            new LinearIonTrapScanRate(125, 1.0 / 8),
            new LinearIonTrapScanRate(200, 1.0 / 5),
        };

        /// <summary>Spacings within this fraction of a scan rate's are that scan rate's.</summary>
        private const double SPACING_TOLERANCE = 0.02;

        private LinearIonTrapScanRate(double kThPerSecond, double spacingTh)
        {
            KThPerSecond = kThPerSecond;
            SpacingTh = spacingTh;
        }

        /// <summary>The scan rate, thousands of Th per second.</summary>
        public double KThPerSecond { get; }

        /// <summary>The profile point spacing, Th.</summary>
        public double SpacingTh { get; }

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
    /// The peak sigma Centrix fits a run's profile spectra with.
    /// <para>On a linear ion trap, whose point spacing identifies its scan rate
    /// (<see cref="LinearIonTrapScanRate.FromSpacing"/>), it is a little over one point spacing
    /// (<see cref="SIGMA_PER_SPACING"/>). Centrix is there to split a peak wider than one species, and splitting a
    /// little too much costs little. On a Thermo Stellar's MS2 (200 kTh/s, 0.2 Th points) centroided at a range of
    /// sigmas and searched with an entrapment library, identifications plateau from 1.05 to 1.2 spacings (0.21 to
    /// 0.24 Th) and fall on either side, from one spacing down most steeply, as the centroids per spectrum jump
    /// (docs/25-profile-centroiding.md). The rule is set from that one scan rate and stands until others are
    /// measured.</para>
    /// <para>Elsewhere, a spacing no scan rate has, it is measured from the run: the width of one species' peak.</para>
    /// <list type="bullet">
    /// <item>A wider peak is most often two or more species the instrument did not resolve, which is what Centrix is
    /// there to separate. So the width is taken from the narrow side of the run's isolated peaks' widths, their lower
    /// quartile (<see cref="WIDTH_QUANTILE"/>), not their median or mode.</item>
    /// <item>Each peak's width is its full width at half maximum, the resolution's own measure, rather than the sigma
    /// of a Gaussian fitted to it: these peaks have a narrow core and broad wings, and a fit's sigma grows with the
    /// points it is given (on the Stellar MS2, 0.31 Th from three points, 0.36 from five, 0.61 from eleven, the last
    /// what Centrix's wavelet calibration reports).</item>
    /// </list>
    /// <para>The measurement is why the Stellar's rule is not a width. Its MS2 widths' lower quartile is 0.71 to
    /// 0.74 Th at every m/z from 200 to 1200, as a linear ion trap's should be, but it rises with the ions in the peak,
    /// from 0.41 Th FWHM under 3 ions to 0.9 Th over 300 (space charge, or more merged species among the larger
    /// peaks), with no plateau, so which peaks are counted sets it. Whole runs read through Thermo's library, where a
    /// quarter of the profile points are zeros, give sigma 0.27 Th over 3,000 spectra of a 2 Th run, 0.31 Th with the
    /// intensity threshold taken over the positive points only, which keeps stronger peaks; spectra of summed windows
    /// read wider still (0.331 and 0.340 Th for two and four 2 Th windows summed). The identifications' optimum,
    /// 0.21 Th, is narrower than all of them.</para>
    /// </summary>
    public static class CentrixCalibration
    {
        /// <summary>A Gaussian's full width at half maximum over its sigma, 2 sqrt(2 ln 2).</summary>
        public const double FWHM_PER_SIGMA = 2.3548200450309493;

        /// <summary>
        /// A linear ion trap's sigma, in point spacings of the scan rate its spacing identifies: inside the Stellar's
        /// plateau of 1.05 to 1.2, clear of the fall at one spacing.
        /// </summary>
        public const double SIGMA_PER_SPACING = 1.1;

        /// <summary>A width is measured from at least this many peaks; with fewer, <see cref="FALLBACK_SIGMA_TH"/> stands.</summary>
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
        /// The sigma, Th, for spectra of one MS level: with a linear ion trap scan rate, <see cref="SIGMA_PER_SPACING"/>
        /// of its point spacing. Without one, from the isolated peaks' FWHMs collected: their
        /// <see cref="WIDTH_QUANTILE"/> quantile over <see cref="FWHM_PER_SIGMA"/>, or <see cref="FALLBACK_SIGMA_TH"/>
        /// with fewer than <see cref="MIN_PEAKS"/> widths.
        /// </summary>
        public static double EstimateSigma(List<double> fwhms, LinearIonTrapScanRate scanRate)
        {
            if (scanRate != null)
                return SIGMA_PER_SPACING * scanRate.SpacingTh;
            if (fwhms.Count < MIN_PEAKS)
                return FALLBACK_SIGMA_TH;
            var sorted = fwhms.ToArray();
            Array.Sort(sorted); // Array.Sort OK: equal doubles are indistinguishable
            return sorted[(int)(WIDTH_QUANTILE * (sorted.Length - 1))] / FWHM_PER_SIGMA;
        }

        /// <summary>
        /// Adds the FWHM, Th, of each isolated intense peak of a profile spectrum (m/z ascending, zero points present or
        /// not): a local maximum above <see cref="PEAK_QUANTILE"/> of the spectrum's intensities whose intensity falls
        /// without rising again, or reaching zero, to half its apex on both sides, over points adjacent on the profile
        /// grid. The apex is the vertex of the log-parabola through the top three points, so a peak sampled off its
        /// center is not read as wider; the crossings are interpolated linearly.
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
                // An intense local maximum with positive neighbors (of a flat top, the first point; skipped below).
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
                // A walk that cannot leave the apex (the log-parabola's apex at least twice the sampled maximum, or a
                // flat top) has no crossing to interpolate.
                if (left == i || right == i || intensity[left] > half || intensity[right] > half ||
                    !Contiguous(mz, left, right, maxStep))
                {
                    continue;
                }
                // A peak that falls to zero before half its apex is a few ions' spike, not the peak shape the scan
                // rate gives: an ion trap ejects each ion at one instant, so a handful of them land on one or two
                // points with nothing beside them. On the Stellar's MS2 they are a third of the intense local maxima,
                // and counting them moves the lower quartile from 0.72 Th to 0.63, narrower than the species' peaks.
                // A reader that drops zero points leaves a gap there instead, which the contiguity check above
                // rejects, so the widths do not depend on whether the zeros are present.
                if (!(intensity[left] > 0) || !(intensity[right] > 0))
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
    /// rate, taken from the point spacing of the level's densest spectrum so far (a sparse one, its zero points
    /// dropped, shows a multiple of the spacing). The caller offers the spectra of <see cref="SampleIndices"/>, spread
    /// across the run, until the levels it needs are <see cref="IsSettled"/>: a linear ion trap's from its first
    /// spectrum of <see cref="MIN_SPACING_POINTS"/> points, any other's from every sample, since the run's first minutes are not
    /// the whole (on the Stellar, the first 3,000 peaks read sigma 0.17 Th where the whole run reads 0.27).
    /// </summary>
    public sealed class CentrixSigmaCalibrator
    {
        /// <summary>
        /// Spectra a run is sampled at, evenly spaced, for its widths: on the Stellar's MS2, 10,000 to 15,000 isolated
        /// peaks, whose lower quartile varies by 0.6% between samples of 3,000.
        /// </summary>
        public const int SAMPLE_SPECTRA = 3000;

        private readonly List<double>[] _fwhms = { new List<double>(), new List<double>() };
        /// <summary>A level's spacing settles its sigma once taken from a spectrum of at least this many points.</summary>
        public const int MIN_SPACING_POINTS = 100;

        private readonly double[] _spacing = { double.NaN, double.NaN };
        private readonly int[] _spacingPoints = new int[2];   // the points of the spectrum each spacing was taken from

        /// <summary>Up to <see cref="SAMPLE_SPECTRA"/> spectrum indices spread evenly over a run, ascending.</summary>
        public static IEnumerable<int> SampleIndices(int spectrumCount)
        {
            int samples = Math.Min(SAMPLE_SPECTRA, spectrumCount);
            for (int s = 0; s < samples; s++)
                yield return (int)((long)s * spectrumCount / samples);
        }

        /// <summary>
        /// Offers the spectra of <see cref="SampleIndices"/> in turn, each through <paramref name="offer"/> (which
        /// <see cref="Add"/>s it if it is a profile spectrum), until every level needed is <see cref="IsSettled"/>.
        /// </summary>
        public void Sample(int spectrumCount, bool needMs1, bool needMs2, Action<int> offer)
        {
            foreach (int i in SampleIndices(spectrumCount))
            {
                if ((!needMs1 || IsSettled(1)) && (!needMs2 || IsSettled(2)))
                    break;
                offer(i);
            }
        }

        /// <summary>Adds a profile spectrum's isolated peak widths to its level's.</summary>
        public void Add(int msLevel, IReadOnlyList<double> mz, IReadOnlyList<double> intensity)
        {
            int level = Slot(msLevel);
            if (mz.Count >= 2 && mz.Count > _spacingPoints[level])
            {
                _spacing[level] = CentrixCentroider.DetectGridSpacing(mz);
                _spacingPoints[level] = mz.Count;
            }
            CentrixCalibration.AddPeakWidths(mz, intensity, _fwhms[level]);
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

        /// <summary>Where a level's sigma comes from, for a log: its scan rate, or the peaks measured.</summary>
        public string Describe(int msLevel)
        {
            var rate = ScanRate(msLevel);
            return rate != null
                ? string.Format(CultureInfo.InvariantCulture, @"{0} kTh/s scan rate, from its point spacing", rate.KThPerSecond)
                : string.Format(CultureInfo.InvariantCulture, @"{0} isolated peaks measured", PeakCount(msLevel));
        }

        /// <summary>A level's sigma, Th.</summary>
        public double Sigma(int msLevel)
        {
            return CentrixCalibration.EstimateSigma(_fwhms[Slot(msLevel)], ScanRate(msLevel));
        }

        /// <summary>
        /// Whether more spectra cannot change a level's sigma: its point spacing, from a spectrum of at least
        /// <see cref="MIN_SPACING_POINTS"/> points, identifies a scan rate.
        /// </summary>
        public bool IsSettled(int msLevel)
        {
            return _spacingPoints[Slot(msLevel)] >= MIN_SPACING_POINTS && ScanRate(msLevel) != null;
        }

        private static int Slot(int msLevel)
        {
            return msLevel == 1 ? 0 : 1;
        }
    }
}
