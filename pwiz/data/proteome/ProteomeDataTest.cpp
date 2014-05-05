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


#include "ProteomeData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::proteome;


void testProteinListSimple()
{
    // fill in ProteinListSimple

    shared_ptr<ProteinListSimple> proteinListSimple(new ProteinListSimple);

    unit_assert(proteinListSimple->empty());

    Protein emptyProtein("", 0, "", "");
    unit_assert(emptyProtein.empty());

    ProteinPtr protein0(new Protein("IPI-1701", 0, "The final frontier!", ""));
    ProteinPtr protein1(new Protein("SPROT|42", 1, "Life, the universe, and everything.", ""));

    unit_assert(!protein0->empty());

    proteinListSimple->proteins.push_back(protein0);
    proteinListSimple->proteins.push_back(protein1);

    // let a ProteomeData object hold onto it as a ProteinListPtr

    ProteomeData data;
    data.proteinListPtr = proteinListSimple;

    // test ProteinList interface

    // verify index()
    const ProteinList& proteinList = *data.proteinListPtr;
    unit_assert(proteinList.size() == 2);
    unit_assert(proteinList.find("IPI-1701") == 0);
    unit_assert(proteinList.find("SPROT|42") == 1);

    // verify findKeyword
    IndexList result = proteinList.findKeyword("final");
    unit_assert(result.size()==1 && result[0]==0);

    result = proteinList.findKeyword("the", false);
    unit_assert(result.size()==2 && result[0]==0 && result[1]==1);

    result = proteinList.findKeyword("the");
    unit_assert(result.size()==1 && result[0]==1);

    result = proteinList.findKeyword("42");
    unit_assert(result.empty());

    // verify protein 0
    ProteinPtr protein = proteinList.protein(0);
    unit_assert(protein->index == protein0->index);
    unit_assert(protein->id == protein0->id);

    // verify spectrum 1
    protein = proteinList.protein(1);
    unit_assert(protein->index == protein1->index);
    unit_assert(protein->id == protein1->id);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        testProteinListSimple();
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
