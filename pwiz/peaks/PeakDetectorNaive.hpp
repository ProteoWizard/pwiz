//
// PeakDetectorNaive.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _PEAKDETECTORNAIVE_HPP_
#define _PEAKDETECTORNAIVE_HPP_


#include "PeakDetector.hpp"
#include <memory>


namespace pwiz {
namespace peaks {


/// Naive implementation of the PeakDetector interface. 
///
/// Reports peaks where:
///  -# magnitude > noise*noiseFactor
///  -# magnitude is increasing on [center-detectionRadius, center] 
///  -# magnitude is decreasing on [center, center+detectionRadius] 
///
/// All peaks are reported as charge==1

class PeakDetectorNaive : public PeakDetector
{
    public:
    /// create an instance.
    static std::auto_ptr<PeakDetectorNaive> create(double noiseFactor = 5, 
                                                   unsigned int detectionRadius = 2);

    virtual double noiseFactor() const = 0;
    virtual unsigned int detectionRadius() const = 0;

    /// \name PeakDetector interface
    //@{
    virtual void findPeaks(const pwiz::data::FrequencyData& fd, 
                           pwiz::data::peakdata::Scan& result) const = 0; 
    virtual ~PeakDetectorNaive(){}
    //@}
};


} // namespace peaks
} // namespace pwiz


#endif // _PEAKDETECTORNAIVE_HPP_


