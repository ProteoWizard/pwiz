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


#include "CVTranslator.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::data;


ostream* os_ = 0;


void test(const CVTranslator& translator, const string& text, CVID correct)
{
    CVID result = translator.translate(text);
    if (os_) *os_ << text << " -> (" << cvTermInfo(result).id << ", \"" 
                  << cvTermInfo(result).name << "\")\n"; 
    unit_assert(result == correct);
}


void test()
{
    if (os_) *os_ << "test()\n"; 

    CVTranslator translator;
    test(translator, "FT-ICR", MS_FT_ICR);
    test(translator, " \nFT -  \tICR\t", MS_FT_ICR);
    test(translator, " Total \t\n iOn  @#$CurRENT", MS_TIC);

    unit_assert(translator.translate("Darren Kessner") == CVID_Unknown);
    translator.insert("DARREN.#$@#$^KESSNER", MS_software);
    test(translator, "dARren kESSner", MS_software);

    // test collision detection
    bool caught = false;
    try
    {
        translator.insert("darren kessner", MS_m_z); 
    }
    catch (exception& )
    {
        caught = true;
    }
    if (os_) *os_ << "collision caught: " << boolalpha << caught << endl;
    unit_assert(caught);

    // test default extra entries

    test(translator, " itms ", MS_ion_trap);
    test(translator, " FTmS\n", MS_FT_ICR);
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

