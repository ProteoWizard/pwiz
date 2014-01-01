//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "MassSpread.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::calibration;


void test()
{
    auto_ptr<MassSpread> ms = MassSpread::create();
    ms->distribution().push_back(MassSpread::Pair(1,1));
    ms->distribution().push_back(MassSpread::Pair(2,2));
    ms->distribution().push_back(MassSpread::Pair(3,3));
    ms->recalculate();
    
    unit_assert(ms->distribution().size() == 3);
    unit_assert(ms->sumProbabilityOverMass() == 3);
    unit_assert_equal(ms->sumProbabilityOverMass2(), 1+5./6, 1e-10); 
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
}

