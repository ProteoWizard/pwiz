/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on skyline-glimpse (https://github.com/maccoss/skyline-glimpse)
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
using System.Linq;

namespace pwiz.Quasar.Features
{
    /// <summary>How envelopes are linked across scans into persistent features (Kronik's defaults).</summary>
    /// <param name="PpmTolerance">Largest mass difference, in ppm of the seed's mass, between linked envelopes.</param>
    /// <param name="GapTolerance">Scans in a row that may miss the envelope before the feature ends.</param>
    /// <param name="MinScans">Fewest scans (the seed included) an envelope must be found in to persist.</param>
    public sealed record KronikSettings(double PpmTolerance = 10, int GapTolerance = 1, int MinScans = 3)
    {
        public static KronikSettings Default { get; } = new KronikSettings();
    }

    /// <summary>What linking needs to know about one envelope.</summary>
    /// <param name="MonoMass">Neutral monoisotopic mass (Da).</param>
    /// <param name="Charge">Charge state.</param>
    /// <param name="Intensity">Envelope intensity; the most intense unlinked envelope seeds the next feature.</param>
    public readonly record struct EnvelopeKey(double MonoMass, int Charge, float Intensity);

    /// <summary>One envelope persisting across scans.</summary>
    /// <param name="MonoMass">The seed's monoisotopic mass.</param>
    /// <param name="Charge">Charge state.</param>
    /// <param name="BestScan">Position (in the series) of the seed, the most intense scan.</param>
    /// <param name="BestIntensity">The seed's intensity.</param>
    /// <param name="FirstScan">Position of the first scan the envelope was found in.</param>
    /// <param name="LastScan">Position of the last.</param>
    /// <param name="ScanCount">Scans the envelope was found in (missed scans in between not counted).</param>
    /// <param name="SummedIntensity">Sum of the found envelopes' intensities (Kronik also adds interpolated
    /// values for missed scans; this does not).</param>
    public sealed record PersistentFeature(
        double MonoMass, int Charge, int BestScan, float BestIntensity, int FirstScan, int LastScan, int ScanCount, float SummedIntensity);

    /// <summary>The features found, and which feature each envelope belongs to.</summary>
    /// <param name="Features">The persistent features, in the order they were found (most intense seed first).</param>
    /// <param name="FeatureOf">Per scan, per envelope (in the order given): its feature's index, or -1.</param>
    public sealed record KronikResult(IReadOnlyList<PersistentFeature> Features, int[][] FeatureOf);

    /// <summary>
    /// Links isotope envelopes found in successive scans into features that persist over time.
    /// </summary>
    /// <remarks>
    /// Ported from Kronik as BullseyeSharp implements it (pwiz_tools/BullseyeSharp/src/CKronik2.cs, processHK;
    /// Hoopmann, Hsieh, MacCoss): repeatedly take the most intense envelope not yet used, look in the scans
    /// before and after it for the most intense envelope of the same charge within the ppm tolerance, stop a
    /// direction after more than <see cref="KronikSettings.GapTolerance"/> scans in a row without one, and keep
    /// the feature when it was found in at least <see cref="KronikSettings.MinScans"/> scans. A kept feature
    /// uses up its envelopes; a rejected seed uses up only itself, so its neighbors can still seed or join
    /// other features. Kronik searches each scan's envelopes in intensity order; this looks them up by mass
    /// (same result: the most intense match), which keeps a run with thousands of envelopes per scan fast.
    /// Kronik's SIM/boxcar window skip is supported through <c>scanWindows</c>.
    /// </remarks>
    public static class Kronik
    {
        private const double PROTON = 1.00727649;   // CKronik2's PROTON

        /// <param name="scans">The envelopes of each scan of one series (e.g. MS1 scans, or one DIA window), in time order.</param>
        /// <param name="settings">Linking tolerances.</param>
        /// <param name="scanWindows">Optional m/z range each scan covers (SIM/boxcar); a scan whose range
        /// misses a feature is skipped without counting as a gap. Null, or a range with Upper 0, covers all.</param>
        public static KronikResult Link(
            IReadOnlyList<IReadOnlyList<EnvelopeKey>> scans, KronikSettings settings,
            IReadOnlyList<(double Lower, double Upper)> scanWindows = null)
        {
            var n = scans.Count;
            var featureOf = new int[n][];
            var used = new bool[n][];
            var byMass = new int[n][];
            var masses = new double[n][];
            var total = 0;
            for (var s = 0; s < n; s++)
            {
                var envelopes = scans[s];
                featureOf[s] = new int[envelopes.Count];
                Array.Fill(featureOf[s], -1);
                used[s] = new bool[envelopes.Count];
                var order = Enumerable.Range(0, envelopes.Count).ToArray();
                var m = envelopes.Select(e => e.MonoMass).ToArray();
                Array.Sort((double[])m.Clone(), order);
                byMass[s] = order;
                masses[s] = order.Select(i => envelopes[i].MonoMass).ToArray();
                total += envelopes.Count;
            }

            // Seeds in falling intensity (ties: earlier scan, then earlier envelope), as Kronik's findMax picks them.
            var seeds = new (int Scan, int Index)[total];
            var k = 0;
            for (var s = 0; s < n; s++)
            {
                for (var i = 0; i < scans[s].Count; i++)
                    seeds[k++] = (s, i);
            }
            Array.Sort(seeds, (a, b) =>
            {
                var c = scans[b.Scan][b.Index].Intensity.CompareTo(scans[a.Scan][a.Index].Intensity);
                return c != 0 ? c : a.Scan != b.Scan ? a.Scan.CompareTo(b.Scan) : a.Index.CompareTo(b.Index);
            });

            var features = new List<PersistentFeature>();
            var matched = new List<(int Scan, int Index)>();
            foreach (var (scan, index) in seeds)
            {
                if (used[scan][index])
                    continue;
                var seed = scans[scan][index];
                var tolerance = seed.MonoMass * settings.PpmTolerance / 1e6;
                var mzLow = MzOf(seed.MonoMass - tolerance, seed.Charge);
                var mzHigh = MzOf(seed.MonoMass + tolerance, seed.Charge);
                matched.Clear();
                var first = scan;
                var last = scan;
                foreach (var step in new[] { -1, 1 })
                {
                    var gap = 0;
                    for (var s = scan + step; s >= 0 && s < n && gap <= settings.GapTolerance; s += step)
                    {
                        if (scanWindows != null && scanWindows[s].Upper > 0
                            && (mzHigh < scanWindows[s].Lower || mzLow > scanWindows[s].Upper))
                            continue;
                        var hit = BestMatch(scans[s], byMass[s], masses[s], used[s], seed, tolerance);
                        if (hit < 0)
                        {
                            gap++;
                            continue;
                        }
                        gap = 0;
                        matched.Add((s, hit));
                        first = Math.Min(first, s);
                        last = Math.Max(last, s);
                    }
                }

                used[scan][index] = true;
                if (matched.Count + 1 < settings.MinScans)
                    continue;
                var id = features.Count;
                featureOf[scan][index] = id;
                foreach (var (s, i) in matched)
                {
                    used[s][i] = true;
                    featureOf[s][i] = id;
                }
                var members = matched.Select(m => (m.Scan, scans[m.Scan][m.Index].Intensity)).Append((scan, seed.Intensity));
                features.Add(new PersistentFeature(seed.MonoMass, seed.Charge, scan, seed.Intensity, first, last, matched.Count + 1, SummedIntensity(members)));
            }
            return new KronikResult(features, featureOf);
        }

        /// <summary>A feature's summed intensity: its envelopes in scan order, added in double precision.</summary>
        internal static float SummedIntensity(IEnumerable<(int Scan, float Intensity)> members) =>
            (float)members.OrderBy(m => m.Scan).Sum(m => (double)m.Intensity);

        private static double MzOf(double mass, int charge) => (mass + charge * PROTON) / charge;

        /// <summary>The most intense unused envelope of the seed's charge within the tolerance of its mass, or -1.</summary>
        private static int BestMatch(
            IReadOnlyList<EnvelopeKey> envelopes, int[] byMass, double[] masses, bool[] used, EnvelopeKey seed, double tolerance)
        {
            // Kronik's test is |mass - seed| < tolerance (strict).
            var lo = LowerBound(masses, seed.MonoMass - tolerance);
            var best = -1;
            for (var j = lo; j < masses.Length && masses[j] - seed.MonoMass < tolerance; j++)
            {
                var i = byMass[j];
                if (used[i] || envelopes[i].Charge != seed.Charge || Math.Abs(masses[j] - seed.MonoMass) >= tolerance)
                    continue;
                if (best < 0 || envelopes[i].Intensity > envelopes[best].Intensity
                    || (envelopes[i].Intensity == envelopes[best].Intensity && i < best))
                    best = i;
            }
            return best;
        }

        /// <summary>First position whose value is at least <paramref name="value"/>.</summary>
        private static int LowerBound(double[] sorted, double value)
        {
            int lo = 0, hi = sorted.Length;
            while (lo < hi)
            {
                var mid = (lo + hi) >>> 1;
                if (sorted[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }
    }
}
