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

namespace pwiz.Osprey.Centroid
{
    /// <summary>How each region's lasso is solved.</summary>
    public enum CentrixSolver
    {
        /// <summary>Centrix's coordinate descent, stopped at a tolerance or an iteration cap: Rust Centrix's centroids.</summary>
        coordinate_descent,
        /// <summary>
        /// The exact optimum of the same problem, by an active-set (Lawson-Hanson) solve of its few nonzero coefficients:
        /// the same centroids where coordinate descent converges, the converged ones where it stops at its cap.
        /// </summary>
        active_set,
    }

    /// <summary>
    /// Settings of <see cref="CentrixCentroider"/>. The defaults are those of Rust Centrix (maccoss/centrix), so the
    /// same peak sigma gives the same centroids.
    /// </summary>
    public sealed class CentrixParams
    {
        /// <summary>
        /// The sigma of a profile peak, in Th. Centrix calibrates it from the filter string or a wavelet scan of the
        /// first spectra; neither is ported, since both mis-read the Stellar (the scan rate letter gives 0.849 and the
        /// wavelet 0.61, where the peaks measure 0.327), so the caller gives it.
        /// </summary>
        public double SigmaTh { get; set; } = 0.33;

        /// <summary>
        /// The profile's point spacing, in Th, which is also the spacing of the peak basis. Zero: the median gap of
        /// each spectrum, as Centrix takes it from the first spectrum of each MS level.
        /// </summary>
        public double GridSpacingTh { get; set; }

        /// <summary>The m/z of basis position 0, so the basis is at GridOffsetTh + k * spacing. Centrix uses 0.</summary>
        public double GridOffsetTh { get; set; }

        /// <summary>The lasso penalty, as this many noise sigmas.</summary>
        public double LambdaFactor { get; set; } = 3;

        /// <summary>The lasso solver; coordinate descent, Centrix's own, by default.</summary>
        public CentrixSolver Solver { get; set; } = CentrixSolver.coordinate_descent;

        /// <summary>Coordinate descent sweeps before a solve stops unconverged.</summary>
        public int MaxIterations { get; set; } = 2000;

        /// <summary>A solve has converged when no coefficient moved by this much in a sweep.</summary>
        public double Tolerance { get; set; } = 1e-6;

        /// <summary>A region is solved again with the refined noise when its penalty changes by more than this fraction.</summary>
        public double LambdaChangeThreshold { get; set; } = 0.2;

        /// <summary>Signal regions are the points above the baseline by this many noise sigmas.</summary>
        public double SignalThresholdSigma { get; set; } = 3;

        /// <summary>Runs of points above the threshold separated by no more than this many points form one region.</summary>
        public int MergeGapPoints { get; set; } = 2;

        /// <summary>Points added to each side of a region.</summary>
        public int ExtensionPoints { get; set; } = 3;

        /// <summary>Regions narrower than this many points, extension included, are dropped.</summary>
        public int MinRegionWidth { get; set; } = 3;

        /// <summary>The width, in Th, of the windows the noise is refined in.</summary>
        public double NoiseWindowTh { get; set; } = 20;

        /// <summary>The step, in Th, between the noise windows.</summary>
        public double NoiseStepTh { get; set; } = 5;

        /// <summary>Centroids closer than this, in Th, merge into one. Zero: the peak sigma.</summary>
        public double MinCentroidSeparationTh { get; set; }

        public CentrixParams Copy()
        {
            return (CentrixParams)MemberwiseClone();
        }
    }
}
