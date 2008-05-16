// (C) Copyright Jonathan Turkanis 2004
// Distributed under the Boost Software License, Version 1.0. (See accompanying
// file LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt.)

// See http://www.boost.org/libs/iostreams for documentation.

#include <vector>
#include <boost/config.hpp>
#include <boost/detail/workaround.hpp>
#include <boost/iostreams/device/array.hpp>
#include <boost/iostreams/device/array.hpp>
#include <boost/iostreams/filtering_stream.hpp>
#include <boost/range/iterator_range.hpp>
#include <boost/test/test_tools.hpp>
#include <boost/test/unit_test.hpp>
#include "detail/filters.hpp"
#include "detail/temp_file.hpp"
#include "detail/verification.hpp"

using namespace std;
using namespace boost;
using namespace boost::iostreams;
using namespace boost::iostreams::test;
using boost::unit_test::test_suite;  


// Code generation bugs cause tests to fail with global optimization.
#if BOOST_WORKAROUND(BOOST_MSVC, < 1300)
# pragma optimize("g", off)
#endif

void seekable_filter_test()
{
    {
        vector<char> test(data_reps * data_length(), '0');
        filtering_stream<seekable> io;
        io.push(identity_seekable_filter());
        io.push(make_iterator_range(test));
        io.exceptions(BOOST_IOS::failbit | BOOST_IOS::badbit);
        BOOST_CHECK_MESSAGE(
            test_seekable_in_chars(io),
            "failed seeking within a file, in chars"
        );
    }

    {
        vector<char> test(data_reps * data_length(), '0');
        filtering_stream<seekable> io;
        io.push(identity_seekable_filter());
        io.push(make_iterator_range(test));
        io.exceptions(BOOST_IOS::failbit | BOOST_IOS::badbit);
        BOOST_CHECK_MESSAGE(
            test_seekable_in_chunks(io),
            "failed seeking within a file, in chunks"
        );
    }

    {
        vector<char> test(data_reps * data_length(), '0');
        filtering_stream<seekable> io;
        io.push(identity_seekable_multichar_filter());
        io.push(make_iterator_range(test));
        io.exceptions(BOOST_IOS::failbit | BOOST_IOS::badbit);
        BOOST_CHECK_MESSAGE(
            test_seekable_in_chars(io),
            "failed seeking within a file, in chars"
        );
    }

    {
        vector<char> test(data_reps * data_length(), '0');
        filtering_stream<seekable> io;
        io.push(identity_seekable_multichar_filter());
        io.push(make_iterator_range(test));
        io.exceptions(BOOST_IOS::failbit | BOOST_IOS::badbit);
        BOOST_CHECK_MESSAGE(
            test_seekable_in_chunks(io),
            "failed seeking within a file, in chunks"
        );
    }
}

#if BOOST_WORKAROUND(BOOST_MSVC, < 1300)
# pragma optimize("", on)
#endif

test_suite* init_unit_test_suite(int, char* []) 
{
    test_suite* test = BOOST_TEST_SUITE("seekable filter test");
    test->add(BOOST_TEST_CASE(&seekable_filter_test));
    return test;
}
