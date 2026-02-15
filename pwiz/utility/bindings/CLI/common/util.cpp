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

#include "util.hpp"


namespace pwiz {
namespace CLI {
namespace util {

    System::String^ FileSystem::GetNonUnicodePath(System::String^ path)
    {
        // Try to use NTFS 8.3 short filename conversion to get a non-Unicode path
        std::string resultUtf8 = get_non_unicode_path(ToStdString(path));
        return ToSystemString(resultUtf8);
    }


} // namespace util
} // namespace CLI
} // namespace pwiz
