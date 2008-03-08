//
// FrequencyDataTestData.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _FREQUENCYDATATESTDATA_HPP_
#define  _FREQUENCYDATATESTDATA_HPP_


struct RawMassDatum
{
    double mz;
    double intensity;
};


extern RawMassDatum sampleMassData_[];
extern const unsigned int sampleMassDataSize_;


extern const char* sampleFrequencyData_;


#endif // _FREQUENCYDATATESTDATA_HPP_

