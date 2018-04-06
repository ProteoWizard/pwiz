//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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


#include <stdexcept>
#include "optimized_lexical_cast.hpp"
#include <cstdlib>


namespace pwiz {
namespace util {
namespace env {


template <typename T>
T get(const char* name, const T& defaultValue)
{
    if (!name)
        throw std::runtime_error("[env::get()] null variable name");

    T value(defaultValue);
    char* result = ::getenv(name);
    if (result)
        value = boost::lexical_cast<T>(result);
    return value;
}


template <typename T>
T get(const std::string& name, const T& defaultValue)
{
    if (name.empty())
        throw std::runtime_error("[env::get()] empty variable name");

    return get(name.c_str(), defaultValue);
}


/// explicit single-argument overload
inline std::string get(const std::string& name) {return get<std::string>(name, std::string());}


} // namespace env
} // namespace util
} // namespace pwiz
