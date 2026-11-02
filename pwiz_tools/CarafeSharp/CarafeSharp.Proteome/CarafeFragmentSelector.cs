/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.AIGear
 *   (get_fragment_ion_intensity4parquet_all)
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
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Chooses a precursor's library fragments from its predicted b_z1, b_z2, y_z1 and y_z2
    /// intensities the way Carafe's Java does:
    /// <list type="number">
    /// <item>A fragment is a candidate when its intensity is above 0, its float32 m/z lies in
    /// [min, max] and its ion number is at least <c>-lf_frag_n_min</c>.</item>
    /// <item>The <c>-lf_top_n_frag</c> most intense candidates are kept. Carafe sorts them out
    /// of a <c>HashMap&lt;Integer, Double&gt;</c> keyed by the 1-based fragment position, stably,
    /// so an intensity tie is broken by that map's iteration order (<see cref="JavaHashOrder"/>).</item>
    /// <item>Each kept fragment's intensity is divided by the largest candidate intensity (in
    /// float64, then rounded to float32), and the fragments are sorted by that value, most
    /// intense first, ties in fragment position order.</item>
    /// </list>
    /// </summary>
    public sealed class CarafeFragmentSelector
    {
        private readonly double _minMz;
        private readonly double _maxMz;
        private readonly int _topN;
        private readonly int _minFragmentNumber;

        public CarafeFragmentSelector(double minMz, double maxMz, int topN, int minFragmentNumber)
        {
            if (topN < 0)
                throw new ArgumentOutOfRangeException(nameof(topN), topN, @"The number of fragments to keep cannot be negative.");
            _minMz = minMz;
            _maxMz = maxMz;
            _topN = topN;
            _minFragmentNumber = minFragmentNumber;
        }

        /// <summary>
        /// The library fragments of one precursor.
        /// </summary>
        /// <param name="intensities">
        /// Normalized predicted intensities, row-major with <paramref name="stride"/> values per
        /// row, the first four being b_z1, b_z2, y_z1 and y_z2; row r is b(r+1) and y(n-1-r).
        /// </param>
        /// <param name="stride">Values per row of <paramref name="intensities"/>.</param>
        /// <param name="theoreticalMz">
        /// The fragment m/z of <see cref="AlphabaseFragmentMz.Calculate"/>, four per row.
        /// </param>
        public List<LibraryFragment> Select(float[] intensities, int stride, double[] theoreticalMz)
        {
            int rows = theoreticalMz.Length / AlphabaseFragmentMz.COLUMN_COUNT;
            var candidateIds = new List<int>();
            var candidateIntensities = new List<double>();
            double maxIntensity = 0;
            for (int row = 0; row < rows; row++)
            {
                int bNumber = row + 1;
                int yNumber = rows - row;
                for (int k = 0; k < AlphabaseFragmentMz.COLUMN_COUNT; k++)
                {
                    double intensity = intensities[row * stride + k];
                    double mz = (float)theoreticalMz[row * AlphabaseFragmentMz.COLUMN_COUNT + k];
                    if (!(intensity > 0.0 && mz >= _minMz && mz <= _maxMz))
                        continue;
                    if ((IsB(k) ? bNumber : yNumber) < _minFragmentNumber)
                        continue;
                    candidateIds.Add(row * AlphabaseFragmentMz.COLUMN_COUNT + k + 1);
                    candidateIntensities.Add(intensity);
                    if (maxIntensity < intensity)
                        maxIntensity = intensity;
                }
            }

            var selected = SelectTop(candidateIds, candidateIntensities);
            var fragments = new List<LibraryFragment>(selected.Count);
            var relative = new List<float>(selected.Count);
            foreach (int index in selected)
            {
                int position = candidateIds[index] - 1;
                int row = position / AlphabaseFragmentMz.COLUMN_COUNT;
                int k = position % AlphabaseFragmentMz.COLUMN_COUNT;
                float relativeIntensity = (float)(candidateIntensities[index] / maxIntensity);
                double mz = theoreticalMz[position];
                fragments.Add(new LibraryFragment(IsB(k) ? 'b' : 'y', IsB(k) ? row + 1 : rows - row, IsCharge2(k) ? 2 : 1,
                    LibraryFragment.NO_LOSS, mz, (float)mz, relativeIntensity));
                relative.Add(relativeIntensity);
            }
            return SortByIntensity(fragments, relative);
        }

        /// <summary>
        /// Indexes of the kept candidates, in fragment position order: the top N by intensity,
        /// ties in HashMap iteration order.
        /// </summary>
        private List<int> SelectTop(List<int> candidateIds, List<double> candidateIntensities)
        {
            int count = candidateIds.Count;
            int tableSize = JavaHashOrder.TableSize(count);
            var order = new int[count];
            for (int i = 0; i < count; i++)
                order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int byIntensity = candidateIntensities[b].CompareTo(candidateIntensities[a]);
                if (byIntensity != 0)
                    return byIntensity;
                int byBucket = JavaHashOrder.BucketIndex(candidateIds[a], tableSize)
                    .CompareTo(JavaHashOrder.BucketIndex(candidateIds[b], tableSize));
                return byBucket != 0 ? byBucket : a.CompareTo(b);
            });
            int kept = Math.Min(_topN, count);
            var selected = new List<int>(kept);
            for (int i = 0; i < kept; i++)
                selected.Add(order[i]);
            selected.Sort();
            return selected;
        }

        /// <summary>A stable sort by relative intensity, most intense first.</summary>
        private static List<LibraryFragment> SortByIntensity(List<LibraryFragment> fragments, List<float> relative)
        {
            var order = new int[fragments.Count];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int byIntensity = relative[b].CompareTo(relative[a]);
                return byIntensity != 0 ? byIntensity : a.CompareTo(b);
            });
            var sorted = new List<LibraryFragment>(fragments.Count);
            foreach (int i in order)
                sorted.Add(fragments[i]);
            return sorted;
        }

        private static bool IsB(int column)
        {
            return column == AlphabaseFragmentMz.B_Z1 || column == AlphabaseFragmentMz.B_Z2;
        }

        private static bool IsCharge2(int column)
        {
            return column == AlphabaseFragmentMz.B_Z2 || column == AlphabaseFragmentMz.Y_Z2;
        }
    }
}
