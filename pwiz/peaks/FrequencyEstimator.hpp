//
// FrequencyEstimator.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _FREQUENCYESTIMATOR_HPP_ 
#define _FREQUENCYESTIMATOR_HPP_ 


#include "data/FrequencyData.hpp"
#include "data/PeakData.hpp"


namespace pwiz {
namespace peaks {


/// Interface for frequency estimator modules. 
class FrequencyEstimator
{
    public:

    typedef pwiz::data::FrequencyData FrequencyData;
    typedef pwiz::data::peakdata::Peak Peak;

    virtual Peak estimate(const FrequencyData& fd, 
                          const Peak& initialEstimate) const = 0; 

    virtual ~FrequencyEstimator(){}
};


} // namespace peaks
} // namespace pwiz


#endif // _FREQUENCYESTIMATOR_HPP_ 


