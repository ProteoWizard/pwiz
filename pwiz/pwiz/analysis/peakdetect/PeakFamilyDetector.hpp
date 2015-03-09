//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _PEAKFAMILYDETECTOR_HPP_
#define _PEAKFAMILYDETECTOR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/data/misc/PeakData.hpp"


namespace pwiz {
namespace analysis {


///
/// interface for peak family (isotope envelope) detection
/// 
class PWIZ_API_DECL PeakFamilyDetector
{
    public:

    typedef pwiz::msdata::MZIntensityPair MZIntensityPair;
    typedef pwiz::data::peakdata::PeakFamily PeakFamily;
    
    /// find peak families in a specified array of MZIntensityPair 
    virtual void detect(const MZIntensityPair* begin,
                        const MZIntensityPair* end,
                        std::vector<PeakFamily>& result) = 0;

    /// convenience function -- equivalent to:
    ///   detect(&data[0], &data[0]+data.size(), result) 
    virtual void detect(const std::vector<MZIntensityPair>& data,
                        std::vector<PeakFamily>& result);

    virtual ~PeakFamilyDetector() {} 
};


} // namespace analysis 
} // namespace pwiz


#endif // _PEAKFAMILYDETECTOR_HPP_

