//
// CalibrationParametersTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "CalibrationParameters.hpp"
#include "util/unit.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::data;


ostream* os_ = 0;


void test()
{
    CalibrationParameters p = CalibrationParameters::thermo();
    CalibrationParameters q(0,1);

    unit_assert(p!=q);
    q.A = thermoA_;
    q.B = thermoB_;
    unit_assert(p==q);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "CalibrationParametersTest\n";
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

