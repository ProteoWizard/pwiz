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
#include <crtdbg.h>
#include <iostream>
inline int CrtReportHook(int reportType, char *message, int *returnValue)
{
    std::cerr << message;
    if (returnValue) *returnValue = 0;
    return 1;
}

inline int CrtReportHookW(int reportType, wchar_t *message, int *returnValue)
{
    std::wcerr << message;
    if (returnValue) *returnValue = 0;
    return 1;
}

struct ReportHooker
{
    ReportHooker()
    {
        _CrtSetReportHook2(_CRT_RPTHOOK_INSTALL, &CrtReportHook);
        _CrtSetReportHookW2(_CRT_RPTHOOK_INSTALL, &CrtReportHookW);
    }

    ~ReportHooker()
    {
        _CrtSetReportHook2(_CRT_RPTHOOK_REMOVE, &CrtReportHook);
        _CrtSetReportHookW2(_CRT_RPTHOOK_REMOVE, &CrtReportHookW);
    }

    // TODO: redesign to support once-per-process (or once-per-thread?) initialization
    //private:
    //bool isReportHookSet;
};

static ReportHooker reportHooker;

#endif // _DEBUG


// handle Boost assertions with a message to stderr
#if !defined(NDEBUG)
#include <sstream>
#define BOOST_ENABLE_ASSERT_HANDLER
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
