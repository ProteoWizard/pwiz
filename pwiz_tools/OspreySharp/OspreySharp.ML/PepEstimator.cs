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

// Posterior Error Probability estimation using KDE + isotonic regression
//
// Implements the Percolator/qvality-style PEP calculation:
// 1. KDE density estimation for target and decoy score distributions
// 2. Bayes' rule for posterior probability of being incorrect
// 3. Isotonic regression (PAVA) for monotonicity enforcement
//
// Originally from Sage (https://github.com/lazear/sage)
// Copyright (c) 2022 Michael Lazear
// Licensed under the MIT License

using System;
using System.Collections.Generic;

namespace pwiz.OspreySharp.ML
{
    /// <summary>
    /// Posterior error probability estimator using KDE + isotonic regression.
    /// Port of PepEstimator from pep.rs.
    /// </summary>
    public class PepEstimator
    {
        private static readonly int DEFAULT_N_BINS = 1000;

        private readonly double[] _bins;
        private readonly double _minScore;
        private readonly double _scoreStep;

        private PepEstimator(double[] bins, double minScore, double scoreStep)
        {
            _bins = bins;
            _minScore = minScore;
            _scoreStep = scoreStep;
        }

        /// <summary>
        /// Fit PEP model on competition winners.
        /// </summary>
        /// <param name="scores">Scores of competition winners</param>
        /// <param name="isDecoy">Whether each winner is a decoy</param>
        /// <param name="nBins">Number of bins for score discretization</param>
        /// <returns>Fitted PepEstimator</returns>
        public static PepEstimator Fit(double[] scores, bool[] isDecoy, int nBins)
        {
            if (scores.Length != isDecoy.Length)
                throw new ArgumentException("scores and isDecoy must have same length");

            if (scores.Length == 0)
                return new PepEstimator(new[] { 1.0 }, 0.0, 1.0);

            // Separate target and decoy scores
            var decoyScores = new List<double>();
            var targetScores = new List<double>();
            for (int i = 0; i < scores.Length; i++)
            {
                if (isDecoy[i])
                    decoyScores.Add(scores[i]);
                else
                    targetScores.Add(scores[i]);
            }

            if (targetScores.Count == 0 || decoyScores.Count == 0)
            {
                // Can't estimate PEP without both classes
                double minScoreFallback = double.PositiveInfinity;
                for (int i = 0; i < scores.Length; i++)
                    minScoreFallback = Math.Min(minScoreFallback, scores[i]);
                var fallbackBins = new double[nBins];
                for (int i = 0; i < nBins; i++)
                    fallbackBins[i] = 1.0;
                return new PepEstimator(fallbackBins, minScoreFallback, 1.0);
            }

            // pi0: prior probability that a target is incorrect
            double pi0 = Math.Max(0.01, Math.Min(0.99,
                (double)decoyScores.Count / targetScores.Count));

            // KDE for each distribution
            var decoyKde = new Kde(decoyScores.ToArray());
            var targetKde = new Kde(targetScores.ToArray());

            // Score range for binning
            double minScore = double.PositiveInfinity;
            double maxScore = double.NegativeInfinity;
            for (int i = 0; i < scores.Length; i++)
            {
                minScore = Math.Min(minScore, scores[i]);
                maxScore = Math.Max(maxScore, scores[i]);
            }
            double scoreStep = nBins > 1 ? (maxScore - minScore) / (nBins - 1) : 1.0;

            // Compute raw PEP at each bin
            var bins = new double[nBins];
            for (int i = 0; i < nBins; i++)
            {
                double score = minScore + i * scoreStep;
                double fDecoy = decoyKde.Pdf(score) * pi0;
                double fTarget = targetKde.Pdf(score) * (1.0 - pi0);
                double denom = fDecoy + fTarget;
                if (denom > 0.0)
                    bins[i] = Math.Max(0.0, Math.Min(1.0, fDecoy / denom));
                else
                    bins[i] = 1.0;
            }

            // Enforce monotonicity: PEP must be non-increasing as score increases
            IsotonicRegressionDecreasing(bins);

            return new PepEstimator(bins, minScore, scoreStep);
        }

        /// <summary>
        /// Fit with default 1000 bins.
        /// </summary>
        public static PepEstimator FitDefault(double[] scores, bool[] isDecoy)
        {
            return Fit(scores, isDecoy, DEFAULT_N_BINS);
        }

        /// <summary>
        /// Look up PEP for a given score using linear interpolation.
        /// </summary>
        public double PosteriorError(double score)
        {
            if (_bins.Length == 0)
                return 1.0;

            int binLo = Math.Min(_bins.Length - 1,
                Math.Max(0, (int)Math.Floor((score - _minScore) / _scoreStep)));
            int binHi = Math.Min(_bins.Length - 1, binLo + 1);

            double lower = _bins[binLo];
            double upper = _bins[binHi];

            // Linear interpolation
            double binLoScore = binLo * _scoreStep + _minScore;
            double frac = Math.Max(0.0, Math.Min(1.0, (score - binLoScore) / _scoreStep));

            double pep = lower + (upper - lower) * frac;
            return Math.Max(0.0, Math.Min(1.0, pep));
        }

        /// <summary>
        /// Pool Adjacent Violators Algorithm (PAVA) for isotonic regression.
        /// Enforces that the output is monotonically non-increasing
        /// (higher index = higher score = lower PEP).
        /// </summary>
        public static void IsotonicRegressionDecreasing(double[] values)
        {
            if (values.Length <= 1)
                return;

            int n = values.Length;

            // Store blocks as (startIndex, endIndexExclusive, value)
            var blocks = new List<double[]>(); // each: [start, end, value]

            for (int i = 0; i < n; i++)
            {
                // Start a new block with this single element
                blocks.Add(new[] { i, i + 1, values[i] });

                // Merge with previous block while we have a violation
                while (blocks.Count >= 2)
                {
                    int len = blocks.Count;
                    var prev = blocks[len - 2];
                    var curr = blocks[len - 1];

                    if (curr[2] > prev[2])
                    {
                        // Violation: current block has higher value than previous
                        double prevCount = prev[1] - prev[0];
                        double currCount = curr[1] - curr[0];
                        double avg = (prev[2] * prevCount + curr[2] * currCount) / (prevCount + currCount);

                        double newStart = prev[0];
                        double newEnd = curr[1];

                        blocks.RemoveAt(blocks.Count - 1);
                        blocks.RemoveAt(blocks.Count - 1);
                        blocks.Add(new[] { newStart, newEnd, avg });
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Write merged block values back
            foreach (var block in blocks)
            {
                int start = (int)block[0];
                int end = (int)block[1];
                double value = block[2];
                for (int i = start; i < end; i++)
                    values[i] = value;
            }
        }

        /// <summary>
        /// Gaussian KDE for density estimation (Silverman's rule bandwidth).
        /// </summary>
        private class Kde
        {
            private readonly double[] _sample;
            private readonly double _bandwidth;
            private readonly double _constant;

            public Kde(double[] sample)
            {
                _sample = sample;
                double sigma = MlMath.Std(sample);
                double n = sample.Length;
                double factor = 4.0 / 3.0;
                double exponent = 1.0 / 5.0;
                double bandwidth = sigma * Math.Pow(factor / n, exponent);
                double constant = Math.Sqrt(2.0 * Math.PI) * bandwidth * n;

                _bandwidth = bandwidth > 0.0 ? bandwidth : 1.0;
                _constant = constant > 0.0 ? constant : 1.0;
            }

            public double Pdf(double x)
            {
                double h = _bandwidth;
                double sum = 0.0;
                for (int i = 0; i < _sample.Length; i++)
                {
                    double z = (x - _sample[i]) / h;
                    sum += Math.Exp(-0.5 * z * z);
                }
                return sum / _constant;
            }
        }
    }
}
