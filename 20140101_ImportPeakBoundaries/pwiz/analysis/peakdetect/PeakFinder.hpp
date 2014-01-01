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
                                                                                                     
#ifndef _PEAKFINDER_HPP_
#define _PEAKFINDER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/analysis/peakdetect/Noise.hpp"
#include "boost/shared_ptr.hpp"


namespace pwiz {
namespace analysis {


///
/// interface for finding peaks in an array of ordered pairs
///
class PWIZ_API_DECL PeakFinder
{
    public:
    
    virtual void findPeaks(const math::OrderedPairContainerRef& pairs,
                           std::vector<size_t>& resultIndices) const = 0; 
    virtual ~PeakFinder(){}
};


///
/// PeakFinder implementation based on signal-to-noise ratio
///
class PWIZ_API_DECL PeakFinder_SNR : public PeakFinder
{
    public:
    
    struct Config
    {
        size_t windowRadius;
        double zValueThreshold;
        bool preprocessWithLogarithm;
        std::ostream* log;
        
        Config(size_t _windowRadius = 2, 
               double _zValueThreshold = 3,
               bool _preprocessWithLogarithm = true,
               std::ostream* _log = 0)
        :   windowRadius(_windowRadius), 
            zValueThreshold(_zValueThreshold),
            preprocessWithLogarithm(_preprocessWithLogarithm),
            log(_log)
        {}
    };

    PeakFinder_SNR(boost::shared_ptr<NoiseCalculator> noiseCalculator,
                   const Config& config = Config());

    virtual void findPeaks(const math::OrderedPairContainerRef& pairs,
                           std::vector<size_t>& resultIndices) const; 

    private:
    boost::shared_ptr<NoiseCalculator> noiseCalculator_;
    Config config_;
};


} // namespace analysis
} // namespace pwiz


#endif // _PEAKFINDER_HPP_

