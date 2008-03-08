//
// PeakDetector.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _PEAKDETECTOR_HPP_
#define _PEAKDETECTOR_HPP_


#include "data/FrequencyData.hpp"
#include "data/PeakData.hpp"


namespace pwiz {
namespace peaks {


/// Interface for finding peaks in frequency data.
class PeakDetector
{
    public:

    /// Find the peaks in the frequency data, filling in Scan structure 
    virtual void findPeaks(const pwiz::data::FrequencyData& fd, 
                           pwiz::data::peakdata::Scan& result) const = 0;
    virtual ~PeakDetector(){}
};


} // namespace peaks
} // namespace pwiz


#endif // _PEAKDETECTOR_HPP_


