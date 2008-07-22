//
// SpectrumList_PrecursorRecalculatorTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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
#include "PrecursorRecalculatorDefault.hpp"
#include "analysis/peakdetect/PeakFamilyDetectorFT.hpp"
#include "data/msdata/MSDataFile.hpp"
#include "utility/misc/unit.hpp"
#include "boost/filesystem/path.hpp"
#include <iostream>


using namespace pwiz::msdata;
using namespace pwiz::data;
using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace std;
using boost::shared_ptr;
namespace bfs = boost::filesystem;


ostream* os_ = 0;


void verifyPrecursorInfo(const Spectrum& spectrum, double precursorMZ, int precursorCharge)
{
    unit_assert(!spectrum.spectrumDescription.precursors.empty()); 
    const Precursor& precursor = spectrum.spectrumDescription.precursors[0];
    unit_assert(!precursor.selectedIons.empty());
    const SelectedIon& selectedIon = precursor.selectedIons[0];

    const double epsilon = 1e-2;
    if (os_)
    {
        *os_ << "[verifyPrecursorInfo] " << spectrum.index << " " << spectrum.id << " "
             << precursorMZ << " " << precursorCharge << ": "
             << selectedIon.cvParam(MS_m_z).value << " " << selectedIon.cvParam(MS_charge_state).value << endl;
    }

    unit_assert_equal(selectedIon.cvParam(MS_m_z).valueAs<double>(), precursorMZ, epsilon);

    if (precursorCharge != 0)
        unit_assert(selectedIon.cvParam(MS_m_z).valueAs<int>() == precursorCharge);
}


shared_ptr<PrecursorRecalculatorDefault> createPrecursorRecalculator_msprefix()
{
    // instantiate PeakFamilyDetector

    PeakFamilyDetectorFT::Config pfdftConfig;
    pfdftConfig.cp = CalibrationParameters::thermo();
    shared_ptr<PeakFamilyDetector> pfd(new PeakFamilyDetectorFT(pfdftConfig));

    // instantiate PrecursorRecalculatorDefault

    PrecursorRecalculatorDefault::Config config;
    config.peakFamilyDetector = pfd;
    config.mzLeftWidth = 3;
    config.mzRightWidth = 1.6;
    return shared_ptr<PrecursorRecalculatorDefault>(new PrecursorRecalculatorDefault(config));
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

    shared_ptr<PrecursorRecalculatorDefault> prd = createPrecursorRecalculator_msprefix();

    shared_ptr<SpectrumList_PrecursorRecalculator> spectrumListRecalculated(
        new SpectrumList_PrecursorRecalculator(msd.run.spectrumListPtr, prd));

    unit_assert(spectrumListRecalculated->size() == 7); 
    if (os_) *os_ << "recalculated spectra:\n";
    verifyPrecursorInfo(*spectrumListRecalculated->spectrum(2), 810.79, 0); // TODO: fix
    verifyPrecursorInfo(*spectrumListRecalculated->spectrum(3), 837.34, 0); // TODO: fix
    verifyPrecursorInfo(*spectrumListRecalculated->spectrum(4), 725.36, 0); // TODO: fix
    verifyPrecursorInfo(*spectrumListRecalculated->spectrum(5), 558.87, 0); // TODO: fix
    verifyPrecursorInfo(*spectrumListRecalculated->spectrum(6), 812.33, 0); // TODO: fix
}


void test(const bfs::path& datadir)
{
    test5peptideFT(datadir);
}


int main(int argc, char* argv[])
{
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
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}


