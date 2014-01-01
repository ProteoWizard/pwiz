//
// $Id$
//
//
// Original author: Kate Hoff <Katherine.Hoff@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Cnter, Los Angeles, California  90048
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
       
#ifndef _FEATUREDETECTORSIMPLE_HPP_
#define _FEATUREDETECTORSIMPLE_HPP_

#include "FeatureDetector.hpp"
#include "pwiz/analysis/peakdetect/PeakFamilyDetector.hpp"
#include "pwiz/data/misc/PeakData.hpp"

namespace pwiz{
namespace analysis{


using namespace pwiz::data::peakdata;


/// FeatureDetectorSimple detects 'rectangular' features, ie number of peaks in 
/// isotope envelope is the same for each scan included in the feature
class PWIZ_API_DECL FeatureDetectorSimple : public FeatureDetector
{
    public:

    FeatureDetectorSimple(boost::shared_ptr<PeakFamilyDetector> _pfd);
    virtual void detect(const MSData& msd, FeatureField& result) const;

    private:

    class Impl;
    boost::shared_ptr<Impl> _pimpl;
    FeatureDetectorSimple(const FeatureDetectorSimple&);
    FeatureDetectorSimple& operator=(const FeatureDetectorSimple&);
};


} // namespace analysis
} // namespace pwiz


#endif // _FEATUREDETECTORSIMPLE_HPP_

