//
// $Id$
//
//
// Original author: Brian Pratt <bspratt at proteinms.net>
//
// Copyright 2025 University of Washington
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


#ifndef _CLI_UTIL_HPP_
#define _CLI_UTIL_HPP_

#ifndef NOMINMAX
# define NOMINMAX
#endif

#include <stdlib.h>
#include <vcclr.h>
#pragma unmanaged
#include <string>
#include <vector>
#include <boost/shared_ptr.hpp>
#include <boost/pointer_cast.hpp>
#include <boost/preprocessor/stringize.hpp>
#include "pwiz/utility/misc/Exception.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "comdef.h" // for _com_error

#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
using namespace pwiz::util;
#pragma managed

namespace pwiz { namespace CLI { namespace util {
    public ref class FileSystem
    {
    public:
        static System::String^ GetNonUnicodePath(System::String^ path);
    };
}}}

#endif // _CLI_UTIL_HPP_
