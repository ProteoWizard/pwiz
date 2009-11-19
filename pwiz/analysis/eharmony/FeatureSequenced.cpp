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
/// FeatureSequenced.cpp
///

#include "FeatureSequenced.hpp"

using namespace pwiz;
using namespace eharmony;

FeatureSequenced::FeatureSequenced(FeaturePtr _feature) : ms2(""), ms1_5(""), peptideCount(0), feature(_feature){}

FeatureSequenced::FeatureSequenced(const FeatureSequenced& _fs) : feature(_fs.feature), ms2(_fs.ms2), ms1_5(_fs.ms1_5), calculatedMass(_fs.calculatedMass), ppProb(_fs.ppProb), peptideCount(_fs.peptideCount) 
{}

    
bool FeatureSequenced::operator==(const FeatureSequenced& that) const
{
    return *feature == *that.feature &&
            ms2 == that.ms2 &&
            ms1_5 == that.ms1_5;

}
    
bool FeatureSequenced::operator!=(const FeatureSequenced& that) const 
{ 
    return !(*this == that); 
}


