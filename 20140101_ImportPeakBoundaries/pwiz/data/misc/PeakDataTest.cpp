//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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
#include "pwiz/utility/misc/Std.hpp"


using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::math;
using namespace pwiz::data::peakdata;


ostream* os_ = 0;

PeakFamily initializePeakFamily()
{
    PeakFamily peakFamily;

    peakFamily.mzMonoisotopic = 329.86;
    peakFamily.charge = 3;
    peakFamily.score = 0.11235811;

    Peak peak;
    Peak a;
    Peak boo;

    peak.mz = 329.86;
    a.mz = 109.87;
    boo.mz = 6.022141730;

    peakFamily.peaks.push_back(peak);
    peakFamily.peaks.push_back(a);
    peakFamily.peaks.push_back(boo);
 
    return peakFamily;

}

Scan initializeScan()
{
    Scan scan;
    scan.index = 12;
    scan.nativeID = "24";
    scan.scanNumber = 24;
    scan.retentionTime = 12.345;
    scan.observationDuration = 6.78;

    scan.calibrationParameters.A = 987.654;
    scan.calibrationParameters.B = 321.012;
    
    PeakFamily flintstones = initializePeakFamily();
    PeakFamily jetsons = initializePeakFamily();

    scan.peakFamilies.push_back(flintstones);
    scan.peakFamilies.push_back(jetsons);

    return scan;
}

Software initializeSoftware()
{
    Software software;
    software.name = "World of Warcraft";
    software.version = "Wrath of the Lich King";
    software.source = "Blizzard Entertainment";

    Software::Parameter parameter1("Burke ping","level 70");
    Software::Parameter parameter2("Kate ping", "level 0");

    software.parameters.push_back(parameter1);
    software.parameters.push_back(parameter2);

    return software;

}

PeakData initializePeakData()
{
    PeakData pd;

    Software software = initializeSoftware();
    pd.software = software;

    Scan scan = initializeScan();

    pd.scans.push_back(scan);
    pd.scans.push_back(scan);

    return pd;

}

PeakelPtr initializePeakel()
{
    PeakelPtr pkl(new Peakel);
    pkl->mz = 432.1;
    pkl->retentionTime = 1234.56;
    pkl->maxIntensity = 9876.54;
    pkl->totalIntensity = 32123.45;
    pkl->mzVariance = 6.023;

    PeakFamily peakFamily = initializePeakFamily();
    
    pkl->peaks = peakFamily.peaks;
    
    return pkl;
}


void testPeakEquality()
{
    if (os_) *os_ << "testPeakEquality()" <<endl;

    Peak peak;

    peak.id = 5;
    peak.mz = 1;
    peak.retentionTime = 1.5;
    peak.intensity = 2;
    peak.area = 3;
    peak.error = 4;

    Peak peak2 = peak;

    unit_assert(peak == peak2);
    peak.attributes[Peak::Attribute_Phase] = 4.20;
    unit_assert(peak != peak2);
    peak2.attributes[Peak::Attribute_Phase] = 4.20;
    peak2.attributes[Peak::Attribute_Decay] = 6.66;
    unit_assert(peak != peak2);
    peak.attributes[Peak::Attribute_Decay] = 6.66;
    unit_assert(peak == peak2);
}


void testPeak()
{
    if (os_) *os_ << "testPeak()" <<endl;

    // instantiate a Peak

    Peak peak;

    peak.id = 5;
    peak.mz = 1;
    peak.retentionTime = 1.5;
    peak.intensity = 2;
    peak.area = 3;
    peak.error = 4;

    peak.data.push_back(OrderedPair(1,2));
    peak.data.push_back(OrderedPair(3,4));

    peak.attributes[Peak::Attribute_Frequency] = 5;
    peak.attributes[Peak::Attribute_Phase] = 6;
    peak.attributes[Peak::Attribute_Decay] = 7;

    if (os_) *os_ << peak << endl;

    // write out XML to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    peak.write(writer);

    // allocate a new Peak

    Peak peakIn;
    unit_assert(peak != peakIn);

    // read from stream into new Peak

    istringstream iss(oss.str());
    peakIn.read(iss);
    if (os_) *os_ << peakIn << endl;

    // verify that new Peak is the same as old Peak

    unit_assert(peak == peakIn);
}


void testPeakFamily()
{
    // initialize a PeakFamily
    
    PeakFamily jetsons = initializePeakFamily();

    // write out XML to a stream                                                                                                                                                    

    ostringstream oss;
    XMLWriter writer(oss);


    jetsons.write(writer);

    // instantiate new PeakFamily

    PeakFamily flintstones;

    // read from stream into new PeakFamily                                                                                                                                   
    istringstream iss(oss.str());
    flintstones.read(iss);

    // verify that new PeakFamily is the same as old PeakFamily                                                                 

    unit_assert(flintstones == jetsons);
    if (os_) *os_    << "Testing PeakFamily ... " << endl << oss.str() <<endl;
}

void testScan()
{
    // initialize a new Scan

    Scan scan = initializeScan();

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
    if (os_) *os_    << "Testing Scan ... " << endl << oss_scan.str() << endl;

}

void testSoftware()
{
    // initialize a new Software
    
    Software software = initializeSoftware();

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
    if (os_) *os_    << "Testing Software ... " << endl << oss_soft.str() <<endl;

}

void testPeakData()
{
    // initialize a PeakData

    PeakData pd = initializePeakData();

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
    if (os_) *os_    << "Testing PeakData ... " << endl << oss_pd.str()<<endl;

}

void testPeakel()
{
    // initialize a peakel

    PeakelPtr dill = initializePeakel();

    // write it out
    ostringstream oss_pkl;
    XMLWriter writer_pkl(oss_pkl);
    dill->write(writer_pkl);

    // instantiate another Peakel

    Peakel gherkin;
    
    // read into it
    istringstream iss_pkl(oss_pkl.str());
    gherkin.read(iss_pkl);
    
    // assert that the two Peakels are equal

    unit_assert(*dill == gherkin);
    if (os_) *os_ << "Testing Peakel ... " << endl << oss_pkl.str() << endl;
}


void testPeakelAux()
{
    Peakel p;
    p.retentionTime = 420;
    unit_assert(p.retentionTimeMin() == 420);
    unit_assert(p.retentionTimeMax() == 420);

    p.peaks.resize(2);
    p.peaks[0].retentionTime = 666;
    p.peaks[1].retentionTime = 667;
    unit_assert(p.retentionTimeMin() == 666);
    unit_assert(p.retentionTimeMax() == 667);
}


void testPeakelConstruction()
{
    Peak peak(420, 666);
    Peakel peakel(Peak(420,666));
    unit_assert(peakel.mz == 420);
    unit_assert(peakel.retentionTime == 666);
    unit_assert(peakel.peaks.size() == 1);
    unit_assert(peakel.peaks[0] == peak);
}


void testFeature()
{
    //    initialize a new Feature
    
    Feature feature;
    feature.mz = 1863.0101;
    feature.retentionTime = 1492.1012;
    feature.charge = 3;
    feature.totalIntensity = 1776.0704;
    feature.rtVariance = 1969.0720;
    feature.score = 420.0;
    feature.error = 666.0;
    
    PeakelPtr stateFair = initializePeakel();
    PeakelPtr deli = initializePeakel();

    feature.peakels.push_back(stateFair);
    feature.peakels.push_back(deli);

    // write it out
    ostringstream oss_f;
    XMLWriter writer_f(oss_f);
    feature.write(writer_f);

    // instantiate another feature

    Feature feature2;

    // read into it

    istringstream iss(oss_f.str());
    feature2.read(iss);

    // assert that the two Features are equal

    if (os_) 
    {
        *os_ << "Testing Feature ... " << endl << oss_f.str() << endl;
        *os_ << "feature2:\n";
        XMLWriter writer(*os_);
        feature2.write(writer);
    }

    unit_assert(feature == feature2);
}


void testFeatureAux()
{
    Feature feature;
    feature.retentionTime = 420;
    unit_assert(feature.retentionTimeMin() == 420);
    unit_assert(feature.retentionTimeMax() == 420);

    // retention time ranges determined by first two peakels

    PeakelPtr dill(new Peakel);
    dill->peaks.push_back(Peak(666,419));
    dill->peaks.push_back(Peak(666,423));

    PeakelPtr sweet(new Peakel);
    sweet->peaks.push_back(Peak(666,421));
    sweet->peaks.push_back(Peak(666,424));

    PeakelPtr gherkin(new Peakel);
    gherkin->peaks.push_back(Peak(666,418));
    gherkin->peaks.push_back(Peak(666,425));

    feature.peakels.push_back(dill);
    feature.peakels.push_back(sweet);
    feature.peakels.push_back(gherkin);

    unit_assert(feature.retentionTimeMin() == 419);
    unit_assert(feature.retentionTimeMax() == 424);
}


void test()
{
    testPeakEquality();
    testPeak();

    testPeakFamily();
    testScan();
    testSoftware();
    testPeakData();
    testPeakel();
    testPeakelAux();
    testPeakelConstruction();
    testFeature();
    testFeatureAux();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "PeakDataTest\n";
         
	    test(); 
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


