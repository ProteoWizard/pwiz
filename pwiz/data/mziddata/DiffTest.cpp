//
// DiffTest.cpp
//
//
// Original author: Robert Burke <robetr.burke@proteowizard.org>
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

#include "Diff.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>


using namespace std;
using namespace pwiz::util;
using namespace pwiz;
using namespace pwiz::mziddata;
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

void testIdentifiableType()
{
    IdentifiableType a, b;
    a.id="a";
    a.name="a_name";
    b = a;

    Diff<IdentifiableType> diff(a, b);
    if (diff && os_) *os_ << diff << endl;
    unit_assert(!diff);

    b.id="b";
    
    diff(a, b);
    if (os_) *os_ << diff << endl;
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


void testAnalysisProtocolCollection()
{
    if (os_) *os_ << "testAnalysisProtocolCollection()\n";
    
}


void testAnalysisCollection()
{
    if (os_) *os_ << "testAnalysisCollection()\n";
    
}

void testSequenceCollection()
{
    if (os_) *os_ << "testSequenceCollection()\n";
    
}


void testAnalysisSampleCollection()
{
    if (os_) *os_ << "testAnalysisSampleCollection()\n";
}


void testContact()
{
    if (os_) *os_ << "testContact()\n";
    
}


void testPerson()
{
    if (os_) *os_ << "testPerson()\n";
    
}


void testOrganization()
{
    if (os_) *os_ << "testOrganization()\n";
    
}


void testProvider()
{
    if (os_) *os_ << "testProvider()\n";
    
}


void testContactRole()
{
    if (os_) *os_ << "testContactRole()\n";
    
}

void testAnalysisSoftware()
{
    if (os_) *os_ << "testAnalysisSoftware()\n";

    AnalysisSoftware a, b;

    Diff<AnalysisSoftware> diff(a,b);
    unit_assert(!diff);

    // a.version
    a.version="version";
    // b.contactRole
    // a.softwareName
    // b.URI
    b.URI="URI";
    // a.customizations
    a.customizations="customizations";

    diff(a, b);
}


void testDataCollection()
{
    if (os_) *os_ << "testDataCollection()\n";

    DataCollection a, b;
    Diff<DataCollection> diff(a, b);
    unit_assert(!diff);

    // a.inputs
    // b.analysisData

    diff(a, b);
}


void testMzIdentML()
{
    if (os_) *os_ << "testMzIdentML()\n";

    MzIdentML a, b;

    examples::initializeTiny(a);
    examples::initializeTiny(b);


    Diff<MzIdentML> diff(a, b);
    unit_assert(!diff);

    b.version = "version";
    a.cvs.push_back(CV());
    b.analysisSoftwareList.push_back(AnalysisSoftwarePtr(new AnalysisSoftware));
    a.auditCollection.push_back(ContactPtr(new Contact()));
    b.bibliographicReference.push_back(BibliographicReferencePtr(new BibliographicReference));
    // a.analysisSampleCollection
    // b.sequenceCollection
    // a.analysisCollection
    // b.analysisProtocolCollection
    // a.dataCollection
    // b.bibliographicReference

    diff(a, b);
    if (os_) *os_ << diff << endl;

    unit_assert(diff);

    unit_assert(diff.a_b.version.empty());
    unit_assert(diff.b_a.version == "version");

    unit_assert(diff.a_b.cvs.size() == 1);
    unit_assert(diff.b_a.cvs.empty());
}

void test()
{
    testString();
    testIdentifiableType();
    testParamContainer();
    
    testAnalysisProtocolCollection();
    
    testDataCollection();
    testMzIdentML();
}

int main(int argc, char* argv[])
{
    try
    {
        //if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        // TODO debug - remove
        os_ = &cout;
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

