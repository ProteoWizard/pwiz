//
// PeakDetectorMatchedFilterTestData.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
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

