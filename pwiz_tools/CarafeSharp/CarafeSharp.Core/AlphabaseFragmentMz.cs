/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on alphabase (https://github.com/MannLabs/alphabase), Apache-2.0
 *   alphabase.peptide.fragment.create_fragment_mz_dataframe
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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// The b and y fragment m/z values alphabase 1.2.1 computes for the <c>b_z1</c>,
    /// <c>b_z2</c>, <c>y_z1</c> and <c>y_z2</c> columns of Carafe's <c>_ms2_mz_df</c>, the m/z
    /// Carafe writes into its libraries. Per residue, the modification masses at that site
    /// are summed onto a zero, then added to the residue mass; b masses are the running sum
    /// over residues, the peptide mass is the full sum plus water, y masses are the peptide
    /// mass minus b, and m/z is <c>mass / z + proton</c>. A modification at site 0 (N-term)
    /// adds to the first residue and one at site -1 (C-term) to the last. Charge-2 columns are
    /// zero when the precursor charge is below 2. Values are float64; alphabase stores them as
    /// float32 (<see cref="ToFloat32"/>).
    /// </summary>
    public static class AlphabaseFragmentMz
    {
        /// <summary>Columns per fragment row: b_z1, b_z2, y_z1, y_z2.</summary>
        public const int COLUMN_COUNT = 4;

        public const int B_Z1 = 0;
        public const int B_Z2 = 1;
        public const int Y_Z1 = 2;
        public const int Y_Z2 = 3;

        /// <summary>
        /// The m/z values <c>[nAA - 1, 4]</c>, row-major: row r holds b(r+1) and y(nAA-1-r).
        /// </summary>
        public static double[] Calculate(PrecursorForm precursor)
        {
            var peptide = precursor.Peptide;
            string sequence = peptide.Sequence;
            int length = sequence.Length;
            var modMasses = new double[length];
            for (int i = 0; i < peptide.ModNames.Count; i++)
            {
                int site = peptide.ModSites[i];
                int index = site == 0 ? 0 : site == -1 ? length - 1 : site - 1;
                modMasses[index] += ModificationTable.Get(peptide.ModNames[i]).Mass;
            }
            var bMasses = new double[length];
            double running = 0;
            for (int i = 0; i < length; i++)
            {
                double residue = AlphabaseMasses.GetResidueMass(sequence[i]) + modMasses[i];
                running = i == 0 ? residue : running + residue;
                bMasses[i] = running;
            }
            double peptideMass = bMasses[length - 1] + AlphabaseMasses.H2O;
            bool maskCharge2 = precursor.Charge < 2;
            var mz = new double[(length - 1) * COLUMN_COUNT];
            for (int row = 0; row < length - 1; row++)
            {
                double b = bMasses[row];
                double y = peptideMass - b;
                int offset = row * COLUMN_COUNT;
                mz[offset + B_Z1] = b / 1 + AlphabaseMasses.PROTON;
                mz[offset + B_Z2] = maskCharge2 ? 0 : b / 2 + AlphabaseMasses.PROTON;
                mz[offset + Y_Z1] = y / 1 + AlphabaseMasses.PROTON;
                mz[offset + Y_Z2] = maskCharge2 ? 0 : y / 2 + AlphabaseMasses.PROTON;
            }
            return mz;
        }

        /// <summary>The float32 values alphabase stores (<c>PEAK_MZ_DTYPE</c>).</summary>
        public static float[] ToFloat32(double[] mz)
        {
            var result = new float[mz.Length];
            for (int i = 0; i < mz.Length; i++)
                result[i] = (float)mz[i];
            return result;
        }
    }
}
