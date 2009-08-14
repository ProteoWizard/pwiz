//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "PeptideIDMap.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>


using namespace pwiz::util;
using namespace pwiz::peptideid;
using namespace std;


void test()
{
    PeptideIDMap peptideIDMap;

    PeptideID::Record* record = &peptideIDMap["1"];
    record->nativeID = "1";
    record->sequence = "DARREN";
    record->normalizedScore = .5;

    record = &peptideIDMap["2"];
    record->nativeID = "2";
    record->sequence = "KESSNER";
    record->normalizedScore = .6;

    PeptideID::Record result = peptideIDMap.record(PeptideID::Location("goober", 0, 0));
    unit_assert(result.nativeID.empty());
    unit_assert(result.sequence.empty());
    unit_assert_equal(result.normalizedScore, 0, 1e-15);

    result = peptideIDMap.record(PeptideID::Location("1", 0, 0));
    unit_assert(result.nativeID == "1");
    unit_assert(result.sequence == "DARREN");
    unit_assert_equal(result.normalizedScore, .5, 1e-15);

    result = peptideIDMap.record(PeptideID::Location("2", 0, 0));
    unit_assert(result.nativeID == "2");
    unit_assert(result.sequence == "KESSNER");
    unit_assert_equal(result.normalizedScore, .6, 1e-15);
}


int main()
{
    try
    {
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
        return 1;
    }
}

