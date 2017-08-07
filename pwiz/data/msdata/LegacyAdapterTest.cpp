//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#include "LegacyAdapter.hpp"
#include "pwiz/data/common/CVTranslator.hpp"
#include "TextWriter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/lambda/lambda.hpp"
#include "boost/lambda/bind.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace boost::lambda;


ostream* os_ = 0;


void testModelAndManufacturer()
{
    if (os_) *os_ << "testModelAndManufacturer()\n"; 

    InstrumentConfiguration instrumentConfiguration;
    CVTranslator cvTranslator;
    LegacyAdapter_Instrument adapter(instrumentConfiguration, cvTranslator);

    unit_assert(instrumentConfiguration.cvParams.empty() && instrumentConfiguration.userParams.empty());

    adapter.manufacturerAndModel("dummy", "LTQ-FT");
    if (os_) *os_ << "manufacturer: " << adapter.manufacturer() << endl 
                  << "model: " << adapter.model() << endl;
    unit_assert(instrumentConfiguration.cvParams.size() == 1);
    unit_assert(instrumentConfiguration.userParams.empty());
    unit_assert(adapter.manufacturer() == "Thermo Scientific");
    unit_assert(adapter.model() == "LTQ FT");

    adapter.manufacturerAndModel("doobie", "420");
    if (os_) *os_ << "manufacturer: " << adapter.manufacturer() << endl 
                  << "model: " << adapter.model() << endl;
    unit_assert(instrumentConfiguration.cvParams.empty());
    unit_assert(instrumentConfiguration.userParams.size() == 2);
    unit_assert(adapter.manufacturer() == "doobie");
    unit_assert(adapter.model() == "420");

    adapter.manufacturerAndModel("dummy", "LTQ-FT");
    if (os_) *os_ << "manufacturer: " << adapter.manufacturer() << endl 
                  << "model: " << adapter.model() << endl;
    unit_assert(instrumentConfiguration.cvParams.size() == 1);
    unit_assert(instrumentConfiguration.userParams.empty());
    unit_assert(adapter.manufacturer() == "Thermo Scientific");
    unit_assert(adapter.model() == "LTQ FT");
}


void testIonisation()
{
    InstrumentConfiguration instrumentConfiguration;
    CVTranslator cvTranslator;
    LegacyAdapter_Instrument adapterEmpty(instrumentConfiguration, cvTranslator);
    unit_assert(adapterEmpty.ionisation() == "Unknown"); // Empty component list is legal
    instrumentConfiguration.componentList.push_back(Component(ComponentType_Source, 2));
    LegacyAdapter_Instrument adapter(instrumentConfiguration, cvTranslator);
    unit_assert(adapter.ionisation() == "Unknown"); // Empty component list is legal

    adapter.ionisation(" esi\t");
    if (os_) *os_ << "ionisation: " << adapter.ionisation() << endl;
    unit_assert(instrumentConfiguration.componentList.source(0).cvParams.size() == 1);
    unit_assert(instrumentConfiguration.componentList.source(0).userParams.empty());
    unit_assert(adapter.ionisation() == "electrospray ionization");

    adapter.ionisation("goober");
    if (os_) *os_ << "ionisation: " << adapter.ionisation() << endl;
    unit_assert(instrumentConfiguration.componentList.source(0).cvParams.empty());
    unit_assert(instrumentConfiguration.componentList.source(0).userParams.size() == 1);
    unit_assert(adapter.ionisation() == "goober");

    adapter.ionisation(" Electrospray-Ionization");
    if (os_) *os_ << "ionisation: " << adapter.ionisation() << endl;
    unit_assert(instrumentConfiguration.componentList.source(0).cvParams.size() == 1);
    unit_assert(instrumentConfiguration.componentList.source(0).userParams.empty());
    unit_assert(adapter.ionisation() == "electrospray ionization");
}


void testAnalyzer()
{
    InstrumentConfiguration instrumentConfiguration;
    CVTranslator cvTranslator;
    LegacyAdapter_Instrument adapterEmpty(instrumentConfiguration, cvTranslator);
    unit_assert(adapterEmpty.analyzer() == "Unknown"); // Empty component list is legal
    instrumentConfiguration.componentList.push_back(Component(ComponentType_Analyzer, 2));
    LegacyAdapter_Instrument adapter(instrumentConfiguration, cvTranslator);
    unit_assert(adapter.analyzer() == "Unknown"); // Empty component list is legal

    adapter.analyzer("IT");
    if (os_) *os_ << "analyzer: " << adapter.analyzer() << endl;
    unit_assert(instrumentConfiguration.componentList.analyzer(0).cvParams.size() == 1);
    unit_assert(instrumentConfiguration.componentList.analyzer(0).userParams.empty());
    unit_assert(adapter.analyzer() == "ion trap");

    adapter.analyzer("goober");
    if (os_) *os_ << "analyzer: " << adapter.analyzer() << endl;
    unit_assert(instrumentConfiguration.componentList.analyzer(0).cvParams.empty());
    unit_assert(instrumentConfiguration.componentList.analyzer(0).userParams.size() == 1);
    unit_assert(adapter.analyzer() == "goober");

    adapter.analyzer(" qit");
    if (os_) *os_ << "analyzer: " << adapter.analyzer() << endl;
    unit_assert(instrumentConfiguration.componentList.analyzer(0).cvParams.size() == 1);
    unit_assert(instrumentConfiguration.componentList.analyzer(0).userParams.empty());
    unit_assert(adapter.analyzer() == "quadrupole ion trap");
}


void testDetector()
{
    InstrumentConfiguration instrumentConfiguration;
    CVTranslator cvTranslator;
    LegacyAdapter_Instrument adapterEmpty(instrumentConfiguration, cvTranslator);
    unit_assert(adapterEmpty.analyzer() == "Unknown"); // Empty component list is legal
    instrumentConfiguration.componentList.push_back(Component(ComponentType_Detector, 3));
    LegacyAdapter_Instrument adapter(instrumentConfiguration, cvTranslator);
    unit_assert(adapter.analyzer() == "Unknown"); // Empty component list is legal

    adapter.detector("emt");
    if (os_) *os_ << "detector: " << adapter.detector() << endl;
    unit_assert(instrumentConfiguration.componentList.detector(0).cvParams.size() == 1);
    unit_assert(instrumentConfiguration.componentList.detector(0).userParams.empty());
    unit_assert(adapter.detector() == "electron multiplier tube");

    adapter.detector("goober");
    if (os_) *os_ << "detector: " << adapter.detector() << endl;
    unit_assert(instrumentConfiguration.componentList.detector(0).cvParams.empty());
    unit_assert(instrumentConfiguration.componentList.detector(0).userParams.size() == 1);
    unit_assert(adapter.detector() == "goober");

    adapter.detector(" Electron   Multiplier ");
    if (os_) *os_ << "detector: " << adapter.detector() << endl;
    unit_assert(instrumentConfiguration.componentList.detector(0).cvParams.size() == 1);
    unit_assert(instrumentConfiguration.componentList.detector(0).userParams.empty());
    unit_assert(adapter.detector() == "electron multiplier");
}


void testInstrumentConfiguration()
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
    CVParam softwareParam = software->cvParamChild(MS_software);
    unit_assert(softwareParam.cvid == MS_Xcalibur); 
    unit_assert(adapter.name() == "Xcalibur");

    adapter.name("goober");
    if (os_) *os_ << "software name: " << adapter.name() << endl;
    softwareParam = software->cvParamChild(MS_software);
    unit_assert(softwareParam.cvid == CVID_Unknown); 
    unit_assert(adapter.name() == "goober");

    adapter.version("4.20");
    if (os_) *os_ << "software version: " << adapter.version() << endl;
    unit_assert(adapter.version() == "4.20");

    //adapter.type("acquisition");
    //if (os_) *os_ << "software type: " << adapter.type() << endl;
    //unit_assert(adapter.type() == "acquisition");

    adapter.type("analysis");
    if (os_) *os_ << "software type: " << adapter.type() << endl;
    unit_assert(adapter.type() == "analysis");
}


void test()
{
    testInstrumentConfiguration();
    testSoftware();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
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

