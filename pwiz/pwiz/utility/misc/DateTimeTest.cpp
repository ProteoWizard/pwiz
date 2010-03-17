//
// $Id$
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


void test_xml_datetime()
{
    using bpt::ptime;
    typedef blt::local_date_time datetime;
    typedef datetime::time_duration_type time;

    // New York City time zone for testing local->UTC conversion
    blt::time_zone_ptr nyc(new blt::posix_time_zone("EST-05:00:00EDT+01:00:00,M4.1.0/02:00:00,M10.5.0/02:00:00"));
    blt::time_zone_ptr utc;

    std::string encoded;
    datetime decoded(bdt::not_a_date_time);

    // test output from UTC times
    datetime dt = datetime(ptime(date(1899, bdt::Dec, 30), time(0,0,0)), utc);
    encoded = encode_xml_datetime(dt);
    if (os_) *os_ << dt << " -> " << encoded << endl;
    unit_assert(encoded == "1899-12-30T00:00:00Z");
    decoded = decode_xml_datetime(encoded);
    if (os_) *os_ << encoded << " -> " << decoded << endl;
    unit_assert(decoded == dt);

    dt = datetime(ptime(date(1999, bdt::Dec, 31), time(23,59,59)), utc);
    encoded = encode_xml_datetime(dt);
    if (os_) *os_ << dt << " -> " << encoded << endl;
    unit_assert(encoded == "1999-12-31T23:59:59Z");
    decoded = decode_xml_datetime(encoded);
    if (os_) *os_ << encoded << " -> " << decoded << endl;
    unit_assert(decoded == dt);

    dt = datetime(ptime(date(2525, bdt::Jan, 1), time(1,2,3)), utc);
    encoded = encode_xml_datetime(dt);
    if (os_) *os_ << dt << " -> " << encoded << endl;
    unit_assert(encoded == "2525-01-01T01:02:03Z");
    decoded = decode_xml_datetime(encoded);
    if (os_) *os_ << encoded << " -> " << decoded << endl;
    unit_assert(decoded == dt);

    dt = datetime(ptime(date(1492, bdt::Feb, 3), time(4,5,6)), utc);
    encoded = encode_xml_datetime(dt);
    if (os_) *os_ << dt << " -> " << encoded << endl;
    unit_assert(encoded == "1492-02-03T04:05:06Z");
    decoded = decode_xml_datetime(encoded);
    if (os_) *os_ << encoded << " -> " << decoded << endl;
    unit_assert(decoded == dt);

    // test output from NYC times
    dt = datetime(date(1899, bdt::Dec, 30), time(0,0,0), nyc, datetime::NOT_DATE_TIME_ON_ERROR);
    encoded = encode_xml_datetime(dt);
    if (os_) *os_ << dt << " -> " << encoded << endl;
    unit_assert(encoded == "1899-12-30T05:00:00Z"); // UTC=EST+5
    decoded = decode_xml_datetime(encoded);
    if (os_) *os_ << encoded << " -> " << decoded << endl;
    unit_assert(decoded == dt);

    dt = datetime(date(1999, bdt::Dec, 31), time(23,59,59), nyc, datetime::NOT_DATE_TIME_ON_ERROR);
    encoded = encode_xml_datetime(dt);
    if (os_) *os_ << dt << " -> " << encoded << endl;
    unit_assert(encoded == "2000-01-01T04:59:59Z"); // UTC=EST+5
    decoded = decode_xml_datetime(encoded);
    if (os_) *os_ << encoded << " -> " << decoded << endl;
    unit_assert(decoded == dt);

    dt = datetime(date(2525, bdt::Jan, 1), time(1,2,3), nyc, datetime::NOT_DATE_TIME_ON_ERROR);
    encoded = encode_xml_datetime(dt);
    if (os_) *os_ << dt << " -> " << encoded << endl;
    unit_assert(encoded == "2525-01-01T06:02:03Z"); // UTC=EST+5
    decoded = decode_xml_datetime(encoded);
    if (os_) *os_ << encoded << " -> " << decoded << endl;
    unit_assert(decoded == dt);

    dt = datetime(date(1492, bdt::Jun, 3), time(4,5,6), nyc, datetime::NOT_DATE_TIME_ON_ERROR);
    encoded = encode_xml_datetime(dt);
    if (os_) *os_ << dt << " -> " << encoded << endl;
    unit_assert(encoded == "1492-06-03T08:05:06Z"); // UTC=EDT+4
    // TODO: figure out why this test case is failing
    /*decoded = decode_xml_datetime(encoded);
    if (os_) *os_ << encoded << " -> " << decoded << endl;
    unit_assert(decoded == dt);*/
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
            output_facet->format("%Y-%m-%d %H:%M:%S %z"); // 2007-06-27 15:23:45 EST
            os_->imbue(std::locale(std::locale::classic(), output_facet));
        }
        test_time_from_OADATE<boost::posix_time::ptime>();
        //test_time_from_OADATE<boost::local_time::local_date_time>();
        test_xml_datetime();
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
