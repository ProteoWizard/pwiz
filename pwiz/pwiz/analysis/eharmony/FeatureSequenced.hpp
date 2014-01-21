//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
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

///
/// FeatureSequenced.hpp
///

#ifndef _FEATURESEQUENCED_HPP_
#define _FEATURESEQUENCED_HPP_

#include "pwiz/data/misc/PeakData.hpp"
#include "boost/shared_ptr.hpp"

namespace pwiz{
namespace eharmony{

using namespace pwiz::data::peakdata;

struct PWIZ_API_DECL FeatureSequenced
{

    FeatureSequenced() : ms2(""), ms1_5(""), calculatedMass(0), ppProb(0), peptideCount(0) { feature = FeaturePtr(new Feature());}
    FeatureSequenced(FeaturePtr _feature);
    FeatureSequenced(const FeatureSequenced& _fs);

    FeaturePtr feature;
    std::string ms2;
    std::string ms1_5;
    double calculatedMass;
    double ppProb;
    size_t peptideCount;

    bool operator==(const FeatureSequenced& that) const;    
    bool operator!=(const FeatureSequenced& that) const;

private:

    // no copying
    
    FeatureSequenced(FeatureSequenced&);
    FeatureSequenced& operator=(FeatureSequenced&);

};

} // namespace eharmony
} // namespace pwiz

#endif
