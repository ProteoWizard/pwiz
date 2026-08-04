//
// $Id$
//
//
// Original author: Brian Pratt <bspratt .at. proteinms.net>
// AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
//
// Copyright 2026 University of Washington - Seattle, WA
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

#pragma once

#include <cstddef>

namespace BiblioSpec {

/**
 * Remembers whether one file's writer presents peaks in ascending m/z order, so that the question
 * is settled from a handful of spectra rather than re-asked for every spectrum in the file.
 * Reading the answer off the very first spectrum would be wrong: early scans can precede the
 * sample and carry almost no peaks, and a spectrum with two of them ascends half the time by
 * chance.
 * The two verdicts are not symmetric. One spectrum out of order proves the writer does not sort,
 * however few peaks it holds, and every spectrum from then on is sorted. Peaks found in order
 * prove nothing on their own, so that verdict is only accepted from a spectrum with enough peaks
 * to mean it, and until such a spectrum arrives the checking continues.
 * Mirrors MsDataSpectrum's MzOrderVerdict on the Skyline side, which answers the same question
 * about the same files.
 */
class MzOrderVerdict
{
 public:
    MzOrderVerdict() : settled_(false), writerSortsByMz_(false) {}

    /**
     * Forget everything - a new file gets its own verdict.
     */
    void reset()
    {
        settled_ = false;
        writerSortsByMz_ = false;
    }

    /**
     * True while spectra still have to be examined, either because the file has not yet shown
     * enough to settle the question or because it settled it the wrong way and every spectrum
     * now needs sorting.
     */
    bool needsSpectrum() const
    {
        return !settled_ || !writerSortsByMz_;
    }

    /**
     * Take one spectrum's evidence into account.
     */
    void record(bool wasInOrder, size_t peakCount)
    {
        if (settled_)
            return; // A settled verdict does not change - one bad spectrum condemns the file
        if (!wasInOrder)
            settled_ = true;
        else if (peakCount > TOO_FEW_PEAKS_TO_MEAN_ANYTHING)
            settled_ = writerSortsByMz_ = true;
    }

 private:
    // Peaks needed before an ordered spectrum is taken as evidence about the writer, not coincidence
    static const size_t TOO_FEW_PEAKS_TO_MEAN_ANYTHING = 10;

    bool settled_;
    bool writerSortsByMz_;
};

} // namespace BiblioSpec
