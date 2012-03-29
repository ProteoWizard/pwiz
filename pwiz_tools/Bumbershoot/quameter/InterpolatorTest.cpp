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
#include "Interpolator.hpp"
#include <boost/assign.hpp>

using namespace pwiz::util;
using namespace freicore;
using namespace boost::assign;

int main()
{
    try
    {
        {
            vector<double> x; x += 1, 2, 3,  4,  6,  7,  8, 9;
            vector<double> y; y += 1, 8, 27, 64, 64, 27, 8, 1;
            Interpolator test(x, y);

            unit_assert_equal(1, test.interpolate(x, y, 1), 1e-6);
            unit_assert_equal(8, test.interpolate(x, y, 2), 1e-6);
            unit_assert_equal(27, test.interpolate(x, y, 3), 1e-6);
            unit_assert_equal(64, test.interpolate(x, y, 4), 1e-6);
            //unit_assert_equal(64, test.interpolate(x, y, 5), 1e-6);
            unit_assert_equal(64, test.interpolate(x, y, 6), 1e-6);
            unit_assert_equal(27, test.interpolate(x, y, 7), 1e-6);
            unit_assert_equal(1, test.interpolate(x, y, 9), 1e-6);

            test.resample(x, y);
            unit_assert_operator_equal(18, x.size() + y.size());
            unit_assert_equal(1, x[0], 1e-6);
            unit_assert_equal(2, x[1], 1e-6);
            unit_assert_equal(8, x[7], 1e-6);
            unit_assert_equal(9, x[8], 1e-6);

            unit_assert_equal(1, y[0], 1e-6);
            unit_assert_equal(8, y[1], 1e-6);
            unit_assert_equal(27, y[2], 1e-6);
            unit_assert_equal(64, y[3], 1e-6);
            //unit_assert_equal(64, y[4], 1e-6);
            unit_assert_equal(64, y[5], 1e-6);
            unit_assert_equal(27, y[6], 1e-6);
            //unit_assert_equal(8, y[7], 1e-6);
            unit_assert_equal(1, y[8], 1e-6);
        }

        {
            vector<double> x; x += 1, 3,  4,  5,  6,  7,  9;
            vector<double> y; y += 1, 27, 64, 64, 64, 27, 1;
            Interpolator test(x, y);

            unit_assert_operator_equal(1, test.interpolate(x, y, 1));
            unit_assert_operator_equal(8, test.interpolate(x, y, 2));
            unit_assert_operator_equal(64, test.interpolate(x, y, 5));
            unit_assert_operator_equal(27, test.interpolate(x, y, 7));
            unit_assert_operator_equal(8, test.interpolate(x, y, 8));
            unit_assert_operator_equal(1, test.interpolate(x, y, 9));

            test.resample(x, y);
            unit_assert_operator_equal(18, x.size() + y.size());
            unit_assert_operator_equal(1, x[0]);
            unit_assert_operator_equal(2, x[1]);
            unit_assert_operator_equal(8, x[7]);
            unit_assert_operator_equal(9, x[8]);

            unit_assert_operator_equal(1, y[0]);
            unit_assert_operator_equal(8, y[1]);
            unit_assert_operator_equal(27, y[2]);
            unit_assert_operator_equal(64, y[3]);
            unit_assert_operator_equal(64, y[4]);
            unit_assert_operator_equal(64, y[5]);
            unit_assert_operator_equal(27, y[6]);
            unit_assert_operator_equal(8, y[7]);
            unit_assert_operator_equal(1, y[8]);
        }
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }

    return 0;
}
