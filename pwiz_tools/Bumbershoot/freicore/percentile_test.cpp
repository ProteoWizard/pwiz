//
// $Id$ 
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
// Copyright 2011 Vanderbilt University
//
// Licensed under the Code Project Open License, Version 1.02 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.codeproject.com/info/cpol10.aspx
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/accumulators/statistics/stats.hpp>
#include <boost/accumulators/framework/accumulator_set.hpp>
#include "percentile.hpp"

using namespace pwiz::util;
using namespace boost::accumulators;

void test()
{
    // tested at http://www.wessa.net

    accumulator_set<double, stats<tag::percentile> > acc;
    const double epsilon = 1e-6;

    acc(10);
    unit_assert_equal(10, percentile(acc, percentile_number = 25), epsilon);
    unit_assert_equal(10, percentile(acc, percentile_number = 50), epsilon);
    unit_assert_equal(10, percentile(acc, percentile_number = 75), epsilon);

    acc(30);
    unit_assert_equal(15, percentile(acc, percentile_number = 25), epsilon);
    unit_assert_equal(20, percentile(acc, percentile_number = 50), epsilon);
    unit_assert_equal(25, percentile(acc, percentile_number = 75), epsilon);

    acc(20);
    unit_assert_equal(15, percentile(acc, percentile_number = 25), epsilon);
    unit_assert_equal(20, percentile(acc, percentile_number = 50), epsilon);
    unit_assert_equal(25, percentile(acc, percentile_number = 75), epsilon);

    acc(40);
    unit_assert_equal(17.5, percentile(acc, percentile_number = 25), epsilon);
    unit_assert_equal(25, percentile(acc, percentile_number = 50), epsilon);
    unit_assert_equal(32.5, percentile(acc, percentile_number = 75), epsilon);

    acc(50);
    unit_assert_equal(20, percentile(acc, percentile_number = 25), epsilon);
    unit_assert_equal(30, percentile(acc, percentile_number = 50), epsilon);
    unit_assert_equal(40, percentile(acc, percentile_number = 75), epsilon);

    acc(60);
    unit_assert_equal(22.5, percentile(acc, percentile_number = 25), epsilon);
    unit_assert_equal(35, percentile(acc, percentile_number = 50), epsilon);
    unit_assert_equal(47.5, percentile(acc, percentile_number = 75), epsilon);

    acc(80);
    unit_assert_equal(25, percentile(acc, percentile_number = 25), epsilon);
    unit_assert_equal(40, percentile(acc, percentile_number = 50), epsilon);
    unit_assert_equal(55, percentile(acc, percentile_number = 75), epsilon);

    acc(35);
    unit_assert_equal(27.5, percentile(acc, percentile_number = 25), epsilon);
    unit_assert_equal(37.5, percentile(acc, percentile_number = 50), epsilon);
    unit_assert_equal(52.5, percentile(acc, percentile_number = 75), epsilon);

    acc(77); acc(88); acc(99); acc(100);
    unit_assert_equal(21, percentile(acc, percentile_number = 10), epsilon);
    unit_assert_equal(31, percentile(acc, percentile_number = 20), epsilon);
    unit_assert_equal(36.5, percentile(acc, percentile_number = 30), epsilon);
    unit_assert_equal(44, percentile(acc, percentile_number = 40), epsilon);
    unit_assert_equal(55, percentile(acc, percentile_number = 50), epsilon);
    unit_assert_equal(70.2, percentile(acc, percentile_number = 60), epsilon);
    unit_assert_equal(79.1, percentile(acc, percentile_number = 70), epsilon);
    unit_assert_equal(86.4, percentile(acc, percentile_number = 80), epsilon);
    unit_assert_equal(97.9, percentile(acc, percentile_number = 90), epsilon);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        test();
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
