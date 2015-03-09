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
                                                                                                     
#ifndef _PEAKFITTER_HPP_
#define _PEAKFITTER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/math/OrderedPair.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include <vector>


namespace pwiz {
namespace analysis {


///
/// interface for fitting peaks in an array of ordered pairs
///
class PWIZ_API_DECL PeakFitter
{
    public:

    typedef pwiz::data::peakdata::Peak Peak;
    
    virtual void fitPeak(const math::OrderedPairContainerRef& pairs,
                         size_t index,
                         Peak& result) const = 0; 

    void fitPeaks(const math::OrderedPairContainerRef& pairs,
                  std::vector<size_t>& indices,
                  std::vector<Peak>& result) const; 

    virtual ~PeakFitter(){}
};


///
/// PeakFitter implementation based on fitting a parabola
///
class PWIZ_API_DECL PeakFitter_Parabola : public PeakFitter
{
    public:
    
    struct Config
    {
        size_t windowRadius;
        
        Config(size_t _windowRadius = 1)
        :   windowRadius(_windowRadius)
        {}
    };

    PeakFitter_Parabola(const Config& config = Config());

    virtual void fitPeak(const math::OrderedPairContainerRef& pairs,
                         size_t index,
                         Peak& result) const;

    private:
    Config config_;
};


} // namespace analysis
} // namespace pwiz


#endif // _PEAKFITTER_HPP_

