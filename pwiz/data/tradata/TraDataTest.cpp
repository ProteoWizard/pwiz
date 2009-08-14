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


#include "TraData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/String.hpp"
#include <iostream>
#include <iterator>


using namespace std;
using namespace pwiz;
using namespace pwiz::util;
using namespace pwiz::tradata;
using boost::shared_ptr;


void testParamContainer()
{
    ParamContainer pc;
    pc.cvParams.push_back(MS_reflectron_on);
    pc.cvParams.push_back(MS_MSn_spectrum);
    pc.cvParams.push_back(MS_reflectron_off);
    pc.cvParams.push_back(CVParam(MS_ionization_type, 420));
    pc.userParams.push_back(UserParam("name1", "1", "type1", UO_second));
    pc.userParams.push_back(UserParam("name2", "2", "type2", UO_minute));
   
    unit_assert(pc.hasCVParam(MS_reflectron_off));
    unit_assert(!pc.hasCVParam(MS_spectrum_type));
      
    unit_assert(pc.hasCVParamChild(MS_spectrum_type));

    unit_assert(pc.cvParam(MS_selected_ion_m_z) == CVID_Unknown);
    unit_assert(pc.cvParam(MS_reflectron_off) == MS_reflectron_off);

    unit_assert(pc.cvParamChild(MS_spectrum_type) == MS_MSn_spectrum);

    string result = "goober";
    result = pc.cvParam(MS_selected_ion_m_z).value;
    unit_assert(result == "");
    result = pc.cvParam(MS_ionization_type).value;
    unit_assert(result == "420");

    UserParam userParam = pc.userParam("name");
    unit_assert(userParam.empty());
    userParam = pc.userParam("name1");
    unit_assert(userParam.name == "name1");
    unit_assert(userParam.valueAs<int>() == 1);
    unit_assert(userParam.type == "type1");
    unit_assert(userParam.units == UO_second);
    userParam = pc.userParam("name2");
    unit_assert(userParam.name == "name2");
    unit_assert(userParam.valueAs<double>() == 2);
    unit_assert(userParam.type == "type2");
    unit_assert(userParam.units == UO_minute);
    unit_assert(pc.userParam("goober").valueAs<int>() == 0);

    pc.set(MS_ms_level, 2);
    unit_assert(pc.cvParam(MS_ms_level).valueAs<int>() == 2);
    pc.set(MS_ms_level, 3);
    unit_assert(pc.cvParam(MS_ms_level).valueAs<int>() == 3);

    pc.set(MS_deisotoping, true);
    unit_assert(pc.cvParam(MS_deisotoping).valueAs<bool>() == true);
    pc.set(MS_deisotoping, false);
    unit_assert(pc.cvParam(MS_deisotoping).valueAs<bool>() == false);
}


int main()
{
    try
    {
        testParamContainer();
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
