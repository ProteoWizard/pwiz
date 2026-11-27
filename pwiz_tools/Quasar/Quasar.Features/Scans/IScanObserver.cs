/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on skyline-glimpse (https://github.com/maccoss/skyline-glimpse)
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

using pwiz.Quasar.Features.Data;

namespace pwiz.Quasar.Features.Scans
{
    /// <summary>
    /// Something that wants to see every spectrum during the one sequential pass over a file: a heatmap
    /// accumulator, the assigned-signal sweep, the per-precursor XIC extractor, the envelope search. Reading
    /// a large run is the expensive part, so everything that needs the peaks rides along on the same pass.
    /// </summary>
    public interface IScanObserver
    {
        /// <summary>Called once before the first spectrum.</summary>
        /// <param name="spectrumCount">Number of spectra in the file.</param>
        /// <param name="rtFirst">Retention time of the first spectrum (minutes), NaN when unknown.</param>
        /// <param name="rtLast">Retention time of the last spectrum (minutes), NaN when unknown.</param>
        void Begin(int spectrumCount, double rtFirst, double rtLast);

        /// <summary>Called for each spectrum, in file order.</summary>
        void Observe(SpectrumData spectrum);

        /// <summary>Called once after the last spectrum (not called when the pass is cancelled).</summary>
        void End();
    }
}
