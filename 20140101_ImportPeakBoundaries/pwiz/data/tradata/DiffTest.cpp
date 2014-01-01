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

#include "Diff.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::tradata;


ostream* os_ = 0;


void testContact()
{
    if (os_) *os_ << "testContact()\n";

    Contact a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);

    a.id = b.id = "foo";
   
    Diff<Contact, DiffConfig> diff(a, b);
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
   
    Diff<Instrument, DiffConfig> diff(a, b);
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
   
    Diff<Configuration, DiffConfig> diff(a, b);
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
    a.contactPtr = ContactPtr(new Contact("common"));
    b.contactPtr = ContactPtr(new Contact("common"));

    Diff<Prediction, DiffConfig> diff(a, b);
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
   
    Diff<Validation, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.set(MS_peak_intensity, 42);

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
   
    Diff<Evidence, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.set(MS_peak_intensity, 42);

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
   
    Diff<RetentionTime, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.set(MS_peak_intensity, 42);

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
   
    Diff<Protein, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.sequence = "DCBA";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testModification()
{
    if (os_) *os_ << "testModification()\n";

    Modification a, b;
    a.location = b.location = 7;
    a.monoisotopicMassDelta = b.monoisotopicMassDelta = 42;
    a.averageMassDelta = b.averageMassDelta = 42;

    Diff<Modification, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.monoisotopicMassDelta = 84;

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
    a.evidence.set(MS_peak_intensity, 42);
    b.evidence.set(MS_peak_intensity, 42);
    a.sequence = b.sequence = "ABCD";
    a.id = b.id = "foo";
   
    Diff<Peptide, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.sequence = "DCBA";

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
    a.id = b.id = "foo";
   
    Diff<Compound, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.retentionTimes.push_back(RetentionTime());

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testTransition()
{
    if (os_) *os_ << "testTransition()\n";

    Transition a, b;
    a.id = b.id = "T1";
    a.precursor.set(MS_selected_ion_m_z, 123.45);
    b.precursor.set(MS_selected_ion_m_z, 123.45);
    a.product.set(MS_selected_ion_m_z, 456.78);
    b.product.set(MS_selected_ion_m_z, 456.78);
    Validation v; v.set(MS_peak_intensity, 42);
    Configuration c; c.validations.push_back(v);
    a.configurationList.push_back(c);
    b.configurationList.push_back(c);
    a.peptidePtr = PeptidePtr(new Peptide("common"));
    b.peptidePtr = PeptidePtr(new Peptide("common"));
   
    Diff<Transition, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.peptidePtr->sequence = "different";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}

void testTarget()
{
    if (os_) *os_ << "testTarget()\n";

    Target a, b;
    a.id = b.id = "T1";
    a.precursor.set(MS_selected_ion_m_z, 123.45);
    b.precursor.set(MS_selected_ion_m_z, 123.45);
    Validation v; v.set(MS_peak_intensity, 42);
    Configuration c; c.validations.push_back(v);
    a.configurationList.push_back(c);
    b.configurationList.push_back(c);
    a.peptidePtr = PeptidePtr(new Peptide("common"));
    b.peptidePtr = PeptidePtr(new Peptide("common"));
   
    Diff<Target, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.peptidePtr->sequence = "different";

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

    Diff<Software, DiffConfig> diff(a, b);
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

    Diff<TraData, DiffConfig> diff(a, b);
    unit_assert(!diff);

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

    unit_assert(diff.a_b.cvs.size() == 1);
    unit_assert(diff.b_a.cvs.empty());

    unit_assert(diff.a_b.softwarePtrs.empty());
    unit_assert(!diff.b_a.softwarePtrs.empty());

    unit_assert(diff.a_b.publications.empty());
    unit_assert(diff.b_a.publications.empty());
}


void test()
{
    testContact();
    testInstrument();
    testSoftware();
    testConfiguration();
    testPrediction();
    testValidation();
    testEvidence();
    testRetentionTime();
    testProtein();
    testModification();
    testPeptide();
    testCompound();
    testTransition();
    testTarget();
    //testPrecursor();
    //testProduct();
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

