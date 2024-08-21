//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _UNIT_HPP_
#define _UNIT_HPP_


#include "Exception.hpp"
#include "DateTime.hpp"
#include "Filesystem.hpp"
#include "Stream.hpp"
#include "pwiz/utility/math/round.hpp"
#include <cmath>
#include <boost/filesystem/detail/utf8_codecvt_facet.hpp>


namespace pwiz {
namespace util {


//
// These are assertion macros for unit testing.  They throw a runtime_error 
// exception on failure, instead of calling abort(), allowing the application
// to recover and return an appropriate error value to the shell.
//
// unit_assert(x):                             asserts x is true
// unit_assert_equal(x, y, epsilon):           asserts x==y, within epsilon
// unit_assert_matrices_equal(A, B, epsilon):  asserts A==B, within epsilon
//


inline std::string unit_assert_message(const char* filename, int line, const char* expression)
{
    std::ostringstream oss;
    oss << "[" << filename << ":" << line << "] Assertion failed: " << expression;
    return oss.str();
}

inline std::string unit_assert_equal_message(const char* filename, int line, const std::string& x, const std::string& y, const char* expression)
{
    std::ostringstream oss;
    oss << "[" << filename << ":" << line << "] Assertion failed: expected \"" << x << "\" but got \"" << y << "\" (" << expression << ")";
    return oss.str();
}

inline std::string unit_assert_numeric_equal_message(const char* filename, int line, double x, double y, double epsilon)
{
    std::ostringstream oss;
    oss.precision(10);
    oss << "[" << filename << ":" << line << "] Assertion failed: |" << x << " - " << y << "| < " << epsilon;
    return oss.str();
}

inline std::string unit_assert_exception_message(const char* filename, int line, const char* expression, const std::string& exception)
{
    std::ostringstream oss;
    oss << "[" << filename << ":" << line << "] Assertion \"" << expression << "\" failed to throw " << exception;
    return oss.str();
}

inline std::string quote_string(const string& str) {return "\"" + str + "\"";}


#define unit_assert(x) \
    (!(x) ? throw std::runtime_error(pwiz::util::unit_assert_message(__FILE__, __LINE__, #x)) : 0)

#define unit_assert_to_stream(x, os) \
    ((os) << (!(x) ? pwiz::util::unit_assert_message(__FILE__, __LINE__, #x) + "\n" : ""))


#define unit_assert_operator_equal(expected, actual) unit_assert_operator_equal_message(expected, actual, "")

#define unit_assert_operator_equal_message(expected, actual, message) \
    (!((expected) == (actual)) ? throw std::runtime_error(pwiz::util::unit_assert_equal_message(__FILE__, __LINE__, lexical_cast<string>(expected), lexical_cast<string>(actual), #actual) + (message)) : 0)

#define unit_assert_operator_equal_to_stream(expected, actual, os) \
    ((os) << (!((expected) == (actual)) ? pwiz::util::unit_assert_equal_message(__FILE__, __LINE__, lexical_cast<string>(expected), lexical_cast<string>(actual), #actual) + "\n" : ""))


#define unit_assert_equal(x, y, epsilon) \
    (!(fabs((x)-(y)) <= (epsilon)) ? throw std::runtime_error(pwiz::util::unit_assert_numeric_equal_message(__FILE__, __LINE__, (x), (y), (epsilon))) : 0)

#define unit_assert_equal_to_stream(x, y, epsilon, os) \
    ((os) << (!(fabs((x)-(y)) <= (epsilon)) ? pwiz::util::unit_assert_numeric_equal_message(__FILE__, __LINE__, (x), (y), (epsilon)) + "\n" : ""))


#define unit_assert_throws(x, exception) \
    { \
        bool threw = false; \
        try { (x); } \
        catch (exception&) \
        { \
            threw = true; \
        } \
        if (!threw) \
            throw std::runtime_error(pwiz::util::unit_assert_exception_message(__FILE__, __LINE__, #x, #exception)); \
    }


#define unit_assert_throws_what(x, exception, whatStr) \
    { \
        bool threw = false; \
        try { (x); } \
        catch (exception& e) \
        { \
            if (e.what() == std::string(whatStr)) \
                threw = true; \
            else \
                throw std::runtime_error(pwiz::util::unit_assert_exception_message(__FILE__, __LINE__, #x, std::string(#exception)+" "+pwiz::util::quote_string(whatStr)+"\nBut a different exception was thrown: ")+pwiz::util::quote_string(e.what())); \
        } \
        if (!threw) \
            throw std::runtime_error(pwiz::util::unit_assert_exception_message(__FILE__, __LINE__, #x, std::string(#exception)+" "+pwiz::util::quote_string(whatStr))); \
    }


#define unit_assert_matrices_equal(A, B, epsilon) \
    unit_assert(boost::numeric::ublas::norm_frobenius((A)-(B)) < (epsilon))


#define unit_assert_vectors_equal(A, B, epsilon) \
    unit_assert(boost::numeric::ublas::norm_2((A)-(B)) < (epsilon))


// the following macros are used by the ProteoWizard tests to report test status and duration to TeamCity

inline std::string escape_teamcity_string(const std::string& str)
{
    string result = str;
    bal::replace_all(result, "'", "|'");
    bal::replace_all(result, "\n", "|n");
    bal::replace_all(result, "\r", "|r");
    bal::replace_all(result, "|", "||");
    bal::replace_all(result, "[", "|[");
    bal::replace_all(result, "]", "|]");
    return result;
}

#define TEST_PROLOG_EX(argc, argv, suffix) \
    bnw::args args(argc, argv); \
    std::locale global_loc = std::locale(); \
    std::locale loc(global_loc, new boost::filesystem::detail::utf8_codecvt_facet); \
    bfs::path::imbue(loc); \
    bfs::path testName = bfs::change_extension(bfs::basename(argv[0]), (suffix)); \
    string teamcityTestName = pwiz::util::escape_teamcity_string(testName.string()); \
    bpt::ptime testStartTime; \
    vector<string> testArgs(argv, argv+argc); \
    bool teamcityTestDecoration = find(testArgs.begin(), testArgs.end(), "--teamcity-test-decoration") != testArgs.end(); \
    if (teamcityTestDecoration) \
    { \
        testStartTime = bpt::microsec_clock::local_time(); \
        cout << "##teamcity[testStarted name='" << teamcityTestName << "']" << endl; \
    } \
    int testExitStatus = 0;


#define TEST_PROLOG(argc, argv) TEST_PROLOG_EX(argc, argv, "")

#define TEST_FAILED(x) \
    if (teamcityTestDecoration) \
        cout << "##teamcity[testFailed name='" << teamcityTestName << "' message='" << pwiz::util::escape_teamcity_string((x)) << "']\n"; \
    cerr << (x) << endl; \
    testExitStatus = 1;

#define TEST_EPILOG \
    if (teamcityTestDecoration) \
        cout << "##teamcity[testFinished name='" << teamcityTestName << \
                "' duration='" << round((bpt::microsec_clock::local_time() - testStartTime).total_microseconds() / 1000.0) << "']" << endl; \
    return testExitStatus;


} // namespace util
} // namespace pwiz


// without PWIZ_DOCTEST defined, disable doctest macros; when it is defined, doctest will be configured with main()
#if !defined(PWIZ_DOCTEST) && !defined(PWIZ_DOCTEST_NO_MAIN)
#ifndef __cplusplus_cli
#define DOCTEST_CONFIG_DISABLE
#include "libraries/doctest.h"
#endif
#else
#define DOCTEST_CONFIG_IMPLEMENT
#include "libraries/doctest.h"

#ifndef PWIZ_DOCTEST_NO_MAIN
int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        doctest::Context context;
        testExitStatus = context.run();
    }
    catch (std::exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
#endif

namespace std
{
    template <typename T>
    vector<doctest::Approx> operator~(const vector<T>& lhs)
    {
        vector<doctest::Approx> result(lhs.size(), doctest::Approx(0));
        for (size_t i = 0; i < lhs.size(); ++i)
            result[i] = doctest::Approx(lhs[i]);
        return result;
    }

    inline ostream& operator<< (ostream& o, const doctest::Approx& rhs)
    {
        o << toString(rhs);
        return o;
    }

    template <typename T>
    bool operator==(const vector<T>& lhs, const vector<doctest::Approx>& rhs)
    {
        if (doctest::is_running_in_test)
            REQUIRE(lhs.size() == rhs.size());
        else if (lhs.size() != rhs.size())
            return false;

        for (size_t i = 0; i < lhs.size(); ++i)
            if (lhs[i] != rhs[i]) return false;
        return true;
    }

    template <typename T>
    bool operator==(const vector<doctest::Approx>& rhs, const vector<T>& lhs)
    {
        return lhs == rhs;
    }
}
#endif


#endif // _UNIT_HPP_

