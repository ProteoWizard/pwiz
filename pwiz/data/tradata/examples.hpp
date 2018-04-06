//
// $Id$ 
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _TRADATA_EXAMPLES_HPP_
#define _TRADATA_EXAMPLES_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "TraData.hpp"


namespace pwiz {
namespace tradata {
namespace examples {


PWIZ_API_DECL void initializeTiny(TraData& td);
PWIZ_API_DECL void addMIAPEExampleMetadata(TraData& td);


} // namespace examples
} // namespace msdata
} // namespace pwiz


#endif // _TRADATA_EXAMPLES_HPP_
