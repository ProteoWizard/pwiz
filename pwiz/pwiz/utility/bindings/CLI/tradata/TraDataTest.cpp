//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
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


// #include "TraData.hpp"
#include "../common/unit.hpp"
#include <stdexcept>

using namespace System;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::data;
using namespace pwiz::CLI::util;
using namespace pwiz::CLI::tradata;


void testParamContainer()
{
    ParamContainer^ pc = gcnew ParamContainer();
    pc->set(CVID::MS_reflectron_on);
    pc->set(CVID::MS_MSn_spectrum);
    pc->set(CVID::MS_reflectron_off);
    pc->set(CVID::MS_ionization_type, 420);
    pc->userParams->Add(gcnew UserParam("name1", "1", "type1", CVID::UO_second));
    pc->userParams->Add(gcnew UserParam("name2", "2", "type2", CVID::UO_minute));
	
	unit_assert(pc->hasCVParam(CVID::MS_reflectron_off));
    unit_assert(!pc->hasCVParam(CVID::MS_spectrum_type));
      
    unit_assert(pc->hasCVParamChild(CVID::MS_spectrum_type));

    unit_assert(pc->cvParam(CVID::MS_selected_ion_m_z) == CVID::CVID_Unknown);
    unit_assert(pc->cvParam(CVID::MS_reflectron_off) == CVID::MS_reflectron_off);

    unit_assert(pc->cvParamChild(CVID::MS_spectrum_type) == CVID::MS_MSn_spectrum);
	
	System::String^ result = "goober";
    result = pc->cvParam(CVID::MS_selected_ion_m_z)->value;
    unit_assert(result == "");
    result = pc->cvParam(CVID::MS_ionization_type)->value;
    unit_assert(result == "420");
	
	UserParam^ userParam = pc->userParam("name");
    unit_assert(userParam->empty());
    userParam = pc->userParam("name1");
    unit_assert(userParam->name == "name1");
    unit_assert((int)(userParam->value) == 1);
    unit_assert(userParam->type == "type1");
    unit_assert(userParam->units == CVID::UO_second);
    userParam = pc->userParam("name2");
    unit_assert(userParam->name == "name2");
    unit_assert((double)(userParam->value) == 2);
    unit_assert(userParam->type == "type2");
    unit_assert(userParam->units == CVID::UO_minute);
    unit_assert((int)(pc->userParam("goober")->value) == 0);
	
	pc->set(CVID::MS_ms_level, 2);
    unit_assert((int)(pc->cvParam(CVID::MS_ms_level)->value) == 2);
    pc->set(CVID::MS_ms_level, 3);
    unit_assert((int)(pc->cvParam(CVID::MS_ms_level)->value) == 3);

    pc->set(CVID::MS_deisotoping, true);
    unit_assert((bool)(pc->cvParam(CVID::MS_deisotoping)->value) == true);
    pc->set(CVID::MS_deisotoping, false);
    unit_assert((bool)(pc->cvParam(CVID::MS_deisotoping)->value) == false);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        testParamContainer();
    }
    catch (exception& e)
    {
        TEST_FAILED("std::exception: " + string(e.what()))
    }
    catch (System::Exception^ e)
    {
        TEST_FAILED("System.Exception: " + ToStdString(e->Message))
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}