//
// PeakelPicker.hpp
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
                                                                                                     
#ifndef _PEAKELPICKER_HPP_
#define _PEAKELPICKER_HPP_


#include "PeakelPicker.hpp"
#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/misc/PeakData.hpp"


namespace pwiz {
namespace analysis {


///
/// interface for picking Peakels and arranging into Features
///
class PWIZ_API_DECL PeakelPicker
{
    public:

    virtual ~PeakelPicker(){}
};


} // namespace analysis
} // namespace pwiz


#endif // _PEAKELPICKER_HPP_

