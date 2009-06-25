//
// DateTimeTest.cpp
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
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

#include "DateTime.hpp"
#include "Stream.hpp"
#include "Exception.hpp"
#include "pwiz/utility/misc/unit.hpp"


using namespace pwiz::util;
using bdt::time_from_OADATE;


ostream* os_ = 0;


template<typename time_type>
void test_time_from_OADATE()
{
    typedef typename time_type::date_type date_type;
    typedef typename time_type::date_duration_type date_duration_type;
    typedef typename time_type::time_duration_type time_duration_type;

    if (os_) *os_ << "OADATE: 0.0 -> " << time_from_OADATE<time_type>(0.0) << endl;
    unit_assert(time_from_OADATE<time_type>(0.0) == time_type(date_type(1899, bdt::Dec, 30), time_duration_type(0,0,0)));

    if (os_) *os_ << "OADATE: 1.0 -> " << time_from_OADATE<time_type>(1.0) << endl;
    unit_assert(time_from_OADATE<time_type>(1.0) == time_type(date_type(1899, bdt::Dec, 31), time_duration_type(0,0,0)));

    if (os_) *os_ << "OADATE: -1.0 -> " << time_from_OADATE<time_type>(-1.0) << endl;
    unit_assert(time_from_OADATE<time_type>(-1.0) == time_type(date_type(1899, bdt::Dec, 29), time_duration_type(0,0,0)));

    if (os_) *os_ << "OADATE: 2.0 -> " << time_from_OADATE<time_type>(2.0) << endl;
    unit_assert(time_from_OADATE<time_type>(2.0) == time_type(date_type(1900, bdt::Jan, 1), time_duration_type(0,0,0)));

    if (os_) *os_ << "OADATE: 2.25 -> " << time_from_OADATE<time_type>(2.25) << endl;
    unit_assert(time_from_OADATE<time_type>(2.25) == time_type(date_type(1900, bdt::Jan, 1), time_duration_type(6,0,0)));

    if (os_) *os_ << "OADATE: -1.25 -> " << time_from_OADATE<time_type>(-1.25) << endl;
    unit_assert(time_from_OADATE<time_type>(-1.25) == time_type(date_type(1899, bdt::Dec, 29), time_duration_type(6,0,0)));
}


void test_encode_xml_datetime()
{
    using bpt::ptime;
    typedef blt::local_date_time datetime;
    typedef datetime::time_duration_type time;

    std::string encoded;

    encoded = encode_xml_datetime(datetime(ptime(date(1899, bdt::Dec, 30), time(0,0,0)), blt::time_zone_ptr()));
    if (os_) *os_ << encoded << endl;
    unit_assert(encoded == "1899-12-30T00:00:00");

    encoded = encode_xml_datetime(datetime(ptime(date(1999, bdt::Dec, 31), time(23,59,59)), blt::time_zone_ptr()));
    if (os_) *os_ << encoded << endl;
    unit_assert(encoded == "1999-12-31T23:59:59");

    encoded = encode_xml_datetime(datetime(ptime(date(2525, bdt::Jan, 1), time(1,2,3)), blt::time_zone_ptr()));
    if (os_) *os_ << encoded << endl;
    unit_assert(encoded == "2525-01-01T01:02:03");

    encoded = encode_xml_datetime(datetime(ptime(date(1492, bdt::Feb, 3), time(4,5,6)), blt::time_zone_ptr()));
    if (os_) *os_ << encoded << endl;
    unit_assert(encoded == "1492-02-03T04:05:06");
}


int main(int argc, const char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_)
        {
            using namespace boost::local_time;
            *os_ << "DateTimeTest\n";
            local_time_facet* output_facet = new local_time_facet;
            output_facet->format("%Y-%m-%d %H:%M:%S"); // 2007-06-27 15:23:45
            os_->imbue(std::locale(std::locale::classic(), output_facet));
        }
        test_time_from_OADATE<boost::posix_time::ptime>();
        //test_time_from_OADATE<boost::local_time::local_date_time>();
        test_encode_xml_datetime();
        return 0;
    }
    catch (exception& e)
    {
        cerr << "Caught exception: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception" << endl;
    }

    return 1;
}
