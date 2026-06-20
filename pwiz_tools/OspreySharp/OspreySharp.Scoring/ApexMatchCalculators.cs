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
using System.Linq;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Shared apex-spectrum fragment-match statistics for the mass-accuracy /
    /// explained-intensity family (explained_intensity,
    /// mass_accuracy_deviation_mean, abs_mass_accuracy_deviation_mean). These three
    /// features come from ONE pass over the candidate fragments against the apex MS2
    /// spectrum (the inline <c>ComputeApexMatchFeatures</c>), so the producer runs
    /// that pass once and the three calculators read its fields -- guaranteeing the
    /// same closest-peak picks and avoiding a 3x re-scan. Published once per
    /// candidate to the <see cref="OspreyScoringContext"/> byproduct cache.
    ///
    /// NOTE: consecutive_ions (feature 7) does NOT use this set -- it is a separate
    /// boolean <see cref="SpectralScorer.HasMatch"/> pass keyed by ion type +
    /// ordinal, with no intensity / error / closest-peak pick.
    /// </summary>
    internal sealed class ApexFragmentMatchSet
    {
        public double TotalIntensity;
        public double MatchedIntensity;
        public double MassErrSum;
        public double AbsMassErrSum;
        public int NMatched;

        public static ApexFragmentMatchSet GetOrCompute(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            if (context.TryGetInfo(out ApexFragmentMatchSet matchSet))
                return matchSet;
            matchSet = Compute(context, peakData);
            context.AddInfo(matchSet);
            return matchSet;
        }

        private static ApexFragmentMatchSet Compute(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            var matchSet = new ApexFragmentMatchSet();

            var apexSpectrum = peakData.ApexSpectrum;
            var candidate = peakData.Candidate;
            var tolerance = context.Config.FragmentTolerance;

            // Do NOT early-return when the apex spectrum or candidate fragments are
            // empty. Rust's compute_mass_accuracy (osprey-scoring/src/lib.rs:464)
            // handles empty inputs by returning (0.0, tolerance, tolerance) -- so a
            // candidate that reaches here with no matchable fragments still
            // contributes the calibrated tolerance as its abs mass error. Let the
            // matching loop run zero iterations and fall through to the nMatched==0
            // fallback for cross-impl symmetry.
            double totalIntensity = 0.0;
            if (apexSpectrum.Intensities != null)
            {
                for (int i = 0; i < apexSpectrum.Intensities.Length; i++)
                    totalIntensity += apexSpectrum.Intensities[i];
            }

            double matchedIntensity = 0.0;
            double massErrSum = 0.0;
            double absMassErrSum = 0.0;
            int nMatched = 0;

            if (apexSpectrum.Mzs != null && apexSpectrum.Intensities != null &&
                candidate.Fragments != null)
            {
                foreach (var frag in candidate.Fragments)
                {
                    double tolDa = tolerance.ToleranceDa(frag.Mz);
                    double lower = frag.Mz - tolDa;
                    double upper = frag.Mz + tolDa;

                    int lo = ScoringMath.BinarySearchLowerBound(apexSpectrum.Mzs, lower);
                    double bestError = double.MaxValue;
                    double bestIntensity = 0.0;
                    double bestMz = 0.0;
                    bool found = false;

                    // Match closest peak by m/z (not most intense). Strict < keeps
                    // the first/lower-index peak on an equal-distance tie. Matches
                    // Rust SpectralScorer::match_fragments in lib.rs:2239.
                    for (int k = lo; k < apexSpectrum.Mzs.Length && apexSpectrum.Mzs[k] <= upper; k++)
                    {
                        double errorDa = Math.Abs(apexSpectrum.Mzs[k] - frag.Mz);
                        if (errorDa < bestError)
                        {
                            bestError = errorDa;
                            bestIntensity = apexSpectrum.Intensities[k];
                            bestMz = apexSpectrum.Mzs[k];
                            found = true;
                        }
                    }

                    if (found)
                    {
                        matchedIntensity += bestIntensity;
                        double err = tolerance.MassError(frag.Mz, bestMz);
                        massErrSum += err;
                        absMassErrSum += Math.Abs(err);
                        nMatched++;
                    }
                }
            }

            matchSet.TotalIntensity = totalIntensity;
            matchSet.MatchedIntensity = matchedIntensity;
            matchSet.MassErrSum = massErrSum;
            matchSet.AbsMassErrSum = absMassErrSum;
            matchSet.NMatched = nMatched;
            return matchSet;
        }
    }

    /// <summary>
    /// consecutive_ions: the longest run of consecutive b- or y-ion ordinals whose
    /// theoretical m/z matches a peak in the apex MS2 spectrum. Integer-valued
    /// (byte widened to double). Separate from the mass-accuracy family -- a
    /// boolean <see cref="SpectralScorer.HasMatch"/> pass keyed by ion type +
    /// ordinal, with the same closed-both-ends tolerance window.
    /// </summary>
    internal sealed class ConsecutiveIonsCalc : ApexSpectrumOspreyFeatureCalculator
    {
        public override string Name { get { return "consecutive_ions"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            var candidate = peakData.Candidate;
            if (candidate.Fragments == null || candidate.Fragments.Count == 0)
                return 0.0;

            var apexMzs = peakData.ApexSpectrum.Mzs;
            var tolerance = context.Config.FragmentTolerance;

            // Group matched fragments by ion type, recording the ordinals that
            // matched a spectrum peak.
            var bMatched = new HashSet<int>();
            var yMatched = new HashSet<int>();

            foreach (var frag in candidate.Fragments)
            {
                if (SpectralScorer.HasMatch(frag.Mz, apexMzs, tolerance))
                {
                    if (frag.Annotation.IonType == IonType.B)
                        bMatched.Add(frag.Annotation.Ordinal);
                    else if (frag.Annotation.IonType == IonType.Y)
                        yMatched.Add(frag.Annotation.Ordinal);
                }
            }

            byte maxConsecutive = 0;
            maxConsecutive = Math.Max(maxConsecutive, LongestConsecutiveRun(bMatched));
            maxConsecutive = Math.Max(maxConsecutive, LongestConsecutiveRun(yMatched));
            return maxConsecutive;
        }

        private static byte LongestConsecutiveRun(HashSet<int> ordinals)
        {
            if (ordinals.Count == 0)
                return 0;

            var sorted = ordinals.OrderBy(x => x).ToList();
            byte maxRun = 1;
            byte currentRun = 1;

            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] == sorted[i - 1] + 1)
                {
                    currentRun++;
                    if (currentRun > maxRun)
                        maxRun = currentRun;
                }
                else
                {
                    currentRun = 1;
                }
            }

            return maxRun;
        }
    }

    /// <summary>
    /// explained_intensity: matched fragment intensity as a fraction of the apex
    /// spectrum's total intensity. The denominator guard is strict
    /// (<c>totalIntensity &gt; 1e-12</c>); below it the feature is exactly 0.0
    /// (never NaN/Inf).
    /// </summary>
    internal sealed class ExplainedIntensityCalc : ApexSpectrumOspreyFeatureCalculator
    {
        public override string Name { get { return "explained_intensity"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            var matchSet = ApexFragmentMatchSet.GetOrCompute(context, peakData);
            return matchSet.TotalIntensity > 1e-12
                ? matchSet.MatchedIntensity / matchSet.TotalIntensity
                : 0.0;
        }
    }

    /// <summary>
    /// mass_accuracy_deviation_mean: the SIGNED mean fragment mass error (in the
    /// tolerance unit, typically ppm) over matched fragments. No-match fallback is
    /// exactly 0.0 (Rust returns a 0.0 mean on empty matches).
    /// </summary>
    internal sealed class MassAccuracyMeanCalc : ApexSpectrumOspreyFeatureCalculator
    {
        public override string Name { get { return "mass_accuracy_deviation_mean"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            var matchSet = ApexFragmentMatchSet.GetOrCompute(context, peakData);
            return matchSet.NMatched > 0
                ? matchSet.MassErrSum / matchSet.NMatched
                : 0.0;
        }
    }

    /// <summary>
    /// abs_mass_accuracy_deviation_mean: the mean ABSOLUTE fragment mass error over
    /// matched fragments. CRITICAL no-match fallback: returns the live (calibrated)
    /// <c>FragmentTolerance.Tolerance</c>, NOT 0.0 -- reporting the worst-case
    /// tolerance penalizes unmatched entries in FDR. Leaving it 0.0 historically
    /// caused ~65 divergent Astral rows. (Rust compute_mass_accuracy returns
    /// (0.0, tolerance, tolerance) on empty matches, osprey-scoring/src/lib.rs:462.)
    /// </summary>
    internal sealed class AbsMassAccuracyMeanCalc : ApexSpectrumOspreyFeatureCalculator
    {
        public override string Name { get { return "abs_mass_accuracy_deviation_mean"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            var matchSet = ApexFragmentMatchSet.GetOrCompute(context, peakData);
            return matchSet.NMatched > 0
                ? matchSet.AbsMassErrSum / matchSet.NMatched
                : context.Config.FragmentTolerance.Tolerance;
        }
    }
}
