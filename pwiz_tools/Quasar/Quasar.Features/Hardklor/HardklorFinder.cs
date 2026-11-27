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

namespace pwiz.Quasar.Features.Hardklor
{
    /// <summary>
    /// Finds the isotope envelopes of peptide-like molecules in one centroided spectrum: Hardklor's Version2
    /// algorithm with the Quick charge algorithm, as Skyline runs it for feature detection.
    /// </summary>
    /// <remarks>
    /// <para>Ported from Hardklor (Hoopmann and MacCoss; Skyline's fork in
    /// pwiz_tools/Skyline/Executables/Hardklor/Hardklor/CHardklor2.cpp): QuickHardklor, QuickCharge,
    /// MatchSubSpectrum, PeakMatcher, PeakMatcherB, CheckForPeak, RefineHits, LinReg and BinarySearch. File
    /// reading, output writing, centroiding (input is centroided), averagine_mod variants (Skyline uses none) and
    /// the Version1 algorithms are left out. Isotope distributions come from an <see cref="IEnvelopeModel"/>
    /// instead of Mercury, so results are close to, not identical with, Hardklor.exe's.</para>
    /// <para>Deviations from the C++, all where it reads out of bounds or exits: a peak above
    /// <see cref="HardklorSettings.MaxMoleculeMz"/> is skipped instead of stopping the program; an overlap
    /// with no matching earlier envelope is ignored; a shifted alignment past either end of a model is skipped.
    /// The search for the next most intense peak uses a heap instead of a scan of every peak, with the same
    /// result (largest intensity below the last one, lowest index on ties).</para>
    /// <para>One instance can be shared: <see cref="Find"/> is safe to call from several threads at once
    /// (scratch arrays are per thread, the model library fills in lazily without locks).</para>
    /// </remarks>
    public sealed class HardklorFinder
    {
        [ThreadStatic]
        private static Scratch _threadScratch;

        /// <summary>
        /// Index of the peak nearest <paramref name="mz"/> from one side (CHardklor2::BinarySearch): with
        /// <paramref name="floor"/>, the last peak at or below it; otherwise the first at or above it. Clamped to
        /// the spectrum.
        /// </summary>
        internal static int BinarySearch(double[] s, int size, double mz, bool floor)
        {
            var mid = size / 2;
            var upper = size;
            var lower = 0;

            if (mz >= s[size - 1])
                return size - 1;
            if (mz <= s[0])
                return 0;

            while (s[mid] != mz)
            {
                if (lower >= upper)
                    break;
                if (s[mid] > mz)
                {
                    upper = mid - 1;
                    mid = (lower + upper) / 2;
                }
                else
                {
                    lower = mid + 1;
                    mid = (lower + upper) / 2;
                }
            }

            if (floor && s[mid] > mz && mid > 0)
                return mid - 1;
            if (!floor && s[mid] < mz && mid < size)
                return mid + 1;
            return mid;
        }

        /// <summary>Cosine of the angle between model and observed intensities (CHardklor2::LinReg).</summary>
        internal static double LinReg(float[] mer, float[] obs, int count)
        {
            double sxx = 0, syy = 0, sxy = 0;
            for (var i = 0; i < count; i++)
            {
                // float products, as in the C++.
                sxy += mer[i] * obs[i];
                sxx += mer[i] * mer[i];
                syy += obs[i] * obs[i];
            }
            if (sxx > 0 && syy > 0 && sxy > 0)
                return sxy / Math.Sqrt(sxx * syy);
            return 0;
        }

        /// <summary>
        /// Candidate charges of the peak at <paramref name="index"/> (CHardklor2::QuickCharge): the charges
        /// whose isotope spacing matches a peak of at least a quarter of its intensity within 1.1 m/z, looking
        /// right first; none when nothing matches on the right. Ascending.
        /// </summary>
        internal static void QuickCharge(double[] mz, float[] inten, int size, int index, int minCharge, int maxCharge,
            bool[] chargeFlags, List<int> charges)
        {
            var indexMz = mz[index];
            var minIntensity = inten[index] / 4;
            for (var i = minCharge; i <= maxCharge; i++)
                chargeFlags[i] = false;

            var any = false;
            for (var j = index + 1; j < size; j++)
            {
                if (inten[j] < minIntensity)
                    continue;
                var dif = mz[j] - indexMz;
                if (dif > 1.1)
                    break;
                if (MarkCharge(dif, minCharge, maxCharge, chargeFlags))
                    any = true;
            }
            charges.Clear();
            if (!any)
                return;

            for (var j = index - 1; j >= 0; j--)
            {
                if (inten[j] < minIntensity)
                    continue;
                var dif = indexMz - mz[j];
                if (dif > 1.1)
                    break;
                MarkCharge(dif, minCharge, maxCharge, chargeFlags);
            }

            for (var i = minCharge; i <= maxCharge; i++)
            {
                if (chargeFlags[i])
                    charges.Add(i);
            }
        }

        /// <summary>
        /// Matches an aligned model to the spectrum's peaks between <paramref name="lower"/> and
        /// <paramref name="upper"/> and returns the correlation (CHardklor2::PeakMatcher). Expected peaks above 50%
        /// with no observed peak count as zero; low-abundance trailing peaks are dropped while that improves a
        /// correlation below 0.9. <paramref name="indexOverlap"/> is the first strong match that is the base
        /// peak of an earlier envelope, or -1.
        /// </summary>
        internal static double PeakMatcher(Aligned vMR, double[] mz, float[] inten, int size, bool[] mask, double lower,
            double upper, double deltaM, int matchIndex, Match match, out int indexOverlap, float[] mer, float[] obs)
        {
            match.Index.Clear();
            match.Peak.Clear();
            var bMax = false;
            var matchCount = 0;
            var n = 0;
            indexOverlap = -1;

            for (var k = 0; k < vMR.Count; k++)
            {
                double data = vMR.Data[k];
                if (data > 99.9)
                    bMax = true;

                var found = false;
                var dif = deltaM;

                var j = matchIndex;
                while (j > -1 && mz[j] >= lower)
                {
                    var massDif = mz[j] - vMR.Mass[k];
                    if (massDif < -deltaM)
                        break;
                    if (Math.Abs(massDif) < dif)
                    {
                        dif = Math.Abs(massDif);
                        found = true;
                        matchIndex = j;
                    }
                    j--;
                }

                j = matchIndex + 1;
                while (j < size && mz[j] <= upper)
                {
                    var massDif = mz[j] - vMR.Mass[k];
                    if (massDif > deltaM)
                        break;
                    if (Math.Abs(massDif) < dif)
                    {
                        dif = Math.Abs(massDif);
                        found = true;
                        matchIndex = j;
                    }
                    j++;
                }

                if (!found)
                {
                    // An expected peak above 50% with no match counts as zero.
                    if (data > 50.0)
                    {
                        mer[n] = vMR.Data[k];
                        obs[n] = 0f;
                        n++;
                        if (bMax)
                            break;
                    }
                }
                else
                {
                    mer[n] = vMR.Data[k];
                    if (mask[matchIndex] && data > 50)
                    {
                        if (indexOverlap < 0)
                            indexOverlap = matchIndex;
                    }
                    if (inten[matchIndex] < 0.1f)
                    {
                        obs[n] = 0f;
                    }
                    else
                    {
                        matchCount++;
                        obs[n] = inten[matchIndex];
                    }
                    n++;
                    match.Index.Add(matchIndex);
                    match.Peak.Add(vMR.Data[k] / 100.0f);
                }
            }

            var corr = matchCount < 2 ? 0.0 : LinReg(mer, obs, n);

            // Drop trailing low-abundance peaks (possibly overlapping other envelopes) while that helps.
            var tmpCount = matchCount;
            while (corr < 0.90 && matchCount > 2 && mer[n - 1] < 50.0)
            {
                n--;
                matchCount--;
                var corr2 = LinReg(mer, obs, n);
                if (corr2 > corr)
                {
                    corr = corr2;
                    tmpCount = matchCount;
                }
            }
            match.Count = tmpCount;
            return corr;
        }

        /// <summary>
        /// <see cref="PeakMatcher"/>'s variant for re-solving an overlapped sub-spectrum
        /// (CHardklor2::PeakMatcherB): stops at the first expected peak with no match, and trims any trailing
        /// peak while the correlation is below 0.9.
        /// </summary>
        internal static double PeakMatcherB(Aligned vMR, double[] mz, float[] inten, int size, double lower, double upper,
            double deltaM, int matchIndex, Match match, float[] mer, float[] obs)
        {
            match.Index.Clear();
            match.Peak.Clear();
            var matchCount = 0;
            var n = 0;

            for (var k = 0; k < vMR.Count; k++)
            {
                var found = false;
                var dif = deltaM;

                var j = matchIndex;
                while (j > -1 && mz[j] >= lower)
                {
                    var massDif = mz[j] - vMR.Mass[k];
                    if (massDif < -deltaM)
                        break;
                    if (Math.Abs(massDif) < dif)
                    {
                        dif = Math.Abs(massDif);
                        found = true;
                        matchIndex = j;
                    }
                    j--;
                }

                j = matchIndex + 1;
                while (j < size && mz[j] <= upper)
                {
                    var massDif = mz[j] - vMR.Mass[k];
                    if (massDif > deltaM)
                        break;
                    if (Math.Abs(massDif) < dif)
                    {
                        dif = Math.Abs(massDif);
                        found = true;
                        matchIndex = j;
                    }
                    j++;
                }

                if (!found)
                    break;

                mer[n] = vMR.Data[k];
                if (inten[matchIndex] < 0.1f)
                {
                    obs[n] = 0f;
                }
                else
                {
                    matchCount++;
                    obs[n] = inten[matchIndex];
                }
                n++;
                match.Index.Add(matchIndex);
                match.Peak.Add(vMR.Data[k] / 100.0f);
            }

            var corr = matchCount < 2 ? 0.0 : LinReg(mer, obs, n);

            var tmpCount = matchCount;
            while (corr < 0.90 && matchCount > 2)
            {
                n--;
                matchCount--;
                var corr2 = LinReg(mer, obs, n);
                if (corr2 > corr)
                {
                    corr = corr2;
                    tmpCount = matchCount;
                }
            }
            match.Count = tmpCount;
            return corr;
        }

        private readonly HardklorSettings _settings;
        private readonly HardklorModelLibrary _models;

        public HardklorFinder(HardklorSettings settings, IEnvelopeModel model)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(model);
            settings.Validate();
            _settings = settings;
            _models = new HardklorModelLibrary(model, settings.MinCharge, settings.MaxCharge, settings.MaxMoleculeMz);
        }

        public HardklorSettings Settings => _settings;

        internal HardklorModelLibrary Models => _models;

        /// <summary>
        /// The isotope envelopes in a centroided spectrum, in order of base peak m/z.
        /// </summary>
        /// <param name="mz">Peak m/z values, ascending.</param>
        /// <param name="intensity">Peak intensities, parallel to <paramref name="mz"/>.</param>
        public IReadOnlyList<IsotopeEnvelope> Find(double[] mz, double[] intensity)
        {
            ArgumentNullException.ThrowIfNull(mz);
            ArgumentNullException.ThrowIfNull(intensity);
            if (mz.Length != intensity.Length)
                throw new ArgumentException(@"m/z and intensity arrays differ in length.");
            if (mz.Length == 0)
                return Array.Empty<IsotopeEnvelope>();

            var scratch = _threadScratch ??= new Scratch();
            var run = new Run(this, scratch, mz, intensity);
            return run.Execute();
        }

        private static bool MarkCharge(double dif, int minCharge, int maxCharge, bool[] chargeFlags)
        {
            if (!(dif > 0))
                return false; // a duplicate m/z: undefined in the C++, no charge in effect
            var rawCh = 1 / dif;
            var ch = (int)(rawCh + 0.5);
            var rawChR = rawCh - (int)rawCh;
            if (rawChR > 0.2 && rawChR < 0.8)
                return false;
            if (ch < minCharge || ch > maxCharge)
                return false;
            chargeFlags[ch] = true;
            return true;
        }

        /// <summary>A model envelope aligned to the spectrum (Hardklor's vector&lt;Result&gt; vMR).</summary>
        internal sealed class Aligned
        {
            public double[] Mass = new double[16];
            public float[] Data = new float[16];
            public int Count;

            public void Set(EnvelopeModel model, double shift)
            {
                if (Mass.Length < model.Size)
                {
                    Mass = new double[model.Size * 2];
                    Data = new float[model.Size * 2];
                }
                for (var k = 0; k < model.Size; k++)
                {
                    Mass[k] = model.Mz[k] + shift;
                    Data[k] = model.Intensity[k];
                }
                Count = model.Size;
            }
        }

        /// <summary>Result of matching an aligned model to the spectrum.</summary>
        internal sealed class Match
        {
            public readonly List<int> Index = new List<int>();
            public readonly List<float> Peak = new List<float>();
            public int Count;

            public void CopyFrom(Match other)
            {
                Index.Clear();
                Index.AddRange(other.Index);
                Peak.Clear();
                Peak.AddRange(other.Peak);
                Count = other.Count;
            }
        }

        /// <summary>Hardklor's pepHit: one envelope found so far.</summary>
        internal struct PepHit
        {
            public int BasePeakIndex;
            public int Charge;
            public int LowIndex;
            public int HighIndex;
            public float Intensity;
            public float Area;
            public double LowMz;
            public double HighMz;
            public double MonoMass;
            public double Corr;
            public int PeakStart;
            public int PeakCount;
        }

        /// <summary>Per-thread working storage, reused across spectra.</summary>
        internal sealed class Scratch
        {
            public float[] S = Array.Empty<float>();
            public float[] Orig = Array.Empty<float>();
            public float[] Ref = Array.Empty<float>();
            public bool[] Mask = Array.Empty<bool>();
            public double[] TmpMz = Array.Empty<double>();
            public float[] TmpInt = Array.Empty<float>();
            public float[] HeapValue = Array.Empty<float>();
            public int[] HeapIndex = Array.Empty<int>();
            public float[] Mer = new float[64];
            public float[] Obs = new float[64];
            public bool[] ChargeFlags = new bool[101];
            public readonly List<int> Charges = new List<int>();
            public readonly List<int> SubCharges = new List<int>();
            public readonly Aligned VMR = new Aligned();
            public readonly Aligned SubVMR = new Aligned();
            public readonly Match VMatch = new Match();
            public readonly Match VMatch2 = new Match();
            public readonly Match BestMatch = new Match();
            public readonly Match SubMatch = new Match();
            public readonly Match SubBest = new Match();
            public readonly List<PepHit> Peps = new List<PepHit>();
            public readonly List<int> PepPeaks = new List<int>();
            public readonly List<int> RefineList = new List<int>();
            public int[] PepMask = Array.Empty<int>();

            public void Ensure(int n)
            {
                if (S.Length >= n)
                    return;
                var size = Math.Max(n, S.Length * 2);
                S = new float[size];
                Orig = new float[size];
                Ref = new float[size];
                Mask = new bool[size];
                TmpMz = new double[size];
                TmpInt = new float[size];
                HeapValue = new float[size * 2];
                HeapIndex = new int[size * 2];
            }

            public void EnsureModel(int size)
            {
                if (Mer.Length >= size)
                    return;
                Mer = new float[size * 2];
                Obs = new float[size * 2];
            }
        }

        /// <summary>One call of <see cref="Find"/>: CHardklor2::QuickHardklor on one spectrum.</summary>
        private sealed class Run
        {
            private readonly HardklorFinder _finder;
            private readonly HardklorSettings _cs;
            private readonly Scratch _x;
            private readonly double[] _mz;
            private readonly int _n;
            private int _heapCount;

            public Run(HardklorFinder finder, Scratch scratch, double[] mz, double[] intensity)
            {
                _finder = finder;
                _cs = finder._settings;
                _x = scratch;
                _mz = mz;
                _n = mz.Length;
                _heapCount = 0;
                scratch.Ensure(_n);
                for (var i = 0; i < _n; i++)
                {
                    var v = (float)intensity[i];
                    scratch.S[i] = v;
                    scratch.Orig[i] = v;
                    scratch.Ref[i] = v;
                    scratch.Mask[i] = false;
                }
            }

            public IReadOnlyList<IsotopeEnvelope> Execute()
            {
                var x = _x;
                var s = x.S;
                var mz = _mz;
                var n = _n;
                var cs = _cs;
                x.Peps.Clear();
                x.PepPeaks.Clear();

                // Lowest intensity: the search stops there, to avoid overfitting partial noise peaks.
                var lowPoint = 9999999999999.9f;
                for (var i = 0; i < n; i++)
                {
                    if (s[i] < 1)
                        continue; // zero is not allowed as a low point
                    if (s[i] < lowPoint)
                        lowPoint = s[i];
                }

                for (var i = 0; i < n; i++)
                {
                    if (s[i] > 0)
                        HeapPush(s[i], i);
                }

                var maxHeight = 9999999999999.9f;
                var vMR = x.VMR;
                var vMatch = x.VMatch;
                var vMatch2 = x.VMatch2;
                var best = x.BestMatch;
                var charges = x.Charges;
                PepHit ph2 = default;

                while (true)
                {
                    // Most intense remaining peak below the last one. Peaks only lose intensity as envelopes are
                    // subtracted, and each change pushes a fresh heap entry, so stale entries are skipped.
                    var max = 0f;
                    var maxIndex = -1;
                    while (_heapCount > 0)
                    {
                        var value = x.HeapValue[0];
                        var index = x.HeapIndex[0];
                        HeapPop();
                        if (value != s[index] || value >= maxHeight)
                            continue;
                        max = value;
                        maxIndex = index;
                        break;
                    }
                    if (max < lowPoint)
                        break;

                    var deltaM = cs.PeakWidth(mz[maxIndex]);
                    QuickCharge(mz, s, n, maxIndex, cs.MinCharge, cs.MaxCharge, x.ChargeFlags, charges);

                    var bestCorr = 0.0;
                    var bestMatchCount = 0;
                    double bestLow = 0, bestHigh = 0, bestMass = 0;
                    float bestDA = 0;
                    var bestCharge = 0;
                    var bestKeepPH = false;
                    PepHit bestPH = default;
                    var bestOverlap = -1;
                    int bestLowIndex = 0, bestHighIndex = 0;

                    foreach (var charge in charges)
                    {
                        var model = _finder._models.Get(charge, mz[maxIndex]);
                        if (model == null || model.Size == 0)
                            continue;
                        x.EnsureModel(model.Size);

                        // Align the model's three most abundant peaks in turn to the observed peak.
                        float top0 = 0, top1 = 0, top2 = 0;
                        int idx0 = -1, idx1 = -1, idx2 = -1;
                        for (var k = 0; k < model.Size; k++)
                        {
                            var v = model.Intensity[k];
                            if (v > top0)
                            {
                                top2 = top1;
                                top1 = top0;
                                top0 = v;
                                idx2 = idx1;
                                idx1 = idx0;
                                idx0 = k;
                            }
                            else if (v > top1)
                            {
                                top2 = top1;
                                top1 = v;
                                idx2 = idx1;
                                idx1 = k;
                            }
                            else if (v > top2)
                            {
                                top2 = v;
                                idx2 = k;
                            }
                        }

                        for (var t = 0; t < 3; t++)
                        {
                            var aligned = t == 0 ? idx0 : t == 1 ? idx1 : idx2;
                            if (aligned < 0)
                                continue;

                            var shft = mz[maxIndex] - model.Mz[aligned];
                            vMR.Set(model, shft);
                            var thisMaxIndex = 0;
                            var lower = 5000.0;
                            var upper = 0.0;
                            for (var k = 0; k < vMR.Count; k++)
                            {
                                if (vMR.Data[k] > 99.999)
                                    thisMaxIndex = k;
                                if (vMR.Mass[k] < lower)
                                    lower = vMR.Mass[k];
                                if (vMR.Mass[k] > upper)
                                    upper = vMR.Mass[k];
                            }
                            var da = model.Area;
                            lower -= 0.1;
                            upper += 0.1;

                            var lowIndex = BinarySearch(mz, n, lower, true);
                            var highIndex = BinarySearch(mz, n, upper, false);

                            // Skip when the model's base peak lands on an already solved base peak.
                            if (!CheckForPeak(vMR.Mass[thisMaxIndex]))
                                continue;

                            var corr = PeakMatcher(vMR, mz, s, n, x.Mask, lower, upper, deltaM / 2, maxIndex, vMatch,
                                out var indexOverlap, x.Mer, x.Obs);
                            var matchCount = vMatch.Count;

                            // Overlap with an envelope found earlier: re-solve that envelope with this model's
                            // share removed, and keep the merged result if it correlates better.
                            var keepPH = false;
                            var m = -1;
                            if (indexOverlap > -1)
                            {
                                m = FindPep(indexOverlap);
                                if (m >= 0)
                                {
                                    var pep = x.Peps[m];
                                    var tmpCount = 0;
                                    var xi = 0;
                                    var subIndex = -1;
                                    for (var j = pep.LowIndex; j <= pep.HighIndex; j++)
                                    {
                                        while (xi < vMatch.Index.Count && j > vMatch.Index[xi])
                                            xi++;
                                        x.TmpMz[tmpCount] = mz[j];
                                        if (xi < vMatch.Index.Count && j == vMatch.Index[xi])
                                        {
                                            x.TmpInt[tmpCount] = x.Orig[j] - vMatch.Peak[xi] * max;
                                            xi++;
                                        }
                                        else
                                        {
                                            x.TmpInt[tmpCount] = x.Orig[j];
                                        }
                                        if (j == indexOverlap)
                                            subIndex = tmpCount;
                                        tmpCount++;
                                    }

                                    if (subIndex >= 0 && MatchSubSpectrum(x.TmpMz, x.TmpInt, tmpCount, subIndex, out ph2)
                                        && ph2.Corr + 0.025 > pep.Corr)
                                    {
                                        xi = 0;
                                        for (var j = lowIndex; j <= highIndex; j++)
                                        {
                                            if (xi < tmpCount && mz[j] == x.TmpMz[xi])
                                            {
                                                x.Ref[j] = x.Orig[j] + x.TmpInt[xi];
                                                xi++;
                                            }
                                            else
                                            {
                                                x.Ref[j] = s[j];
                                            }
                                        }

                                        var corr3 = PeakMatcher(vMR, mz, x.Ref, n, x.Mask, lower, upper, deltaM / 2,
                                            maxIndex, vMatch2, out _, x.Mer, x.Obs);
                                        if (corr3 > corr)
                                        {
                                            corr = corr3;
                                            vMatch.CopyFrom(vMatch2);
                                            matchCount = vMatch2.Count;
                                            keepPH = true;
                                        }
                                    }
                                }
                            }

                            var tCorr = bestMatchCount == 0 ? 0 : 0.025 * (matchCount - bestMatchCount) / bestMatchCount;
                            if (corr > cs.Correlation && corr + tCorr > bestCorr)
                            {
                                best.CopyFrom(vMatch);
                                bestMatchCount = matchCount;
                                bestCorr = corr;
                                bestMass = model.ZeroMass + shft * charge;
                                bestCharge = charge;
                                bestDA = da;
                                bestLow = lower;
                                bestHigh = upper;
                                bestKeepPH = keepPH;
                                if (keepPH)
                                    bestPH = ph2;
                                bestOverlap = m;
                                bestLowIndex = lowIndex;
                                bestHighIndex = highIndex;
                            }
                        }
                    }

                    if (bestCorr > cs.Correlation)
                    {
                        if (bestKeepPH)
                        {
                            var o = x.Peps[bestOverlap];
                            o.Area = bestPH.Area;
                            o.Intensity = bestPH.Intensity;
                            o.Corr = bestPH.Corr;
                            o.Charge = bestPH.Charge;
                            o.MonoMass = bestPH.MonoMass;
                            x.Peps[bestOverlap] = o;
                        }

                        var peakStart = x.PepPeaks.Count;
                        x.PepPeaks.AddRange(best.Index);
                        x.Peps.Add(new PepHit
                        {
                            Area = bestDA,
                            BasePeakIndex = maxIndex,
                            Charge = bestCharge,
                            Corr = bestCorr,
                            HighMz = bestHigh,
                            Intensity = max,
                            LowMz = bestLow,
                            MonoMass = bestMass,
                            LowIndex = bestLowIndex,
                            HighIndex = bestHighIndex,
                            PeakStart = peakStart,
                            PeakCount = best.Index.Count,
                        });
                        x.Mask[maxIndex] = true;

                        for (var k = 0; k < best.Index.Count; k++)
                        {
                            var idx = best.Index[k];
                            if (best.Peak[k] * max / s[idx] > 0.5)
                                s[idx] = -s[idx];
                            else
                                s[idx] -= best.Peak[k] * max;
                            if (s[idx] > 0)
                                HeapPush(s[idx], idx);
                        }
                    }

                    maxHeight = max;
                }

                SortPepsByBasePeak();
                RefineHits();
                return BuildResults();
            }

            /// <summary>
            /// Whether the observed peak nearest <paramref name="mass"/> is within one peak width and is not the
            /// base peak of an envelope already found (CHardklor2::CheckForPeak).
            /// </summary>
            private bool CheckForPeak(double mass)
            {
                var s = _mz;
                var size = _n;
                var mid = size / 2;
                var upper = size;
                var lower = 0;

                var fwhm = _cs.PeakWidth(mass);

                if (mass >= s[size - 1])
                {
                    mid = size - 1;
                }
                else if (mass <= s[0])
                {
                    mid = 0;
                }
                else
                {
                    while (s[mid] != mass)
                    {
                        if (lower >= upper)
                            break;
                        if (s[mid] > mass)
                        {
                            upper = mid - 1;
                            mid = (lower + upper) / 2;
                        }
                        else
                        {
                            lower = mid + 1;
                            mid = (lower + upper) / 2;
                        }
                    }
                }

                var dif = Math.Abs(s[mid] - mass);
                if (mid > 0)
                {
                    var massDif = Math.Abs(s[mid - 1] - mass);
                    if (massDif < dif)
                    {
                        dif = massDif;
                        mid--;
                    }
                }
                if (mid < size - 1)
                {
                    var massDif = Math.Abs(s[mid + 1] - mass);
                    if (massDif < dif)
                    {
                        dif = massDif;
                        mid++;
                    }
                }

                if (dif < fwhm)
                    return !_x.Mask[mid];
                return false;
            }

            private int FindPep(int basePeakIndex)
            {
                var peps = _x.Peps;
                for (var m = 0; m < peps.Count; m++)
                {
                    if (peps[m].BasePeakIndex == basePeakIndex)
                        return m;
                }
                return -1;
            }

            /// <summary>
            /// Re-solves a sub-spectrum around the base peak at <paramref name="peakIndex"/>
            /// (CHardklor2::MatchSubSpectrum), subtracting the best envelope from <paramref name="inten"/>.
            /// </summary>
            private bool MatchSubSpectrum(double[] mz, float[] inten, int size, int peakIndex, out PepHit pep)
            {
                pep = default;
                var x = _x;
                var cs = _cs;
                var max = inten[peakIndex];
                var vMR = x.SubVMR;
                var match = x.SubMatch;
                var best = x.SubBest;
                var charges = x.SubCharges;

                var deltaM = cs.PeakWidth(mz[peakIndex]);
                QuickCharge(mz, inten, size, peakIndex, cs.MinCharge, cs.MaxCharge, x.ChargeFlags, charges);

                var bestCorr = 0.0;
                var bestMatchCount = 0;
                double bestMass = 0;
                float bestDA = 0;
                var bestCharge = 0;

                foreach (var charge in charges)
                {
                    var model = _finder._models.Get(charge, mz[peakIndex]);
                    if (model == null || model.Size == 0)
                        continue;
                    x.EnsureModel(model.Size);

                    var dif = 0f;
                    var maxIdx = 0;
                    for (var k = 0; k < model.Size; k++)
                    {
                        if (model.Intensity[k] > dif)
                        {
                            dif = model.Intensity[k];
                            maxIdx = k;
                        }
                    }

                    // The base peak, then one peak to either side of it.
                    for (var t = 0; t < 3; t++)
                    {
                        var aligned = t == 0 ? maxIdx : t == 1 ? maxIdx - 1 : maxIdx + 1;
                        if (aligned < 0 || aligned >= model.Size)
                            continue;

                        var shft = mz[peakIndex] - model.Mz[aligned];
                        vMR.Set(model, shft);
                        var lower = 5000.0;
                        var upper = 0.0;
                        for (var k = 0; k < vMR.Count; k++)
                        {
                            if (vMR.Mass[k] < lower)
                                lower = vMR.Mass[k];
                            if (vMR.Mass[k] > upper)
                                upper = vMR.Mass[k];
                        }
                        var da = model.Area;
                        lower -= 0.1;
                        upper += 0.1;

                        var corr = PeakMatcherB(vMR, mz, inten, size, lower, upper, deltaM / 2, peakIndex, match, x.Mer, x.Obs);
                        var matchCount = match.Count;

                        if (corr > bestCorr || (corr > cs.Correlation && corr + 0.025 * (matchCount - bestMatchCount) > bestCorr))
                        {
                            best.CopyFrom(match);
                            bestMatchCount = matchCount;
                            bestCorr = corr;
                            bestMass = model.ZeroMass + shft * charge;
                            bestCharge = charge;
                            bestDA = da;
                        }
                    }
                }

                if (!(bestCorr > cs.Correlation))
                    return false;

                pep.Area = bestDA;
                pep.Charge = bestCharge;
                pep.Corr = bestCorr;
                pep.MonoMass = bestMass;
                pep.Intensity = inten[peakIndex];

                for (var k = 0; k < best.Index.Count; k++)
                {
                    var idx = best.Index[k];
                    if (best.Peak[k] * max / inten[idx] > 0.5)
                        inten[idx] = -inten[idx];
                    else
                        inten[idx] -= best.Peak[k] * max;
                }
                return true;
            }

            private void SortPepsByBasePeak()
            {
                // Hardklor qsorts by base peak index; insertion order breaks ties here so results are stable.
                var peps = _x.Peps;
                for (var i = 1; i < peps.Count; i++)
                {
                    var item = peps[i];
                    var j = i - 1;
                    while (j >= 0 && peps[j].BasePeakIndex > item.BasePeakIndex)
                    {
                        peps[j + 1] = peps[j];
                        j--;
                    }
                    peps[j + 1] = item;
                }
            }

            /// <summary>
            /// Keeps at most Depth envelopes, the most intense, among those whose base peaks lie within 0.5 m/z of
            /// each envelope's base peak (CHardklor2::RefineHits). Removes false hits on the jagged tails of large
            /// peaks.
            /// </summary>
            private void RefineHits()
            {
                var x = _x;
                var peps = x.Peps;
                var count = peps.Count;
                if (x.PepMask.Length < count)
                    x.PepMask = new int[Math.Max(count, x.PepMask.Length * 2)];
                var pepMask = x.PepMask;
                Array.Clear(pepMask, 0, count);
                var list = x.RefineList;
                var mz = _mz;
                var orig = x.Orig;
                var depth = _cs.Depth;

                for (var i = 0; i < count; i++)
                {
                    if (pepMask[i] > 0)
                        continue;

                    var lowp = mz[peps[i].BasePeakIndex] - 0.5;
                    var highp = mz[peps[i].BasePeakIndex] + 0.5;

                    list.Clear();
                    list.Add(i);

                    for (var j = i - 1; j > -1; j--)
                    {
                        if (mz[peps[j].BasePeakIndex] < lowp)
                            break;
                        if (pepMask[j] > 0)
                            continue;
                        InsertByIntensity(list, j, peps, orig);
                    }

                    for (var j = i + 1; j < count; j++)
                    {
                        if (mz[peps[j].BasePeakIndex] > highp)
                            break;
                        if (pepMask[j] > 0)
                            continue;
                        InsertByIntensity(list, j, peps, orig);
                    }

                    for (var k = depth; k < list.Count; k++)
                        pepMask[list[k]] = 1;
                }

                var keep = 0;
                for (var i = 0; i < count; i++)
                {
                    if (pepMask[i] != 0)
                        continue;
                    peps[keep++] = peps[i];
                }
                peps.RemoveRange(keep, count - keep);
            }

            private static void InsertByIntensity(List<int> list, int j, List<PepHit> peps, float[] orig)
            {
                // Before the first entry it is more intense than: the list runs from high to low.
                var intensity = orig[peps[j].BasePeakIndex];
                var at = 0;
                while (at < list.Count && !(intensity > orig[peps[list[at]].BasePeakIndex]))
                    at++;
                list.Insert(at, j);
            }

            private IReadOnlyList<IsotopeEnvelope> BuildResults()
            {
                var x = _x;
                var peps = x.Peps;
                var mz = _mz;
                var results = new IsotopeEnvelope[peps.Count];
                for (var i = 0; i < peps.Count; i++)
                {
                    var p = peps[i];
                    var indices = new int[p.PeakCount];
                    x.PepPeaks.CopyTo(p.PeakStart, indices, 0, p.PeakCount);
                    Array.Sort(indices);
                    var distinct = 0;
                    for (var k = 0; k < indices.Length; k++)
                    {
                        if (k == 0 || indices[k] != indices[k - 1])
                            indices[distinct++] = indices[k];
                    }
                    if (distinct != indices.Length)
                        Array.Resize(ref indices, distinct);

                    var intensity = _cs.DistributionArea ? p.Area * p.Intensity : p.Intensity;
                    results[i] = new IsotopeEnvelope(p.MonoMass, p.Charge, intensity, mz[p.BasePeakIndex], p.Corr,
                        mz[p.LowIndex], mz[p.HighIndex], indices);
                }
                return results;
            }

            private void HeapPush(float value, int index)
            {
                var x = _x;
                if (_heapCount == x.HeapValue.Length)
                {
                    Array.Resize(ref x.HeapValue, _heapCount * 2);
                    Array.Resize(ref x.HeapIndex, _heapCount * 2);
                }
                var values = x.HeapValue;
                var indices = x.HeapIndex;
                var i = _heapCount++;
                while (i > 0)
                {
                    var parent = (i - 1) >> 1;
                    if (!Before(value, index, values[parent], indices[parent]))
                        break;
                    values[i] = values[parent];
                    indices[i] = indices[parent];
                    i = parent;
                }
                values[i] = value;
                indices[i] = index;
            }

            private void HeapPop()
            {
                var values = _x.HeapValue;
                var indices = _x.HeapIndex;
                var count = --_heapCount;
                if (count == 0)
                    return;
                var value = values[count];
                var index = indices[count];
                var i = 0;
                while (true)
                {
                    var child = 2 * i + 1;
                    if (child >= count)
                        break;
                    if (child + 1 < count && Before(values[child + 1], indices[child + 1], values[child], indices[child]))
                        child++;
                    if (!Before(values[child], indices[child], value, index))
                        break;
                    values[i] = values[child];
                    indices[i] = indices[child];
                    i = child;
                }
                values[i] = value;
                indices[i] = index;
            }

            /// <summary>Heap order: higher intensity first, then lower index (the C++ scan's first maximum).</summary>
            private static bool Before(float v1, int i1, float v2, int i2) => v1 > v2 || (v1 == v2 && i1 < i2);
        }
    }
}
