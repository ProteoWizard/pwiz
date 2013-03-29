//
// $Id$
//
//
// Original author: Chris Paulse <cpaulse@systemsbiology.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "SpectrumList_PrecursorRefine.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
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


void verifyPrecursorMZ(const Spectrum& spectrum, double precursorMZ)
{
    unit_assert(!spectrum.precursors.empty()); 
    const Precursor& precursor = spectrum.precursors[0];
    unit_assert(!precursor.selectedIons.empty());
    const SelectedIon& selectedIon = precursor.selectedIons[0];

    double foo = selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>();
    foo++; // quiet an "initalized but not used" warning

    const double epsilon = 1e-4;
    if (os_)
    {
        *os_ << "[verifyPrecursorMZ] " << spectrum.index << " " << spectrum.id << " "
             << precursorMZ << ": "
             << selectedIon.cvParam(MS_selected_ion_m_z).value << " " << selectedIon.cvParam(MS_charge_state).value << endl;
    }

    unit_assert_equal(selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>(), precursorMZ, epsilon);

}


void testPrecursorRefine(const bfs::path& datadir)
{
    MSDataFile msd((datadir / "PrecursorRefineOrbi.mzML").string());
    
    unit_assert(msd.run.spectrumListPtr.get() && msd.run.spectrumListPtr->size()==51);
    if (os_) *os_ << "original spectra:\n";
    verifyPrecursorMZ(*msd.run.spectrumListPtr->spectrum(21), 747.37225);
    verifyPrecursorMZ(*msd.run.spectrumListPtr->spectrum(22), 614.867065);
    verifyPrecursorMZ(*msd.run.spectrumListPtr->spectrum(24), 547.2510);
    verifyPrecursorMZ(*msd.run.spectrumListPtr->spectrum(25), 533.2534);
    verifyPrecursorMZ(*msd.run.spectrumListPtr->spectrum(26), 401.22787);

    shared_ptr<SpectrumList_PrecursorRefine> spectrumListRecalculated(
        new SpectrumList_PrecursorRefine(msd));

    unit_assert(spectrumListRecalculated->size() == 51); 
    if (os_) *os_ << "recalculated spectra:\n";
    verifyPrecursorMZ(*spectrumListRecalculated->spectrum(21), 747.37078);
    verifyPrecursorMZ(*spectrumListRecalculated->spectrum(22), 614.86648);
    verifyPrecursorMZ(*spectrumListRecalculated->spectrum(24), 547.2507);
    verifyPrecursorMZ(*spectrumListRecalculated->spectrum(25), 533.2534);
    verifyPrecursorMZ(*spectrumListRecalculated->spectrum(26), 401.226957);
}


void test(const bfs::path& datadir)
{
    testPrecursorRefine(datadir);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        bfs::path datadir = ".";

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


