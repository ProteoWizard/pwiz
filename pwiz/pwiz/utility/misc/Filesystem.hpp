//
// $Id$
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


#ifndef _FILESYSTEM_HPP_
#define _FILESYSTEM_HPP_

#include "Export.hpp"
#include "String.hpp"
#include "Container.hpp"
#include <boost/filesystem/operations.hpp>
#include <boost/filesystem/convenience.hpp>
#include <boost/filesystem/fstream.hpp>
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"

namespace bfs = boost::filesystem;


namespace pwiz {
namespace util {


/// expands (aka globs) a pathmask to zero or more matching paths
/// - matching paths can be either files or directories
/// - matching paths will be absolute if input pathmask was absolute
/// - matching paths will be relative if input pathmask was relative
PWIZ_API_DECL void expand_pathmask(const bfs::path& pathmask,
                                   vector<bfs::path>& matchingPaths);


PWIZ_API_DECL enum ByteSizeAbbreviation
{
    /// sizes are treated as multiples of 2;
    /// abbreviations are: GiB (Gibibyte), MiB (Mebibyte), KiB (Kibibyte), B (byte)
    ByteSizeAbbreviation_IEC,

    /// sizes are treated as multiples of 2;
    /// abbreviations are: GB (Gigabyte), MB (Megabyte), KB (Kilobyte), B (byte)
    ByteSizeAbbreviation_JEDEC,

    /// sizes are treated as multiples of 10;
    /// abbreviations are: GB (Gigabyte), MB (Megabyte), KB (Kilobyte), B (byte)
    ByteSizeAbbreviation_SI
};


/// abbreviates a byte size (file or RAM) as a readable string, using the specified notation
PWIZ_API_DECL
std::string abbreviate_byte_size(boost::uintmax_t byteSize,
                                 ByteSizeAbbreviation abbreviationType = ByteSizeAbbreviation_SI);


PWIZ_API_DECL std::string read_file_header(const std::string& filepath, size_t length = 512);


} // util
} // pwiz

#endif // _FILESYSTEM_HPP_
