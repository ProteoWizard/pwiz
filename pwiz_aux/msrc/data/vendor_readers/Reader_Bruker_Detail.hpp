//
// Reader_Bruker_Detail.hpp
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#ifndef _READER_BRUKER_DETAIL_HPP_ 
#define _READER_BRUKER_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include <string>

namespace pwiz {
namespace msdata {
namespace detail {

enum SpectrumList_Bruker_Format
{
    SpectrumList_Bruker_Format_Unknown,
    SpectrumList_Bruker_Format_FID,
    SpectrumList_Bruker_Format_YEP,
    SpectrumList_Bruker_Format_BAF
};

/// returns Bruker format of 'path' if it is a Bruker directory;
/// otherwise returns empty string
SpectrumList_Bruker_Format format(const std::string& path);

} // namespace detail
} // namespace msdata
} // namespace pwiz

#endif // _READER_BRUKER_DETAIL_HPP_
