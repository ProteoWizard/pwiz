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


void testPeak()
{
    // instantiate a Peak

    if (os_) *os_ << "testPeak() ... " <<endl;
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

    unit_assert(peak == peakIn); 
    // write out the xml if -v and if test is passed
    if (os_) *os_ << "Testing Peak ... " << oss.str() << endl;
}

void testTheRest()
{

  // instantiante a PeakFamily
  if (os_) *os_ << "testPeakFamily() ... " << endl;
  PeakFamily jetsons;

  jetsons.mzMonoisotopic = 329.86;
  jetsons.charge = 3;
  jetsons.score = 0.11235811;

  Peak peak;
  Peak a;
  Peak boo;

  peak.mz = 329.86;
  a.mz = 109.87;
  boo.mz = 6.022141730;

  jetsons.peaks.push_back(peak);
  jetsons.peaks.push_back(a);
  jetsons.peaks.push_back(boo);

  // write out XML to a stream                                                                          

  ostringstream oss;
  XMLWriter writer(oss);


  jetsons.write(writer);
  if (os_) *os_  << oss.str() << endl;

  // instantiate new PeakFamily

  PeakFamily flintstones;

  // read from stream into new PeakFamily                                                                     
  istringstream iss(oss.str());
  flintstones.read(iss);

  // verify that new PeakFamily is the same as old PeakFamily                                                       
  if (os_)
    {
      *os_ << flintstones << endl;
      XMLWriter osWriter(*os_);
      flintstones.write(osWriter);
    }

  unit_assert(flintstones == jetsons);
  if (os_) *os_  << "Testing PeakFamily ... " << endl << oss.str() <<endl;

  // instantiate a new Scan

  Scan scan;
  
  scan.index = 12;
  scan.nativeID = "24";
  scan.scanNumber = 24;
  scan.retentionTime = 12.345;
  scan.observationDuration = 6.78;
  
  scan.calibrationParameters.A = 987.654;
  scan.calibrationParameters.B = 321.012;

  scan.peakFamilies.push_back(flintstones);
  scan.peakFamilies.push_back(jetsons);
  
  // write out XML to a stream                                          
  ostringstream oss_scan;
  XMLWriter writer_scan(oss_scan);
  scan.write(writer_scan);

   // instantiate a second Scan
  Scan scan2;

  // read it back in
  istringstream iss_scan(oss_scan.str());
   scan2.read(iss_scan);
  
 

  // assert that the two Scans are equal

  unit_assert(scan == scan2);
  if (os_) *os_  << "Testing Scan ... " << endl << oss_scan.str() << endl;
 
  // instantiate a new Software
  
  Software software;
  software.name = "World of Warcraft";
  software.version = "Wrath of the Lich King";
  software.source = "Blizzard Entertainment";
  
  Software::Parameter parameter1("Burke ping","level 70");
  Software::Parameter parameter2("Kate ping", "level 0");
  
  software.parameters.push_back(parameter1);
  software.parameters.push_back(parameter2);

  // write out XML to a stream
  ostringstream oss_soft;
  XMLWriter writer_soft(oss_soft);
  software.write(writer_soft);

  // instantiate another Software
  Software software2;
 
  // read it back in
  istringstream iss_soft(oss_soft.str());
  software2.read(iss_soft);

  // assert that the two Softwares are equal
   
  unit_assert(software == software2);
  if (os_) *os_  << "Testing Software ... " << endl << oss_soft.str() <<endl;

  // instantiate a PeakData

  PeakData pd;
  
  pd.software = software;
  pd.scans.push_back(scan);
  pd.scans.push_back(scan);
  
  ostringstream oss_pd;
  XMLWriter writer_pd(oss_pd);
  pd.write(writer_pd);
  
  // instantiate another PeakData

  PeakData pd2;

  // read into it

  istringstream iss_pd(oss_pd.str());
  pd2.read(iss_pd);

  // assert that the two PeakData are equal

  unit_assert(pd == pd2);
  if (os_) *os_  << "Testing PeakData ... " << endl << oss_pd.str()<<endl;

}

void test()
{
 
  testPeak();
  testTheRest();
 
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


