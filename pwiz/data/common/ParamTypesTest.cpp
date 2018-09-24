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


#include "ParamTypes.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::cv;
using namespace pwiz::util;
using namespace pwiz::data;


ostream* os_ = 0;


class WriteCVParam
{
    public:

    WriteCVParam(ostream& os) : os_(os) {}

    void operator()(const CVParam& param)
    {
        os_ << "<cvParam " 
            << "cvLabel=\"" << cvTermInfo(param.cvid).id.substr(0,2) << "\" "
            << "accession=\"" << cvTermInfo(param.cvid).id << "\" "
            << "name=\"" << cvTermInfo(param.cvid).name << "\" "
            << "value=\"" << param.value << "\"";

        if (param.units != CVID_Unknown)
        {
            os_ << " unitAccession=\"" << cvTermInfo(param.units).id << "\" "
                << "unitName=\"" << cvTermInfo(param.units).name << "\""; 
        }

        os_ << "/>\n";
    }

    private:
    ostream& os_;
};


const char* mzmlScanTime = 
    "<cvParam cvLabel=\"MS\" accession=\"MS:1000016\" name=\"scan start time\" value=\"5.890500\" "
    "unitAccession=\"UO:0000031\" unitName=\"minute\"/>\n";

const char* mzmlCollisionEnergy = 
    "<cvParam cvLabel=\"MS\" accession=\"MS:1000045\" name=\"collision energy\" value=\"35.00\" "
    "unitAccession=\"UO:0000266\" unitName=\"electronvolt\"/>\n";


void test()
{
    vector<CVParam> params;

    params.push_back(CVParam(MS_lowest_observed_m_z, 420));
    params.push_back(CVParam(MS_highest_observed_m_z, 2000.012345));
    params.push_back(CVParam(MS_m_z, "goober"));
    params.push_back(CVParam(MS_scan_start_time, 5.890500, UO_minute)); 
    params.push_back(CVParam(MS_collision_energy, 35.00, UO_electronvolt)); 
    params.push_back(CVParam(MS_deisotoping, true)); 
    params.push_back(CVParam(MS_peak_picking, false)); 

    if (os_)
    {
        *os_ << "params:\n";
        copy(params.begin(), params.end(), ostream_iterator<CVParam>(*os_, "\n")); 
        *os_ << endl;
    
        *os_ << "as mzML <cvParam> elements:\n";
        for_each(params.begin(), params.end(), WriteCVParam(*os_));
        *os_ << endl;

        *os_ << "value casting:\n";
        int temp = params[0].valueAs<int>();
        *os_ << temp << endl;
        float temp2 = params[1].valueAs<float>();
        *os_ << temp2 << endl;
        string temp3 = params[2].valueAs<string>();
        *os_ << temp3 << "\n\n";
    }

    // verify simple things
    unit_assert(420 == params[0].valueAs<int>());
    unit_assert(2000.012345 == params[1].valueAs<double>());
    unit_assert("goober" == params[2].value);
    unit_assert(5.890500 == params[3].valueAs<double>());
    unit_assert(35.00 == params[4].valueAs<double>());
    unit_assert(params[0] == CVParam(MS_lowest_observed_m_z, 420));
    unit_assert(params[1] != CVParam(MS_lowest_observed_m_z, 420));
    unit_assert(CVParam(MS_m_z) == MS_m_z);
    unit_assert(params[5].valueAs<bool>() == true);
    unit_assert(params[6].valueAs<bool>() == false);

    // verify manual mzml writing -- this is to verify that we have enough
    // info to write <cvParam> elements as required by mzML

    ostringstream ossScanTime;
    CVParam scanTime(MS_scan_start_time, "5.890500", UO_minute); 
    (WriteCVParam(ossScanTime))(scanTime);
    if (os_) *os_ << "mzmlScanTime: " << mzmlScanTime << endl
                  << "ossScanTime: " << ossScanTime.str() << endl;
    unit_assert(ossScanTime.str() == mzmlScanTime);
    if (os_) *os_ << "scan time in seconds: " << scanTime.timeInSeconds() << endl;
    unit_assert_equal(scanTime.timeInSeconds(), 5.8905 * 60, 1e-10);

    ostringstream ossCollisionEnergy;
    (WriteCVParam(ossCollisionEnergy))(CVParam(MS_collision_energy, "35.00", UO_electronvolt));
    if (os_) *os_ << "mzmlCollisionEnergy: " << mzmlCollisionEnergy << endl
                  << "ossCollisionEnergy: " << ossCollisionEnergy.str() << endl;
    unit_assert(ossCollisionEnergy.str() == mzmlCollisionEnergy);
}


void testIs()
{
    vector<CVParam> params;
    params.push_back(CVParam(MS_plasma_desorption));
    params.push_back(CVParam(MS_lowest_observed_m_z, 420));
    params.push_back(CVParam(MS_collision_induced_dissociation));

    vector<CVParam>::const_iterator it = 
        find_if(params.begin(), params.end(), CVParamIs(MS_lowest_observed_m_z));

    unit_assert(it->value == "420");
}


void testIsChildOf()
{
    // example of how to search through a collection of CVParams
    // to find the first one whose cvid IsA specified CVID

    vector<CVParam> params;
    params.push_back(CVParam(MS_lowest_observed_m_z, 420));
    params.push_back(CVParam(MS_plasma_desorption));
    params.push_back(CVParam(MS_collision_induced_dissociation));
    params.push_back(CVParam(UO_electronvolt));
    params.push_back(CVParam(MS_highest_observed_m_z, 2400.0));

    vector<CVParam>::const_iterator itDiss = 
        find_if(params.begin(), params.end(), CVParamIsChildOf(MS_dissociation_method));

    vector<CVParam>::const_iterator itUnit = 
        find_if(params.begin(), params.end(), CVParamIsChildOf(UO_unit));

    if (os_)
    {
        *os_ << "find dissociation method: " 
             << (itDiss!=params.end() ? cvTermInfo(itDiss->cvid).name : "not found")
             << endl;

        *os_ << "find unit: " 
             << (itUnit!=params.end() ? cvTermInfo(itUnit->cvid).name : "not found")
             << endl;

    }

    unit_assert(itDiss!=params.end() && itDiss->cvid==MS_plasma_desorption);
    unit_assert(itUnit!=params.end() && itUnit->cvid==UO_electronvolt);
}


void testParamContainer()
{
    ParamContainer pc;
    pc.cvParams.push_back(MS_reflectron_on);
    pc.cvParams.push_back(MS_MSn_spectrum);
    pc.cvParams.push_back(MS_reflectron_off);
    pc.cvParams.push_back(CVParam(MS_ionization_type, 420));
    pc.userParams.push_back(UserParam("name1", "1", "type1", UO_second));
    pc.userParams.push_back(UserParam("name2", "2", "type2", UO_minute));

    ParamGroupPtr pg(new ParamGroup);
    pg->cvParams.push_back(CVParam(UO_dalton, 666));
    pc.paramGroupPtrs.push_back(pg);
   
    unit_assert(pc.hasCVParam(MS_reflectron_off));
    unit_assert(!pc.hasCVParam(MS_spectrum_type));
    unit_assert(pc.hasCVParam(UO_dalton));
    unit_assert(!pc.hasCVParam(UO_mass_unit));
      
    unit_assert(pc.hasCVParamChild(MS_spectrum_type));
    unit_assert(pc.hasCVParamChild(UO_mass_unit));

    unit_assert(pc.cvParam(MS_selected_ion_m_z) == CVID_Unknown);
    unit_assert(pc.cvParam(MS_reflectron_off) == MS_reflectron_off);
    unit_assert(pc.cvParam(UO_mass_unit) == CVID_Unknown);
    unit_assert(pc.cvParam(UO_dalton).cvid == UO_dalton);

    unit_assert(pc.cvParamChild(MS_spectrum_type) == MS_MSn_spectrum);
    unit_assert(pc.cvParamChild(UO_mass_unit).cvid == UO_dalton);

    string result = "goober";
    result = pc.cvParam(MS_selected_ion_m_z).value;
    unit_assert(result == "");
    result = pc.cvParam(MS_ionization_type).value;
    unit_assert(result == "420");
    result = pc.cvParam(UO_dalton).value;
    unit_assert(result == "666");

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

    unit_assert(pc.cvParamValueOrDefault(MS_deisotoping, true) == false);
    unit_assert(pc.cvParamValueOrDefault(MS_peak_picking, false) == false);
    unit_assert(pc.cvParamValueOrDefault(MS_ms_level, 0) == 3);

    pc.set(MS_electric_field_strength, 123.4);
    unit_assert_operator_equal(123.4, pc.cvParamChildValueOrDefault(MS_ion_optics_attribute, 0.0));
    unit_assert(pc.cvParamChildValueOrDefault(MS_precursor_activation_attribute, 0) == 0);

    pc.set(MS_CID);
    pc.set(MS_ETD);
    pg->set(MS_PQD);
    vector<CVParam> dissociationMethods = pc.cvParamChildren(MS_dissociation_method);
    unit_assert(dissociationMethods.size() == 3);
    unit_assert(dissociationMethods[0] == MS_CID);
    unit_assert(dissociationMethods[1] == MS_ETD);
    unit_assert(dissociationMethods[2] == MS_PQD);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        testIs();
        testIsChildOf();
        testParamContainer();
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

