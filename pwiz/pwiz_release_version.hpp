//
// pwiz_release_version.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _PWIZ_RELEASE_VERSION_HPP_
#define _PWIZ_RELEASE_VERSION_HPP_


#include "utility/misc/Export.hpp"
#include <string>


namespace pwiz {
PWIZ_API_DECL const std::string& pwiz_release_version();
} // namespace pwiz


#endif // _PWIZ_RELEASE_VERSION_HPP_
