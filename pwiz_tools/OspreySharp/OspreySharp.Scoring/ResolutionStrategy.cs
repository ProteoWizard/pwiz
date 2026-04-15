using System.Collections.Generic;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Encapsulates all resolution-dependent behavior so pipeline code never
    /// checks ResolutionMode directly. Created once at pipeline start from
    /// <see cref="Create"/>.
    /// </summary>
    public interface IResolutionStrategy
    {
        /// <summary>Whether MS1 features (precursor coelution, isotope cosine) should be computed.</summary>
        bool HasMs1Features { get; }

        /// <summary>Create a SpectralScorer with the appropriate BinConfig.</summary>
        SpectralScorer CreateScorer();

        /// <summary>
        /// Pre-preprocess all spectra in a window for XCorr. The pool-aware
        /// overload rents a <c>double[NBins]</c> per spectrum from the
        /// supplied pool; the returned <c>double[][]</c> must be handed
        /// back via <see cref="XcorrScratchPool.ReturnBinsArray"/> when
        /// the window is done scoring. Returns null when pre-preprocessing
        /// is not supported (e.g. no pool available on HRAM before the
        /// pool fix).
        /// </summary>
        double[][] PreprocessWindowSpectra(IList<Spectrum> spectra,
            SpectralScorer scorer, XcorrScratchPool scratchPool);

        /// <summary>
        /// Score XCorr for a library entry against one spectrum. Uses pre-preprocessed
        /// array when available, otherwise computes inline.
        /// </summary>
        double ScoreXcorr(double[][] preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer);

        /// <summary>
        /// Pool-aware variant of <see cref="ScoreXcorr"/> for hot-path callers
        /// (main-search apex + SG-weighted neighbours). When
        /// <paramref name="preprocessed"/> is non-null, routes to the
        /// O(n_fragments) <c>XcorrFromPreprocessed</c> fast path. Otherwise
        /// HRAM uses the pool to avoid per-call 100K-bin LOH allocation.
        /// </summary>
        double ScoreXcorr(double[][] preprocessed, int spectrumIndex,
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
    /// Unit resolution: small dense bin arrays (NBins ~2K), pre-preprocess
    /// all spectra for O(n_frags) scoring. No MS1 features.
    /// </summary>
    internal sealed class UnitStrategy : IResolutionStrategy
    {
        public bool HasMs1Features { get { return false; } }

        public SpectralScorer CreateScorer()
        {
            return new SpectralScorer(BinConfig.UnitResolution());
        }

        public double[][] PreprocessWindowSpectra(IList<Spectrum> spectra,
            SpectralScorer scorer, XcorrScratchPool scratchPool)
        {
            var pp = new double[spectra.Count][];
            if (scratchPool != null)
            {
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
                return pp;
            }

            for (int i = 0; i < spectra.Count; i++)
                pp[i] = scorer.PreprocessSpectrumForXcorr(spectra[i]);
            return pp;
        }

        public double ScoreXcorr(double[][] preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer)
        {
            return scorer.XcorrFromPreprocessed(preprocessed[spectrumIndex], entry);
        }

        public double ScoreXcorr(double[][] preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer,
            XcorrScratchPool scratchPool)
        {
            // Unit resolution already uses pre-preprocessed arrays — no
            // scratch needed. Forward to the allocating overload.
            return scorer.XcorrFromPreprocessed(preprocessed[spectrumIndex], entry);
        }
    }

    /// <summary>
    /// HRAM resolution: dense bin arrays are too large (NBins ~100K, ~800KB
    /// each). Skips pre-preprocessing; computes XCorr inline per scan.
    /// Computes MS1 features (precursor coelution, isotope cosine).
    /// </summary>
    internal sealed class HramStrategy : IResolutionStrategy
    {
        public bool HasMs1Features { get { return true; } }

        public SpectralScorer CreateScorer()
        {
            return new SpectralScorer(BinConfig.HRAM());
        }

        public double[][] PreprocessWindowSpectra(IList<Spectrum> spectra,
            SpectralScorer scorer, XcorrScratchPool scratchPool)
        {
            // Pre-preprocessing matches the Rust HRAM fast path
            // (pipeline.rs:5954: preprocessed_xcorr per window). The pool
            // makes it affordable by holding the 100K-bin arrays in gen-2
            // across the whole run instead of allocating per window. Fall
            // back to null when no pool is provided (e.g. during
            // calibration paths that never score enough candidates to
            // justify the preprocessing cost).
            if (scratchPool == null)
                return null;

            var pp = new double[spectra.Count][];
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
            return pp;
        }

        public double ScoreXcorr(double[][] preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer)
        {
            return scorer.XcorrAtScan(spectrum, entry);
        }

        public double ScoreXcorr(double[][] preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer,
            XcorrScratchPool scratchPool)
        {
            // Fast path: per-window pre-preprocessed array is available,
            // reduce to an O(n_fragments) bin lookup with dedup. Matches
            // Rust's main-search HRAM path (pipeline.rs:5273 uses
            // xcorr_from_preprocessed against the per-window cache).
            if (preprocessed != null && spectrumIndex >= 0 &&
                spectrumIndex < preprocessed.Length &&
                preprocessed[spectrumIndex] != null)
            {
                return scorer.XcorrFromPreprocessed(preprocessed[spectrumIndex], entry);
            }

            // Fallback: inline preprocess + score via pool. Retained so the
            // strategy stays robust when PreprocessWindowSpectra is skipped
            // (e.g. no pool, or caller decides to stream).
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
