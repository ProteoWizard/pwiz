using System;
using System.Collections.Generic;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Spectral similarity scorer implementing XCorr and LibCosine.
    /// Port of SpectralScorer from osprey-scoring/src/lib.rs.
    /// </summary>
    public class SpectralScorer
    {
        private const int XCORR_WINDOW_OFFSET = 75;
        private const double XCORR_SCALING = 0.005;

        private readonly BinConfig _binConfig;

        /// <summary>
        /// Create a new SpectralScorer with the specified BinConfig.
        /// </summary>
        public SpectralScorer(BinConfig binConfig)
        {
            _binConfig = binConfig;
        }

        /// <summary>
        /// Create a new SpectralScorer with default unit resolution binning.
        /// </summary>
        public SpectralScorer() : this(BinConfig.UnitResolution()) { }

        /// <summary>
        /// Compute XCorr between preprocessed observed spectrum bins and library fragment bins.
        /// Preprocessing: subtract local average (+/- 75 bins) from observed spectrum,
        /// then dot product with library spectrum bins.
        /// </summary>
        /// <param name="observedBins">Binned observed spectrum (length = BinConfig.NBins).</param>
        /// <param name="libraryBins">Binned library spectrum (length = BinConfig.NBins).</param>
        /// <returns>Scaled XCorr score.</returns>
        public double XCorr(double[] observedBins, double[] libraryBins)
        {
            int n = observedBins.Length;

            // Build prefix sum for O(n) sliding window
            double[] prefix = new double[n + 1];
            for (int i = 0; i < n; i++)
                prefix[i + 1] = prefix[i] + observedBins[i];

            double normFactor = 1.0 / (2 * XCORR_WINDOW_OFFSET);

            // Preprocess observed: subtract local average
            double[] preprocessed = new double[n];
            for (int i = 0; i < n; i++)
            {
                int left = Math.Max(0, i - XCORR_WINDOW_OFFSET);
                int right = Math.Min(n, i + XCORR_WINDOW_OFFSET + 1);
                double windowSum = prefix[right] - prefix[left];
                double sumExcludingCenter = windowSum - observedBins[i];
                preprocessed[i] = observedBins[i] - sumExcludingCenter * normFactor;
            }

            // Dot product of preprocessed observed with library spectrum
            double score = 0.0;
            for (int i = 0; i < n; i++)
                score += preprocessed[i] * libraryBins[i];

            return score * XCORR_SCALING;
        }

        /// <summary>
        /// Compute LibCosine score between an observed spectrum and a library entry.
        /// Uses sqrt intensity preprocessing and cosine similarity.
        /// Matches fragments within the specified tolerance.
        /// </summary>
        public double LibCosine(Spectrum observed, LibraryEntry library, FragmentToleranceConfig tolerance)
        {
            if (library.Fragments == null || library.Fragments.Count == 0 ||
                observed.Mzs == null || observed.Mzs.Length == 0)
                return 0.0;

            var libPreprocessed = new List<double>();
            var obsPreprocessed = new List<double>();

            foreach (var frag in library.Fragments)
            {
                // Find closest matching observed peak within tolerance
                double bestIntensity = 0.0;
                double bestDiff = double.MaxValue;

                for (int j = 0; j < observed.Mzs.Length; j++)
                {
                    if (tolerance.WithinTolerance(frag.Mz, observed.Mzs[j]))
                    {
                        double diff = Math.Abs(observed.Mzs[j] - frag.Mz);
                        if (diff < bestDiff)
                        {
                            bestDiff = diff;
                            bestIntensity = observed.Intensities[j];
                        }
                    }
                }

                // sqrt preprocessing
                libPreprocessed.Add(Math.Sqrt(frag.RelativeIntensity));
                obsPreprocessed.Add(Math.Sqrt(bestIntensity));
            }

            if (libPreprocessed.Count == 0)
                return 0.0;

            return CosineAngle(libPreprocessed, obsPreprocessed);
        }

        /// <summary>
        /// Check if a target m/z has a match within the sorted spectrum m/z array,
        /// using binary search and the specified tolerance.
        /// </summary>
        public static bool HasMatch(double targetMz, double[] spectrumMzs, FragmentToleranceConfig tolerance)
        {
            if (spectrumMzs == null || spectrumMzs.Length == 0)
                return false;

            double tolDa = tolerance.ToleranceDa(targetMz);
            double lower = targetMz - tolDa;
            double upper = targetMz + tolDa;

            // Binary search for first m/z >= lower
            int startIdx = BinarySearchLowerBound(spectrumMzs, lower);
            return startIdx < spectrumMzs.Length && spectrumMzs[startIdx] <= upper;
        }

        /// <summary>
        /// Get the bin configuration used by this scorer.
        /// </summary>
        public BinConfig BinConfig { get { return _binConfig; } }

        private static double CosineAngle(List<double> a, List<double> b)
        {
            if (a.Count != b.Count || a.Count == 0)
                return 0.0;

            double dot = 0.0;
            double normA = 0.0;
            double normB = 0.0;

            for (int i = 0; i < a.Count; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            double dNormA = Math.Sqrt(normA);
            double dNormB = Math.Sqrt(normB);

            if (dNormA < 1e-10 || dNormB < 1e-10)
                return 0.0;

            double cos = dot / (dNormA * dNormB);
            return Math.Max(0.0, Math.Min(1.0, cos));
        }

        private static int BinarySearchLowerBound(double[] sortedArray, double value)
        {
            int lo = 0;
            int hi = sortedArray.Length;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (sortedArray[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }
    }
}
