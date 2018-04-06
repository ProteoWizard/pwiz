//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#ifndef _PEAKDETECTORNAIVE_HPP_
#define _PEAKDETECTORNAIVE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "PeakDetector.hpp"
#include <memory>


namespace pwiz {
namespace frequency {


/// Naive implementation of the PeakDetector interface. 
///
/// Reports peaks where:
///  -# magnitude > noise*noiseFactor
///  -# magnitude is increasing on [center-detectionRadius, center] 
///  -# magnitude is decreasing on [center, center+detectionRadius] 
///
/// All peaks are reported as charge==1

class PWIZ_API_DECL PeakDetectorNaive : public PeakDetector
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


} // namespace frequency
} // namespace pwiz


#endif // _PEAKDETECTORNAIVE_HPP_


