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
#include <algorithm>
#include <cctype>
#include <boost/algorithm/string.hpp>
#include <boost/format.hpp>
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"

using std::string;
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


/// heuristic that returns iterator in str pointing to first Unicode character, or str.end() if there are no Unicode characters
inline std::string::const_iterator findUnicodeBytes(const std::string& str)
{
    return std::find_if(str.begin(), str.end(), [](char ch) { return !isprint(static_cast<unsigned char>(ch)) || static_cast<int>(ch) < 0; });
}


/// Convenience wrapper for std::getline that strips trailing \r from DOS-style text files on any platform (e.g. OSX and Linux)
/// NB: DO NOT USE THIS IF YOU REQUIRE ACCURATE LINE LENGTH, E.G. FOR INDEXING A FILE!
template <class _Elem, class _Traits, class _Alloc>
std::basic_istream<_Elem, _Traits>& getlinePortable(std::basic_istream<_Elem, _Traits>&& _Istr, std::basic_string<_Elem, _Traits, _Alloc>& _Str, const _Elem _Delim)
{ // get characters into string, discard delimiter and trailing \r
    auto& result = std::getline(_Istr, _Str, _Delim);
    if (_Delim == _Istr.widen('\n'))
        bal::trim_right_if(_Str, bal::is_any_of("\r"));
    return result;
}

// Convenience wrapper for std::getline that strips trailing \r from DOS-style text files on any platform (e.g. OSX and Linux)
/// NB: DO NOT USE THIS IF YOU REQUIRE ACCURATE LINE LENGTH, E.G. FOR INDEXING A FILE!
template <class _Elem, class _Traits, class _Alloc>
std::basic_istream<_Elem, _Traits>& getlinePortable(std::basic_istream<_Elem, _Traits>&& _Istr, std::basic_string<_Elem, _Traits, _Alloc>& _Str)
{ // get characters into string, discard newline and trailing \r
    return getlinePortable(_Istr, _Str, _Istr.widen('\n'));
}

// Convenience wrapper for std::getline that strips trailing \r from DOS-style text files on any platform (e.g. OSX and Linux)
/// NB: DO NOT USE THIS IF YOU REQUIRE ACCURATE LINE LENGTH, E.G. FOR INDEXING A FILE!
template <class _Elem, class _Traits, class _Alloc>
std::basic_istream<_Elem, _Traits>& getlinePortable(std::basic_istream<_Elem, _Traits>& _Istr, std::basic_string<_Elem, _Traits, _Alloc>& _Str, const _Elem _Delim)
{ // get characters into string, discard delimiter and trailing \r
    return getlinePortable(std::move(_Istr), _Str, _Delim);
}

// Convenience wrapper for std::getline that strips trailing \r from DOS-style text files on any platform (e.g. OSX and Linux)
/// NB: DO NOT USE THIS IF YOU REQUIRE ACCURATE LINE LENGTH, E.G. FOR INDEXING A FILE!
template <class _Elem, class _Traits, class _Alloc>
std::basic_istream<_Elem, _Traits>& getlinePortable(std::basic_istream<_Elem, _Traits>& _Istr, std::basic_string<_Elem, _Traits, _Alloc>& _Str)
{ // get characters into string, discard newline and trailing \r
    return getlinePortable(std::move(_Istr), _Str, _Istr.widen('\n'));
}


enum class RealConvertPolicy
{
    AutoNotation,
    FixedNotation,
    ScientificNotation
};

/// uses boost::spirit::karma to do fast, fixed-precision conversion of floats to string (avoids lexical_cast's tendency to make values like 123.000007)
std::string toString(float value, RealConvertPolicy policyFlags = RealConvertPolicy::AutoNotation);

/// uses boost::spirit::karma to do fast, fixed-precision conversion of doubles to string (avoids lexical_cast's tendency to make values like 123.00000000007)
std::string toString(double value, RealConvertPolicy policyFlags = RealConvertPolicy::AutoNotation);

/// uses boost::spirit::karma to do faster conversion (relative to lexical_cast) of int to string
std::string toString(int value);

} // namespace util
} // namespace pwiz

using pwiz::util::getlinePortable;


#endif // _STRING_HPP_
