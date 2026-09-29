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

namespace pwiz.Osprey.Demux
{
    /// <summary>How demultiplexed scanning data is laid out in output spectra.</summary>
    public enum ScanningLayoutKind
    {
        /// <summary>
        /// One spectrum per encoded bin, under that bin's own window, carrying the demultiplexed
        /// signal of the k positions centered on it: each precursor sits in the middle of what
        /// its spectrum carries.
        /// </summary>
        centered,

        /// <summary>
        /// One spectrum per k consecutive encoded bins, under their combined window: ordinary
        /// narrow-window DIA, about k times fewer spectra.
        /// </summary>
        tiled,

        /// <summary>
        /// Like tiled, but each spectrum also carries the demultiplexed signal of m positions
        /// beyond its window on each side, so a precursor near a window edge keeps the signal that
        /// placement noise moved into the next position. Windows still tile without overlap.
        /// </summary>
        framed,
    }

    /// <summary>One output spectrum of a layout, as encoded bin ranges.</summary>
    public struct ScanningOutputSpectrum
    {
        public ScanningOutputSpectrum(int firstBin, int lastBin, int firstSourceBin, int lastSourceBin)
        {
            FirstBin = firstBin;
            LastBin = lastBin;
            FirstSourceBin = firstSourceBin;
            LastSourceBin = lastSourceBin;
        }

        /// <summary>First encoded bin of the spectrum's isolation window.</summary>
        public int FirstBin { get; }

        /// <summary>Last encoded bin of the spectrum's isolation window.</summary>
        public int LastBin { get; }

        /// <summary>First source position whose demultiplexed signal the spectrum carries.</summary>
        public int FirstSourceBin { get; }

        /// <summary>Last source position whose demultiplexed signal the spectrum carries.</summary>
        public int LastSourceBin { get; }
    }

    /// <summary>
    /// A layout of demultiplexed scanning data into spectra: <c>centered:k</c> (k odd),
    /// <c>tiled:k</c> or <c>framed:k:m</c>.
    /// </summary>
    /// <remarks>
    /// One sweep's counts cannot place a fragment within one 1.18 Th encoded bin, so each
    /// spectrum carries several neighboring source positions. Measured on a ZT Scan slice with
    /// DIA-NN: one position per spectrum halved identifications; with Poisson weights, centered:5
    /// and framed:3:1 gained 3-5% over the undemultiplexed data. See docs/22-demultiplexing.md.
    /// </remarks>
    public sealed class ScanningLayout
    {
        /// <summary>Peaks of one channel from neighboring positions closer than this merge, in ppm.</summary>
        public const double MERGE_PPM = 5.0;

        public ScanningLayout(ScanningLayoutKind kind, int bins, int margin = 0)
        {
            if (bins < 1)
                throw new ArgumentOutOfRangeException(nameof(bins));
            if (margin < 0 || (margin > 0 && kind != ScanningLayoutKind.framed))
                throw new ArgumentOutOfRangeException(nameof(margin));
            if (kind == ScanningLayoutKind.centered && bins % 2 == 0)
                throw new ArgumentException(@"A centered layout needs an odd number of bins.");
            Kind = kind;
            Bins = bins;
            Margin = margin;
        }

        /// <summary>Parses <c>centered:5</c>, <c>tiled:5</c> or <c>framed:3:1</c> (3 bins, 1 bin of margin).</summary>
        public static ScanningLayout Parse(string text)
        {
            string[] parts = text.Split(':');
            ScanningLayoutKind kind = ScanningLayoutKind.centered;
            int bins = 0, margin = 0;
            bool valid = parts.Length >= 2 && Enum.TryParse(parts[0], out kind) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out bins);
            if (valid && kind == ScanningLayoutKind.framed)
                valid = parts.Length == 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out margin);
            else if (valid)
                valid = parts.Length == 2;
            if (!valid)
            {
                throw new FormatException(string.Format(CultureInfo.InvariantCulture,
                    @"'{0}' is not a scanning layout (centered:k, tiled:k or framed:k:m).", text));
            }
            return new ScanningLayout(kind, bins, margin);
        }

        public ScanningLayoutKind Kind { get; }
        public int Bins { get; }

        /// <summary>For a framed layout, the positions carried beyond the window on each side.</summary>
        public int Margin { get; }

        /// <summary>A short name, for file names and provenance: centered5, tiled5, framed3m1.</summary>
        public string Name
        {
            get
            {
                string name = Kind.ToString() + Bins.ToString(CultureInfo.InvariantCulture);
                return Kind == ScanningLayoutKind.framed ? name + @"m" + Margin.ToString(CultureInfo.InvariantCulture) : name;
            }
        }

        /// <summary>The output spectra of one sweep, for the encoded bins firstBin to lastBin.</summary>
        public List<ScanningOutputSpectrum> Plan(int firstBin, int lastBin)
        {
            var spectra = new List<ScanningOutputSpectrum>();
            if (Kind == ScanningLayoutKind.centered)
            {
                int half = Bins / 2;
                for (int b = firstBin; b <= lastBin; b++)
                    spectra.Add(new ScanningOutputSpectrum(b, b, b - half, b + half));
            }
            else
            {
                for (int b = firstBin; b <= lastBin; b += Bins)
                {
                    int last = Math.Min(lastBin, b + Bins - 1);
                    spectra.Add(new ScanningOutputSpectrum(b, last, b - Margin, last + Margin));
                }
            }
            return spectra;
        }

        /// <summary>
        /// The peaks of one output spectrum: the pass-through peaks of its own bins as acquired,
        /// and the demultiplexed peaks of its source positions, with one channel's peaks from
        /// neighboring positions (closer than <paramref name="mergePpm"/>, by default <see cref="MERGE_PPM"/>)
        /// summed at their intensity-weighted m/z. Sorted by m/z.
        /// </summary>
        /// <param name="passedThrough">Peaks kept as acquired.</param>
        /// <param name="demultiplexed">The source positions' demultiplexed peaks.</param>
        /// <param name="mz">The spectrum's m/z.</param>
        /// <param name="ions">The spectrum's intensities.</param>
        /// <param name="mergePpm">Consecutive peaks this close (ppm) are chained into one.</param>
        /// <param name="mergeWithin">
        /// When given, replaces <paramref name="mergePpm"/>: a peak joins the one being built when it lies within
        /// this distance (m/z, a function of m/z) of that one's intensity-weighted m/z so far, as Centrix merges
        /// centroids closer than the peak's sigma.
        /// </param>
        public static void Assemble(IEnumerable<ScanningPeak> passedThrough, IEnumerable<ScanningPeak> demultiplexed,
            out double[] mz, out double[] ions, double mergePpm = MERGE_PPM, Func<double, double> mergeWithin = null)
        {
            var dem = new List<ScanningPeak>(demultiplexed);
            dem.Sort(CompareMz); // Array.Sort OK: ties broken by bin and intensity in CompareMz
            var merged = new List<(double Mz, double Ions)>();
            int i = 0;
            while (i < dem.Count)
            {
                double sum = dem[i].Ions, weighted = dem[i].Ions * dem[i].Mz;
                int j = i + 1;
                while (j < dem.Count && (mergeWithin != null
                           ? dem[j].Mz - weighted / sum < mergeWithin(weighted / sum)
                           : dem[j].Mz - dem[j - 1].Mz <= dem[j].Mz * mergePpm * 1e-6))
                {
                    sum += dem[j].Ions;
                    weighted += dem[j].Ions * dem[j].Mz;
                    j++;
                }
                merged.Add((weighted / sum, sum));
                i = j;
            }
            foreach (var peak in passedThrough)
                merged.Add((peak.Mz, peak.Ions));
            merged.Sort((a, b) => a.Mz != b.Mz ? a.Mz.CompareTo(b.Mz) : a.Ions.CompareTo(b.Ions)); // Array.Sort OK: equal m/z ordered by intensity
            mz = new double[merged.Count];
            ions = new double[merged.Count];
            for (int k = 0; k < merged.Count; k++)
            {
                mz[k] = merged[k].Mz;
                ions[k] = merged[k].Ions;
            }
        }

        private static int CompareMz(ScanningPeak a, ScanningPeak b)
        {
            int byMz = a.Mz.CompareTo(b.Mz);
            if (byMz != 0)
                return byMz;
            int byBin = a.Bin.CompareTo(b.Bin);
            return byBin != 0 ? byBin : a.Ions.CompareTo(b.Ions);
        }
    }
}
