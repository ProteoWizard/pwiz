//
// CVTranslatorTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "CVTranslator.hpp"
#include "util/unit.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::msdata;


ostream* os_ = 0;


void test(const CVTranslator& translator, const string& text, CVID correct)
{
    CVID result = translator.translate(text);
    if (os_) *os_ << text << " -> (" << cvinfo(result).id << ", \"" 
                  << cvinfo(result).name << "\")\n"; 
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
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
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

