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

            // Apply windowing normalization + sliding window in f64 (test path).
            double[] windowed = ApplyWindowingNormalizationD(observedBins);
            double[] preprocessed = ApplySlidingWindowD(windowed);

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
        /// Calibration + unit-res use this f64 path for bit-identical parity.
        /// The f32 variant <see cref="PreprocessSpectrumForXcorrFloat"/> is used
        /// only by the HRAM main-search per-window cache, where halving the
        /// 800 KB / 400 KB per spectrum matters for parallel-file memory.
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

            double[] windowed = ApplyWindowingNormalizationD(binned);
            return ApplySlidingWindowD(windowed);
        }

        /// <summary>
        /// Pool-aware preprocessing that writes the final sliding-window
        /// result into the caller-supplied <paramref name="output"/> buffer.
        /// All intermediate steps use the scratch's f64 buffers; only the
        /// final store narrows to f32. HRAM main search uses this variant
        /// so its per-window cache can be f32 (halves 800 KB -> 400 KB
        /// per spectrum) without affecting the f64 preprocessing precision
        /// that calibration and unit-resolution paths rely on.
        /// </summary>
        public void PreprocessSpectrumForXcorrInto(
            Spectrum spectrum, XcorrScratch scratch, float[] output)
        {
            if (scratch == null || output == null)
                throw new System.ArgumentNullException();
            int n = _binConfig.NBins;
            if (output.Length < n)
                throw new System.ArgumentException("output length < NBins");

            double[] binned = scratch.Binned;
            double[] windowed = scratch.Windowed;
            double[] prefix = scratch.Prefix;
            double[] preprocessed = scratch.Preprocessed;

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

            ApplyWindowingNormalizationD(binned, windowed);
            ApplySlidingWindowD(windowed, prefix, preprocessed);

            // Narrow the final preprocessed buffer to f32 only at the cache
            // store step. Matches Rust upstream maccoss/osprey which stores
            // Vec<f32> in the per-window preprocessed_xcorr cache.
            for (int i = 0; i < n; i++)
                output[i] = (float)preprocessed[i];
        }

        /// <summary>
        /// Compute XCorr score from a pre-preprocessed spectrum (f64) and a
        /// library entry. Used by calibration and unit-resolution main
        /// search where the cache is stored in full f64 precision.
        /// Allocating overload (visitedBins) for calibration; main-search
        /// callers should use the overload taking a reusable bool[].
        /// </summary>
        public double XcorrFromPreprocessed(double[] preprocessed, LibraryEntry entry)
        {
            if (preprocessed == null || entry == null ||
                entry.Fragments == null || entry.Fragments.Count == 0)
                return 0.0;

            var visitedBins = new bool[preprocessed.Length];
            return XcorrFromPreprocessed(preprocessed, entry, visitedBins);
        }

        /// <summary>
        /// Pool-aware overload: caller supplies a reusable <paramref name="visitedBins"/>
        /// array (from <see cref="WindowXcorrCache.VisitedBins"/>). The array
        /// is cleared on entry so callers don't need to zero it between uses.
        /// Eliminates the per-candidate 100 KB bool[NBins] LOH allocation
        /// that caused 152 GB virtual commit on Astral with 66M candidate
        /// invocations.
        /// </summary>
        public double XcorrFromPreprocessed(double[] preprocessed, LibraryEntry entry, bool[] visitedBins)
        {
            if (preprocessed == null || entry == null ||
                entry.Fragments == null || entry.Fragments.Count == 0)
                return 0.0;

            int n = preprocessed.Length;
            double xcorrRaw = 0.0;
            int nVisited = 0;
            for (int f = 0; f < entry.Fragments.Count; f++)
            {
                int bin = _binConfig.MzToBin(entry.Fragments[f].Mz);
                if (bin >= 0 && bin < n && !visitedBins[bin])
                {
                    visitedBins[bin] = true;
                    xcorrRaw += preprocessed[bin];
                    nVisited++;
                }
            }
            // Clear only the bins we touched (O(nVisited) vs O(NBins))
            if (nVisited > 0)
            {
                for (int f = 0; f < entry.Fragments.Count; f++)
                {
                    int bin = _binConfig.MzToBin(entry.Fragments[f].Mz);
                    if (bin >= 0 && bin < n)
                        visitedBins[bin] = false;
                }
            }
            return xcorrRaw * XCORR_SCALING;
        }

        /// <summary>
        /// Compute XCorr score from an f32-narrowed pre-preprocessed cache
        /// (HRAM main search only). Allocating overload (visitedBins) for
        /// fallback paths; main search uses the overload taking a reusable bool[].
        /// </summary>
        public double XcorrFromPreprocessed(float[] preprocessed, LibraryEntry entry)
        {
            if (preprocessed == null || entry == null ||
                entry.Fragments == null || entry.Fragments.Count == 0)
                return 0.0;

            var visitedBins = new bool[preprocessed.Length];
            return XcorrFromPreprocessed(preprocessed, entry, visitedBins);
        }

        /// <summary>Pool-aware f32 overload: see f64 variant for documentation.</summary>
        public double XcorrFromPreprocessed(float[] preprocessed, LibraryEntry entry, bool[] visitedBins)
        {
            if (preprocessed == null || entry == null ||
                entry.Fragments == null || entry.Fragments.Count == 0)
                return 0.0;

            int n = preprocessed.Length;
            double xcorrRaw = 0.0;
            int nVisited = 0;
            for (int f = 0; f < entry.Fragments.Count; f++)
            {
                int bin = _binConfig.MzToBin(entry.Fragments[f].Mz);
                if (bin >= 0 && bin < n && !visitedBins[bin])
                {
                    visitedBins[bin] = true;
                    xcorrRaw += preprocessed[bin];
                    nVisited++;
                }
            }
            if (nVisited > 0)
            {
                for (int f = 0; f < entry.Fragments.Count; f++)
                {
                    int bin = _binConfig.MzToBin(entry.Fragments[f].Mz);
                    if (bin >= 0 && bin < n)
                        visitedBins[bin] = false;
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

            // (1) Bin observed spectrum — ACCUMULATE sqrt intensities in each bin.
            // f64 throughout to preserve calibration parity; the HRAM main-
            // search path stores a narrowed f32 cache via
            // PreprocessSpectrumForXcorrInto, but this in-place scan path is
            // used by calibration fallback and unit-res scoring where the
            // full f64 precision is required. Scratch.Binned arrives zeroed
            // from Return().
            for (int i = 0; i < spectrum.Mzs.Length; i++)
            {
                int bin = _binConfig.MzToBin(spectrum.Mzs[i]);
                if (bin >= 0 && bin < n)
                    binned[bin] += Math.Sqrt(spectrum.Intensities[i]);
            }

            // (2) Windowing normalization (writes windowed in full).
            ApplyWindowingNormalizationD(binned, windowed);

            // (3) Sliding window subtraction (writes prefix + preprocessed in full).
            ApplySlidingWindowD(windowed, prefix, preprocessed);

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
        /// Apply Comet MakeCorrData windowing normalization (f64): split the
        /// spectrum into 10 equal-width m/z windows, normalize each window
        /// so the max in that window becomes 50.0, and zero out any value
        /// below 5% of the global max. Direct port of Rust
        /// apply_windowing_normalization. Used by calibration and unit-res
        /// main search; HRAM main search narrows to f32 only at final cache
        /// store.
        /// </summary>
        private static double[] ApplyWindowingNormalizationD(double[] spectrum)
        {
            double[] result = new double[spectrum.Length];
            ApplyWindowingNormalizationD(spectrum, result);
            return result;
        }

        private static void ApplyWindowingNormalizationD(double[] spectrum, double[] result)
        {
            int n = spectrum.Length;
            const int numWindows = 10;
            int windowSize = (n / numWindows) + 1;

            double globalMax = 0.0;
            for (int i = 0; i < n; i++)
                if (spectrum[i] > globalMax)
                    globalMax = spectrum[i];
            double threshold = globalMax * 0.05;

            Array.Clear(result, 0, n);

            for (int w = 0; w < numWindows; w++)
            {
                int start = w * windowSize;
                int end = Math.Min((w + 1) * windowSize, n);
                if (start >= end) break;

                double windowMax = 0.0;
                for (int i = start; i < end; i++)
                    if (spectrum[i] > windowMax)
                        windowMax = spectrum[i];

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
        /// Comet-style fast-XCorr sliding window subtraction with offset=75
        /// (f64). result[i] = spectrum[i] - (sum of window excluding center)
        /// / 150. O(n) via prefix sum.
        /// </summary>
        private static double[] ApplySlidingWindowD(double[] spectrum)
        {
            double[] prefix = new double[spectrum.Length + 1];
            double[] result = new double[spectrum.Length];
            ApplySlidingWindowD(spectrum, prefix, result);
            return result;
        }

        private static void ApplySlidingWindowD(double[] spectrum, double[] prefix, double[] result)
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
