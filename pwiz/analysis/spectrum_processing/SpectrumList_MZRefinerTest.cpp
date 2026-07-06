//
// $Id$
//
//
// Original author: Bryson Gibbons <bryson.gibbons@pnnl.gov>
//
// Copyright 2014 Pacific Northwest National Laboratory
//                Richland, WA 99352
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


#include "SpectrumList_MZRefiner.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/identdata/IdentDataFile.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/filesystem/path.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::analysis;
namespace bfs = boost::filesystem;


ostream* os_ = 0;

// Check scan metadata and a small sample of the m/z data for high-res scans.
void verifyScanInfo(const Spectrum& spectrum, const double& epsilon, double basePeakMZ, double lowestObservedMZ, double highestObservedMZ, int mzArrayIndex1, double mzArrayValue1, int mzArrayIndex2, double mzArrayValue2)
{
    unit_assert(spectrum.hasBinaryData());
    const BinaryDataArrayPtr binaryData = spectrum.getMZArray();

    if (os_)
    {
        // Use max double precision so we can capture cpp's exact refined values for the C# port
        // to match against. (Default ostream precision is 6 sig figs, way too coarse.)
        std::streamsize oldPrec = os_->precision();
        os_->precision(17);
        *os_ << "[verifyScanInfo] " << spectrum.index << " " << spectrum.id << " "
             << basePeakMZ << " " << lowestObservedMZ << " " << highestObservedMZ << " "
             << mzArrayValue1 << " " << mzArrayValue2 << ": "
             << spectrum.cvParam(MS_base_peak_m_z).valueAs<double>() << " "
             << spectrum.cvParam(MS_lowest_observed_m_z).valueAs<double>() << " "
             << spectrum.cvParam(MS_highest_observed_m_z).valueAs<double>() << " "
             << binaryData->data[mzArrayIndex1] << " "
             << binaryData->data[mzArrayIndex2] << endl;
        os_->precision(oldPrec);
    }

    unit_assert_equal(spectrum.cvParam(MS_base_peak_m_z).valueAs<double>(), basePeakMZ, epsilon);
    unit_assert_equal(spectrum.cvParam(MS_lowest_observed_m_z).valueAs<double>(), lowestObservedMZ, epsilon);
    unit_assert_equal(spectrum.cvParam(MS_highest_observed_m_z).valueAs<double>(), highestObservedMZ, epsilon);
    unit_assert_equal(binaryData->data[mzArrayIndex1], mzArrayValue1, epsilon);
    unit_assert_equal(binaryData->data[mzArrayIndex2], mzArrayValue2, epsilon);
}

// Check scan precursor metadata for MS/MS scans
void verifyPrecursorInfo(const Spectrum& spectrum, const double& epsilon, double precursorMZ, double isolationWindowTarget)
{
    unit_assert(!spectrum.precursors.empty()); 
    const Precursor& precursor = spectrum.precursors[0];
    unit_assert(!precursor.selectedIons.empty());
    const SelectedIon& selectedIon = precursor.selectedIons[0];
    unit_assert(!precursor.isolationWindow.empty());
    const IsolationWindow& isoWindow = precursor.isolationWindow;

    if (os_)
    {
        std::streamsize oldPrec = os_->precision();
        os_->precision(17);
        *os_ << "[verifyPrecursorInfo] " << spectrum.index << " " << spectrum.id << " "
            << precursorMZ << " " << isolationWindowTarget << ": "
            << selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>() << " "
            << isoWindow.cvParam(MS_isolation_window_target_m_z).valueAs<double>() << endl;
        os_->precision(oldPrec);
    }

    unit_assert_equal(selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>(), precursorMZ, epsilon);
    unit_assert_equal(isoWindow.cvParam(MS_isolation_window_target_m_z).valueAs<double>(), isolationWindowTarget, epsilon);
}

void testShift(const bfs::path& datadir)
{
    MSDataFile msd((datadir / "JD_06232014_sample4_C.mzML").string());
    
    unit_assert(msd.run.spectrumListPtr.get() && msd.run.spectrumListPtr->size()==610);
    if (os_) *os_ << "original spectra:\n";
    // Provided mzML file is high-res/high-res
    double epsilon = 1e-4;
    // MS1 scans 0, 224, 398 (0 and 224
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(0, true), epsilon, 371.09958, 300.14306, 1568.55126, 30, 303.64633, 1200, 416.24838);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(224, true), epsilon, 558.30688, 301.05908, 1522.72473, 200, 407.26425, 1500, 724.32824);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(10, true), epsilon, 530.32782, 74.06039, 887.42852, 41, 188.11117, 93, 442.22839);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(10), epsilon, 530.26684, 530.27);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(173, true), epsilon, 141.10162, 87.05542, 1187.53137, 63, 248.15817, 116, 887.44793);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(173), epsilon, 629.30160, 629.3);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(346, true), epsilon, 848.45895, 116.00368, 1454.73327, 16, 185.16418, 95, 862.43109);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(346), epsilon, 840.45480, 840.45);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(470, true), epsilon, 249.15857, 119.04895, 1402.77331, 23, 217.08113, 102, 1154.59863);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(470), epsilon, 838.96706, 838.97);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(551, true), epsilon, 1048.55047, 155.08105, 1321.67761, 50, 368.19134, 104, 941.96954);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(551), epsilon, 739.69935, 740.03);

    shared_ptr<SpectrumList_MZRefiner> spectrumListMZRefined(
        new SpectrumList_MZRefiner(msd, (datadir / "JD_06232014_sample4_C.mzid").string(), "specEValue", "-1e-10", IntegerSet(1, 2)));

    unit_assert(spectrumListMZRefined->size() == 610);
    if (os_) *os_ << "refined spectra:\n";
    // Gold-standard refined values come from running cpp's m/z-dependent shift on this fixture
    // — captured via the verbose output of the test itself with precision(17). Use 1e-5 Da to
    // absorb any acceptable floating-point reordering in the median / smoothing / interpolation
    // chain. The pwiz-sharp port must match these values to within the same tolerance.
    // Note: call spectrumListMZRefined->spectrum(...) — the original test called
    // msd.run.spectrumListPtr->spectrum(...) which is the un-refined source list, so the
    // refinement was never actually exercised by the verify* assertions. Caught during the
    // pwiz-sharp port.
    epsilon = 1e-5;
    verifyScanInfo(*spectrumListMZRefined->spectrum(0, true), epsilon, 371.1006001117666, 300.14388782681374, 1568.5563126956004, 30, 303.64716279528164, 1200, 416.24952163046731);
    verifyScanInfo(*spectrumListMZRefined->spectrum(224, true), epsilon, 558.30841403178533, 301.0599059587318, 1522.7296272693961, 200, 407.26536629488913, 1500, 724.33010710071767);
    verifyScanInfo(*spectrumListMZRefined->spectrum(10, true), epsilon, 530.33027936737869, 74.060736453505228, 887.43262784019601, 41, 188.11204462947168, 93, 442.23043669390097);
    verifyPrecursorInfo(*spectrumListMZRefined->spectrum(10, true), epsilon, 530.26830278971977, 530.27145671623236);
    verifyScanInfo(*spectrumListMZRefined->spectrum(173, true), epsilon, 141.1021310944723, 87.055741585444437, 1187.5356558403723, 63, 248.15906778591972, 116, 887.45113829379954);
    verifyPrecursorInfo(*spectrumListMZRefined->spectrum(173, true), epsilon, 629.30333335222315, 629.30172822487316);
    verifyScanInfo(*spectrumListMZRefined->spectrum(346, true), epsilon, 848.46212730691445, 116.00411836876587, 1454.738711017955, 16, 185.16487631408029, 95, 862.43431321316427);
    verifyPrecursorInfo(*spectrumListMZRefined->spectrum(346, true), epsilon, 840.45738089107431, 840.45257911504211);
    verifyScanInfo(*spectrumListMZRefined->spectrum(470, true), epsilon, 249.15954203160186, 119.04941028782407, 1402.7787367752446, 23, 217.0819699422467, 102, 1154.6030950289528);
    verifyPrecursorInfo(*spectrumListMZRefined->spectrum(470, true), epsilon, 838.96963622848989, 838.97257622429117);
    verifyScanInfo(*spectrumListMZRefined->spectrum(551, true), epsilon, 1048.5544004193118, 155.08163481495836, 1321.6825564384221, 50, 368.19272254561912, 104, 941.97306717821186);
    verifyPrecursorInfo(*spectrumListMZRefined->spectrum(551, true), epsilon, 739.70141234258222, 740.03206232796811);

    // For this fixture cpp's selection logic picks the m/z-binned shift (% improvement over
    // global > 3% AND > scan-time-binned's % improvement). The C# port must reproduce this.
    string chosenShift;
    BOOST_FOREACH(const ProcessingMethod& pm, spectrumListMZRefined->dataProcessingPtr()->processingMethods)
        BOOST_FOREACH(const UserParam& up, pm.userParams)
            if (up.name == "Shift dependency") chosenShift = up.value;
    unit_assert(chosenShift == "Using mass to charge dependency");

    // Verify which shift type was chosen via the ProcessingMethod's "Shift dependency"
    // UserParam. Print it during -v runs so the chosen path is visible in test output.
    auto dp = spectrumListMZRefined->dataProcessingPtr();
    unit_assert(dp.get() && !dp->processingMethods.empty());
    string shiftDependency;
    string globalMedianPpm;
    string shiftRange;
    BOOST_FOREACH(const ProcessingMethod& pm, dp->processingMethods)
    {
        BOOST_FOREACH(const UserParam& up, pm.userParams)
        {
            if (up.name == "Shift dependency") shiftDependency = up.value;
            if (up.name == "Global Median Mass Measurement Error (PPM)") globalMedianPpm = up.value;
            if (up.name == "Shift range") shiftRange = up.value;
        }
    }
    if (os_)
        *os_ << "[chosen shift] " << shiftDependency
             << " | globalMedianPpm=" << globalMedianPpm
             << " | shiftRange=" << shiftRange << endl;
    unit_assert(!shiftDependency.empty());

    bfs::remove(datadir / "JD_06232014_sample4_C.mzRefinement.tsv");
}


void test(const bfs::path& datadir)
{
    testShift(datadir);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        bfs::path datadir = ".";

        // grab the parent directory for the test files.
        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) 
                os_ = &cout;
            else
                // hack to allow running unit test from a different directory:
                // Jamfile passes full path to specified input file.
                // we want the path, so we can ignore filename
                datadir = bfs::path(argv[i]).parent_path(); 
        }   

        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test(datadir);
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


