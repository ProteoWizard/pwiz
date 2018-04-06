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


#include "LeastSquaresCalibrator.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <iomanip>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::calibration;
using namespace pwiz::data;


void test()
{
    vector<double> trueMasses;
    trueMasses.push_back(100);
    trueMasses.push_back(200);
    trueMasses.push_back(300);

    CalibrationParameters p = CalibrationParameters::thermo_FT();

    vector<double> observedFrequencies; 
    for (vector<double>::iterator it=trueMasses.begin(); it!=trueMasses.end(); ++it)
        observedFrequencies.push_back(p.frequency(*it)); 

    auto_ptr<LeastSquaresCalibrator> calibrator = LeastSquaresCalibrator::create(trueMasses,
                                                                                 observedFrequencies);
    calibrator->calibrate();

    /*
    cout << setprecision(10) << p << endl;
    cout << calibrator->parameters() << endl;
    cout << "error: " << calibrator->error() << endl;
    */

    const double epsilon = .2; // not too small since A,B ~ 1e8
    unit_assert_equal(p.A, calibrator->parameters().A, epsilon);
    unit_assert_equal(p.B, calibrator->parameters().B, epsilon);
    unit_assert_equal(0, calibrator->error(), 1e-15);
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

