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
        /// Compute XCorr between pre-binned observed spectrum bins and library bins.
        /// Applies the full Comet-style fast-XCorr preprocessing to the observed
        /// spectrum (windowing normalization + sliding window subtraction) then
        /// computes a dot product with the library bins. This entry point is used
        /// by unit tests; production callers should use <see cref="XcorrAtScan"/>
        /// which bins the observed spectrum internally and sums at fragment bin
        /// positions (the actual Comet scoring form).
        /// </summary>
        /// <param name="observedBins">Binned observed spectrum (length = BinConfig.NBins).</param>
        /// <param name="libraryBins">Binned library spectrum (length = BinConfig.NBins).</param>
        /// <returns>Scaled XCorr score.</returns>
        public double XCorr(double[] observedBins, double[] libraryBins)
        {
            int n = observedBins.Length;

            // Apply windowing normalization + sliding window in f64.
            double[] windowed = ApplyWindowingNormalization(observedBins);
            double[] preprocessed = ApplySlidingWindow(windowed);

            // Dot product of preprocessed observed with library bins.
            double score = 0.0;
            for (int i = 0; i < n; i++)
                score += preprocessed[i] * libraryBins[i];

            return score * XCORR_SCALING;
        }

        /// <summary>
        /// Preprocess a spectrum for XCorr: bin with sqrt intensities, apply
        /// windowing normalization, then sliding window subtraction. The result
        /// can be reused across all library entries scored against this spectrum.
        /// Matches Rust's <c>preprocess_spectrum_for_xcorr</c> in pipeline.rs.
        /// </summary>
        public double[] PreprocessSpectrumForXcorr(Spectrum spectrum)
        {
            if (_binConfig.NBins <= 0 || spectrum == null ||
                spectrum.Mzs == null || spectrum.Mzs.Length == 0)
                return null;

            int n = _binConfig.NBins;
            double[] binned = new double[n];
            for (int i = 0; i < spectrum.Mzs.Length; i++)
            {
                int bin = _binConfig.MzToBin(spectrum.Mzs[i]);
                if (bin >= 0 && bin < n)
                    binned[bin] += Math.Sqrt(spectrum.Intensities[i]);
            }

            double[] windowed = ApplyWindowingNormalization(binned);
            return ApplySlidingWindow(windowed);
        }

        /// <summary>
        /// Pool-aware preprocessing that writes the final sliding-window
        /// result into the caller-supplied <paramref name="output"/> buffer.
        /// Uses <paramref name="scratch"/> for the three intermediate passes
        /// (bin / windowing / prefix); the scratch's <c>Preprocessed</c>
        /// field is not used. <paramref name="scratch"/>.Binned is zeroed on
        /// entry here so callers can reuse one scratch across many spectra
        /// in a single window without bouncing it back to the pool.
        /// </summary>
        public void PreprocessSpectrumForXcorrInto(
            Spectrum spectrum, XcorrScratch scratch, double[] output)
        {
            if (scratch == null || output == null)
                throw new System.ArgumentNullException();
            int n = _binConfig.NBins;
            if (output.Length < n)
                throw new System.ArgumentException("output length < NBins");

            double[] binned = scratch.Binned;
            double[] windowed = scratch.Windowed;
            double[] prefix = scratch.Prefix;

            // Caller reuses scratch across spectra in the same window; zero
            // the accumulator before binning the next spectrum.
            Array.Clear(binned, 0, n);

            if (spectrum == null || spectrum.Mzs == null || spectrum.Mzs.Length == 0)
            {
                Array.Clear(output, 0, n);
                return;
            }

            for (int i = 0; i < spectrum.Mzs.Length; i++)
            {
                int bin = _binConfig.MzToBin(spectrum.Mzs[i]);
                if (bin >= 0 && bin < n)
                    binned[bin] += Math.Sqrt(spectrum.Intensities[i]);
            }

            ApplyWindowingNormalization(binned, windowed);
            ApplySlidingWindow(windowed, prefix, output);
        }

        /// <summary>
        /// Compute XCorr score from a pre-preprocessed spectrum and a library entry.
        /// The preprocessed array is from <see cref="PreprocessSpectrumForXcorr"/>.
        /// Only does O(n_fragments) bin lookups with dedup - no binning or windowing.
        /// </summary>
        public double XcorrFromPreprocessed(double[] preprocessed, LibraryEntry entry)
        {
            if (preprocessed == null || entry == null ||
                entry.Fragments == null || entry.Fragments.Count == 0)
                return 0.0;

            int n = preprocessed.Length;
            double xcorrRaw = 0.0;
            var visitedBins = new bool[n];
            for (int f = 0; f < entry.Fragments.Count; f++)
            {
                int bin = _binConfig.MzToBin(entry.Fragments[f].Mz);
                if (bin >= 0 && bin < n && !visitedBins[bin])
                {
                    visitedBins[bin] = true;
                    xcorrRaw += preprocessed[bin];
                }
            }
            return xcorrRaw * XCORR_SCALING;
        }

        /// <summary>
        /// Compute XCorr at a single spectrum against a library entry. Direct port
        /// of Rust's <c>SpectralScorer::xcorr_at_scan</c> / <c>xcorr()</c> in
        /// osprey-scoring/src/lib.rs. Performs:
        /// (1) bin observed spectrum with sum-accumulated sqrt intensities;
        /// (2) windowing normalization (Comet MakeCorrData: 10 windows, normalize
        /// to 50.0, drop values below 5% of global max);
        /// (3) sliding window subtraction with offset=75;
        /// (4) sum preprocessed values at library fragment bin positions;
        /// (5) scale by 0.005.
        /// This is the authoritative Comet fast-XCorr form used by Rust Osprey.
        /// </summary>
        public double XcorrAtScan(Spectrum spectrum, LibraryEntry entry)
        {
            // Allocating overload retained for calibration, tests, and
            // diagnostics. Main-search hot path uses the scratch overload
            // below to avoid per-call LOH allocation at HRAM NBins.
            if (_binConfig.NBins <= 0) return 0.0;
            if (entry == null || entry.Fragments == null || entry.Fragments.Count == 0)
                return 0.0;
            if (spectrum == null || spectrum.Mzs == null || spectrum.Mzs.Length == 0)
                return 0.0;

            var scratch = new XcorrScratch(_binConfig.NBins);
            return XcorrAtScan(spectrum, entry, scratch);
        }

        /// <summary>
        /// Pool-aware XCorr at a single spectrum. Writes into the passed
        /// scratch buffers instead of allocating new ones. The caller is
        /// responsible for getting <paramref name="scratch"/> from an
        /// <see cref="XcorrScratchPool"/> and returning it afterwards;
        /// Return() re-zeros the fields that accumulate by +=.
        /// </summary>
        public double XcorrAtScan(Spectrum spectrum, LibraryEntry entry, XcorrScratch scratch)
        {
            if (_binConfig.NBins <= 0) return 0.0;
            if (entry == null || entry.Fragments == null || entry.Fragments.Count == 0)
                return 0.0;
            if (spectrum == null || spectrum.Mzs == null || spectrum.Mzs.Length == 0)
                return 0.0;

            int n = _binConfig.NBins;
            double[] binned = scratch.Binned;
            double[] windowed = scratch.Windowed;
            double[] prefix = scratch.Prefix;
            double[] preprocessed = scratch.Preprocessed;
            bool[] visitedBins = scratch.VisitedBins;

            // (1) Bin observed spectrum — ACCUMULATE sqrt intensities in each bin
            // (matches Rust obs_binned[bin] += (intensity as f64).sqrt()).
            // Scratch.Binned arrives zeroed from Return().
            for (int i = 0; i < spectrum.Mzs.Length; i++)
            {
                int bin = _binConfig.MzToBin(spectrum.Mzs[i]);
                if (bin >= 0 && bin < n)
                    binned[bin] += Math.Sqrt(spectrum.Intensities[i]);
            }

            // (2) Windowing normalization (writes windowed in full).
            ApplyWindowingNormalization(binned, windowed);

            // (3) Sliding window subtraction (writes prefix + preprocessed in full).
            ApplySlidingWindow(windowed, prefix, preprocessed);

            // (4) Sum preprocessed values at library fragment bin positions.
            // Scratch.VisitedBins arrives zeroed from Return().
            double xcorrRaw = 0.0;
            for (int f = 0; f < entry.Fragments.Count; f++)
            {
                int bin = _binConfig.MzToBin(entry.Fragments[f].Mz);
                if (bin >= 0 && bin < n && !visitedBins[bin])
                {
                    visitedBins[bin] = true;
                    xcorrRaw += preprocessed[bin];
                }
            }

            // Dirty state: `binned` is accumulated and `visitedBins` is set
            // at fragment positions. Both are cleared when the scratch is
            // returned to the pool, so the caller must Return() before
            // Rent()-ing again on the same logical task. `windowed`,
            // `prefix`, `preprocessed` are fully overwritten by the next
            // call and do not need zeroing.

            // XCorr diagnostic for bisection
            string diagXcorrScan = System.Environment.GetEnvironmentVariable("OSPREY_DIAG_XCORR_SCAN");
            if (!string.IsNullOrEmpty(diagXcorrScan) &&
                spectrum.ScanNumber.ToString() == diagXcorrScan)
            {
                using (var dw = new System.IO.StreamWriter("cs_xcorr_diag.txt", true))
                {
                    dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "# XCORR DIAG scan={0} entry={1}", spectrum.ScanNumber, entry.ModifiedSequence));
                    dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "# nbins={0} xcorr_raw={1:G17} xcorr_scaled={2:G17}", n, xcorrRaw, xcorrRaw * XCORR_SCALING));
                    double bsum = 0; int bnz = 0;
                    for (int di = 0; di < n; di++) { bsum += binned[di]; if (binned[di] > 0) bnz++; }
                    dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "# binned_sum={0:G17} nonzero={1}", bsum, bnz));
                    double wsum = 0;
                    for (int di = 0; di < n; di++) wsum += windowed[di];
                    dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "# windowed_sum={0:G17}", wsum));
                    double psum = 0;
                    for (int di = 0; di < n; di++) psum += preprocessed[di];
                    dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "# preprocessed_sum={0:G17}", psum));
                    // Dump first 20 nonzero preprocessed bins
                    int dumped = 0;
                    for (int di = 0; di < n && dumped < 20; di++)
                        if (preprocessed[di] != 0.0)
                        {
                            dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                "pre\t{0}\t{1:G17}", di, preprocessed[di]));
                            dumped++;
                        }
                    // Fragment bin lookups
                    dw.WriteLine("# fragment_bins (with dedup)");
                    var visited2 = new bool[n];
                    for (int f = 0; f < entry.Fragments.Count; f++)
                    {
                        int fb = _binConfig.MzToBin(entry.Fragments[f].Mz);
                        bool dup = (fb >= 0 && fb < n) ? visited2[fb] : false;
                        if (fb >= 0 && fb < n) visited2[fb] = true;
                        dw.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "frag\t{0}\tmz={1:G17}\tbin={2}\tval={3}\tdup={4}",
                            f, entry.Fragments[f].Mz, fb,
                            (fb >= 0 && fb < n) ? preprocessed[fb].ToString("G17") : "OOB",
                            dup));
                    }
                }
            }

            // (5) Scale.
            return xcorrRaw * XCORR_SCALING;
        }

        /// <summary>
        /// Apply Comet MakeCorrData windowing normalization: split the spectrum
        /// into 10 equal-width m/z windows, normalize each window so the max
        /// in that window becomes 50.0, and zero out any value below 5% of the
        /// global max. Direct port of Rust apply_windowing_normalization.
        /// </summary>
        private static double[] ApplyWindowingNormalization(double[] spectrum)
        {
            double[] result = new double[spectrum.Length];
            ApplyWindowingNormalization(spectrum, result);
            return result;
        }

        /// <summary>
        /// In-place variant used by the pool-aware XcorrAtScan. Writes into
        /// the caller-supplied <paramref name="result"/> buffer which must
        /// already be the right length. Fully overwrites result (zeros
        /// windows that don't meet the threshold), so no pre-zeroing needed.
        /// </summary>
        private static void ApplyWindowingNormalization(double[] spectrum, double[] result)
        {
            int n = spectrum.Length;
            const int numWindows = 10;
            int windowSize = (n / numWindows) + 1;

            // Global max → threshold
            double globalMax = 0.0;
            for (int i = 0; i < n; i++)
                if (spectrum[i] > globalMax)
                    globalMax = spectrum[i];
            double threshold = globalMax * 0.05;

            // Fully overwrite: default to 0, then fill windows that qualify.
            // Zero any cells left over from a previous scratch invocation.
            Array.Clear(result, 0, n);

            for (int w = 0; w < numWindows; w++)
            {
                int start = w * windowSize;
                int end = Math.Min((w + 1) * windowSize, n);
                if (start >= end) break;

                // Find max in this window
                double windowMax = 0.0;
                for (int i = start; i < end; i++)
                    if (spectrum[i] > windowMax)
                        windowMax = spectrum[i];

                // Normalize to 50.0 (skipping values below the global threshold).
                if (windowMax > 0.0)
                {
                    double normFactor = 50.0 / windowMax;
                    for (int i = start; i < end; i++)
                    {
                        if (spectrum[i] > threshold)
                            result[i] = spectrum[i] * normFactor;
                    }
                }
            }
        }

        /// <summary>
        /// Comet-style fast-XCorr sliding window subtraction with offset=75.
        /// result[i] = spectrum[i] - (sum of window excluding center) / 150.
        /// O(n) via prefix sum. Direct port of Rust apply_sliding_window.
        /// </summary>
        private static double[] ApplySlidingWindow(double[] spectrum)
        {
            double[] prefix = new double[spectrum.Length + 1];
            double[] result = new double[spectrum.Length];
            ApplySlidingWindow(spectrum, prefix, result);
            return result;
        }

        /// <summary>
        /// In-place variant. Both <paramref name="prefix"/> (length n+1) and
        /// <paramref name="result"/> (length n) are fully overwritten, so
        /// neither needs pre-zeroing.
        /// </summary>
        private static void ApplySlidingWindow(double[] spectrum, double[] prefix, double[] result)
        {
            int n = spectrum.Length;
            const int offset = XCORR_WINDOW_OFFSET;
            double normFactor = 1.0 / (2 * offset);

            prefix[0] = 0.0;
            for (int i = 0; i < n; i++)
                prefix[i + 1] = prefix[i] + spectrum[i];

            for (int i = 0; i < n; i++)
            {
                int left = Math.Max(0, i - offset);
                int right = Math.Min(n, i + offset + 1);
                double windowSum = prefix[right] - prefix[left];
                double sumExcludingCenter = windowSum - spectrum[i];
                result[i] = spectrum[i] - sumExcludingCenter * normFactor;
            }
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
