//
// LegacyAdapterTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "LegacyAdapter.hpp"
#include "CVTranslator.hpp"
#include "TextWriter.hpp"
#include "util/unit.hpp"
#include "boost/lambda/lambda.hpp"
#include "boost/lambda/bind.hpp"
#include <iostream>
#include <algorithm>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::msdata;
using namespace boost::lambda;


ostream* os_ = 0;


void testModelAndManufacturer()
{
    if (os_) *os_ << "testModelAndManufacturer()\n"; 

    Instrument instrument;
    CVTranslator cvTranslator;
    LegacyAdapter_Instrument adapter(instrument, cvTranslator);

    unit_assert(instrument.cvParams.empty() && instrument.userParams.empty());

    adapter.manufacturerAndModel("dummy", "LTQ-FT");
    if (os_) *os_ << "manufacturer: " << adapter.manufacturer() << endl 
                  << "model: " << adapter.model() << endl;
    unit_assert(instrument.cvParams.size() == 1);
    unit_assert(instrument.userParams.empty());
    unit_assert(adapter.manufacturer() == "Thermo Scientific");
    unit_assert(adapter.model() == "LTQ FT");

    adapter.manufacturerAndModel("doobie", "420");
    if (os_) *os_ << "manufacturer: " << adapter.manufacturer() << endl 
                  << "model: " << adapter.model() << endl;
    unit_assert(instrument.cvParams.empty());
    unit_assert(instrument.userParams.size() == 2);
    unit_assert(adapter.manufacturer() == "doobie");
    unit_assert(adapter.model() == "420");

    adapter.manufacturerAndModel("dummy", "LTQ-FT");
    if (os_) *os_ << "manufacturer: " << adapter.manufacturer() << endl 
                  << "model: " << adapter.model() << endl;
    unit_assert(instrument.cvParams.size() == 1);
    unit_assert(instrument.userParams.empty());
    unit_assert(adapter.manufacturer() == "Thermo Scientific");
    unit_assert(adapter.model() == "LTQ FT");
}


void testIonisation()
{
    Instrument instrument;
    CVTranslator cvTranslator;
    LegacyAdapter_Instrument adapter(instrument, cvTranslator);

    adapter.ionisation(" esi\t");
    if (os_) *os_ << "ionisation: " << adapter.ionisation() << endl;
    unit_assert(instrument.componentList.source.cvParams.size() == 1);
    unit_assert(instrument.componentList.source.userParams.empty());
    unit_assert(adapter.ionisation() == "electrospray ionization");

    adapter.ionisation("goober");
    if (os_) *os_ << "ionisation: " << adapter.ionisation() << endl;
    unit_assert(instrument.componentList.source.cvParams.empty());
    unit_assert(instrument.componentList.source.userParams.size() == 1);
    unit_assert(adapter.ionisation() == "goober");

    adapter.ionisation(" Electrospray-Ionization");
    if (os_) *os_ << "ionisation: " << adapter.ionisation() << endl;
    unit_assert(instrument.componentList.source.cvParams.size() == 1);
    unit_assert(instrument.componentList.source.userParams.empty());
    unit_assert(adapter.ionisation() == "electrospray ionization");
}


void testAnalyzer()
{
    Instrument instrument;
    CVTranslator cvTranslator;
    LegacyAdapter_Instrument adapter(instrument, cvTranslator);

    adapter.analyzer("IT");
    if (os_) *os_ << "analyzer: " << adapter.analyzer() << endl;
    unit_assert(instrument.componentList.analyzer.cvParams.size() == 1);
    unit_assert(instrument.componentList.analyzer.userParams.empty());
    unit_assert(adapter.analyzer() == "ion trap");

    adapter.analyzer("goober");
    if (os_) *os_ << "analyzer: " << adapter.analyzer() << endl;
    unit_assert(instrument.componentList.analyzer.cvParams.empty());
    unit_assert(instrument.componentList.analyzer.userParams.size() == 1);
    unit_assert(adapter.analyzer() == "goober");

    adapter.analyzer(" qit");
    if (os_) *os_ << "analyzer: " << adapter.analyzer() << endl;
    unit_assert(instrument.componentList.analyzer.cvParams.size() == 1);
    unit_assert(instrument.componentList.analyzer.userParams.empty());
    unit_assert(adapter.analyzer() == "quadrupole ion trap");
}


void testDetector()
{
    Instrument instrument;
    CVTranslator cvTranslator;
    LegacyAdapter_Instrument adapter(instrument, cvTranslator);

    adapter.detector("emt");
    if (os_) *os_ << "detector: " << adapter.detector() << endl;
    unit_assert(instrument.componentList.detector.cvParams.size() == 1);
    unit_assert(instrument.componentList.detector.userParams.empty());
    unit_assert(adapter.detector() == "electron multiplier tube");

    adapter.detector("goober");
    if (os_) *os_ << "detector: " << adapter.detector() << endl;
    unit_assert(instrument.componentList.detector.cvParams.empty());
    unit_assert(instrument.componentList.detector.userParams.size() == 1);
    unit_assert(adapter.detector() == "goober");

    adapter.detector(" Electron   Multiplier ");
    if (os_) *os_ << "detector: " << adapter.detector() << endl;
    unit_assert(instrument.componentList.detector.cvParams.size() == 1);
    unit_assert(instrument.componentList.detector.userParams.empty());
    unit_assert(adapter.detector() == "electron multiplier");
}


void testInstrument()
{
    testModelAndManufacturer();
    testIonisation();
    testAnalyzer();
    testDetector();
}


void testSoftware()
{
    SoftwarePtr software(new Software("abcd"));
    MSData msd;
    CVTranslator cvTranslator;
    LegacyAdapter_Software adapter(software, msd, cvTranslator);

    adapter.name(" XcaLibur  ");
    if (os_) *os_ << "software name: " << adapter.name() << endl;
    unit_assert(software->softwareParam.cvid == MS_Xcalibur); 
    unit_assert(adapter.name() == "Xcalibur");
    adapter.name("goober");
    if (os_) *os_ << "software name: " << adapter.name() << endl;
    unit_assert(software->softwareParam.cvid == CVID_Unknown); 
    unit_assert(adapter.name() == "goober");

    adapter.version("4.20");
    if (os_) *os_ << "software version: " << adapter.version() << endl;
    unit_assert(adapter.version() == "4.20");

    adapter.type("acquisition");
    if (os_) *os_ << "software type: " << adapter.type() << endl;
    unit_assert(adapter.type() == "acquisition");
    adapter.type("analysis");
    if (os_) *os_ << "software type: " << adapter.type() << endl;
    unit_assert(adapter.type() == "analysis");
}


void test()
{
    testInstrument();
    testSoftware();
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

