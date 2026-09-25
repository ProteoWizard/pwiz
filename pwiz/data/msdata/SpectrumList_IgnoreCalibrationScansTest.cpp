//
// $Id$
//
//
// Original author: Brian Pratt <bspratt at proteinms.net>
//
// Copyright 2026 University of Washington, Seattle, WA
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


#include "SpectrumList_IgnoreCalibrationScans.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"


using namespace pwiz::cv;
using namespace pwiz::util;
using namespace pwiz::msdata;


namespace {

// A lockspray scan sits in the middle, so a wrapper that forgot to remap would still line up at
// index 0 and only go wrong later.
void fillMSData(MSData& msd, bool declareInFileContent, bool tagSpectrum = true)
{
    SpectrumListSimplePtr sl(new SpectrumListSimple);
    const char* ids[] = {"scan=1", "scan=2", "scan=3", "scan=4"};
    for (size_t i = 0; i < 4; ++i)
    {
        SpectrumPtr s(new Spectrum);
        s->index = i;
        s->id = ids[i];
        s->set(MS_ms_level, 1);
        s->set(i == 1 && tagSpectrum ? MS_calibration_spectrum : MS_MS1_spectrum);
        s->defaultArrayLength = i; // something per-spectrum to prove the right one is fetched
        sl->spectra.push_back(s);
    }
    msd.run.spectrumListPtr = sl;

    msd.fileDescription.fileContent.set(MS_MS1_spectrum);
    if (declareInFileContent)
        msd.fileDescription.fileContent.set(MS_calibration_spectrum);
}

void testRemovesCalibrationSpectra()
{
    MSData msd;
    fillMSData(msd, true);
    SpectrumListPtr inner = msd.run.spectrumListPtr;
    SpectrumListPtr sl = SpectrumList_IgnoreCalibrationScans::create(inner, msd);

    unit_assert(sl != inner); // it wrapped
    unit_assert_operator_equal(3, sl->size());
    unit_assert(sl->calibrationSpectraAreOmitted());

    // The survivors keep their ids, and are renumbered so nothing sees a gap
    const char* expected[] = {"scan=1", "scan=3", "scan=4"};
    for (size_t i = 0; i < 3; ++i)
    {
        unit_assert_operator_equal(string(expected[i]), sl->spectrumIdentity(i).id);
        unit_assert_operator_equal(i, sl->spectrumIdentity(i).index);
        unit_assert_operator_equal(string(expected[i]), sl->spectrum(i)->id);
        unit_assert_operator_equal(i, sl->spectrum(i)->index);
    }

    // The mapping has to reach the right inner spectrum, not merely one with the right id
    unit_assert_operator_equal(0, sl->spectrum(0)->defaultArrayLength);
    unit_assert_operator_equal(2, sl->spectrum(1)->defaultArrayLength);
    unit_assert_operator_equal(3, sl->spectrum(2)->defaultArrayLength);

    // find() has to follow the renumbering too
    unit_assert_operator_equal(1, sl->find("scan=3"));
    unit_assert_operator_equal(3, sl->find("scan=2")); // gone, so "not found" is size()

    // Fetching through the wrapper must not renumber the inner list's own spectra. SpectrumListSimple
    // hands back the element itself, so assigning index in place would corrupt it - and every
    // assertion above would still pass, since none of them looks at inner afterwards.
    unit_assert_operator_equal(2, inner->spectrum(2)->index);
    unit_assert_operator_equal(2, inner->spectrumIdentity(2).index);
    unit_assert_operator_equal(string("scan=3"), inner->spectrumIdentity(2).id);
}


// A file may declare calibration spectra in fileContent and carry the term on no spectrum at all -
// msconvert with a spectrum-dropping filter writes exactly that (ProteoWizard #4499). Wrapping one
// would leave a list reporting, through calibrationSpectraAreOmitted, that it removed calibration
// spectra while every spectrum is still present. Consumers act on that: Skyline stands down from its
// own lockspray heuristic when a list says so, so it would stop filtering scans nothing had removed.
void testLeavesDeclaredButUntaggedFilesAlone()
{
    MSData msd;
    fillMSData(msd, true, false);
    SpectrumListPtr inner = msd.run.spectrumListPtr;
    SpectrumListPtr sl = SpectrumList_IgnoreCalibrationScans::create(inner, msd);

    unit_assert(sl == inner);
    unit_assert_operator_equal(4, sl->size());
    unit_assert(!sl->calibrationSpectraAreOmitted());
}

// Scanning every spectrum to find out there was nothing to remove is the cost this avoids, so a
// file that does not declare calibration spectra must come back untouched rather than merely equal.
void testLeavesUndeclaredFilesAlone()
{
    MSData msd;
    fillMSData(msd, false);
    SpectrumListPtr inner = msd.run.spectrumListPtr;
    SpectrumListPtr sl = SpectrumList_IgnoreCalibrationScans::create(inner, msd);

    unit_assert(sl == inner);
    unit_assert_operator_equal(4, sl->size());
    unit_assert(!sl->calibrationSpectraAreOmitted());
}

// Reveals "calibration spectrum" only at or above a chosen detail level, and records every level it
// is asked for. SpectrumListSimple ignores detail level altogether, so without a list like this the
// tests above pass whatever level create() picks - including the FullMetadata it used to hardcode.
class DetailLevelRecordingSpectrumList : public SpectrumListSimple
{
    public:

    DetailLevelRecordingSpectrumList(DetailLevel revealAt) : revealAt_(revealAt) {}

    mutable vector<DetailLevel> requested;

    SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const override
    {
        requested.push_back(detailLevel);

        SpectrumPtr s(new Spectrum(*spectra[index]));
        if ((int) detailLevel < (int) revealAt_)
        {
            // Below the revealing level nothing about the spectrum's type is populated yet, which is
            // what the predicate reads as "ask me again in more detail" rather than "not calibration"
            vector<CVParam>& cvParams = s->cvParams;
            cvParams.erase(std::remove_if(cvParams.begin(), cvParams.end(),
                                          [](const CVParam& cvParam)
                                          {
                                              return cvIsA(cvParam.cvid, MS_spectrum_type);
                                          }),
                           cvParams.end());
        }
        return s;
    }

    /// Deliberately not recorded: the wrapper asks by DetailLevel, and this overload is reached only
    /// by callers that are not choosing a level at all.
    SpectrumPtr spectrum(size_t index, bool getBinaryData) const override
    {
        return SpectrumPtr(new Spectrum(*spectra[index]));
    }

    /// SpectrumListSimple implements this as *spectrum(index, false), which would both charge this
    /// test for a read the wrapper did not ask for and return a reference to a temporary, since the
    /// spectrum() above hands back a copy rather than the stored element. Read the element directly.
    const SpectrumIdentity& spectrumIdentity(size_t index) const override
    {
        return *spectra[index];
    }

    private:

    DetailLevel revealAt_;
};

// The scan must cost no more of each spectrum than the question needs. Pins the resolved level
// rather than the wording of the call: reverting to a hardcoded DetailLevel_FullMetadata fails here
// while leaving every other assertion in this file green.
void testAsksForTheCheapestDetailLevelThatWorks()
{
    boost::shared_ptr<DetailLevelRecordingSpectrumList> sl(
        new DetailLevelRecordingSpectrumList(DetailLevel_FastMetadata));

    const char* ids[] = {"scan=1", "scan=2", "scan=3", "scan=4"};
    for (size_t i = 0; i < 4; ++i)
    {
        SpectrumPtr s(new Spectrum);
        s->index = i;
        s->id = ids[i];
        s->set(MS_ms_level, 1);
        s->set(i == 1 ? MS_calibration_spectrum : MS_MS1_spectrum);
        sl->spectra.push_back(s);
    }

    MSData msd;
    msd.run.spectrumListPtr = sl;
    msd.fileDescription.fileContent.set(MS_MS1_spectrum);
    msd.fileDescription.fileContent.set(MS_calibration_spectrum);

    SpectrumListPtr filtered = SpectrumList_IgnoreCalibrationScans::create(msd.run.spectrumListPtr, msd);

    // It still works: the tagged spectrum is gone
    unit_assert_operator_equal(3, filtered->size());
    unit_assert(filtered->calibrationSpectraAreOmitted());

    // ...and it got there without ever reading more than FastMetadata. InstantMetadata appears too,
    // since that is where min_level_accepted starts before escalating.
    unit_assert(!sl->requested.empty());
    for (DetailLevel detailLevel : sl->requested)
        unit_assert((int) detailLevel <= (int) DetailLevel_FastMetadata);
}

} // namespace


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        testRemovesCalibrationSpectra();
        testLeavesUndeclaredFilesAlone();
        testLeavesDeclaredButUntaggedFilesAlone();
        testAsksForTheCheapestDetailLevelThatWorks();
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
