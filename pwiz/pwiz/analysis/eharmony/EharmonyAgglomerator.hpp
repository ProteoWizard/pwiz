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
/// EharmonyAgglomerator.hpp
///

#ifndef _EHARMONYAGGLOMERATOR_HPP_
#define _EHARMONYAGGLOMERATOR_HPP_

#include "AMTContainer.hpp"

namespace pwiz{
namespace eharmony{

    void pairwiseMatchmake(boost::shared_ptr<AMTContainer> a, boost::shared_ptr<AMTContainer> b, bool doFirst = false, bool doSecond = false, WarpFunctionEnum wfe = Default, NormalDistributionSearch snc = NormalDistributionSearch());

    boost::shared_ptr<AMTContainer> generateAMTDatabase(vector<boost::shared_ptr<AMTContainer> >& runs, vector<pair<int, int> > tree, WarpFunctionEnum& wfe, NormalDistributionSearch& snc);

} 
}

#endif
