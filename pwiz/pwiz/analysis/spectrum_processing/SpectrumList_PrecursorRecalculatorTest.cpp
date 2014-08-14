//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#include "SpectrumList_PrecursorRecalculator.hpp"
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


void verifyPrecursorInfo(const Spectrum& spectrum, double precursorMZ, int precursorCharge)
{
    unit_assert(!spectrum.precursors.empty()); 
    const Precursor& precursor = spectrum.precursors[0];
    unit_assert(!precursor.selectedIons.empty());
    const SelectedIon& selectedIon = precursor.selectedIons[0];

    const double epsilon = 1e-2;
    if (os_)
    {
        *os_ << "[verifyPrecursorInfo] " << spectrum.index << " " << spectrum.id << " "
             << precursorMZ << " " << precursorCharge << ": "
             << selectedIon.cvParam(MS_selected_ion_m_z).value << " " << selectedIon.cvParam(MS_charge_state).value << endl;
    }

    unit_assert_equal(selectedIon.cvParam(MS_selected_ion_m_z).valueAs<double>(), precursorMZ, epsilon);

    if (precursorCharge != 0)
        unit_assert(selectedIon.cvParam(MS_charge_state).valueAs<int>() == precursorCharge);
}


void test5peptideFT(const bfs::path& datadir)
{
    MSDataFile msd((datadir / "5peptideFT.mzML").string());
    
    unit_assert(msd.run.spectrumListPtr.get() && msd.run.spectrumListPtr->size()==7);
    if (os_) *os_ << "original spectra:\n";
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(2), 810.79, 0);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(3), 837.34, 0);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(4), 725.36, 0);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(5), 558.87, 0);
    verifyPrecursorInfo(*msd.run.spectrumListPtr->spectrum(6), 812.33, 0);

    shared_ptr<SpectrumList_PrecursorRecalculator> spectrumListRecalculated(
        new SpectrumList_PrecursorRecalculator(msd));

    unit_assert(spectrumListRecalculated->size() == 7); 
    if (os_) *os_ << "recalculated spectra:\n";
    verifyPrecursorInfo(*spectrumListRecalculated->spectrum(2), 810.42, 2);
    verifyPrecursorInfo(*spectrumListRecalculated->spectrum(3), 836.96, 2);
    verifyPrecursorInfo(*spectrumListRecalculated->spectrum(4), 724.91, 2);
    verifyPrecursorInfo(*spectrumListRecalculated->spectrum(5), 558.31, 3);
    verifyPrecursorInfo(*spectrumListRecalculated->spectrum(6), 810.42, 2);
}


void test(const bfs::path& datadir)
{
    test5peptideFT(datadir);
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


