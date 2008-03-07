//
// SpectrumInfoTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "SpectrumInfo.hpp"
#include "msdata/examples.hpp"
#include "util/unit.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::analysis;


ostream* os_ = 0;
const double epsilon_ = 1e-6;


void test()
{
    if (os_) *os_ << "test()\n"; 

    MSData tiny;
    examples::initializeTiny(tiny);

    SpectrumInfo info;
    info.update(*tiny.run.spectrumListPtr->spectrum(0));
    
    unit_assert(info.index == 0);
    unit_assert(info.id == "S19");
    unit_assert(info.nativeID == "19");
    unit_assert(info.scanNumber == 19);
    unit_assert(info.massAnalyzerType == MS_QIT);
    unit_assert(info.msLevel == 1);
    unit_assert_equal(info.retentionTime, 353.43, epsilon_);
    unit_assert_equal(info.mzLow, 400.39, epsilon_);
    unit_assert_equal(info.mzHigh, 1795.56, epsilon_);
    unit_assert(info.precursors.empty());
    unit_assert(info.data.size() == 15);

    info.update(*tiny.run.spectrumListPtr->spectrum(1));
    unit_assert(info.index == 1);
    unit_assert(info.id == "S20");
    unit_assert(info.nativeID == "20");
    unit_assert(info.scanNumber == 20);
    unit_assert(info.massAnalyzerType == MS_QIT);
    unit_assert(info.msLevel == 2);
    unit_assert_equal(info.retentionTime, 359.43, epsilon_);
    unit_assert_equal(info.mzLow, 320.39, epsilon_);
    unit_assert_equal(info.mzHigh, 1003.56, epsilon_);
    unit_assert(info.precursors.size() == 1);
    unit_assert(info.precursors[0].index == 0);
    unit_assert_equal(info.precursors[0].mz, 445.34, epsilon_);
    unit_assert_equal(info.precursors[0].intensity, 120053, epsilon_);
    unit_assert(info.precursors[0].charge == 2);
    unit_assert(info.data.size() == 10);

    if (os_) *os_ << "ok\n";
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}

