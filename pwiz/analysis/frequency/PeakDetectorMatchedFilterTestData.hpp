//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Louis Warschaw Prostate Cancer Center
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


#ifndef _PEAKDETECTORMATCHEDFILTERTESTDATA_HPP_ 
#define _PEAKDETECTORMATCHEDFILTERTESTDATA_HPP_ 


struct TestDatum
{
    double frequency;
    double mz;
    double real;
    double imaginary;
    double magnitude;
};


extern TestDatum testData_[];
extern const unsigned int testDataSize_;


const double testDataObservationDuration_ = .768;
const double testDataCalibrationA_ = 1.075339687500000e+008; 
const double testDataCalibrationB_ = -3.454602661132810e+008;


#endif // _PEAKDETECTORMATCHEDFILTERTESTDATA_HPP_ 

