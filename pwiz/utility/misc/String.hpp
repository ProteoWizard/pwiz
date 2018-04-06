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

#ifndef _STRING_HPP_
#define _STRING_HPP_

#include <string>
#include <sstream>
#include <boost/algorithm/string.hpp>
#include <boost/format.hpp>
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"

using std::string;
using std::getline;
using std::stringstream;
using std::istringstream;
using std::ostringstream;

#ifndef BOOST_NO_STD_WSTRING
// these cause trouble on mingw gcc - libstdc++ widechar not fully there yet
using std::wstring;
using std::wstringstream;
using std::wistringstream;
using std::wostringstream;
#endif

namespace bal = boost::algorithm;
using boost::lexical_cast;
using boost::bad_lexical_cast;
using boost::format;


namespace pwiz {
namespace util {


template <typename SequenceT>
std::string longestCommonPrefix(const SequenceT& strings)
{
    if (strings.empty())
        return "";

    typename SequenceT::const_iterator itr = strings.begin();
    std::string result = *itr;
    for (++itr; itr != strings.end(); ++itr)
    {
        const std::string& target = *itr;

        if (result.empty())
            return "";

        for (size_t j=0; j < target.length() && j < result.length(); ++j)
            if (target[j] != result[j])
            {
                result.resize(j);
                break;
            }
    }
    return result;
}


} // namespace util
} // namespace pwiz


#endif // _STRING_HPP_
