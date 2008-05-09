//
// Filesystem.hpp
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "Export.hpp"
#include <string>
#include <vector>
#include <boost/filesystem/operations.hpp>
#include <boost/filesystem/convenience.hpp>

using boost::filesystem::path;
using boost::filesystem::file_size;
using boost::filesystem::last_write_time;
using boost::filesystem::exists;
using boost::filesystem::current_path;
using boost::filesystem::change_extension;
using boost::filesystem::basename;
using boost::filesystem::extension;


namespace pwiz {
namespace util {
PWIZ_API_DECL void FindFilesByMask(const std::string& mask,
                                   std::vector<std::string>& matchingFilepaths);
PWIZ_API_DECL std::vector<std::string> FindFilesByMask(const std::string& mask);
} // util
} // pwiz
