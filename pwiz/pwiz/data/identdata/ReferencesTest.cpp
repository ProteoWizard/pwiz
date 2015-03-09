//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#define PWIZ_SOURCE

#include "References.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "TextWriter.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::identdata;


ostream* os_ = 0;

IdentData createMzid()
{
    IdentData mzid;
    ContactPtr c1 = ContactPtr(new Contact("c1", "larry"));
    PersonPtr p1  = PersonPtr(new Person("p1", "mo"));
    PersonPtr p2  = PersonPtr(new Person("p2", "curly"));
    OrganizationPtr o1 = OrganizationPtr(new Organization("o1", "three stooges"));
    mzid.auditCollection.push_back(c1);
    mzid.auditCollection.push_back(p1);
    mzid.auditCollection.push_back(p2);
    mzid.auditCollection.push_back(o1);

    return mzid;
}


void testContactRole()
{
    IdentData mzid;
    ContactPtr c1 = ContactPtr(new Contact("c1", "larry"));
    PersonPtr p1  = PersonPtr(new Person("p1", "mo"));
    PersonPtr p2  = PersonPtr(new Person("p2", "curly"));
    OrganizationPtr o1 = OrganizationPtr(new Organization("o1", "three stooges"));
    mzid.auditCollection.push_back(c1);
    mzid.auditCollection.push_back(p1);
    mzid.auditCollection.push_back(p2);
    mzid.auditCollection.push_back(o1);

    mzid.provider.contactRolePtr.reset(new ContactRole(MS_role_type, ContactPtr(new Contact("c1"))));

    AnalysisSoftwarePtr software(new AnalysisSoftware);
    software->contactRolePtr.reset(new ContactRole(MS_role_type, ContactPtr(new Person("p2"))));
    mzid.analysisSoftwareList.push_back(software);

    SamplePtr sample(new Sample);
    sample->contactRole.push_back(ContactRolePtr(new ContactRole(MS_role_type, ContactPtr(new Person("p1")))));
    sample->contactRole.push_back(ContactRolePtr(new ContactRole(MS_role_type, ContactPtr(new Organization("o1")))));
    mzid.analysisSampleCollection.samples.push_back(sample);

    References::resolve(mzid);

    unit_assert(mzid.provider.contactRolePtr->contactPtr->name == "larry");
    unit_assert(software->contactRolePtr->contactPtr->name == "curly");
    unit_assert(sample->contactRole.front()->contactPtr->name == "mo");
    unit_assert(dynamic_cast<Person*>(sample->contactRole.front()->contactPtr.get()));
    unit_assert(sample->contactRole.back()->contactPtr->name == "three stooges");
    unit_assert(dynamic_cast<Organization*>(sample->contactRole.back()->contactPtr.get()));
}


void testAnalysisSampleCollection()
{
    IdentData mzid;
    
    SamplePtr sample(new Sample("s1", "Sample No. 1"));
    sample->subSamples.push_back(SamplePtr(new Sample("s2")));

    mzid.analysisSampleCollection.samples.push_back(sample);

    unit_assert(mzid.analysisSampleCollection.samples.at(0)->id == "s1");
    unit_assert(mzid.analysisSampleCollection.samples.at(0)->subSamples.at(0)->name.empty());

    sample = SamplePtr(new Sample("s2", "Sample No. 2"));
    sample->subSamples.push_back(SamplePtr(new Sample("s1")));

    mzid.analysisSampleCollection.samples.push_back(sample);

    References::resolve(mzid);
    
    unit_assert(mzid.analysisSampleCollection.samples.size() == 2);
    unit_assert(mzid.analysisSampleCollection.samples.at(0)->id == "s1");
    unit_assert(mzid.analysisSampleCollection.samples.at(1)->id == "s2");
    unit_assert(mzid.analysisSampleCollection.samples.at(0)->subSamples.at(0)->name == "Sample No. 2");
    unit_assert(mzid.analysisSampleCollection.samples.at(1)->subSamples.at(0)->name == "Sample No. 1");
}

void testContacts()
{
    ContactPtr cont(new Contact("c1", "contact1"));

    PersonPtr peep1(new Person("p1", "person1"));
    peep1->affiliations.push_back(OrganizationPtr(new Organization("o1")));
    PersonPtr peep2(new Person("p2", "person2"));
    peep2->affiliations.push_back(OrganizationPtr(new Organization("o2")));
    peep2->affiliations.push_back(OrganizationPtr(new Organization("O")));
    
    OrganizationPtr mail_organ(new Organization("o1", "organ1"));
    OrganizationPtr feemail_organ(new Organization("o2", "organ2"));
    OrganizationPtr big_Organ(new Organization("O", "Organ"));
    big_Organ->parent = OrganizationPtr(new Organization("o1"));

    IdentData mzid;

    mzid.auditCollection.push_back(cont);
    mzid.auditCollection.push_back(peep1);
    mzid.auditCollection.push_back(peep2);
    mzid.auditCollection.push_back(mail_organ);
    mzid.auditCollection.push_back(feemail_organ);
    mzid.auditCollection.push_back(big_Organ);

    References::resolve(mzid);

    Person* tp = (Person*)mzid.auditCollection.at(1).get();
    unit_assert(tp->affiliations.at(0) == mail_organ);
    tp = (Person*)mzid.auditCollection.at(2).get();
    unit_assert(tp->affiliations.at(0) == feemail_organ);
    unit_assert(tp->affiliations.at(1) == big_Organ);
    
    Organization* to = (Organization*)mzid.auditCollection.at(5).get();
    unit_assert(to->parent == mail_organ);    
}


void testDBSequence()
{
    IdentData mzid;
    
    SearchDatabasePtr sd(new SearchDatabase("sd1", "searching"));
    mzid.dataCollection.inputs.searchDatabase.push_back(sd);

    sd = SearchDatabasePtr(new SearchDatabase("sd2", "everywhere"));
    mzid.dataCollection.inputs.searchDatabase.push_back(sd);

    sd = SearchDatabasePtr(new SearchDatabase("sd3", "for"));
    mzid.dataCollection.inputs.searchDatabase.push_back(sd);

    sd = SearchDatabasePtr(new SearchDatabase("sd4", "SearchDatabase"));
    mzid.dataCollection.inputs.searchDatabase.push_back(sd);

    DBSequencePtr dbs(new DBSequence("dbs1", "db pointers"));
    dbs->searchDatabasePtr = SearchDatabasePtr(new SearchDatabase("sd2"));
    mzid.sequenceCollection.dbSequences.push_back(dbs);

    dbs = DBSequencePtr(new DBSequence("dbs2", "closing sequence"));
    dbs->searchDatabasePtr = SearchDatabasePtr(new SearchDatabase("sd3"));
    mzid.sequenceCollection.dbSequences.push_back(dbs);

    References::resolve(mzid);

    DBSequencePtr& dps1 = mzid.sequenceCollection.dbSequences.at(0);
    unit_assert(dps1->searchDatabasePtr->name == "everywhere");
    
    dps1 = mzid.sequenceCollection.dbSequences.at(1);
    unit_assert(dps1->searchDatabasePtr->name == "for");
}


void testMeasure()
{
    IdentData mzid;
    SpectrumIdentificationListPtr sil(new SpectrumIdentificationList);
    SpectrumIdentificationResultPtr sir(new SpectrumIdentificationResult("SIR_1"));
    SpectrumIdentificationItemPtr sii(new SpectrumIdentificationItem("SII_1"));

    mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(sil);
    sil->spectrumIdentificationResult.push_back(sir);
    sir->spectrumIdentificationItem.push_back(sii);

    MeasurePtr measureMz(new Measure("M_MZ", "m/z measure"));
    sil->fragmentationTable.push_back(measureMz);

    IonTypePtr it(new IonType);
    sii->fragmentation.push_back(it);

    FragmentArrayPtr fa(new FragmentArray);
    fa->measurePtr.reset(new Measure("M_MZ"));
    it->fragmentArray.push_back(fa);

    References::resolve(mzid);

    unit_assert_operator_equal("m/z measure", it->fragmentArray.back()->measurePtr->name);
}


void test()
{
    testContactRole();
    testContacts();
    testAnalysisSampleCollection();
    testDBSequence();
    testMeasure();
}

int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_IdentData")

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

