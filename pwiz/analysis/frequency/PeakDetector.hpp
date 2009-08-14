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


#ifndef _PEAKDETECTOR_HPP_
#define _PEAKDETECTOR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/misc/FrequencyData.hpp"
#include "pwiz/data/misc/PeakData.hpp"


namespace pwiz {
namespace frequency {


/// Interface for finding peaks in frequency data.
class PWIZ_API_DECL PeakDetector
{
    public:

    /// Find the peaks in the frequency data, filling in Scan structure 
    virtual void findPeaks(const pwiz::data::FrequencyData& fd, 
                           pwiz::data::peakdata::Scan& result) const = 0;
    virtual ~PeakDetector(){}
};


} // namespace frequency
} // namespace pwiz


#endif // _PEAKDETECTOR_HPP_


