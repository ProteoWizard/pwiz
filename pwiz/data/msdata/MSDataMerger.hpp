//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#ifndef _MSDATAMERGER_HPP_
#define _MSDATAMERGER_HPP_


#include "MSData.hpp"


namespace pwiz {
namespace msdata {


struct PWIZ_API_DECL MSDataMerger : public MSData
{
    MSDataMerger(const std::vector<MSDataPtr>& inputs);

    private:
    std::vector<MSDataPtr> inputMSDataPtrs_;
};


} // namespace pwiz
} // namespace msdata


#endif // _MSDATAMERGER_HPP_
