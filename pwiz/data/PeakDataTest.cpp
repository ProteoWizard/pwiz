//
// PeakDataTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "PeakData.hpp"
#include "util/unit.hpp"
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::data;


ostream* os_ = 0;


void test()
{
    // create a PeakData object

    peakdata::PeakData pd;
    pd.sourceFilename = "none";
    pd.software.name = "PeakDataTest";
    pd.software.version = "1.0";
    pd.software.source = "Spielberg Family Center for Applied Proteomics";
    pd.scans.push_back(peakdata::Scan());

    peakdata::Scan& scan = pd.scans.front();
    scan.scanNumber = 666;
    scan.retentionTime = 1.234;
    scan.observationDuration = .987;
    scan.calibrationParameters = CalibrationParameters::thermo();
    scan.peakFamilies.push_back(peakdata::PeakFamily());

    peakdata::PeakFamily& peakFamily = scan.peakFamilies.front();
    peakFamily.mzMonoisotopic = 810.4148;
    peakFamily.charge = 2;
    peakFamily.peaks.resize(2);

    const CalibrationParameters& cp = scan.calibrationParameters;

    peakdata::Peak* peak = &peakFamily.peaks[0];
    peak->frequency = cp.frequency(peakFamily.mzMonoisotopic); 
    peak->intensity = 100;

    peak = &peakFamily.peaks[1];
    peak->frequency = cp.frequency(peakFamily.mzMonoisotopic + 1);
    peak->intensity = 101;

    if (os_) *os_ << "pd:\n" << pd << endl;

    // test io

    const char* filename = "PeakDataTest.temp.xml";
    ofstream os(filename);
    os << pd;
    os.close();

    ifstream is(filename);
    peakdata::PeakData pd2;
    is >> pd2;
    if (os_) *os_ << "pd2:\n" << pd2 << endl; 

    system((string("rm ") + filename).c_str());

    // verify xml is the same before and after io

    ostringstream oss;
    oss << pd;
    ostringstream oss2;
    oss2 << pd2;
    unit_assert(oss.str() == oss2.str()); 

    // misc operator<< checking

    if (os_)
    {
        *os_ << scan
             << "simple peak list:\n";

        scan.printSimple(*os_);
        *os_ << endl;

        // simple xml writing
        pd.writeXML(*os_);
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "PeakDataTest\n";
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
        return 1;
    }
    
    return 5;
}


