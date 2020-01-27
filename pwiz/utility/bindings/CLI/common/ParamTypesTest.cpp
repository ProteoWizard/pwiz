//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "unit.hpp"
#include <stdexcept>


using namespace pwiz::CLI::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::data;
using System::String;

void test()
{
    CVParamList params;
    UserParamList userParams;

    params.Add(gcnew CVParam(CVID::MS_lowest_observed_m_z, 420));
    params.Add(gcnew CVParam(CVID::MS_highest_observed_m_z, 2000.012345));
    params.Add(gcnew CVParam(CVID::MS_m_z, "goober"));
    params.Add(gcnew CVParam(CVID::MS_scan_start_time, 5.890500, CVID::UO_minute)); 
    params.Add(gcnew CVParam(CVID::MS_collision_energy, 35.00, CVID::UO_electronvolt)); 
    params.Add(gcnew CVParam(CVID::MS_deisotoping, true)); 
    params.Add(gcnew CVParam(CVID::MS_peak_picking, false));

    userParams.Add(gcnew UserParam("highest m/z", "2000.012345", "xsd:double"));
    userParams.Add(gcnew UserParam("m/z", "goober", "xsd:string"));

    // verify simple things
    unit_assert(420 == (int) params[0]->value);
    unit_assert(2000.012345 == (double) params[1]->value);
    unit_assert("goober" == (String^) params[2]->value);
    unit_assert(5.890500 == (double) params[3]->value);
    unit_assert(35.00 == (double) params[4]->value);
    unit_assert(params[0] == gcnew CVParam(CVID::MS_lowest_observed_m_z, 420));
    unit_assert(params[1] != gcnew CVParam(CVID::MS_lowest_observed_m_z, 420));
    unit_assert(gcnew CVParam(CVID::MS_m_z) == CVID::MS_m_z);
    unit_assert((bool) params[5]->value == true);
    unit_assert((bool) params[6]->value == false);

    unit_assert(CV::cvTermInfo(CVID::MS_LTQ)->cvid == (gcnew CVTermInfo(CVID::MS_LTQ))->cvid);
    unit_assert(CV::cvTermInfo("MS:1000447")->cvid == (gcnew CVTermInfo("MS:1000447"))->cvid);
    unit_assert(CV::cvIsA(CVID::MS_LTQ, CVID::MS_Thermo_Scientific_instrument_model));
    unit_assert(!CV::cvIsA(CVID::MS_QSTAR, CVID::MS_Thermo_Scientific_instrument_model));

    unit_assert_throws_what((double) params[2]->value, System::InvalidCastException, "Failed to cast CVParam");

    unit_assert(2000.012345 == (double) userParams[0]->value);
    unit_assert("goober" == (String^) userParams[1]->value);
    unit_assert_throws_what((double) userParams[1]->value, System::InvalidCastException, "Failed to cast UserParam");

    System::Collections::Generic::IList<CVID>^ cvids = CV::cvids();
    unit_assert(cvids->Count > 0);
}


/*void testIs()
{
    CVParamList params;
    params.Add(gcnew CVParam(CVID::MS_plasma_desorption));
    params.Add(gcnew CVParam(CVID::MS_lowest_observed_m_z, 420));
    params.Add(gcnew CVParam(CVID::MS_collision_induced_dissociation));


    vector<CVParam>::const_iterator it = 
        find_if(params.begin(), params.end(), CVParamIs(CVID::MS_lowest_observed_m_z));

    unit_assert(it->value == "420");
}


void testIsChildOf()
{
    // example of how to search through a collection of CVParams
    // to find the first one whose cvid IsA specified CVID

    CVParamList params;
    params.Add(gcnew CVParam(CVID::MS_lowest_observed_m_z, 420));
    params.Add(gcnew CVParam(CVID::MS_plasma_desorption));
    params.Add(gcnew CVParam(CVID::MS_collision_induced_dissociation));
    params.Add(gcnew CVParam(CVID::UO_electronvolt));
    params.Add(gcnew CVParam(CVID::MS_highest_observed_m_z, 2400.0));

    vector<CVParam>::const_iterator itDiss = 
        find_if(params.begin(), params.end(), CVParamIsChildOf(CVID::MS_dissociation_method));

    vector<CVParam>::const_iterator itUnit = 
        find_if(params.begin(), params.end(), CVParamIsChildOf(CVID::UO_unit));

    if (os_)
    {
        *os_ << "find dissociation method: " 
             << (itDiss!=params.end() ? cvTermInfo(itDiss->cvid).name : "not found")
             << endl;

        *os_ << "find unit: " 
             << (itUnit!=params.end() ? cvTermInfo(itUnit->cvid).name : "not found")
             << endl;

    }

    unit_assert(itDiss!=params.end() && itDiss->cvid==CVID::MS_plasma_desorption);
    unit_assert(itUnit!=params.end() && itUnit->cvid==CVID::UO_electronvolt);
}*/


void testParamContainer()
{
    ParamContainer pc;
    pc.cvParams->Add(gcnew CVParam(CVID::MS_reflectron_on));
    pc.cvParams->Add(gcnew CVParam(CVID::MS_MSn_spectrum));
    pc.cvParams->Add(gcnew CVParam(CVID::MS_reflectron_off));
    pc.cvParams->Add(gcnew CVParam(CVID::MS_ionization_type, 420));
    pc.userParams->Add(gcnew UserParam("name1", "1", "type1", CVID::UO_second));
    pc.userParams->Add(gcnew UserParam("name2", "2", "type2", CVID::UO_minute));

    ParamGroup^ pg = gcnew ParamGroup();
    pg->cvParams->Add(gcnew CVParam(CVID::UO_dalton, 666));
    pc.paramGroups->Add(pg);
   
    unit_assert(pc.hasCVParam(CVID::MS_reflectron_off));
    unit_assert(!pc.hasCVParam(CVID::MS_spectrum_type));
    unit_assert(pc.hasCVParam(CVID::UO_dalton));
    unit_assert(!pc.hasCVParam(CVID::UO_mass_unit));
      
    unit_assert(pc.hasCVParamChild(CVID::MS_spectrum_type));
    unit_assert(pc.hasCVParamChild(CVID::UO_mass_unit));

    unit_assert(pc.cvParam(CVID::MS_selected_ion_m_z) == CVID::CVID_Unknown);
    unit_assert(pc.cvParam(CVID::MS_reflectron_off) == CVID::MS_reflectron_off);
    unit_assert(pc.cvParam(CVID::UO_mass_unit) == CVID::CVID_Unknown);
    unit_assert(pc.cvParam(CVID::UO_dalton)->cvid == CVID::UO_dalton);

    unit_assert(pc.cvParamChild(CVID::MS_spectrum_type) == CVID::MS_MSn_spectrum);
    unit_assert(pc.cvParamChild(CVID::UO_mass_unit)->cvid == CVID::UO_dalton);

    System::String^ result = "goober";
    result = pc.cvParam(CVID::MS_selected_ion_m_z)->value;
    unit_assert(result == "");
    result = pc.cvParam(CVID::MS_ionization_type)->value;
    unit_assert(result == "420");
    result = pc.cvParam(CVID::UO_dalton)->value;
    unit_assert(result == "666");

    UserParam^ userParam = pc.userParam("name");
    unit_assert(userParam->empty());
    userParam = pc.userParam("name1");
    unit_assert(userParam->name == "name1");
    unit_assert((int) userParam->value == 1);
    unit_assert(userParam->type == "type1");
    unit_assert(userParam->units == CVID::UO_second);
    userParam = pc.userParam("name2");
    unit_assert(userParam->name == "name2");
    unit_assert((double) userParam->value == 2);
    unit_assert(userParam->type == "type2");
    unit_assert(userParam->units == CVID::UO_minute);
    unit_assert((int) pc.userParam("goober")->value == 0);

    pc.set(CVID::MS_ms_level, 2);
    unit_assert((int) pc.cvParam(CVID::MS_ms_level)->value == 2);
    pc.set(CVID::MS_ms_level, 3);
    unit_assert((int) pc.cvParam(CVID::MS_ms_level)->value == 3);

    pc.set(CVID::MS_deisotoping, true);
    unit_assert((bool) pc.cvParam(CVID::MS_deisotoping)->value == true);
    pc.set(CVID::MS_deisotoping, false);
    unit_assert((bool) pc.cvParam(CVID::MS_deisotoping)->value == false);
    
    pc.set(CVID::MS_CID);
    pc.set(CVID::MS_ETD);
    pg->set(CVID::MS_PQD);
    CVParamList^ dissociationMethods = pc.cvParamChildren(CVID::MS_dissociation_method);
    unit_assert(dissociationMethods->Count == 3);
    unit_assert(dissociationMethods[0] == CVID::MS_CID);
    unit_assert(dissociationMethods[1] == CVID::MS_ETD);
    unit_assert(dissociationMethods[2] == CVID::MS_PQD);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        test();
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
