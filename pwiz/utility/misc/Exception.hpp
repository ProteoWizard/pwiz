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


#include <boost/stacktrace.hpp>
#include <boost/exception/exception.hpp>
#include <boost/exception/info.hpp>
#include <boost/exception/get_error_info.hpp>
#include <stdexcept>
#include <string>

namespace pwiz {
namespace util {

typedef boost::error_info<struct tag_stacktrace, boost::stacktrace::stacktrace> traced;

template <class E>
void throw_with_trace(const E& e) {
    throw boost::enable_error_info(e)
        << traced(boost::stacktrace::stacktrace());
}

inline std::string to_string_brief(const boost::stacktrace::stacktrace& st)
{
    std::string result;

    int throw_with_trace_index = -1;
    for (size_t i = 0, end = st.size(); i < end; ++i)
    {
        auto frame_str = boost::stacktrace::to_string(st[i]);

        // skip frames up to the throw_with_trace call
        if (throw_with_trace_index < 0)
        {
            if (frame_str.find("throw_with_trace") != std::string::npos)
                throw_with_trace_index = i;
            continue;
        }

        size_t adjustedIndex = i - throw_with_trace_index;
        if (adjustedIndex < 10)
            result += ' ';
        result += std::to_string(adjustedIndex);
        result += '#';
        result += ' ';
        result += frame_str;
        result += '\n';

        // skip frames after main()
        if (frame_str.find("main at") != std::string::npos)
            break;
    }

    return result;
}


class usage_exception : public std::runtime_error
{
    public: usage_exception(const std::string& usage) : std::runtime_error(usage) {}
};

class user_error : public std::runtime_error
{
    public: user_error(const std::string& what) : std::runtime_error(what) {}
};

} // namespace util
} // namespace pwiz


// make debug assertions throw exceptions in MSVC
#ifdef _DEBUG
#include <crtdbg.h>
#include <iostream>
#include <locale>
#include <sstream>

// preprocessed prototype of SetErrorMode so windows.h doesn't have to be included;
// this requires linking to the shared runtime but pwiz always does that on Windows
extern "C" __declspec(dllimport) unsigned int __stdcall SetErrorMode(unsigned int uMode);

namespace {

inline std::string narrow(const std::wstring& str)
{
    std::ostringstream oss;
    const std::ctype<wchar_t>& ctfacet = std::use_facet< std::ctype<wchar_t> >(oss.getloc());
    for (size_t i=0; i < str.size(); ++i)
        oss << ctfacet.narrow(str[i], 0);
    return oss.str();
}

inline int CrtReportHook(int reportType, char *message, int *returnValue)
{
    if (reportType == _CRT_ERROR || reportType == _CRT_ASSERT)
        throw std::runtime_error(message);
    return 0;
}

inline int CrtReportHookW(int reportType, wchar_t *message, int *returnValue)
{
    if (reportType == _CRT_ERROR || reportType == _CRT_ASSERT)
        throw std::runtime_error(narrow(message));
    return 0;
}

} // namespace

struct ReportHooker
{
    ReportHooker()
    {
        SetErrorMode(SetErrorMode(0) | 0x0002); // SEM_NOGPFAULTERRORBOX
        _CrtSetReportMode(_CRT_ASSERT, _CRTDBG_MODE_FILE);
        _CrtSetReportFile(_CRT_ASSERT, _CRTDBG_FILE_STDERR);
        _CrtSetReportHook2(_CRT_RPTHOOK_INSTALL, &CrtReportHook);
        _CrtSetReportHookW2(_CRT_RPTHOOK_INSTALL, &CrtReportHookW);
    }

    ~ReportHooker()
    {
        _CrtSetReportHook2(_CRT_RPTHOOK_REMOVE, &CrtReportHook);
        _CrtSetReportHookW2(_CRT_RPTHOOK_REMOVE, &CrtReportHookW);
    }
};
static ReportHooker reportHooker;
#endif // _DEBUG


// make Boost assertions throw exceptions
#if !defined(NDEBUG)
#define BOOST_ENABLE_ASSERT_HANDLER
#include <sstream>

namespace boost
{
    inline void assertion_failed_msg(char const * expr, char const * msg, char const * function, char const * file, long line) // user defined
    {
        std::ostringstream oss;
        oss << "[" << file << ":" << line << "] Assertion failed: " << expr;
        if (msg) oss << " (" << msg << ")";

        //oss << std::endl << "Backtrace:\n" << boost::stacktrace::stacktrace() << '\n';
        //pwiz::util::throw_with_trace(std::runtime_error(oss.str()));
        throw std::runtime_error(oss.str());
    }

    inline void assertion_failed(char const * expr, char const * function, char const * file, long line) // user defined
    {
        assertion_failed_msg(expr, 0, function, file, line);
    }
} // namespace boost
#endif // !defined(NDEBUG)


#endif // _EXCEPTION_HPP_
