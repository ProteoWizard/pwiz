// (C) Copyright Jonathan Turkanis 2004
// Distributed under the Boost Software License, Version 1.0. (See accompanying
// file LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt.)

// See http://www.boost.org/libs/iostreams for documentation.

#include <fstream>
#include <boost/iostreams/device/file.hpp>
#include <boost/iostreams/filtering_stream.hpp>
#include <boost/iostreams/tee.hpp>
#include <boost/test/test_tools.hpp>
#include <boost/test/unit_test.hpp>
#include "detail/temp_file.hpp"
#include "detail/verification.hpp"

using namespace std;
using namespace boost;
using namespace boost::iostreams;
using namespace boost::iostreams::test;
using boost::unit_test::test_suite;  

void tee_test()
{
    {
        temp_file          dest1;
        temp_file          dest2;
        filtering_ostream  out;
        out.push(tee(file_sink(dest1.name(), out_mode)));
        out.push(file_sink(dest2.name(), out_mode));
        write_data_in_chars(out);
        out.reset();
        BOOST_CHECK_MESSAGE(
            compare_files(dest1.name(), dest2.name()),
            "failed writing to a tee_filter in chars"
        );
    }

    {
        temp_file          dest1;
        temp_file          dest2;
        filtering_ostream  out;
        out.push(tee(file_sink(dest1.name(), out_mode)));
        out.push(file_sink(dest2.name(), out_mode));
        write_data_in_chunks(out);
        out.reset();
        BOOST_CHECK_MESSAGE(
            compare_files(dest1.name(), dest2.name()),
            "failed writing to a tee_filter in chunks"
        );
    }

    {
        temp_file          dest1;
        temp_file          dest2;
        filtering_ostream  out;
        out.push( tee( file_sink(dest1.name(), out_mode),
                       file_sink(dest2.name(), out_mode) ) );
        write_data_in_chars(out);
        out.reset();
        BOOST_CHECK_MESSAGE(
            compare_files(dest1.name(), dest2.name()),
            "failed writing to a tee_device in chars"
        );
    }

    {
        temp_file          dest1;
        temp_file          dest2;
        filtering_ostream  out;
        out.push( tee( file_sink(dest1.name(), out_mode),
                       file_sink(dest2.name(), out_mode) ) );
        write_data_in_chunks(out);
        out.reset();
        BOOST_CHECK_MESSAGE(
            compare_files(dest1.name(), dest2.name()),
            "failed writing to a tee_device in chunks"
        );
    }
}

test_suite* init_unit_test_suite(int, char* []) 
{
    test_suite* test = BOOST_TEST_SUITE("tee test");
    test->add(BOOST_TEST_CASE(&tee_test));
    return test;
}
