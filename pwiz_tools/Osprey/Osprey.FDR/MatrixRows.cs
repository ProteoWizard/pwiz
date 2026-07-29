/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Row-gathering helpers over <see cref="Matrix"/>, extracted from
    /// the original <c>PercolatorFdr</c> god class (issue #4468) because both the training and the scoring
    /// paths use them - they belong to neither, so they would have had to be
    /// duplicated or reached across once those two split apart.
    ///
    /// This is the hottest code in Percolator: <see cref="MatrixRows.ExtractRows"/> is
    /// called roughly 540 times per file on 200K x 21 matrices, so the direct
    /// array access and the caller-supplied-buffer variant below are deliberate
    /// performance choices, not style. Moved verbatim.
    /// </summary>
    internal static class MatrixRows
    {
        internal static Matrix ExtractRows(Matrix matrix, int[] rowIndices)
        {
            int nCols = matrix.Cols;
            int nRows = rowIndices.Length;
            var data = new double[nRows * nCols];
            // Direct array access avoids property accessor overhead and the
            // bounds-check on every cell. This loop is the hottest path in
            // Percolator: called ~540 times per file with 200K x 21 matrices.
            double[] src = matrix.Data;
            for (int i = 0; i < nRows; i++)
            {
                int srcOffset = rowIndices[i] * nCols;
                int dstOffset = i * nCols;
                Array.Copy(src, srcOffset, data, dstOffset, nCols);
            }
            return Matrix.WrapNoClone(data, nRows, nCols);
        }

        /// <summary>
        /// Variant of <see cref="MatrixRows.ExtractRows"/> that writes into a
        /// caller-supplied <paramref name="destData"/> buffer (must be
        /// at least <c>rowIndices.Length * matrix.Cols</c> long) and
        /// wraps the prefix as a Matrix. Avoids the ~8 MB LOH allocation
        /// per call on HRAM Astral. The trailing unused suffix of
        /// <paramref name="destData"/> is left untouched (Matrix.Rows
        /// hides it).
        /// </summary>
        internal static Matrix ExtractRowsInto(Matrix matrix, int[] rowIndices, double[] destData)
        {
            int nCols = matrix.Cols;
            int nRows = rowIndices.Length;
            int need = nRows * nCols;
            if (destData.Length < need)
                throw new ArgumentException(
                    string.Format("destData length {0} < required {1}", destData.Length, need));
            double[] src = matrix.Data;
            for (int i = 0; i < nRows; i++)
            {
                int srcOffset = rowIndices[i] * nCols;
                int dstOffset = i * nCols;
                Array.Copy(src, srcOffset, destData, dstOffset, nCols);
            }
            // Pool-friendly wrap: Matrix.WrapPrefixNoClone accepts a
            // backing array >= rows*cols. The trailing suffix of
            // destData (from prior larger calls) is left untouched and
            // never read.
            return Matrix.WrapPrefixNoClone(destData, nRows, nCols);
        }

        internal static double[] ExtractRow(Matrix matrix, int row)
        {
            var dest = new double[matrix.Cols];
            CopyRow(matrix, row, dest);
            return dest;
        }

        internal static void CopyRow(Matrix matrix, int row, double[] dest)
        {
            int cols = matrix.Cols;
            for (int j = 0; j < cols; j++)
                dest[j] = matrix[row, j];
        }
    }
}
