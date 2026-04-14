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
        /// Pre-preprocess all spectra in a window for XCorr. Returns null when
        /// dense pre-preprocessing is impractical (HRAM: 100K bins per spectrum).
        /// </summary>
        double[][] PreprocessWindowSpectra(IList<Spectrum> spectra, SpectralScorer scorer);

        /// <summary>
        /// Score XCorr for a library entry against one spectrum. Uses pre-preprocessed
        /// array when available, otherwise computes inline.
        /// </summary>
        double ScoreXcorr(double[][] preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer);
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

        public double[][] PreprocessWindowSpectra(IList<Spectrum> spectra, SpectralScorer scorer)
        {
            var pp = new double[spectra.Count][];
            for (int i = 0; i < spectra.Count; i++)
                pp[i] = scorer.PreprocessSpectrumForXcorr(spectra[i]);
            return pp;
        }

        public double ScoreXcorr(double[][] preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer)
        {
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

        public double[][] PreprocessWindowSpectra(IList<Spectrum> spectra, SpectralScorer scorer)
        {
            return null;
        }

        public double ScoreXcorr(double[][] preprocessed, int spectrumIndex,
            Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer)
        {
            return scorer.XcorrAtScan(spectrum, entry);
        }
    }
}
