//
// PeakDataTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "PeakData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <boost/filesystem/operations.hpp>
#include <iostream>
#include <fstream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::data::peakdata;


ostream* os_ = 0;


/*
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
    is.close();

    boost::filesystem::remove(filename);

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
*/


void testPeak()
{
    // instantiate a Peak

    Peak peak;

    peak.mz = 1;
    peak.intensity = 2;
    peak.area = 3;
    peak.error = 4;
    peak.frequency = 5;
    peak.phase = 6;
    peak.decay = 7;

    if (os_) *os_ << peak << endl;

    // write out XML to a stream

    ostringstream oss;
    XMLWriter writer(oss);

    peak.write(writer);
    if (os_) *os_ << oss.str() << endl;
    
    // allocate a new Peak

    Peak peakIn;

    if (os_) *os_ << peakIn << endl; 
    unit_assert(peak != peakIn);

    // read from stream into new Peak

    istringstream iss(oss.str());
    peakIn.read(iss);

    // verify that new Peak is the same as old Peak

    if (os_) 
    {
        *os_ << peakIn << endl; 
        XMLWriter osWriter(*os_);
        peakIn.write(osWriter);
    }

    unit_assert(peak == peakIn);
}

void testPeakDataWriter()
{
  if (os_)
    {
      *os_ << "testPeakDataWriter(): " <<endl;
    }

  PeakData peakData;
  peakData.sourceFilename = "FunRun";

  ostringstream oss;
  XMLWriter writer(oss);
  peakData.write(writer);

  if (os_)
    {
      *os_ << "Writing PeakData with XML writer..." <<endl;
      XMLWriter writer(*os_);
      peakData.write(writer);
    }
  
  PeakData peakData2;
  istringstream iss(oss.str());
  peakData2.read(iss);

  unit_assert(peakData2.sourceFilename == peakData.sourceFilename);

}

void test()
{
    testPeak();
    testPeakDataWriter();
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
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}


