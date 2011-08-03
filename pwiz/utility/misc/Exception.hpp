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

#ifndef _EXCEPTION_HPP_
#define _EXCEPTION_HPP_


#include "Export.hpp"
#include <stdexcept>


using std::exception;
using std::runtime_error;
using std::out_of_range;
using std::domain_error;
using std::invalid_argument;
using std::length_error;
using std::logic_error;
using std::overflow_error;
using std::range_error;
using std::underflow_error;


// make debug assertions throw exceptions in MSVC
#ifdef _DEBUG
struct PWIZ_API_DECL ReportHooker {ReportHooker(); ~ReportHooker();};
static ReportHooker reportHooker;
#endif // _DEBUG


// handle Boost assertions with a message to stderr
#if !defined(NDEBUG)
#include <sstream>
namespace boost
{
    inline void assertion_failed(char const * expr, char const * function, char const * file, long line) // user defined
    {
        std::ostringstream oss;
        oss << "[" << file << ":" << line << "] Assertion failed: " << expr;
        throw std::runtime_error(oss.str());
    }
} // namespace boost
#endif // !defined(NDEBUG)


#endif // _EXCEPTION_HPP_
