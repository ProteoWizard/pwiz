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

#include "Spectrum.h"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace BiblioSpec;
using namespace pwiz::util;

namespace {

PEAK_T makePeak(double mz, double intensity)
{
    PEAK_T peak;
    peak.mz = mz;
    peak.intensity = (float) intensity;
    return peak;
}

// Ascending m/z is nowhere required of a writer, but it is what the peak processing assumes:
// binPeaks() merges a peak into the previous bin only when the two are adjacent in the array, so
// on a spectrum presented in some other order peaks landing in one bin never merge, and
// removePrecursorPeaks() then binary searches an unsorted vector and erases the wrong range.
// Neither failure reports anything - the intensities are simply wrong.
// The peaks here are ordered by ascending intensity, one shape this has taken in practice.
void testUnsortedPeaksAreSortedOnLoad()
{
    vector<PEAK_T> intensityOrdered;
    intensityOrdered.push_back(makePeak(500.5, 10));
    intensityOrdered.push_back(makePeak(300.3, 20));
    intensityOrdered.push_back(makePeak(700.7, 30));
    intensityOrdered.push_back(makePeak(200.2, 40));
    intensityOrdered.push_back(makePeak(600.6, 50));
    intensityOrdered.push_back(makePeak(400.4, 60));

    Spectrum spec;
    spec.setRawPeaks(intensityOrdered);

    const vector<PEAK_T>& loaded = spec.getRawPeaks();
    unit_assert_operator_equal(6, loaded.size());

    // Ascending m/z, and every intensity still paired with the m/z it arrived with. Sorting the
    // m/z values alone would leave each one plausible and each pairing wrong, which is a worse
    // failure than the unsorted input this exists to correct.
    unit_assert_equal(200.2, loaded[0].mz, 1e-9); unit_assert_equal(40, loaded[0].intensity, 1e-4);
    unit_assert_equal(300.3, loaded[1].mz, 1e-9); unit_assert_equal(20, loaded[1].intensity, 1e-4);
    unit_assert_equal(400.4, loaded[2].mz, 1e-9); unit_assert_equal(60, loaded[2].intensity, 1e-4);
    unit_assert_equal(500.5, loaded[3].mz, 1e-9); unit_assert_equal(10, loaded[3].intensity, 1e-4);
    unit_assert_equal(600.6, loaded[4].mz, 1e-9); unit_assert_equal(50, loaded[4].intensity, 1e-4);
    unit_assert_equal(700.7, loaded[5].mz, 1e-9); unit_assert_equal(30, loaded[5].intensity, 1e-4);
}

// Peaks that arrive in order must be left exactly as they are. This exercises the already-sorted
// fast path, where no sort runs at all - it does not say anything about how equal m/z values are
// ordered relative to each other when a sort IS needed, which testDuplicateMzValues covers.
void testSortedPeaksAreUntouched()
{
    vector<PEAK_T> mzOrdered;
    mzOrdered.push_back(makePeak(100.0, 5));
    mzOrdered.push_back(makePeak(200.0, 7));
    mzOrdered.push_back(makePeak(200.0, 9));
    mzOrdered.push_back(makePeak(300.0, 3));

    Spectrum spec;
    spec.setRawPeaks(mzOrdered);

    const vector<PEAK_T>& loaded = spec.getRawPeaks();
    unit_assert_operator_equal(4, loaded.size());
    for (size_t i = 0; i < loaded.size(); i++)
    {
        unit_assert_equal(mzOrdered[i].mz, loaded[i].mz, 1e-9);
        unit_assert_equal(mzOrdered[i].intensity, loaded[i].intensity, 1e-4);
    }
}

// Unsorted input that also contains repeated m/z values, so a sort really does run over them.
// std::sort is not stable, so the two intensities at 300.3 may come back in either order - what
// must hold is that both survive, still attached to 300.3, and that the m/z axis is ascending.
void testDuplicateMzValues()
{
    vector<PEAK_T> peaks;
    peaks.push_back(makePeak(400.4, 10));
    peaks.push_back(makePeak(300.3, 20));
    peaks.push_back(makePeak(200.2, 30));
    peaks.push_back(makePeak(300.3, 40));

    Spectrum spec;
    spec.setRawPeaks(peaks);

    const vector<PEAK_T>& loaded = spec.getRawPeaks();
    unit_assert_operator_equal(4, loaded.size());
    for (size_t i = 1; i < loaded.size(); i++)
        unit_assert(loaded[i - 1].mz <= loaded[i].mz);

    unit_assert_equal(200.2, loaded[0].mz, 1e-9);
    unit_assert_equal(30, loaded[0].intensity, 1e-4);
    unit_assert_equal(300.3, loaded[1].mz, 1e-9);
    unit_assert_equal(300.3, loaded[2].mz, 1e-9);
    unit_assert_equal(60, loaded[1].intensity + loaded[2].intensity, 1e-4); // 20 and 40, either order
    unit_assert_equal(400.4, loaded[3].mz, 1e-9);
    unit_assert_equal(10, loaded[3].intensity, 1e-4);
}

void testEmptyAndSinglePeak()
{
    Spectrum empty;
    empty.setRawPeaks(vector<PEAK_T>());
    unit_assert_operator_equal(0, empty.getRawPeaks().size());

    vector<PEAK_T> one;
    one.push_back(makePeak(123.4, 56));
    Spectrum single;
    single.setRawPeaks(one);
    unit_assert_operator_equal(1, single.getRawPeaks().size());
    unit_assert_equal(123.4, single.getRawPeaks()[0].mz, 1e-9);
}

} // namespace

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        testUnsortedPeaksAreSortedOnLoad();
        testSortedPeaksAreUntouched();
        testDuplicateMzValues();
        testEmptyAndSinglePeak();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
