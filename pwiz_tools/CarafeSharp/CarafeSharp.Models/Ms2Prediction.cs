/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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

using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Predicted relative fragment intensities for one precursor, normalized to the most
    /// intense ion with values below 1e-4 set to 0. Stored row-major as <c>[nAA - 1, 8]</c>:
    /// row r holds b(r+1) and y(nAA-1-r), columns follow
    /// <see cref="PeptdeepConstants.CHARGED_FRAG_TYPES"/>.
    /// </summary>
    public sealed class Ms2Prediction
    {
        public Ms2Prediction(PrecursorForm precursor, float[] intensities)
        {
            Precursor = precursor;
            Intensities = intensities;
        }

        public PrecursorForm Precursor { get; }

        public float[] Intensities { get; }

        public int RowCount
        {
            get { return Precursor.Peptide.Length - 1; }
        }

        public float Get(int row, int fragTypeColumn)
        {
            return Intensities[row * PeptdeepConstants.CHARGED_FRAG_TYPES.Length + fragTypeColumn];
        }
    }
}
