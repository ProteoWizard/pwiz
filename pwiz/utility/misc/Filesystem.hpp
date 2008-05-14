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
#include "String.hpp"
#include "Container.hpp"
#include <boost/filesystem/operations.hpp>
#include <boost/filesystem/convenience.hpp>

namespace bfs = boost::filesystem;


namespace pwiz {
namespace util {
PWIZ_API_DECL void FindFilesByMask(const string& mask,
                                   vector<string>& matchingFilepaths);
PWIZ_API_DECL vector<string> FindFilesByMask(const string& mask);
} // util
} // pwiz
