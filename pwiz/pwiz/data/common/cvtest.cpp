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


#include "cv.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>


using namespace pwiz::cv;
using namespace pwiz::util;
using namespace std;


ostream* os_ = 0;


void test()
{
    if (os_)
    {
        *os_ << "name: " << cvTermInfo(MS_sample_number).name << endl
             << "def: " << cvTermInfo(MS_sample_number).def << "\n\n";

        *os_ << "name: " << cvTermInfo(MS_polarity).name << endl
             << "def: " << cvTermInfo(MS_polarity).def << endl; 
    }

    // some simple tests
    unit_assert(cvTermInfo(MS_sample_number).name == "sample number");
    unit_assert(cvTermInfo(MS_contact_email).name == "contact email");
    unit_assert(cvTermInfo(MS_contact_email).def == "Email adress of the contact person.");

    unit_assert(cvTermInfo(MS_zlib_compression).parentsIsA.size() == 1 &&
                cvTermInfo(MS_zlib_compression).parentsIsA[0] == MS_binary_data_compression_type);

    unit_assert(cvTermInfo(MS_instrument_model).parentsPartOf.size() == 1 &&
                cvTermInfo(MS_instrument_model).parentsPartOf[0] == MS_instrument);

    unit_assert(cvTermInfo(MS_None_____OBSOLETE).isObsolete);
}


void testIsA()
{
    unit_assert(cvIsA(UO_dalton, UO_mass_unit));
    unit_assert(cvIsA(UO_mass_unit, UO_unit));
    unit_assert(cvIsA(UO_dalton, UO_unit));
    unit_assert(!cvIsA(UO_dalton, UO_energy_unit));
    unit_assert(cvIsA(MS_m_z, MS_m_z));
    unit_assert(cvIsA(MS_FT_ICR, MS_mass_analyzer_type));
}


void testOtherRelations()
{
    const CVTermInfo& info = cvTermInfo(MS_accuracy);
    unit_assert(info.otherRelations.size() == 2);
    unit_assert(info.otherRelations.begin()->first == "has_units");
    unit_assert(info.otherRelations.begin()->second == MS_m_z);
    unit_assert(info.otherRelations.rbegin()->first == "has_units");
    unit_assert(info.otherRelations.rbegin()->second == UO_parts_per_million);

    const CVTermInfo& info2 = cvTermInfo(MS_Trypsin);
    unit_assert(info2.otherRelations.size() == 1);
    unit_assert(info2.otherRelations.begin()->first == "has_regexp");
    unit_assert(info2.otherRelations.begin()->second == MS______KR_____P_);
}


void testSynonyms()
{
    const CVTermInfo& info = cvTermInfo(MS_B);
    unit_assert(info.name == "magnetic field strength");
    unit_assert(info.exactSynonyms.size() == 1);
    unit_assert(info.exactSynonyms[0] == "B");
    unit_assert(cvTermInfo(MS_QIT).exactSynonyms.size() == 3);

    unit_assert(cvTermInfo(MS_chemical_ionization).shortName() == "CI");
    unit_assert(cvTermInfo(MS_FT_ICR).shortName() == "FT_ICR");
    unit_assert(cvTermInfo(MS_fourier_transform_ion_cyclotron_resonance_mass_spectrometer).shortName() == "FT_ICR");
    unit_assert(cvTermInfo(CVID_Unknown).shortName() == "Unknown");
}


void testIDTranslation()
{
    unit_assert(cvTermInfo("MS:1000025").cvid == MS_B);
    unit_assert(cvTermInfo("MS:1000042").cvid == MS_peak_intensity);
    unit_assert(cvTermInfo("UO:0000231").cvid == UO_information_unit);
    unit_assert(cvTermInfo("XX:0000231").cvid == CVID_Unknown);
}


int main(int argc, char* argv[])
{
    if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout; 

    try
    {
        test();
        testIsA();
        testOtherRelations();
        testSynonyms();
        testIDTranslation();
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

