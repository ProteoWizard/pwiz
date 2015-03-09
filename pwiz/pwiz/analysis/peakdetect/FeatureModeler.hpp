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
                                                                                                     

#ifndef _FEATUREMODELER_HPP_
#define _FEATUREMODELER_HPP_


#include "MZRTField.hpp"
#include "pwiz/utility/misc/Export.hpp"


namespace pwiz {
namespace analysis {


///
/// interface for fitting and scoring Feature data to a model
///
class PWIZ_API_DECL FeatureModeler
{
    public:

    typedef pwiz::data::peakdata::Feature Feature;

    virtual void fitFeature(const Feature& in, Feature& out) const = 0;
    virtual void fitFeatures(const FeatureField& in, FeatureField& out) const;

    virtual ~FeatureModeler(){}
};


///
/// Gaussian implementation
///
class PWIZ_API_DECL FeatureModeler_Gaussian : public FeatureModeler
{
    public:

    virtual void fitFeature(const Feature& in, Feature& out) const;
};


} // namespace analysis
} // namespace pwiz


#endif // _FEATUREMODELER_HPP_

