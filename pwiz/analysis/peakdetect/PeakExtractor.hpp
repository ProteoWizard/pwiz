//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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
                                                                                                     
#ifndef _PEAKEXTRACTOR_HPP_
#define _PEAKEXTRACTOR_HPP_


#include "PeakFinder.hpp"
#include "PeakFitter.hpp"
#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/math/OrderedPair.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "boost/shared_ptr.hpp"
#include <vector>


namespace pwiz {
namespace analysis {


///
/// Class for extracting Peak objects from an array of ordered pairs;
/// in design pattern lingo, this is a "template method" delegating
/// peak finding and peak fitting to "strategies".
///
class PWIZ_API_DECL PeakExtractor
{
    public:

    typedef pwiz::data::peakdata::Peak Peak;

    PeakExtractor(boost::shared_ptr<PeakFinder> peakFinder,
                  boost::shared_ptr<PeakFitter> peakFitter);
    
    void extractPeaks(const pwiz::math::OrderedPairContainerRef& pairs,
                      std::vector<Peak>& result) const; 

    private:

    boost::shared_ptr<PeakFinder> peakFinder_;
    boost::shared_ptr<PeakFitter> peakFitter_;
};


} // namespace analysis
} // namespace pwiz


#endif // _PEAKEXTRACTOR_HPP_

