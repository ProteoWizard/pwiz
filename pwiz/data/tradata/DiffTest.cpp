//
// DiffTest.cpp
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

#include "Diff.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>


using namespace std;
using namespace pwiz::util;
using namespace pwiz;
using namespace pwiz::tradata;
using boost::shared_ptr;


ostream* os_ = 0;


void testString()
{
    if (os_) *os_ << "testString()\n";

    Diff<string> diff("goober", "goober");
    unit_assert(diff.a_b.empty() && diff.b_a.empty());
    unit_assert(!diff);

    diff("goober", "goo");
    unit_assert(diff);
    if (os_) *os_ << diff << endl;
}


void testCV()
{
    if (os_) *os_ << "testCV()\n";

    CV a, b;
    a.URI = "uri";
    a.id = "cvLabel";
    a.fullName = "fullName";
    a.version = "version";
    b = a;

    Diff<CV> diff;
    diff(a,b);

    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());
    unit_assert(!diff);

    a.version = "version_changed";

    diff(a,b); 
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.URI.empty() && diff.b_a.URI.empty());
    unit_assert(diff.a_b.id.empty() && diff.b_a.id.empty());
    unit_assert(diff.a_b.fullName.empty() && diff.b_a.fullName.empty());
    unit_assert(diff.a_b.version == "version_changed");
    unit_assert(diff.b_a.version == "version");
}


void testUserParam()
{
    if (os_) *os_ << "testUserParam()\n";

    UserParam a, b;
    a.name = "name";
    a.value = "value";
    a.type = "type";
    a.units = UO_minute;
    b = a;

    Diff<UserParam> diff(a, b);
    unit_assert(!diff);
    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());

    b.value = "value_changed";
    a.units = UO_second;
    unit_assert(diff(a,b));
    if (os_) *os_ << diff << endl;
    unit_assert(diff.a_b.name == "name");
    unit_assert(diff.b_a.name == "name");
    unit_assert(diff.a_b.value == "value");
    unit_assert(diff.b_a.value == "value_changed");
    unit_assert(diff.a_b.type.empty() && diff.b_a.type.empty());
    unit_assert(diff.a_b.units == UO_second);
    unit_assert(diff.b_a.units == UO_minute);
}


void testCVParam()
{
    if (os_) *os_ << "testCVParam()\n";

    CVParam a, b;
    a.cvid = MS_ionization_type; 
    a.value = "420";
    b = a;

    Diff<CVParam> diff(a, b);
    unit_assert(!diff);
    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());

    b.value = "value_changed";
    diff(a,b);
    unit_assert(diff);
    if (os_) *os_ << diff << endl;
    unit_assert(diff.a_b.cvid == MS_ionization_type);
    unit_assert(diff.b_a.cvid == MS_ionization_type);
    unit_assert(diff.a_b.value == "420");
    unit_assert(diff.b_a.value == "value_changed");
}


void testParamContainer()
{
    if (os_) *os_ << "testParamContainer()\n";

    ParamContainer a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
   
    Diff<ParamContainer> diff(a, b);
    unit_assert(!diff);

    a.userParams.push_back(UserParam("different", "1"));
    b.userParams.push_back(UserParam("different", "2"));
    a.cvParams.push_back(MS_charge_state);
    b.cvParams.push_back(MS_intensity);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.userParams.size() == 1);
    unit_assert(diff.a_b.userParams[0] == UserParam("different","1"));
    unit_assert(diff.b_a.userParams.size() == 1);
    unit_assert(diff.b_a.userParams[0] == UserParam("different","2"));

    unit_assert(diff.a_b.cvParams.size() == 1);
    unit_assert(diff.a_b.cvParams[0] == MS_charge_state); 
    unit_assert(diff.b_a.cvParams.size() == 1);
    unit_assert(diff.b_a.cvParams[0] == MS_intensity);
}


void testContact()
{
    if (os_) *os_ << "testContact()\n";

    Contact a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);

    a.id = b.id = "foo";
   
    Diff<Contact> diff(a, b);
    unit_assert(!diff);

    a.id = "bar";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testInstrument()
{
    if (os_) *os_ << "testInstrument()\n";

    Instrument a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);

    a.id = b.id = "foo";
   
    Diff<Instrument> diff(a, b);
    unit_assert(!diff);

    a.id = "bar";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testConfiguration()
{
    if (os_) *os_ << "testConfiguration()\n";

    Configuration a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
    a.instrumentPtr = InstrumentPtr(new Instrument("common"));
    b.instrumentPtr = InstrumentPtr(new Instrument("common"));
    a.contactPtr = ContactPtr(new Contact("common"));
    b.contactPtr = ContactPtr(new Contact("common"));
   
    Diff<Configuration> diff(a, b);
    unit_assert(!diff);

    a.instrumentPtr->id = "different";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testPrediction()
{
    if (os_) *os_ << "testPrediction()\n";

    Prediction a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
    a.intensityRank = b.intensityRank = 1;
    a.transitionSource = b.transitionSource = "common";
    a.contactPtr = ContactPtr(new Contact("common"));
    b.contactPtr = ContactPtr(new Contact("common"));

    Diff<Prediction> diff(a, b);
    unit_assert(!diff);

    a.softwarePtr = SoftwarePtr(new Software("different"));

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testValidation()
{
    if (os_) *os_ << "testValidation()\n";

    Validation a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
    a.intensityRank = b.intensityRank = 1;
    a.transitionSource = b.transitionSource = "common";
   
    Diff<Validation> diff(a, b);
    unit_assert(!diff);

    a.transitionSource = "different";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testEvidence()
{
    if (os_) *os_ << "testEvidence()\n";

    Evidence a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
   
    Diff<Evidence> diff(a, b);
    unit_assert(!diff);

    a.set(MS_intensity, 42);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testRetentionTime()
{
    if (os_) *os_ << "testRetentionTime()\n";

    RetentionTime a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
    a.localRetentionTime = b.localRetentionTime = 123;
   
    Diff<RetentionTime> diff(a, b);
    unit_assert(!diff);

    a.localRetentionTime = 321;

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testProtein()
{
    if (os_) *os_ << "testProtein()\n";

    Protein a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
    a.sequence = b.sequence = "ABCD";
    a.id = b.id = "foo";
   
    Diff<Protein> diff(a, b);
    unit_assert(!diff);

    a.sequence = "DCBA";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testPeptide()
{
    if (os_) *os_ << "testPeptide()\n";

    Peptide a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
    a.evidence.set(MS_intensity, 42);
    b.evidence.set(MS_intensity, 42);
    a.retentionTime.localRetentionTime = 123;
    b.retentionTime.localRetentionTime = 123;
    a.id = b.id = "foo";
   
    Diff<Peptide> diff(a, b);
    unit_assert(!diff);

    a.retentionTime.normalizationStandard = "different";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testCompound()
{
    if (os_) *os_ << "testCompound()\n";

    Compound a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
    a.retentionTime.localRetentionTime = 123;
    b.retentionTime.localRetentionTime = 123;
    a.id = b.id = "foo";
   
    Diff<Compound> diff(a, b);
    unit_assert(!diff);

    b.retentionTime.predictedRetentionTime = 321;

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testTransition()
{
    if (os_) *os_ << "testTransition()\n";

    Transition a, b;
    a.name = b.name = "T1";
    a.precursor.mz = b.precursor.mz = 123.45;
    a.product.mz = b.product.mz = 456.78;
    Validation v; v.intensityRank = 1;
    Configuration c; c.validations.push_back(v);
    a.configurationList.push_back(c);
    b.configurationList.push_back(c);
    a.peptidePtr = PeptidePtr(new Peptide("common"));
    b.peptidePtr = PeptidePtr(new Peptide("common"));
   
    Diff<Transition> diff(a, b);
    unit_assert(!diff);

    b.peptidePtr->modifiedSequence = "different";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testSoftware()
{
    if (os_) *os_ << "testSoftware()\n";

    Software a, b;

    a.id = "msdata";
    a.version = "4.20";
    a.set(MS_ionization_type);
    b = a;

    Diff<Software> diff(a, b);
    unit_assert(!diff);

    b.version = "4.21";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


/*void testPrecursor()
{
    if (os_) *os_ << "testPrecursor()\n";

    Precursor a, b;

    a.mz = 420;
    a.charge = 2;
    b = a;

    Diff<Precursor> diff(a, b);
    unit_assert(!diff);

    b.charge = 3;
    
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.mz == 0);
    unit_assert(diff.a_b.charge == -1);
    unit_assert(diff.b_a.mz == 0);
    unit_assert(diff.b_a.charge == 1);
}


void testProduct()
{
    if (os_) *os_ << "testProduct()\n";

    Product a, b;

    a.mz = 420;
    a.charge = 2;
    b = a;

    Diff<Product> diff(a, b);
    unit_assert(!diff);

    b.charge = 3;
    
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.mz == 0);
    unit_assert(diff.a_b.charge == -1);
    unit_assert(diff.b_a.mz == 0);
    unit_assert(diff.b_a.charge == 1);
}*/


void testTraData()
{
    if (os_) *os_ << "testTraData()\n";

    TraData a, b;

    Diff<TraData> diff(a, b);
    unit_assert(!diff);

    b.version = "version";
    a.cvs.push_back(CV());
    b.softwarePtrs.push_back(SoftwarePtr(new Software("software")));

    Publication pub;
    pub.id = "PUBMED1";
    pub.set(UO_dalton, 123);
    a.publications.push_back(pub);
    b.publications.push_back(pub);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.version.empty());
    unit_assert(diff.b_a.version == "version");

    unit_assert(diff.a_b.cvs.size() == 1);
    unit_assert(diff.b_a.cvs.empty());

    unit_assert(diff.a_b.softwarePtrs.empty());
    unit_assert(!diff.b_a.softwarePtrs.empty());

    unit_assert(diff.a_b.publications.empty());
    unit_assert(diff.b_a.publications.empty());
}


void test()
{
    testString();
    testCV();
    testUserParam();
    testCVParam();
    testParamContainer();
    testContact();
    testInstrument();
    testSoftware();
    testConfiguration();
    testPrediction();
    testValidation();
    testEvidence();
    testRetentionTime();
    testProtein();
    testPeptide();
    testCompound();
    testTransition();
    //testPrecursor();
    //testProduct();
    testTraData();
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

