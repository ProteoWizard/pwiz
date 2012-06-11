//
// $Id$ (CLI binding)
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _UNIT_HPP_CLI_
#define _UNIT_HPP_CLI_


namespace pwiz {
namespace CLI {
namespace util {


//
// These are assertion macros for unit testing.  They throw a System::Exception 
// exception on failure, instead of calling abort(), allowing the application
// to recover and return an appropriate error value to the shell.
//
// unit_assert(x):                             asserts x is true
// unit_assert_equal(x, y, epsilon):           asserts x==y, within epsilon
// unit_assert_matrices_equal(A, B, epsilon):  asserts A==B, within epsilon
//


inline System::String^ unit_assert_message(const char* filename, int line, const char* expression)
{
    System::Text::StringBuilder sb;
    sb.AppendFormat("[{0}:{1}] Assertion failed: {2}", gcnew System::String(filename), line, gcnew System::String(expression));
    return sb.ToString();
}

inline System::String^ unit_assert_equal_message(const char* filename, int line, System::String^ x, System::String^ y, const char* expression)
{
    System::Text::StringBuilder sb;
    sb.AppendFormat("[{0}:{1}] Assertion failed: expected \"{2}\" but got \"{3}\" ({4})",
                    gcnew System::String(filename), line,
                    gcnew System::String(x),
                    gcnew System::String(y),
                    gcnew System::String(expression));
    return sb.ToString();
}

inline System::String^ unit_assert_numeric_equal_message(const char* filename, int line, double x, double y, double epsilon)
{
    System::Text::StringBuilder sb;
    sb.AppendFormat("[{0}:{1}] Assertion failed: |{2} - {3}| < {4}", gcnew System::String(filename), line, x, y, epsilon);
    return sb.ToString();
}

inline System::String^ unit_assert_exception_message(const char* filename, int line, const char* expression, System::String^ exception)
{
    System::Text::StringBuilder sb;
    sb.AppendFormat("[{0}:{1}] Assertion failed to throw \"{2}\": {3}", gcnew System::String(filename), line, exception, gcnew System::String(expression));
    return sb.ToString();
}


#define unit_assert(x) \
    (!(x) ? throw gcnew System::Exception(unit_assert_message(__FILE__, __LINE__, #x)) : 0) 


#define unit_assert_operator_equal(expected, actual) \
    (!(expected == actual) ? throw gcnew System::Exception(unit_assert_equal_message(__FILE__, __LINE__, (expected).ToString(), (actual).ToString(), #actual)) : 0)


#define unit_assert_string_operator_equal(expected, actual) \
    (!(expected == actual) ? throw gcnew System::Exception(unit_assert_equal_message(__FILE__, __LINE__, (expected), (actual), #actual)) : 0)


#define unit_assert_equal(x, y, epsilon) \
    (!(System::Math::Abs((x)-(y)) <= (epsilon)) ? throw gcnew System::Exception(unit_assert_numeric_equal_message(__FILE__, __LINE__, (x), (y), (epsilon))) : 0)


#define unit_assert_throws(x, exception) \
    { \
        bool threw = false; \
        try { (x); } \
        catch (exception^) \
        { \
            threw = true; \
        } \
        if (!threw) \
            throw gcnew System::Exception(unit_assert_exception_message(__FILE__, __LINE__, #x, #exception)); \
    }


#define unit_assert_throws_what(x, exception, whatStr) \
    { \
        bool threw = false; \
        try { (x); } \
        catch (exception^ e) \
        { \
        if (e->Message == gcnew System::String(whatStr)) \
                threw = true; \
            else \
            throw gcnew System::Exception(unit_assert_exception_message(__FILE__, __LINE__, #x, System::String::Format("{0} {1}\nBut a different exception was thrown: {2}", gcnew System::String(#exception), gcnew System::String((whatStr)), e->Message))); \
        } \
        if (!threw) \
            throw gcnew System::Exception(unit_assert_exception_message(__FILE__, __LINE__, #x, System::String::Format("{0} {1}", gcnew System::String(#exception), gcnew System::String((whatStr))))); \
    }


//#define unit_assert_matrices_equal(A, B, epsilon) \
//    unit_assert(boost::numeric::ublas::norm_frobenius((A)-(B)) < (epsilon))


//#define unit_assert_vectors_equal(A, B, epsilon) \
//    unit_assert(boost::numeric::ublas::norm_2((A)-(B)) < (epsilon))


} // namespace util
} // namespace CLI
} // namespace pwiz


#endif // _UNIT_HPP_CLI_
