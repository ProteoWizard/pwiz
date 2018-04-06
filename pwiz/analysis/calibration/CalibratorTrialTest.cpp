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


#include "CalibratorTrial.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::calibration;
using namespace pwiz::data;


void testConfiguration()
{
    CalibratorTrial::Configuration good;

    good.trueMasses.push_back(1.23);
    good.trueMasses.push_back(4.56);
    good.trueMasses.push_back(5.67);
    good.measurements.push_back(Calibrator::Measurement(12.3,1));
    good.measurements.push_back(Calibrator::Measurement(45.6,2));
    good.measurements.push_back(Calibrator::Measurement(78.9,3));
    good.parametersTrue = CalibrationParameters::thermo_FT();
    good.parametersInitialEstimate = CalibrationParameters(1,2);
    good.measurementError = 2.34567891e-6;
    good.initialErrorEstimate = 1.23456789e-6;

    const string& filename = "CalibratorTrialTest.temp.txt";
    good.writeTrialData(filename);
    CalibratorTrial::Configuration test;
    test.readTrialData(filename);
    
    unit_assert(good.trueMasses == test.trueMasses);
    unit_assert(good.measurements == test.measurements);
    unit_assert(good.parametersTrue == test.parametersTrue);
    unit_assert(good.parametersInitialEstimate == test.parametersInitialEstimate);
    unit_assert(good.initialErrorEstimate == test.initialErrorEstimate);
    unit_assert(good.measurementError == test.measurementError);

    system(("rm " + filename).c_str());
}


int main()
{
    try
    {
        testConfiguration();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

