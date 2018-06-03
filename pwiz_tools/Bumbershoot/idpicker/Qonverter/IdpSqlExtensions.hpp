//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2017 Vanderbilt University
//
// Contributor(s):
//


#ifndef _IDPSQLEXTENSIONS_HPP_
#define _IDPSQLEXTENSIONS_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include <string>

namespace IDPicker {

/// sets the separator used to separate groups in the GROUP_CONCAT_EX user function;
/// this allows changing the separator with the DISTINCT keyword, e.g. GROUP_CONCAT_EX(DISTINCT <...>)
PWIZ_API_DECL void setGroupConcatSeparator(const std::string& separator);

/// gets the separator used to separate groups in the GROUP_CONCAT_EX user function
PWIZ_API_DECL const std::string& getGroupConcatSeparator();

} // IDPicker

#endif // _IDPSQLEXTENSIONS_HPP_
