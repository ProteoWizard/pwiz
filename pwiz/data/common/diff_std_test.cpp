//
// diff_std.hpp
//
//
// Original author: Robert Burke <robetr.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#define PWIZ_SOURCE

#include "diff_std.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <cstring>
#include <iostream>

using namespace std;
using namespace pwiz::util;
using namespace pwiz::diff_std;
using namespace pwiz::diff_std::diff_impl;

ostream* os_ = 0;

void testString()
{
    if (os_) *os_ << "testString()\n";

    Diff<string> diff("goober", "goober");
    unit_assert(diff.a_b.empty() && diff.b_a.empty());
    unit_assert(!diff);

    diff("goober", "goo");
    unit_assert(diff);
    // Doesn't mean as much without the TextWriter.
    // TextWriter excluded from diff_std for now.
    if (os_) *os_ << diff << endl;
}

void testNumeric()
{
    if (os_) *os_ << "testNumeric()\n";
    DiffConfig config;
    
    int a_i, b_i, a_b_i, b_a_i;

    a_i = 1;
    b_i = 1;
    a_b_i = 0;
    b_a_i = 0;
    diff_numeric(a_i , b_i, a_b_i, b_a_i, config);
    unit_assert(a_b_i == 0);
    unit_assert(b_a_i == 0);

    a_i = -1;
    diff_numeric(a_i , b_i, a_b_i, b_a_i, config);
    unit_assert(a_b_i == -1);
    unit_assert(b_a_i == 1);
    
    short a_s, b_s, a_b_s, b_a_s;

    a_s = 1;
    b_s = 1;
    a_b_s = 0;
    b_a_s = 0;
    diff_numeric(a_s , b_s, a_b_s, b_a_s, config);
    unit_assert(a_b_s == 0);
    unit_assert(b_a_s == 0);
    
    a_s = -1;
    diff_numeric(a_s , b_s, a_b_s, b_a_s, config);
    unit_assert(a_b_s == -1);
    unit_assert(b_a_s == 1);
    
    long a_l, b_l, a_b_l, b_a_l;
    
    a_l = 1;
    b_l = 1;
    a_b_l = 0;
    b_a_l = 0;
    diff_numeric(a_l , b_l, a_b_l, b_a_l, config);
    unit_assert(a_b_l == 0);
    unit_assert(b_a_l == 0);
    
    a_l = -1;
    diff_numeric(a_l , b_l, a_b_l, b_a_l, config);
    unit_assert(a_b_l == -1);
    unit_assert(b_a_l == 1);

    float a_f, b_f, a_b_f, b_a_f;

    a_f = 1;
    b_f = 1;
    a_b_f = 0;
    b_a_f = 0;
    diff_numeric(a_f , b_f, a_b_f, b_a_f, config);
    unit_assert(a_b_f == 0);
    unit_assert(b_a_f == 0);
    
    a_f = -1;
    diff_numeric(a_f , b_f, a_b_f, b_a_f, config);
    unit_assert(a_b_f == -1);
    unit_assert(b_a_f == 1);
    
    double a_d, b_d, a_b_d, b_a_d;

    a_d = 1.;
    b_d = 1.;
    a_b_d = 0.;
    b_a_d = 0.;
    diff_numeric(a_d , b_d, a_b_d, b_a_d, config);
    unit_assert_equal(a_b_d, 0., config.precision +
                      std::numeric_limits<double>::epsilon());
    unit_assert_equal(b_a_d, 0., config.precision +
                      std::numeric_limits<double>::epsilon());
    
    a_d = -1.;
    diff_numeric(a_d , b_d, a_b_d, b_a_d, config);

    unit_assert_equal(a_b_d, 2., config.precision +
                      std::numeric_limits<double>::epsilon());
    unit_assert_equal(b_a_d, 2., config.precision +
                      std::numeric_limits<double>::epsilon());
}

void test()
{
    testString();
    testNumeric();
}

int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}

