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

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// xcorr (feature 6): Comet-style cross-correlation of the candidate's
    /// theoretical fragments against the chosen apex MS2 spectrum, scored through
    /// the resolution strategy (Unit f64 cache / HRAM f32 cache). A SINGLE call --
    /// no Savitzky-Golay sweep, no shared byproduct.
    ///
    /// INDEX TRAP: this feature indexes the preprocessed cache at the WINDOW-GLOBAL
    /// apex index (<see cref="IOspreyApexSpectrumPeakData.ApexGlobalIndex"/> =
    /// WindowStartIndex + candidate-local apex). It is a DIFFERENT index space from
    /// the SG sweep (features 17/18), which bound-checks a candidate-local index
    /// against WindowLength before mapping to global. Do not unify the two.
    ///
    /// RESOLUTION-MODE CACHE TYPE: the call MUST go through
    /// <see cref="IResolutionStrategy.ScoreXcorr"/>; Unit reads
    /// preprocessed.Doubles (f64), HRAM reads preprocessed.Floats (f32 narrowed on
    /// store). Never re-preprocess or assume f64. Per net8.0-canonical parity, gate
    /// on net8.0.
    ///
    /// PERF: the shared <see cref="OspreyScoringContext.XcorrScratchPool"/> is
    /// threaded into ScoreXcorr so HRAM scoring avoids per-call 100K-bin LOH
    /// allocation. This family is the perf gate -- keep the pool on the call.
    /// Mirrors inline AbstractScoringTask.cs apex xcorr.
    /// </summary>
    internal sealed class XcorrCalc : ApexSpectrumOspreyFeatureCalculator
    {
        public override string Name { get { return "xcorr"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectrumPeakData peakData)
        {
            // Single apex-spectrum xcorr via the resolution strategy. The Spectrum
            // arg is passed for fidelity with the inline call; the Unit/HRAM cache
            // path reads the cache row at ApexGlobalIndex and ignores it.
            return context.Resolution.ScoreXcorr(
                context.PreprocessedXcorr, peakData.ApexGlobalIndex, peakData.ApexSpectrum,
                peakData.Candidate, context.Scorer, context.XcorrScratchPool);
        }
    }

    /// <summary>
    /// Shared Savitzky-Golay weighted sweep for features 17 (sg_weighted_xcorr)
    /// and 18 (sg_weighted_cosine). Both features share the SAME apex+/-2 offset
    /// loop, the SAME asymmetric boundary skip, and the SAME candidate-local ->
    /// window-global index mapping; they differ only in the per-offset scalar
    /// (ScoreXcorr vs ComputeCosineAtScan). Computing the sweep once guarantees the
    /// two features use identical index handling / boundary skips and avoids a
    /// second pass over the apex+/-2 spectra. Published once per candidate to the
    /// <see cref="OspreyScoringContext"/> byproduct cache. Mirrors the inline
    /// AbstractScoringTask.cs SG loop.
    ///
    /// INDEX TRAP: candIdx = ApexLocalIndex + offset, bound-checked against
    /// [0, WindowLength) (candidate-local rangeLen, NOT WindowSpectra.Count), then
    /// mapped to globalIdx = WindowStartIndex + candIdx. At offset 0 globalIdx
    /// equals feature 6's ApexGlobalIndex by construction, but the bounds convention
    /// is deliberately distinct -- a naive reuse of one "apex global index" would
    /// break the window-edge bound check.
    ///
    /// ASYMMETRIC BOUNDARY SKIP / NO RENORMALIZATION: near a window edge fewer than
    /// 5 terms contribute; out-of-range offsets are continue'd (NOT added as zero)
    /// and the partial sum is left as-is (no divide by contributing-weight sum) to
    /// match Rust.
    ///
    /// ACCUMULATION ORDER (f64): the sweep runs strictly offset -2,-1,0,1,2 with
    /// (score * weight) computed before the +=, accumulating left-to-right; both
    /// sums seed 0.0. Do not vectorize or reorder.
    ///
    /// SHARED MUTABLE SCRATCH: PreprocessedXcorr.VisitedBins and the rented
    /// XcorrScratchPool are mutated per ScoreXcorr call. Running the sweep as a
    /// single producer pass serializes those mutations across features 17/18 within
    /// a candidate (no interleaving).
    /// </summary>
    internal sealed class SgWeightedSweep
    {
        // Savitzky-Golay quadratic filter weights for length 5, center offset.
        // Matches Rust pipeline.rs sg_weights: [-3/35, 12/35, 17/35, 12/35, -3/35].
        // Relocated verbatim from AbstractScoringTask.cs.
        private static readonly double[] SG_WEIGHTS =
        {
            -3.0 / 35.0,
            12.0 / 35.0,
            17.0 / 35.0,
            12.0 / 35.0,
            -3.0 / 35.0,
        };

        public double SgXcorr;   // feature 17
        public double SgCosine;  // feature 18

        public static SgWeightedSweep GetOrCompute(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            if (context.TryGetInfo(out SgWeightedSweep sweep))
                return sweep;
            sweep = Compute(context, peakData);
            context.AddInfo(sweep);
            return sweep;
        }

        private static SgWeightedSweep Compute(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            var sweep = new SgWeightedSweep();

            var resolution = context.Resolution;
            var preprocessedXcorr = context.PreprocessedXcorr;
            var scorer = context.Scorer;
            var pool = context.XcorrScratchPool;
            var config = context.Config;
            var candidate = peakData.Candidate;

            double sgXcorr = 0.0;
            double sgCosine = 0.0;
            for (int offset = -2; offset <= 2; offset++)
            {
                double weight = SG_WEIGHTS[offset + 2];
                // The bounded apex+/-N accessor owns the candidate-local ->
                // window-global index mapping and the asymmetric edge skip: it
                // returns false when apex+offset falls outside the scoring range, so
                // out-of-range offsets contribute nothing (no renormalization).
                if (!peakData.TryGetApexOffsetSpectrum(offset, out var s, out int globalIdx))
                    continue;
                sgXcorr += resolution.ScoreXcorr(preprocessedXcorr, globalIdx, s, candidate, scorer,
                    pool) * weight;
                sgCosine += ComputeCosineAtScan(candidate, s, config) * weight;
            }

            sweep.SgXcorr = sgXcorr;
            sweep.SgCosine = sgCosine;
            return sweep;
        }

        /// <summary>
        /// Mass-range-filtered, sqrt-intensity, L2-normalized cosine between the
        /// candidate fragments and one spectrum. This is the sg_weighted_cosine
        /// per-scan kernel -- a DIFFERENT function from SpectralScorer.LibCosine
        /// (used for the unrelated libCosine path); do not conflate them.
        /// Relocated verbatim from AbstractScoringTask.cs.
        ///
        /// TIE-BREAK: strict <c>diff &lt; bestDiff</c> keeps the first/closest peak
        /// scanning ascending m/z. bestIntensity seeds 0.0 so an in-range fragment
        /// with NO peak inside [lower, upper] still pushes Sqrt(relIntensity) to
        /// libPre and Sqrt(0)=0 to obsPre (asymmetric vector entry that drives the
        /// cosine down). Norm guard is the literal 1e-12 on the post-Sqrt norm.
        /// Sqrt is taken per term before the dot/norm accumulation, in insertion
        /// order of in-range fragments.
        /// </summary>
        private static double ComputeCosineAtScan(
            LibraryEntry candidate, Spectrum spectrum, OspreyConfig config)
        {
            if (candidate.Fragments == null || candidate.Fragments.Count == 0 ||
                spectrum.Mzs == null || spectrum.Mzs.Length == 0)
                return 0.0;

            double specMzMin = spectrum.Mzs[0];
            double specMzMax = spectrum.Mzs[spectrum.Mzs.Length - 1];

            var libPre = new List<double>();
            var obsPre = new List<double>();

            foreach (var frag in candidate.Fragments)
            {
                // Skip fragments outside the spectrum's mass range
                if (frag.Mz < specMzMin || frag.Mz > specMzMax)
                    continue;

                double tolDa = config.FragmentTolerance.ToleranceDa(frag.Mz);
                double lower = frag.Mz - tolDa;
                double upper = frag.Mz + tolDa;

                int lo = ScoringMath.BinarySearchLowerBound(spectrum.Mzs, lower);
                double bestIntensity = 0.0;
                double bestDiff = double.MaxValue;

                for (int k = lo; k < spectrum.Mzs.Length && spectrum.Mzs[k] <= upper; k++)
                {
                    double diff = Math.Abs(spectrum.Mzs[k] - frag.Mz);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestIntensity = spectrum.Intensities[k];
                    }
                }

                libPre.Add(Math.Sqrt(frag.RelativeIntensity));
                obsPre.Add(Math.Sqrt(bestIntensity));
            }

            if (libPre.Count == 0)
                return 0.0;

            // L2 normalize and dot product
            double libNorm = 0, obsNorm = 0, dot = 0;
            for (int i = 0; i < libPre.Count; i++)
            {
                libNorm += libPre[i] * libPre[i];
                obsNorm += obsPre[i] * obsPre[i];
                dot += libPre[i] * obsPre[i];
            }
            libNorm = Math.Sqrt(libNorm);
            obsNorm = Math.Sqrt(obsNorm);
            if (libNorm < 1e-12 || obsNorm < 1e-12)
                return 0.0;
            return dot / (libNorm * obsNorm);
        }
    }

    /// <summary>
    /// sg_weighted_xcorr (feature 17): Savitzky-Golay weighted sum of the
    /// apex+/-2 per-scan xcorr scores. Reads the shared
    /// <see cref="SgWeightedSweep"/> byproduct so it uses the exact same offset
    /// loop / boundary skip / index mapping as sg_weighted_cosine (feature 18) and
    /// the apex+/-2 spectra are scanned once. See <see cref="SgWeightedSweep"/> for
    /// the index trap, asymmetric boundary skip, and accumulation-order hazards.
    /// </summary>
    internal sealed class SgXcorrCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return "sg_weighted_xcorr"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return SgWeightedSweep.GetOrCompute(context, peakData).SgXcorr;
        }
    }

    /// <summary>
    /// sg_weighted_cosine (feature 18): Savitzky-Golay weighted sum of the
    /// apex+/-2 per-scan mass-range-filtered cosine scores
    /// (<see cref="SgWeightedSweep"/>'s ComputeCosineAtScan, NOT
    /// SpectralScorer.LibCosine). Reads the shared <see cref="SgWeightedSweep"/>
    /// byproduct. See <see cref="SgWeightedSweep"/> for the index trap, asymmetric
    /// boundary skip, accumulation-order, and tie-break hazards.
    /// </summary>
    internal sealed class SgCosineCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return "sg_weighted_cosine"; } }

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return SgWeightedSweep.GetOrCompute(context, peakData).SgCosine;
        }
    }
}
