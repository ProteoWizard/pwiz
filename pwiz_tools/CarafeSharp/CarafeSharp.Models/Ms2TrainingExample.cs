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

using System;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// One observed spectrum to fine-tune the MS2 model on: the precursor, the collision
    /// energy and instrument it was acquired with, and the matched intensity of every b and y
    /// ion at fragment charges 1 and 2, laid out like the network output
    /// (row r = b(r+1) and y(nAA-1-r); columns b_z1, b_z2, y_z1, y_z2). An ion with a positive
    /// <see cref="Invalid"/> count is masked out of the loss; a zero intensity with count 0
    /// teaches the model that the ion is absent.
    /// </summary>
    public sealed class Ms2TrainingExample
    {
        public const int FRAGMENT_TYPES = PeptdeepConstants.NUM_NON_MODLOSS_FRAG_TYPES;

        public Ms2TrainingExample(PrecursorForm precursor, double nce, string instrument,
            double[] intensities, double[] invalid)
        {
            int expected = (precursor.Peptide.Length - 1) * FRAGMENT_TYPES;
            if (intensities.Length != expected || invalid.Length != expected)
            {
                throw new ArgumentException(string.Format(@"{0} needs {1} fragment values, got {2} and {3}.",
                    precursor, expected, intensities.Length, invalid.Length));
            }
            Precursor = precursor;
            Nce = nce;
            Instrument = instrument;
            Intensities = intensities;
            Invalid = invalid;
        }

        public PrecursorForm Precursor { get; }

        public double Nce { get; }

        public string Instrument { get; }

        /// <summary>Observed intensities, row-major <c>[nAA - 1, 4]</c>.</summary>
        public double[] Intensities { get; }

        /// <summary>Masking counts, row-major <c>[nAA - 1, 4]</c>; positive means excluded.</summary>
        public double[] Invalid { get; }

        public string Sequence
        {
            get { return Precursor.Peptide.Sequence; }
        }
    }
}
