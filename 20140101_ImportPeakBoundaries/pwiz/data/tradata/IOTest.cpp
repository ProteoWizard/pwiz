//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "IO.hpp"
#include "Diff.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::cv;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::tradata;
using boost::iostreams::stream_offset;


ostream* os_ = 0;


template <typename object_type>
void testObject(const object_type& a)
{
    if (os_) *os_ << "testObject(): " << typeid(a).name() << endl;

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream

    object_type b; 
    istringstream iss(oss.str());
    IO::read(iss, b);

    // compare 'a' and 'b'

    Diff<object_type> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);

    
}


void testCV()
{
    CV a;
    a.URI = "abcd";
    a.id = "efgh";
    a.fullName = "ijkl";
    a.version = "mnop";

    testObject(a);
}


void testUserParam()
{
    UserParam a;
    a.name = "abcd";
    a.value = "efgh";
    a.type = "ijkl";
    a.units = UO_minute;

    testObject(a);
}


void testCVParam()
{
    CVParam a(MS_selected_ion_m_z, "810.48", MS_m_z);
    testObject(a);

    CVParam b(UO_second, "123.45");
    testObject(b);
}


template <typename object_type>
void testNamedParamContainer()
{
    object_type a;
    a.userParams.push_back(UserParam("goober", "goo", "peanuts"));
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
    a.cvParams.push_back(CVParam(MS_selected_ion_m_z, "666", MS_m_z));
    testObject(a);
}


void testSoftware()
{
    Software a;
    a.id = "goober";
    a.set(MS_ionization_type);
    a.version = "4.20";
    testObject(a);
}


/*void testInstrument()
{
    Instrument a;
    a.id = "LCQ Deca";
    a.cvParams.push_back(MS_LCQ_Deca);
    a.cvParams.push_back(CVParam(MS_instrument_serial_number, 23433));
    testObject(a);
}

void testConfiguration()
{
    Configuration a;
    a.instrumentPtr = "LCA Deca";
    a.contactPtr = "Bob";
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
}*/


/*void testPrecursor()
{
    Precursor a;
    a.mz = 123.45;
    a.charge = 2;
    testObject(a);

    Precursor b;
    b.mz = 456.78;
    testObject(b);  
}


void testProduct()
{
    Product a;
    a.mz = 123.45;
    a.charge = 2;
    testObject(a);

    Product b;
    b.mz = 456.78;
    testObject(b); 
}*/


void testTraData()
{
    if (os_) *os_ << "testTraData():\n";

    TraData a;
    examples::initializeTiny(a);

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream

    TraData b;
    istringstream iss(oss.str());
    IO::read(iss, b);

    // compare 'a' and 'b'

    Diff<TraData, DiffConfig> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);
}


void test()
{
    testCV();
    testUserParam();
    testCVParam();
    //testNamedParamContainer<Contact>();
    //testNamedParamContainer<Publication>();
    /*testInstrument();
    testConfiguration();
    testPrecursor();
    testProduct();*/
    testTraData();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_TraData")

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

