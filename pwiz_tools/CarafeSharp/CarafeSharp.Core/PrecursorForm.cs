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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>A peptide form at one precursor charge, the unit the MS2 model predicts.</summary>
    public sealed class PrecursorForm
    {
        public PrecursorForm(PeptideForm peptide, int charge)
        {
            if (charge < 1)
                throw new ArgumentException(string.Format(@"Invalid precursor charge {0}.", charge), nameof(charge));
            Peptide = peptide;
            Charge = charge;
        }

        public PeptideForm Peptide { get; }

        public int Charge { get; }

        public override string ToString()
        {
            return Peptide + @"/" + Charge;
        }
    }
}
