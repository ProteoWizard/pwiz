//
// $Id$
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


#include "Std.hpp"
#include "automation_vector.h"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz::util;


ostream* os_ = 0;


template<typename T> T defaultValue() {return numeric_limits<T>::max();}
template<> BSTR defaultValue() {return ::SysAllocString(L"abc");}

template<typename T> bool equals(const T& lhs, const T& rhs) {return lhs == rhs;}
template<> bool equals(const BSTR& lhs, const BSTR& rhs) {return !strcmp((const char*)lhs, (const char*)rhs);}

template<typename T> T& assign(T& lhs, const T& rhs) {return lhs = rhs;}
template<> BSTR& assign(BSTR& lhs, const BSTR& rhs) {lhs = ::SysAllocString((const OLECHAR*)rhs); return lhs;}


template<typename T>
void testInterface()
{
    typedef automation_vector<T> TestVector;
    T testValue(defaultValue<T>());

    // test std::vector-ish constructors and accessor methods
    {
        TestVector v;
        unit_assert(v.empty());
        unit_assert(v.size() == 0);

        v.resize(10);
        unit_assert(!v.empty());
        unit_assert(v.size() == 10);

        v.clear();
        unit_assert(v.empty());
        unit_assert(v.size() == 0);

        v.resize(1000000, testValue);
        unit_assert(!v.empty());
        unit_assert(v.size() == 1000000);
    }

    // test values returned by reference
    {
        TestVector v(10);
        unit_assert(!v.empty());
        unit_assert(v.size() == 10);

        for (size_t i=0; i < v.size(); ++i)
        {
            assign(v[i], testValue);
            unit_assert(equals(v[i], testValue));
        }

        // test out_of_range exception
        unit_assert_throws(v.at(v.size()), std::out_of_range);
    }

    // test iterators
    {
        TestVector v(10, testValue);
        unit_assert(!v.empty());
        unit_assert(v.size() == 10);
        unit_assert(equals(v.front(), testValue));
        unit_assert(equals(v.back(), testValue));
        unit_assert(equals(*v.begin(), v.front()));
        unit_assert(equals(*v.rbegin(), v.back()));

        for (size_t i=0; i < v.size(); ++i)
        {
            unit_assert(equals(v[i], testValue));
            unit_assert(equals(v.at(i), testValue));
        }

        for (TestVector::iterator itr = v.begin(); itr != v.end(); ++itr)
            unit_assert(equals(*itr, testValue));

        for (TestVector::const_iterator itr = v.begin(); itr != v.end(); ++itr)
            unit_assert(equals(*itr, testValue));

        for (TestVector::reverse_iterator itr = v.rbegin(); itr != v.rend(); ++itr)
            unit_assert(equals(*itr, testValue));

        for (TestVector::const_reverse_iterator itr = v.rbegin(); itr != v.rend(); ++itr)
            unit_assert(equals(*itr, testValue));
    }

    // test transfer of ownership from automation_vector to and from VARIANT
    VARIANT variant;
    ::VariantInit(&variant);
    {
        TestVector v(10, testValue);
        v.detach(variant); // swap with variant
        unit_assert(v.empty());
        unit_assert(variant.vt & VT_ARRAY);
        unit_assert(variant.parray != NULL);
        unit_assert(variant.parray->cLocks == 0); // lock count
        unit_assert(variant.parray->rgsabound->cElements == 10); // number of elements

        TestVector v2(variant, TestVector::COPY);
        unit_assert(v2.size() == 10);
        unit_assert(variant.parray != NULL);
        unit_assert(variant.parray->cLocks == 0); // lock count
        unit_assert(variant.parray->rgsabound->cElements == 10); // number of elements
        for (size_t i=0; i < v2.size(); ++i)
            unit_assert(equals(v2[i], testValue));
    }

    {
        TestVector v(variant, TestVector::MOVE); // swap with variant
        unit_assert(v.size() == 10);
        for (size_t i=0; i < v.size(); ++i)
            unit_assert(equals(v[i], testValue));
        ::VariantClear(&variant);
        v.detach(variant); // swap with variant
        unit_assert(v.empty());

        TestVector v2(variant, TestVector::COPY);
        for (size_t i=0; i < v2.size(); ++i)
            unit_assert(equals(v2[i], testValue));
    }

    {
        TestVector v;
        v.attach(variant); // swap with variant
        ::VariantClear(&variant);
        unit_assert(v.size() == 10);
        for (size_t i=0; i < v.size(); ++i)
            unit_assert(equals(v[i], testValue));
        v.detach(variant); // swap with variant
        unit_assert(v.empty());

        TestVector v2(variant, TestVector::COPY);
        for (size_t i=0; i < v2.size(); ++i)
            unit_assert(equals(v2[i], testValue));
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "automation_vector_test\n";
        testInterface<BSTR>();
        testInterface<signed char>();
        testInterface<signed short>();
        testInterface<signed int>();
        testInterface<signed long>();
        testInterface<unsigned char>();
        testInterface<unsigned short>();
        testInterface<unsigned int>();
        testInterface<unsigned long>();
        testInterface<float>();
        testInterface<double>();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}