//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#include "pwiz/utility/misc/unit.hpp"
#include "ProteinList_DecoyGenerator.hpp"
#include "pwiz/data/proteome/examples.hpp"
#include "boost/random.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>
#include <random>


using namespace pwiz::proteome;
using namespace pwiz::analysis;
using namespace pwiz::util;


ostream* os_ = 0;


void testReversedList(ProteinListPtr pl)
{
    unit_assert(pl->size() == 3);
    ProteinList_DecoyGenerator decoyList(pl, ProteinList_DecoyGenerator::PredicatePtr(new ProteinList_DecoyGeneratorPredicate_Reversed("reversed_")));
    unit_assert(decoyList.size() == 6);
    for (size_t i=0; i < pl->size(); ++i)
    {
        ProteinPtr target = decoyList.protein(i);
        ProteinPtr decoy = decoyList.protein(i + pl->size());

        if (os_) *os_ << target->id << " " << target->sequence() << endl;
        if (os_) *os_ << decoy->id << " " << decoy->sequence() << endl;

        unit_assert("reversed_" + target->id == decoy->id);
        unit_assert(decoy->description.empty());
        unit_assert(string(target->sequence().rbegin(), target->sequence().rend()) == decoy->sequence());
    }
}


void testShuffledList(ProteinListPtr pl)
{
    unit_assert(pl->size() == 3);
    ProteinList_DecoyGenerator decoyList(pl, ProteinList_DecoyGenerator::PredicatePtr(new ProteinList_DecoyGeneratorPredicate_Shuffled("shuffled_")));
    unit_assert(decoyList.size() == 6);

    std::mt19937 rng(0);

    for (size_t i=0; i < pl->size(); ++i)
    {
        ProteinPtr target = decoyList.protein(i);
        ProteinPtr decoy = decoyList.protein(i + pl->size());

        if (os_) *os_ << target->id << " " << target->sequence() << endl;
        if (os_) *os_ << decoy->id << " " << decoy->sequence() << endl;

        unit_assert("shuffled_" + target->id == decoy->id);
        unit_assert(decoy->description.empty());
        string sequence = target->sequence();
        std::shuffle(sequence.begin(), sequence.end(), rng);
        unit_assert(sequence == decoy->sequence());
    }
}


void test()
{
    ProteomeData pd;
    examples::initializeTiny(pd);
    ProteinListPtr pl = pd.proteinListPtr;
    testReversedList(pl);
    testShuffledList(pl);
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


