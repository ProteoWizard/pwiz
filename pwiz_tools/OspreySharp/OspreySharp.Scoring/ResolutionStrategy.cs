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

using System.Collections.Generic;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Opaque handle to a per-window pre-preprocessed XCorr cache.
    /// Unit-res stores f64 (calibration-equivalent precision, small arrays);
    /// HRAM stores f32 (halves the ~800 KB per-spectrum cost, within the
    /// precision Rust upstream uses). The strategy owns the type; callers
    /// pass the handle back to <see cref="IResolutionStrategy.ScoreXcorr"/>
    /// and <see cref="IResolutionStrategy.ReleaseWindowCache"/>.
    /// </summary>
    public sealed class WindowXcorrCache
    {
        internal readonly double[][] Doubles;
        internal readonly float[][] Floats;
        internal readonly bool[] VisitedBins;

        internal WindowXcorrCache(double[][] dd, int nBins)
        {
            Doubles = dd;
            VisitedBins = new bool[nBins];
        }

        internal WindowXcorrCache(float[][] ff, int nBins)
        {
            Floats = ff;
            VisitedBins = new bool[nBins];
        }

        public int Count { get { return Doubles != null ? Doubles.Length : Floats.Length; } }
    }

    /// <summary>
    /// Encapsulates all resolution-dependent behavior so pipeline code never
    /// checks ResolutionMode directly. Created once at pipeline start from
    /// <see cref="ResolutionStrategy.Create"/>.
    /// </summary>
    public interface IResolutionStrategy
    {
        /// <summary>Whether MS1 features (precursor coelution, isotope cosine) should be computed.</summary>
        bool HasMs1Features { get; }

        /// <summary>Create a SpectralScorer with the appropriate BinConfig.</summary>
        SpectralScorer CreateScorer();

        /// <summary>
        /// Pre-preprocess all spectra in a window for XCorr. Returns a
        /// strategy-typed cache handle. Caller releases via
        /// <see cref="ReleaseWindowCache"/> at end of window.
        /// </summary>
        WindowXcorrCache PreprocessWindowSpectra(IList<Spectrum> spectra,
            SpectralScorer scorer, XcorrScratchPool scratchPool);

        /// <summary>Release rented buffers from a cache produced by
        /// <see cref="PreprocessWindowSpectra"/>. Pass the same cache back.</summary>
        void ReleaseWindowCache(WindowXcorrCache cache, XcorrScratchPool scratchPool);

        /// <summary>Pool-aware scoring for a library entry at one spectrum.</summary>
        double ScoreXcorr(WindowXcorrCache preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer,
            XcorrScratchPool scratchPool);
    }

    /// <summary>
    /// Factory for resolution strategies.
    /// </summary>
    public static class ResolutionStrategy
    {
        public static IResolutionStrategy Create(ResolutionMode mode)
        {
            if (mode == ResolutionMode.HRAM)
                return new HramStrategy();
            return new UnitStrategy();
        }
    }

    /// <summary>
    /// Unit resolution: small dense bin arrays (NBins ~2K). f64 throughout
    /// keeps bit-identical parity with calibration; memory impact is
    /// negligible (~16 KB per cache array).
    /// </summary>
    internal sealed class UnitStrategy : IResolutionStrategy
    {
        public bool HasMs1Features { get { return false; } }

        public SpectralScorer CreateScorer()
        {
            return new SpectralScorer(BinConfig.UnitResolution());
        }

        public WindowXcorrCache PreprocessWindowSpectra(IList<Spectrum> spectra,
            SpectralScorer scorer, XcorrScratchPool scratchPool)
        {
            var pp = new double[spectra.Count][];
            for (int i = 0; i < spectra.Count; i++)
                pp[i] = scorer.PreprocessSpectrumForXcorr(spectra[i]);
            return new WindowXcorrCache(pp, scorer.BinConfig.NBins);
        }

        public void ReleaseWindowCache(WindowXcorrCache cache, XcorrScratchPool scratchPool)
        {
            // Unit-res arrays are small and short-lived; simply drop.
        }

        public double ScoreXcorr(WindowXcorrCache preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer,
            XcorrScratchPool scratchPool)
        {
            return scorer.XcorrFromPreprocessed(
                preprocessed.Doubles[spectrumIndex], entry, preprocessed.VisitedBins);
        }
    }

    /// <summary>
    /// HRAM resolution: dense bin arrays are large (NBins ~100K). Uses the
    /// pool and an f32 cache to bring the Rust HRAM fast path
    /// (pipeline.rs:5954 preprocessed_xcorr per window) while halving the
    /// per-spectrum cost vs f64. Computation is still f64 internally; only
    /// the final store narrows to f32.
    /// </summary>
    internal sealed class HramStrategy : IResolutionStrategy
    {
        public bool HasMs1Features { get { return true; } }

        public SpectralScorer CreateScorer()
        {
            return new SpectralScorer(BinConfig.HRAM());
        }

        public WindowXcorrCache PreprocessWindowSpectra(IList<Spectrum> spectra,
            SpectralScorer scorer, XcorrScratchPool scratchPool)
        {
            if (scratchPool == null)
                return null;

            var pp = new float[spectra.Count][];
            var scratch = scratchPool.Rent();
            try
            {
                for (int i = 0; i < spectra.Count; i++)
                {
                    pp[i] = scratchPool.RentBins();
                    scorer.PreprocessSpectrumForXcorrInto(spectra[i], scratch, pp[i]);
                }
            }
            finally { scratchPool.Return(scratch); }
            return new WindowXcorrCache(pp, scorer.BinConfig.NBins);
        }

        public void ReleaseWindowCache(WindowXcorrCache cache, XcorrScratchPool scratchPool)
        {
            if (cache == null || scratchPool == null)
                return;
            scratchPool.ReturnBinsArray(cache.Floats);
        }

        public double ScoreXcorr(WindowXcorrCache preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer,
            XcorrScratchPool scratchPool)
        {
            if (preprocessed != null && preprocessed.Floats != null &&
                spectrumIndex >= 0 && spectrumIndex < preprocessed.Floats.Length &&
                preprocessed.Floats[spectrumIndex] != null)
            {
                return scorer.XcorrFromPreprocessed(
                    preprocessed.Floats[spectrumIndex], entry, preprocessed.VisitedBins);
            }

            if (scratchPool == null)
                return scorer.XcorrAtScan(spectrum, entry);

            var scratch = scratchPool.Rent();
            try
            {
                return scorer.XcorrAtScan(spectrum, entry, scratch);
            }
            finally
            {
                scratchPool.Return(scratch);
            }
        }
    }
}
