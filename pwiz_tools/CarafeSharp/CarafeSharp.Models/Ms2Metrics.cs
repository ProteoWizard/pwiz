/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/models.py
 *   (calc_ms2_similarity_mask and its helpers), from AlphaPeptDeep, Apache-2.0
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
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>Median similarity of predicted to observed spectra over a test set.</summary>
    public sealed class Ms2MetricSummary
    {
        public Ms2MetricSummary(double pcc, double cos, double sa, double spc, int count)
        {
            Pcc = pcc;
            Cos = cos;
            Sa = sa;
            Spc = spc;
            Count = count;
        }

        public double Pcc { get; }
        public double Cos { get; }
        public double Sa { get; }
        public double Spc { get; }
        public int Count { get; }

        /// <summary>Carafe's rule for using a fine-tuned model: better on all four medians.</summary>
        public bool BeatsOnAll(Ms2MetricSummary other)
        {
            return Pcc > other.Pcc && Cos > other.Cos && Sa > other.Sa && Spc > other.Spc;
        }

        public override string ToString()
        {
            return string.Format(@"PCC {0:F4}, COS {1:F4}, SA {2:F4}, SPC {3:F4} (n={4})", Pcc, Cos, Sa, Spc, Count);
        }
    }

    /// <summary>
    /// Carafe's masked MS2 similarity metrics. Each spectrum is flattened over all eight
    /// fragment columns (the four modloss columns are zeros on both sides but still count in
    /// the Pearson mean and the ranks, as in Carafe), ions with a positive invalid count are
    /// masked, and the per-spectrum values are summarized by their medians.
    /// </summary>
    public static class Ms2Metrics
    {
        public static Ms2MetricSummary Evaluate(Ms2Model model, IReadOnlyList<Ms2TrainingExample> test)
        {
            var predictions = model.Predict(test.Select(e => new Ms2Request(e.Precursor, e.Nce, e.Instrument)).ToArray());
            var pcc = new List<double>();
            var cos = new List<double>();
            var sa = new List<double>();
            var spc = new List<double>();
            foreach (var group in Enumerable.Range(0, test.Count).GroupBy(i => test[i].Precursor.Peptide.Length))
            {
                int[] rows = group.ToArray();
                int width = (group.Key - 1) * PeptdeepConstants.CHARGED_FRAG_TYPES.Length;
                var predicted = new float[rows.Length * width];
                var observed = new float[rows.Length * width];
                var mask = new float[rows.Length * width];
                for (int r = 0; r < rows.Length; r++)
                {
                    var example = test[rows[r]];
                    Array.Copy(predictions[rows[r]].Intensities, 0, predicted, r * width, width);
                    FillObserved(example, observed, mask, r * width);
                }
                using (NewDisposeScope())
                {
                    var x = tensor(predicted, new long[] { rows.Length, width });
                    var y = tensor(observed, new long[] { rows.Length, width });
                    var m = tensor(mask, new long[] { rows.Length, width });
                    var pearson = PearsonMasked(x, y, m);
                    var cosine = nn.functional.cosine_similarity(x * m, y * m);
                    var angle = 1 - 2 * cosine.clamp_max(1).arccos() / Math.PI;
                    var spearman = PearsonMasked(Ranks(x), Ranks(y), m);
                    pcc.AddRange(pearson.data<float>().ToArray().Select(v => (double)v));
                    cos.AddRange(cosine.data<float>().ToArray().Select(v => (double)v));
                    sa.AddRange(angle.data<float>().ToArray().Select(v => (double)v));
                    spc.AddRange(spearman.data<float>().ToArray().Select(v => (double)v));
                }
            }
            return new Ms2MetricSummary(Median(pcc), Median(cos), Median(sa), Median(spc), test.Count);
        }

        /// <summary>The median, ignoring NaN, as pandas computes it.</summary>
        public static double Median(IEnumerable<double> values)
        {
            var sorted = values.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToArray();
            if (sorted.Length == 0)
                return double.NaN;
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
        }

        /// <summary>
        /// Observed intensities and the loss mask in the eight-column layout: modloss columns
        /// are 0 with mask 1.
        /// </summary>
        internal static void FillObserved(Ms2TrainingExample example, float[] observed, float[] mask, int offset)
        {
            int rows = example.Precursor.Peptide.Length - 1;
            int columns = PeptdeepConstants.CHARGED_FRAG_TYPES.Length;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int target = offset + row * columns + col;
                    if (col < Ms2TrainingExample.FRAGMENT_TYPES)
                    {
                        int source = row * Ms2TrainingExample.FRAGMENT_TYPES + col;
                        observed[target] = (float)example.Intensities[source];
                        mask[target] = example.Invalid[source] <= 0 ? 1 : 0;
                    }
                    else
                    {
                        observed[target] = 0;
                        mask[target] = 1;
                    }
                }
            }
        }

        private static Tensor PearsonMasked(Tensor x, Tensor y, Tensor mask)
        {
            var count = mask.sum(1, true);
            var xMean = (x * mask).sum(1, true) / count;
            var yMean = (y * mask).sum(1, true) / count;
            return nn.functional.cosine_similarity((x - xMean) * mask, (y - yMean) * mask);
        }

        /// <summary>
        /// Carafe's <c>_get_ranks</c>: each value's position in its row sorted ascending, with the
        /// rank of every zero value set to 0.
        /// </summary>
        private static Tensor Ranks(Tensor x)
        {
            var order = x.argsort(1);
            var ranks = zeros_like(order);
            ranks.scatter_(1, order, arange(x.shape[1], dtype: ScalarType.Int64).unsqueeze(0).expand(x.shape[0], -1));
            ranks = ranks.masked_fill(x == 0, 0);
            return ranks.to_type(ScalarType.Float32);
        }
    }
}
