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

#include "Diff.hpp"
//#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::proteome;
using namespace pwiz::data;


ostream* os_ = 0;


// BUG: Protein doesn't have a default ctor, should it?
/*void testProtein()
{
    if (os_) *os_ << "testProtein()\n";

    Protein a("420", 0, "", ""), b("420", 0, "", "");

    Diff<Protein> diff(a, b);
    if (diff) cout << diff;
    unit_assert(!diff);

    b.id = "421";
    unit_assert(diff);
}*/


void testProteinList()
{
    if (os_) *os_ << "testProteinList()\n";

    ProteinListSimple aSimple, bSimple;

    ProteinPtr protein1a = ProteinPtr(new Protein("420", 0, "", ""));
    ProteinPtr protein1b = ProteinPtr(new Protein("420", 0, "", ""));
   
    aSimple.proteins.push_back(protein1a); 
    bSimple.proteins.push_back(protein1b); 
    
    ProteinList& a = aSimple;
    ProteinList& b = bSimple;
    
    Diff<ProteinList, DiffConfig, ProteinListSimple> diff(a, b);

    DiffConfig config_ignore;
    config_ignore.ignoreMetadata = true;

    Diff<ProteinList, DiffConfig, ProteinListSimple> diffIgnore(a, b, config_ignore);
    unit_assert(!diff);
    unit_assert(!diffIgnore);

    // check: different ProteinList::size()
    
    ProteinPtr protein2 = ProteinPtr(new Protein("421", 0, "", ""));
    aSimple.proteins.push_back(protein2);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.proteins.size() == 1);

    diffIgnore(a, b);
    if (os_) *os_ << diffIgnore << endl;
    unit_assert(diffIgnore);
    unit_assert(diffIgnore.a_b.proteins.size() == 1);

    // check: same ProteinList::size(), different last id

    ProteinPtr protein3 = ProteinPtr(new Protein("422", 0, "", ""));
    bSimple.proteins.push_back(protein3);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.proteins.size() == 1);
    unit_assert(diff.a_b.proteins[0]->id == "421");
    unit_assert(diff.b_a.proteins.size() == 1);
    unit_assert(diff.b_a.proteins[0]->id == "422");

    // id is not ignored
    diffIgnore(a, b);
    unit_assert(diffIgnore);

    // check: ids match, different description
   
    bSimple.proteins.back() = ProteinPtr(new Protein("421", 0, "different metadata", ""));

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.proteins.size() == 1);
    unit_assert(diff.a_b.proteins[0]->description == "");
    unit_assert(diff.b_a.proteins.size() == 1);
    unit_assert(diff.b_a.proteins[0]->description == "different metadata");


    diffIgnore(a, b);
    unit_assert(!diffIgnore);

    // check: same metadata, different sequences

    bSimple.proteins.back() = ProteinPtr(new Protein("421", 0, "", "ELVISLIVES"));

    diff(a, b);
    unit_assert(diff);

    diffIgnore(a, b);
    unit_assert(diffIgnore);
}


void testProteomeData()
{
    if (os_) *os_ << "testProteomeData()\n";

    ProteomeData a, b;
   
    a.id = "goober";  
    b.id = "goober";  

    Diff<ProteomeData, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.id = "raisinet";        

    shared_ptr<ProteinListSimple> proteinList1(new ProteinListSimple);
    proteinList1->proteins.push_back(ProteinPtr(new Protein("p1", 0, "", "")));
    b.proteinListPtr = proteinList1;

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.proteinListPtr.get());
    unit_assert(diff.a_b.proteinListPtr->size() == 1);
}


void test()
{
    //testProtein();
    testProteinList();
    testProteomeData();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_ProteomeData")

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

