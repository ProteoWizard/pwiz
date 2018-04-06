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


#ifndef CLICKWRAPPROMPTER_HPP_
#define CLICKWRAPPROMPTER_HPP_


#include "Export.hpp"
#include <string>


namespace pwiz {
namespace util {


/// a barrier to force end users to agree/disagree to a prompt before continuing
class PWIZ_API_DECL ClickwrapPrompter
{
    public:

    /// shows a modal dialog with the specified caption, text, and Agree/Disagree buttons;
    /// returns true iff the user clicked the Agree button;
    /// if oneTimeKey is non-empty, all calls with that key after the user has agreed will be ignored
    static bool prompt(const std::string& caption, const std::string& text, const std::string& oneTimeKey = "");
};


} // namespace util
} // namespace pwiz


#endif // CLICKWRAPPROMPTER_HPP_
