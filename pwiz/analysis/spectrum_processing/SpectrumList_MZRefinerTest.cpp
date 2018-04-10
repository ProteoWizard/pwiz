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
        *os_ << "[verifyScanInfo] " << spectrum.index << " " << spectrum.id << " "
             << basePeakMZ << " " << lowestObservedMZ << " " << highestObservedMZ << " "
             << mzArrayValue1 << " " << mzArrayValue2 << ": "
             << spectrum.cvParam(MS_base_peak_m_z).value << " " << spectrum.cvParam(MS_lowest_observed_m_z).value << " "
             << spectrum.cvParam(MS_highest_observed_m_z).value << " " << binaryData->data[mzArrayIndex1] << " "
             << binaryData->data[mzArrayIndex2] << endl;
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
        *os_ << "[verifyPrecursorInfo] " << spectrum.index << " " << spectrum.id << " "
            << precursorMZ << " " << isolationWindowTarget << ": "
            << selectedIon.cvParam(MS_selected_ion_m_z).value << " " << isoWindow.cvParam(MS_isolation_window_target_m_z).value << endl;
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
    epsilon = 1e-2; // Increase the tolerance a little bit for the refined results (add some forgiveness with algorithm updates...)
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(0, true), epsilon, 371.10060, 300.14388, 1568.55631, 30, 303.64715, 1200, 416.24951);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(224, true), epsilon, 558.30841, 301.05990, 1522.72962, 200, 407.26538, 1500, 724.33007);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(10, true), epsilon, 530.32928, 74.06059, 887.43126, 41, 188.11169, 93, 442.22961);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(10), epsilon, 530.26830, 530.27145);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(173, true), epsilon, 141.10200, 87.05566, 1187.53519, 63, 248.15885, 116, 887.45068);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(173), epsilon, 629.30333, 629.30172);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(346, true), epsilon, 848.46155, 116.00400, 1454.73795, 16, 185.16468, 95, 862.43371);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(346), epsilon, 840.45738, 840.45257);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(470, true), epsilon, 249.15926, 119.04927, 1402.77782, 23, 217.08172, 102, 1154.60229);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(470), epsilon, 838.96963, 838.97257);
    verifyScanInfo(*msd.run.spectrumListPtr->spectrum(551, true), epsilon, 1048.55384, 155.08147, 1321.68186, 50, 368.19235, 104, 941.97253);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(551), epsilon, 739.70141, 740.03206);

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
                datadir = bfs::path(argv[i]).branch_path(); 
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


