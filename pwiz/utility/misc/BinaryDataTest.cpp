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
#include "BinaryData.hpp"
#include "sort_together.hpp"
#include "pwiz/utility/misc/unit.hpp"

#ifdef __cplusplus_cli
#include <gcroot.h>
#include <vcclr.h>
typedef System::Runtime::InteropServices::GCHandle GCHandle;
#endif

using namespace pwiz::util;


ostream* os_ = 0;


template<typename T> T defaultValue() { return numeric_limits<T>::max(); }
template<typename T> bool equals(const T& lhs, const T& rhs) { return lhs == rhs; }


template <typename T>
void test()
{
    typedef BinaryData<T> TestVector;
    T testValue(defaultValue<T>());

    // test std::vector-ish constructors and accessor methods
    {
        TestVector v;
        unit_assert_operator_equal(INT_MAX / sizeof(T), v.max_size());

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

        for (size_t i = 0; i < v.size(); ++i)
        {
            v[i] = testValue;
            unit_assert(equals(v[i], testValue));
        }

        // test out_of_range exception
        unit_assert_throws(v.at(v.size()), std::out_of_range);
    }

    // test reserve
    {
        TestVector v;
        v.reserve(10);
        unit_assert(v.empty());
        unit_assert_operator_equal(0, v.size());
        unit_assert_operator_equal(10, v.capacity());
    }

    // test iterators on empty data
    {
        TestVector v;
        unit_assert(v.empty());
        unit_assert_operator_equal(0, v.size());
        unit_assert(v.begin() == v.end());
    }

    // test iterators
    {
        TestVector v(10, testValue);
        unit_assert(!v.empty());
        unit_assert_operator_equal(10, v.size());
        unit_assert(equals(v.front(), testValue));
        unit_assert(equals(v.back(), testValue));
        unit_assert(equals(*v.begin(), v.front()));
        unit_assert(equals(*v.rbegin(), v.back()));

        for (size_t i = 0; i < v.size(); ++i)
        {
            unit_assert(equals(v[i], testValue));
            unit_assert(equals(v.at(i), testValue));
        }

        for (typename TestVector::iterator itr = v.begin(); itr != v.end(); ++itr)
            unit_assert(equals(*itr, testValue));

        for (typename TestVector::const_iterator itr = v.cbegin(); itr != v.cend(); ++itr)
            unit_assert(equals(*itr, testValue));

        for (typename TestVector::reverse_iterator itr = v.rbegin(); itr != v.rend(); ++itr)
            unit_assert(equals(*itr, testValue));

        for (typename TestVector::const_reverse_iterator itr = v.crbegin(); itr != v.crend(); ++itr)
            unit_assert(equals(*itr, testValue));
    }

    // test swap with std::vector<T>
    {
        std::vector<T> vStd(10, testValue);

        unit_assert(!vStd.empty());
        unit_assert(equals(vStd.front(), testValue));

        TestVector v;
        unit_assert(v.empty());

        std::swap(vStd, v);

        unit_assert(vStd.empty());
        unit_assert(!v.empty());
        unit_assert_operator_equal(10, v.size());
        unit_assert(equals(v.front(), testValue));
        unit_assert(equals(v.back(), testValue));
        unit_assert(equals(*v.begin(), v.front()));
        unit_assert(equals(*v.rbegin(), v.back()));

        std::swap(vStd, v);

        unit_assert(v.empty());

        unit_assert(!vStd.empty());
        unit_assert_operator_equal(10, vStd.size());
        unit_assert(equals(vStd.front(), testValue));
        unit_assert(equals(vStd.back(), testValue));
    }

    // test implicit cast to std::vector<T>
    {
        TestVector v(10, testValue);
        std::vector<T> vStd = v;

        unit_assert(!vStd.empty());
        unit_assert_operator_equal(10, vStd.size());
        unit_assert(equals(vStd.front(), testValue));
        unit_assert(equals(vStd.back(), testValue));
        unit_assert(equals(*vStd.begin(), vStd.front()));
        unit_assert(equals(*vStd.rbegin(), vStd.back()));
    }

    // test implicit cast to std::vector<T>& (not supported with iterator caching)
    /*{
        TestVector v(10, testValue);
        std::vector<T>& vStd = v;

        unit_assert(!vStd.empty());
        unit_assert_operator_equal(10, vStd.size());
        unit_assert(equals(vStd.front(), testValue));
        unit_assert(equals(vStd.back(), testValue));
        unit_assert(equals(*vStd.begin(), vStd.front()));
        unit_assert(equals(*vStd.rbegin(), vStd.back()));
    }*/

    // test implicit cast to const std::vector<T>&
    {
        const TestVector v(10, testValue);
        const std::vector<T>& vStd = v;

        unit_assert(!vStd.empty());
        unit_assert_operator_equal(10, vStd.size());
        unit_assert(equals(vStd.front(), testValue));
        unit_assert(equals(vStd.back(), testValue));
        unit_assert(equals(*vStd.begin(), vStd.front()));
        unit_assert(equals(*vStd.rbegin(), vStd.back()));
    }

    // test sort_together
    {
        std::vector<T> expectedV1{ 3,  2,  5,  7,  1,  8,  6,  4 };
        std::vector<T> expectedV2{ 5, 10, 15, 15, 20, 20, 30, 50 };

        {
            std::vector<T> v1 = {  1,  2, 3,  4,  5,  6,  7,  8 };
            std::vector<T> v2 = { 20, 10, 5, 50, 15, 30, 15, 20 };

            vector<boost::iterator_range<typename std::vector<T>::iterator>> cov = { v1 };
            pwiz::util::sort_together(v2, cov.begin(), cov.end());

            for (size_t i = 0; i < v1.size(); ++i)
            {
                unit_assert_operator_equal(expectedV1[i], v1[i]);
                unit_assert_operator_equal(expectedV2[i], v2[i]);
            }
        }

        {
            std::vector<T> v1 = { 1,  2, 3,  4,  5,  6,  7,  8 };
            std::vector<T> v2 = { 20, 10, 5, 50, 15, 30, 15, 20 };

            pwiz::util::sort_together(v2, v1);

            for (size_t i = 0; i < v1.size(); ++i)
            {
                unit_assert_operator_equal(expectedV1[i], v1[i]);
                unit_assert_operator_equal(expectedV2[i], v2[i]);
            }
        }

        {
            std::vector<T> v1 = {  1,  2, 3,  4,  5,  6,  7,  8 };
            std::vector<T> v2 = { 20, 10, 5, 50, 15, 30, 15, 20 };
            std::vector<T> v3 = {  6,  5, 4,  3,  2,  1,  0, -1 };

            vector<boost::iterator_range<typename std::vector<T>::iterator>> cov = { v1, v3 };
            pwiz::util::sort_together(v2, cov.begin(), cov.end());

            std::vector<T> expectedV3{ 4, 5, 2, 0, 6, -1, 1, 3 };
            for (size_t i = 0; i < v1.size(); ++i)
            {
                unit_assert_operator_equal(expectedV1[i], v1[i]);
                unit_assert_operator_equal(expectedV2[i], v2[i]);
                unit_assert_operator_equal(expectedV3[i], v3[i]);
            }
        }

        {
            std::vector<T> v1 = {  1,  2, 3,  4,  5,  6,  7,  8 };
            std::vector<T> v2 = { 20, 10, 5, 50, 15, 30, 15, 20 };
            std::vector<T> v3 = {  6,  5, 4,  3,  2,  1,  0, -1 };

            vector<boost::iterator_range<typename std::vector<T>::iterator>> cov = { v1, v3 };
            pwiz::util::sort_together(v2, cov);

            std::vector<T> expectedV3{ 4, 5, 2, 0, 6, -1, 1, 3 };
            for (size_t i = 0; i < v1.size(); ++i)
            {
                unit_assert_operator_equal(expectedV1[i], v1[i]);
                unit_assert_operator_equal(expectedV2[i], v2[i]);
                unit_assert_operator_equal(expectedV3[i], v3[i]);
            }
        }
    }

#ifdef __cplusplus_cli
    // test swap with std::vector<T> with managed array
    {
        std::vector<T> vStd(10, testValue);

        unit_assert(!vStd.empty());
        unit_assert(equals(vStd.front(), testValue));

        GCHandle handle = GCHandle::Alloc(gcnew cli::array<T> { (T) 1.23, (T) 2.34, (T) 3.45, (T) 4.56 });
        TestVector v = ((System::IntPtr)handle).ToPointer();
        handle.Free();
        unit_assert(!v.empty());
        unit_assert_operator_equal(4, v.size());

        std::swap(vStd, v);

        unit_assert(!v.empty());
        unit_assert_operator_equal(10, v.size());
        unit_assert(equals(v.front(), testValue));
        unit_assert_operator_equal(testValue, v.back());
        unit_assert(equals(*v.begin(), v.front()));
        unit_assert(equals(*v.rbegin(), v.back()));

        unit_assert(!vStd.empty());
        unit_assert_operator_equal(4, vStd.size());

        std::swap(vStd, v);

        unit_assert(!v.empty());
        unit_assert_operator_equal(4, v.size());

        unit_assert(!vStd.empty());
        unit_assert_operator_equal(10, vStd.size());
        unit_assert(equals(vStd.front(), testValue));
        unit_assert(equals(vStd.back(), testValue));
    }

    // test implicit cast to const std::vector<T>& with managed array and then a swap
    {
        GCHandle handle = GCHandle::Alloc(gcnew cli::array<T> { (T) 1.23, (T) 2.34, (T) 3.45, (T) 4.56 });
        TestVector v = ((System::IntPtr)handle).ToPointer();
        handle.Free();

        {
            const std::vector<T>& vStd2 = v;

            unit_assert(!vStd2.empty());
            unit_assert_operator_equal(4, vStd2.size());
            unit_assert(equals(vStd2.front(), v.front()));
            unit_assert(equals(vStd2.back(), v.back()));
            unit_assert(equals(*vStd2.begin(), vStd2.front()));
            unit_assert(equals(*vStd2.rbegin(), vStd2.back()));
        }

        std::vector<T> vStd(10, testValue);
        std::swap(vStd, v);

        unit_assert(!v.empty());
        unit_assert_operator_equal(10, v.size());
        unit_assert(equals(v.front(), testValue));
        unit_assert(equals(v.back(), testValue));
        unit_assert(equals(*v.begin(), v.front()));
        unit_assert(equals(*v.rbegin(), v.back()));

        {
            const std::vector<T>& vStd2 = v;

            unit_assert(!vStd2.empty());
            unit_assert_operator_equal(10, vStd2.size());
            unit_assert(equals(v.front(), vStd2.front()));
            unit_assert(equals(v.back(), vStd2.back()));
            unit_assert(equals(*vStd2.begin(), vStd2.front()));
            unit_assert(equals(*vStd2.rbegin(), vStd2.back()));
        }
    }
#endif
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1], "-v")) os_ = &cout;
        if (os_) *os_ << "BinaryDataTest\n";
        test<double>();
        test<float>();
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