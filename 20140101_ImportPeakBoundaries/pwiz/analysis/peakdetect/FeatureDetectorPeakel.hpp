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
       

#ifndef _FEATUREDETECTORPEAKEL_HPP_
#define _FEATUREDETECTORPEAKEL_HPP_


#include "FeatureDetector.hpp"
#include "PeakExtractor.hpp"
#include "PeakelGrower.hpp"
#include "PeakelPicker.hpp"


namespace pwiz {
namespace analysis {


///
/// FeatureDetectorPeakel implements a 'template method', delegating to 'strategies'
/// encapsulated by the following interfaces:
///   PeakExtractor
///   PeakelGrower
///   PeakelPicker
///
class PWIZ_API_DECL FeatureDetectorPeakel : public FeatureDetector
{
    public:

    typedef pwiz::msdata::MSData MSData;

    FeatureDetectorPeakel(boost::shared_ptr<PeakExtractor> peakExtractor,
                          boost::shared_ptr<PeakelGrower> peakelGrower,
                          boost::shared_ptr<PeakelPicker> peakelPicker);

    virtual void detect(const MSData& msd, FeatureField& result) const;
    
    /// convenience construction

    struct Config
    {
        std::ostream* log; // propagates to sub-objects during create()
        NoiseCalculator_2Pass::Config noiseCalculator_2Pass;
        PeakFinder_SNR::Config peakFinder_SNR;
        PeakFitter_Parabola::Config peakFitter_Parabola;
        PeakelGrower_Proximity::Config peakelGrower_Proximity;
        PeakelPicker_Basic::Config peakelPicker_Basic;
        
        Config() : log(0) {}
    };
    
    static boost::shared_ptr<FeatureDetectorPeakel> create(Config config);

    private:
    boost::shared_ptr<PeakExtractor> peakExtractor_;
    boost::shared_ptr<PeakelGrower> peakelGrower_;
    boost::shared_ptr<PeakelPicker> peakelPicker_;
};


} // namespace analysis
} // namespace pwiz


#endif // _FEATUREDETECTORPEAKEL_HPP_

