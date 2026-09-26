/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/ai.py
 *   (train_rt), which scores with scikit-learn's r2_score and median_absolute_error
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
using System.Linq;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>RT prediction quality on a test set, on the normalized (<c>rt / rt_max</c>) scale.</summary>
    public sealed class RtMetricSummary
    {
        public RtMetricSummary(double r2, double medianAbsoluteError, int count)
        {
            R2 = r2;
            MedianAbsoluteError = medianAbsoluteError;
            Count = count;
        }

        public double R2 { get; }
        public double MedianAbsoluteError { get; }
        public int Count { get; }

        public override string ToString()
        {
            return string.Format(@"R2 {0:F4}, median |error| {1:F5} (n={2})", R2, MedianAbsoluteError, Count);
        }
    }

    /// <summary>RT metrics and Carafe's collapsing of RT training rows.</summary>
    public static class RtMetrics
    {
        public static RtMetricSummary Evaluate(RtModel model, IReadOnlyList<RtTrainingExample> test)
        {
            double[] predicted = model.Predict(test.Select(e => e.Peptide).ToArray());
            double[] observed = test.Select(e => e.RtNorm).ToArray();
            double mean = observed.Average();
            double residual = 0, total = 0;
            var errors = new double[observed.Length];
            for (int i = 0; i < observed.Length; i++)
            {
                double error = observed[i] - predicted[i];
                residual += error * error;
                total += (observed[i] - mean) * (observed[i] - mean);
                errors[i] = Math.Abs(error);
            }
            // scikit-learn's r2_score: 1 when the observations are constant and fitted exactly.
            double r2 = total > 0 ? 1 - residual / total : (residual > 0 ? 0 : 1);
            return new RtMetricSummary(r2, Ms2Metrics.Median(errors), observed.Length);
        }

        /// <summary>
        /// Carafe's <c>groupby(['sequence','mods','mod_sites'])['rt_norm'].median()</c>: one row per
        /// peptide form, in pandas' sorted key order (ordinal string order on the three keys).
        /// </summary>
        public static IReadOnlyList<RtTrainingExample> CollapseByForm(IEnumerable<RtTrainingExample> rows)
        {
            return rows.GroupBy(r => (r.Peptide.Sequence, r.Peptide.ModsText, r.Peptide.ModSitesText))
                .OrderBy(g => g.Key.Sequence, StringComparer.Ordinal)
                .ThenBy(g => g.Key.ModsText, StringComparer.Ordinal)
                .ThenBy(g => g.Key.ModSitesText, StringComparer.Ordinal)
                .Select(g => new RtTrainingExample(g.First().Peptide, Ms2Metrics.Median(g.Select(r => r.RtNorm))))
                .ToArray();
        }
    }
}
