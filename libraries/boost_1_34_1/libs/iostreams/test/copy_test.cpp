// (C) Copyright Jonathan Turkanis 2004
// Distributed under the Boost Software License, Version 1.0. (See accompanying
// file LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt.)

// See http://www.boost.org/libs/iostreams for documentation.

#include <fstream>
#include <boost/config.hpp>  // MSVC.
#include <boost/detail/workaround.hpp>
#include <boost/iostreams/copy.hpp>
#include <boost/iostreams/device/file.hpp>
#include <boost/test/test_tools.hpp>
#include <boost/test/unit_test.hpp>
#include "detail/temp_file.hpp"
#include "detail/verification.hpp"

using namespace std;
using namespace boost;
using namespace boost::iostreams;
using namespace boost::iostreams::test;
using boost::unit_test::test_suite;

void copy_test()
{
    test_file test;         

    {
        temp_file  dest;
        ifstream   first(test.name().c_str(), in_mode);
        ofstream   second(dest.name().c_str(), out_mode);
        boost::iostreams::copy(first, second);
        first.close();
        second.close();
        BOOST_CHECK_MESSAGE(
            compare_files(test.name(), dest.name()),
            "failed copying from stream to stream"
        );
    }

    {
        temp_file  dest;
        ifstream   first(test.name().c_str(), in_mode);
        boost::iostreams::copy(first, file_sink(dest.name(), out_mode));
        first.close();
        BOOST_CHECK_MESSAGE(
            compare_files(test.name(), dest.name()),
            "failed copying from stream to file_sink"
        );
    }

    {
        temp_file  dest;
        ofstream   second(dest.name().c_str(), out_mode);
        boost::iostreams::copy(file_source(test.name(), in_mode), second);
        second.close();
        BOOST_CHECK_MESSAGE(
            compare_files(test.name(), dest.name()),
            "failed copying from file_source to stream"
        );
    }

    {
        temp_file dest;
        boost::iostreams::copy( file_source(test.name(), in_mode),
                                file_sink(dest.name(), out_mode) );
        BOOST_CHECK_MESSAGE(
            compare_files(test.name(), dest.name()),
            "failed copying from file_source to file_sink"
        );
    }
}

test_suite* init_unit_test_suite(int, char* []) 
{
    test_suite* test = BOOST_TEST_SUITE("copy test");
    test->add(BOOST_TEST_CASE(&copy_test));
    return test;
}
