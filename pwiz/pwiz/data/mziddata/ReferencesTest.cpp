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
#include <iostream>
#include <stdexcept>

using namespace std;
using namespace pwiz::util;
using namespace pwiz;
using namespace pwiz::mziddata;


ostream* os_ = 0;

MzIdentML createMzid()
{
    MzIdentML mzid;
    ContactPtr c1 = ContactPtr(new Contact("c1", "larry"));
    PersonPtr p1  = PersonPtr(new Person("p1", "mo"));
    OrganizationPtr o1 = OrganizationPtr(new Organization("o1", "curly"));
    mzid.auditCollection.push_back(c1);
    mzid.auditCollection.push_back(p1);
    mzid.auditCollection.push_back(o1);

    return mzid;
}


void testContactRole()
{
    ContactRole cr1;
    cr1.contactPtr = ContactPtr(new Contact("c1"));
    ContactRole cr2;
    cr2.contactPtr = ContactPtr(new Contact("p1"));
    ContactRole cr3;
    cr3.contactPtr = ContactPtr(new Contact("o1"));

    unit_assert(cr1.contactPtr->name.empty());
    unit_assert(cr2.contactPtr->name.empty());
    unit_assert(cr3.contactPtr->name.empty());
    
    MzIdentML mzid = createMzid();

    References::resolve(cr1, mzid);

    unit_assert(cr1.contactPtr->name == "larry");

    References::resolve(cr2, mzid);

    unit_assert(cr2.contactPtr->name == "mo");
    unit_assert(dynamic_cast<Person*>(cr2.contactPtr.get()));

    References::resolve(cr3, mzid);

    unit_assert(cr3.contactPtr->name == "curly");
    unit_assert(dynamic_cast<Organization*>(cr3.contactPtr.get()));
}


void testAnalysisSampleCollection()
{
    MzIdentML mzid;
    
    SamplePtr sample(new Sample("s1", "Sample No. 1"));
    sample->subSamples.push_back(Sample::subSample("s2"));

    mzid.analysisSampleCollection.samples.push_back(sample);

    unit_assert(mzid.analysisSampleCollection.samples.at(0)->id == "s1");
    unit_assert(mzid.analysisSampleCollection.samples.at(0)->subSamples.at(0).samplePtr->name.empty());

    sample = SamplePtr(new Sample("s2", "Sample No. 2"));
    sample->subSamples.push_back(Sample::subSample("s1"));

    mzid.analysisSampleCollection.samples.push_back(sample);

    References::resolve(mzid.analysisSampleCollection, mzid);
    
    unit_assert(mzid.analysisSampleCollection.samples.size() == 2);
    unit_assert(mzid.analysisSampleCollection.samples.at(0)->id == "s1");
    unit_assert(mzid.analysisSampleCollection.samples.at(1)->id == "s2");
    unit_assert(mzid.analysisSampleCollection.samples.at(0)->subSamples.at(0).samplePtr->name == "Sample No. 2");
    unit_assert(mzid.analysisSampleCollection.samples.at(1)->subSamples.at(0).samplePtr->name == "Sample No. 1");
}

void testContacts()
{
    ContactPtr cont(new Contact("c1", "contact1"));

    PersonPtr peep1(new Person("p1", "person1"));
    peep1->affiliations.push_back(Affiliations("o1"));
    PersonPtr peep2(new Person("p2", "person2"));
    peep2->affiliations.push_back(Affiliations("o2"));
    peep2->affiliations.push_back(Affiliations("O"));
    
    OrganizationPtr mail_organ(new Organization("o1", "organ1"));
    OrganizationPtr feemail_organ(new Organization("o2", "organ2"));
    OrganizationPtr big_Organ(new Organization("O", "Organ"));
    big_Organ->parent.organizationPtr = ContactPtr(new Contact("o1"));

    MzIdentML mzid;

    mzid.auditCollection.push_back(cont);
    mzid.auditCollection.push_back(peep1);
    mzid.auditCollection.push_back(peep2);
    mzid.auditCollection.push_back(mail_organ);
    mzid.auditCollection.push_back(feemail_organ);
    mzid.auditCollection.push_back(big_Organ);

    References::resolve(mzid.auditCollection, mzid);

    Person* tp = (Person*)mzid.auditCollection.at(1).get();
    unit_assert(tp->affiliations.at(0).organizationPtr->name == "organ1");
    tp = (Person*)mzid.auditCollection.at(2).get();
    unit_assert(tp->affiliations.at(0).organizationPtr->name == "organ2");
    unit_assert(tp->affiliations.at(1).organizationPtr->name == "Organ");
    
    Organization* to = (Organization*)mzid.auditCollection.at(5).get();
    unit_assert(to->parent.organizationPtr->name == "organ1");    
}


void testDBSequence()
{
    MzIdentML mzid;
    
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

    References::resolve(mzid.sequenceCollection, mzid);

    DBSequencePtr& dps1 = mzid.sequenceCollection.dbSequences.at(0);
    unit_assert(dps1->searchDatabasePtr->name == "everywhere");
    
    dps1 = mzid.sequenceCollection.dbSequences.at(1);
    unit_assert(dps1->searchDatabasePtr->name == "for");
}


void test()
{
    testContactRole();
    testContacts();
    testAnalysisSampleCollection();
    testDBSequence();
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

