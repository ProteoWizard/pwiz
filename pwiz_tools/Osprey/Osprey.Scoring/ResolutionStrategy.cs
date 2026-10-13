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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Opaque handle to a per-window pre-preprocessed XCorr cache.
    /// Unit-res stores f64 dense (calibration-equivalent precision, NBins ~2K,
    /// ~16 KB per array). HRAM stores the sparse form
    /// (<see cref="SparseXcorrSpectrum"/>): the dense f32 cache it replaced cost
    /// 391 KB per spectrum on the LOH, which reached tens of GB across the
    /// concurrent windows of an Astral run (issue #4398). The strategy owns the
    /// type; callers pass the handle back to
    /// <see cref="IResolutionStrategy.ScoreXcorr"/> and
    /// <see cref="IResolutionStrategy.ReleaseWindowCache"/>.
    ///
    /// <para>The HRAM form fills on demand: a spectrum is preprocessed the first time xcorr asks
    /// for it and kept for the rest of the window. Xcorr reads only each scored candidate's apex
    /// and the four scans around it, so a window with few candidates needs few of its spectra -
    /// measured on 3 Astral files, Stage 6's gap-fill passes used 2.5-5% of them and its re-score
    /// 64%, first-pass scoring 91%. A cache serves one window on one thread (candidates are
    /// scored in sequence), so filling it needs no lock.</para>
    /// </summary>
    public sealed class WindowXcorrCache
    {
        internal readonly double[][] Doubles;
        internal readonly SparseXcorrSpectrum[] Sparse;
        internal readonly bool[] VisitedBins;
        // On-demand HRAM fill: the window's spectra, the scorer that preprocesses them and the
        // scratch rented for the window's lifetime (returned by ReleaseWindowCache). The scratch
        // is for filling only - each fill clears the buffers it reads, but leaves them dirty, so
        // it must never be handed to XcorrAtScan, which expects the pool's zeroed buffers.
        internal readonly IList<Spectrum> Spectra;
        internal readonly SpectralScorer Scorer;
        internal XcorrScratch Scratch;

        internal WindowXcorrCache(double[][] dd, int nBins)
        {
            Doubles = dd;
            VisitedBins = new bool[nBins];
        }

        internal WindowXcorrCache(IList<Spectrum> spectra, SpectralScorer scorer, XcorrScratch scratch, int nBins)
        {
            Sparse = new SparseXcorrSpectrum[spectra.Count];
            Spectra = spectra;
            Scorer = scorer;
            Scratch = scratch;
            VisitedBins = new bool[nBins];
        }

        public int Count { get { return Doubles != null ? Doubles.Length : Sparse.Length; } }
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
        /// Create a window's XCorr cache. Returns a strategy-typed cache handle. Unit
        /// resolution preprocesses every spectrum here; HRAM preprocesses each one the first
        /// time <see cref="ScoreXcorr"/> asks for it, so the cache keeps
        /// <paramref name="spectra"/> and reads it until release: the list and its spectra
        /// must not change, and the cache must be used from one thread at a time. Caller
        /// releases via <see cref="ReleaseWindowCache"/> at end of window.
        /// </summary>
        WindowXcorrCache PreprocessWindowSpectra(IList<Spectrum> spectra,
            SpectralScorer scorer, XcorrScratchPool scratchPool);

        /// <summary>Release rented buffers from a cache produced by
        /// <see cref="PreprocessWindowSpectra"/>. Pass the same cache back. Rows already filled
        /// are still served afterwards; no new rows are filled.</summary>
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
            // Pure-f32 preprocess to match Rust upstream maccoss/osprey.
            // Values are widened to double[] losslessly for the downstream
            // XcorrFromPreprocessed(double[], ...) consumer.
            int n = scorer.BinConfig.NBins;
            var pp = new double[spectra.Count][];
            for (int i = 0; i < spectra.Count; i++)
            {
                float[] f32pp = scorer.PreprocessSpectrumForXcorrF32(spectra[i]);
                var widened = new double[n];
                for (int k = 0; k < n; k++)
                    widened[k] = f32pp[k];
                pp[i] = widened;
            }
            return new WindowXcorrCache(pp, n);
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
    /// HRAM resolution: dense bin arrays are large (NBins ~100K). Brings the Rust
    /// HRAM fast path (pipeline.rs:5954 preprocessed_xcorr per window), but caches
    /// each spectrum in the sparse form rather than as a dense f32[NBins]. The dense
    /// cache cost 391 KB per spectrum on the LOH -- ~2,000 spectra per window times
    /// NThreads concurrent windows put scoring at ~18-37 GB (issue #4398). The sparse
    /// form keeps only the ~1-3K nonzero windowed bins (~20 B each) and recovers each
    /// probed bin's post-subtraction value on demand, bit-identically.
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

            // Nothing is preprocessed here: ScoreXcorr fills each spectrum the first time a
            // candidate asks for it (see WindowXcorrCache).
            return new WindowXcorrCache(spectra, scorer, scratchPool.Rent(), scorer.BinConfig.NBins);
        }

        public void ReleaseWindowCache(WindowXcorrCache cache, XcorrScratchPool scratchPool)
        {
            // The sparse spectra are dropped with the window; the scratch goes back to the pool.
            if (cache != null && cache.Scratch != null && scratchPool != null)
            {
                scratchPool.Return(cache.Scratch);
                cache.Scratch = null;
            }
        }

        public double ScoreXcorr(WindowXcorrCache preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer,
            XcorrScratchPool scratchPool)
        {
            if (preprocessed != null && preprocessed.Sparse != null &&
                spectrumIndex >= 0 && spectrumIndex < preprocessed.Sparse.Length)
            {
                // A filled row is served whether or not the window still holds its scratch, so a
                // released cache returns the same (f32-narrowed) values rather than the live f64
                // path's. Only filling needs the scratch. The window's own spectrum and scorer, so
                // a spectrum preprocessed on demand is the one an up-front loop would have produced.
                var sparse = preprocessed.Sparse[spectrumIndex];
                if (sparse == null && preprocessed.Scratch != null)
                {
                    sparse = preprocessed.Sparse[spectrumIndex] = preprocessed.Scorer.PreprocessSpectrumForXcorrSparse(
                        preprocessed.Spectra[spectrumIndex], preprocessed.Scratch);
                }
                if (sparse != null)
                    return scorer.XcorrFromSparse(sparse, entry, preprocessed.VisitedBins);
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
