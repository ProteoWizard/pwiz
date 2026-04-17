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
    /// Result of fragment matching for a single library-spectrum pair.
    /// </summary>
    public class FragmentMatchResult
    {
        public double[] MatchedExpIntensities { get; set; }
        public double[] MatchedLibIntensities { get; set; }
        public double[] MatchedLibMzs { get; set; }
        public double[] MassErrors { get; set; }
        public int NMatched { get; set; }
    }

    /// <summary>
    /// Preprocessed library ready for batch XCorr scoring.
    /// Stores binned, sqrt-preprocessed, L2-normalized vectors.
    /// </summary>
    public class PreprocessedLibrary
    {
        /// <summary>Matrix of preprocessed library vectors (n_entries x n_bins), row-major.</summary>
        public float[,] Matrix { get; private set; }
        /// <summary>Library entry IDs corresponding to each row.</summary>
        public uint[] EntryIds { get; private set; }
        /// <summary>Mapping from entry ID to matrix row index.</summary>
        public Dictionary<uint, int> IdToRow { get; private set; }
        /// <summary>Number of bins used.</summary>
        public int NumBins { get; private set; }

        private PreprocessedLibrary() { }

        /// <summary>
        /// Preprocess library entries into binned, sqrt-preprocessed, L2-normalized vectors.
        /// </summary>
        public static PreprocessedLibrary FromEntries(
            LibraryEntry[] entries, int numBins, double minMz, double binWidth)
        {
            var validEntries = new List<int>();
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Fragments != null && entries[i].Fragments.Count > 0)
                    validEntries.Add(i);
            }

            int nValid = validEntries.Count;
            var matrix = new float[nValid, numBins];
            var entryIds = new uint[nValid];
            var idToRow = new Dictionary<uint, int>(nValid);

            for (int row = 0; row < nValid; row++)
            {
                var entry = entries[validEntries[row]];
                entryIds[row] = entry.Id;
                idToRow[entry.Id] = row;

                PreprocessSingleEntry(entry, numBins, minMz, binWidth, matrix, row);
            }

            return new PreprocessedLibrary
            {
                Matrix = matrix,
                EntryIds = entryIds,
                IdToRow = idToRow,
                NumBins = numBins
            };
        }

        private static void PreprocessSingleEntry(
            LibraryEntry entry, int numBins, double minMz, double binWidth,
            float[,] matrix, int row)
        {
            double maxMz = minMz + numBins * binWidth;

            foreach (var frag in entry.Fragments)
            {
                if (frag.Mz >= minMz && frag.Mz < maxMz)
                {
                    int bin = (int)((frag.Mz - minMz) / binWidth);
                    if (bin >= 0 && bin < numBins)
                        matrix[row, bin] += (float)Math.Sqrt(frag.RelativeIntensity);
                }
            }

            // L2 normalize
            float norm = 0f;
            for (int b = 0; b < numBins; b++)
                norm += matrix[row, b] * matrix[row, b];
            norm = (float)Math.Sqrt(norm);

            if (norm > 1e-10f)
            {
                for (int b = 0; b < numBins; b++)
                    matrix[row, b] /= norm;
            }
        }
    }

    /// <summary>
    /// Preprocessed spectra ready for batch XCorr scoring.
    /// Stores binned, sqrt-preprocessed, L2-normalized vectors.
    /// </summary>
    public class PreprocessedSpectra
    {
        /// <summary>Matrix of preprocessed spectra (n_spectra x n_bins), row-major.</summary>
        public float[,] Matrix { get; private set; }
        /// <summary>Spectrum indices corresponding to each row.</summary>
        public int[] SpectrumIndices { get; private set; }
        /// <summary>Retention times for each spectrum.</summary>
        public double[] RetentionTimes { get; private set; }
        /// <summary>Number of bins used.</summary>
        public int NumBins { get; private set; }

        private PreprocessedSpectra() { }

        /// <summary>
        /// Preprocess spectra into binned, sqrt-preprocessed, L2-normalized vectors.
        /// </summary>
        public static PreprocessedSpectra FromSpectra(
            Spectrum[] spectra, int numBins, double minMz, double binWidth)
        {
            var validIndices = new List<int>();
            for (int i = 0; i < spectra.Length; i++)
            {
                if (spectra[i].Mzs != null && spectra[i].Mzs.Length > 0)
                    validIndices.Add(i);
            }

            int nValid = validIndices.Count;
            var matrix = new float[nValid, numBins];
            var spectrumIndices = new int[nValid];
            var retentionTimes = new double[nValid];

            double maxMz = minMz + numBins * binWidth;

            for (int row = 0; row < nValid; row++)
            {
                int specIdx = validIndices[row];
                var spectrum = spectra[specIdx];
                spectrumIndices[row] = specIdx;
                retentionTimes[row] = spectrum.RetentionTime;

                for (int p = 0; p < spectrum.Mzs.Length; p++)
                {
                    double mz = spectrum.Mzs[p];
                    if (mz >= minMz && mz < maxMz)
                    {
                        int bin = (int)((mz - minMz) / binWidth);
                        if (bin >= 0 && bin < numBins)
                            matrix[row, bin] += (float)Math.Sqrt(spectrum.Intensities[p]);
                    }
                }

                // L2 normalize
                float norm = 0f;
                for (int b = 0; b < numBins; b++)
                    norm += matrix[row, b] * matrix[row, b];
                norm = (float)Math.Sqrt(norm);

                if (norm > 1e-10f)
                {
                    for (int b = 0; b < numBins; b++)
                        matrix[row, b] /= norm;
                }
            }

            return new PreprocessedSpectra
            {
                Matrix = matrix,
                SpectrumIndices = spectrumIndices,
                RetentionTimes = retentionTimes,
                NumBins = numBins
            };
        }
    }

    /// <summary>
    /// Batch scorer for all-vs-all library-spectrum scoring using matrix multiply.
    /// Port of BatchScorer from osprey-scoring/src/batch.rs (simplified, no BLAS).
    /// </summary>
    public class BatchScorer
    {
        private const int DEFAULT_NUM_BINS = 2000;
        private const double DEFAULT_MIN_MZ = 200.0;
        private const double DEFAULT_BIN_WIDTH = 1.0005;

        private readonly int _numBins;
        private readonly double _minMz;
        private readonly double _binWidth;

        public BatchScorer()
            : this(DEFAULT_NUM_BINS, DEFAULT_MIN_MZ, DEFAULT_BIN_WIDTH) { }

        public BatchScorer(int numBins, double minMz, double binWidth)
        {
            _numBins = numBins;
            _minMz = minMz;
            _binWidth = binWidth;
        }

        /// <summary>
        /// Preprocess library entries for batch scoring.
        /// </summary>
        public PreprocessedLibrary PreprocessLibrary(LibraryEntry[] entries)
        {
            return PreprocessedLibrary.FromEntries(entries, _numBins, _minMz, _binWidth);
        }

        /// <summary>
        /// Preprocess spectra for batch scoring.
        /// </summary>
        public PreprocessedSpectra PreprocessSpectra(Spectrum[] spectra)
        {
            return PreprocessedSpectra.FromSpectra(spectra, _numBins, _minMz, _binWidth);
        }

        /// <summary>
        /// Compute all-vs-all scores: library (rows) x spectra (cols).
        /// Returns a score matrix [n_library, n_spectra].
        /// Uses direct loops (no BLAS dependency).
        /// </summary>
        public double[,] BatchXCorr(PreprocessedLibrary library, PreprocessedSpectra spectra)
        {
            int nLib = library.EntryIds.Length;
            int nSpec = spectra.SpectrumIndices.Length;
            int nBins = library.NumBins;
            var scores = new double[nLib, nSpec];

            for (int lib = 0; lib < nLib; lib++)
            {
                for (int spec = 0; spec < nSpec; spec++)
                {
                    float dot = 0f;
                    for (int b = 0; b < nBins; b++)
                        dot += library.Matrix[lib, b] * spectra.Matrix[spec, b];
                    scores[lib, spec] = dot;
                }
            }

            return scores;
        }

        /// <summary>
        /// For each library entry, find the spectrum with the highest score.
        /// Returns list of (entryId, spectrumIdx, score, rt).
        /// </summary>
        public List<Tuple<uint, int, double, double>> FindBestMatches(
            PreprocessedLibrary library, PreprocessedSpectra spectra)
        {
            double[,] scores = BatchXCorr(library, spectra);
            int nLib = library.EntryIds.Length;
            int nSpec = spectra.SpectrumIndices.Length;

            var results = new List<Tuple<uint, int, double, double>>();

            for (int lib = 0; lib < nLib; lib++)
            {
                double bestScore = 0.0;
                int bestCol = -1;

                for (int spec = 0; spec < nSpec; spec++)
                {
                    if (scores[lib, spec] > bestScore)
                    {
                        bestScore = scores[lib, spec];
                        bestCol = spec;
                    }
                }

                if (bestCol >= 0 && bestScore > 0.0)
                {
                    results.Add(Tuple.Create(
                        library.EntryIds[lib],
                        spectra.SpectrumIndices[bestCol],
                        bestScore,
                        spectra.RetentionTimes[bestCol]));
                }
            }

            return results;
        }
    }

    /// <summary>
    /// LibCosine scorer using PPM-based matching with sqrt intensity.
    /// Port of LibCosineScorer from osprey-scoring/src/batch.rs.
    /// </summary>
    public class LibCosineScorer
    {
        private readonly FragmentToleranceConfig _tolerance;

        public LibCosineScorer()
        {
            _tolerance = FragmentToleranceConfig.Default();
        }

        public LibCosineScorer(FragmentToleranceConfig tolerance)
        {
            _tolerance = tolerance;
        }

        /// <summary>
        /// Match fragments from a library entry against an observed spectrum.
        /// </summary>
        public FragmentMatchResult MatchFragments(LibraryEntry library, Spectrum spectrum)
        {
            var matchedExp = new List<double>();
            var matchedLib = new List<double>();
            var matchedMzs = new List<double>();
            var massErrors = new List<double>();
            int nMatched = 0;

            foreach (var frag in library.Fragments)
            {
                double libMz = frag.Mz;
                double libIntensity = frag.RelativeIntensity;

                double bestIntensity = 0.0;
                double? bestMz = null;
                double bestMzDiff = double.PositiveInfinity;

                for (int j = 0; j < spectrum.Mzs.Length; j++)
                {
                    double expMz = spectrum.Mzs[j];
                    if (_tolerance.WithinTolerance(libMz, expMz))
                    {
                        double mzDiff = Math.Abs(expMz - libMz);
                        if (mzDiff < bestMzDiff)
                        {
                            bestMzDiff = mzDiff;
                            bestIntensity = spectrum.Intensities[j];
                            bestMz = expMz;
                        }
                    }
                }

                matchedExp.Add(bestIntensity);
                matchedLib.Add(libIntensity);
                matchedMzs.Add(libMz);

                if (bestMz.HasValue)
                {
                    massErrors.Add(_tolerance.MassError(libMz, bestMz.Value));
                    nMatched++;
                }
            }

            return new FragmentMatchResult
            {
                MatchedExpIntensities = matchedExp.ToArray(),
                MatchedLibIntensities = matchedLib.ToArray(),
                MatchedLibMzs = matchedMzs.ToArray(),
                MassErrors = massErrors.ToArray(),
                NMatched = nMatched
            };
        }

        /// <summary>
        /// Calculate LibCosine score from a fragment match result.
        /// Uses sqrt preprocessing and cosine similarity.
        /// </summary>
        public double CalculateScore(FragmentMatchResult matchResult)
        {
            if (matchResult.MatchedExpIntensities.Length == 0)
                return 0.0;

            int n = matchResult.MatchedExpIntensities.Length;
            double dot = 0.0;
            double normA = 0.0;
            double normB = 0.0;

            for (int i = 0; i < n; i++)
            {
                double expSqrt = Math.Sqrt(matchResult.MatchedExpIntensities[i]);
                double libSqrt = Math.Sqrt(matchResult.MatchedLibIntensities[i]);
                dot += expSqrt * libSqrt;
                normA += expSqrt * expSqrt;
                normB += libSqrt * libSqrt;
            }

            double dNormA = Math.Sqrt(normA);
            double dNormB = Math.Sqrt(normB);

            if (dNormA < 1e-10 || dNormB < 1e-10)
                return 0.0;

            double cos = dot / (dNormA * dNormB);
            return Math.Max(0.0, Math.Min(1.0, cos));
        }

        /// <summary>
        /// For each library entry, find the best matching scan from a set of spectra.
        /// Returns list of (entryId, bestSpectrumIdx, score, rt).
        /// </summary>
        public List<Tuple<uint, int, double, double>> FindBestMatches(
            LibraryEntry[] entries, Spectrum[] spectra)
        {
            var results = new List<Tuple<uint, int, double, double>>();

            for (int e = 0; e < entries.Length; e++)
            {
                var entry = entries[e];
                double bestScore = 0.0;
                int bestIdx = -1;

                for (int s = 0; s < spectra.Length; s++)
                {
                    var matchResult = MatchFragments(entry, spectra[s]);
                    double score = CalculateScore(matchResult);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIdx = s;
                    }
                }

                if (bestIdx >= 0 && bestScore > 0.0)
                {
                    results.Add(Tuple.Create(
                        entry.Id, bestIdx, bestScore, spectra[bestIdx].RetentionTime));
                }
            }

            return results;
        }

        /// <summary>
        /// Count how many of the top-6 library fragments (by intensity) match
        /// in the given spectrum.
        /// </summary>
        public static byte CountTop6MatchedAtApex(
            List<LibraryFragment> libraryFragments,
            double[] spectrumMzs,
            FragmentToleranceConfig tolerance)
        {
            if (libraryFragments == null || libraryFragments.Count == 0 ||
                spectrumMzs == null || spectrumMzs.Length == 0)
                return 0;

            // Get top 6 by intensity
            int nTop = Math.Min(libraryFragments.Count, 6);
            var indexed = new List<KeyValuePair<int, float>>();
            for (int i = 0; i < libraryFragments.Count; i++)
                indexed.Add(new KeyValuePair<int, float>(i, libraryFragments[i].RelativeIntensity));
            indexed.Sort((a, b) => b.Value.CompareTo(a.Value));

            byte count = 0;
            for (int t = 0; t < nTop; t++)
            {
                int idx = indexed[t].Key;
                double libMz = libraryFragments[idx].Mz;
                if (SpectralScorer.HasMatch(libMz, spectrumMzs, tolerance))
                    count++;
            }

            return count;
        }
    }
}
