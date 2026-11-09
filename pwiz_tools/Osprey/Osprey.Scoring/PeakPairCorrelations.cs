/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Chromatography;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// The Pearson correlation of every pair of fragment XICs over one inclusive scan range, as
    /// <see cref="ScoringMath.PearsonCorrelationInRange"/> returns it (NaN for a range under three
    /// scans). The peak pick, the reference-fragment choice and the coelution, reference and
    /// top-fragment feature families all read the pairwise correlations over the chosen peak, so
    /// they are computed once per candidate and cached on the <see cref="OspreyScoringContext"/>.
    /// <para>Exact for every reader in either argument order: the correlation sums the products
    /// x[i] * y[i] and combines the two arrays' sums by multiplication only, and IEEE multiplication
    /// is commutative, so swapping the arrays gives the same bits.</para>
    /// </summary>
    internal sealed class PeakPairCorrelations
    {
        private readonly IReadOnlyList<XicData> _xics;
        private readonly int _start;
        private readonly int _end;
        private readonly double[] _values;   // Pairs i < j in loop order: (0,1), (0,2), ..., (1,2), ...

        private PeakPairCorrelations(IReadOnlyList<XicData> xics, int start, int end)
        {
            _xics = xics;
            _start = start;
            _end = end;
            int n = xics.Count;
            _values = new double[n * (n - 1) / 2];
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                    _values[k++] = ScoringMath.PearsonCorrelationInRange(xics[i].Intensities, xics[j].Intensities, start, end);
            }
        }

        /// <summary>
        /// The correlations of <paramref name="peakData"/>'s XICs over its peak bounds, computed on
        /// first use for the current candidate.
        /// </summary>
        public static PeakPairCorrelations GetOrCompute(OspreyScoringContext context, IOspreyDetailedPeakData peakData)
        {
            if (context.TryGetInfo(out PeakPairCorrelations pairs))
                return pairs;
            pairs = Compute(peakData.Xics, peakData.PeakBounds.StartIndex, peakData.PeakBounds.EndIndex);
            context.AddInfo(pairs);
            return pairs;
        }

        /// <summary>The correlations of every pair of <paramref name="xics"/> over [start, end].</summary>
        public static PeakPairCorrelations Compute(IReadOnlyList<XicData> xics, int start, int end)
        {
            return new PeakPairCorrelations(xics, start, end);
        }

        /// <summary>True when these are the correlations of <paramref name="xics"/> over [start, end].</summary>
        public bool Covers(IReadOnlyList<XicData> xics, int start, int end)
        {
            return ReferenceEquals(xics, _xics) && start == _start && end == _end;
        }

        /// <summary>The number of XICs correlated.</summary>
        public int XicCount { get { return _xics.Count; } }

        /// <summary>The correlations of pairs i &lt; j, in the order of a loop over i, then j from i + 1.</summary>
        public IReadOnlyList<double> InLoopOrder { get { return _values; } }

        /// <summary>The correlation of XICs <paramref name="i"/> and <paramref name="j"/>, in either order.</summary>
        public double Get(int i, int j)
        {
            if (i == j)
                return ScoringMath.PearsonCorrelationInRange(_xics[i].Intensities, _xics[j].Intensities, _start, _end);
            if (i > j)
                (i, j) = (j, i);
            return _values[i * (2 * _xics.Count - i - 1) / 2 + (j - i - 1)];
        }

        /// <summary>The mean of the valid (non-NaN) correlations, summed in loop order; 0 when none is.</summary>
        public double MeanValid()
        {
            double sum = 0.0;
            int count = 0;
            foreach (double corr in _values)
            {
                if (!double.IsNaN(corr))
                {
                    sum += corr;
                    count++;
                }
            }
            return count > 0 ? sum / count : 0.0;
        }
    }
}
